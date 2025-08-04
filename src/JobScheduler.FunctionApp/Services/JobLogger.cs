// Services/JobLogger.cs
using System.Text.Json;
using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Services
{
    public class JobLogger : IJobLogger
    {
        private readonly ILogger<JobLogger> _logger;
        private readonly HttpClient _httpClient;

        public JobLogger(ILogger<JobLogger> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
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
                Metadata = metadata
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
                var datadogApiKey = Environment.GetEnvironmentVariable("DATADOG_API_KEY");
                if (string.IsNullOrEmpty(datadogApiKey)) return;

                var json = JsonSerializer.Serialize(logEntry);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var datadogSite = Environment.GetEnvironmentVariable("DD_SITE") ?? "us3.datadoghq.com";
                var url = $"https://http-intake.logs.{datadogSite}/v1/input/{datadogApiKey}";

                await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send log to Datadog");
            }
        }
    }
}