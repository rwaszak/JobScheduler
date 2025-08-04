namespace JobScheduler.FunctionApp.Core.Models
{
    public class JobConfig
    {
        public string JobName { get; set; }
        public string Endpoint { get; set; }
        public HttpMethod HttpMethod { get; set; } = null!;
        public string AuthType { get; set; } = "bearer";
        public string AuthSecretName { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public object RequestBody { get; set; }
        public int TimeoutSeconds { get; set; } = 300;
        public RetryPolicy RetryPolicy { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
    }
}
