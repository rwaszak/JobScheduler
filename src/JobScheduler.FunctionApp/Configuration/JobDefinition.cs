using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace JobScheduler.FunctionApp.Configuration;

public class JobDefinition
{
    [Required]
    public required string JobName { get; init; }
    
    [Required]
    [Url]
    public required string Endpoint { get; init; }
    
    [Required]
    public required HttpMethod HttpMethod { get; init; }
    
    public AuthenticationType AuthType { get; init; } = AuthenticationType.None;
    public string? AuthSecretName { get; init; }
    public string? RequestBody { get; init; }
    
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
    
    public RetryPolicyOptions RetryPolicy { get; init; } = new();
}
