using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests;

/// <summary>
/// Tests for JobScheduler options validation using the same configuration approach as runtime.
/// These tests verify that validation works correctly with the appsettings.json configuration.
/// </summary>
public class ValidateJobSchedulerOptionsTests
{
    [Fact]
    public void Validate_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange - Use the standard test configuration which includes both required jobs
        using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
        var result = setup.ValidateConfiguration();

        // Act & Assert - Default configuration should pass validation
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithMissingJobConstant_ReturnsFailure()
    {
        // Arrange - Configuration with only one of the required jobs
        using var setup = TestConfigurationHelper.CreateSingleJobConfiguration(
            jobName: JobNames.ContainerAppHealth,
            endpoint: "https://api.example.com/health",
            httpMethod: "GET",
            authType: "none",
            timeoutSeconds: 30);

        var result = setup.ValidateConfiguration();

        // Act & Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{JobNames.DailyBatch}' is defined in JobNames constants but missing from configuration.");
    }

    [Fact]
    public void Validate_WithExtraConfiguredJob_ReturnsFailure()
    {
        // Arrange - Configuration includes both required jobs plus an unknown job
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://api.example.com/health",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30",
            
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:JobName"] = JobNames.DailyBatch,
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:Endpoint"] = "https://api.example.com/batch",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:HttpMethod"] = "POST",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:AuthType"] = "bearer",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:AuthSecretName"] = "BATCH_TOKEN",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:TimeoutSeconds"] = "120",
            
            ["JobScheduler:Jobs:unknown-job:JobName"] = "unknown-job",
            ["JobScheduler:Jobs:unknown-job:Endpoint"] = "https://api.example.com/unknown",
            ["JobScheduler:Jobs:unknown-job:HttpMethod"] = "GET",
            ["JobScheduler:Jobs:unknown-job:AuthType"] = "none",
            ["JobScheduler:Jobs:unknown-job:TimeoutSeconds"] = "30",
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var result = setup.ValidateConfiguration();

        // Act & Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Job 'unknown-job' is configured but not defined in JobNames constants. Consider adding it for type safety.");
    }

    [Fact]
    public void Validate_WithInvalidEndpoint_ReturnsFailure()
    {
        // Arrange - Configuration with invalid endpoint URL for one job, valid for the other
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "not-a-valid-url",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30",
            
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:JobName"] = JobNames.DailyBatch,
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:Endpoint"] = "https://api.example.com/batch",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:HttpMethod"] = "POST",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:AuthType"] = "bearer",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:AuthSecretName"] = "BATCH_TOKEN",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:TimeoutSeconds"] = "120",
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var result = setup.ValidateConfiguration();

        // Act & Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{JobNames.ContainerAppHealth}': Invalid endpoint URL format.");
    }

    [Fact]
    public void Validate_WithBearerAuthButMissingSecretName_ReturnsFailure()
    {
        // Arrange - Configuration with bearer auth but missing secret name for one job
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://api.example.com/health",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30",
            
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:JobName"] = JobNames.DailyBatch,
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:Endpoint"] = "https://api.example.com/batch",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:HttpMethod"] = "POST",
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:AuthType"] = "bearer",
            // Missing AuthSecretName
            [$"JobScheduler:Jobs:{JobNames.DailyBatch}:TimeoutSeconds"] = "120",
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var result = setup.ValidateConfiguration();

        // Act & Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{JobNames.DailyBatch}': AuthSecretName is required when AuthType is 'bearer'.");
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllFailures()
    {
        // Arrange - Configuration with multiple validation errors
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "invalid-url",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30",
            
            ["JobScheduler:Jobs:unknown-job:JobName"] = "unknown-job",
            ["JobScheduler:Jobs:unknown-job:Endpoint"] = "https://api.example.com/unknown",
            ["JobScheduler:Jobs:unknown-job:HttpMethod"] = "POST",
            ["JobScheduler:Jobs:unknown-job:AuthType"] = "bearer",
            // Missing AuthSecretName for bearer auth
            ["JobScheduler:Jobs:unknown-job:TimeoutSeconds"] = "30",
            // Missing daily-batch job entirely
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var result = setup.ValidateConfiguration();

        // Act & Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().HaveCountGreaterThan(1);
        result.Failures.Should().Contain($"Job '{JobNames.ContainerAppHealth}': Invalid endpoint URL format.");
        result.Failures.Should().Contain($"Job '{JobNames.DailyBatch}' is defined in JobNames constants but missing from configuration.");
        result.Failures.Should().Contain("Job 'unknown-job' is configured but not defined in JobNames constants. Consider adding it for type safety.");
        result.Failures.Should().Contain("Job 'unknown-job': AuthSecretName is required when AuthType is 'bearer'.");
    }

    [Fact]
    public void Validate_WithEmptyConfiguration_ReturnsFailure()
    {
        // Arrange - No jobs configured at all
        using var setup = TestConfigurationHelper.CreateEmptyConfiguration();
        var result = setup.ValidateConfiguration();

        // Act & Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("At least one job must be configured.");
    }
}
