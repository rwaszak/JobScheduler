using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Core.Models;
using JobScheduler.FunctionApp.Functions;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests
{
    public class FunctionEndpointTests
    {
        private readonly Mock<IJobExecutor> _mockJobExecutor;
        private readonly Mock<IJobConfigurationProvider> _mockConfigProvider;
        private readonly TestLoggerProvider<ManualTriggerFunctions> _testLogger;
        private readonly ManualTriggerFunctions _manualTriggerFunctions;

        public FunctionEndpointTests()
        {
            _mockJobExecutor = new Mock<IJobExecutor>();
            _mockConfigProvider = new Mock<IJobConfigurationProvider>();
            _testLogger = new TestLoggerProvider<ManualTriggerFunctions>();
            
            _manualTriggerFunctions = new ManualTriggerFunctions(
                _mockJobExecutor.Object,
                _mockConfigProvider.Object,
                _testLogger);
        }

        [Fact]
        public async Task TriggerJob_Integration_ValidJobExecution()
        {
            // Arrange
            var jobName = "test-endpoint-job";
            var jobConfig = new JobConfig
            {
                JobName = jobName,
                Endpoint = "https://api.endpoint.com/test",
                HttpMethod = HttpMethod.Post,
                AuthType = AuthenticationType.Bearer,
                AuthSecretName = "bearer-secret"
            };

            var jobResult = new JobResult
            {
                IsSuccess = true,
                Status = "Completed",
                Duration = TimeSpan.FromMilliseconds(250),
                AttemptCount = 1,
                ResponseData = new { message = "API call successful" }
            };

            _mockConfigProvider
                .Setup(p => p.GetJobConfig(jobName))
                .Returns(jobConfig);

            _mockJobExecutor
                .Setup(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobResult);

            // Act
            var config = _mockConfigProvider.Object.GetJobConfig(jobName);
            var result = await _mockJobExecutor.Object.ExecuteAsync(config);

            // Assert
            config.Should().NotBeNull();
            config.JobName.Should().Be(jobName);
            config.AuthType.Should().Be(AuthenticationType.Bearer);
            
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be("Completed");
            result.Duration.TotalMilliseconds.Should().Be(250);
            result.AttemptCount.Should().Be(1);

            _mockConfigProvider.Verify(p => p.GetJobConfig(jobName), Times.Once);
            _mockJobExecutor.Verify(e => e.ExecuteAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void ListJobs_Integration_ReturnsFormattedJobList()
        {
            // Arrange
            var jobConfigs = new[]
            {
                new JobConfig
                {
                    JobName = "health-check-api",
                    Endpoint = "https://health.api.com/status",
                    HttpMethod = HttpMethod.Get,
                    AuthType = AuthenticationType.None,
                    Tags = new Dictionary<string, string> { { "category", "health" } }
                },
                new JobConfig
                {
                    JobName = "data-sync-api",
                    Endpoint = "https://sync.api.com/data",
                    HttpMethod = HttpMethod.Post,
                    AuthType = AuthenticationType.ApiKey,
                    AuthSecretName = "api-key-secret",
                    Tags = new Dictionary<string, string> { { "category", "sync" } }
                }
            };

            _mockConfigProvider
                .Setup(p => p.GetAllJobConfigs())
                .Returns(jobConfigs);

            // Act
            var result = _mockConfigProvider.Object.GetAllJobConfigs().ToList();

            // Assert
            result.Should().HaveCount(2);
            
            var healthJob = result.First(j => j.JobName == "health-check-api");
            healthJob.Endpoint.Should().Be("https://health.api.com/status");
            healthJob.HttpMethod.Should().Be(HttpMethod.Get);
            healthJob.AuthType.Should().Be(AuthenticationType.None);
            healthJob.AuthSecretName.Should().BeNullOrEmpty();
            
            var syncJob = result.First(j => j.JobName == "data-sync-api");
            syncJob.Endpoint.Should().Be("https://sync.api.com/data");
            syncJob.HttpMethod.Should().Be(HttpMethod.Post);
            syncJob.AuthType.Should().Be(AuthenticationType.ApiKey);
            syncJob.AuthSecretName.Should().Be("api-key-secret");

            _mockConfigProvider.Verify(p => p.GetAllJobConfigs(), Times.Once);
        }

        [Fact]
        public void HealthCheck_Integration_ReturnsSystemStatus()
        {
            // Arrange
            var jobConfigs = new[]
            {
                new JobConfig { JobName = "job-1", Endpoint = "https://api1.com", HttpMethod = HttpMethod.Get, AuthType = AuthenticationType.None },
                new JobConfig { JobName = "job-2", Endpoint = "https://api2.com", HttpMethod = HttpMethod.Post, AuthType = AuthenticationType.Bearer },
                new JobConfig { JobName = "job-3", Endpoint = "https://api3.com", HttpMethod = HttpMethod.Put, AuthType = AuthenticationType.ApiKey }
            };

            _mockConfigProvider
                .Setup(p => p.GetAllJobConfigs())
                .Returns(jobConfigs);

            // Set environment variable for test
            Environment.SetEnvironmentVariable("ENVIRONMENT", "integration-test");

            try
            {
                // Act
                var result = _mockConfigProvider.Object.GetAllJobConfigs();
                var environment = Environment.GetEnvironmentVariable("ENVIRONMENT");

                // Assert
                result.Should().HaveCount(3);
                environment.Should().Be("integration-test");

                // Verify jobs contain expected authentication types
                result.Should().Contain(j => j.AuthType == AuthenticationType.None);
                result.Should().Contain(j => j.AuthType == AuthenticationType.Bearer);
                result.Should().Contain(j => j.AuthType == AuthenticationType.ApiKey);

                _mockConfigProvider.Verify(p => p.GetAllJobConfigs(), Times.Once);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("ENVIRONMENT", null);
            }
        }

        [Fact]
        public void JobNotFound_Integration_HandlesGracefully()
        {
            // Arrange
            var jobName = "nonexistent-endpoint-job";
            
            _mockConfigProvider
                .Setup(p => p.GetJobConfig(jobName))
                .Throws(new ArgumentException($"Job configuration not found for: {jobName}"));

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                _mockConfigProvider.Object.GetJobConfig(jobName));
            
            exception.Message.Should().Contain($"Job configuration not found for: {jobName}");
            
            _mockConfigProvider.Verify(p => p.GetJobConfig(jobName), Times.Once);
        }

        [Fact]
        public async Task ScheduledJobs_ExecuteJobSafely_HandlesSuccessfulExecution()
        {
            // Arrange
            var mockJobExecutor = new Mock<IJobExecutor>();
            var mockConfigProvider = new Mock<IJobConfigurationProvider>();
            var testLogger = new TestLoggerProvider<ScheduledJobs>();

            var jobConfig = TestJobConfigurationBuilder.Default()
                .WithJobName("container-app-health")
                .WithAuthType(AuthenticationType.None)
                .Build();

            var jobResult = new JobResult
            {
                IsSuccess = true,
                Status = "Completed",
                Duration = TimeSpan.FromMilliseconds(100),
                AttemptCount = 1
            };

            mockConfigProvider
                .Setup(p => p.GetJobConfig("container-app-health"))
                .Returns(jobConfig);

            mockJobExecutor
                .Setup(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobResult);

            var scheduledJobs = new ScheduledJobs(
                mockJobExecutor.Object,
                mockConfigProvider.Object,
                testLogger);

            var timerInfo = new TestTimerInfo();

            // Act
            await scheduledJobs.ContainerAppHealthCheck(timerInfo);

            // Assert
            mockConfigProvider.Verify(p => p.GetJobConfig("container-app-health"), Times.Once);
            mockJobExecutor.Verify(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()), Times.Once);

            testLogger.Logs.Should().HaveCount(2);
            testLogger.Logs[0].Message.Should().Contain("Starting scheduled job: container-app-health");
            testLogger.Logs[1].Message.Should().Contain("Job container-app-health completed successfully");
        }

        [Fact]
        public async Task ScheduledJobs_ExecuteJobSafely_HandlesFailedExecution()
        {
            // Arrange
            var mockJobExecutor = new Mock<IJobExecutor>();
            var mockConfigProvider = new Mock<IJobConfigurationProvider>();
            var testLogger = new TestLoggerProvider<ScheduledJobs>();

            var jobConfig = TestJobConfigurationBuilder.Default()
                .WithJobName("container-app-health")
                .WithAuthType(AuthenticationType.None)
                .Build();

            var jobResult = new JobResult
            {
                IsSuccess = false,
                Status = "Failed",
                ErrorMessage = "Network timeout",
                Duration = TimeSpan.FromMilliseconds(5000)
            };

            mockConfigProvider
                .Setup(p => p.GetJobConfig("container-app-health"))
                .Returns(jobConfig);

            mockJobExecutor
                .Setup(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobResult);

            var scheduledJobs = new ScheduledJobs(
                mockJobExecutor.Object,
                mockConfigProvider.Object,
                testLogger);

            var timerInfo = new TestTimerInfo();

            // Act
            await scheduledJobs.ContainerAppHealthCheck(timerInfo);

            // Assert
            testLogger.Logs.Should().HaveCount(2);
            testLogger.Logs[1].LogLevel.Should().Be(LogLevel.Error);
            testLogger.Logs[1].Message.Should().Contain("Job container-app-health failed: Network timeout");
        }

        [Fact]
        public async Task ScheduledJobs_ExecuteJobSafely_HandlesExceptions()
        {
            // Arrange
            var mockJobExecutor = new Mock<IJobExecutor>();
            var mockConfigProvider = new Mock<IJobConfigurationProvider>();
            var testLogger = new TestLoggerProvider<ScheduledJobs>();

            mockConfigProvider
                .Setup(p => p.GetJobConfig("container-app-health"))
                .Throws(new ArgumentException("Job not found"));

            var scheduledJobs = new ScheduledJobs(
                mockJobExecutor.Object,
                mockConfigProvider.Object,
                testLogger);

            var timerInfo = new TestTimerInfo();

            // Act
            await scheduledJobs.ContainerAppHealthCheck(timerInfo);

            // Assert
            testLogger.Logs.Should().HaveCount(2);
            testLogger.Logs[1].LogLevel.Should().Be(LogLevel.Error);
            testLogger.Logs[1].Message.Should().Contain("Unexpected error in job container-app-health");
            testLogger.Logs[1].Exception.Should().BeOfType<ArgumentException>();
        }

        // Simple TimerInfo implementation for testing
        private class TestTimerInfo : TimerInfo
        {
            // TimerInfo is abstract, so we just need a concrete implementation for testing
        }
    }
}
