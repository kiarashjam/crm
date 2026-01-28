# Create Azure resource group + Static Web App (Free tier - cheapest, $0/month)
# Run: ./scripts/azure-create.ps1
# Requires: Azure CLI installed and logged in (az login)

param(
    [string]$ResourceGroupName = "rg-crm-hubspot",
    [string]$StaticWebAppName = "crm-hubspot-writer",
    [string]$Location = "eastus2"
)

Write-Host "Creating resource group: $ResourceGroupName in $Location ..."
az group create --name $ResourceGroupName --location $Location

Write-Host "Creating Static Web App (Free tier): $StaticWebAppName ..."
az staticwebapp create `
    --name $StaticWebAppName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Free

Write-Host ""
Write-Host "Getting deployment token (add this as GitHub secret AZURE_STATIC_WEB_APPS_API_TOKEN) ..."
$token = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv
Write-Host $token

$hostName = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "defaultHostname" -o tsv
Write-Host ""
Write-Host "Your site URL (after first deploy): https://$hostName"
Write-Host ""
Write-Host "Next: Add the token above as a GitHub Actions secret named AZURE_STATIC_WEB_APPS_API_TOKEN."
