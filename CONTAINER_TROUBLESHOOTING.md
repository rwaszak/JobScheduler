# Container Startup Troubleshooting Guide

## 🚨 Current Issue: Persistent 503 Errors

The Function App is consistently returning 503 Service Unavailable errors even after multiple fixes:

### ✅ **Fixes Applied:**
- ✅ Added Application Insights connection string to Key Vault
- ✅ Added Docker Registry password to Key Vault  
- ✅ Enhanced diagnostics and validation
- ✅ Increased startup wait times and retry logic

### 🔍 **Next Diagnostic Steps:**

1. **Check Container App Status Stage** - Look for these in Jenkins output:
   ```bash
   === Function App Status ===
   === Function App Logs (last 50 lines) ===
   === Container Status ===
   ```

2. **Common 503 Root Causes:**
   - **Container Pull Failures**: Invalid registry credentials
   - **Application Startup Exceptions**: Missing dependencies or config errors
   - **Memory/Resource Issues**: Insufficient resources for startup
   - **Key Vault Access**: Function App can't retrieve secrets

### 🧪 **Local Docker Test Command:**
```bash
# Test the Docker image locally to validate it works
docker run -d -p 8080:80 --name test-jobscheduler \
  -e AzureWebJobsStorage="UseDevelopmentStorage=true" \
  -e FUNCTIONS_WORKER_RUNTIME="dotnet-isolated" \
  -e Environment="Development" \
  continuumaicontainers.azurecr.io/jobscheduler-functions:test-build-1

# Test health endpoint locally
curl http://localhost:8080/api/health

# Check container logs
docker logs test-jobscheduler

# Cleanup
docker stop test-jobscheduler && docker rm test-jobscheduler
```

### 🔧 **Enhanced Deploy Script Features Added:**
1. **Docker Registry Password**: Automatically stored in Key Vault
2. **Container Configuration Validation**: Shows current container settings
3. **Runtime Status Checks**: Validates Function App state before operations
4. **Enhanced Logging**: Better error messages and diagnostics

### 📋 **What to Look For in Next Jenkins Run:**
1. **"Setting docker-registry-password secret in Key Vault..."** - Should succeed
2. **Container Configuration Diagnostics** - Should show current image
3. **Function App runtime status** - Should show "Running" state
4. **Key Vault validation** - All 4 secrets should be found

If the issue persists after this deployment, we'll need to examine the actual container logs from the "Check Function App Status" stage to see the specific startup error.
