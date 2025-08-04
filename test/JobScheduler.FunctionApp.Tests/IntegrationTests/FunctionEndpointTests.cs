using FluentAssertions;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Core.Models;
using JobScheduler.FunctionApp.Functions;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests
{
    public class FunctionEndpointTests
    {
        [Fact]
        public async Task ScheduledJobs_ExecuteJobSafely_HandlesSuccessfulExecution()
        {
            // Arrange
            var mockJobExecutor = new Mock<IJobExecutor>();
            var mockConfigProvider = new Mock<IJobConfigurationProvider>();
            var testLogger = new TestLoggerProvider<ScheduledJobs>();

            var jobConfig = TestJobConfigurationBuilder.Default()
                .WithJobName("test-job")
                .Build();

            var jobResult = new JobResult
            {
                IsSuccess = true,
                Status = "Completed",
                Duration = TimeSpan.FromMilliseconds(100),
                AttemptCount = 1
            };

            mockConfigProvider
                .Setup(p => p.GetJobConfig("test-job"))
                .Returns(jobConfig);

            mockJobExecutor
                .Setup(e => e.ExecuteAsync(It.IsAny<JobConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobResult);

            var scheduledJobs = new ScheduledJobs(
                mockJobExecutor.Object,
                mockConfigProvider.Object,
                testLogger);

            var timerInfo = new TimerInfo();

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
                .WithJobName("failing-job")
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

            var timerInfo = new TimerInfo();

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

            var timerInfo = new TimerInfo();

            // Act
            await scheduledJobs.ContainerAppHealthCheck(timerInfo);

            // Assert
            testLogger.Logs.Should().HaveCount(2);
            testLogger.Logs[1].LogLevel.Should().Be(LogLevel.Error);
            testLogger.Logs[1].Message.Should().Contain("Unexpected error in job container-app-health");
            testLogger.Logs[1].Exception.Should().BeOfType<ArgumentException>();
        }

        // Simple TimerInfo implementation for testing
        private class TestTimerInfo : Microsoft.Azure.Functions.Worker.TimerInfo
        {
        }
    }
}
