using System.Text.Json;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Core
{
    public class JobExecutor : IJobExecutor
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISecretManager _secretManager;
        private readonly IJobLogger _jobLogger;
        private readonly IJobMetrics _jobMetrics;

        public JobExecutor(
            IHttpClientFactory httpClientFactory,
            ISecretManager secretManager,
            IJobLogger jobLogger,
            IJobMetrics jobMetrics)
        {
            _httpClientFactory = httpClientFactory;
            _secretManager = secretManager;
            _jobLogger = jobLogger;
            _jobMetrics = jobMetrics;
        }

        public async Task<JobResult> ExecuteAsync(JobConfig config, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var jobId = Guid.NewGuid().ToString("N")[..8];
            var result = new JobResult();

            await _jobLogger.LogAsync(LogLevel.Information, config.JobName, "Job started", new
            {
                JobId = jobId,
                Endpoint = config.Endpoint,
                JobName = config.JobName
            });

            try
            {
                // Get authentication token if needed
                var authToken = await GetAuthTokenAsync(config);

                // Execute with retry logic
                var response = await ExecuteWithRetryAsync(config, authToken, jobId, cancellationToken);

                result.IsSuccess = true;
                result.Status = "Completed";
                result.ResponseData = response.Data;
                result.AttemptCount = response.AttemptCount;
                result.Duration = DateTime.UtcNow - startTime;

                await _jobLogger.LogAsync(LogLevel.Information, config.JobName, "Job completed successfully", new
                {
                    JobId = jobId,
                    Duration = result.Duration.TotalMilliseconds,
                    AttemptCount = result.AttemptCount
                });

                await _jobMetrics.RecordJobSuccessAsync(config.JobName, result.Duration, result.AttemptCount);
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Status = "Failed";
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.UtcNow - startTime;

                await _jobLogger.LogAsync(LogLevel.Error, config.JobName, "Job failed", new
                {
                    JobId = jobId,
                    Duration = result.Duration.TotalMilliseconds,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });

                await _jobMetrics.RecordJobFailureAsync(config.JobName, result.Duration, ex.Message);
                return result;
            }
        }

        private async Task<string> GetAuthTokenAsync(JobConfig config)
        {
            if (config.AuthType == "none" || string.IsNullOrEmpty(config.AuthSecretName))
                return null;

            return await _secretManager.GetSecretAsync(config.AuthSecretName);
        }

        private async Task<(object Data, int AttemptCount)> ExecuteWithRetryAsync(
            JobConfig config,
            string authToken,
            string jobId,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            Exception lastException = null!;

            while (attempt < config.RetryPolicy.MaxAttempts)
            {
                attempt++;

                try
                {
                    var response = await MakeHttpCallAsync(config, authToken, attempt, cancellationToken);
                    return (response, attempt);
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;

                    // Check if we should retry this exception
                    if (!ShouldRetry(ex, config.RetryPolicy, attempt))
                    {
                        // Not retryable, break out of the loop
                        break;
                    }

                    await _jobLogger.LogAsync(LogLevel.Warning, config.JobName,
                        $"Attempt {attempt} failed, retrying...", new
                        {
                            JobId = jobId,
                            Attempt = attempt,
                            Error = ex.Message
                        });

                    if (attempt < config.RetryPolicy.MaxAttempts)
                    {
                        var delay = CalculateBackoffDelay(config.RetryPolicy, attempt);
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            throw lastException ?? new Exception("All retry attempts failed");
        }

        private async Task<object> MakeHttpCallAsync(JobConfig config, string authToken, int attempt, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient("job-executor");
            using var request = new HttpRequestMessage(config.HttpMethod, config.Endpoint);

            // Set headers
            request.Headers.Add("User-Agent", $"job-executor/{config.JobName}");
            foreach (var header in config.Headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            // Set authentication
            if (!string.IsNullOrEmpty(authToken))
            {
                if (config.AuthType == "bearer")
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                else if (config.AuthType == "apikey")
                    request.Headers.Add("X-API-Key", authToken);
            }

            // Set request body
            if (config.RequestBody != null)
            {
                var json = JsonSerializer.Serialize(config.RequestBody);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var response = await httpClient.SendAsync(request, combinedCts.Token);

            // Check if this is a retryable error status code
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                if (config.RetryPolicy.RetryableStatusCodes.Contains(statusCode))
                {
                    throw new HttpRequestException($"HTTP {statusCode} {response.StatusCode}: {response.ReasonPhrase}");
                }
                
                // Not retryable, use standard behavior
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<object>(responseContent);
        }

        private bool ShouldRetry(HttpRequestException ex, RetryPolicy policy, int attempt)
        {
            if (attempt >= policy.MaxAttempts) return false;

            // Check for timeout and connection issues
            if (ex.Message.Contains("timeout") || ex.Message.Contains("connection"))
                return true;

            // Check for retryable HTTP status codes in the exception message
            return policy.RetryableStatusCodes.Any(code => ex.Message.Contains($"HTTP {code}"));
        }

        private TimeSpan CalculateBackoffDelay(RetryPolicy policy, int attempt)
        {
            var delay = policy.BaseDelayMs * Math.Pow(policy.BackoffMultiplier, attempt - 1);
            var delayMs = Math.Min(delay, policy.MaxDelayMs);
            return TimeSpan.FromMilliseconds(delayMs);
        }
    }
}