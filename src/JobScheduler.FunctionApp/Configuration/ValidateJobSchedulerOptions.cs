using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

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

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
