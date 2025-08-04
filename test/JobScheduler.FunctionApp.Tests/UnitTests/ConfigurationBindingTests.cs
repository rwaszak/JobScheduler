using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests;

public class ConfigurationBindingTests
{
    [Fact]
    public void HttpMethod_ShouldBindFromStringConfiguration()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:test-job:JobName"] = "test-job",
            ["JobScheduler:Jobs:test-job:Endpoint"] = "https://example.com",
            ["JobScheduler:Jobs:test-job:HttpMethod"] = "GET"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        
        // Use the same configuration approach as the application
        services.Configure<JobSchedulerOptions>(options =>
        {
            var section = configuration.GetSection(JobSchedulerOptions.SectionName);
            section.Bind(options);
            
            // Manually fix HttpMethod properties that couldn't be bound
            foreach (var kvp in options.Jobs)
            {
                var jobConfig = kvp.Value;
                if (jobConfig.HttpMethod == null)
                {
                    // Try to get the HttpMethod value from configuration
                    var httpMethodString = section.GetSection($"Jobs:{kvp.Key}:HttpMethod").Value;
                    if (!string.IsNullOrEmpty(httpMethodString))
                    {
                        jobConfig.HttpMethod = httpMethodString.ToUpperInvariant() switch
                        {
                            "GET" => HttpMethod.Get,
                            "POST" => HttpMethod.Post,
                            "PUT" => HttpMethod.Put,
                            "DELETE" => HttpMethod.Delete,
                            "PATCH" => HttpMethod.Patch,
                            "HEAD" => HttpMethod.Head,
                            "OPTIONS" => HttpMethod.Options,
                            "TRACE" => HttpMethod.Trace,
                            _ => throw new InvalidOperationException($"Unknown HTTP method: {httpMethodString} for job {kvp.Key}")
                        };
                    }
                }
            }
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;

        // Assert
        options.Jobs.Should().ContainKey("test-job");
        var job = options.Jobs["test-job"];
        job.HttpMethod.Should().Be(HttpMethod.Get);
    }
}
