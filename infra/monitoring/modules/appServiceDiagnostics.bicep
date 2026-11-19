// Sends an App Service's platform logs (console output, HTTP access log) to the
// Log Analytics workspace. Deployed into the web app's own resource group, which may
// differ from the one that holds the monitoring resources.
//
// Only categories the site supports may be named: AppServiceConsoleLogs, for one, is
// not offered on every Windows plan. deploy.sh asks Azure which categories the site
// has and passes the ones it can use.

@description('Name of the existing web app.')
param siteName string

@description('Resource id of the Log Analytics workspace that receives the logs.')
param workspaceId string

@description('Log categories to send, all of which the site must support.')
@minLength(1)
param categories array

@description('Name of the diagnostic setting. Re-running replaces the setting of this name and leaves any other untouched.')
param settingName string = 'cadence-monitoring'

resource site 'Microsoft.Web/sites@2023-12-01' existing = {
  name: siteName
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: settingName
  scope: site
  properties: {
    workspaceId: workspaceId
    logs: [
      for category in categories: {
        category: category
        enabled: true
      }
    ]
  }
}
