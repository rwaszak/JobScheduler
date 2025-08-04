using FluentAssertions;
using JobScheduler.FunctionApp.Core.Models;
using Xunit;

namespace JobScheduler.FunctionApp.Tests.UnitTests
{
    public class RetryPolicyTests
    {
        [Fact]
        public void RetryPolicy_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var policy = new RetryPolicy();

            // Assert
            policy.MaxAttempts.Should().Be(3);
            policy.BaseDelayMs.Should().Be(1000);
            policy.BackoffMultiplier.Should().Be(2.0);
            policy.MaxDelayMs.Should().Be(30000);
            policy.RetryableStatusCodes.Should().Contain(new[] { 429, 502, 503, 504 });
        }

        [Theory]
        [InlineData(1, 1000, 1000)]
        [InlineData(2, 1000, 2000)]
        [InlineData(3, 1000, 4000)]
        [InlineData(4, 1000, 8000)]
        [InlineData(5, 1000, 16000)]
        public void CalculateBackoffDelay_WithExponentialBackoff_ReturnsCorrectDelay(
            int attempt, int baseDelayMs, int expectedDelayMs)
        {
            // Arrange
            var policy = new RetryPolicy
            {
                BaseDelayMs = baseDelayMs,
                BackoffMultiplier = 2.0,
                MaxDelayMs = 30000
            };

            // Act
            var delay = CalculateBackoffDelay(policy, attempt);

            // Assert
            delay.TotalMilliseconds.Should().Be(expectedDelayMs);
        }

        [Fact]
        public void CalculateBackoffDelay_RespectsMaxDelay()
        {
            // Arrange
            var policy = new RetryPolicy
            {
                BaseDelayMs = 1000,
                BackoffMultiplier = 2.0,
                MaxDelayMs = 5000 // Cap at 5 seconds
            };

            // Act
            var delay = CalculateBackoffDelay(policy, 10); // Would normally be 512 seconds

            // Assert
            delay.TotalMilliseconds.Should().Be(5000);
        }

        // Helper method to test backoff calculation (copy the logic from JobExecutor)
        private TimeSpan CalculateBackoffDelay(RetryPolicy policy, int attempt)
        {
            var delay = policy.BaseDelayMs * Math.Pow(policy.BackoffMultiplier, attempt - 1);
            var delayMs = Math.Min(delay, policy.MaxDelayMs);
            return TimeSpan.FromMilliseconds(delayMs);
        }
    }
}
