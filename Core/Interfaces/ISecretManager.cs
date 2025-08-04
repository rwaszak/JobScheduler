namespace HelloWorldFunctionApp.Core.Interfaces
{
    public interface ISecretManager
    {
        Task<string> GetSecretAsync(string secretName);
    }
}
