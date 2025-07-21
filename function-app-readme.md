# HelloWorld Function App Scheduler

A .NET 8 Azure Function App that demonstrates scheduled job execution by calling Container App endpoints on a timer.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- Visual Studio 2022 or VS Code with Azure Functions extension

### Installing Azure Functions Core Tools

```bash
npm install -g azure-functions-core-tools@4 --unsafe-perm true
func --version  # Verify installation
```

## Configuration

### local.settings.json

Create `local.settings.json` in the project root (never commit this file):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "INTEGRATION_LAYER_DEV_HEALTH_ENDPOINT": "https://your-container-app.azurecontainerapps.io/health",
    "ENVIRONMENT": "dev",
    "DATADOG_API_KEY": "your-datadog-api-key-here",
    "DD_SITE": "us3.datadoghq.com",
    "DD_ENV": "dev",
    "DD_SERVICE": "function-scheduler-poc"
  }
}
```

## Running the Application

### Option 1: Azure Functions Core Tools (Recommended)

Best for testing timer schedules and production-like behavior:

```bash
func start
```

- ✅ Timer executes automatically on schedule
- ✅ Production-like Azure Functions runtime
- ✅ Real-time logs
- ❌ No breakpoints/debugging

### Option 2: Visual Studio Debugging

Best for debugging code and understanding data flow:

1. Open project in Visual Studio
2. Set breakpoints as needed
3. Press F5 to start debugging
4. Manually trigger function (timer won't auto-execute)

- ✅ Full debugging with breakpoints
- ✅ Variable inspection and call stack
- ❌ Timer doesn't execute automatically
- ❌ Must manually trigger functions

### Manual Function Triggering

When using Visual Studio or need to test immediately:

```bash
curl -X POST http://localhost:7071/admin/functions/HelloWorldScheduler
```

## Timer Schedule

Configure in `HelloWorldScheduler.cs`:

```csharp
[TimerTrigger("*/10 * * * * *")]  // Every 10 seconds (testing)
[TimerTrigger("0 */5 * * * *")]   // Every 5 minutes  
[TimerTrigger("0 6 * * *")]       // Daily at 6 AM
```

## Deployment

Deploy to Azure Function App:

```bash
func azure functionapp publish your-function-app-name
```

Remember to configure the same environment variables in Azure Portal under Function App → Configuration → Application Settings.