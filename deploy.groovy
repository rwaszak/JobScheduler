def call(Map config) {
    echo "Deploying JobScheduler Functions to ${config.environment} environment"

    // FOR TESTING: Use existing Functions app first, then switch to per-env later
    def useExistingApp = env.USE_EXISTING_APP?.toBoolean() ?: true
    
    def resourceGroup
    def functionAppName
    def appServicePlan
    def storageAccount
    
    if (useExistingApp) {
        // Use your existing manually deployed Functions app for testing
        resourceGroup = "continuum_scheduled_jobs"  // From your Azure portal screenshot
        functionAppName = "job-scheduler-poc-container"  // NEW: Container-compatible test app
        echo "TESTING MODE: Deploying to container-compatible Functions app: ${functionAppName}"
    } else {
        // Future: Azure Functions App configuration per environment
        resourceGroup = "jobscheduler-functions-${config.environment}-rg"
        functionAppName = "jobscheduler-functions-${config.environment}"
        appServicePlan = "jobscheduler-functions-${config.environment}-plan"
        storageAccount = "jobschedstorage${config.environment}"
        echo "PRODUCTION MODE: Deploying to new Functions app: ${functionAppName}"
    }
    
    // For Container Apps deployment (alternative approach)
    def containerAppResourceGroup = "jobscheduler-capp-${config.environment}-rg"
    def containerAppName = "jobscheduler-functions-capp-${config.environment}"
    def containerAppEnv = "jobscheduler-capp-env-${config.environment}"
    
    def applicationPort = 80
    def cpuSize = "1"
    def memorySize = "2Gi"

    withCredentials([
            azureServicePrincipal('jenkins-service-principal-2'),
            string(credentialsId: 'datadog-api-key', variable: 'DD_API_KEY')
    ]) {
        // Azure login
        sh """
            az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET -t $AZURE_TENANT_ID
            az account set --subscription $AZURE_SUBSCRIPTION_ID
        """

        // Choose deployment method: Azure Functions or Container Apps
        def deploymentMethod = env.DEPLOYMENT_METHOD ?: 'functions' // Default to Azure Functions

        if (deploymentMethod == 'functions') {
            if (useExistingApp) {
                deployToExistingFunctionsApp(config, resourceGroup, functionAppName)
            } else {
                deployToAzureFunctions(config, resourceGroup, functionAppName, appServicePlan, storageAccount)
            }
        } else if (deploymentMethod == 'container-apps') {
            deployToContainerApps(config, containerAppResourceGroup, containerAppName, containerAppEnv, applicationPort, cpuSize, memorySize)
        } else {
            error "Unknown deployment method: ${deploymentMethod}. Use 'functions' or 'container-apps'"
        }

        sh "az logout"
    }
}

