// Services/SecretManager.cs
using HelloWorldFunctionApp.Core.Interfaces;

namespace HelloWorldFunctionApp.Services
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