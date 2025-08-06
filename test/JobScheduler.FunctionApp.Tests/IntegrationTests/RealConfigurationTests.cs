using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using JobScheduler.FunctionApp.Services;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

/// <summary>
/// Integration tests that verify the complete configuration pipeline works end-to-end.
/// These tests simulate the exact same setup as Program.cs using test-specific job names
/// to ensure the configuration system works without coupling to production job definitions.
/// </summary>
public class ConfigurationPipelineIntegrationTests
{
    [Fact]
    public void ConfigurationPipeline_ShouldWorkEndToEnd_WithTestConfiguration()
    {
        // Arrange - Use comprehensive test configuration that exercises the full pipeline
        using var setup = IndependentTestConfigurationHelper.CreateComprehensiveTestConfiguration();
        var jobConfigProvider = setup.GetJobConfigurationProvider();
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert - Verify the complete pipeline works
        options.Jobs.Should().ContainKey(TestJobNames.TestHealthCheck);
        options.Jobs.Should().ContainKey(TestJobNames.TestAuthJob);

        var healthCheckJob = jobConfigProvider.GetJobConfig(TestJobNames.TestHealthCheck);
        healthCheckJob.Should().NotBeNull();
        healthCheckJob.JobName.Should().Be(TestJobNames.TestHealthCheck);
        healthCheckJob.HttpMethod.Should().Be(HttpMethod.Get);
        healthCheckJob.AuthType.Should().Be(AuthenticationType.None);

        var authJob = jobConfigProvider.GetJobConfig(TestJobNames.TestAuthJob);
        authJob.Should().NotBeNull();
        authJob.JobName.Should().Be(TestJobNames.TestAuthJob);
        authJob.HttpMethod.Should().Be(HttpMethod.Post);
        authJob.AuthType.Should().Be(AuthenticationType.Bearer);
    }

    [Fact]
    public void ConfigurationValidation_ShouldPassWithValidTestConfiguration()
    {
        // Arrange - Use a valid test configuration
        using var setup = IndependentTestConfigurationHelper.CreateBasicTestConfiguration();
        var validator = TestJobSchedulerOptionsValidator.ForBasicHealthCheck();
        var options = setup.GetJobSchedulerOptions();

        // Act
        var validationResult = validator.Validate(null, options);

        // Assert - Validation should succeed with well-formed configuration
        validationResult.Succeeded.Should().BeTrue($"Validation failed: {string.Join(", ", validationResult.Failures ?? [])}");
    }

    [Fact]
    public void ConfigurationValidation_ShouldFailGracefullyWithBadConfiguration()
    {
        // Arrange - Configuration that should fail validation (missing expected jobs)
        using var setup = IndependentTestConfigurationHelper.CreateEmptyTestConfiguration();
        var validator = TestJobSchedulerOptionsValidator.ForBasicHealthCheck();
        var options = setup.GetJobSchedulerOptions();

        // Act
        var validationResult = validator.Validate(null, options);

        // Assert - Should fail gracefully with clear error messages
        validationResult.Succeeded.Should().BeFalse("Should fail when expected jobs are missing");
        validationResult.Failures.Should().Contain($"Job '{TestJobNames.TestHealthCheck}' is expected but missing from configuration.");
    }

    [Fact]
    public void OptionsJobConfigurationProvider_ShouldWorkWithTestConfiguration()
    {
        // Arrange - Verify that OptionsJobConfigurationProvider works with test data
        using var setup = IndependentTestConfigurationHelper.CreateComprehensiveTestConfiguration();
        var provider = setup.GetJobConfigurationProvider();

        // Act & Assert - Ensure we're using the correct provider type
        provider.Should().BeOfType<OptionsJobConfigurationProvider>("Should use OptionsJobConfigurationProvider for consistency with runtime");
        
        // Verify it can fetch test jobs correctly
        var allJobs = provider.GetAllJobConfigs().ToList();
        allJobs.Should().HaveCount(3, "Should have TestHealthCheck, TestAuthJob, and TestErrorJob");
        allJobs.Should().Contain(job => job.JobName == TestJobNames.TestHealthCheck);
        allJobs.Should().Contain(job => job.JobName == TestJobNames.TestAuthJob);
        allJobs.Should().Contain(job => job.JobName == TestJobNames.TestErrorJob);
    }
}
