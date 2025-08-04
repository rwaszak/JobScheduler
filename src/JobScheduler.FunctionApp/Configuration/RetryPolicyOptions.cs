using System.ComponentModel.DataAnnotations;

namespace JobScheduler.FunctionApp.Configuration;

public class RetryPolicyOptions
{
    [Range(1, 10)]
    public int MaxAttempts { get; init; } = 3;
    
    [Range(100, 30000)]
    public int BaseDelayMs { get; init; } = 1000;
    
    [Range(1.0, 5.0)]
    public double BackoffMultiplier { get; init; } = 2.0;
    
    [Range(1000, 300000)]
    public int MaxDelayMs { get; init; } = 30000;
    
    public List<int> RetryableStatusCodes { get; init; } = new() { 429, 502, 503, 504 };
}
