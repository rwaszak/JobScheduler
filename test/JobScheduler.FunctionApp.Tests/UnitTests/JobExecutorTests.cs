using FluentAssertions;
using JobScheduler.FunctionApp.Core;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using System.Net;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests
{
    public class JobExecutorTests : IDisposable
    {
        private readonly TestHttpMessageHandler _httpHandler;
        private readonly HttpClient _httpClient;
        private readonly TestSecretManager _secretManager;
        private readonly TestJobLogger _jobLogger;
        private readonly TestJobMetrics _jobMetrics;
        private readonly JobExecutor _jobExecutor;

        public JobExecutorTests()
        {
            _httpHandler = new TestHttpMessageHandler();
            _httpClient = new HttpClient(_httpHandler);
            _secretManager = new TestSecretManager();
            _jobLogger = new TestJobLogger();
            _jobMetrics = new TestJobMetrics();
            
            _jobExecutor = new JobExecutor(_httpClient, _secretManager, _jobLogger, _jobMetrics);
        }

        [Fact]
        public async Task ExecuteAsync_SuccessfulJob_ReturnsSuccessResult()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithJobName("test-success")
                .WithEndpoint("https://api.test.com/success")
                .Build();

            _httpHandler.AddResponse(HttpStatusCode.OK, "{\"status\":\"success\"}");

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be("Completed");
            result.AttemptCount.Should().Be(1);
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            result.ErrorMessage.Should().BeNull();

            // Verify HTTP call
            _httpHandler.Requests.Should().HaveCount(1);
            var request = _httpHandler.GetRequest(0);
            request.Method.ToString().Should().Be("POST");
            request.RequestUri.ToString().Should().Be("https://api.test.com/success");

            // Verify logging
            _jobLogger.Logs.Should().HaveCount(2);
            _jobLogger.Logs[0].Message.Should().Be("Job started");
            _jobLogger.Logs[1].Message.Should().Be("Job completed successfully");

            // Verify metrics
            _jobMetrics.SuccessMetrics.Should().HaveCount(1);
            _jobMetrics.FailureMetrics.Should().HaveCount(0);
        }

        [Fact]
        public async Task ExecuteAsync_WithBearerAuth_AddsAuthorizationHeader()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithAuthType("bearer")
                .WithAuthSecret("test-secret")
                .Build();

            _secretManager.AddSecret("test-secret", "my-bearer-token");
            _httpHandler.AddResponse(HttpStatusCode.OK, "{}");

            // Act
            await _jobExecutor.ExecuteAsync(config);

            // Assert
            var request = _httpHandler.GetRequest(0);
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("my-bearer-token");
        }

        [Fact]
        public async Task ExecuteAsync_WithApiKeyAuth_AddsApiKeyHeader()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithAuthType("apikey")
                .WithAuthSecret("api-key-secret")
                .Build();

            _secretManager.AddSecret("api-key-secret", "my-api-key");
            _httpHandler.AddResponse(HttpStatusCode.OK, "{}");

            // Act
            await _jobExecutor.ExecuteAsync(config);

            // Assert
            var request = _httpHandler.GetRequest(0);
            request.Headers.Should().ContainKey("X-API-Key");
            request.Headers.GetValues("X-API-Key").First().Should().Be("my-api-key");
        }

        [Fact]
        public async Task ExecuteAsync_WithNoAuth_DoesNotAddAuthHeaders()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithAuthType("none")
                .Build();

            _httpHandler.AddResponse(HttpStatusCode.OK, "{}");

            // Act
            await _jobExecutor.ExecuteAsync(config);

            // Assert
            var request = _httpHandler.GetRequest(0);
            request.Headers.Authorization.Should().BeNull();
            request.Headers.Should().NotContainKey("X-API-Key");
        }

        [Fact]
        public async Task ExecuteAsync_WithCustomHeaders_AddsHeaders()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", "custom-value" },
                { "X-Request-ID", "12345" }
            };

            var config = TestJobConfigurationBuilder.Default()
                .WithHeaders(headers)
                .Build();

            _httpHandler.AddResponse(HttpStatusCode.OK, "{}");

            // Act
            await _jobExecutor.ExecuteAsync(config);

            // Assert
            var request = _httpHandler.GetRequest(0);
            request.Headers.Should().ContainKey("X-Custom-Header");
            request.Headers.Should().ContainKey("X-Request-ID");
            request.Headers.GetValues("X-Custom-Header").First().Should().Be("custom-value");
            request.Headers.GetValues("X-Request-ID").First().Should().Be("12345");
        }

        [Fact]
        public async Task ExecuteAsync_WithRequestBody_SerializesBody()
        {
            // Arrange
            var requestBody = new { name = "test", value = 42 };
            var config = TestJobConfigurationBuilder.Default()
                .WithRequestBody(requestBody)
                .Build();

            _httpHandler.AddResponse(HttpStatusCode.OK, "{}");

            // Act
            await _jobExecutor.ExecuteAsync(config);

            // Assert
            var request = _httpHandler.GetRequest(0);
            request.Content.Should().NotBeNull();
            
            var content = await request.Content.ReadAsStringAsync();
            content.Should().Contain("\"name\":\"test\"");
            content.Should().Contain("\"value\":42");
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        public async Task ExecuteAsync_WithDifferentHttpMethods_UsesCorrectMethod(string httpMethod)
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithHttpMethod(httpMethod)
                .Build();

            _httpHandler.AddResponse(HttpStatusCode.OK, "{}");

            // Act
            await _jobExecutor.ExecuteAsync(config);

            // Assert
            var request = _httpHandler.GetRequest(0);
            request.Method.ToString().Should().Be(httpMethod);
        }

        [Fact]
        public async Task ExecuteAsync_WithRetries_RetriesOnFailure()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithRetryPolicy(maxAttempts: 3, baseDelayMs: 10) // Fast retries for testing
                .Build();

            // First two calls fail, third succeeds
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable);
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable);
            _httpHandler.AddResponse(HttpStatusCode.OK, "{\"status\":\"success\"}");

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.AttemptCount.Should().Be(3);
            _httpHandler.Requests.Should().HaveCount(3);

            // Verify retry logging
            var warningLogs = _jobLogger.Logs.Where(l => l.LogLevel == LogLevel.Warning).ToList();
            warningLogs.Should().HaveCount(2); // Two retry attempts
            warningLogs[0].Message.Should().Contain("Attempt 1 failed, retrying");
            warningLogs[1].Message.Should().Contain("Attempt 2 failed, retrying");
        }

        [Fact]
        public async Task ExecuteAsync_MaxRetriesExceeded_ReturnsFailure()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithRetryPolicy(maxAttempts: 2, baseDelayMs: 10)
                .Build();

            // All calls fail
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable);
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable);

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Status.Should().Be("Failed");
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            _httpHandler.Requests.Should().HaveCount(2);

            // Verify failure metrics
            _jobMetrics.FailureMetrics.Should().HaveCount(1);
            _jobMetrics.SuccessMetrics.Should().HaveCount(0);
        }

        [Fact]
        public async Task ExecuteAsync_SecretNotFound_ReturnsFailure()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithAuthType("bearer")
                .WithAuthSecret("nonexistent-secret")
                .Build();

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("nonexistent-secret");
            
            // Should not make HTTP call if secret is missing
            _httpHandler.Requests.Should().HaveCount(0);
        }

        [Fact]
        public async Task ExecuteAsync_NetworkTimeout_ReturnsFailure()
        {
            // Arrange
            var config = TestJobConfigurationBuilder.Default()
                .WithTimeout(1) // 1 second timeout
                .Build();

            // Simulate a long-running request by not adding any responses
            // The test handler will throw when no responses are configured

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _httpHandler?.Reset();
        }
    }
}
