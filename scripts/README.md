# Scripts (`scripts/`)

Automation scripts for provisioning/deploying infrastructure.

## Contents

### West Europe (recommended – cheapest, West Europe)

- **`azure-create-west-europe.ps1`**: Creates all CRM resources in **West Europe** with cheapest tiers:
  - Resource group `rg-crm-aci-we`, Static Web App (Free), SQL (Basic), Web App (F1 Free)
  - Use `-CreateServicePrincipal` to auto-create GitHub-ready Azure credentials
  - Run: `./scripts/azure-create-west-europe.ps1 -SqlAdminPassword 'YourPwd1!'`

### Legacy / other regions

- **`azure-create.ps1`**: Creates Azure resources (default: East US 2):
  - Resource group, Static Web App, Azure SQL Server + Database, App Service Plan + Web App
  - Requires `-SqlAdminPassword`. Use `-SkipBackendAndDatabase` for frontend-only.
- **`azure-create-webapp-only.ps1`**: Creates only App Service Plan + Web App. Use when RG/SQL already exist.

