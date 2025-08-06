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
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://int-svc-be-capp-dev.whitesky-4effbccc.centralus.azurecontainerapps.io/health",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30"
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(productionLikeConfig);
        var jobConfigProvider = setup.GetJobConfigurationProvider();
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert - Verify the complete pipeline works
        options.Jobs.Should().ContainKey(JobNames.ContainerAppHealth);

        var healthCheckJob = jobConfigProvider.GetJobConfig(JobNames.ContainerAppHealth);
        healthCheckJob.Should().NotBeNull();
        healthCheckJob.JobName.Should().Be(JobNames.ContainerAppHealth);
        healthCheckJob.HttpMethod.Should().Be(HttpMethod.Get);
        healthCheckJob.AuthType.Should().Be(AuthenticationType.None);
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
        allJobs.Should().HaveCount(1);
        allJobs.Should().Contain(job => job.JobName == JobNames.ContainerAppHealth);
        allJobs.Should().Contain(job => job.JobName == JobNames.ContainerAppHealth);
    }
}
