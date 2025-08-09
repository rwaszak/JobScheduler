namespace JobScheduler.FunctionApp.Configuration;

public class JobSchedulerOptions
{
    public const string SectionName = "JobScheduler";

    public Dictionary<string, JobDefinition> Jobs { get; init; } = new();
    
    public LoggingOptions Logging { get; init; } = new();
}