def deployToExistingFunctionsApp(config, resourceGroup, functionAppName) {
    echo "TESTING: Creating/updating container-compatible Azure Functions app: ${functionAppName}"
    
    sh """
        # Create Key Vault for secrets management (always ensure it exists)
        az keyvault create \\
            --name ${functionAppName}-kv \\
            --resource-group ${resourceGroup} \\
            --location centralus \\
            --sku standard \\
            --enable-rbac-authorization true || echo "Key Vault might already exist, continuing..."

        # Check if the Function App exists (proper Jenkins shell syntax)
        set +e  # Don't fail on error for the check
        az functionapp show --name ${functionAppName} --resource-group ${resourceGroup} > /dev/null 2>&1
        APP_EXISTS=\$?
        set -e  # Re-enable fail on error
        
        if [ \$APP_EXISTS -eq 0 ]; then
            echo "Function App ${functionAppName} exists - updating container"
            
            # Update the existing Function App with new container image
            az functionapp config container set \\
                --name ${functionAppName} \\
                --resource-group ${resourceGroup} \\
                --image ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${config.buildVersion} \\
                --registry-server https://${env.DOCKER_REGISTRY_NAME}.azurecr.io
        else
            echo "Function App ${functionAppName} does not exist - creating new container-compatible Function App"
            
            # Create storage account if it doesn't exist (required for Function Apps)
            az storage account create \\
                --name jobschedulerteststorage \\
                --resource-group ${resourceGroup} \\
                --location centralus \\
                --sku Standard_LRS \\
                --kind StorageV2 \\
                --allow-blob-public-access false || echo "Storage account might already exist, continuing..."
            
            # Create a new Premium Function App that supports containers
            # First create an App Service Plan (Premium V2 for Functions)
            az appservice plan create \\
                --name ${functionAppName}-plan \\
                --resource-group ${resourceGroup} \\
                --location centralus \\
                --sku P1V2 \\
                --is-linux
            
            # Create Function App with container support
            az functionapp create \\
                --name ${functionAppName} \\
                --resource-group ${resourceGroup} \\
                --plan ${functionAppName}-plan \\
                --storage-account jobschedulerteststorage \\
                --runtime dotnet-isolated \\
                --runtime-version 8 \\
                --functions-version 4 \\
                --image ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${config.buildVersion} \\
                --registry-server https://${env.DOCKER_REGISTRY_NAME}.azurecr.io \\
                --assign-identity
        fi

        # Ensure Function App has managed identity and Key Vault access (regardless of whether app was created or updated)
        # Get the Function App's managed identity principal ID
        FUNCTION_APP_IDENTITY=\$(az functionapp identity show --name ${functionAppName} --resource-group ${resourceGroup} --query principalId -o tsv)
        echo "Function App Managed Identity: \$FUNCTION_APP_IDENTITY"

        # Grant Key Vault Secrets User role to the Function App's managed identity
        az role assignment create \\
            --assignee \$FUNCTION_APP_IDENTITY \\
            --role "Key Vault Secrets User" \\
            --scope "/subscriptions/\$(az account show --query id -o tsv)/resourceGroups/${resourceGroup}/providers/Microsoft.KeyVault/vaults/${functionAppName}-kv" || echo "Role assignment might already exist, continuing..."

        # Store secrets in Key Vault
        az keyvault secret set --vault-name ${functionAppName}-kv --name "datadog-api-key" --value "${DD_API_KEY}"
        
        # Get storage account connection string and store in Key Vault
        STORAGE_CONN_STRING=\$(az storage account show-connection-string --name jobschedulerteststorage --resource-group ${resourceGroup} --query connectionString -o tsv)
        az keyvault secret set --vault-name ${functionAppName}-kv --name "azure-webjobs-storage" --value "\$STORAGE_CONN_STRING"

        # Update app settings with Key Vault references (keeping existing environment variables)
        az functionapp config appsettings set \\
            --name ${functionAppName} \\
            --resource-group ${resourceGroup} \\
            --settings \\
                DOCKER_REGISTRY_SERVER_URL="https://${env.DOCKER_REGISTRY_NAME}.azurecr.io" \\
                DD_VERSION="${config.buildVersion}" \\
                LAST_JENKINS_DEPLOY="\$(date)" \\
                JENKINS_BUILD_NUMBER="${config.buildNumber}" \\
                FUNCTIONS_WORKER_RUNTIME="dotnet-isolated" \\
                WEBSITES_ENABLE_APP_SERVICE_STORAGE=false \\
                AzureWebJobsStorage="@Microsoft.KeyVault(VaultName=${functionAppName}-kv;SecretName=azure-webjobs-storage)" \\
                AZURE_KEY_VAULT_URL="https://${functionAppName}-kv.vault.azure.net/" \\
                DATADOG_API_KEY="${DD_API_KEY}" \\
                JobScheduler__Logging__DatadogApiKey="@Microsoft.KeyVault(VaultName=${functionAppName}-kv;SecretName=datadog-api-key)" \\
                DD_SITE="us3.datadoghq.com" \\
                DD_ENV="${config.environment}" \\
                DD_SERVICE="jobscheduler-functions" \\
                ENVIRONMENT="${config.environment}"

        # Restart the function app to pick up new container
        az functionapp restart --name ${functionAppName} --resource-group ${resourceGroup}

        # Force sync function triggers to refresh function metadata and clear portal cache
        echo "Syncing function triggers to refresh Azure Portal function metadata..."
        SUBSCRIPTION_ID=\$(az account show --query id -o tsv)
        az rest --method post --url "https://management.azure.com/subscriptions/\$SUBSCRIPTION_ID/resourceGroups/${resourceGroup}/providers/Microsoft.Web/sites/${functionAppName}/syncfunctiontriggers?api-version=2016-08-01"
        echo "Function metadata sync completed - Portal should now reflect current functions"

        echo "TESTING: Function App ready: https://${functionAppName}.azurewebsites.net"
        echo "You can test the deployment at:"
        echo "  Health: https://${functionAppName}.azurewebsites.net/api/health"
        echo "  Jobs:   https://${functionAppName}.azurewebsites.net/api/jobs"
    """
}

