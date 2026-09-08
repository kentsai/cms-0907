using System.Text;
using CMS.API.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

/// <summary>
/// Supplies the key(s) the JWT bearer middleware validates token signatures with. The key is
/// <c>SysConfig.appConfig.symmetricSecurityKey</c> — the same secret <see cref="JwtTokenIssuer"/> signs with.
/// </summary>
public interface ISigningKeyCache
{
    /// <summary>Keys to validate with. Empty until the first successful load, or after the configuration became invalid.</summary>
    IReadOnlyList<SecurityKey> CurrentKeys { get; }

    /// <summary>
    /// Re-reads the key from SysConfig when the cached copy is older than <see cref="SigningKeyCache.CacheDuration"/>.
    /// Never throws (other than cancellation): a failure is logged and the last good key is kept, so a transient
    /// database error does not invalidate every session; an <see cref="AppConfigException"/> clears the keys instead.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Process-wide cache of the token-validation key. The bearer handler calls <see cref="RefreshAsync"/> (async, from
/// <c>OnMessageReceived</c>) before validation, so the synchronous <c>IssuerSigningKeyResolver</c> only ever reads
/// <see cref="CurrentKeys"/>. A rotated key therefore takes effect within <see cref="CacheDuration"/>, no restart needed.
/// </summary>
public sealed class SigningKeyCache(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SigningKeyCache> logger) : ISigningKeyCache
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile SecurityKey[] _keys = [];
    private long _loadedAtUtcTicks;

    public IReadOnlyList<SecurityKey> CurrentKeys => _keys;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return;
            }

            // IAuthRepository is scoped; this cache is a singleton, so resolve it from a scope of its own.
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();

            var secret = await repository.GetSymmetricSecurityKeyAsync(cancellationToken);
            _keys = [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))];
            Volatile.Write(ref _loadedAtUtcTicks, timeProvider.GetUtcNow().UtcTicks);
        }
        catch (AppConfigException ex)
        {
            // The configuration itself is missing or invalid: nothing can be validated until it is fixed.
            _keys = [];
            logger.LogError(ex, "無法載入 JWT 驗證金鑰（SysConfig appConfig.symmetricSecurityKey）；所有受保護的請求將回應 401。");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Transient (typically the database): keep the last known key and try again on the next request.
            logger.LogError(ex, "讀取 JWT 驗證金鑰失敗，沿用上次載入的金鑰。");
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh()
    {
        var loadedAt = Volatile.Read(ref _loadedAtUtcTicks);
        return _keys.Length > 0 && timeProvider.GetUtcNow().UtcTicks - loadedAt < CacheDuration.Ticks;
    }
}
