using HelloWorldFunctionApp.Core.Models;

namespace HelloWorldFunctionApp.Core.Interfaces
{
    public interface IJobConfigurationProvider
    {
        JobConfig GetJobConfig(string jobName);
        IEnumerable<JobConfig> GetAllJobConfigs();
    }
}
