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
        options.Jobs.Should().HaveCount(1);
        
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
    public void Validation_ShouldFail_WithMissingJob_BecauseJobNamesConstantsRequireAllJobs()
    {
        // Arrange - Test with empty configuration (should fail because ContainerAppHealth is missing)
        using var setup = TestConfigurationHelper.CreateEmptyConfiguration();
        
        // Act
        var result = setup.ValidateConfiguration();
        
        // Assert - Should fail because ContainerAppHealth job is missing
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain($"Job '{JobNames.ContainerAppHealth}' is defined in JobNames constants but missing from configuration.");
    }
}
