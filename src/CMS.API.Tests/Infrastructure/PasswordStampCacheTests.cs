using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CMS.API.Tests.Infrastructure;

public class PasswordStampCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthRepository> _repository = new(MockBehavior.Strict);
    private readonly FixedTimeProvider _clock = new(Start);
    private readonly PasswordStampCache _cache;

    public PasswordStampCacheTests()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _repository.Object);
        _cache = new PasswordStampCache(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _clock);
    }

    private void SetupStamp(string userId, DateTime? passwordUpdatedTime) =>
        _repository.Setup(r => r.GetPasswordStampAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordStamp { PasswordUpdatedTime = passwordUpdatedTime });

    // ---- IsIssuedBeforePasswordChange ----

    [Fact]
    public void IsIssuedBeforePasswordChange_IsFalse_WhenThePasswordWasNeverChanged()
    {
        Assert.False(PasswordStampCache.IsIssuedBeforePasswordChange(Start.UtcDateTime, null));
    }

    [Fact]
    public void IsIssuedBeforePasswordChange_IsTrue_ForATokenOlderThanTheChange()
    {
        var changedAt = Start.UtcDateTime;
        Assert.True(PasswordStampCache.IsIssuedBeforePasswordChange(changedAt.AddSeconds(-1), changedAt));
        Assert.True(PasswordStampCache.IsIssuedBeforePasswordChange(changedAt.AddHours(-5), changedAt));
    }

    [Fact]
    public void IsIssuedBeforePasswordChange_IsFalse_ForATokenIssuedAfterTheChange()
    {
        var changedAt = Start.UtcDateTime;
        Assert.False(PasswordStampCache.IsIssuedBeforePasswordChange(changedAt.AddSeconds(1), changedAt));
        Assert.False(PasswordStampCache.IsIssuedBeforePasswordChange(changedAt.AddHours(1), changedAt));
    }

    [Fact]
    public void IsIssuedBeforePasswordChange_ComparesWholeSeconds_SoALoginInTheSameSecondAsTheChangeIsAccepted()
    {
        // iat has one-second resolution: 12:00:00 (token) vs 12:00:00.750 (column) must not count as "before".
        var changedAt = Start.UtcDateTime.AddMilliseconds(750);
        Assert.False(PasswordStampCache.IsIssuedBeforePasswordChange(Start.UtcDateTime, changedAt));
        Assert.True(PasswordStampCache.IsIssuedBeforePasswordChange(Start.UtcDateTime.AddSeconds(-1), changedAt));
    }

    // ---- caching ----

    [Fact]
    public async Task GetAsync_ReadsTheRepositoryOnce_WithinTheCacheDuration()
    {
        SetupStamp("helen", Start.UtcDateTime);

        var first = await _cache.GetAsync("helen", CancellationToken.None);
        _clock.Advance(PasswordStampCache.CacheDuration - TimeSpan.FromSeconds(1));
        var second = await _cache.GetAsync("helen", CancellationToken.None);

        Assert.Same(first, second);
        _repository.Verify(r => r.GetPasswordStampAsync("helen", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ReReads_AfterTheCacheDuration()
    {
        SetupStamp("helen", Start.UtcDateTime);

        await _cache.GetAsync("helen", CancellationToken.None);
        _clock.Advance(PasswordStampCache.CacheDuration);
        await _cache.GetAsync("helen", CancellationToken.None);

        _repository.Verify(r => r.GetPasswordStampAsync("helen", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_CachesPerUser()
    {
        SetupStamp("helen", null);
        SetupStamp("mike", Start.UtcDateTime);

        var helen = await _cache.GetAsync("helen", CancellationToken.None);
        var mike = await _cache.GetAsync("mike", CancellationToken.None);

        Assert.Null(helen!.PasswordUpdatedTime);
        Assert.Equal(Start.UtcDateTime, mike!.PasswordUpdatedTime);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForAnUnknownUser_AndCachesThatToo()
    {
        _repository.Setup(r => r.GetPasswordStampAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((PasswordStamp?)null);

        Assert.Null(await _cache.GetAsync("ghost", CancellationToken.None));
        Assert.Null(await _cache.GetAsync("ghost", CancellationToken.None));

        _repository.Verify(r => r.GetPasswordStampAsync("ghost", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalidate_ForcesTheNextGetToReRead()
    {
        SetupStamp("helen", null);
        await _cache.GetAsync("helen", CancellationToken.None);

        SetupStamp("helen", Start.UtcDateTime); // the password was changed meanwhile
        _cache.Invalidate("helen");
        var reloaded = await _cache.GetAsync("helen", CancellationToken.None);

        Assert.Equal(Start.UtcDateTime, reloaded!.PasswordUpdatedTime);
        _repository.Verify(r => r.GetPasswordStampAsync("helen", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_PropagatesARepositoryFailure_WithoutCachingIt()
    {
        _repository.SetupSequence(r => r.GetPasswordStampAsync("helen", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"))
            .ReturnsAsync(new PasswordStamp { PasswordUpdatedTime = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _cache.GetAsync("helen", CancellationToken.None));
        var recovered = await _cache.GetAsync("helen", CancellationToken.None);

        Assert.NotNull(recovered);
    }
}
