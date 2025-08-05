using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using JobScheduler.FunctionApp.Services;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

/// <summary>
/// Integration tests that verify the complete configuration pipeline matches the runtime behavior.
/// These tests simulate the exact same setup as Program.cs to ensure the configuration system
/// works end-to-end as it would in production.
/// </summary>
public class RealConfigurationTests
{
    [Fact]
    public void RuntimeConfiguration_ShouldMatchProductionAppsettingsJson()
    {
        // Arrange - Use configuration that matches actual appsettings.json structure
        var productionLikeConfig = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:container-app-health:JobName"] = "container-app-health",
            ["JobScheduler:Jobs:container-app-health:Endpoint"] = "https://int-svc-be-capp-dev.whitesky-4effbccc.centralus.azurecontainerapps.io/health",
            ["JobScheduler:Jobs:container-app-health:HttpMethod"] = "GET",
            ["JobScheduler:Jobs:container-app-health:AuthType"] = "none",
            ["JobScheduler:Jobs:container-app-health:TimeoutSeconds"] = "30",
            
            ["JobScheduler:Jobs:daily-batch:JobName"] = "daily-batch",
            ["JobScheduler:Jobs:daily-batch:Endpoint"] = "https://your-api.azurecontainerapps.io/api/batch-process",
            ["JobScheduler:Jobs:daily-batch:HttpMethod"] = "POST",
            ["JobScheduler:Jobs:daily-batch:AuthType"] = "bearer",
            ["JobScheduler:Jobs:daily-batch:AuthSecretName"] = "DAILY_BATCH_TOKEN",
            ["JobScheduler:Jobs:daily-batch:TimeoutSeconds"] = "120",
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(productionLikeConfig);
        var jobConfigProvider = setup.GetJobConfigurationProvider();
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert - Verify the complete pipeline works
        options.Jobs.Should().ContainKey("container-app-health");
        options.Jobs.Should().ContainKey("daily-batch");

        var healthCheckJob = jobConfigProvider.GetJobConfig("container-app-health");
        healthCheckJob.Should().NotBeNull();
        healthCheckJob.JobName.Should().Be("container-app-health");
        healthCheckJob.HttpMethod.Should().Be(HttpMethod.Get);
        healthCheckJob.AuthType.Should().Be(AuthenticationType.None);

        var batchJob = jobConfigProvider.GetJobConfig("daily-batch");
        batchJob.Should().NotBeNull();
        batchJob.JobName.Should().Be("daily-batch");
        batchJob.HttpMethod.Should().Be(HttpMethod.Post);
        batchJob.AuthType.Should().Be(AuthenticationType.Bearer);
        batchJob.AuthSecretName.Should().Be("DAILY_BATCH_TOKEN");
    }

    [Fact]
    public void RuntimeValidation_ShouldPassWithCorrectConfiguration()
    {
        // Arrange - Use the standard test configuration
        using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
        var validationResult = setup.ValidateConfiguration();

        // Act & Assert - Validation should succeed with well-formed configuration
        validationResult.Succeeded.Should().BeTrue($"Validation failed: {string.Join(", ", validationResult.Failures ?? [])}");
    }

    [Fact]
    public void RuntimeValidation_ShouldFailGracefullyWithBadConfiguration()
    {
        // Arrange - Configuration that should fail validation (missing jobs)
        using var setup = TestConfigurationHelper.CreateEmptyConfiguration();
        var validationResult = setup.ValidateConfiguration();

        // Act & Assert - Should fail gracefully with clear error messages
        validationResult.Succeeded.Should().BeFalse("Should fail when no jobs are configured");
        validationResult.Failures.Should().Contain("At least one job must be configured.");
    }

    [Fact]
    public void OptionsJobConfigurationProvider_ShouldBeUsedInRuntime()
    {
        // Arrange - Verify that we're using the same provider as runtime
        using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
        var provider = setup.GetJobConfigurationProvider();

        // Act & Assert - Ensure we're using the correct provider type
        provider.Should().BeOfType<OptionsJobConfigurationProvider>("Runtime should use OptionsJobConfigurationProvider");
        
        // Verify it can fetch jobs correctly
        var allJobs = provider.GetAllJobConfigs().ToList();
        allJobs.Should().HaveCount(2);
        allJobs.Should().Contain(job => job.JobName == "container-app-health");
        allJobs.Should().Contain(job => job.JobName == "daily-batch");
    }
}
