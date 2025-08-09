namespace JobScheduler.FunctionApp.Configuration;

public class LoggingOptions
{
    public string? DatadogApiKey { get; init; }
    public required string DatadogSite { get; init; }
}
