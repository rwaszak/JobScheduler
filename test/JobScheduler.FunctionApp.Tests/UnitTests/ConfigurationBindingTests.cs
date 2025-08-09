using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests;

public class ConfigurationBindingTests
{
    [Fact]
    public void HttpMethod_ShouldBindFromStringConfiguration()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:test-job:JobName"] = "test-job",
            ["JobScheduler:Jobs:test-job:Endpoint"] = "https://example.com",
            ["JobScheduler:Jobs:test-job:HttpMethod"] = "GET",
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxAttempts"] = "3",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BaseDelayMs"] = "1000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BackoffMultiplier"] = "2.0",
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxDelayMs"] = "30000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:0"] = "429",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:1"] = "502",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:2"] = "503",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:3"] = "504"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        
        // Use the same type converter approach as the application
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;

        // Assert
        options.Jobs.Should().ContainKey("test-job");
        var job = options.Jobs["test-job"];
        job.HttpMethod.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public void HttpMethod_ShouldFailSilentlyForInvalidValue_CurrentBehavior()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:test-job:JobName"] = "test-job",
            ["JobScheduler:Jobs:test-job:Endpoint"] = "https://example.com",
            ["JobScheduler:Jobs:test-job:HttpMethod"] = "INVALID",
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxAttempts"] = "3",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BaseDelayMs"] = "1000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BackoffMultiplier"] = "2.0",
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxDelayMs"] = "30000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:0"] = "429",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:1"] = "502",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:2"] = "503",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:3"] = "504"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;
        
        // Assert - Current behavior: job is silently dropped
        options.Jobs.Should().BeEmpty("invalid HttpMethod causes configuration binding to fail silently");
    }

    [Fact]
    public void HttpMethod_ShouldFailValidationForInvalidValue_WithValidation()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:test-job:JobName"] = "test-job",
            ["JobScheduler:Jobs:test-job:Endpoint"] = "https://example.com",
            ["JobScheduler:Jobs:test-job:HttpMethod"] = "INVALID",
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxAttempts"] = "3",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BaseDelayMs"] = "1000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BackoffMultiplier"] = "2.0",
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxDelayMs"] = "30000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:0"] = "429",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:1"] = "502",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:2"] = "503",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:3"] = "504"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));
        
        // Add validation (same as in the application)
        services.AddSingleton<IValidateOptions<JobSchedulerOptions>, ValidateJobSchedulerOptions>();

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Should throw validation error when trying to access the options
        var act = () => serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;
        
        act.Should().Throw<OptionsValidationException>()
           .WithMessage("*Configuration binding failed*INVALID*");
    }

    [Theory]
    [InlineData("GET", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("get", "GET")]  // Test case insensitive
    [InlineData("post", "POST")]
    [InlineData("PUT", "PUT")]
    [InlineData("DELETE", "DELETE")]
    public void HttpMethod_ShouldConvertValidValues(string configValue, string expectedMethod)
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:test-job:JobName"] = "test-job",
            ["JobScheduler:Jobs:test-job:Endpoint"] = "https://example.com",
            ["JobScheduler:Jobs:test-job:HttpMethod"] = configValue,
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxAttempts"] = "3",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BaseDelayMs"] = "1000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:BackoffMultiplier"] = "2.0",
            ["JobScheduler:Jobs:test-job:RetryPolicy:MaxDelayMs"] = "30000",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:0"] = "429",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:1"] = "502",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:2"] = "503",
            ["JobScheduler:Jobs:test-job:RetryPolicy:RetryableStatusCodes:3"] = "504"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;

        // Assert
        var job = options.Jobs["test-job"];
        job.HttpMethod.Method.Should().Be(expectedMethod);
    }
}
