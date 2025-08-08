# Key Vault RBAC Migration Plan

## Current State
- Using Access Policies model
- Jenkins service principal: `fa90ed15-231d-4a57-b5f8-ac0d42d86973` (full access)
- Function App identity: `7dafcf56-2d1a-4fe6-a521-ee17f2768275` (get, list secrets)
- Your user: `4f47b843-1ebf-4cc9-b44c-f1b6835de302` (get, list, set, delete secrets)

## Benefits of RBAC Migration
✅ Modern, granular permission model
✅ Better auditing and governance  
✅ Unlimited role assignments
✅ You can manage secrets in Azure Portal
✅ Follows Azure security best practices
✅ Easier to understand permissions

## RBAC Roles We'll Use
- **Key Vault Administrator** (for you) - Full management
- **Key Vault Secrets Officer** (for Jenkins) - Deploy/manage secrets
- **Key Vault Secrets User** (for Function App) - Read secrets only

## Migration Steps

### Phase 1: Enable RBAC (Non-breaking)
```bash
# Enable RBAC while keeping access policies (both work together)
az keyvault update --name jobscheduler-poc-kv --resource-group continuum_scheduled_jobs --enable-rbac-authorization true
```

### Phase 2: Assign RBAC Roles
```bash
# Give yourself admin access
az role assignment create \
  --role "Key Vault Administrator" \
  --assignee rwaszak@gocontinuum.ai \
  --scope /subscriptions/dd5374ff-ccf8-450c-ab5f-c71adba0f1c5/resourceGroups/continuum_scheduled_jobs/providers/Microsoft.KeyVault/vaults/jobscheduler-poc-kv

# Give Jenkins service principal secrets officer access
az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee fa90ed15-231d-4a57-b5f8-ac0d42d86973 \
  --scope /subscriptions/dd5374ff-ccf8-450c-ab5f-c71adba0f1c5/resourceGroups/continuum_scheduled_jobs/providers/Microsoft.KeyVault/vaults/jobscheduler-poc-kv

# Give Function App identity secrets user access
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee 7dafcf56-2d1a-4fe6-a521-ee17f2768275 \
  --scope /subscriptions/dd5374ff-ccf8-450c-ab5f-c71adba0f1c5/resourceGroups/continuum_scheduled_jobs/providers/Microsoft.KeyVault/vaults/jobscheduler-poc-kv
```

### Phase 3: Test Everything
```bash
# Test Function App still works
curl https://job-scheduler-poc-container.azurewebsites.net/api/health

# Test Jenkins deployment still works
# Run your Jenkins pipeline

# Test you can manage secrets in Azure Portal
```

### Phase 4: Update deploy.groovy (Remove access policy management)
Remove the access policy commands since RBAC handles permissions:
```groovy
# Remove these lines from deploy.groovy:
# az keyvault set-policy --name ${keyVaultName} --spn $AZURE_CLIENT_ID --secret-permissions get set list delete
# az keyvault set-policy --name ${keyVaultName} --object-id $FUNCTION_APP_IDENTITY --secret-permissions get list
```

### Phase 5: Clean Up (Optional)
After confirming everything works, remove access policies:
```bash
az keyvault update --name jobscheduler-poc-kv --resource-group continuum_scheduled_jobs --enable-rbac-authorization true --bypass AzureServices
```

## Rollback Plan
If anything breaks, disable RBAC and revert to access policies:
```bash
az keyvault update --name jobscheduler-poc-kv --resource-group continuum_scheduled_jobs --enable-rbac-authorization false
```

## Testing Checklist
- [ ] You can view/manage secrets in Azure Portal
- [ ] Function App logs still show in Application Insights
- [ ] Function App logs still show in Datadog  
- [ ] Jenkins pipeline can deploy and update secrets
- [ ] All Key Vault references work: `AzureWebJobsStorage`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `JobScheduler__Logging__DatadogApiKey`
