# Multi-stage build for optimized production image

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["src/JobScheduler.FunctionApp/JobScheduler.FunctionApp.csproj", "src/JobScheduler.FunctionApp/"]

# Restore dependencies
RUN dotnet restore "src/JobScheduler.FunctionApp/JobScheduler.FunctionApp.csproj"

# Copy source code
COPY src/ src/

# Build and publish
WORKDIR "/src/src/JobScheduler.FunctionApp"
RUN dotnet build "JobScheduler.FunctionApp.csproj" -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish "JobScheduler.FunctionApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS final

# Set the working directory
WORKDIR /home/site/wwwroot

# Copy the published application from build stage
COPY --from=build /app/publish .

# Set environment variables for Azure Functions
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

# Expose port 80 (default for Azure Functions in containers)
EXPOSE 80
