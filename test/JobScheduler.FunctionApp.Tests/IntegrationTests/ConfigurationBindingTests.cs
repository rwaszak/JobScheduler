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
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://example.com/health",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30",
            
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://example.com/comprehensive",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "POST",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "bearer",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthSecretName"] = "TEST_TOKEN",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:RequestBody"] = "{\"action\":\"process\"}",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "60",
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert
        options.Jobs.Should().HaveCount(1);
        
        var comprehensiveJob = options.Jobs[JobNames.ContainerAppHealth];
        comprehensiveJob.JobName.Should().Be(JobNames.ContainerAppHealth);
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
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://example.com",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "INVALID_METHOD",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30"
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
        // Arrange - Test valid authentication type using valid job name
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:JobName"] = JobNames.ContainerAppHealth,
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:Endpoint"] = "https://example.com/health",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{JobNames.ContainerAppHealth}:TimeoutSeconds"] = "30"
        };

        using var setup = TestConfigurationHelper.CreateCustomConfiguration(configData);
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert
        options.Jobs.Should().HaveCount(1);
        
        var healthJob = options.Jobs[JobNames.ContainerAppHealth];
        healthJob.AuthType.Should().Be(AuthenticationType.None);
        healthJob.AuthSecretName.Should().BeNull();
    }
}
