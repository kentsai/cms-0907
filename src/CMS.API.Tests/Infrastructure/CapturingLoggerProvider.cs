using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CMS.API.Tests.Infrastructure;

/// <summary>One captured log call, with the exception object (if any) so tests can assert on its stack trace.</summary>
public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>
/// An <see cref="ILoggerProvider"/> (and <see cref="ILogger{T}"/> factory) that keeps every log call in memory.
/// Plugged into <see cref="CmsApiFactory"/> so integration tests can prove an exception was logged server-side
/// while the HTTP response stayed generic.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToList();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    /// <summary>A typed logger writing into this provider, for unit-testing a class directly.</summary>
    public ILogger<T> CreateLogger<T>() => new CapturingLogger<T>(_entries);

    public void Dispose() { }

    private class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(logLevel, category, formatter(state, exception), exception));
    }

    private sealed class CapturingLogger<T>(ConcurrentQueue<LogEntry> entries)
        : CapturingLogger(typeof(T).FullName ?? typeof(T).Name, entries), ILogger<T>;
}
