using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Core.Interfaces
{
    public interface IJobLogger
    {
        Task LogAsync(LogLevel level, string jobName, string message, object metadata = null);
    }
}
