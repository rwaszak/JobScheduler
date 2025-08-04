using JobScheduler.FunctionApp.Core.Models;

namespace JobScheduler.FunctionApp.Core.Interfaces
{
    public interface IJobConfigurationProvider
    {
        JobConfig GetJobConfig(string jobName);
        IEnumerable<JobConfig> GetAllJobConfigs();
    }
}
