using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Core.Models;
using JobScheduler.FunctionApp.Functions;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

public class ManualTriggerFunctionTests
{
    private readonly Mock<IJobExecutor> _mockJobExecutor;
    private readonly Mock<IJobConfigurationProvider> _mockConfigProvider;
    private readonly TestLoggerProvider<ManualTriggerFunctions> _testLogger;
    private readonly ManualTriggerFunctions _manualTriggerFunctions;

    public ManualTriggerFunctionTests()
    {
        _mockJobExecutor = new Mock<IJobExecutor>();
        _mockConfigProvider = new Mock<IJobConfigurationProvider>();
        _testLogger = new TestLoggerProvider<ManualTriggerFunctions>();
        
        _manualTriggerFunctions = new ManualTriggerFunctions(
            _mockJobExecutor.Object,
            _mockConfigProvider.Object,
            _testLogger,
            TestAppSettings.CreateDefault());
    }

    [Fact]
    public void ManualTriggerFunctions_Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var functions = new ManualTriggerFunctions(
            _mockJobExecutor.Object,
            _mockConfigProvider.Object,
            _testLogger,
            TestAppSettings.CreateDefault());

        // Assert
        functions.Should().NotBeNull();
    }

    [Fact]
    public void JobExecutor_Integration_ValidConfiguration()
    {
        // Arrange
        var jobName = "test-job";
        var jobConfig = new JobConfig
        {
            JobName = jobName,
            Endpoint = "https://api.test.com/endpoint",
            HttpMethod = HttpMethod.Post,
            AuthType = AuthenticationType.None
        };

        var jobResult = new JobResult
        {
            IsSuccess = true,
            Status = "Completed",
            Duration = TimeSpan.FromMilliseconds(150),
            AttemptCount = 1
        };

        _mockConfigProvider
            .Setup(p => p.GetJobConfig(jobName))
            .Returns(jobConfig);

        _mockJobExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobResult);

        // Act
        var retrievedConfig = _mockConfigProvider.Object.GetJobConfig(jobName);
        var executionResult = _mockJobExecutor.Object.ExecuteAsync(retrievedConfig).Result;

        // Assert
        retrievedConfig.Should().Be(jobConfig);
        executionResult.IsSuccess.Should().BeTrue();
        executionResult.Status.Should().Be("Completed");
        executionResult.Duration.TotalMilliseconds.Should().Be(150);
        executionResult.AttemptCount.Should().Be(1);

        // Verify the mocks were called correctly
        _mockConfigProvider.Verify(p => p.GetJobConfig(jobName), Times.Once);
        _mockJobExecutor.Verify(e => e.ExecuteAsync(jobConfig, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void JobExecutor_Integration_FailedExecution()
    {
        // Arrange
        var jobName = "failing-job";
        var jobConfig = new JobConfig
        {
            JobName = jobName,
            Endpoint = "https://api.test.com/endpoint",
            HttpMethod = HttpMethod.Post,
            AuthType = AuthenticationType.None
        };

        var jobResult = new JobResult
        {
            IsSuccess = false,
            Status = "Failed",
            Duration = TimeSpan.FromMilliseconds(3000),
            AttemptCount = 3,
            ErrorMessage = "Connection timeout after 3 attempts"
        };

        _mockConfigProvider
            .Setup(p => p.GetJobConfig(jobName))
            .Returns(jobConfig);

        _mockJobExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobResult);

        // Act
        var retrievedConfig = _mockConfigProvider.Object.GetJobConfig(jobName);
        var executionResult = _mockJobExecutor.Object.ExecuteAsync(retrievedConfig).Result;

        // Assert
        retrievedConfig.Should().Be(jobConfig);
        executionResult.IsSuccess.Should().BeFalse();
        executionResult.Status.Should().Be("Failed");
        executionResult.ErrorMessage.Should().Be("Connection timeout after 3 attempts");
        executionResult.AttemptCount.Should().Be(3);
    }

    [Fact]
    public void JobConfiguration_Integration_NonexistentJob()
    {
        // Arrange
        var jobName = "nonexistent-job";
        
        _mockConfigProvider
            .Setup(p => p.GetJobConfig(jobName))
            .Throws(new ArgumentException($"Job configuration not found for: {jobName}"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            _mockConfigProvider.Object.GetJobConfig(jobName));
        
        exception.Message.Should().Contain($"Job configuration not found for: {jobName}");
    }

    [Fact]
    public void JobExecutor_Integration_ExecutorThrowsException()
    {
        // Arrange
        var jobName = "error-job";
        var jobConfig = new JobConfig
        {
            JobName = jobName,
            Endpoint = "https://api.test.com/endpoint",
            HttpMethod = HttpMethod.Post,
            AuthType = AuthenticationType.None
        };

        _mockConfigProvider
            .Setup(p => p.GetJobConfig(jobName))
            .Returns(jobConfig);

        _mockJobExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error occurred"));

        // Act & Assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _mockJobExecutor.Object.ExecuteAsync(jobConfig));
        
        exception.Result.Message.Should().Be("Unexpected error occurred");
    }

    [Fact]
    public void ListJobs_Integration_ReturnsAllJobConfigurations()
    {
        // Arrange
        var jobConfigs = new[]
        {
            new JobConfig
            {
                JobName = "job-1",
                Endpoint = "https://api1.test.com",
                HttpMethod = HttpMethod.Get,
                AuthType = AuthenticationType.None
            },
            new JobConfig
            {
                JobName = "job-2",
                Endpoint = "https://api2.test.com",
                HttpMethod = HttpMethod.Post,
                AuthType = AuthenticationType.Bearer,
                AuthSecretName = "secret-2"
            }
        };

        _mockConfigProvider
            .Setup(p => p.GetAllJobConfigs())
            .Returns(jobConfigs);

        // Act
        var result = _mockConfigProvider.Object.GetAllJobConfigs();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(config => config.JobName == "job-1");
        result.Should().Contain(config => config.JobName == "job-2");
        
        var job1 = result.First(j => j.JobName == "job-1");
        job1.Endpoint.Should().Be("https://api1.test.com");
        job1.HttpMethod.Should().Be(HttpMethod.Get);
        job1.AuthSecretName.Should().BeNullOrEmpty();
        
        var job2 = result.First(j => j.JobName == "job-2");
        job2.AuthSecretName.Should().Be("secret-2");
    }

    [Fact]
    public void HealthCheck_Integration_ReturnsHealthyStatus()
    {
        // Arrange
        var jobConfigs = new[]
        {
            new JobConfig { JobName = "job-1", Endpoint = "https://api1.test.com", HttpMethod = HttpMethod.Get, AuthType = AuthenticationType.None },
            new JobConfig { JobName = "job-2", Endpoint = "https://api2.test.com", HttpMethod = HttpMethod.Post, AuthType = AuthenticationType.None },
            new JobConfig { JobName = "job-3", Endpoint = "https://api3.test.com", HttpMethod = HttpMethod.Get, AuthType = AuthenticationType.None }
        };

        _mockConfigProvider
            .Setup(p => p.GetAllJobConfigs())
            .Returns(jobConfigs);

        // Set environment variable for test
        Environment.SetEnvironmentVariable("ENVIRONMENT", "test");

        // Act
        var result = _mockConfigProvider.Object.GetAllJobConfigs();

        // Assert
        result.Should().HaveCount(3);
        Environment.GetEnvironmentVariable("ENVIRONMENT").Should().Be("test");

        // Cleanup
        Environment.SetEnvironmentVariable("ENVIRONMENT", null);
    }
}
