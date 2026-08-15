namespace Catalog.Infrastructure.Time;

/// <summary>
/// The only place in the application allowed to ask the operating system what time it is.
/// Built on <see cref="TimeProvider"/> so a test can substitute <c>FakeTimeProvider</c> without
/// a second abstraction.
/// </summary>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
}
