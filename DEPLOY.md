# Publish to GitHub and Azure

## 1. Publish to GitHub

Create a new repository on GitHub (e.g. `crm` or `hubspot-ai-writer`), then run:

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

## 2. Create Azure resource group and website (cheapest = Free)

- Install [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) if needed.
- Log in: `az login`
- Run the script:

```powershell
./scripts/azure-create.ps1
```

Optional parameters (defaults are fine):

```powershell
./scripts/azure-create.ps1 -ResourceGroupName "rg-crm" -StaticWebAppName "crm-app" -Location "eastus2"
```

- Copy the **deployment token** printed at the end.

## 3. Add GitHub secret for Azure deploy

1. Open your repo on GitHub → **Settings** → **Secrets and variables** → **Actions**.
2. Click **New repository secret**.
3. Name: `AZURE_STATIC_WEB_APPS_API_TOKEN`
4. Value: paste the token from step 2.

## 4. Deploy (publish new version)

Deploys run automatically when:

- You **push to `main`** (excluding `.md` and `.gitignore`), or  
- You **publish a release**: Repo → **Releases** → **Create a new release** → choose tag (e.g. `v0.0.1`) → **Publish release**.

The workflow builds the app and deploys to Azure Static Web Apps.

## 5. Check that it works

1. After the first successful run, open the URL shown when you ran `azure-create.ps1` (e.g. `https://crm-hubspot-writer.azurestaticapps.net`).
2. Or in Azure Portal: **Static Web Apps** → your app → **Overview** → **URL**.
3. Confirm the HubSpot AI Writer app loads and navigation works (SPA routing is configured).

## Summary

| Step | What |
|------|------|
| 1 | Push code to GitHub |
| 2 | Run `./scripts/azure-create.ps1`, copy token |
| 3 | Add `AZURE_STATIC_WEB_APPS_API_TOKEN` in GitHub Actions secrets |
| 4 | Push to `main` or create a release to deploy |
| 5 | Open the Static Web App URL and verify |

Cost: **Azure Static Web Apps Free tier = $0/month** (cheapest option).
