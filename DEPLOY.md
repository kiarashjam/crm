# Publish to GitHub and Azure

## 1. Publish to GitHub

Create a new repository on GitHub (e.g. `crm` or `aci`), then run:

```powershell
cd "c:\Users\KiaJamishidi\OneDrive - BonApp Group\Documents\repo\crm"

git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO_NAME.git
git push -u origin main
```

Replace `YOUR_USERNAME` and `YOUR_REPO_NAME` with your GitHub username and repo name.

## 2. Create Azure resources (resource group, Static Web App, SQL, Backend Web App)

All resources are created in the **same resource group** (`rg-crm-aci` by default).

- Install [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) if needed.
- Log in: `az login`
- Run the script **(SQL admin password required)**:

```powershell
./scripts/azure-create.ps1 -SqlAdminPassword 'YourSecurePwd1!'
```

The password must have at least 8 characters, upper, lower, number, and a special character. Avoid `"` and `'` in the password.

**Optional parameters:**

```powershell
./scripts/azure-create.ps1 `
  -ResourceGroupName "rg-crm-aci" `
  -StaticWebAppName "aci" `
  -Location "eastus2" `
  -SqlAdminPassword 'YourSecurePwd1!' `
  -SqlServerName "aci-sql-mysuffix" `
  -SqlDatabaseName "ACI" `
  -SqlAdminLogin "sqladmin" `
  -AppServicePlanName "asp-aci" `
  -WebAppName "aci-api-mysuffix"
```

**Frontend-only (no database or backend):**

```powershell
./scripts/azure-create.ps1 -SkipBackendAndDatabase
```

(This only creates the resource group and Static Web App, like the original script. No `-SqlAdminPassword` needed.)

**Finish Web App only** (if the main script timed out after creating SQL):

```powershell
./scripts/azure-create-webapp-only.ps1 -SqlAdminPassword 'YourPwd1!' -SqlServerName "aci-sql-XXXXXX" -WebAppName "aci-api-XXXXXX"
```

Use the same suffix as your SQL server (see Azure Portal → SQL servers).

The script prints:

- **Static Web App** deployment token and URL
- **Backend Web App** name and URL
- **GitHub secrets** to add (see below)

## 3. Add GitHub secrets (and optional variable)

1. Open your repo on GitHub → **Settings** → **Secrets and variables** → **Actions**.
2. Add **Secrets**:

| Secret | Value |
|--------|--------|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Token printed by the script (for frontend deploy) |
| `AZURE_WEBAPP_NAME` | Backend Web App name (e.g. `aci-api-xxxxx`) printed by the script |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Download from Azure Portal → **Web App** (your backend app) → **Get publish profile** → paste entire XML |
| `AZURE_CREDENTIALS` | JSON for service principal (see below) – **required for backend deploy** |

**Creating `AZURE_CREDENTIALS` (for backend deploy):**

1. In Azure Portal: **Microsoft Entra ID** → **App registrations** → **New registration** (e.g. name `github-actions-crm`). Note the **Application (client) ID** and **Directory (tenant) ID**.
2. In the app: **Certificates & secrets** → **New client secret** → copy the **Value** (not the Secret ID).
3. **Subscriptions** → your subscription → **Access control (IAM)** → **Add role assignment** → Role: **Contributor** → Members: select the app you created → Save.
4. Build this JSON (replace placeholders with your values), then add it as secret `AZURE_CREDENTIALS`:

```json
{
  "clientId": "<Application (client) ID>",
  "clientSecret": "<client secret value>",
  "subscriptionId": "<your subscription ID>",
  "tenantId": "<Directory (tenant) ID>"
}
```

3. **Optional** – so the frontend calls the deployed API:
   - **Variables** → **New repository variable**
   - Name: `VITE_API_URL`
   - Value: backend URL (e.g. `https://aci-api-xxxxx.azurewebsites.net`), no trailing slash

## 4. Deploy

**Frontend (Static Web App)**  
- Workflow: `Build and deploy to Azure Static Web Apps`  
- Runs on **push to `main`** (ignoring `.md` / `.gitignore`) or **publish release**.  
- Builds the React app and deploys to the Static Web App.

**Backend (Web App)**  
- Workflow: `Build and deploy backend to Azure Web App`  
- Runs on **push to `main`** when `backend/**` or `.github/workflows/deploy-backend.yml` change, on **publish release**, or via **workflow_dispatch**.  
- Builds the .NET Web API and deploys to the Azure Web App. EF migrations run on app startup.

## 5. Verify

1. **Frontend:** Open the Static Web App URL from the script (e.g. `https://aci.azurestaticapps.net`). Confirm the app loads and SPA routing works.
2. **Backend:** Open `https://<your-webapp-name>.azurewebsites.net` (or `/swagger` if you enable it in production). Confirm the API responds (e.g. 401 on protected routes without a token).
3. If `VITE_API_URL` is set, use the frontend, log in, and check that API calls go to the deployed backend.

## Summary

| Step | What |
|------|------|
| 1 | Push code to GitHub |
| 2 | Run `./scripts/azure-create.ps1 -SqlAdminPassword '...'` (or `-SkipBackendAndDatabase` for frontend-only) |
| 3 | Add `AZURE_STATIC_WEB_APPS_API_TOKEN`, `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_PUBLISH_PROFILE`, and `AZURE_CREDENTIALS`; optionally `VITE_API_URL` variable |
| 4 | Push to `main` or create a release → frontend and/or backend deploy |
| 5 | Check Static Web App URL and backend Swagger |

**Cost:**  
- **Static Web App** Free tier: $0/month.  
- **Azure SQL** Basic: paid. **App Service** uses **F1** (Free) by default; switch to B1 in the script if you have quota and want a paid plan.

**Quota:** If App Service Plan creation fails with *"Operation cannot be completed without additional quota"*, your subscription has no App Service (Free/Basic) quota in that region. Either request a quota increase (Azure Portal → Subscriptions → your subscription → Usage + quotas → App Service), or run the script in another region (e.g. `-Location eastus`) where you have quota.
