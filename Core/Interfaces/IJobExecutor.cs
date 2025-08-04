using JobScheduler.FunctionApp.Core.Models;

namespace JobScheduler.FunctionApp.Core.Interfaces
{
    public interface IJobExecutor
    {
        Task<JobResult> ExecuteAsync(JobConfig config, CancellationToken cancellationToken = default);
    }
}