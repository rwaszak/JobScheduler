namespace JobScheduler.FunctionApp.Configuration;

/// <summary>
/// Centralized constants for job names to ensure consistency between 
/// function triggers, configuration keys, and code references.
/// 
/// Benefits:
/// - Compile-time safety: Typos in job names cause build errors instead of runtime failures
/// - Single source of truth: All job names defined in one place
/// - Automatic validation: ValidateJobSchedulerOptions checks for mismatches at startup
/// - Easy refactoring: Rename a job name in one place and it updates everywhere
/// - IntelliSense support: Auto-completion helps avoid mistakes
/// 
/// Usage:
/// - In ScheduledJobs.cs: ExecuteJobSafely(JobNames.ContainerAppHealth, myTimer)
/// - In local.settings.json: Use the exact string value as the configuration key
/// - In tests: Use the constants instead of hardcoded strings
/// </summary>
public static class JobNames
{
    public const string ContainerAppHealth = "containerAppHealth";
    public const string DailyBatch = "dailyBatch";
    
    // TODO: Add new job names here when adding new scheduled functions
    // Remember to also add the corresponding configuration in appsettings.json
}
