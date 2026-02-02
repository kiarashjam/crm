# Create Azure CRM resources in West Europe (cheapest tiers)
# Run: ./scripts/azure-create-west-europe.ps1 -SqlAdminPassword 'YourSecurePwd1!'
# Requires: Azure CLI installed and logged in (az login)
#
# Creates: Resource group, Static Web App (Free), SQL (Basic), Web App (F1)
# Location: West Europe (westeurope)

param(
    [string]$ResourceGroupName = "rg-crm-aci-we",
    [string]$StaticWebAppName = "aci-we",
    [string]$Location = "westeurope",
    [Parameter(Mandatory = $true, HelpMessage = "Password for Azure SQL admin (min 8 chars, upper, lower, number, special).")]
    [string]$SqlAdminPassword,
    [string]$SqlServerName = "",      # default: aci-sql-we-<random>
    [string]$SqlDatabaseName = "ACI",
    [string]$SqlAdminLogin = "sqladmin",
    [string]$AppServicePlanName = "asp-aci-we",
    [string]$WebAppName = "",         # default: aci-api-we-<random>
    [switch]$CreateServicePrincipal = $false  # set to create SP for GitHub Actions (requires Contributor)
)

$ErrorActionPreference = "Stop"

# Validate SQL password
if ($SqlAdminPassword.Length -lt 8) { throw "SqlAdminPassword must be at least 8 characters." }
if ($SqlAdminPassword -notmatch '[A-Z]') { throw "SqlAdminPassword must contain an uppercase letter." }
if ($SqlAdminPassword -notmatch '[a-z]') { throw "SqlAdminPassword must contain a lowercase letter." }
if ($SqlAdminPassword -notmatch '\d') { throw "SqlAdminPassword must contain a number." }
if ($SqlAdminPassword -notmatch '[^A-Za-z0-9]') { throw "SqlAdminPassword must contain a special character." }

$suffix = -join ((48..57) + (97..122) | Get-Random -Count 6 | ForEach-Object { [char]$_ })
if (-not $SqlServerName) { $SqlServerName = "aci-sql-we-$suffix" }
if (-not $WebAppName) { $WebAppName = "aci-api-we-$suffix" }

Write-Host "=== Creating CRM resources in West Europe (cheapest tiers) ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "Creating resource group: $ResourceGroupName in $Location ..."
az group create --name $ResourceGroupName --location $Location

Write-Host "Creating Static Web App (Free tier): $StaticWebAppName ..."
az staticwebapp create `
    --name $StaticWebAppName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Free

$swaHost = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "defaultHostname" -o tsv
$swaUrl = "https://$swaHost"

Write-Host "Creating Azure SQL Server: $SqlServerName ..."
az sql server create `
    --name $SqlServerName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --admin-user $SqlAdminLogin `
    --admin-password $SqlAdminPassword

Write-Host "Allowing Azure services to access SQL Server ..."
az sql server firewall-rule create `
    --resource-group $ResourceGroupName `
    --server $SqlServerName `
    --name AllowAzureServices `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0

Write-Host "Creating Azure SQL Database (Basic - cheapest): $SqlDatabaseName ..."
az sql db create -g $ResourceGroupName -s $SqlServerName -n $SqlDatabaseName --service-objective Basic

$sqlConnStr = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Persist Security Info=False;User ID=$SqlAdminLogin;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "Creating App Service Plan (F1 Free): $AppServicePlanName ..."
az appservice plan create `
    --name $AppServicePlanName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku F1

Write-Host "Creating Web App (backend): $WebAppName ..."
az webapp create `
    --name $WebAppName `
    --resource-group $ResourceGroupName `
    --plan $AppServicePlanName `
    --runtime "dotnet:8"

$jwtSecret = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object { [char]$_ })
$corsOrigins = "https://$swaHost,http://localhost:5173,http://localhost:3000"
$backendUrl = "https://$WebAppName.azurewebsites.net"

Write-Host "Configuring Web App settings (connection string, JWT, CORS) ..."
az webapp config appsettings set `
    --name $WebAppName `
    --resource-group $ResourceGroupName `
    --settings `
        "ConnectionStrings__DefaultConnection=$sqlConnStr" `
        "Jwt__SecretKey=$jwtSecret" `
        "Jwt__Issuer=ACI" `
        "Jwt__Audience=ACI" `
        "Jwt__ExpiryMinutes=1440" `
        "Cors__AllowedOrigins=$corsOrigins" `
    --output none

Write-Host ""
Write-Host "Getting Static Web App deployment token ..."
$token = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv

# Optional: Create Service Principal for GitHub Actions
$spJson = $null
if ($CreateServicePrincipal) {
    Write-Host ""
    Write-Host "Creating Service Principal for GitHub Actions ..."
    $spName = "github-aci-crm-deploy"
    $subscriptionId = az account show --query id -o tsv
    $spJson = az ad sp create-for-rbac `
        --name $spName `
        --role "Contributor" `
        --scopes "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName" `
        --sdk-auth
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Could not create Service Principal. You may need 'User Access Administrator' permission. Add secrets manually - see SECRETS_SETUP.md" -ForegroundColor Yellow
        $spJson = $null
    }
}

Write-Host ""
Write-Host "========== GITHUB SECRETS (Settings -> Secrets and variables -> Actions) ==========" -ForegroundColor Green
Write-Host ""
Write-Host "Add these as REPOSITORY SECRETS:"
Write-Host ""
Write-Host "AZURE_STATIC_WEB_APPS_API_TOKEN"
Write-Host $token
Write-Host ""
Write-Host "AZURE_WEBAPP_NAME = $WebAppName"
Write-Host "AZURE_WEBAPP_PUBLISH_PROFILE = (Azure Portal -> Web Apps -> $WebAppName -> Get publish profile -> copy entire XML)"
Write-Host ""
if ($spJson) {
    $sp = $spJson | ConvertFrom-Json
    Write-Host "AZURE_CLIENT_ID = $($sp.clientId)"
    Write-Host "AZURE_CLIENT_SECRET = $($sp.clientSecret)"
    Write-Host "AZURE_SUBSCRIPTION_ID = $($sp.subscriptionId)"
    Write-Host "AZURE_TENANT_ID = $($sp.tenantId)"
} else {
    $subId = az account show --query id -o tsv
    $tenId = az account show --query tenantId -o tsv
    Write-Host "AZURE_CLIENT_ID = (run script with -CreateServicePrincipal, or create SP manually - see SECRETS_SETUP.md)"
    Write-Host "AZURE_CLIENT_SECRET = (from Service Principal)"
    Write-Host "AZURE_SUBSCRIPTION_ID = $subId"
    Write-Host "AZURE_TENANT_ID = $tenId"
}
Write-Host ""
Write-Host "Add as VARIABLE (optional, for frontend -> backend):"
Write-Host "VITE_API_URL = $backendUrl"
Write-Host ""
Write-Host "========== URLS ==========" -ForegroundColor Green
Write-Host "Static Web App (frontend, after deploy): $swaUrl"
Write-Host "Backend API: $backendUrl"
Write-Host ""
Write-Host "JWT Secret (stored in Web App; backup if needed): $jwtSecret"
Write-Host ""
Write-Host "Done. Push to main to trigger GitHub Actions." -ForegroundColor Cyan
