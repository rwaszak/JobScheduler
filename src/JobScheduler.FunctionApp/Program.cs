using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);

        builder.ConfigureFunctionsWebApplication();

        // Configuration
        builder.Services.Configure<JobSchedulerOptions>(
            builder.Configuration.GetSection(JobSchedulerOptions.SectionName));

        // Validate configuration on startup
        builder.Services.AddSingleton<IValidateOptions<JobSchedulerOptions>, ValidateJobSchedulerOptions>();

        // HttpClient with factory pattern
        builder.Services.AddHttpClient("job-executor", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", "JobScheduler/1.0");
        });

        builder.Services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()

            // Register core services with appropriate lifetimes
            .AddScoped<IJobExecutor, JobExecutor>()
            .AddSingleton<ISecretManager, EnvironmentSecretManager>()
            .AddScoped<IJobLogger, JobLogger>()
            .AddScoped<IJobMetrics, JobMetrics>()
            .AddSingleton<IJobConfigurationProvider, OptionsJobConfigurationProvider>();

        builder.Build().Run();
    }
}

// Configuration validation class
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