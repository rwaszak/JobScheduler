# Secrets Management Strategy

## Current State
- Using Jenkins credentials for deployment secrets
- Environment variables for runtime secrets (not secure)
- Datadog API key passed as env var

## Target State: Azure Key Vault Integration

### 1. Secrets to be Managed
| Secret | Current Location | Target Location | Usage |
|--------|------------------|-----------------|-------|
| `DATADOG_API_KEY` | Jenkins credential | Key Vault | Observability |
| `AzureWebJobsStorage` | Environment variable | Key Vault | Functions runtime |
| Service principal secrets | Jenkins credential | Key Vault (deployment only) | CI/CD authentication |
| Job authentication tokens | None (future) | Key Vault | HTTP client auth |

### 2. Implementation Plan

#### Phase 1: Key Vault Setup
- [x] Create Azure Key Vault in resource group
- [x] Configure Managed Identity for Function App
- [x] Grant Key Vault access to Managed Identity
- [x] Store secrets in Key Vault

#### Phase 2: Function App Integration
- [x] Update app settings to use Key Vault references
- [x] Test Key Vault access from Function App
- [x] Update deployment scripts

#### Phase 3: Application Code Updates
- [x] Update configuration model for Key Vault
- [x] Test secret retrieval in Functions
- [x] Remove hardcoded secrets

### 3. Key Vault Reference Format
```
@Microsoft.KeyVault(VaultName=<vault-name>;SecretName=<secret-name>)
```

### 4. Managed Identity Permissions
The Function App's managed identity needs:
- `Key Vault Secrets User` role on the Key Vault
- `GET` permission for secrets

### 5. Deployment Integration
- Jenkins will store secrets in Key Vault during deployment
- Function App reads secrets via Managed Identity
- No secrets in environment variables or app settings
