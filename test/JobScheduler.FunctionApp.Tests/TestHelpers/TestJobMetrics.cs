using JobScheduler.FunctionApp.Core.Interfaces;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestJobMetrics : IJobMetrics
    {
        public List<string> RecordedSuccesses { get; } = new();
        public List<string> RecordedFailures { get; } = new();
        
        // Properties for backward compatibility with existing tests
        public List<string> SuccessMetrics => RecordedSuccesses;
        public List<string> FailureMetrics => RecordedFailures;

        public Task RecordJobSuccessAsync(string jobName, TimeSpan duration, int attempts)
        {
            RecordedSuccesses.Add($"{jobName}-{duration.TotalMilliseconds}ms-{attempts}");
            return Task.CompletedTask;
        }

        public Task RecordJobFailureAsync(string jobName, TimeSpan duration, string errorMessage)
        {
            RecordedFailures.Add($"{jobName}-{duration.TotalMilliseconds}ms-{errorMessage}");
            return Task.CompletedTask;
        }
    }
}
