using System.ComponentModel.DataAnnotations;

namespace JobScheduler.FunctionApp.Configuration;

public class RetryPolicyOptions
{
    [Range(1, 10)]
    public required int MaxAttempts { get; init; }
    
    [Range(100, 30000)]
    public required int BaseDelayMs { get; init; }
    
    [Range(1.0, 5.0)]
    public required double BackoffMultiplier { get; init; }
    
    [Range(1000, 300000)]
    public required int MaxDelayMs { get; init; }
    
    public required List<int> RetryableStatusCodes { get; init; }
}
