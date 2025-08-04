using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using System.Net;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests
{
    public class JobExecutorIntegrationTests : IDisposable
    {
        private readonly TestHttpMessageHandler _httpHandler;
        private readonly HttpClient _httpClient;
        private readonly TestSecretManager _secretManager;
        private readonly TestJobLogger _jobLogger;
        private readonly TestJobMetrics _jobMetrics;
        private readonly JobExecutor _jobExecutor;

        public JobExecutorIntegrationTests()
        {
            _httpHandler = new TestHttpMessageHandler();
            _httpClient = new HttpClient(_httpHandler);
            var httpClientFactory = new TestHttpClientFactory(_httpClient);
            _secretManager = new TestSecretManager();
            _jobLogger = new TestJobLogger();
            _jobMetrics = new TestJobMetrics();
            
            _jobExecutor = new JobExecutor(httpClientFactory, _secretManager, _jobLogger, _jobMetrics);
        }

        [Fact]
        public async Task CompleteWorkflow_EndToEnd_SuccessScenario()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithJobName("complete-workflow-test")
                .WithEndpoint("https://api.example.com/data")
                .WithHttpMethod(HttpMethod.Get)
                .WithAuthType(AuthenticationType.None)
                .Build();

            _httpHandler.AddResponse(HttpStatusCode.OK, "{\"message\":\"success\",\"data\":[1,2,3]}");

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be("Completed");
            result.AttemptCount.Should().Be(1);
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);

            // Verify logging occurred
            _jobLogger.Logs.Should().HaveCount(2);
            _jobLogger.Logs[0].Message.Should().Be("Job started");
            _jobLogger.Logs[1].Message.Should().Be("Job completed successfully");

            // Verify metrics recorded
            _jobMetrics.RecordedSuccesses.Should().HaveCount(1);
            _jobMetrics.RecordedFailures.Should().HaveCount(0);
        }

        [Fact]
        public async Task RetryLogic_Integration_SucceedsAfterRetries()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithJobName("retry-test")
                .WithRetryPolicy(maxAttempts: 3, baseDelayMs: 50)
                .Build();

            // First two attempts fail, third succeeds
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable);
            _httpHandler.AddResponse(HttpStatusCode.BadGateway);
            _httpHandler.AddResponse(HttpStatusCode.OK, "{\"status\":\"recovered\"}");

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.AttemptCount.Should().Be(3);
            _httpHandler.Requests.Should().HaveCount(3);

            // Verify retry logging
            var warningLogs = _jobLogger.Logs.Where(l => l.LogLevel == LogLevel.Warning).ToList();
            warningLogs.Should().HaveCount(2);
            warningLogs[0].Message.Should().Contain("Attempt 1 failed");
            warningLogs[1].Message.Should().Contain("Attempt 2 failed");
        }

        [Fact]
        public async Task FailureAfterRetries_Integration_ReturnsFailure()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithJobName("failure-test")
                .WithRetryPolicy(maxAttempts: 2, baseDelayMs: 10)
                .Build();

            // All attempts fail
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable);
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable);

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Status.Should().Be("Failed");
            result.AttemptCount.Should().Be(2);
            _httpHandler.Requests.Should().HaveCount(2);

            // Verify failure metrics
            _jobMetrics.RecordedFailures.Should().HaveCount(1);
            _jobMetrics.RecordedSuccesses.Should().HaveCount(0);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _httpHandler?.Reset();
        }
    }
}
