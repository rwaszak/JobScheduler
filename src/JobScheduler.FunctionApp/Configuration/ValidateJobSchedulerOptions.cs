using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace JobScheduler.FunctionApp.Configuration;

public class ValidateJobSchedulerOptions : IValidateOptions<JobSchedulerOptions>
{
    public ValidateOptionsResult Validate(string? name, JobSchedulerOptions options)
    {
        var failures = new List<string>();

        if (!options.Jobs.Any())
        {
            failures.Add("At least one job must be configured.");
        }

        foreach (var (jobName, job) in options.Jobs)
        {
            var validationContext = new ValidationContext(job);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(job, validationContext, validationResults, true))
            {
                failures.AddRange(validationResults.Select(r => $"Job '{jobName}': {r.ErrorMessage}"));
            }

            // Additional business logic validation
            if (!Uri.TryCreate(job.Endpoint, UriKind.Absolute, out _))
            {
                failures.Add($"Job '{jobName}': Invalid endpoint URL format.");
            }

            if (job.AuthType == AuthenticationType.Bearer && string.IsNullOrEmpty(job.AuthSecretName))
            {
                failures.Add($"Job '{jobName}': AuthSecretName is required when AuthType is 'bearer'.");
            }
        }

        // Validate job name constants match configuration
        ValidateJobNameConstants(options, failures);

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Validates that all job names defined in JobNames constants exist in configuration.
    /// This helps catch discrepancies between code and configuration at startup.
    /// </summary>
    private void ValidateJobNameConstants(JobSchedulerOptions options, List<string> failures)
    {
        var definedJobNames = GetDefinedJobNames();
        var configuredJobNames = options.Jobs.Keys.ToHashSet();

        foreach (var jobName in definedJobNames)
        {
            if (!configuredJobNames.Contains(jobName))
            {
                failures.Add($"Job '{jobName}' is defined in JobNames constants but missing from configuration.");
            }
        }

        // Optional: Warn about configured jobs that don't have constants
        foreach (var configuredJob in configuredJobNames)
        {
            if (!definedJobNames.Contains(configuredJob))
            {
                failures.Add($"Job '{configuredJob}' is configured but not defined in JobNames constants. Consider adding it for type safety.");
            }
        }
    }

    private static HashSet<string> GetDefinedJobNames()
    {
        return typeof(JobNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet();
    }
}
