namespace JobScheduler.FunctionApp.Configuration;

/// <summary>
/// Application-wide settings that can be configured per environment
/// Non-secret configuration values that come from appsettings.json
/// </summary>
public class AppSettings
{
    public const string SectionName = "AppSettings";
    
    /// <summary>
    /// Azure Key Vault URL for secret management
    /// </summary>
    public string? KeyVaultUrl { get; set; }
    
    /// <summary>
    /// Current environment name (dev, sit, uat, prod)
    /// </summary>
    public string Environment { get; set; } = "unknown";
    
    /// <summary>
    /// Datadog environment tag
    /// </summary>
    public string DatadogEnvironment { get; set; } = "unknown";
    
    /// <summary>
    /// Application version for monitoring and logging
    /// </summary>
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// Service name for Datadog and monitoring
    /// </summary>
    public string ServiceName { get; set; } = "job-scheduler-functions";
    
    /// <summary>
    /// Integration layer endpoint for development environment health checks
    /// </summary>
    public string? IntegrationLayerDevHealthEndpoint { get; set; }
}
