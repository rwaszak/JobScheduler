using Microsoft.Extensions.Logging;

namespace HelloWorldFunctionApp.Core.Interfaces
{
    public interface IJobLogger
    {
        Task LogAsync(LogLevel level, string jobName, string message, object metadata = null);
    }
}
