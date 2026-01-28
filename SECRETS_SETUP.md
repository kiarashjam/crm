# GitHub Secrets & Variables Checklist

Use this after running `./scripts/azure-create.ps1` (or `azure-create-webapp-only.ps1`). Add everything under **Settings → Secrets and variables → Actions**.

## Secrets (Settings → Actions → Secrets)

| Secret | Where to get it |
|--------|------------------|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Printed by the script; or **Azure Portal → Static Web Apps → aci → Manage deployment token** |
| `AZURE_WEBAPP_NAME` | Printed by the script (e.g. `aci-api-xxxxxx`); or **Azure Portal → Web Apps** |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | **Azure Portal → Web Apps → \<your-backend-app\> → Get publish profile** → copy entire XML |

## Variable (optional, for frontend → backend)

| Variable | Value |
|----------|--------|
| `VITE_API_URL` | Backend URL, no trailing slash (e.g. `https://aci-api-xxxxxx.azurewebsites.net`) |

---

## Quick check

- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN` added  
- [ ] `AZURE_WEBAPP_NAME` added  
- [ ] `AZURE_WEBAPP_PUBLISH_PROFILE` added (full XML)  
- [ ] `VITE_API_URL` added as **variable** (optional)

After that, push to `main` or publish a release to trigger deploys.
