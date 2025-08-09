using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

/// <summary>
/// Integration tests for job validation framework using test-specific job names.
/// These tests are independent of production JobNames constants and appsettings.json,
/// allowing users to add new jobs without breaking existing tests.
/// </summary>
public class JobValidationFrameworkTests
{
    [Fact]
    public void ValidationFramework_WithValidTestConfiguration_ShouldSucceed()
    {
        // Arrange - Valid test configuration
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [TestJobNames.TestHealthCheck] = new JobDefinition
                {
                    JobName = TestJobNames.TestHealthCheck,
                    Endpoint = "https://api.example.com/health",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30,
                    RetryPolicy = new RetryPolicyOptions
                    {
                        MaxAttempts = 3,
                        BaseDelayMs = 1000,
                        BackoffMultiplier = 2.0,
                        MaxDelayMs = 30000,
                        RetryableStatusCodes = new List<int> { 429, 502, 503, 504 }
                    }
                }
            },
            Logging = new LoggingOptions
            {
                DatadogSite = "us3.datadoghq.com"
            }
        };

        var validator = TestJobSchedulerOptionsValidator.ForBasicHealthCheck();

        // Act & Assert - Should not throw
        var result = validator.Validate(null, options);
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void ValidationFramework_WithMissingJobConfiguration_ShouldFailValidation()
    {
        // Arrange - Missing required job configuration (empty configuration)
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>(),
            // No jobs configured, but validator expects TestHealthCheck
            Logging = new LoggingOptions
            {
                DatadogSite = "us3.datadoghq.com"
            }
        };

        var validator = TestJobSchedulerOptionsValidator.ForBasicHealthCheck();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{TestJobNames.TestHealthCheck}' is expected but missing from configuration.");
    }

    [Fact]
    public void ValidationFramework_WithExtraJobConfiguration_ShouldFailValidation()
    {
        // Arrange - Extra job configuration not expected by validator
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [TestJobNames.TestHealthCheck] = new JobDefinition
                {
                    JobName = TestJobNames.TestHealthCheck,
                    Endpoint = "https://api.example.com/health",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30,
                    RetryPolicy = new RetryPolicyOptions
                    {
                        MaxAttempts = 3,
                        BaseDelayMs = 1000,
                        BackoffMultiplier = 2.0,
                        MaxDelayMs = 30000,
                        RetryableStatusCodes = new List<int> { 429, 502, 503, 504 }
                    }
                },
                ["unexpected-job"] = new JobDefinition
                {
                    JobName = "unexpected-job",
                    Endpoint = "https://api.example.com/unexpected",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30,
                    RetryPolicy = new RetryPolicyOptions
                    {
                        MaxAttempts = 3,
                        BaseDelayMs = 1000,
                        BackoffMultiplier = 2.0,
                        MaxDelayMs = 30000,
                        RetryableStatusCodes = new List<int> { 429, 502, 503, 504 }
                    }
                }
            },
            Logging = new LoggingOptions
            {
                DatadogSite = "us3.datadoghq.com"
            }
        };

        var validator = TestJobSchedulerOptionsValidator.ForBasicHealthCheck();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Job 'unexpected-job' is configured but not expected in this test scenario.");
    }

    [Fact]
    public void TestJobNamesConstants_ShouldHaveExpectedValues()
    {
        // Arrange & Act - Verify test constants have expected values
        var testHealthCheck = TestJobNames.TestHealthCheck;
        var testAuthJob = TestJobNames.TestAuthJob;

        // Assert - These tests ensure test constants remain stable
        testHealthCheck.Should().Be("testHealthCheck");
        testAuthJob.Should().Be("testAuthJob");
        
        // Verify all test jobs are unique
        TestJobNames.AllTestJobs.Should().OnlyHaveUniqueItems("Test job names should not have duplicates");
        TestJobNames.AllTestJobs.Should().NotBeEmpty("At least one test job name should be defined");
    }

    [Fact]
    public void ValidationFramework_WithMultipleJobs_ShouldValidateAll()
    {
        // Arrange - Configuration with multiple test jobs
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [TestJobNames.TestHealthCheck] = new JobDefinition
                {
                    JobName = TestJobNames.TestHealthCheck,
                    Endpoint = "https://api.example.com/health",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30,
                    RetryPolicy = new RetryPolicyOptions
                    {
                        MaxAttempts = 3,
                        BaseDelayMs = 1000,
                        BackoffMultiplier = 2.0,
                        MaxDelayMs = 30000,
                        RetryableStatusCodes = new List<int> { 429, 502, 503, 504 }
                    }
                },
                [TestJobNames.TestAuthJob] = new JobDefinition
                {
                    JobName = TestJobNames.TestAuthJob,
                    Endpoint = "https://api.example.com/auth",
                    HttpMethod = HttpMethod.Post,
                    AuthType = AuthenticationType.Bearer,
                    AuthSecretName = "TEST_TOKEN",
                    TimeoutSeconds = 60,
                    RetryPolicy = new RetryPolicyOptions
                    {
                        MaxAttempts = 3,
                        BaseDelayMs = 1000,
                        BackoffMultiplier = 2.0,
                        MaxDelayMs = 30000,
                        RetryableStatusCodes = new List<int> { 429, 502, 503, 504 }
                    }
                }
            },
            Logging = new LoggingOptions
            {
                DatadogSite = "us3.datadoghq.com"
            }
        };

        var validator = new TestJobSchedulerOptionsValidator(TestJobNames.TestHealthCheck, TestJobNames.TestAuthJob);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void ValidationFramework_ConfigurationIntegration_ShouldWork()
    {
        // This test verifies the framework can work with configuration providers
        
        // Arrange
        using var setup = IndependentTestConfigurationHelper.CreateBasicTestConfiguration();
        var options = setup.GetJobSchedulerOptions();
        var validator = TestJobSchedulerOptionsValidator.ForBasicHealthCheck();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
        options.Jobs.Should().ContainKey(TestJobNames.TestHealthCheck);
        
        var healthCheckJob = options.Jobs[TestJobNames.TestHealthCheck];
        healthCheckJob.JobName.Should().Be(TestJobNames.TestHealthCheck);
        healthCheckJob.HttpMethod.Should().Be(HttpMethod.Get);
        healthCheckJob.AuthType.Should().Be(AuthenticationType.None);
    }
}
