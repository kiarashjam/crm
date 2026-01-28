# Scripts (`scripts/`)

Automation scripts for provisioning/deploying infrastructure.

## Contents
- **`azure-create.ps1`**: Creates Azure resources in a single resource group (used with `DEPLOY.md`):
  - Resource group, Static Web App, Azure SQL Server + Database, App Service Plan + Web App
  - Configures connection string, JWT, and CORS for the Web App
  - Requires `-SqlAdminPassword` unless `-SkipBackendAndDatabase` is used.
- **`azure-create-webapp-only.ps1`**: Creates only App Service Plan + Web App + settings. Use if the main script timed out after SQL (same resource group). See `DEPLOY.md`.

