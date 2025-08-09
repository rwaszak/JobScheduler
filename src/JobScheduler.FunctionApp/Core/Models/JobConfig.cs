using JobScheduler.FunctionApp.Configuration;

namespace JobScheduler.FunctionApp.Core.Models
{
    public class JobConfig
    {
        public string JobName { get; set; }
        public string Endpoint { get; set; }
        public HttpMethod HttpMethod { get; set; } = null!;
        public AuthenticationType AuthType { get; set; } = AuthenticationType.Bearer;
        public string AuthSecretName { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public object RequestBody { get; set; }
        public int TimeoutSeconds { get; set; } = 300;
        public RetryPolicy RetryPolicy { get; set; } = new();
    }
}
