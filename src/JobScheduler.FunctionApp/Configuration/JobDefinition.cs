using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace JobScheduler.FunctionApp.Configuration;

public class JobDefinition
{
    [Required]
    public string JobName { get; init; } = string.Empty;
    
    [Required]
    [Url]
    public string Endpoint { get; init; } = string.Empty;
    
    [Required]
    public HttpMethod HttpMethod { get; set; } = null!;
    
    public AuthenticationType AuthType { get; init; } = AuthenticationType.None;
    public string? AuthSecretName { get; init; }
    public string? RequestBody { get; init; }
    
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
    
    public RetryPolicyOptions RetryPolicy { get; init; } = new();
    public Dictionary<string, string> Tags { get; init; } = new();
}
