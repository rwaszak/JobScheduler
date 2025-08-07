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
    
    // Generate a valid Key Vault name (max 24 chars, no consecutive hyphens)
    def keyVaultName = "jobscheduler-poc-kv"  // 18 characters, compliant
    
    sh """
        # Create Key Vault for secrets management (always ensure it exists)
        echo "Creating/verifying Key Vault: ${keyVaultName}"
        
        # Check if Key Vault exists and delete if it has RBAC enabled (incompatible with access policies)
        if az keyvault show --name ${keyVaultName} --resource-group ${resourceGroup} 2>/dev/null; then
            echo "Key Vault exists, checking if RBAC is enabled..."
            RBAC_ENABLED=\$(az keyvault show --name ${keyVaultName} --resource-group ${resourceGroup} --query properties.enableRbacAuthorization -o tsv)
            if [ "\$RBAC_ENABLED" = "true" ]; then
                echo "Key Vault has RBAC enabled, deleting and recreating with access policies..."
                az keyvault delete --name ${keyVaultName} --resource-group ${resourceGroup}
                az keyvault purge --name ${keyVaultName} --location centralus || echo "Purge not needed or failed, continuing..."
                sleep 10
            else
                echo "Key Vault already uses access policies, good to continue"
            fi
        fi
        
        # Create Key Vault with access policies (no RBAC)
        az keyvault create \\
            --name ${keyVaultName} \\
            --resource-group ${resourceGroup} \\
            --location centralus \\
            --sku standard \\
            --enable-rbac-authorization false \\
            --default-action Allow \\
            --bypass AzureServices || echo "Key Vault might already exist, continuing..."
        
        # Wait for Key Vault to be fully available
        echo "Waiting for Key Vault to be available..."
        sleep 30
        
        # Verify Key Vault accessibility
        echo "Testing Key Vault connectivity..."
        az keyvault show --name ${keyVaultName} --resource-group ${resourceGroup} || {
            echo "Key Vault not accessible, waiting longer..."
            sleep 60
            az keyvault show --name ${keyVaultName} --resource-group ${resourceGroup}
        }

        # Grant access policies to Jenkins service principal for deployment operations
        echo "Setting access policy for Jenkins service principal..."
        az keyvault set-policy \\
            --name ${keyVaultName} \\
            --resource-group ${resourceGroup} \\
            --spn \$AZURE_CLIENT_ID \\
            --secret-permissions get set list delete || echo "Access policy might already be set, continuing..."

        # Check if the Function App exists (proper Jenkins shell syntax)
        set +e  # Don't fail on error for the check
        az functionapp show --name ${functionAppName} --resource-group ${resourceGroup} > /dev/null 2>&1
        APP_EXISTS=\$?
        set -e  # Re-enable fail on error
        
        if [ \$APP_EXISTS -eq 0 ]; then
            echo "Function App ${functionAppName} exists - updating container"
            
            # Ensure the Function App has managed identity enabled
            echo "Enabling managed identity on existing Function App..."
            az functionapp identity assign --name ${functionAppName} --resource-group ${resourceGroup}
            
            # Use the image tag passed from Jenkins pipeline
            IMAGE_TAG="${config.buildVersion}"
            echo "Using image tag from Jenkins: \$IMAGE_TAG"
            
            # Update the existing Function App with new container image
            az functionapp config container set \\
                --name ${functionAppName} \\
                --resource-group ${resourceGroup} \\
                --image ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:\$IMAGE_TAG \\
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
            
            # Use the image tag passed from Jenkins pipeline
            IMAGE_TAG="${config.buildVersion}"  
            echo "Using image tag from Jenkins for new Function App: \$IMAGE_TAG"
            
            # Create Function App with container support
            az functionapp create \\
                --name ${functionAppName} \\
                --resource-group ${resourceGroup} \\
                --plan ${functionAppName}-plan \\
                --storage-account jobschedulerteststorage \\
                --runtime dotnet-isolated \\
                --runtime-version 8 \\
                --functions-version 4 \\
                --image ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:\$IMAGE_TAG \\
                --registry-server https://${env.DOCKER_REGISTRY_NAME}.azurecr.io \\
                --assign-identity
        fi

        # Ensure Function App has managed identity and Key Vault access (regardless of whether app was created or updated)
        # Get the Function App's managed identity principal ID
        echo "Getting Function App managed identity..."
        FUNCTION_APP_IDENTITY=\$(az functionapp identity show --name ${functionAppName} --resource-group ${resourceGroup} --query principalId -o tsv)
        
        # Check if identity was retrieved successfully
        if [ -z "\$FUNCTION_APP_IDENTITY" ] || [ "\$FUNCTION_APP_IDENTITY" = "None" ]; then
            echo "No managed identity found, assigning one..."
            az functionapp identity assign --name ${functionAppName} --resource-group ${resourceGroup}
            sleep 10  # Wait for identity assignment to complete
            FUNCTION_APP_IDENTITY=\$(az functionapp identity show --name ${functionAppName} --resource-group ${resourceGroup} --query principalId -o tsv)
        fi
        
        echo "Function App Managed Identity: \$FUNCTION_APP_IDENTITY"

        # Grant access policy to Function App's managed identity for runtime operations
        echo "Setting access policy for Function App managed identity..."
        if [ -n "\$FUNCTION_APP_IDENTITY" ] && [ "\$FUNCTION_APP_IDENTITY" != "None" ]; then
            az keyvault set-policy \\
                --name ${keyVaultName} \\
                --resource-group ${resourceGroup} \\
                --object-id \$FUNCTION_APP_IDENTITY \\
                --secret-permissions get list || echo "Access policy might already be set, continuing..."
        else
            echo "ERROR: Could not get Function App managed identity. Cannot set Key Vault access policy."
            exit 1
        fi

        # Wait for access policies to propagate
        echo "Waiting for access policies to propagate..."
        sleep 15

        # Store secrets in Key Vault (will update if they already exist)
        echo "Setting datadog-api-key secret in Key Vault..."
        az keyvault secret set --vault-name ${keyVaultName} --name "datadog-api-key" --value "${DD_API_KEY}" || {
            echo "Failed to set datadog-api-key, retrying in 30 seconds..."
            sleep 30
            az keyvault secret set --vault-name ${keyVaultName} --name "datadog-api-key" --value "${DD_API_KEY}"
        }
        
        # Get storage account connection string and store in Key Vault
        echo "Setting azure-webjobs-storage secret in Key Vault..."
        STORAGE_CONN_STRING=\$(az storage account show-connection-string --name jobschedulerteststorage --resource-group ${resourceGroup} --query connectionString -o tsv)
        az keyvault secret set --vault-name ${keyVaultName} --name "azure-webjobs-storage" --value "\$STORAGE_CONN_STRING" || {
            echo "Failed to set azure-webjobs-storage, retrying in 30 seconds..."
            sleep 30
            az keyvault secret set --vault-name ${keyVaultName} --name "azure-webjobs-storage" --value "\$STORAGE_CONN_STRING"
        }
        
        # Get or create Application Insights and store connection string in Key Vault
        echo "Setting up Application Insights..."
        APP_INSIGHTS_NAME="jobscheduler-poc-insights"
        
        # Check if App Insights exists, create if not
        if ! az monitor app-insights component show --app \$APP_INSIGHTS_NAME --resource-group ${resourceGroup} > /dev/null 2>&1; then
            echo "Creating Application Insights instance..."
            az monitor app-insights component create \\
                --app \$APP_INSIGHTS_NAME \\
                --resource-group ${resourceGroup} \\
                --location centralus \\
                --kind web \\
                --application-type web || echo "App Insights might already exist, continuing..."
        fi
        
        # Get App Insights connection string and store in Key Vault
        echo "Setting app-insights-connection secret in Key Vault..."
        APP_INSIGHTS_CONN=\$(az monitor app-insights component show --app \$APP_INSIGHTS_NAME --resource-group ${resourceGroup} --query connectionString -o tsv)
        if [ -n "\$APP_INSIGHTS_CONN" ] && [ "\$APP_INSIGHTS_CONN" != "null" ]; then
            az keyvault secret set --vault-name ${keyVaultName} --name "app-insights-connection" --value "\$APP_INSIGHTS_CONN" || {
                echo "Failed to set app-insights-connection, retrying in 30 seconds..."
                sleep 30
                az keyvault secret set --vault-name ${keyVaultName} --name "app-insights-connection" --value "\$APP_INSIGHTS_CONN"
            }
        else
            echo "WARNING: Could not retrieve Application Insights connection string"
        fi
        
        # Get Docker Registry password and store in Key Vault
        echo "Setting docker-registry-password secret in Key Vault..."
        DOCKER_REGISTRY_PASSWORD=\$(az acr credential show --name ${env.DOCKER_REGISTRY_NAME} --query "passwords[0].value" -o tsv)
        if [ -n "\$DOCKER_REGISTRY_PASSWORD" ] && [ "\$DOCKER_REGISTRY_PASSWORD" != "null" ]; then
            az keyvault secret set --vault-name ${keyVaultName} --name "docker-registry-password" --value "\$DOCKER_REGISTRY_PASSWORD" || {
                echo "Failed to set docker-registry-password, retrying in 30 seconds..."
                sleep 30
                az keyvault secret set --vault-name ${keyVaultName} --name "docker-registry-password" --value "\$DOCKER_REGISTRY_PASSWORD"
            }
        else
            echo "WARNING: Could not retrieve Docker Registry password"
        fi

        # Update app settings with Key Vault references - Clean configuration post-appsettings.json migration
        az functionapp config appsettings set \\
            --name ${functionAppName} \\
            --resource-group ${resourceGroup} \\
            --settings \\
                FUNCTIONS_EXTENSION_VERSION="~4" \\
                FUNCTIONS_WORKER_RUNTIME="dotnet-isolated" \\
                FUNCTION_APP_EDIT_MODE="readwrite" \\
                WEBSITES_ENABLE_APP_SERVICE_STORAGE=false \\
                AzureWebJobsStorage="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=azure-webjobs-storage)" \\
                APPLICATIONINSIGHTS_CONNECTION_STRING="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=app-insights-connection)" \\
                JobScheduler__Logging__DatadogApiKey="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=datadog-api-key)" \\
                ASPNETCORE_ENVIRONMENT="dev" \\
                DOTNET_ENVIRONMENT="dev" \\
                DOCKER_REGISTRY_SERVER_URL="https://${env.DOCKER_REGISTRY_NAME}.azurecr.io" \\
                DOCKER_REGISTRY_SERVER_USERNAME="${env.DOCKER_REGISTRY_NAME}" \\
                DOCKER_REGISTRY_SERVER_PASSWORD="@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=docker-registry-password)" \\
                JENKINS_BUILD_NUMBER="${config.buildNumber}" \\
                LAST_JENKINS_DEPLOY="\$(date)"

        # Restart the function app to pick up new container
        az functionapp restart --name ${functionAppName} --resource-group ${resourceGroup}
        
        # Wait for restart to complete
        echo "Waiting for Function App to restart..."
        sleep 30

        # Validate Key Vault references before sync
        echo "Validating Key Vault references..."
        az keyvault secret show --vault-name ${keyVaultName} --name "azure-webjobs-storage" --query "value" -o tsv > /dev/null || echo "WARNING: azure-webjobs-storage secret not found in Key Vault"
        az keyvault secret show --vault-name ${keyVaultName} --name "app-insights-connection" --query "value" -o tsv > /dev/null || echo "WARNING: app-insights-connection secret not found in Key Vault"
        az keyvault secret show --vault-name ${keyVaultName} --name "datadog-api-key" --query "value" -o tsv > /dev/null || echo "WARNING: datadog-api-key secret not found in Key Vault"
        az keyvault secret show --vault-name ${keyVaultName} --name "docker-registry-password" --query "value" -o tsv > /dev/null || echo "WARNING: docker-registry-password secret not found in Key Vault"
        
        # Additional container diagnostics
        echo "=== Container Configuration Diagnostics ==="
        echo "Checking Function App container configuration..."
        az functionapp config container show --name ${functionAppName} --resource-group ${resourceGroup} || echo "Could not retrieve container config"
        
        echo "Checking Function App runtime status..."
        az functionapp show --name ${functionAppName} --resource-group ${resourceGroup} --query "{name:name,state:state,kind:kind,repositorySiteName:repositorySiteName}" -o table

        # Check Function App status before triggering sync
        echo "Checking Function App status..."
        FUNCTION_STATUS=\$(az functionapp show --name ${functionAppName} --resource-group ${resourceGroup} --query state -o tsv)
        echo "Function App State: \$FUNCTION_STATUS"
        
        if [ "\$FUNCTION_STATUS" = "Running" ]; then
            # Force sync function triggers to refresh function metadata and clear portal cache
            echo "Syncing function triggers to refresh Azure Portal function metadata..."
            SUBSCRIPTION_ID=\$(az account show --query id -o tsv)
            az rest --method post --url "https://management.azure.com/subscriptions/\$SUBSCRIPTION_ID/resourceGroups/${resourceGroup}/providers/Microsoft.Web/sites/${functionAppName}/syncfunctiontriggers?api-version=2016-08-01" || {
                echo "WARNING: Function trigger sync failed - this may indicate runtime issues"
                echo "Checking Function App logs for errors..."
                az functionapp log tail --name ${functionAppName} --resource-group ${resourceGroup} --provider filesystem || echo "Could not retrieve logs"
            }
        else
            echo "WARNING: Function App is not in Running state, skipping trigger sync"
        fi
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

        # Configure Function App settings - Clean configuration post-appsettings.json migration
        az functionapp config appsettings set \\
            --name ${functionAppName} \\
            --resource-group ${resourceGroup} \\
            --settings \\
                FUNCTIONS_EXTENSION_VERSION="~4" \\
                FUNCTIONS_WORKER_RUNTIME="dotnet-isolated" \\
                FUNCTION_APP_EDIT_MODE="readwrite" \\
                WEBSITES_ENABLE_APP_SERVICE_STORAGE=false \\
                AzureWebJobsStorage="${AZURE_STORAGE_CONNECTION_STRING}" \\
                DOCKER_REGISTRY_SERVER_URL="https://${env.DOCKER_REGISTRY_NAME}.azurecr.io"

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
                FUNCTIONS_EXTENSION_VERSION=~4 \\
                FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \\
                FUNCTION_APP_EDIT_MODE=readwrite \\
                WEBSITES_PORT=${applicationPort} \\
                AzureWebJobsScriptRoot=/home/site/wwwroot \\
                AzureFunctionsJobHost__Logging__Console__IsEnabled=true \\
                AzureWebJobsStorage=secretref:azure-storage-connection-string \\
                DD_API_KEY=secretref:dd-api-key \\
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
