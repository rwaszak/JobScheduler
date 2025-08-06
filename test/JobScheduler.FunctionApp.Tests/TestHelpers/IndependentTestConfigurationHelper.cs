using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Tests.TestHelpers;

/// <summary>
/// Independent test configuration helper that doesn't depend on production JobNames or appsettings.json.
/// Creates isolated test configurations for framework testing without coupling to production job definitions.
/// </summary>
public static class IndependentTestConfigurationHelper
{
    /// <summary>
    /// Creates a basic test configuration with a single health check job
    /// </summary>
    public static TestConfigurationSetup CreateBasicTestConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:JobName"] = TestJobNames.TestHealthCheck,
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:Endpoint"] = "https://test-api.example.com/health",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:TimeoutSeconds"] = "30"
        };

        return CreateTestConfiguration(configData);
    }

    /// <summary>
    /// Creates a comprehensive test configuration with multiple job types
    /// </summary>
    public static TestConfigurationSetup CreateComprehensiveTestConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            // Basic health check job
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:JobName"] = TestJobNames.TestHealthCheck,
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:Endpoint"] = "https://test-api.example.com/health",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:TimeoutSeconds"] = "30",

            // Auth job with bearer token
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:JobName"] = TestJobNames.TestAuthJob,
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:Endpoint"] = "https://test-api.example.com/auth-endpoint",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:HttpMethod"] = "POST",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:AuthType"] = "bearer",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:AuthSecretName"] = "TEST_AUTH_TOKEN",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:TimeoutSeconds"] = "60",

            // Error handling job
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:JobName"] = TestJobNames.TestErrorJob,
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:Endpoint"] = "https://test-api.example.com/error-endpoint",
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:TimeoutSeconds"] = "15"
        };

        return CreateTestConfiguration(configData);
    }

    /// <summary>
    /// Creates an empty test configuration for validation testing
    /// </summary>
    public static TestConfigurationSetup CreateEmptyTestConfiguration()
    {
        return CreateTestConfiguration(new Dictionary<string, string?>());
    }

    /// <summary>
    /// Creates a test configuration with specific job data
    /// </summary>
    public static TestConfigurationSetup CreateCustomTestConfiguration(string jobName, JobDefinition jobDefinition)
    {
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{jobName}:JobName"] = jobDefinition.JobName,
            [$"JobScheduler:Jobs:{jobName}:Endpoint"] = jobDefinition.Endpoint,
            [$"JobScheduler:Jobs:{jobName}:HttpMethod"] = jobDefinition.HttpMethod.ToString().ToUpper(),
            [$"JobScheduler:Jobs:{jobName}:AuthType"] = jobDefinition.AuthType.ToString().ToLower(),
            [$"JobScheduler:Jobs:{jobName}:TimeoutSeconds"] = jobDefinition.TimeoutSeconds.ToString()
        };

        if (!string.IsNullOrEmpty(jobDefinition.AuthSecretName))
        {
            configData[$"JobScheduler:Jobs:{jobName}:AuthSecretName"] = jobDefinition.AuthSecretName;
        }

        return CreateTestConfiguration(configData);
    }

    /// <summary>
    /// Creates a test configuration with custom configuration data
    /// </summary>
    public static TestConfigurationSetup CreateTestConfiguration(Dictionary<string, string?> configData)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        
        // Use the exact same setup as the original TestConfigurationHelper for compatibility
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));
        services.AddSingleton<IValidateOptions<JobSchedulerOptions>>(provider => 
            new TestJobSchedulerOptionsWithConfigurationValidator(configuration));
        services.AddSingleton<IJobConfigurationProvider, OptionsJobConfigurationProvider>();

        var serviceProvider = services.BuildServiceProvider();

        return new TestConfigurationSetup(serviceProvider, configuration);
    }
}

/// <summary>
/// Test configuration setup wrapper that provides access to configured services
/// </summary>
public class TestConfigurationSetup : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    internal TestConfigurationSetup(ServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public IJobConfigurationProvider GetJobConfigurationProvider()
        => _serviceProvider.GetRequiredService<IJobConfigurationProvider>();

    public JobSchedulerOptions GetJobSchedulerOptions()
    {
        try
        {
            return _serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;
        }
        catch (OptionsValidationException ex)
        {
            // Rethrow with more context about which test is calling this
            throw new InvalidOperationException(
                $"Configuration validation failed when accessing JobSchedulerOptions. " +
                $"Failures: {string.Join(", ", ex.Failures)}", ex);
        }
    }

    public IConfiguration GetConfiguration() => _configuration;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
