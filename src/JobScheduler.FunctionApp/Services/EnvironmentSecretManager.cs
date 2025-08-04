using JobScheduler.FunctionApp.Core.Interfaces;

namespace JobScheduler.FunctionApp.Services
{
    public class EnvironmentSecretManager : ISecretManager
    {
        public Task<string> GetSecretAsync(string secretName)
        {
            var value = Environment.GetEnvironmentVariable(secretName);
            return Task.FromResult(value ?? throw new InvalidOperationException($"Secret '{secretName}' not found"));
        }
    }
}