# JobScheduler Function App

A secure, scalable Azure Functions application for executing scheduled HTTP-based jobs with comprehensive monitoring and logging.

## 🚀 Features

- **🔐 Secure Secret Management**: Azure Key Vault integration for API keys and sensitive data
- **📊 Comprehensive Monitoring**: Datadog integration with custom metrics and logging
- **⚡ Configurable Jobs**: Easy job definition through configuration
- **🔄 Retry Logic**: Built-in retry policies for failed requests
- **🎯 Multiple Authentication**: Support for API keys, bearer tokens, and custom headers
- **📈 Health Checks**: Built-in health monitoring endpoints
- **🐳 Containerized**: Docker support for consistent deployments

## 📋 Prerequisites

- Azure Subscription
- Azure Key Vault (automatically created during deployment)
- Datadog account and API key
- Docker (for local development)
- .NET 8.0 SDK

## 🏗️ Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Azure Timer   │    │  Job Executor   │    │  Target APIs    │
│   Functions     │───▶│                 │───▶│                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Azure Key      │    │  Job Logger     │    │  Datadog        │
│  Vault          │    │  & Metrics      │    │  Monitoring     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## ⚙️ Configuration

### Environment Variables

| Variable | Description | Required | Example |
|----------|-------------|----------|---------|
| `KeyVault__VaultUrl` | Azure Key Vault URL | Yes | `https://your-vault.vault.azure.net/` |
| `DD_ENV` | Datadog environment tag | Yes | `dev`, `staging`, `prod` |
| `DD_SERVICE` | Datadog service name | Yes | `job-scheduler-functions` |
| `DD_VERSION` | Application version | Yes | `1.0.0` |
| `DD_SITE` | Datadog site (org standard) | Yes | `us3.datadoghq.com` |

### Key Vault Secrets

| Secret Name | Description | Example |
|-------------|-------------|---------|
| `datadog-api-key` | Datadog API key for logging | `abc123...` |
| `azure-webjobs-storage` | Azure Storage connection string | `DefaultEndpoints...` |
| `job-api-key-{jobname}` | API keys for specific jobs | `Bearer xyz...` |

### Job Configuration (appsettings.json)

```json
{
  "JobScheduler": {
    "Jobs": [
      {
        "JobName": "HealthCheckJob",
        "Endpoint": "https://api.example.com/health",
        "HttpMethod": "GET",
        "Schedule": "0 */5 * * * *",
        "AuthenticationType": "ApiKey",
        "AuthSecretName": "health-check-api-key",
        "Timeout": "00:00:30",
        "RetryPolicy": {
          "MaxRetries": 3,
          "BaseDelay": "00:00:02",
          "MaxDelay": "00:00:10"
        }
      }
    ],
    "Logging": {
      "DatadogApiKey": "your-datadog-api-key",
      "DatadogSite": "us3.datadoghq.com"
    }
  }
}
```

## 🚀 Getting Started

### 1. Clone and Setup

```bash
git clone <repository-url>
cd JobScheduler.FunctionApp
dotnet restore
```

### 2. Local Development

```bash
# Set up local environment variables
export KeyVault__VaultUrl="https://your-vault.vault.azure.net/"
export DD_ENV="local"
export DD_SERVICE="jobscheduler-functions"
export DD_VERSION="dev"

# Run locally
dotnet run --project src/JobScheduler.FunctionApp
```

### 3. Docker Development

```bash
# Build and run with Docker
docker build -t jobscheduler-functions .
docker run -p 7071:80 \
  -e KeyVault__VaultUrl="https://your-vault.vault.azure.net/" \
  -e DD_ENV="local" \
  jobscheduler-functions
```

## 📝 Adding a New Job

### Step 1: Define Job Configuration

Add your job to `appsettings.json`:

