namespace Catalog.UnitTests.Api;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what it was told.
///
/// <para>Some values exist only to be logged — the source address on a failed login is one: it is
/// never returned to the caller and never stored, because the point of it is the audit trail.
/// Asserting on the emitted record is the only honest way to prove it survived the trip from
/// <c>HttpContext.Connection</c> through the controller.</para>
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _messages.Add(formatter(state, exception));
}
