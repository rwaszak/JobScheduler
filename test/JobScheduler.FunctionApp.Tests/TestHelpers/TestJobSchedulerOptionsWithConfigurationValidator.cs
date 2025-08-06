using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using JobScheduler.FunctionApp.Configuration;
using System.ComponentModel.DataAnnotations;

namespace JobScheduler.FunctionApp.Tests.TestHelpers;

/// <summary>
/// Test-specific validator that has access to configuration and can detect binding failures
/// such as invalid HTTP methods that cause jobs to be silently dropped during binding.
/// </summary>
public class TestJobSchedulerOptionsWithConfigurationValidator : IValidateOptions<JobSchedulerOptions>
{
    private readonly IConfiguration _configuration;

    public TestJobSchedulerOptionsWithConfigurationValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, JobSchedulerOptions options)
    {
        var failures = new List<string>();

        if (options.Jobs == null)
        {
            failures.Add("JobScheduler:Jobs configuration section is missing.");
            return ValidateOptionsResult.Fail(failures);
        }

        // Validate individual job configurations
        foreach (var (jobName, jobDefinition) in options.Jobs)
        {
            if (string.IsNullOrWhiteSpace(jobDefinition.JobName))
            {
                failures.Add($"Job '{jobName}' has missing or empty JobName.");
            }
            else if (jobDefinition.JobName != jobName)
            {
                failures.Add($"Job '{jobName}' has mismatched JobName: expected '{jobName}', got '{jobDefinition.JobName}'.");
            }

            if (string.IsNullOrWhiteSpace(jobDefinition.Endpoint))
            {
                failures.Add($"Job '{jobName}' has missing or empty Endpoint.");
            }

            if (jobDefinition.TimeoutSeconds <= 0)
            {
                failures.Add($"Job '{jobName}' has invalid TimeoutSeconds: {jobDefinition.TimeoutSeconds}.");
            }

            // Validate auth configuration
            if (jobDefinition.AuthType == AuthenticationType.Bearer)
            {
                if (string.IsNullOrWhiteSpace(jobDefinition.AuthSecretName))
                {
                    failures.Add($"Job '{jobName}' has AuthType 'Bearer' but missing AuthSecretName.");
                }
            }
        }

        // Check for configuration binding failures by comparing configured jobs vs bound jobs
        var jobsSection = _configuration.GetSection("JobScheduler:Jobs");
        if (jobsSection.Exists())
        {
            var configuredJobNames = jobsSection.GetChildren().Select(section => section.Key).ToHashSet();
            var boundJobNames = options.Jobs.Keys.ToHashSet();
            
            var failedBindingJobs = configuredJobNames.Except(boundJobNames).ToList();
            
            foreach (var jobName in failedBindingJobs)
            {
                var jobSection = jobsSection.GetSection(jobName);
                var httpMethod = jobSection["HttpMethod"];
                
                if (!string.IsNullOrEmpty(httpMethod))
                {
                    // Check if this looks like an invalid HttpMethod value
                    var validHttpMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
                    if (!validHttpMethods.Contains(httpMethod.ToUpperInvariant()))
                    {
                        failures.Add($"Job '{jobName}': Configuration binding failed. Invalid HttpMethod value '{httpMethod}'. Valid values are: {string.Join(", ", validHttpMethods)}.");
                    }
                    else
                    {
                        failures.Add($"Job '{jobName}': Configuration binding failed. HttpMethod '{httpMethod}' could not be converted to HttpMethod object.");
                    }
                }
                else
                {
                    failures.Add($"Job '{jobName}': Configuration binding failed. Job was configured but could not be bound to JobDefinition object.");
                }
            }
        }

        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures) 
            : ValidateOptionsResult.Success;
    }
}
