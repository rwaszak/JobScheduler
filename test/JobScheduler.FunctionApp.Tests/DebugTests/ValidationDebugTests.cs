using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.DebugTests;

public class ValidationDebugTests
{
    [Fact]
    public void Validation_ShouldWork_WithDefaultConfiguration()
    {
        // Arrange 
        using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
        
        // Act - Try to get the validator
        var validator = setup.GetValidator();
        validator.Should().NotBeNull();
        
        // Act - Try to get options
        var options = setup.GetJobSchedulerOptions();
        options.Should().NotBeNull();
        options.Jobs.Should().HaveCount(2);
        
        // Act - Try validation manually
        var result = validator.Validate(null, options);
        
        // Assert
        result.Should().NotBeNull();
        if (result.Failed)
        {
            // Output the failures to help debug
            var failures = string.Join(", ", result.Failures ?? []);
            throw new Exception($"Validation failed: {failures}");
        }
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validation_ShouldWork_WithSingleJob()
    {
        // Arrange - Test with just the container health job
        using var setup = TestConfigurationHelper.CreateSingleJobConfiguration(
            jobName: "container-app-health",
            endpoint: "https://example.com/health", 
            httpMethod: "GET",
            authType: "none");
        
        // Act & Assert
        var result = setup.ValidateConfiguration();
        if (result.Failed)
        {
            var failures = string.Join(", ", result.Failures ?? []);
            throw new Exception($"Single job validation failed: {failures}");
        }
        result.Succeeded.Should().BeTrue();
    }
}
