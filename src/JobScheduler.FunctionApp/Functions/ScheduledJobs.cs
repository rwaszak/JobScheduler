// Functions/ScheduledJobs.cs
using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Functions
{
    public class ScheduledJobs
    {
        private readonly IJobExecutor _jobExecutor;
        private readonly IJobConfigurationProvider _configProvider;
        private readonly ILogger<ScheduledJobs> _logger;

        public ScheduledJobs(
            IJobExecutor jobExecutor,
            IJobConfigurationProvider configProvider,
            ILogger<ScheduledJobs> logger)
        {
            _jobExecutor = jobExecutor;
            _configProvider = configProvider;
            _logger = logger;
        }

        // Your existing timer - now using the modular approach
        [Function("ContainerAppHealthCheck")]
        public async Task ContainerAppHealthCheck([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer)
        {
            await ExecuteJobSafely("container-app-health", myTimer);
        }

        // Additional jobs you can easily add
        [Function("DailyBatchProcessor")]
        public async Task DailyBatchProcessor([TimerTrigger("0 0 6 * * *")] TimerInfo myTimer)
        {
            await ExecuteJobSafely("daily-batch", myTimer);
        }

        // Generic job execution with error handling
        private async Task ExecuteJobSafely(string jobName, TimerInfo timerInfo)
        {
            try
            {
                _logger.LogInformation("Starting scheduled job: {JobName}", jobName);

                var config = _configProvider.GetJobConfig(jobName);
                var result = await _jobExecutor.ExecuteAsync(config);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Job {JobName} completed successfully in {Duration}ms",
                        jobName, result.Duration.TotalMilliseconds);
                }
                else
                {
                    _logger.LogError("Job {JobName} failed: {Error}", jobName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in job {JobName}", jobName);
                // Don't rethrow - we want the Function to complete successfully even if job fails
            }
        }
    }
}