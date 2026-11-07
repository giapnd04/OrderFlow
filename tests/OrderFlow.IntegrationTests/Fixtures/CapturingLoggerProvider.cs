using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OrderFlow.IntegrationTests.Fixtures;

public sealed record CapturedLog(string Category, string? EventName, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Scope);

// Keeps every log line in memory together with the scope values active when it was written
// (for example CorrelationId), so a test can assert which request produced which line.
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> _logs = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<CapturedLog> Logs => _logs;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class Logger(string category, CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scope = new Dictionary<string, object?>();

            provider._scopes.ForEachScope(
                (value, values) =>
                {
                    if (value is IEnumerable<KeyValuePair<string, object>> pairs)
                    {
                        foreach (var (key, item) in pairs)
                        {
                            values[key] = item;
                        }
                    }
                },
                scope);

            provider._logs.Enqueue(new CapturedLog(category, eventId.Name, logLevel, formatter(state, exception), scope));
        }
    }
}
