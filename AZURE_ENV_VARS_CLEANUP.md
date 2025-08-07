# Environment Variables Safe to Remove

## ✅ SAFE TO DELETE from Azure Container App Environment Variables

These are now handled by appsettings.json files and can be removed immediately:

```bash
DD_ENV                   # Now in AppSettings.DatadogEnvironment
DD_SERVICE              # Now in AppSettings.ServiceName
DD_VERSION              # Now in AppSettings.Version  
DD_SITE                 # Now in JobScheduler.Logging.DatadogSite
ENVIRONMENT             # Now in AppSettings.Environment
```

## ⚠️ NEEDS MIGRATION FIRST

This one can be removed after confirming appsettings.json is working:

```bash
AZURE_KEY_VAULT_URL     # Now in AppSettings.KeyVaultUrl
```

## 🔒 MUST KEEP - Secrets from Key Vault

```bash
DATADOG_API_KEY                    # Secret - retrieved from Key Vault
JobScheduler_Logging_DatadogApiKey # Secret - retrieved from Key Vault  
AzureWebJobsStorage               # Secret - Azure storage connection
```

## 🚀 MUST KEEP - Azure Functions Runtime

```bash
APPLICATIONINSIGHTS_CONNECTION_STRING  # Application Insights
FUNCTIONS_EXTENSION_VERSION           # Azure Functions version
FUNCTIONS_WORKER_RUNTIME             # Runtime stack (.NET)
FUNCTION_APP_EDIT_MODE               # Function editing mode
WEBSITES_ENABLE_APP_SERVICE_STORAGE  # App Service storage
```

## 🔧 KEEP FOR NOW - Deployment Related

```bash
DOCKER_CUSTOM_IMAGE_NAME
DOCKER_REGISTRY_SERVER_PASSWORD      # Secret for container registry
DOCKER_REGISTRY_SERVER_URL
DOCKER_REGISTRY_SERVER_USERNAME
JENKINS_BUILD_NUMBER
LAST_JENKINS_DEPLOY
MACHINEKEY_DecryptionKey            # Secret for encryption
```

## 🎯 Immediate Action
You can safely remove these 5 environment variables from Azure right now:
- DD_ENV
- DD_SERVICE  
- DD_VERSION
- DD_SITE
- ENVIRONMENT

This will clean up your Azure configuration and prove that appsettings.json is working correctly.
