using JobScheduler.FunctionApp.Core.Interfaces;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestSecretManager : ISecretManager
    {
        private readonly Dictionary<string, string> _secrets = new();

        public void AddSecret(string name, string value)
        {
            _secrets[name] = value;
        }

        public Task<string> GetSecretAsync(string secretName)
        {
            if (_secrets.TryGetValue(secretName, out var value))
            {
                return Task.FromResult(value);
            }
            
            throw new InvalidOperationException($"Secret '{secretName}' not found");
        }

        public void Clear() => _secrets.Clear();
    }
}
