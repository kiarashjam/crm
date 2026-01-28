# Create Azure resource group + Static Web App + Azure SQL + Backend Web App
# Run: ./scripts/azure-create.ps1 -SqlAdminPassword 'YourSecurePwd1!'
# Requires: Azure CLI installed and logged in (az login)

param(
    [string]$ResourceGroupName = "rg-crm-aci",
    [string]$StaticWebAppName = "aci",
    [string]$Location = "eastus2",
    [Parameter(Mandatory = $true, HelpMessage = "Password for Azure SQL admin (min 8 chars, upper, lower, number, special).")]
    [string]$SqlAdminPassword,
    [string]$SqlServerName = "",      # default: aci-sql-<random>
    [string]$SqlDatabaseName = "ACI",
    [string]$SqlAdminLogin = "sqladmin",
    [string]$AppServicePlanName = "asp-aci",
    [string]$WebAppName = "",         # default: aci-api-<random>
    [switch]$SkipBackendAndDatabase = $false   # use to only create RG + Static Web App
)

$ErrorActionPreference = "Stop"

# Validate SQL password (Azure requirement)
if (-not $SkipBackendAndDatabase) {
    if ($SqlAdminPassword.Length -lt 8) { throw "SqlAdminPassword must be at least 8 characters." }
    if ($SqlAdminPassword -notmatch '[A-Z]') { throw "SqlAdminPassword must contain an uppercase letter." }
    if ($SqlAdminPassword -notmatch '[a-z]') { throw "SqlAdminPassword must contain a lowercase letter." }
    if ($SqlAdminPassword -notmatch '\d') { throw "SqlAdminPassword must contain a number." }
    if ($SqlAdminPassword -notmatch '[^A-Za-z0-9]') { throw "SqlAdminPassword must contain a special character." }
}

$suffix = -join ((48..57) + (97..122) | Get-Random -Count 6 | ForEach-Object { [char]$_ })
if (-not $SqlServerName) { $SqlServerName = "aci-sql-$suffix" }
if (-not $WebAppName) { $WebAppName = "aci-api-$suffix" }

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

if (-not $SkipBackendAndDatabase) {
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

    Write-Host "Creating Azure SQL Database: $SqlDatabaseName ..."
    az sql db create `
        --resource-group $ResourceGroupName `
        --server $SqlServerName `
        --name $SqlDatabaseName `
        --service-objective Basic `
        --zone-redundant false

    $sqlConnStr = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Persist Security Info=False;User ID=$SqlAdminLogin;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    Write-Host "Creating App Service Plan: $AppServicePlanName ..."
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
    Write-Host "Backend URL: $backendUrl"
    Write-Host "JWT Secret (store securely; used by backend): $jwtSecret"
    Write-Host ""
}

Write-Host "Getting Static Web App deployment token ..."
$token = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv
Write-Host ""
Write-Host "========== GitHub Secrets =========="
Write-Host "Add these in GitHub: Settings -> Secrets and variables -> Actions -> New repository secret"
Write-Host ""
Write-Host "1. AZURE_STATIC_WEB_APPS_API_TOKEN"
Write-Host $token
Write-Host ""
if (-not $SkipBackendAndDatabase) {
    Write-Host "2. AZURE_WEBAPP_NAME = $WebAppName"
    Write-Host "3. AZURE_WEBAPP_PUBLISH_PROFILE = (download from Azure Portal -> Web App '$WebAppName' -> Get publish profile, then paste entire XML)"
    Write-Host ""
    Write-Host "Frontend: set VITE_API_URL=$backendUrl when building (e.g. in GitHub Actions env or .env) so the app uses the deployed API."
}
Write-Host "===================================="
Write-Host ""
Write-Host "Static Web App URL (after first deploy): $swaUrl"
