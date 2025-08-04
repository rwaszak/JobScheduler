using System.Text.Json;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Services
{
    public class JobLogger : IJobLogger
    {
        private readonly ILogger<JobLogger> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LoggingOptions _loggingOptions;

        public JobLogger(ILogger<JobLogger> logger, IHttpClientFactory httpClientFactory, IOptions<JobSchedulerOptions> options)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _loggingOptions = options.Value.Logging;
        }

        public async Task LogAsync(LogLevel level, string jobName, string message, object? metadata = null)
        {
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow,
                Level = level.ToString(),
                JobName = jobName,
                Message = message,
                Service = "job-executor",
                Metadata = _loggingOptions.IncludeMetadata ? metadata : null
            };

            // Log to ILogger (Application Insights)
            _logger.Log(level, "[{JobName}] {Message} {@Metadata}", jobName, message, metadata);

            // Log to Datadog
            await SendToDatadogAsync(logEntry);
        }

        private async Task SendToDatadogAsync(object logEntry)
        {
            try
            {
                if (string.IsNullOrEmpty(_loggingOptions.DatadogApiKey)) return;

                var json = JsonSerializer.Serialize(logEntry);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var url = $"https://http-intake.logs.{_loggingOptions.DatadogSite}/v1/input/{_loggingOptions.DatadogApiKey}";

                var httpClient = _httpClientFactory.CreateClient();
                await httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send log to Datadog");
            }
        }
    }
}