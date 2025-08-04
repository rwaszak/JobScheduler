using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestLoggerProvider<T> : ILogger<T>, IDisposable
    {
        private readonly List<LogEntry> _logs = new();

        public IReadOnlyList<LogEntry> Logs => _logs.AsReadOnly();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logs.Add(new LogEntry
            {
                LogLevel = logLevel,
                EventId = eventId,
                Message = formatter(state, exception),
                Exception = exception,
                State = state,
                Timestamp = DateTime.UtcNow
            });
        }

        public void Clear() => _logs.Clear();

        public void Dispose() { }

        public class LogEntry
        {
            public LogLevel LogLevel { get; set; }
            public EventId EventId { get; set; }
            public required string Message { get; set; }
            public Exception? Exception { get; set; }
            public object? State { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
