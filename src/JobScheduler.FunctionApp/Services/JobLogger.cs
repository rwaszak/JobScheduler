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
        private readonly ISecretManager _secretManager;
        private readonly LoggingOptions _loggingOptions;
        private readonly AppSettings _appSettings;

        public JobLogger(ILogger<JobLogger> logger, IHttpClientFactory httpClientFactory, ISecretManager secretManager, IOptions<JobSchedulerOptions> options, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _secretManager = secretManager;
            _loggingOptions = options.Value.Logging;
            _appSettings = appSettings.Value;
        }

        public async Task LogAsync(LogLevel level, string jobName, string message, object? metadata = null)
        {
            var logEntry = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                level = level.ToString().ToUpper(),
                message = message,
                service = _appSettings.ServiceName,
                source = "azure-functions",
                hostname = Environment.MachineName,
                ddsource = "azure-functions",
                ddtags = $"env:{_appSettings.DatadogEnvironment},service:{_appSettings.ServiceName},version:{_appSettings.Version},job_name:{jobName}",
                logger = new
                {
                    name = "JobScheduler.JobLogger",
                    thread_name = System.Threading.Thread.CurrentThread.Name ?? "Unknown"
                },
                attributes = _loggingOptions.IncludeMetadata ? metadata : null
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
                // Use the secret manager to retrieve the Datadog API key
                string? datadogApiKey = null;
                try
                {
                    datadogApiKey = await _secretManager.GetSecretAsync("datadog-api-key");
                }
                catch (InvalidOperationException)
                {
                    _logger.LogWarning("Datadog API key 'datadog-api-key' not found in secret store - skipping Datadog logging");
                    return;
                }

                if (string.IsNullOrEmpty(datadogApiKey))
                {
                    _logger.LogWarning("Datadog API key from secret store is null or empty - skipping Datadog logging");
                    return;
                }

                var json = JsonSerializer.Serialize(logEntry);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var url = $"https://http-intake.logs.{_loggingOptions.DatadogSite}/v1/input/{datadogApiKey}";

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to send log to Datadog. Status: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send log to Datadog");
            }
        }
    }
}