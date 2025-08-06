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
            // Add explicit logging to understand when this method is called
            _logger.LogInformation("JobLogger.LogAsync called - JobName: {JobName}, Level: {Level}, Message: {Message}", 
                jobName, level, message);

            var logEntry = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                level = level.ToString().ToUpper(),
                message = message,
                service = "jobscheduler-functions",
                source = "azure-functions",
                hostname = Environment.MachineName,
                ddsource = "azure-functions",
                ddtags = $"env:{Environment.GetEnvironmentVariable("DD_ENV") ?? "unknown"},service:jobscheduler-functions,version:{Environment.GetEnvironmentVariable("DD_VERSION") ?? "unknown"},job_name:{jobName}",
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
                // Use the resolved DATADOG_API_KEY environment variable instead of the configuration binding
                var datadogApiKey = Environment.GetEnvironmentVariable("DATADOG_API_KEY");
                
                // Debug logging to understand configuration binding
                _logger.LogInformation("Datadog configuration check - ApiKey from config: {HasConfigApiKey} (value: {ConfigValue}), ApiKey from env: {HasEnvApiKey} (value: {EnvValue}), Site: {Site}", 
                    !string.IsNullOrEmpty(_loggingOptions.DatadogApiKey) ? "***PRESENT***" : "NULL/EMPTY",
                    _loggingOptions.DatadogApiKey?.Length > 10 ? _loggingOptions.DatadogApiKey.Substring(0, 10) + "..." : _loggingOptions.DatadogApiKey,
                    !string.IsNullOrEmpty(datadogApiKey) ? "***PRESENT***" : "NULL/EMPTY",
                    datadogApiKey?.Length > 10 ? datadogApiKey.Substring(0, 10) + "..." : datadogApiKey,
                    _loggingOptions.DatadogSite);

                if (string.IsNullOrEmpty(datadogApiKey)) 
                {
                    _logger.LogWarning("Datadog API key from environment is null or empty - skipping Datadog logging");
                    return;
                }

                // Check if we're still getting a Key Vault reference
                if (datadogApiKey.StartsWith("@Microsoft.KeyVault"))
                {
                    _logger.LogError("Environment variable DATADOG_API_KEY contains Key Vault reference instead of resolved value: {Reference}", datadogApiKey);
                    return;
                }

                var json = JsonSerializer.Serialize(logEntry);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending JSON to Datadog: {JsonPayload}", json);

                var url = $"https://http-intake.logs.{_loggingOptions.DatadogSite}/v1/input/{datadogApiKey}";

                _logger.LogInformation("Sending log to Datadog URL: {Url}", url.Replace(datadogApiKey, "***API-KEY***"));

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(url, content);
                
                _logger.LogInformation("Datadog response: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send log to Datadog");
            }
        }
    }
}