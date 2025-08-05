using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.IntegrationTests;

/// <summary>
/// Tests to verify that JSON configuration binds correctly to model objects,
/// particularly for complex types like HttpMethod that require type conversion.
/// These tests complement the JobConfigurationProviderTests by testing edge cases and binding behavior.
/// </summary>
public class ConfigurationBindingTests
{
    [Fact]
    public void JobSchedulerOptions_ShouldBindFromConfiguration_WithAllJobProperties()
    {
        // Arrange - Test comprehensive job configuration with all properties using valid job names
        var configData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:container-app-health:JobName"] = "container-app-health",
            ["JobScheduler:Jobs:container-app-health:Endpoint"] = "https://example.com/health",
            ["JobScheduler:Jobs:container-app-health:HttpMethod"] = "GET",
            ["JobScheduler:Jobs:container-app-health:AuthType"] = "none",
            ["JobScheduler:Jobs:container-app-health:TimeoutSeconds"] = "30",
            
            ["JobScheduler:Jobs:daily-batch:JobName"] = "daily-batch",
            ["JobScheduler:Jobs:daily-batch:Endpoint"] = "https://example.com/comprehensive",
            ["JobScheduler:Jobs:daily-batch:HttpMethod"] = "POST",
            ["JobScheduler:Jobs:daily-batch:AuthType"] = "bearer",
            ["JobScheduler:Jobs:daily-batch:AuthSecretName"] = "TEST_TOKEN",
            ["JobScheduler:Jobs:daily-batch:RequestBody"] = "{\"action\":\"process\"}",
            ["JobScheduler:Jobs:daily-batch:TimeoutSeconds"] = "60",
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert
        options.Jobs.Should().HaveCount(2);
        
        var comprehensiveJob = options.Jobs["daily-batch"];
        comprehensiveJob.JobName.Should().Be("daily-batch");
        comprehensiveJob.Endpoint.Should().Be("https://example.com/comprehensive");
        comprehensiveJob.HttpMethod.Should().Be(HttpMethod.Post);
        comprehensiveJob.AuthType.Should().Be(AuthenticationType.Bearer);
        comprehensiveJob.AuthSecretName.Should().Be("TEST_TOKEN");
        comprehensiveJob.RequestBody.Should().Be("{\"action\":\"process\"}");
        comprehensiveJob.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void JobDefinition_HttpMethodBinding_ShouldFailSilentlyForUnknownMethod()
    {
        // Arrange - Test specifically for unknown HTTP method with valid job names
        var configData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:container-app-health:JobName"] = "container-app-health",
            ["JobScheduler:Jobs:container-app-health:Endpoint"] = "https://example.com",
            ["JobScheduler:Jobs:container-app-health:HttpMethod"] = "INVALID_METHOD",
            ["JobScheduler:Jobs:container-app-health:AuthType"] = "none",
            ["JobScheduler:Jobs:container-app-health:TimeoutSeconds"] = "30",
            
            ["JobScheduler:Jobs:daily-batch:JobName"] = "daily-batch",
            ["JobScheduler:Jobs:daily-batch:Endpoint"] = "https://example.com/batch",
            ["JobScheduler:Jobs:daily-batch:HttpMethod"] = "POST",
            ["JobScheduler:Jobs:daily-batch:AuthType"] = "none",
            ["JobScheduler:Jobs:daily-batch:TimeoutSeconds"] = "30",
        };

        // Act & Assert - Should fail during configuration access due to invalid HttpMethod
        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        
        var action = () => setup.GetJobSchedulerOptions();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Configuration validation failed*");
    }

    [Fact]
    public void JobDefinition_AuthTypeBinding_ShouldHandleAllValidTypes()
    {
        // Arrange - Test all valid authentication types using valid job names
        var configData = new Dictionary<string, string?>
        {
            ["JobScheduler:Jobs:container-app-health:JobName"] = "container-app-health",
            ["JobScheduler:Jobs:container-app-health:Endpoint"] = "https://example.com/health",
            ["JobScheduler:Jobs:container-app-health:HttpMethod"] = "GET",
            ["JobScheduler:Jobs:container-app-health:AuthType"] = "none",
            ["JobScheduler:Jobs:container-app-health:TimeoutSeconds"] = "30",
            
            ["JobScheduler:Jobs:daily-batch:JobName"] = "daily-batch",
            ["JobScheduler:Jobs:daily-batch:Endpoint"] = "https://example.com/batch",
            ["JobScheduler:Jobs:daily-batch:HttpMethod"] = "POST",
            ["JobScheduler:Jobs:daily-batch:AuthType"] = "bearer",
            ["JobScheduler:Jobs:daily-batch:AuthSecretName"] = "BEARER_TOKEN",
            ["JobScheduler:Jobs:daily-batch:TimeoutSeconds"] = "30",
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert
        options.Jobs.Should().HaveCount(2);
        
        var noneAuthJob = options.Jobs["container-app-health"];
        noneAuthJob.AuthType.Should().Be(AuthenticationType.None);
        noneAuthJob.AuthSecretName.Should().BeNull();
        
        var bearerAuthJob = options.Jobs["daily-batch"];
        bearerAuthJob.AuthType.Should().Be(AuthenticationType.Bearer);
        bearerAuthJob.AuthSecretName.Should().Be("BEARER_TOKEN");
    }
}
