using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HelloWorldFunctionApp;

public class HelloWorldScheduler
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public HelloWorldScheduler(ILoggerFactory loggerFactory, HttpClient httpClient)
    {
        _logger = loggerFactory.CreateLogger<HelloWorldScheduler>();
        _httpClient = httpClient;
    }

    [Function("HelloWorldScheduler")]
    public async Task Run([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("Function executed at: {time}", DateTime.Now);

        try
        {
            // Get the Container App endpoint
            var endpoint = Environment.GetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT");
            var environment = Environment.GetEnvironmentVariable("ENVIRONMENT");

            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogWarning("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT not configured");
                return;
            }

            _logger.LogInformation("Calling Container App: {endpoint}", endpoint);

            // Call the Container App
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Container App responded with status: {status}, content: {content}",
                response.StatusCode, responseContent);

            // Log success to Datadog
            await LogToDatadog("info", "Successfully called Container App", new
            {
                endpoint = endpoint,
                environment = environment,
                status = (int)response.StatusCode,
                responseLength = responseContent.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Function execution failed");

            // Log error to Datadog
            await LogToDatadog("error", "Function execution failed", new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    private async Task LogToDatadog(string level, string message, object metadata)
    {
        try
        {
            var datadogApiKey = Environment.GetEnvironmentVariable("DATADOG_API_KEY");
            if (string.IsNullOrEmpty(datadogApiKey)) return;

            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                level = level,
                message = message,
                service = Environment.GetEnvironmentVariable("DD_SERVICE") ?? "function-scheduler-poc",
                env = Environment.GetEnvironmentVariable("DD_ENV") ?? "dev",
                metadata = metadata
            };

            var json = JsonSerializer.Serialize(logEntry);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var datadogUrl = $"https://http-intake.logs.{Environment.GetEnvironmentVariable("DD_SITE")}/v1/input/{datadogApiKey}";
            await _httpClient.PostAsync(datadogUrl, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log to Datadog");
        }
    }
}