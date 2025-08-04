namespace JobScheduler.FunctionApp.Configuration;

public class LoggingOptions
{
    public bool EnableStructuredLogging { get; init; } = true;
    public bool IncludeMetadata { get; init; } = true;
    public string LogLevel { get; init; } = "Information";
    public string? ExternalLoggingUrl { get; init; }
    public string? DatadogApiKey { get; init; }
    public string DatadogSite { get; init; } = "us3.datadoghq.com";
}
