using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using JobScheduler.FunctionApp.Configuration;
using FluentAssertions;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

/// <summary>
/// Tests to verify that actual JSON configuration binds correctly to model objects,
/// particularly for complex types like HttpMethod that require type conversion.
/// </summary>
public class ConfigurationBindingTests
{
    [Fact]
    public void JobSchedulerOptions_ShouldBindFromJsonConfiguration_WithStringHttpMethods()
    {
        // Arrange - JSON configuration exactly as it appears in local.settings.json
        var jsonConfig = """
        {
          "JobScheduler": {
            "Jobs": {
              "container-app-health": {
                "JobName": "container-app-health",
                "Endpoint": "https://example.com/health",
                "HttpMethod": "GET",
                "TimeoutSeconds": 30
              },
              "daily-batch": {
                "JobName": "daily-batch", 
                "Endpoint": "https://example.com/batch",
                "HttpMethod": "POST",
                "RequestBody": "{\"action\":\"process\"}",
                "TimeoutSeconds": 60
              }
            }
          }
        }
        """;

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonConfig)))
            .Build();

        var services = new ServiceCollection();
        
        // Use the same type converter approach as the application
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection("JobScheduler"));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;

        // Assert
        options.Jobs.Should().HaveCount(2);
        
        var healthJob = options.Jobs["container-app-health"];
        healthJob.JobName.Should().Be("container-app-health");
        healthJob.Endpoint.Should().Be("https://example.com/health");
        healthJob.HttpMethod.Should().Be(HttpMethod.Get); // This now works!
        healthJob.TimeoutSeconds.Should().Be(30);

        var batchJob = options.Jobs["daily-batch"];
        batchJob.JobName.Should().Be("daily-batch");
        batchJob.Endpoint.Should().Be("https://example.com/batch");
        batchJob.HttpMethod.Should().Be(HttpMethod.Post); // This now works!
        batchJob.RequestBody.Should().Be("{\"action\":\"process\"}");
        batchJob.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void JobDefinition_HttpMethodBinding_ShouldFailSilentlyForUnknownMethod()
    {
        // Arrange - Test specifically for unknown HTTP method
        var jsonConfig = """
        {
          "JobScheduler": {
            "Jobs": {
              "test-job": {
                "JobName": "test",
                "Endpoint": "https://example.com",
                "HttpMethod": "INVALID"
              }
            }
          }
        }
        """;

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonConfig)))
            .Build();

        var services = new ServiceCollection();
        
        // Use the same type converter approach as the application
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection("JobScheduler"));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;
        
        // Assert - Configuration binding should fail silently for invalid HttpMethod
        // The job won't be added to the configuration at all
        options.Jobs.Should().BeEmpty("invalid HttpMethod should cause configuration binding to fail silently");
    }
}
