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
    public string? KeyVaultUrl { get; init; }
    
    /// <summary>
    /// Current environment name (dev, sit, uat, prod)
    /// </summary>
    public required string Environment { get; init; }
    
    /// <summary>
    /// Datadog environment tag
    /// </summary>
    public required string DatadogEnvironment { get; init; }
    
    /// <summary>
    /// Application version for monitoring and logging
    /// </summary>
    public required string Version { get; init; }
    
    /// <summary>
    /// Service name for Datadog and monitoring
    /// </summary>
    public required string ServiceName { get; init; }
    
    /// <summary>
    /// Integration layer endpoint for development environment health checks
    /// </summary>
    public string? IntegrationLayerDevHealthEndpoint { get; init; }
}
