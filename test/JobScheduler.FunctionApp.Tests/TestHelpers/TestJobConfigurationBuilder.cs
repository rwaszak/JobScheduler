using JobScheduler.FunctionApp.Core.Models;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestJobConfigurationBuilder
    {
        private JobConfig _config = new()
        {
            JobName = "test-job",
            Endpoint = "https://api.test.com/endpoint",
            HttpMethod = HttpMethod.Post,
            AuthType = "bearer",
            TimeoutSeconds = 30,
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 3,
                BaseDelayMs = 1000,
                BackoffMultiplier = 2.0,
                MaxDelayMs = 30000,
                RetryableStatusCodes = new() { 429, 502, 503, 504 } // Keep default retryable status codes
            }
        };

        public static TestJobConfigurationBuilder Default() => new();

        public TestJobConfigurationBuilder WithJobName(string jobName)
        {
            _config.JobName = jobName;
            return this;
        }

        public TestJobConfigurationBuilder WithEndpoint(string endpoint)
        {
            _config.Endpoint = endpoint;
            return this;
        }

        public TestJobConfigurationBuilder WithHttpMethod(string method)
        {
            _config.HttpMethod = new HttpMethod(method);
            return this;
        }

        public TestJobConfigurationBuilder WithHttpMethod(HttpMethod method)
        {
            _config.HttpMethod = method;
            return this;
        }

        public TestJobConfigurationBuilder WithAuthType(string authType)
        {
            _config.AuthType = authType;
            return this;
        }

        public TestJobConfigurationBuilder WithAuthSecret(string secretName)
        {
            _config.AuthSecretName = secretName;
            return this;
        }

        public TestJobConfigurationBuilder WithTimeout(int timeoutSeconds)
        {
            _config.TimeoutSeconds = timeoutSeconds;
            return this;
        }

        public TestJobConfigurationBuilder WithRetryPolicy(int maxAttempts, int baseDelayMs = 1000, double backoffMultiplier = 2.0)
        {
            _config.RetryPolicy = new RetryPolicy
            {
                MaxAttempts = maxAttempts,
                BaseDelayMs = baseDelayMs,
                BackoffMultiplier = backoffMultiplier,
                RetryableStatusCodes = new() { 429, 502, 503, 504 } // Keep default retryable status codes
            };
            return this;
        }

        public TestJobConfigurationBuilder WithRequestBody(object body)
        {
            _config.RequestBody = body;
            return this;
        }

        public TestJobConfigurationBuilder WithHeaders(Dictionary<string, string> headers)
        {
            _config.Headers = headers;
            return this;
        }

        public TestJobConfigurationBuilder WithTags(Dictionary<string, string> tags)
        {
            _config.Tags = tags;
            return this;
        }

        public JobConfig Build() => _config;
    }
}
