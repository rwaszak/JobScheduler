using System.ComponentModel.DataAnnotations;

namespace JobScheduler.FunctionApp.Configuration;

public class JobSchedulerOptions
{
    public const string SectionName = "JobScheduler";

    [Required]
    public Dictionary<string, JobDefinition> Jobs { get; init; } = new();
    
    public LoggingOptions Logging { get; init; } = new();
}
