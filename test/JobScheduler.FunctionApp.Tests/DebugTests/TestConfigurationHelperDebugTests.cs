using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.DebugTests;

public class TestConfigurationHelperDebugTests
{
    [Fact]
    public void TestConfigurationHelper_ShouldCreateServiceProvider()
    {
        // Arrange & Act
        using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
        
        // Assert
        setup.Should().NotBeNull();
        setup.ServiceProvider.Should().NotBeNull();
        setup.Configuration.Should().NotBeNull();
    }

    [Fact] 
    public void TestConfigurationHelper_ShouldGetJobConfigurationProvider()
    {
        // Arrange & Act
        using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
        var provider = setup.GetJobConfigurationProvider();
        
        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public void TestConfigurationHelper_ShouldGetJobSchedulerOptions()
    {
        // Arrange & Act
        using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
        var options = setup.GetJobSchedulerOptions();
        
        // Assert
        options.Should().NotBeNull();
        options.Jobs.Should().HaveCount(2);
    }
}
