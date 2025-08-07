using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.FunctionApp.Services;

/// <summary>
/// Azure Key Vault implementation of ISecretManager that retrieves secrets from Azure Key Vault.
/// Falls back to environment variables for local development when Key Vault URL is not configured.
/// </summary>
public class KeyVaultSecretManager : ISecretManager
{
    private readonly SecretClient? _secretClient;
    private readonly ILogger<KeyVaultSecretManager> _logger;
    private readonly bool _useEnvironmentFallback;
    private readonly AppSettings _appSettings;

    public KeyVaultSecretManager(ILogger<KeyVaultSecretManager> logger, IOptions<AppSettings> appSettings)
    {
        _logger = logger;
        _appSettings = appSettings.Value;
        
        // Get Key Vault URL from configuration
        var keyVaultUrl = _appSettings.KeyVaultUrl;
        
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            try
            {
                // Use DefaultAzureCredential which works for:
                // - Local development (Visual Studio, Azure CLI, etc.)
                // - Azure environments (Managed Identity)
                var credential = new DefaultAzureCredential();
                _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
                _useEnvironmentFallback = false;
                
                _logger.LogInformation("Key Vault Secret Manager initialized with URL: {KeyVaultUrl}", keyVaultUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Key Vault client. Falling back to environment variables.");
                _useEnvironmentFallback = true;
            }
        }
        else
        {
            _logger.LogWarning("KeyVaultUrl not configured in AppSettings. Using environment variable fallback for local development.");
            _useEnvironmentFallback = true;
        }
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        if (_useEnvironmentFallback || _secretClient == null)
        {
            // Fallback to environment variables for local development
            var value = Environment.GetEnvironmentVariable(secretName);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"Secret '{secretName}' not found in environment variables");
            }
            
            _logger.LogDebug("Retrieved secret '{SecretName}' from environment variables", secretName);
            return value;
        }

        try
        {
            // Convert secret name to Key Vault friendly format (replace underscores with hyphens)
            var keyVaultSecretName = secretName.Replace("_", "-").ToLowerInvariant();
            
            _logger.LogDebug("Retrieving secret '{SecretName}' (Key Vault name: '{KeyVaultName}') from Azure Key Vault", 
                secretName, keyVaultSecretName);

            var response = await _secretClient.GetSecretAsync(keyVaultSecretName);
            
            if (response?.Value?.Value == null)
            {
                throw new InvalidOperationException($"Secret '{secretName}' (Key Vault name: '{keyVaultSecretName}') returned null value from Key Vault");
            }

            _logger.LogDebug("Successfully retrieved secret '{SecretName}' from Azure Key Vault", secretName);
            return response.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException($"Secret '{secretName}' not found in Azure Key Vault", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from Azure Key Vault", secretName);
            throw new InvalidOperationException($"Failed to retrieve secret '{secretName}' from Azure Key Vault: {ex.Message}", ex);
        }
    }
}