def deployToAzureFunctions(config, resourceGroup, functionAppName, appServicePlan, storageAccount) {
    echo "Deploying to Azure Functions: ${functionAppName}"
    
    sh """
        # Create resource group if it doesn't exist
        az group create --name ${resourceGroup} --location centralus --tags Environment=${config.environment} Project=JobScheduler

        # Create storage account if it doesn't exist
        az storage account create \\
            --name ${storageAccount} \\
            --resource-group ${resourceGroup} \\
            --location centralus \\
            --sku Standard_LRS \\
            --kind StorageV2

        # Create App Service Plan if it doesn't exist
        az appservice plan create \\
            --name ${appServicePlan} \\
            --resource-group ${resourceGroup} \\
            --location centralus \\
            --sku B1 \\
            --is-linux

        # Create Function App if it doesn't exist
        az functionapp create \\
            --name ${functionAppName} \\
            --resource-group ${resourceGroup} \\
            --plan ${appServicePlan} \\
            --storage-account ${storageAccount} \\
            --runtime dotnet-isolated \\
            --runtime-version 8 \\
            --functions-version 4 \\
            --deployment-container-image-name ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${config.buildVersion}

        # Configure Function App settings
        az functionapp config appsettings set \\
            --name ${functionAppName} \\
            --resource-group ${resourceGroup} \\
            --settings \\
                AzureWebJobsStorage="${AZURE_STORAGE_CONNECTION_STRING}" \\
                FUNCTIONS_WORKER_RUNTIME="dotnet-isolated" \\
                WEBSITES_ENABLE_APP_SERVICE_STORAGE=false \\
                DOCKER_REGISTRY_SERVER_URL="https://${env.DOCKER_REGISTRY_NAME}.azurecr.io" \\
                DATADOG_API_KEY="${DD_API_KEY}" \\
                DD_SITE="us3.datadoghq.com" \\
                DD_ENV="${config.environment}" \\
                DD_SERVICE="jobscheduler-functions" \\
                DD_VERSION="${config.buildVersion}" \\
                ENVIRONMENT="${config.environment}"

        # Enable container registry authentication
        az functionapp config container set \\
            --name ${functionAppName} \\
            --resource-group ${resourceGroup} \\
            --docker-custom-image-name ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${config.buildVersion} \\
            --docker-registry-server-url https://${env.DOCKER_REGISTRY_NAME}.azurecr.io

        # Restart the function app to pick up new container
        az functionapp restart --name ${functionAppName} --resource-group ${resourceGroup}

        echo "Function App deployment completed: https://${functionAppName}.azurewebsites.net"
    """
}

def deployToContainerApps(config, resourceGroup, containerAppName, containerAppEnv, applicationPort, cpuSize, memorySize) {
    echo "Deploying to Azure Container Apps: ${containerAppName}"

    def today = new Date()
    def dateStr = today.format('yyyyMMdd')
    def revisionSuffix = "${dateStr}-${config.buildNumber}"

    sh """
        # Create resource group if it doesn't exist
        az group create --name ${resourceGroup} --location centralus --tags Environment=${config.environment} Project=JobScheduler

        # Create Container Apps Environment if it doesn't exist
        az containerapp env create \\
            --name ${containerAppEnv} \\
            --resource-group ${resourceGroup} \\
            --location centralus

        # Create or update Container App
        az containerapp create \\
            --name ${containerAppName} \\
            --resource-group ${resourceGroup} \\
            --environment ${containerAppEnv} \\
            --image ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${config.buildVersion} \\
            --target-port ${applicationPort} \\
            --ingress external \\
            --cpu ${cpuSize} \\
            --memory ${memorySize} \\
            --min-replicas 1 \\
            --max-replicas 3 \\
            --registry-server ${env.DOCKER_REGISTRY_NAME}.azurecr.io \\
            --env-vars \\
                AzureWebJobsStorage=secretref:azure-storage-connection-string \\
                FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \\
                WEBSITES_PORT=${applicationPort} \\
                AzureWebJobsScriptRoot=/home/site/wwwroot \\
                AzureFunctionsJobHost__Logging__Console__IsEnabled=true \\
                DD_API_KEY=secretref:dd-api-key \\
                DD_SITE=us3.datadoghq.com \\
                DD_ENV=${config.environment} \\
                DD_SERVICE=jobscheduler-functions \\
                DD_VERSION=${config.buildVersion} \\
                ENVIRONMENT=${config.environment} \\
            --secrets \\
                azure-storage-connection-string="${AZURE_STORAGE_CONNECTION_STRING}" \\
                dd-api-key="${DD_API_KEY}" \\
            --revision-suffix ${revisionSuffix}

        # Get the container app URL
        CONTAINER_APP_URL=\$(az containerapp show --name ${containerAppName} --resource-group ${resourceGroup} --query properties.configuration.ingress.fqdn -o tsv)
        echo "Container App deployment completed: https://\$CONTAINER_APP_URL"
    """
}

return this
