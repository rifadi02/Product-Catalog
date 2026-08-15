namespace Catalog.Infrastructure.Time;

/// <summary>
/// The only place in the application allowed to ask the operating system what time it is.
/// Built on <see cref="TimeProvider"/> so a test can substitute <c>FakeTimeProvider</c> without
/// a second abstraction.
///
/// <para>Timestamps are truncated to microseconds, because that is the resolution PostgreSQL
/// <c>timestamptz</c> stores. A .NET <see cref="DateTime"/> counts 100-nanosecond ticks, so an
/// untruncated value loses its last digit on the way into the database — and the API then reports
/// two different <c>createdAt</c> values for the same product: the full-precision one echoed by
/// <c>POST</c>, and the rounded one every subsequent <c>GET</c> reads back. Clients that cache the
/// first and compare against the second see a phantom change. Truncating here means the value an
/// entity is created with is the value it will still have after a round trip.</para>
/// </summary>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTime UtcNow => TruncateToMicroseconds(timeProvider.GetUtcNow().UtcDateTime);

    private static DateTime TruncateToMicroseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond, value.Kind);
}
