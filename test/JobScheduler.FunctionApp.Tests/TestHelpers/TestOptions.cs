using JobScheduler.FunctionApp.Configuration;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestOptions : IOptions<JobSchedulerOptions>
    {
        public JobSchedulerOptions Value { get; }

        public TestOptions(JobSchedulerOptions value)
        {
            Value = value;
        }

        public static TestOptions CreateDefault()
        {
            return new TestOptions(new JobSchedulerOptions
            {
                Logging = new LoggingOptions
                {
                    EnableStructuredLogging = true,
                    IncludeMetadata = true,
                    LogLevel = "Information"
                },
                Metrics = new MetricsOptions
                {
                    EnableMetrics = true
                }
            });
        }
    }
}
