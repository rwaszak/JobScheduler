using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Services;

namespace JobScheduler.FunctionApp.Tests.TestHelpers;

/// <summary>
/// Provides consistent configuration setup for tests using the same approach as the runtime (appsettings.json).
/// This replaces the old environment variable-based approach with the new options-based approach.
/// </summary>
public static class TestConfigurationHelper
{
    /// <summary>
    /// Creates a default configuration with the standard two test jobs
    /// </summary>
    public static ConfigurationSetup CreateDefaultConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://test-api.example.com/health",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30",
            
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:JobName"] = JobNames.DailyBatch,
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:Endpoint"] = "https://test-api.example.com/batch-process",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:HttpMethod"] = "POST",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:AuthType"] = "bearer",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:AuthSecretName"] = "DAILY_BATCH_TOKEN",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:TimeoutSeconds"] = "120",
        };

        return CreateConfiguration(configData);
    }

    /// <summary>
    /// Creates configuration with a single job for simpler tests
    /// </summary>
    public static ConfigurationSetup CreateSingleJobConfiguration(
        string? jobName = null,
        string endpoint = "https://test-api.example.com/test",
        string httpMethod = "GET",
        string authType = "none",
        int timeoutSeconds = 30,
        string? authSecretName = null,
        string? requestBody = null)
    {
        // Use ContainerAppHealth as default if no job name specified
        jobName ??= JobNames.ContainerAppHealth;
        
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{jobName}:JobName"] = jobName,
            [$"JobScheduler:Jobs:{jobName}:Endpoint"] = endpoint,
            [$"JobScheduler:Jobs:{jobName}:HttpMethod"] = httpMethod,
            [$"JobScheduler:Jobs:{jobName}:AuthType"] = authType,
            [$"JobScheduler:Jobs:{jobName}:TimeoutSeconds"] = timeoutSeconds.ToString(),
        };

        if (!string.IsNullOrEmpty(authSecretName))
        {
            configData[$"JobScheduler:Jobs:{jobName}:AuthSecretName"] = authSecretName;
        }

        if (!string.IsNullOrEmpty(requestBody))
        {
            configData[$"JobScheduler:Jobs:{jobName}:RequestBody"] = requestBody;
        }

        return CreateConfiguration(configData);
    }

    /// <summary>
    /// Creates configuration with no jobs (for testing validation failures)
    /// </summary>
    public static ConfigurationSetup CreateEmptyConfiguration()
    {
        return CreateConfiguration(new Dictionary<string, string?>());
    }

    /// <summary>
    /// Creates configuration with custom job definitions
    /// </summary>
    public static ConfigurationSetup CreateCustomConfiguration(Dictionary<string, string?> configData)
    {
        return CreateConfiguration(configData);
    }

    private static ConfigurationSetup CreateConfiguration(Dictionary<string, string?> configData)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        
        // Use the exact same setup as Program.cs
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));
        services.AddSingleton<IValidateOptions<JobSchedulerOptions>>(provider => 
            new ValidateJobSchedulerOptions(configuration));
        services.AddSingleton<IJobConfigurationProvider, OptionsJobConfigurationProvider>();

        var serviceProvider = services.BuildServiceProvider();

        return new ConfigurationSetup(configuration, services, serviceProvider);
    }
}

/// <summary>
/// Contains all the setup objects needed for configuration-based tests
/// </summary>
public class ConfigurationSetup : IDisposable
{
    public IConfiguration Configuration { get; }
    public ServiceCollection Services { get; }
    public ServiceProvider ServiceProvider { get; }

    public ConfigurationSetup(IConfiguration configuration, ServiceCollection services, ServiceProvider serviceProvider)
    {
        Configuration = configuration;
        Services = services;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the job configuration provider (OptionsJobConfigurationProvider)
    /// </summary>
    public IJobConfigurationProvider GetJobConfigurationProvider()
    {
        return ServiceProvider.GetRequiredService<IJobConfigurationProvider>();
    }

    /// <summary>
    /// Gets the JobSchedulerOptions
    /// </summary>
    public JobSchedulerOptions GetJobSchedulerOptions()
    {
        try
        {
            return ServiceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;
        }
        catch (OptionsValidationException ex)
        {
            // Rethrow with more context about which test is calling this
            throw new InvalidOperationException(
                $"Configuration validation failed when accessing JobSchedulerOptions. " +
                $"Failures: {string.Join(", ", ex.Failures)}", ex);
        }
    }

    /// <summary>
    /// Gets the options validator
    /// </summary>
    public IValidateOptions<JobSchedulerOptions> GetValidator()
    {
        return ServiceProvider.GetRequiredService<IValidateOptions<JobSchedulerOptions>>();
    }

    /// <summary>
    /// Validates the current configuration and returns the result
    /// </summary>
    public ValidateOptionsResult ValidateConfiguration()
    {
        try
        {
            var validator = GetValidator();
            
            // Try to get options - this might trigger validation
            var optionsAccessor = ServiceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>();
            var options = optionsAccessor.Value;
            
            return validator.Validate(null, options);
        }
        catch (OptionsValidationException ex)
        {
            // If validation fails during options access, convert to ValidateOptionsResult
            return ValidateOptionsResult.Fail(ex.Failures);
        }
    }

    public void Dispose()
    {
        ServiceProvider?.Dispose();
    }
}