```json
{
  "JobScheduler": {
    "Jobs": [
      {
        "JobName": "YourNewJob",
        "Endpoint": "https://your-api.com/endpoint",
        "HttpMethod": "POST",
        "Schedule": "0 0 9 * * MON-FRI",
        "AuthenticationType": "BearerToken",
        "AuthSecretName": "your-api-token",
        "RequestBody": "{\"action\":\"process\"}",
        "Timeout": "00:01:00",
        "RetryPolicy": {
          "MaxRetries": 5,
          "BaseDelay": "00:00:05",
          "MaxDelay": "00:01:00"
        }
      }
    ]
  }
}
```

### Step 2: Store Secrets in Key Vault

```bash
# Add your API token to Key Vault
az keyvault secret set \
  --vault-name "your-vault-name" \
  --name "your-api-token" \
  --value "your-actual-token"
```

### Step 3: Deploy and Monitor

Deploy your changes and monitor in:
- **Azure Portal**: Function App logs and metrics
- **Datadog**: Custom dashboards and alerts
- **Application Insights**: Performance and error tracking

## 🔧 Configuration Reference

### Authentication Types

| Type | Description | Secret Format |
|------|-------------|---------------|
| `None` | No authentication | N/A |
| `ApiKey` | API key in header | `x-api-key: your-key` |
| `BearerToken` | Bearer token | `Bearer your-token` |
| `Custom` | Custom header | `Custom-Header: value` |

### Schedule Format (Cron)

```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
* * * * *
```

**Examples:**
- `"0 */5 * * * *"` - Every 5 minutes
- `"0 0 9 * * MON-FRI"` - Daily at 9 AM, weekdays only
- `"0 0 0 1 * *"` - First day of every month at midnight

## 📊 Monitoring and Alerting

### Datadog Dashboards

Monitor your jobs with these key metrics:
- **Job Success Rate**: Percentage of successful executions
- **Job Duration**: How long jobs take to complete
- **Error Rate**: Failed job executions
- **API Response Times**: Performance of target APIs

### Setting Up Alerts

1. **Job Failure Alert**:
   ```
   avg(last_5m):avg:jobscheduler.job.failed{*} > 0
   ```

2. **High Response Time Alert**:
   ```
   avg(last_15m):avg:jobscheduler.job.duration{*} > 30000
   ```

### Application Insights

Track additional metrics in Azure:
- Function execution times
- Memory usage
- Dependency failures
- Custom telemetry

## 🧪 Testing

### Run All Tests

```bash
# Run unit and integration tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

- **Unit Tests**: Individual component testing
- **Integration Tests**: End-to-end workflow testing
- **Configuration Tests**: Validation and binding tests

## 🚀 Deployment

### Manual Deployment

```bash
# Build and deploy to Azure
docker build -t your-registry.azurecr.io/jobscheduler:latest .
docker push your-registry.azurecr.io/jobscheduler:latest

# Update Function App
az functionapp config container set \
  --name your-function-app \
  --resource-group your-rg \
  --image your-registry.azurecr.io/jobscheduler:latest
```

### CI/CD Pipeline

The application includes Jenkins pipeline configuration for automated deployment.

## 🔍 Troubleshooting

### Common Issues

1. **Key Vault Access Denied**
   - Verify managed identity is enabled
   - Check Key Vault access policies
   - Ensure correct Key Vault URL

2. **Job Not Executing**
   - Verify cron expression format
   - Check job configuration syntax
   - Review Function App logs

3. **Datadog Logs Missing**
   - Verify API key in Key Vault
   - Check Datadog site configuration
   - Review network connectivity

### Debug Logs

Enable detailed logging by setting:
```json
{
  "Logging": {
    "LogLevel": {
      "JobScheduler": "Debug"
    }
  }
}
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## 📞 Support

For questions or issues:
- Check existing GitHub issues
- Review Azure Function logs
- Monitor Datadog dashboards
- Contact the development team

---

## 🏷️ Version History

- **v1.0.0**: Initial release with basic job scheduling
- **v1.1.0**: Added Key Vault integration and Datadog monitoring
- **v1.2.0**: Enhanced retry policies and error handling

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
