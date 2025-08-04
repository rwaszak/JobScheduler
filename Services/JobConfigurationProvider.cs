// Services/JobConfigurationProvider.cs
using HelloWorldFunctionApp.Core.Interfaces;
using HelloWorldFunctionApp.Core.Models;

namespace HelloWorldFunctionApp.Services
{
    public class EnvironmentJobConfigurationProvider : IJobConfigurationProvider
    {
        private readonly Dictionary<string, JobConfig> _jobConfigs;

        public EnvironmentJobConfigurationProvider()
        {
            _jobConfigs = LoadJobConfigurationsFromEnvironment();
        }

        public JobConfig GetJobConfig(string jobName)
        {
            return _jobConfigs.GetValueOrDefault(jobName) ??
                   throw new ArgumentException($"Job configuration not found for: {jobName}");
        }

        public IEnumerable<JobConfig> GetAllJobConfigs()
        {
            return _jobConfigs.Values;
        }

        private Dictionary<string, JobConfig> LoadJobConfigurationsFromEnvironment()
        {
            var configs = new Dictionary<string, JobConfig>();

            // Migrate your existing configuration
            configs["container-app-health"] = new JobConfig
            {
                JobName = "container-app-health",
                Endpoint = Environment.GetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT") ?? "",
                HttpMethod = "GET",
                AuthType = "none", // Your current endpoint doesn't need auth
                TimeoutSeconds = 30,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    BaseDelayMs = 1000,
                    BackoffMultiplier = 2.0
                },
                Tags = new Dictionary<string, string>
                {
                    { "category", "health-check" },
                    { "priority", "low" },
                    { "team", "platform" }
                }
            };

            // Add more job configurations as needed
            configs["daily-batch"] = new JobConfig
            {
                JobName = "daily-batch",
                Endpoint = Environment.GetEnvironmentVariable("DAILY_BATCH_ENDPOINT") ?? "",
                HttpMethod = "POST",
                AuthType = "bearer",
                AuthSecretName = "DAILY_BATCH_TOKEN",
                TimeoutSeconds = 300,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    BaseDelayMs = 2000,
                    BackoffMultiplier = 2.0
                },
                Tags = new Dictionary<string, string>
                {
                    { "category", "batch-processing" },
                    { "priority", "high" },
                    { "team", "platform" }
                }
            };

            return configs;
        }
    }
}