using System.Collections.Concurrent;
using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Infrastructure;

/// <summary>
/// Answers "was this token issued before the user's last password change?" for the bearer handler, so changing
/// a password signs every existing session out (the user must log in again with the new password).
/// </summary>
public interface IPasswordStampCache
{
    /// <summary>
    /// The user's <see cref="PasswordStamp"/>, cached per user for <see cref="PasswordStampCache.CacheDuration"/>;
    /// null when the user no longer exists. Throws on a database failure (the caller decides the policy).
    /// </summary>
    Task<PasswordStamp?> GetAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Drops the cached entry so the next request re-reads the row (call after writing PasswordUpdatedTime).</summary>
    void Invalidate(string userId);
}

/// <summary>
/// Process-wide per-user cache in front of <see cref="IAuthRepository.GetPasswordStampAsync"/>. A password change
/// through this API calls <see cref="Invalidate"/>, so the old token is rejected on the very next request; a change
/// made elsewhere (SQL, another instance) takes effect within <see cref="CacheDuration"/>.
/// </summary>
public sealed class PasswordStampCache(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IPasswordStampCache
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async Task<PasswordStamp?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (_entries.TryGetValue(userId, out var cached) && now - cached.LoadedAt < CacheDuration)
        {
            return cached.Stamp;
        }

        // IAuthRepository is scoped; this cache is a singleton, so resolve it from a scope of its own.
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var stamp = await repository.GetPasswordStampAsync(userId, cancellationToken);

        _entries[userId] = new Entry(stamp, timeProvider.GetUtcNow());
        return stamp;
    }

    public void Invalidate(string userId) => _entries.TryRemove(userId, out _);

    /// <summary>
    /// True when a token issued at <paramref name="issuedAtUtc"/> predates <paramref name="passwordUpdatedTimeUtc"/>.
    /// JWT <c>iat</c> has one-second resolution, so both sides are compared at whole seconds: a token issued in the
    /// same second as the change is still accepted, and a login right after the change is never rejected.
    /// </summary>
    public static bool IsIssuedBeforePasswordChange(DateTime issuedAtUtc, DateTime? passwordUpdatedTimeUtc)
    {
        if (passwordUpdatedTimeUtc is not { } changedAt)
        {
            return false;
        }

        return TruncateToSeconds(issuedAtUtc) < TruncateToSeconds(changedAt);
    }

    private static long TruncateToSeconds(DateTime value) => value.Ticks / TimeSpan.TicksPerSecond;

    private sealed record Entry(PasswordStamp? Stamp, DateTimeOffset LoadedAt);
}
