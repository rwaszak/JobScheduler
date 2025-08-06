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
/// These tests are independent of production JobNames and use test-specific constants.
/// </summary>
public class ConfigurationBindingTests
{
    [Fact]
    public void JobSchedulerOptions_ShouldBindFromConfiguration_WithAllJobProperties()
    {
        // Arrange - Test comprehensive job configuration with all properties using test job names
        var configData = new Dictionary<string, string?>
        {
            // Basic health check job
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:JobName"] = TestJobNames.TestHealthCheck,
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:Endpoint"] = "https://example.com/health",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:TimeoutSeconds"] = "30",
            
            // Comprehensive auth job with all properties
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:JobName"] = TestJobNames.TestAuthJob,
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:Endpoint"] = "https://example.com/comprehensive",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:HttpMethod"] = "POST",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:AuthType"] = "bearer",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:AuthSecretName"] = "TEST_TOKEN",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:RequestBody"] = "{\"action\":\"process\"}",
            [$"JobScheduler:Jobs:{TestJobNames.TestAuthJob}:TimeoutSeconds"] = "60",
        };

        using var setup = IndependentTestConfigurationHelper.CreateTestConfiguration(configData);
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert
        options.Jobs.Should().HaveCount(2);
        
        // Verify basic health check job
        var healthCheckJob = options.Jobs[TestJobNames.TestHealthCheck];
        healthCheckJob.JobName.Should().Be(TestJobNames.TestHealthCheck);
        healthCheckJob.Endpoint.Should().Be("https://example.com/health");
        healthCheckJob.HttpMethod.Should().Be(HttpMethod.Get);
        healthCheckJob.AuthType.Should().Be(AuthenticationType.None);
        healthCheckJob.TimeoutSeconds.Should().Be(30);
        
        // Verify comprehensive auth job
        var authJob = options.Jobs[TestJobNames.TestAuthJob];
        authJob.JobName.Should().Be(TestJobNames.TestAuthJob);
        authJob.Endpoint.Should().Be("https://example.com/comprehensive");
        authJob.HttpMethod.Should().Be(HttpMethod.Post);
        authJob.AuthType.Should().Be(AuthenticationType.Bearer);
        authJob.AuthSecretName.Should().Be("TEST_TOKEN");
        authJob.RequestBody.Should().Be("{\"action\":\"process\"}");
        authJob.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void JobDefinition_HttpMethodBinding_ShouldFailSilentlyForUnknownMethod()
    {
        // Arrange - Test specifically for unknown HTTP method with test job names
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:JobName"] = TestJobNames.TestErrorJob,
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:Endpoint"] = "https://example.com",
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:HttpMethod"] = "INVALID_METHOD",
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{TestJobNames.TestErrorJob}:TimeoutSeconds"] = "30"
        };

        // Act & Assert - Should fail during configuration access due to invalid HttpMethod
        using var setup = IndependentTestConfigurationHelper.CreateTestConfiguration(configData);
        
        var action = () => setup.GetJobSchedulerOptions();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Configuration validation failed*");
    }

    [Fact]
    public void JobDefinition_AuthTypeBinding_ShouldHandleAllValidTypes()
    {
        // Arrange - Test valid authentication type using test job name
        var configData = new Dictionary<string, string?>
        {
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:JobName"] = TestJobNames.TestHealthCheck,
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:Endpoint"] = "https://example.com/health",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:HttpMethod"] = "GET",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:AuthType"] = "none",
            [$"JobScheduler:Jobs:{TestJobNames.TestHealthCheck}:TimeoutSeconds"] = "30"
        };

        using var setup = IndependentTestConfigurationHelper.CreateTestConfiguration(configData);
        var options = setup.GetJobSchedulerOptions();

        // Act & Assert
        options.Jobs.Should().HaveCount(1);
        
        var healthJob = options.Jobs[TestJobNames.TestHealthCheck];
        healthJob.AuthType.Should().Be(AuthenticationType.None);
        healthJob.AuthSecretName.Should().BeNull();
    }
}
