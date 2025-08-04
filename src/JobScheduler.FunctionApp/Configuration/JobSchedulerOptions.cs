using System.ComponentModel.DataAnnotations;

namespace JobScheduler.FunctionApp.Configuration;

public class JobSchedulerOptions
{
    public const string SectionName = "JobScheduler";

    [Required]
    public Dictionary<string, JobDefinition> Jobs { get; set; } = new();
    
    public LoggingOptions Logging { get; set; } = new();
    public MetricsOptions Metrics { get; set; } = new();
}

public class JobDefinition
{
    [Required]
    public string JobName { get; set; } = string.Empty;
    
    [Required]
    [Url]
    public string Endpoint { get; set; } = string.Empty;
    
    [Required]
    public string HttpMethod { get; set; } = "GET";
    
    public string AuthType { get; set; } = "none";
    public string? AuthSecretName { get; set; }
    public string? RequestBody { get; set; }
    
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
    
    public RetryPolicyOptions RetryPolicy { get; set; } = new();
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class RetryPolicyOptions
{
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;
    
    [Range(100, 30000)]
    public int BaseDelayMs { get; set; } = 1000;
    
    [Range(1.0, 5.0)]
    public double BackoffMultiplier { get; set; } = 2.0;
    
    [Range(1000, 300000)]
    public int MaxDelayMs { get; set; } = 30000;
    
    public List<int> RetryableStatusCodes { get; set; } = new() { 429, 502, 503, 504 };
}

public class LoggingOptions
{
    public bool EnableStructuredLogging { get; set; } = true;
    public bool IncludeMetadata { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
    public string? ExternalLoggingUrl { get; set; }
    public string? DatadogApiKey { get; set; }
    public string DatadogSite { get; set; } = "us3.datadoghq.com";
}

public class MetricsOptions
{
    public bool EnableMetrics { get; set; } = true;
    public bool EnableCustomCounters { get; set; } = true;
    public int MetricsFlushIntervalSeconds { get; set; } = 60;
}
