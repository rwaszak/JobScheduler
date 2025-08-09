namespace JobScheduler.FunctionApp.Configuration;

public class LoggingOptions
{
    public string? DatadogApiKey { get; init; }
    public string DatadogSite { get; init; } = "us3.datadoghq.com";
}
