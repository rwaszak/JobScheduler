namespace JobScheduler.FunctionApp.Core.Models
{
    public class RetryPolicy
    {
        public int MaxAttempts { get; set; } = 3;
        public int BaseDelayMs { get; set; } = 1000;
        public double BackoffMultiplier { get; set; } = 2.0;
        public int MaxDelayMs { get; set; } = 30000;
        public List<int> RetryableStatusCodes { get; set; } = new() { 429, 502, 503, 504 };
    }
}
