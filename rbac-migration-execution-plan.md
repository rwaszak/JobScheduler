# RBAC Migration - DEFERRED UNTIL TERRAFORM PHASE

## ✅ Current State: WORKING PERFECTLY with Access Policies

**Decision**: Focus on Function App development with current working setup.
**Rationale**: 
- Key Vault integration working flawlessly
- Application Insights and Datadog logging both working
- You have full secret management access in Azure Portal
- Zero risk approach during bootstrap phase

## When to Revisit RBAC

**Implement RBAC later with Terraform** for clean infrastructure-as-code:

```hcl
# terraform/keyvault.tf - Future implementation
resource "azurerm_key_vault" "main" {
  enable_rbac_authorization = true  # Start with RBAC from the beginning
  # ... other config
}
```

---

# ORIGINAL MIGRATION PLAN (For Future Reference)

## Phase 1: Enable RBAC (Non-breaking - keeps access policies)

### Step 1.1: Enable RBAC alongside Access Policies
```bash
az keyvault update \
  --name jobscheduler-poc-kv \
  --resource-group continuum_scheduled_jobs \
  --enable-rbac-authorization true
```
**Result**: Both access policies AND RBAC work simultaneously. Zero downtime.

---

## Phase 2: Assign RBAC Roles

### Step 2.1: Give yourself Key Vault Administrator
```bash
az role assignment create \
  --role "Key Vault Administrator" \
  --assignee rwaszak@gocontinuum.ai \
  --scope /subscriptions/dd5374ff-ccf8-450c-ab5f-c71adba0f1c5/resourceGroups/continuum_scheduled_jobs/providers/Microsoft.KeyVault/vaults/jobscheduler-poc-kv
```
**Purpose**: Full management access for you

### Step 2.2: Give Jenkins Service Principal appropriate role  
**Current phase** (bootstrap/development):
```bash
az role assignment create \
  --role "Key Vault Administrator" \
  --assignee fa90ed15-231d-4a57-b5f8-ac0d42d86973 \
  --scope /subscriptions/dd5374ff-ccf8-450c-ab5f-c71adba0f1c5/resourceGroups/continuum_scheduled_jobs/providers/Microsoft.KeyVault/vaults/jobscheduler-poc-kv
```

**Future phase** (production/terraform managed):
```bash
# You'll run this later when ready to lock down Jenkins
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee fa90ed15-231d-4a57-b5f8-ac0d42d86973 \
  --scope /subscriptions/dd5374ff-ccf8-450c-ab5f-c71adba0f1c5/resourceGroups/continuum_scheduled_jobs/providers/Microsoft.KeyVault/vaults/jobscheduler-poc-kv
```

### Step 2.3: Give Function App minimal access
```bash
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee 7dafcf56-2d1a-4fe6-a521-ee17f2768275 \
  --scope /subscriptions/dd5374ff-ccf8-450c-ab5f-c71adba0f1c5/resourceGroups/continuum_scheduled_jobs/providers/Microsoft.KeyVault/vaults/jobscheduler-poc-kv
```
**Purpose**: Runtime secret access only

---

## Phase 3: Test Everything

### Step 3.1: Test Function App
```bash
# Should still work - logs in both App Insights and Datadog
curl https://job-scheduler-poc-container.azurewebsites.net/api/health
```

### Step 3.2: Test Jenkins Deployment
```bash
# Run your Jenkins pipeline - should still deploy successfully
# Jenkins still has admin access during bootstrap phase
```

### Step 3.3: Test Your Access
```bash
# You should be able to manage secrets in Azure Portal
# Try viewing/editing secrets in the portal
```

### Step 3.4: Run Validation Script
```bash
powershell .\validate-keyvault-setup.ps1
```

---

## Phase 4: Update deploy.groovy (Future when ready to lock down)

**When you're ready to move to Terraform management**, update deploy.groovy to remove access policy management:

```groovy
# REMOVE these lines (when moving to Terraform):
# az keyvault set-policy --name ${keyVaultName} --spn $AZURE_CLIENT_ID --secret-permissions get set list delete
# az keyvault set-policy --name ${keyVaultName} --object-id $FUNCTION_APP_IDENTITY --secret-permissions get list

# KEEP these lines (still needed for deployment):
# - Secret creation/updates
# - Function App restart
# - Validation checks
```

---

## Phase 5: Lock Down Jenkins (Future)

**When you switch to Terraform management:**

### Step 5.1: Remove Jenkins admin access
```bash
# Remove the administrator role
az role assignment delete \
  --role "Key Vault Administrator" \
  --assignee fa90ed15-231d-4a57-b5f8-ac0d42d86973 \
  --scope /subscriptions/.../vaults/jobscheduler-poc-kv

# Add minimal runtime access only  
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee fa90ed15-231d-4a57-b5f8-ac0d42d86973 \
  --scope /subscriptions/.../vaults/jobscheduler-poc-kv
```

### Step 5.2: Terraform manages secrets
```hcl
# terraform/keyvault.tf
resource "azurerm_key_vault_secret" "datadog_api_key" {
  name         = "datadog-api-key"
  value        = var.datadog_api_key
  key_vault_id = azurerm_key_vault.main.id
}

resource "azurerm_key_vault_secret" "app_insights_conn" {
  name         = "applicationinsights-connection-string" 
  value        = azurerm_application_insights.main.connection_string
  key_vault_id = azurerm_key_vault.main.id
}
```

---

## Rollback Plan (If Needed)

If anything breaks during migration:

### Disable RBAC (back to access policies only)
```bash
az keyvault update \
  --name jobscheduler-poc-kv \
  --resource-group continuum_scheduled_jobs \
  --enable-rbac-authorization false
```

### Or keep both systems active
```bash
# Keep RBAC enabled but ensure access policies are still working
# Both systems can coexist safely
```

---

## Current vs Future State Summary

| Component | Current (Bootstrap) | Future (Production) |
|-----------|-------------------|-------------------|
| **You** | Key Vault Administrator (RBAC) | Key Vault Administrator (RBAC) |
| **Jenkins** | Key Vault Administrator (RBAC) | Key Vault Secrets User (RBAC) |  
| **Function App** | Key Vault Secrets User (RBAC) | Key Vault Secrets User (RBAC) |
| **Secret Management** | Jenkins deploy.groovy | Terraform |
| **Permission Model** | RBAC | RBAC |

Ready to start? Let's execute Phase 1!
