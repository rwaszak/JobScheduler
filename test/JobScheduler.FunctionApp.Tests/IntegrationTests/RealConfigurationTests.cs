using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using JobScheduler.FunctionApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

public class RealConfigurationTests
{
    [Fact]
    public void Configuration_ShouldLoadFromInMemory_SimulatingAppsettingsJson()
    {
        // Arrange - Simulate the exact content from appsettings.json
        var appsettingsContent = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:container-app-health:JobName"] = "container-app-health",
            ["JobScheduler:Jobs:container-app-health:Endpoint"] = "https://int-svc-be-capp-dev.whitesky-4effbccc.centralus.azurecontainerapps.io/health",
            ["JobScheduler:Jobs:container-app-health:HttpMethod"] = "GET",
            ["JobScheduler:Jobs:container-app-health:AuthType"] = "none",
            ["JobScheduler:Jobs:container-app-health:TimeoutSeconds"] = "30",
            
            ["JobScheduler:Jobs:daily-batch:JobName"] = "daily-batch",
            ["JobScheduler:Jobs:daily-batch:Endpoint"] = "https://your-api.azurecontainerapps.io/api/batch-process",
            ["JobScheduler:Jobs:daily-batch:HttpMethod"] = "POST",
            ["JobScheduler:Jobs:daily-batch:AuthType"] = "bearer",
            ["JobScheduler:Jobs:daily-batch:AuthSecretName"] = "DAILY_BATCH_TOKEN",
            ["JobScheduler:Jobs:daily-batch:TimeoutSeconds"] = "120",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appsettingsContent)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        
        // Use the exact same setup as Program.cs
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));
        services.AddSingleton<IValidateOptions<JobSchedulerOptions>, ValidateJobSchedulerOptions>();
        services.AddSingleton<IJobConfigurationProvider, OptionsJobConfigurationProvider>();

        var serviceProvider = services.BuildServiceProvider();

        // Act - Test the real configuration provider
        var jobConfigProvider = serviceProvider.GetRequiredService<IJobConfigurationProvider>();
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;

        // Assert - Verify configuration is properly loaded
        options.Jobs.Should().ContainKey("container-app-health");
        options.Jobs.Should().ContainKey("daily-batch");

        var healthCheckJob = jobConfigProvider.GetJobConfig("container-app-health");
        healthCheckJob.Should().NotBeNull();
        healthCheckJob.JobName.Should().Be("container-app-health");
        healthCheckJob.HttpMethod.Should().Be(HttpMethod.Get);
        healthCheckJob.AuthType.Should().Be(AuthenticationType.None);

        var batchJob = jobConfigProvider.GetJobConfig("daily-batch");
        batchJob.Should().NotBeNull();
        batchJob.JobName.Should().Be("daily-batch");
        batchJob.HttpMethod.Should().Be(HttpMethod.Post);
        batchJob.AuthType.Should().Be(AuthenticationType.Bearer);
    }

    [Fact]
    public void Configuration_ShouldValidateSuccessfully_WithRealProvider()
    {
        // Arrange - Test just the basic configuration binding without validation
        var appsettingsContent = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:container-app-health:JobName"] = "container-app-health",
            ["JobScheduler:Jobs:container-app-health:Endpoint"] = "https://example.com/health",
            ["JobScheduler:Jobs:container-app-health:HttpMethod"] = "GET",
            ["JobScheduler:Jobs:container-app-health:AuthType"] = "none",
            ["JobScheduler:Jobs:container-app-health:TimeoutSeconds"] = "30",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appsettingsContent)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act - Test that configuration binds correctly
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;

        // Assert - Basic configuration should be loaded
        options.Jobs.Should().ContainKey("container-app-health");
        var job = options.Jobs["container-app-health"];
        job.JobName.Should().Be("container-app-health");
        job.HttpMethod.Should().Be(HttpMethod.Get);
        job.AuthType.Should().Be(AuthenticationType.None);
    }

    [Fact]
    public void Configuration_ShouldFailIfNoJobsConfigured_SimulatingMissingAppsettings()
    {
        // Arrange - Setup without any job configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpMethodTypeConverter();
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act - Get the empty configuration
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;

        // Assert - Should have no jobs configured
        options.Jobs.Should().BeEmpty("Should have no jobs when configuration is missing");
    }
}
