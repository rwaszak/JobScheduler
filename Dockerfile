# Use the official Azure Functions .NET 8 base image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

# Set the working directory
WORKDIR /home/site/wwwroot

# Copy the published application
COPY src/JobScheduler.FunctionApp/bin/Release/net8.0/publish/ .

# Set environment variables for Azure Functions
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

# Expose port 80 (default for Azure Functions in containers)
EXPOSE 80
