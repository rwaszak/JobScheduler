using FluentAssertions;
using JobScheduler.FunctionApp.Services;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests
{
    public class JobConfigurationProviderTests : IDisposable
    {
        public JobConfigurationProviderTests()
        {
            // Clear environment variables before each test
            Environment.SetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT", null);
            Environment.SetEnvironmentVariable("DAILY_BATCH_ENDPOINT", null);
            Environment.SetEnvironmentVariable("DAILY_BATCH_TOKEN", null);
        }

        [Fact]
        public void GetJobConfig_ExistingJob_ReturnsConfiguration()
        {
            // Arrange
            Environment.SetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT", "https://test.api.com/health");
            var provider = new JobConfigurationProvider();

            // Act
            var config = provider.GetJobConfig("container-app-health");

            // Assert
            config.Should().NotBeNull();
            config.JobName.Should().Be("container-app-health");
            config.Endpoint.Should().Be("https://test.api.com/health");
            config.HttpMethod.Should().Be(HttpMethod.Get);
            config.AuthType.Should().Be("none");
        }

        [Fact]
        public void GetJobConfig_NonexistentJob_ThrowsArgumentException()
        {
            // Arrange
            var provider = new JobConfigurationProvider();

            // Act & Assert
            var action = () => provider.GetJobConfig("nonexistent-job");
            action.Should().Throw<ArgumentException>()
                .WithMessage("*nonexistent-job*");
        }

        [Fact]
        public void GetAllJobConfigs_ReturnsAllConfigurations()
        {
            // Arrange
            Environment.SetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT", "https://test.api.com/health");
            Environment.SetEnvironmentVariable("DAILY_BATCH_ENDPOINT", "https://test.api.com/batch");
            var provider = new JobConfigurationProvider();

            // Act
            var configs = provider.GetAllJobConfigs().ToList();

            // Assert
            configs.Should().HaveCount(2);
            configs.Should().Contain(c => c.JobName == "container-app-health");
            configs.Should().Contain(c => c.JobName == "daily-batch");
        }

        [Fact]
        public void LoadJobConfigurationsFromEnvironment_WithMissingEndpoint_UsesEmptyString()
        {
            // Arrange
            Environment.SetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT", null);
            var provider = new JobConfigurationProvider();

            // Act
            var config = provider.GetJobConfig("container-app-health");

            // Assert
            config.Endpoint.Should().Be("");
        }

        [Fact]
        public void JobConfiguration_HasCorrectTags()
        {
            // Arrange
            Environment.SetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT", "https://test.api.com/health");
            var provider = new JobConfigurationProvider();

            // Act
            var config = provider.GetJobConfig("container-app-health");

            // Assert
            config.Tags.Should().ContainKey("category");
            config.Tags.Should().ContainKey("priority");
            config.Tags.Should().ContainKey("team");
            config.Tags["category"].Should().Be("health-check");
            config.Tags["priority"].Should().Be("low");
            config.Tags["team"].Should().Be("platform");
        }

        public void Dispose()
        {
            // Clean up environment variables after each test
            Environment.SetEnvironmentVariable("INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT", null);
            Environment.SetEnvironmentVariable("DAILY_BATCH_ENDPOINT", null);
            Environment.SetEnvironmentVariable("DAILY_BATCH_TOKEN", null);
        }
    }
}
