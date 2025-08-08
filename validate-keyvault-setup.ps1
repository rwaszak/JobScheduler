#!/usr/bin/env pwsh

# PowerShell script to validate Key Vault setup for Function App
param(
    [string]$ResourceGroup = "continuum_scheduled_jobs",
    [string]$FunctionAppName = "job-scheduler-poc-container", 
    [string]$KeyVaultName = "jobscheduler-poc-kv"
)

Write-Host "=== Key Vault Setup Validation ===" -ForegroundColor Green

# Check if Azure CLI is logged in
Write-Host "Checking Azure CLI authentication..." -ForegroundColor Yellow
try {
    az account show --query "user.name" -o tsv | Out-Null
    Write-Host "✅ Azure CLI authenticated" -ForegroundColor Green
} catch {
    Write-Host "❌ Azure CLI not authenticated. Run 'az login' first" -ForegroundColor Red
    exit 1
}

# Check Function App exists and has managed identity
Write-Host "Checking Function App managed identity..." -ForegroundColor Yellow
$functionIdentity = az functionapp identity show --name $FunctionAppName --resource-group $ResourceGroup --query "principalId" -o tsv 2>$null
if ($functionIdentity) {
    Write-Host "✅ Function App has managed identity: $functionIdentity" -ForegroundColor Green
} else {
    Write-Host "❌ Function App managed identity not found" -ForegroundColor Red
    exit 1
}

# Check Key Vault exists
Write-Host "Checking Key Vault accessibility..." -ForegroundColor Yellow
try {
    az keyvault show --name $KeyVaultName --resource-group $ResourceGroup --query "name" -o tsv | Out-Null
    Write-Host "✅ Key Vault accessible: $KeyVaultName" -ForegroundColor Green
} catch {
    Write-Host "❌ Key Vault not accessible: $KeyVaultName" -ForegroundColor Red
    exit 1
}

# Check access policies
Write-Host "Checking Function App access to Key Vault..." -ForegroundColor Yellow
$accessPolicies = az keyvault show --name $KeyVaultName --resource-group $ResourceGroup --query "properties.accessPolicies[?objectId=='$functionIdentity'].permissions.secrets" -o tsv 2>$null
if ($accessPolicies -match "get" -and $accessPolicies -match "list") {
    Write-Host "✅ Function App has proper Key Vault access policies" -ForegroundColor Green
} else {
    Write-Host "❌ Function App missing Key Vault access policies" -ForegroundColor Red
    Write-Host "Current permissions: $accessPolicies" -ForegroundColor Yellow
}

# Check if required secrets exist in Key Vault
Write-Host "Checking required secrets in Key Vault..." -ForegroundColor Yellow
$requiredSecrets = @("azure-webjobs-storage", "applicationinsights-connection-string", "datadog-api-key", "docker-registry-password")

foreach ($secret in $requiredSecrets) {
    try {
        az keyvault secret show --vault-name $KeyVaultName --name $secret --query "name" -o tsv 2>$null | Out-Null
        Write-Host "✅ Secret exists: $secret" -ForegroundColor Green
    } catch {
        Write-Host "❌ Secret missing: $secret" -ForegroundColor Red
    }
}

# Check Function App settings for Key Vault references
Write-Host "Checking Function App Key Vault references..." -ForegroundColor Yellow
$appSettings = az functionapp config appsettings list --name $FunctionAppName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json

$keyVaultReferences = @{
    "AzureWebJobsStorage" = "azure-webjobs-storage"
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = "applicationinsights-connection-string"
    "JobScheduler__Logging__DatadogApiKey" = "datadog-api-key"
    "DOCKER_REGISTRY_SERVER_PASSWORD" = "docker-registry-password"
}

foreach ($setting in $keyVaultReferences.GetEnumerator()) {
    $appSetting = $appSettings | Where-Object { $_.name -eq $setting.Key }
    if ($appSetting -and $appSetting.value -match "@Microsoft\.KeyVault.*$($setting.Value)") {
        Write-Host "✅ Key Vault reference configured: $($setting.Key)" -ForegroundColor Green
    } else {
        Write-Host "❌ Key Vault reference missing or incorrect: $($setting.Key)" -ForegroundColor Red
        if ($appSetting) {
            Write-Host "   Current value: $($appSetting.value)" -ForegroundColor Yellow
        }
    }
}

Write-Host "=== Validation Complete ===" -ForegroundColor Green
Write-Host "If any issues were found above, run your Jenkins deployment pipeline to fix them." -ForegroundColor Yellow
