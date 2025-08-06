using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);

        builder.ConfigureFunctionsWebApplication();

        // Add appsettings.json for application configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        // Add environment variables to override appsettings values
        builder.Configuration.AddEnvironmentVariables();

        // TEMPORARY: Disable token replacement to test if this is causing the issue
        // Replace environment variable placeholders in configuration
        // Note: We'll do this after the builder is created since ConfigurationManager doesn't have Build()
        // var tempConfig = new ConfigurationBuilder()
        //     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        //     .AddEnvironmentVariables()
        //     .Build();
        // 
        // var configWithEnvReplace = new ConfigurationBuilder()
        //     .AddInMemoryCollection(ReplaceEnvironmentTokens(tempConfig))
        //     .Build();
        // 
        // // Clear and add the processed configuration
        // builder.Configuration.Sources.Clear();
        // builder.Configuration.AddConfiguration(configWithEnvReplace);

        // Enable HttpMethod string-to-object conversion for configuration binding
        builder.Services.AddHttpMethodTypeConverter();

        // Configuration
        builder.Services.Configure<JobSchedulerOptions>(
            builder.Configuration.GetSection(JobSchedulerOptions.SectionName));

        // Validate configuration on startup
        builder.Services.AddSingleton<IValidateOptions<JobSchedulerOptions>, ValidateJobSchedulerOptions>();

        // Shared HttpClient configuration
        builder.Services.AddHttpClient();

        builder.Services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();

        // Register core services with appropriate lifetimes
        builder.Services
            .AddSingleton<ISecretManager, KeyVaultSecretManager>()
            .AddSingleton<IJobConfigurationProvider, OptionsJobConfigurationProvider>()
            .AddScoped<IJobExecutor, JobExecutor>()
            .AddScoped<IJobLogger, JobLogger>()
            .AddScoped<IJobMetrics, JobMetrics>();

        builder.Build().Run();
    }

    private static IEnumerable<KeyValuePair<string, string?>> ReplaceEnvironmentTokens(IConfiguration configuration)
    {
        foreach (var item in configuration.AsEnumerable())
        {
            var value = item.Value;
            if (!string.IsNullOrEmpty(value) && value.StartsWith("#{") && value.EndsWith("}#"))
            {
                var envVarName = value.Substring(2, value.Length - 4);
                var envValue = Environment.GetEnvironmentVariable(envVarName);
                yield return new KeyValuePair<string, string?>(item.Key, envValue);
            }
            else
            {
                yield return item;
            }
        }
    }
}