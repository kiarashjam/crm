# GitHub Secrets & Variables Setup

Complete guide to connect GitHub Actions to Azure (West Europe, cheapest tiers).

---

## 1. Create Azure Resources (West Europe)

Run the script to create a new resource group with all resources in **West Europe** (cheapest pricing):

```powershell
# From repo root
./scripts/azure-create-west-europe.ps1 -SqlAdminPassword 'YourSecurePwd1!'
```

**With Service Principal** (recommended – creates GitHub-ready credentials):

```powershell
./scripts/azure-create-west-europe.ps1 -SqlAdminPassword 'YourSecurePwd1!' -CreateServicePrincipal
```

**Resources created:**

| Resource       | Tier   | Location   |
|----------------|--------|------------|
| Resource Group | -      | West Europe |
| Static Web App | Free   | West Europe |
| SQL Database   | Basic  | West Europe |
| Web App (API)  | F1 Free| West Europe |

**Prerequisites:** Azure CLI installed, logged in (`az login`).

---

## 2. Add GitHub Secrets

Go to **GitHub repo → Settings → Secrets and variables → Actions**.

### Required secrets

| Secret | Where to get it |
|--------|------------------|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Printed by the script; or **Azure Portal → Static Web Apps → \<your-swa\> → Manage deployment token** |
| `AZURE_WEBAPP_NAME` | Printed by the script (e.g. `aci-api-we-xxxxxx`); or **Azure Portal → Web Apps** |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | **Azure Portal → Web Apps → \<your-backend\> → Get publish profile** → copy entire XML |
| `AZURE_CLIENT_ID` | Service principal; printed if you used `-CreateServicePrincipal` |
| `AZURE_TENANT_ID` | Service principal; printed by script or `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | Printed by script or `az account show --query id -o tsv` |
| `AZURE_CLIENT_SECRET` | Service principal; printed only if you used `-CreateServicePrincipal` |

### Create Service Principal manually (if not using `-CreateServicePrincipal`)

```bash
# Replace rg-crm-aci-we with your resource group name
az ad sp create-for-rbac --name "github-aci-crm" --role Contributor --scopes "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/rg-crm-aci-we"
```

Copy `appId` → `AZURE_CLIENT_ID`, `password` → `AZURE_CLIENT_SECRET`, `tenant` → `AZURE_TENANT_ID`, and use your subscription ID for `AZURE_SUBSCRIPTION_ID`.

---

## 3. Add Variable (optional)

For the frontend to call the deployed backend:

| Variable | Value |
|----------|--------|
| `VITE_API_URL` | Backend URL, no trailing slash (e.g. `https://aci-api-we-xxxxxx.azurewebsites.net`) |

Add as **Variable** (not Secret): **Settings → Secrets and variables → Actions → Variables**.

---

## 4. OpenAI API Key (Optional - for Intelligent Sales Writer)

The Intelligent Sales Writer works with templates by default. To enable AI-powered generation:

### Local Development

**Option 1: User Secrets (recommended)**
```bash
cd backend/src/ACI.WebApi
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-key-here"
```

**Option 2: Environment Variable**
```powershell
$env:OpenAI__ApiKey = "sk-proj-your-key-here"
```

### Azure Deployment

Add to **Azure Portal → Web Apps → \<your-backend\> → Configuration → Application settings**:

| Name | Value |
|------|-------|
| `OpenAI__ApiKey` | Your OpenAI API key |
| `OpenAI__Model` | `gpt-4o-mini` (or `gpt-4o` for better quality) |

Or add as GitHub Secret and reference in deployment workflow.

---

## 5. Quick check

- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN` added  
- [ ] `AZURE_WEBAPP_NAME` added  
- [ ] `AZURE_WEBAPP_PUBLISH_PROFILE` added (full XML)  
- [ ] `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_CLIENT_SECRET` added  
- [ ] `VITE_API_URL` added as **variable** (optional)
- [ ] `OpenAI__ApiKey` added (optional, for Intelligent Sales Writer)

---

## 6. Trigger deploy

Push to `main` or run **Actions → Build and deploy backend to Azure Web App → Run workflow**.

---

## 7. Legacy / other regions

- **East US 2:** `./scripts/azure-create.ps1 -SqlAdminPassword '...'`
- **Web App only** (existing RG/SQL): `./scripts/azure-create-webapp-only.ps1 -SqlAdminPassword '...' -SqlServerName "aci-sql-xxx" -WebAppName "aci-api-xxx"`
