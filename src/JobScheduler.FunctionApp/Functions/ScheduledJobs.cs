using JobScheduler.FunctionApp.Configuration;
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
        [Function(JobNames.ContainerAppHealth)]
        public async Task ContainerAppHealthCheck([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer)
        {
            await ExecuteJobSafely(JobNames.ContainerAppHealth, myTimer);
        }

        // Generic job execution with error handling
        private async Task ExecuteJobSafely(string jobName, TimerInfo timerInfo)
        {
            try
            {
                _logger.LogInformation("=== Starting scheduled job: {JobName} ===", jobName);
                _logger.LogInformation("JobExecutor is null: {IsNull}", _jobExecutor == null);
                _logger.LogInformation("ConfigProvider is null: {IsNull}", _configProvider == null);

                if (_jobExecutor == null)
                {
                    _logger.LogError("JobExecutor is null - dependency injection failed");
                    return;
                }

                if (_configProvider == null)
                {
                    _logger.LogError("ConfigProvider is null - dependency injection failed");
                    return;
                }

                _logger.LogInformation("Getting job config for: {JobName}", jobName);
                var config = _configProvider.GetJobConfig(jobName);
                
                if (config == null)
                {
                    _logger.LogError("Job config is null for job: {JobName}", jobName);
                    return;
                }

                _logger.LogInformation("Job config retrieved successfully. Endpoint: {Endpoint}", config.Endpoint);
                _logger.LogInformation("Executing job with JobExecutor...");
                
                var result = await _jobExecutor.ExecuteAsync(config);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("=== Job {JobName} completed successfully in {Duration}ms ===",
                        jobName, result.Duration.TotalMilliseconds);
                }
                else
                {
                    _logger.LogError("=== Job {JobName} failed: {Error} ===", jobName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== CRITICAL ERROR in ExecuteJobSafely for job {JobName}: {Error} ===", jobName, ex.Message);
                _logger.LogError("=== Exception Type: {ExceptionType} ===", ex.GetType().Name);
                _logger.LogError("=== Stack Trace: {StackTrace} ===", ex.StackTrace);
                
                // Also try to log to console in case logger is broken
                Console.WriteLine($"CRITICAL ERROR in job {jobName}: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                // Don't rethrow - we want the Function to complete successfully even if job fails
            }
        }
    }
}