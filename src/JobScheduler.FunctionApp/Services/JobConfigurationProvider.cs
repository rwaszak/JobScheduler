using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Extensions.Options;
using JobScheduler.FunctionApp.Core.Models;

namespace JobScheduler.FunctionApp.Services
{
    public class EnvironmentJobConfigurationProvider : IJobConfigurationProvider
    {
        private readonly Dictionary<string, JobConfig> _jobConfigs;
        private readonly AppSettings _appSettings;

        public EnvironmentJobConfigurationProvider(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
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
            configs[JobNames.ContainerAppHealth] = new JobConfig
            {
                JobName = JobNames.ContainerAppHealth,
                Endpoint = _appSettings.IntegrationLayerDevHealthEndpoint ?? "",
                HttpMethod = HttpMethod.Get,
                AuthType = AuthenticationType.None, // Your current endpoint doesn't need auth
                TimeoutSeconds = 30,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    BaseDelayMs = 1000,
                    BackoffMultiplier = 2.0
                }
            };

            return configs;
        }
    }

    // Simple alias for testing purposes
    public class JobConfigurationProvider : EnvironmentJobConfigurationProvider
    {
        public JobConfigurationProvider(IOptions<AppSettings> appSettings) : base(appSettings)
        {
        }
    }
}