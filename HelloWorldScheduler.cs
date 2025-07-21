using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HelloWorldFunctionApp;

public class HelloWorldScheduler
{
    private readonly ILogger _logger;

    public HelloWorldScheduler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<HelloWorldScheduler>();
    }

    [Function("HelloWorldScheduler")]
    // every 10 seconds
    public void Run([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}