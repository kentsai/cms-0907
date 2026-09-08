namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// A <see cref="TimeProvider"/> pinned to one instant so token issue/expiry can be asserted exactly.
/// <see cref="Advance"/> moves the clock forward for cache-expiry tests.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now = Now.Add(by);
}
