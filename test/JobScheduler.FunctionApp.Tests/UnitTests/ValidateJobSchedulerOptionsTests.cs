using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests;

public class ValidateJobSchedulerOptionsTests
{
    private readonly ValidateJobSchedulerOptions _validator = new();

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange - Valid configuration with all required job names
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

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithMissingJobConstant_ReturnsFailure()
    {
        // Arrange - Missing one of the required job constants
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
                // Missing JobNames.DailyBatch
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{JobNames.DailyBatch}' is defined in JobNames constants but missing from configuration.");
    }

    [Fact]
    public void Validate_WithExtraConfiguredJob_ReturnsFailure()
    {
        // Arrange - Configuration has a job that doesn't have a constant
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
                ["unknown-job"] = new JobDefinition
                {
                    JobName = "unknown-job",
                    Endpoint = "https://api.example.com/unknown",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30
                }
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Job 'unknown-job' is configured but not defined in JobNames constants. Consider adding it for type safety.");
    }

    [Fact]
    public void Validate_WithInvalidEndpoint_ReturnsFailure()
    {
        // Arrange - Invalid endpoint URL
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [JobNames.ContainerAppHealth] = new JobDefinition
                {
                    JobName = JobNames.ContainerAppHealth,
                    Endpoint = "not-a-valid-url",
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

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{JobNames.ContainerAppHealth}': Invalid endpoint URL format.");
    }

    [Fact]
    public void Validate_WithBearerAuthButMissingSecretName_ReturnsFailure()
    {
        // Arrange - Bearer auth without secret name
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
                    AuthSecretName = "", // Missing secret name
                    TimeoutSeconds = 120
                }
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain($"Job '{JobNames.DailyBatch}': AuthSecretName is required when AuthType is 'bearer'.");
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllFailures()
    {
        // Arrange - Multiple validation errors
        var options = new JobSchedulerOptions
        {
            Jobs = new Dictionary<string, JobDefinition>
            {
                [JobNames.ContainerAppHealth] = new JobDefinition
                {
                    JobName = JobNames.ContainerAppHealth,
                    Endpoint = "invalid-url",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    TimeoutSeconds = 30
                },
                ["unknown-job"] = new JobDefinition
                {
                    JobName = "unknown-job",
                    Endpoint = "https://api.example.com/unknown",
                    HttpMethod = HttpMethod.Post,
                    AuthType = AuthenticationType.Bearer,
                    AuthSecretName = "", // Missing secret name
                    TimeoutSeconds = 30
                }
                // Missing JobNames.DailyBatch
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().HaveCountGreaterThan(1);
        result.Failures.Should().Contain($"Job '{JobNames.ContainerAppHealth}': Invalid endpoint URL format.");
        result.Failures.Should().Contain($"Job '{JobNames.DailyBatch}' is defined in JobNames constants but missing from configuration.");
        result.Failures.Should().Contain("Job 'unknown-job' is configured but not defined in JobNames constants. Consider adding it for type safety.");
        result.Failures.Should().Contain("Job 'unknown-job': AuthSecretName is required when AuthType is 'bearer'.");
    }
}
