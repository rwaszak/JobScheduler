using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

public class JobNameValidationIntegrationTests
{
    [Fact]
    public void ApplicationStartup_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange - Valid configuration that matches JobNames constants
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [JobNames.ContainerAppHealth] = new JobDefinition
                {
                    JobName = JobNames.ContainerAppHealth,
                    Endpoint = "https://api.example.com/health",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30
                },
                [JobNames.DailyBatch] = new JobDefinition
                {
                    JobName = JobNames.DailyBatch,
                    Endpoint = "https://api.example.com/batch",
                    HttpMethod = HttpMethod.Post,
                    AuthType = AuthenticationType.Bearer,
                    AuthSecretName = "BATCH_TOKEN",
                    TimeoutSeconds = 120
                }
            }
        };

        var validator = new ValidateJobSchedulerOptions();

        // Act & Assert - Should not throw
        var result = validator.Validate(null, options);
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void ApplicationStartup_WithMissingJobConfiguration_ShouldFailValidation()
    {
        // Arrange - Missing one required job configuration
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [JobNames.ContainerAppHealth] = new JobDefinition
                {
                    JobName = JobNames.ContainerAppHealth,
                    Endpoint = "https://api.example.com/health",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30
                }
                // Missing JobNames.DailyBatch configuration
            }
        };

        var validator = new ValidateJobSchedulerOptions();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{JobNames.DailyBatch}' is defined in JobNames constants but missing from configuration.");
    }

    [Fact]
    public void ApplicationStartup_WithExtraJobConfiguration_ShouldFailValidation()
    {
        // Arrange - Extra job configuration not defined in constants
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [JobNames.ContainerAppHealth] = new JobDefinition
                {
                    JobName = JobNames.ContainerAppHealth,
                    Endpoint = "https://api.example.com/health",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30
                },
                [JobNames.DailyBatch] = new JobDefinition
                {
                    JobName = JobNames.DailyBatch,
                    Endpoint = "https://api.example.com/batch",
                    HttpMethod = HttpMethod.Post,
                    AuthType = AuthenticationType.Bearer,
                    AuthSecretName = "BATCH_TOKEN",
                    TimeoutSeconds = 120
                },
                ["unexpected-job"] = new JobDefinition
                {
                    JobName = "unexpected-job",
                    Endpoint = "https://api.example.com/unexpected",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30
                }
            }
        };

        var validator = new ValidateJobSchedulerOptions();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Job 'unexpected-job' is configured but not defined in JobNames constants. Consider adding it for type safety.");
    }

    [Fact]
    public void JobNamesConstants_ShouldMatchExpectedValues()
    {
        // Arrange & Act - Verify the constants have expected values
        var containerHealth = JobNames.ContainerAppHealth;
        var dailyBatch = JobNames.DailyBatch;

        // Assert - These tests will fail if constants are accidentally changed
        containerHealth.Should().Be("container-app-health");
        dailyBatch.Should().Be("daily-batch");
    }

    [Fact]
    public void JobNamesConstants_ShouldNotHaveDuplicates()
    {
        // Arrange - Get all job name constant values using reflection
        var jobNameConstants = typeof(JobNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        // Act & Assert - Verify no duplicate values
        jobNameConstants.Should().OnlyHaveUniqueItems("Job name constants should not have duplicate values");
        jobNameConstants.Should().NotBeEmpty("At least one job name constant should be defined");
    }

    [Fact]
    public void GetDefinedJobNames_ShouldReturnAllConstants()
    {
        // This test uses reflection to verify our validation logic can find all constants
        
        // Arrange - Expected job names from constants
        var expectedJobNames = new HashSet<string>
        {
            JobNames.ContainerAppHealth,
            JobNames.DailyBatch
        };

        // Act - Use the same reflection logic as the validator
        var actualJobNames = typeof(JobNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet();

        // Assert
        actualJobNames.Should().BeEquivalentTo(expectedJobNames);
        actualJobNames.Should().HaveCount(2, "There should be exactly 2 job name constants defined");
    }
}
