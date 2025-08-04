namespace JobScheduler.FunctionApp.Core.Models
{
    public class JobResult
    {
        public bool IsSuccess { get; init; }
        public string Status { get; init; } = string.Empty;
        public object? ResponseData { get; init; }
        public TimeSpan Duration { get; init; }
        public int AttemptCount { get; init; }
        public string? ErrorMessage { get; init; }
        public Dictionary<string, object> Metadata { get; init; } = new();
    }
}
