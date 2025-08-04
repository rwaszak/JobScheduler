namespace HelloWorldFunctionApp.Core.Interfaces
{
    public interface IJobMetrics
    {
        Task RecordJobSuccessAsync(string jobName, TimeSpan duration, int attemptCount);
        Task RecordJobFailureAsync(string jobName, TimeSpan duration, string errorMessage);
    }
}
