# Azure Functions Environment Variables Migration Plan

## 🎯 **Goal: Zero Manual Azure Environment Variables**

Move ALL manually configured environment variables to automated deployment via `deploy.groovy`.

## 🔧 **Current State Analysis**

### ✅ Already Automated in deploy.groovy:
```groovy
FUNCTIONS_WORKER_RUNTIME="dotnet-isolated"
WEBSITES_ENABLE_APP_SERVICE_STORAGE=false
AzureWebJobsStorage="@Microsoft.KeyVault(...)"  # Secret from Key Vault
JobScheduler__Logging__DatadogApiKey="@Microsoft.KeyVault(...)"  # Secret from Key Vault
```

### 📝 **Need to Add to deploy.groovy:**
```groovy
FUNCTIONS_EXTENSION_VERSION="~4"
FUNCTION_APP_EDIT_MODE="readwrite"
APPLICATIONINSIGHTS_CONNECTION_STRING="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=app-insights-connection)"
```

### 🗑️ **Can Remove from Azure (Phase 1 Complete):**
```groovy
DD_VERSION="${config.buildVersion}"      # ✅ Already automated
DD_ENV="${config.environment}"           # ✅ Already automated  
DD_SERVICE="jobscheduler-functions"      # ✅ Already automated
DD_SITE="us3.datadoghq.com"             # ✅ Already automated
ENVIRONMENT="${config.environment}"      # ✅ Already automated
AZURE_KEY_VAULT_URL="https://${keyVaultName}.vault.azure.net/"  # ✅ Already automated
```

## 🚀 **Enhanced deploy.groovy Configuration**

```groovy
# Complete Azure Functions runtime configuration
az functionapp config appsettings set \\
    --name ${functionAppName} \\
    --resource-group ${resourceGroup} \\
    --settings \\
        # Azure Functions Runtime (Required)
        FUNCTIONS_EXTENSION_VERSION="~4" \\
        FUNCTIONS_WORKER_RUNTIME="dotnet-isolated" \\
        FUNCTION_APP_EDIT_MODE="readwrite" \\
        WEBSITES_ENABLE_APP_SERVICE_STORAGE=false \\
        # Secrets from Key Vault
        AzureWebJobsStorage="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=azure-webjobs-storage)" \\
        APPLICATIONINSIGHTS_CONNECTION_STRING="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=app-insights-connection)" \\
        JobScheduler__Logging__DatadogApiKey="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=datadog-api-key)" \\
        # Container Registry (Deployment)
        DOCKER_REGISTRY_SERVER_URL="https://${env.DOCKER_REGISTRY_NAME}.azurecr.io" \\
        DOCKER_REGISTRY_SERVER_USERNAME="${env.DOCKER_REGISTRY_NAME}" \\
        DOCKER_REGISTRY_SERVER_PASSWORD="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=docker-registry-password)" \\
        # Build/Deploy Metadata
        DD_VERSION="${config.buildVersion}" \\
        JENKINS_BUILD_NUMBER="${config.buildNumber}" \\
        LAST_JENKINS_DEPLOY="\$(date)" \\
        # Key Vault URL (Temporary - will be removed after appsettings.json migration)
        AZURE_KEY_VAULT_URL="https://${keyVaultName}.vault.azure.net/" \\
        # Datadog Configuration (Temporary - will be removed after appsettings.json migration)
        DD_ENV="${config.environment}" \\
        DD_SERVICE="jobscheduler-functions" \\
        DD_SITE="us3.datadoghq.com" \\
        ENVIRONMENT="${config.environment}"
```

## 🎯 **Action Plan**

1. **✅ DONE:** Application config moved to appsettings.json
2. **✅ DONE:** Added missing runtime vars to deploy.groovy
3. **🗑️ NEXT:** Remove all manually configured Azure env vars

## 🚀 **What Just Got Automated**

Your `deploy.groovy` now handles ALL Azure Functions runtime configuration:

```groovy
# Added to all deployment methods:
FUNCTIONS_EXTENSION_VERSION="~4"        # ✅ AUTOMATED
FUNCTION_APP_EDIT_MODE="readwrite"       # ✅ AUTOMATED  
APPLICATIONINSIGHTS_CONNECTION_STRING="@Microsoft.KeyVault(...)"  # ✅ AUTOMATED
DOCKER_REGISTRY_SERVER_USERNAME="${env.DOCKER_REGISTRY_NAME}"     # ✅ AUTOMATED
DOCKER_REGISTRY_SERVER_PASSWORD="@Microsoft.KeyVault(...)"        # ✅ AUTOMATED
```

## 🎯 **Result: ZERO Manual Azure Environment Variables Required!**

## 💡 **Key Benefits**
- ✅ **Zero manual Azure configuration**
- 🔒 **Secrets managed via Key Vault references** 
- 🏗️ **Full Infrastructure as Code**
- 🚀 **Environment-specific automated deployment**
- 📝 **Version-controlled configuration**

## ⚠️ **Important Notes**
- Azure Functions runtime vars CANNOT go in appsettings.json (read before app starts)
- Secrets MUST use Key Vault references for security
- deploy.groovy provides the perfect automation layer for this
