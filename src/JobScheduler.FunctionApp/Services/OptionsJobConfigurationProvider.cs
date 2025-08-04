using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Core.Models;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Services;

public class OptionsJobConfigurationProvider : IJobConfigurationProvider
{
    private readonly JobSchedulerOptions _options;
    private readonly Dictionary<string, JobConfig> _jobConfigs;

    public OptionsJobConfigurationProvider(IOptions<JobSchedulerOptions> options)
    {
        _options = options.Value;
        _jobConfigs = ConvertFromOptions(_options.Jobs);
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

    private static Dictionary<string, JobConfig> ConvertFromOptions(Dictionary<string, JobDefinition> jobDefinitions)
    {
        var configs = new Dictionary<string, JobConfig>();

        foreach (var (key, definition) in jobDefinitions)
        {
            configs[key] = new JobConfig
            {
                JobName = definition.JobName,
                Endpoint = definition.Endpoint,
                HttpMethod = definition.HttpMethod,
                AuthType = definition.AuthType,
                AuthSecretName = definition.AuthSecretName,
                RequestBody = definition.RequestBody,
                TimeoutSeconds = definition.TimeoutSeconds,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = definition.RetryPolicy.MaxAttempts,
                    BaseDelayMs = definition.RetryPolicy.BaseDelayMs,
                    BackoffMultiplier = definition.RetryPolicy.BackoffMultiplier,
                    MaxDelayMs = definition.RetryPolicy.MaxDelayMs,
                    RetryableStatusCodes = definition.RetryPolicy.RetryableStatusCodes.ToList()
                },
                Tags = definition.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        return configs;
    }
}
