namespace HelloWorldFunctionApp.Core.Models
{
    public class JobResult
    {
        public bool IsSuccess { get; set; }
        public string Status { get; set; }
        public object ResponseData { get; set; }
        public TimeSpan Duration { get; set; }
        public int AttemptCount { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
