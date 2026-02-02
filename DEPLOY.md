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
| `AZURE_CLIENT_ID` | Application (client) ID of the app registration (for backend deploy) |
| `AZURE_TENANT_ID` | Directory (tenant) ID of the app registration |
| `AZURE_SUBSCRIPTION_ID` | Your Azure subscription ID |
| `AZURE_CLIENT_SECRET` | Client secret value from the app registration (Certificates & secrets) |

**Creating the four Azure login secrets (for backend deploy):**

1. In Azure Portal: **Microsoft Entra ID** → **App registrations** → **New registration** (e.g. name `github-actions-crm`). Note the **Application (client) ID** and **Directory (tenant) ID**.
2. In the app: **Certificates & secrets** → **New client secret** → copy the **Value** (not the Secret ID).
3. **Subscriptions** → your subscription → **Access control (IAM)** → **Add role assignment** → Role: **Contributor** → Members: select the app you created → Save. Note your **Subscription ID** (Subscriptions → your subscription).
4. Add these four **repository secrets** in GitHub (Settings → Secrets and variables → Actions):
   - `AZURE_CLIENT_ID` = Application (client) ID  
   - `AZURE_TENANT_ID` = Directory (tenant) ID  
   - `AZURE_SUBSCRIPTION_ID` = Subscription ID  
   - `AZURE_CLIENT_SECRET` = client secret value

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

## 6. Update Azure database and publish website

### How the Azure database is updated

The backend runs **EF Core migrations on startup** (`Program.cs` → `MigrateAsync()`). When you deploy the backend to the Azure Web App, the app starts and applies any pending migrations (e.g. **SalesCrmCore**: Leads, TaskItems, Activities, Contact.Phone, Deal.ContactId, ExpectedCloseDateUtc, IsWon) to the Azure SQL database. No separate “run migration” step is needed as long as the Web App has the correct connection string (the create script sets `ConnectionStrings__DefaultConnection`).

### Publish the website (frontend + backend)

1. **Push to `main`**
   - **Frontend** deploys on every push to `main` (except when only `.md` / `.gitignore` change).
   - **Backend** deploys only when files under `backend/` or `.github/workflows/deploy-backend.yml` change.

2. **To update the Azure database** (apply the latest migration):
   - Deploy the backend: either **push a change under `backend/`** (e.g. commit the migration and push) or **run the backend workflow manually**:
     - GitHub → **Actions** → **Build and deploy backend to Azure Web App** → **Run workflow** → Run.
   - After the backend finishes deploying, the Web App restarts and runs `MigrateAsync()`, updating the Azure SQL database.

3. **Ensure frontend talks to the backend**
   - In GitHub: **Settings** → **Secrets and variables** → **Actions** → **Variables** → set `VITE_API_URL` to your backend URL (e.g. `https://aci-api-xxxxx.azurewebsites.net`, no trailing slash). Re-run the frontend workflow or push a small change to trigger a new frontend build so the new variable is baked in.

### Checklist: confirm everything is working

| Check | How |
|-------|-----|
| Backend is live | Open `https://<AZURE_WEBAPP_NAME>.azurewebsites.net` (or `/swagger`). You should get 200 or 401, not 500. |
| Database is updated | After a backend deploy, open Swagger or use the app: create a Lead, Task, or Activity. If those work, the SalesCrmCore migration was applied. |
| Frontend is live | Open your Static Web App URL (e.g. `https://<name>.azurestaticapps.net`). The app should load. |
| Frontend → backend | Log in from the Static Web App. If `VITE_API_URL` is set correctly, login and data (leads, deals, etc.) use the Azure backend. |
| CORS | If the frontend shows CORS errors when calling the API, ensure the Web App has `Cors__AllowedOrigins` including your Static Web App host (the create script sets this). |

### If the backend workflow didn’t run

- Backend workflow runs only when `backend/**` or `.github/workflows/deploy-backend.yml` change. If you only changed frontend or docs, trigger it manually: **Actions** → **Build and deploy backend to Azure Web App** → **Run workflow**.

### Optional: run migrations against Azure from your machine

If you need to apply migrations to Azure SQL without redeploying (e.g. you have the connection string and want to update the DB from local):

1. Get the connection string from **Azure Portal** → your Web App → **Configuration** → **Application settings** → `ConnectionStrings__DefaultConnection` (or Connection strings).
2. From the repo root:

```powershell
cd backend
$env:ConnectionStrings__DefaultConnection = "Server=tcp:YOUR_SQL_SERVER.database.windows.net,1433;Initial Catalog=ACI;..."
dotnet ef database update --project src/ACI.Infrastructure/ACI.Infrastructure.csproj --startup-project src/ACI.WebApi/ACI.WebApi.csproj
```

Normally you don’t need this: deploying the backend and letting it run `MigrateAsync()` on startup is enough.

## Summary

| Step | What |
|------|------|
| 1 | Push code to GitHub |
| 2 | Run `./scripts/azure-create.ps1 -SqlAdminPassword '...'` (or `-SkipBackendAndDatabase` for frontend-only) |
| 3 | Add `AZURE_STATIC_WEB_APPS_API_TOKEN`, `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_PUBLISH_PROFILE`, and `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` / `AZURE_CLIENT_SECRET`; set `VITE_API_URL` variable so frontend uses the backend |
| 4 | Push to `main` or create a release → frontend and/or backend deploy. To update Azure DB: deploy backend (push `backend/` or run "Build and deploy backend to Azure Web App" manually). |
| 5 | Check Static Web App URL, backend Swagger, and login from the site (see §6 checklist) |

**Cost:**  
- **Static Web App** Free tier: $0/month.  
- **Azure SQL** Basic: paid. **App Service** uses **F1** (Free) by default; switch to B1 in the script if you have quota and want a paid plan.

**Quota:** If App Service Plan creation fails with *"Operation cannot be completed without additional quota"*, your subscription has no App Service (Free/Basic) quota in that region. Either request a quota increase (Azure Portal → Subscriptions → your subscription → Usage + quotas → App Service), or run the script in another region (e.g. `-Location eastus`) where you have quota.
