namespace JobScheduler.FunctionApp.Configuration;

public class MetricsOptions
{
    public bool EnableMetrics { get; init; } = true;
    public bool EnableCustomCounters { get; init; } = true;
    public int MetricsFlushIntervalSeconds { get; init; } = 60;
}
