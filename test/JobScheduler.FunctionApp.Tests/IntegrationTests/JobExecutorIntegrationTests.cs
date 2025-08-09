using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Core.Models;
using JobScheduler.FunctionApp.Services;
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
        private readonly ISecretManager _secretManager;
        private readonly IJobLogger _jobLogger;
        private readonly IJobMetrics _jobMetrics;
        private readonly JobExecutor _jobExecutor;

        public JobExecutorIntegrationTests()
        {
            _httpHandler = new TestHttpMessageHandler();
            _httpClient = new HttpClient(_httpHandler);
            var httpClientFactory = new TestHttpClientFactory(_httpClient);
            _secretManager = new EnvironmentSecretManager();
            
            // Use the consolidated configuration helper instead of TestOptions
            using var configSetup = IndependentTestConfigurationHelper.CreateBasicTestConfiguration();
            var jobSchedulerOptions = Microsoft.Extensions.Options.Options.Create(new JobSchedulerOptions
            {
                Logging = new LoggingOptions
                {
                }
            });
            
            var testAppSettings = TestAppSettings.CreateDefault();
            var loggerProvider = new TestLoggerProvider<JobLogger>();
            _jobLogger = new JobLogger(loggerProvider, httpClientFactory, _secretManager, jobSchedulerOptions, testAppSettings);
            
            var metricsLoggerProvider = new TestLoggerProvider<JobMetrics>();
            _jobMetrics = new JobMetrics(metricsLoggerProvider);

            _jobExecutor = new JobExecutor(httpClientFactory, _secretManager, _jobLogger, _jobMetrics);
        }

        [Fact]
        public async Task ExecuteAsync_CompleteWorkflow_WorksEndToEnd()
        {
            // Arrange
            Environment.SetEnvironmentVariable("test-auth-token", "my-bearer-token");
            
            var config = new JobConfig
            {
                JobName = "integration-test",
                Endpoint = "https://api.integration.test/endpoint",
                HttpMethod = HttpMethod.Post,
                AuthType = AuthenticationType.Bearer,
                AuthSecretName = "test-auth-token",
                TimeoutSeconds = 30,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 2,
                    BaseDelayMs = 100,
                    BackoffMultiplier = 2.0
                },
                RequestBody = new { test = "data", timestamp = DateTime.UtcNow },
                Headers = new Dictionary<string, string>
                {
                    { "X-Request-ID", "test-12345" }
                }
            };

            _httpHandler.AddResponse(HttpStatusCode.OK, "{\"result\":\"success\",\"processed\":true}");

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Status.Should().Be("Completed");
            result.AttemptCount.Should().Be(1);
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);

            // Verify the HTTP request
            _httpHandler.Requests.Should().HaveCount(1);
            var capturedRequest = _httpHandler.GetCapturedRequest(0);
            
            capturedRequest.Method.ToString().Should().Be("POST");
            capturedRequest.RequestUri!.ToString().Should().Be("https://api.integration.test/endpoint");
            capturedRequest.Authorization.Should().NotBeNull();
            capturedRequest.Authorization!.Scheme.Should().Be("Bearer");
            capturedRequest.Authorization!.Parameter.Should().Be("my-bearer-token");
            capturedRequest.Headers.Should().ContainKey("X-Request-ID");
            capturedRequest.Headers["X-Request-ID"].Should().Be("test-12345");

            capturedRequest.Content.Should().Contain("\"test\":\"data\"");
        }

        [Fact]
        public async Task ExecuteAsync_WithRetryScenario_RetriesCorrectly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("retry-test-token", "retry-token");
            
            var config = new JobConfig
            {
                JobName = "retry-test",
                Endpoint = "https://api.retry.test/endpoint",
                HttpMethod = HttpMethod.Get,
                AuthType = AuthenticationType.Bearer,
                AuthSecretName = "retry-test-token",
                TimeoutSeconds = 30,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 4,
                    BaseDelayMs = 10, // Fast retries for testing
                    BackoffMultiplier = 2.0
                }
            };

            // Setup responses: 503, 502, 429, then success
            _httpHandler.AddResponse(HttpStatusCode.ServiceUnavailable, "{\"error\":\"service unavailable\"}");
            _httpHandler.AddResponse(HttpStatusCode.BadGateway, "{\"error\":\"bad gateway\"}");
            _httpHandler.AddResponse(HttpStatusCode.TooManyRequests, "{\"error\":\"rate limited\"}");
            _httpHandler.AddResponse(HttpStatusCode.OK, "{\"result\":\"finally success\"}");

            var startTime = DateTime.UtcNow;

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            var totalDuration = DateTime.UtcNow - startTime;
            
            result.IsSuccess.Should().BeTrue();
            result.AttemptCount.Should().Be(4);
            
            // Should have made 4 HTTP calls
            _httpHandler.Requests.Should().HaveCount(4);
            
            // All requests should have the same configuration
            foreach (var capturedRequest in _httpHandler.Requests)
            {
                capturedRequest.Method.ToString().Should().Be("GET");
                capturedRequest.RequestUri!.ToString().Should().Be("https://api.retry.test/endpoint");
                capturedRequest.Authorization?.Parameter.Should().Be("retry-token");
            }

            // Should have taken some time due to backoff delays
            // 10ms + 20ms + 40ms = ~70ms minimum
            totalDuration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public async Task ExecuteAsync_WithFailureAfterMaxRetries_ReturnsFailure()
        {
            // Arrange
            _httpHandler.Reset(); // Clear any previous state
            
            var config = new JobConfig
            {
                JobName = "failure-test",
                Endpoint = "https://api.failure.test/endpoint",
                HttpMethod = HttpMethod.Post,
                AuthType = AuthenticationType.None,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    BaseDelayMs = 10,
                    RetryableStatusCodes = new List<int> { 500 } // Make 500 retryable for this test
                }
            };

            // All requests fail
            _httpHandler.AddResponse(HttpStatusCode.InternalServerError, "{\"error\":\"server error\"}");
            _httpHandler.AddResponse(HttpStatusCode.InternalServerError, "{\"error\":\"server error\"}");
            _httpHandler.AddResponse(HttpStatusCode.InternalServerError, "{\"error\":\"server error\"}");

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Status.Should().Be("Failed");
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            
            // Should have attempted all retries
            _httpHandler.Requests.Should().HaveCount(3);
        }

        [Fact]
        public async Task ExecuteAsync_WithDifferentAuthTypes_WorksCorrectly()
        {
            // Test Bearer Auth
            await TestAuthType(AuthenticationType.Bearer, "BEARER_TOKEN", "bearer-secret-123", (request) =>
            {
                request.Headers.Authorization.Should().NotBeNull();
                request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                request.Headers.Authorization!.Parameter.Should().Be("bearer-secret-123");
            });

            _httpHandler.Reset();

            // Test API Key Auth
            await TestAuthType(AuthenticationType.ApiKey, "API_KEY_TOKEN", "api-key-456", (request) =>
            {
                request.Headers.Should().ContainKey("X-API-Key");
                request.Headers.GetValues("X-API-Key").First().Should().Be("api-key-456");
            });

            _httpHandler.Reset();

            // Test No Auth
            await TestAuthType(AuthenticationType.None, null, null, (request) =>
            {
                request.Headers.Authorization.Should().BeNull();
                request.Headers.Should().NotContainKey("X-API-Key");
            });
        }

        private async Task TestAuthType(AuthenticationType authType, string? envVarName, string? tokenValue, Action<HttpRequestMessage> verifyAuth)
        {
            // Arrange
            if (envVarName != null && tokenValue != null)
            {
                Environment.SetEnvironmentVariable(envVarName, tokenValue);
            }

            var config = new JobConfig
            {
                JobName = $"auth-test-{authType}",
                Endpoint = "https://api.auth.test/endpoint",
                HttpMethod = HttpMethod.Get,
                AuthType = authType,
                AuthSecretName = envVarName ?? "",
                RetryPolicy = new RetryPolicy { MaxAttempts = 1 }
            };

            _httpHandler.AddResponse(HttpStatusCode.OK, "{\"authenticated\":true}");

            // Act
            var result = await _jobExecutor.ExecuteAsync(config);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _httpHandler.Requests.Should().HaveCount(1);
            
            var request = _httpHandler.GetRequest(0);
            verifyAuth(request);

            // Cleanup
            if (envVarName != null)
            {
                Environment.SetEnvironmentVariable(envVarName, null);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _httpHandler?.Reset();
            
            // Clean up environment variables
            Environment.SetEnvironmentVariable("test-auth-token", null);
            Environment.SetEnvironmentVariable("retry-test-token", null);
            Environment.SetEnvironmentVariable("BEARER_TOKEN", null);
            Environment.SetEnvironmentVariable("API_KEY_TOKEN", null);
        }
    }
}
