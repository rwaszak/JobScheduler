using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Services
{
    public class JobMetrics : IJobMetrics
    {
        private readonly ILogger<JobMetrics> _logger;
        private readonly MetricsOptions _metricsOptions;

        public JobMetrics(ILogger<JobMetrics> logger, IOptions<JobSchedulerOptions> options)
        {
            _logger = logger;
            _metricsOptions = options.Value.Metrics;
        }

        public Task RecordJobSuccessAsync(string jobName, TimeSpan duration, int attemptCount)
        {
            if (!_metricsOptions.EnableMetrics) return Task.CompletedTask;
            
            _logger.LogInformation("JOB_SUCCESS: {JobName} completed in {Duration}ms after {AttemptCount} attempts",
                jobName, duration.TotalMilliseconds, attemptCount);
            return Task.CompletedTask;
        }

        public Task RecordJobFailureAsync(string jobName, TimeSpan duration, string errorMessage)
        {
            if (!_metricsOptions.EnableMetrics) return Task.CompletedTask;
            
            _logger.LogError("JOB_FAILURE: {JobName} failed after {Duration}ms. Error: {ErrorMessage}",
                jobName, duration.TotalMilliseconds, errorMessage);
            return Task.CompletedTask;
        }
    }
}
