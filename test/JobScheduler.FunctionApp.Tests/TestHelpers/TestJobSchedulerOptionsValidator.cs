using JobScheduler.FunctionApp.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Tests.TestHelpers;

/// <summary>
/// Test-specific validator that validates against test job names instead of production JobNames constants.
/// This allows testing the validation framework without coupling to production job definitions.
/// </summary>
public class TestJobSchedulerOptionsValidator : IValidateOptions<JobSchedulerOptions>
{
    private readonly string[] _expectedJobNames;

    /// <summary>
    /// Creates a validator that expects specific job names
    /// </summary>
    /// <param name="expectedJobNames">The job names this validator should expect</param>
    public TestJobSchedulerOptionsValidator(params string[] expectedJobNames)
    {
        _expectedJobNames = expectedJobNames ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates a validator that expects all test job names
    /// </summary>
    public static TestJobSchedulerOptionsValidator ForAllTestJobs()
        => new(TestJobNames.AllTestJobs);

    /// <summary>
    /// Creates a validator that expects only a basic health check job
    /// </summary>
    public static TestJobSchedulerOptionsValidator ForBasicHealthCheck()
        => new(TestJobNames.TestHealthCheck);

    /// <summary>
    /// Creates a validator that expects no jobs (for testing empty configurations)
    /// </summary>
    public static TestJobSchedulerOptionsValidator ForNoJobs()
        => new();

    public ValidateOptionsResult Validate(string? name, JobSchedulerOptions options)
    {
        var failures = new List<string>();

        if (options.Jobs == null)
        {
            failures.Add("JobScheduler:Jobs configuration section is missing.");
            return ValidateOptionsResult.Fail(failures);
        }

        // Check for missing expected jobs
        foreach (var expectedJobName in _expectedJobNames)
        {
            if (!options.Jobs.ContainsKey(expectedJobName))
            {
                failures.Add($"Job '{expectedJobName}' is expected but missing from configuration.");
            }
        }

        // Check for unexpected jobs (if we have expected jobs defined)
        if (_expectedJobNames.Length > 0)
        {
            foreach (var configuredJobName in options.Jobs.Keys)
            {
                if (!_expectedJobNames.Contains(configuredJobName))
                {
                    failures.Add($"Job '{configuredJobName}' is configured but not expected in this test scenario.");
                }
            }
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

        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures) 
            : ValidateOptionsResult.Success;
    }
}
