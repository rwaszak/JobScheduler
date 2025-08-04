using JobScheduler.FunctionApp.Core.Interfaces;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestJobMetrics : IJobMetrics
    {
        private readonly List<SuccessMetric> _successMetrics = new();
        private readonly List<FailureMetric> _failureMetrics = new();

        public IReadOnlyList<SuccessMetric> SuccessMetrics => _successMetrics.AsReadOnly();
        public IReadOnlyList<FailureMetric> FailureMetrics => _failureMetrics.AsReadOnly();

        public Task RecordJobSuccessAsync(string jobName, TimeSpan duration, int attemptCount)
        {
            _successMetrics.Add(new SuccessMetric
            {
                JobName = jobName,
                Duration = duration,
                AttemptCount = attemptCount,
                Timestamp = DateTime.UtcNow
            });
            
            return Task.CompletedTask;
        }

        public Task RecordJobFailureAsync(string jobName, TimeSpan duration, string errorMessage)
        {
            _failureMetrics.Add(new FailureMetric
            {
                JobName = jobName,
                Duration = duration,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            });
            
            return Task.CompletedTask;
        }

        public void Clear()
        {
            _successMetrics.Clear();
            _failureMetrics.Clear();
        }

        public class SuccessMetric
        {
            public required string JobName { get; set; }
            public TimeSpan Duration { get; set; }
            public int AttemptCount { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class FailureMetric
        {
            public required string JobName { get; set; }
            public TimeSpan Duration { get; set; }
            public required string ErrorMessage { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
