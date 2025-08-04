using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestJobLogger : IJobLogger
    {
        private readonly List<LogEntry> _logs = new();

        public IReadOnlyList<LogEntry> Logs => _logs.AsReadOnly();

        public Task LogAsync(LogLevel level, string jobName, string message, object? metadata = null)
        {
            _logs.Add(new LogEntry
            {
                LogLevel = level,
                JobName = jobName,
                Message = message,
                Metadata = metadata,
                Timestamp = DateTime.UtcNow
            });
            
            return Task.CompletedTask;
        }

        public void Clear() => _logs.Clear();

        public class LogEntry
        {
            public LogLevel LogLevel { get; set; }
            public required string JobName { get; set; }
            public required string Message { get; set; }
            public object? Metadata { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
