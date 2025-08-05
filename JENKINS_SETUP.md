# JobScheduler Functions - Jenkins CI/CD Setup

This document outlines the Jenkins CI/CD pipeline setup for the JobScheduler Azure Functions application.

## 📁 Pipeline Files

- **`jenkins-build.groovy`** - Build pipeline that compiles, tests, and creates Docker images
- **`jenkins-deploy.groovy`** - Deployment pipeline that deploys to Azure environments  
- **`deploy.groovy`** - Deployment logic script (loaded by deploy pipeline)
- **`Dockerfile`** - Multi-stage Docker build configuration
- **`.dockerignore`** - Docker build context optimization

## 🔧 Prerequisites

### Jenkins Plugins Required
- Azure CLI Plugin
- Docker Plugin  
- Azure Service Principal Plugin
- Blue Ocean (recommended)

### Credentials Setup in Jenkins
1. **`github-personal-access-token-dg`** - GitHub PAT for repository access
2. **`jenkins-service-principal-2`** - Azure Service Principal for deployments
3. **`datadog-api-key`** - Datadog API key for monitoring
4. **`azure-storage-connection-string-dev`** - Azure Storage for development
5. **`azure-storage-connection-string-sit`** - Azure Storage for SIT
6. **`azure-storage-connection-string-uat`** - Azure Storage for UAT  
7. **`azure-storage-connection-string-prod`** - Azure Storage for production

### Environment Variables to Configure
Update these in the pipeline files to match your setup:

```groovy
DOCKER_REGISTRY_NAME = 'your-acr-name'        // Your Azure Container Registry
BUILD_PIPELINE_NAME = 'JobScheduler Functions - Build'  // Your build job name
```

## 🚀 Pipeline Setup

### 1. Create Build Pipeline
1. Create new Jenkins Pipeline job: "JobScheduler Functions - Build"  
2. Set pipeline source to "Pipeline script from SCM"
3. Point to `jenkins-build.groovy` in your repository
4. Configure branch parameters and triggers

### 2. Create Deploy Pipeline  
1. Create new Jenkins Pipeline job: "JobScheduler Functions - Deploy"
2. Set pipeline source to "Pipeline script from SCM"  
3. Point to `jenkins-deploy.groovy` in your repository
4. Configure environment parameters

## 🎯 Deployment Options

The deployment script supports two deployment methods:

### Option A: Azure Functions (Recommended)
- Deploys as a containerized Azure Functions app
- Simpler setup and management
- Built-in scaling and triggers
- Set `DEPLOYMENT_METHOD=functions` (default)

### Option B: Azure Container Apps
- Deploys as a container app with more control
- Custom scaling and networking options  
- Set `DEPLOYMENT_METHOD=container-apps`

## 🔄 Workflow

### Build Process
1. **Checkout** - Clone repository from specified branch
2. **Setup .NET** - Install .NET 8 SDK and runtime
3. **Build & Test** - Compile code and run unit tests
4. **Publish** - Create optimized published output
5. **Docker Build** - Create container image with multi-stage build
6. **Push to ACR** - Upload image to Azure Container Registry
7. **Save Artifacts** - Store build metadata for deployment

### Deploy Process  
1. **Get Build Info** - Retrieve artifacts from successful build
2. **Checkout** - Get deployment scripts from repository
3. **Deploy** - Execute deployment based on chosen method
4. **Verify** - Check deployment health and endpoints

## 🧪 Local Testing

Before running in Jenkins, test locally:

```bash
# Test the build
dotnet restore src/JobScheduler.FunctionApp
dotnet build src/JobScheduler.FunctionApp --configuration Release
dotnet publish src/JobScheduler.FunctionApp --configuration Release

# Test Docker build
docker build -t jobscheduler-functions:test .
docker run -p 8080:80 jobscheduler-functions:test

# Test endpoints
curl http://localhost:8080/api/health
curl http://localhost:8080/api/jobs
```

## 📋 Customization Points

### Build Pipeline Customization
- **Test Configuration**: Modify test commands in `jenkins-build.groovy`
- **Build Arguments**: Add Docker build args as needed
- **Notification Settings**: Update Slack/email recipients

### Deploy Pipeline Customization  
- **Resource Names**: Update Azure resource naming conventions
- **Environment Variables**: Add/modify app settings  
- **Scaling**: Adjust CPU/memory/replica settings
- **Health Checks**: Configure custom health check endpoints

## 🔍 Monitoring & Troubleshooting

### Build Issues
- Check .NET SDK installation logs
- Verify test results in Jenkins artifacts
- Review Docker build context size

### Deployment Issues  
- Verify Azure credentials and permissions
- Check resource group and naming conventions
- Review container logs in Azure portal
- Test endpoints after deployment

### Common Problems
1. **"No space left on device"** - Clean up old Docker images
2. **"Authentication failed"** - Refresh Azure service principal
3. **"Function not found"** - Check function discovery and metadata
4. **"Timer trigger failed"** - Verify Azure Storage connection

## 📚 Additional Resources

- [Azure Functions Docker Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-function-linux-custom-image)
- [Jenkins Azure Plugin Documentation](https://plugins.jenkins.io/azure-cli/)
- [Docker Multi-stage Builds](https://docs.docker.com/develop/dev-best-practices/dockerfile_best-practices/)
