using FluentAssertions;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Tests.TestHelpers;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests
{
    /// <summary>
    /// Tests for the OptionsJobConfigurationProvider using appsettings.json-style configuration.
    /// This replaces the old environment variable approach with the runtime configuration approach.
    /// </summary>
    public class JobConfigurationProviderTests
    {
        [Fact]
        public void GetJobConfig_ExistingJob_ReturnsConfiguration()
        {
            // Arrange
            using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
            var provider = setup.GetJobConfigurationProvider();

            // Act
            var config = provider.GetJobConfig(JobNames.ContainerAppHealth);

            // Assert
            config.Should().NotBeNull();
            config.JobName.Should().Be(JobNames.ContainerAppHealth);
            config.Endpoint.Should().Be("https://test-api.example.com/health");
            config.HttpMethod.Should().Be(HttpMethod.Get);
            config.AuthType.Should().Be(AuthenticationType.None);
            config.TimeoutSeconds.Should().Be(30);
        }

        [Fact]
        public void GetJobConfig_NonexistentJob_ThrowsArgumentException()
        {
            // Arrange
            using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
            var provider = setup.GetJobConfigurationProvider();

            // Act & Assert
            var action = () => provider.GetJobConfig("nonexistent-job");
            action.Should().Throw<ArgumentException>()
                .WithMessage("*nonexistent-job*");
        }

        [Fact]
        public void GetAllJobConfigs_ReturnsAllConfigurations()
        {
            // Arrange
            using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
            var provider = setup.GetJobConfigurationProvider();

            // Act
            var configs = provider.GetAllJobConfigs().ToList();

            // Assert
            configs.Should().HaveCount(1);
            configs.Should().Contain(c => c.JobName == JobNames.ContainerAppHealth);
        }

        [Fact]
        public void JobConfiguration_HasCorrectDefaultRetryPolicy()
        {
            // Arrange
            using var setup = TestConfigurationHelper.CreateDefaultConfiguration();
            var provider = setup.GetJobConfigurationProvider();

            // Act
            var config = provider.GetJobConfig(JobNames.ContainerAppHealth);

            // Assert
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.MaxAttempts.Should().Be(3);
            config.RetryPolicy.BaseDelayMs.Should().Be(1000);
            config.RetryPolicy.BackoffMultiplier.Should().Be(2.0);
            config.RetryPolicy.MaxDelayMs.Should().Be(30000);
            config.RetryPolicy.RetryableStatusCodes.Should().Contain(new[] { 429, 502, 503, 504 });
        }
    }
}
