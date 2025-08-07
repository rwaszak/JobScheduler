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

        // Enable HttpMethod string-to-object conversion for configuration binding
        builder.Services.AddHttpMethodTypeConverter();

        // Configuration
        builder.Services.Configure<AppSettings>(
            builder.Configuration.GetSection(AppSettings.SectionName));
            
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
}