# Create only App Service Plan + Web App + app settings (use when RG, SWA, SQL already exist)
# Run after azure-create.ps1 timed out or failed partway, or to add backend to an existing setup.
# Example: ./scripts/azure-create-webapp-only.ps1 -SqlAdminPassword 'YourPwd1!' -SqlServerName "aci-sql-wqkr5v" -WebAppName "aci-api-wqkr5v"

param(
    [string]$ResourceGroupName = "rg-crm-aci",
    [string]$StaticWebAppName = "aci",
    [string]$Location = "eastus2",
    [Parameter(Mandatory = $true)]
    [string]$SqlAdminPassword,
    [Parameter(Mandatory = $true)]
    [string]$SqlServerName,
    [string]$SqlDatabaseName = "ACI",
    [string]$SqlAdminLogin = "sqladmin",
    [string]$AppServicePlanName = "asp-aci",
    [Parameter(Mandatory = $true)]
    [string]$WebAppName
)

$ErrorActionPreference = "Stop"

$swaHost = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "defaultHostname" -o tsv
$sqlConnStr = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Persist Security Info=False;User ID=$SqlAdminLogin;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$jwtSecret = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object { [char]$_ })
$corsOrigins = "https://$swaHost,http://localhost:5173,http://localhost:3000"
$backendUrl = "https://$WebAppName.azurewebsites.net"

Write-Host "Creating App Service Plan: $AppServicePlanName ..."
az appservice plan create `
    --name $AppServicePlanName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku F1

Write-Host "Creating Web App: $WebAppName ..."
az webapp create `
    --name $WebAppName `
    --resource-group $ResourceGroupName `
    --plan $AppServicePlanName `
    --runtime "dotnet:8"

Write-Host "Configuring Web App settings ..."
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

$token = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv
Write-Host ""
Write-Host "========== GitHub Secrets =========="
Write-Host "AZURE_STATIC_WEB_APPS_API_TOKEN:" ; Write-Host $token
Write-Host "AZURE_WEBAPP_NAME = $WebAppName"
Write-Host "AZURE_WEBAPP_PUBLISH_PROFILE = (Azure Portal -> Web App '$WebAppName' -> Get publish profile)"
Write-Host "VITE_API_URL (variable) = $backendUrl"
Write-Host "===================================="
Write-Host "Backend URL: $backendUrl"
