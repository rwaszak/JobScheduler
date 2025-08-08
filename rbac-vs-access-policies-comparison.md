# Key Vault: Access Policies vs RBAC Permission Comparison

## Access Policies Permissions (What you have now)

### Secret Permissions
- `get` - Read secret values
- `list` - List secret names  
- `set` - Create/update secrets
- `delete` - Delete secrets
- `backup` - Backup secrets
- `restore` - Restore secrets
- `recover` - Recover deleted secrets
- `purge` - Permanently delete
- `all` - All of the above

### Key Permissions  
- Similar structure: get, list, create, update, import, delete, backup, restore, recover, purge, decrypt, encrypt, unwrapKey, wrapKey, verify, sign

### Certificate Permissions
- get, list, create, update, deleteissuers, getissuers, listissuers, setissuers, managecontacts, manageissuers

---

## RBAC Built-in Roles (What we'd move to)

### Key Vault Administrator
**What it does**: Full access to Key Vault and all objects
**Permissions**: Everything (like access policy "all")
**Use case**: You (for management)

### Key Vault Secrets Officer  
**What it does**: Manage secrets (CRUD operations)
**Specific actions**:
- Microsoft.KeyVault/vaults/secrets/read
- Microsoft.KeyVault/vaults/secrets/write  
- Microsoft.KeyVault/vaults/secrets/delete
- Microsoft.KeyVault/vaults/secrets/backup/action
- Microsoft.KeyVault/vaults/secrets/restore/action
- Microsoft.KeyVault/vaults/secrets/readMetadata/action
**Use case**: Jenkins (for deployment)

### Key Vault Secrets User
**What it does**: Read secret values only
**Specific actions**:
- Microsoft.KeyVault/vaults/secrets/getSecret/action
- Microsoft.KeyVault/vaults/secrets/readMetadata/action  
**Use case**: Function App (for runtime)

### Key Vault Crypto Officer
**What it does**: Cryptographic operations on keys
**Use case**: Applications doing encryption/decryption

### Key Vault Crypto User  
**What it does**: Use keys for crypto operations (no management)
**Use case**: Applications using keys but not managing them

### Key Vault Certificate Officer/User
**What it does**: Manage/use certificates
**Use case**: Certificate management scenarios

---

## Granularity Examples

### Access Policies (Current)
```bash
# All or nothing per permission type
az keyvault set-policy \
  --name myvault \
  --upn user@domain.com \
  --secret-permissions get list set delete  # Applied to ALL secrets
```

### RBAC (Proposed)
```bash
# Can be applied at different scopes
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee user@domain.com \
  --scope /subscriptions/xxx/resourceGroups/xxx/providers/Microsoft.KeyVault/vaults/myvault/secrets/specific-secret
  # ↑ This would only give access to ONE specific secret!

# Or vault level (like access policies)
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee user@domain.com \
  --scope /subscriptions/xxx/resourceGroups/xxx/providers/Microsoft.KeyVault/vaults/myvault
  # ↑ This gives access to all secrets in the vault
```

---

## Management Experience

### Access Policies
- ✅ Simple UI in Azure Portal (Key Vault → Access policies)
- ✅ Easy to see who has what access in one place
- ❌ Vault-specific management only
- ❌ No inheritance or advanced scenarios

### RBAC  
- ✅ Unified with all Azure resources (same permission model everywhere)
- ✅ Advanced scenarios (conditional access, time-based access, etc.)
- ✅ Better audit trails (all in Azure Activity Log)
- ✅ Can use Azure AD groups for easier management
- ❌ More complex to understand initially
- ❌ Permissions scattered across IAM blades

---

## Audit and Compliance

### Access Policies
```json
// Limited audit information
{
  "operationName": "VaultGet",
  "resourceId": "/subscriptions/.../vaults/myvault",
  "caller": "user@domain.com"
}
```

### RBAC
```json
// Rich audit information  
{
  "operationName": "Microsoft.KeyVault/vaults/secrets/getSecret/action",
  "resourceId": "/subscriptions/.../vaults/myvault/secrets/my-secret", 
  "caller": "user@domain.com",
  "roleAssignmentId": "/subscriptions/.../roleAssignments/xxx",
  "roleDefinitionId": "/subscriptions/.../roleDefinitions/Key Vault Secrets User"
}
```

---

## Migration Impact Analysis

### What Stays The Same ✅
- Azure Functions Key Vault references work identically
- Your application code doesn't change
- Secret values and vault configuration unchanged
- Performance is identical

### What Changes ⚠️
- Permission assignment method (role assignments vs access policies)  
- Jenkins deployment script (remove access policy commands)
- Azure Portal management experience
- Audit log format and detail level

### What Could Break 🚨
- If migration is done incorrectly, Function App could lose access
- Jenkins pipeline could fail if it doesn't have role assignment permissions
- Temporary access issues during transition period
