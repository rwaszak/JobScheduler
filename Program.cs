using HelloWorldFunctionApp.Core;
using HelloWorldFunctionApp.Core.Interfaces;
using HelloWorldFunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddHttpClient()

    // Register core services
    .AddSingleton<IJobExecutor, JobExecutor>()
    .AddSingleton<ISecretManager, EnvironmentSecretManager>()
    .AddSingleton<IJobLogger, JobLogger>()
    .AddSingleton<IJobMetrics, JobMetrics>()
    .AddSingleton<IJobConfigurationProvider, EnvironmentJobConfigurationProvider>();

builder.Build().Run();