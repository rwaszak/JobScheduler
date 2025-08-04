namespace JobScheduler.FunctionApp.Core.Interfaces
{
    public interface ISecretManager
    {
        Task<string> GetSecretAsync(string secretName);
    }
}
