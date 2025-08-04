using HelloWorldFunctionApp.Core.Models;

namespace HelloWorldFunctionApp.Core.Interfaces
{
    public interface IJobExecutor
    {
        Task<JobResult> ExecuteAsync(JobConfig config, CancellationToken cancellationToken = default);
    }
}