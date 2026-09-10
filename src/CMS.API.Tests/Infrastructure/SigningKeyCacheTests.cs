using System.Text;
using CMS.API.Infrastructure;
using CMS.API.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace CMS.API.Tests.Infrastructure;

public class SigningKeyCacheTests
{
    private const string FirstKey = "first-symmetric-security-key-0123456789abcdef";
    private const string RotatedKey = "rotated-symmetric-security-key-0123456789abc";

    private readonly Mock<IAuthRepository> _repository = new(MockBehavior.Strict);
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero));
    private readonly SigningKeyCache _cache;

    public SigningKeyCacheTests()
    {
        // The cache resolves the scoped repository through a scope of its own; a tiny container reproduces that.
        var services = new ServiceCollection().AddScoped(_ => _repository.Object).BuildServiceProvider();
        _cache = new SigningKeyCache(services.GetRequiredService<IServiceScopeFactory>(), _clock, NullLogger<SigningKeyCache>.Instance);
    }

    private void KeyIs(string key) =>
        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(key);

    private static byte[] Bytes(SecurityKey key) => Assert.IsType<SymmetricSecurityKey>(key).Key;

    [Fact]
    public void CurrentKeys_IsEmpty_BeforeTheFirstRefresh()
    {
        Assert.Empty(_cache.CurrentKeys);
    }

    [Fact]
    public async Task RefreshAsync_LoadsTheSysConfigKey()
    {
        KeyIs(FirstKey);

        await _cache.RefreshAsync(CancellationToken.None);

        Assert.Equal(Encoding.UTF8.GetBytes(FirstKey), Bytes(Assert.Single(_cache.CurrentKeys)));
    }

    [Fact]
    public async Task RefreshAsync_DoesNotReReadWithinCacheDuration()
    {
        KeyIs(FirstKey);

        await _cache.RefreshAsync(CancellationToken.None);
        _clock.Advance(SigningKeyCache.CacheDuration - TimeSpan.FromSeconds(1));
        await _cache.RefreshAsync(CancellationToken.None);

        _repository.Verify(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_PicksUpARotatedKey_AfterCacheDuration()
    {
        KeyIs(FirstKey);
        await _cache.RefreshAsync(CancellationToken.None);

        KeyIs(RotatedKey);
        _clock.Advance(SigningKeyCache.CacheDuration);
        await _cache.RefreshAsync(CancellationToken.None);

        Assert.Equal(Encoding.UTF8.GetBytes(RotatedKey), Bytes(Assert.Single(_cache.CurrentKeys)));
        _repository.Verify(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshAsync_KeepsTheLastKnownKey_WhenTheDatabaseFails()
    {
        KeyIs(FirstKey);
        await _cache.RefreshAsync(CancellationToken.None);

        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection refused"));
        _clock.Advance(SigningKeyCache.CacheDuration);
        await _cache.RefreshAsync(CancellationToken.None);

        Assert.Equal(Encoding.UTF8.GetBytes(FirstKey), Bytes(Assert.Single(_cache.CurrentKeys)));
    }

    [Fact]
    public async Task RefreshAsync_ClearsTheKeys_WhenAppConfigIsInvalid()
    {
        KeyIs(FirstKey);
        await _cache.RefreshAsync(CancellationToken.None);

        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppConfigException("SysConfig 缺少「appConfig」設定。"));
        _clock.Advance(SigningKeyCache.CacheDuration);
        await _cache.RefreshAsync(CancellationToken.None);

        Assert.Empty(_cache.CurrentKeys);
    }

    [Fact]
    public async Task RefreshAsync_RetriesOnTheNextCall_WhileNoKeyIsLoaded()
    {
        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection refused"));
        await _cache.RefreshAsync(CancellationToken.None);
        Assert.Empty(_cache.CurrentKeys);

        KeyIs(FirstKey);
        await _cache.RefreshAsync(CancellationToken.None); // no clock advance: an empty cache is never "fresh"

        Assert.Equal(Encoding.UTF8.GetBytes(FirstKey), Bytes(Assert.Single(_cache.CurrentKeys)));
    }

    [Fact]
    public async Task RefreshAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repository.Setup(r => r.GetSymmetricSecurityKeyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _cache.RefreshAsync(cts.Token));
    }
}
