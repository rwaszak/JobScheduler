using JobScheduler.FunctionApp.Configuration;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Tests.TestHelpers;

public static class TestAppSettings
{
    public static IOptions<AppSettings> CreateDefault()
    {
        var appSettings = new AppSettings
        {
            Environment = "test",
            DatadogEnvironment = "test",
            Version = "test-version",
            ServiceName = "job-scheduler-functions-test",
            KeyVaultUrl = null, // Use environment fallback for tests
            IntegrationLayerDevHealthEndpoint = "https://test-endpoint.local/health"
        };

        return Options.Create(appSettings);
    }

    public static IOptions<AppSettings> CreateWithKeyVault(string keyVaultUrl)
    {
        var appSettings = new AppSettings
        {
            Environment = "test",
            DatadogEnvironment = "test", 
            Version = "test-version",
            ServiceName = "job-scheduler-functions-test",
            KeyVaultUrl = keyVaultUrl,
            IntegrationLayerDevHealthEndpoint = "https://test-endpoint.local/health"
        };

        return Options.Create(appSettings);
    }
}
