using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Services
{
    public class JobMetrics : IJobMetrics
    {
        private readonly ILogger<JobMetrics> _logger;

        public JobMetrics(ILogger<JobMetrics> logger)
        {
            _logger = logger;
        }

        public Task RecordJobSuccessAsync(string jobName, TimeSpan duration, int attemptCount)
        {
            _logger.LogInformation("JOB_SUCCESS: {JobName} completed in {Duration}ms after {AttemptCount} attempts",
                jobName, duration.TotalMilliseconds, attemptCount);
            return Task.CompletedTask;
        }

        public Task RecordJobFailureAsync(string jobName, TimeSpan duration, string errorMessage)
        {
            _logger.LogError("JOB_FAILURE: {JobName} failed after {Duration}ms - {Error}",
                jobName, duration.TotalMilliseconds, errorMessage);
            return Task.CompletedTask;
        }
    }
}