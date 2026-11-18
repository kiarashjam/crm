// Azure monitoring for the CadenceE CRM API (cloud role cadence-api) and the
// Pavillon 46 backend (cloud role pavillon46-api).
//
// Deploy it with deploy.sh, which finds the web apps, the SQL database and the log
// categories each site supports, then sets APPLICATIONINSIGHTS_CONNECTION_STRING and
// the health check path on the apps (neither is done here: see deploy.sh). The runbook
// is docs/MONITORING.md.
//
// What it holds, all in the resource group it is deployed to:
//   - a Log Analytics workspace with a daily ingestion cap, and one workspace-based
//     Application Insights resource that both apps send to;
//   - the App Services' console and HTTP logs sent to that workspace (in each app's
//     own resource group);
//   - a standard availability test on each app's /health;
//   - one action group (email, and Teams through a webhook when one is given);
//   - the alert rules of the monitoring plan.
//
// Every resource has a fixed name, so deploying again updates them in place. Alert
// rules changed by hand in the portal are put back to what this file says.

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Where and what
// ---------------------------------------------------------------------------

@description('Region of the workspace, Application Insights, the availability tests and the log alert rules. Defaults to the resource group\'s.')
param location string = resourceGroup().location

@description('Prefix of every resource name this creates.')
@minLength(2)
@maxLength(30)
param namePrefix string = 'cadence'

@description('Name of the CRM API web app.')
param crmAppName string

@description('Resource group of the CRM API web app.')
param crmAppResourceGroup string = resourceGroup().name

@description('The CRM API\'s /health address, tested from several locations.')
param crmHealthUrl string

@description('Log categories to send from the CRM web app to the workspace (only ones it supports). Empty: none.')
param crmDiagnosticCategories array = [
  'AppServiceConsoleLogs'
  'AppServiceHTTPLogs'
]

@description('Also monitor the Pavillon 46 backend.')
param includePavillon bool = true

@description('Name of the Pavillon 46 backend web app.')
param pavillonAppName string = 'pavillon46-api'

@description('Resource group of the Pavillon 46 web app.')
param pavillonAppResourceGroup string = resourceGroup().name

@description('The Pavillon 46 backend\'s /health address. Required when includePavillon is true.')
param pavillonHealthUrl string = ''

@description('Log categories to send from the Pavillon 46 web app to the workspace (only ones it supports). Empty: none.')
param pavillonDiagnosticCategories array = [
  'AppServiceConsoleLogs'
  'AppServiceHTTPLogs'
]

// ---------------------------------------------------------------------------
// Who is told
// ---------------------------------------------------------------------------

@description('Email address(es) for alerts, comma-separated. The first is the tech owner.')
@minLength(3)
param alertEmail string

@description('Teams incoming-webhook or Workflows URL. Empty: no Teams notification.')
@secure()
param teamsWebhookUrl string = ''

@description('Send the Teams webhook the common alert schema. A Teams Workflows flow that parses the alert needs it.')
param teamsUseCommonAlertSchema bool = true

@description('Short name of the action group, shown in SMS and some emails (12 characters at most).')
@maxLength(12)
param actionGroupShortName string = 'CadenceE'

// ---------------------------------------------------------------------------
// Cost bounds
// ---------------------------------------------------------------------------

@description('Daily ingestion cap of the workspace, in GB. Collection stops for the rest of the day when it is reached (an alert says so).')
@minValue(1)
param dailyCapGb int = 1

@description('Days the workspace keeps data.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Availability test locations (Azure population ids). Each location runs every test once per interval, and each run is billed.')
@minLength(2)
param availabilityTestLocations array = [
  'emea-nl-ams-azr' // West Europe
  'emea-gb-db3-azr' // North Europe
  'emea-fr-pra-edge' // France Central
]

@description('Seconds between availability test runs, per location.')
@allowed([
  300
  600
  900
])
param availabilityTestFrequency int = 300

@description('Alert when at least this many locations fail the availability test.')
@minValue(1)
param availabilityFailedLocationCount int = 2

// ---------------------------------------------------------------------------
// Thresholds (see "Tuning" in docs/MONITORING.md)
// ---------------------------------------------------------------------------

@description('API server errors: alert when more than this percentage of an app\'s requests return 5xx over 10 minutes...')
param serverErrorPercent int = 2

@description('...and at least this many of them failed (so a single 500 in a quiet hour does not alert).')
@minValue(1)
param serverErrorMinCount int = 3

@description('API server errors: alert when an app returns more than this many 5xx in 5 minutes, whatever the rate.')
param serverErrorBurstCount int = 20

@description('New exceptions: alert when an app records more than this many exceptions in 10 minutes.')
param exceptionCount int = 10

@description('Also create the Application Insights "Failure Anomalies" smart detection rule, sent to the action group.')
param enableSmartDetection bool = true

// ---------------------------------------------------------------------------
// The CRM's Azure SQL database (optional; deploy.sh finds it)
// ---------------------------------------------------------------------------

@description('SQL server name (without .database.windows.net). Empty: no database alerts.')
param sqlServerName string = ''

@description('Database name. Empty: no database alerts.')
param sqlDatabaseName string = ''

@description('Resource group of the SQL server.')
param sqlResourceGroup string = resourceGroup().name

@description('Utilization metric: dtu_consumption_percent for DTU tiers (Basic, Standard, Premium), cpu_percent for vCore and serverless.')
@allowed([
  'dtu_consumption_percent'
  'cpu_percent'
])
param sqlUtilizationMetric string = 'dtu_consumption_percent'

@description('Database under strain: alert when utilization averages above this percentage over 15 minutes.')
param sqlUtilizationPercent int = 80

@description('Database: alert when more than this many connections fail (system errors) in 15 minutes.')
param sqlFailedConnections int = 5

@description('Database: alert when the database is fuller than this percentage of its maximum size.')
param sqlStoragePercent int = 85

// ---------------------------------------------------------------------------

var hasSql = !empty(sqlServerName) && !empty(sqlDatabaseName)
var sqlDatabaseId = hasSql
  ? resourceId(sqlResourceGroup, 'Microsoft.Sql/servers/databases', sqlServerName, sqlDatabaseName)
  : ''

var alertEmails = filter(map(split(alertEmail, ','), e => trim(e)), e => !empty(e))

var apps = concat(
  [
    {
      key: 'crm'
      role: 'cadence-api'
      label: 'CadenceE CRM API'
      siteName: crmAppName
      resourceGroup: crmAppResourceGroup
      healthUrl: crmHealthUrl
      diagnosticCategories: crmDiagnosticCategories
    }
  ],
  includePavillon
    ? [
        {
          key: 'pavillon'
          role: 'pavillon46-api'
          label: 'Pavillon 46 backend'
          siteName: pavillonAppName
          resourceGroup: pavillonAppResourceGroup
          healthUrl: pavillonHealthUrl
          diagnosticCategories: pavillonDiagnosticCategories
        }
      ]
    : []
)
var appsWithDiagnostics = filter(apps, app => !empty(app.diagnosticCategories))

// The metric alert window must hold at least one run of each location.
var availabilityWindow = availabilityTestFrequency == 300 ? 'PT5M' : 'PT15M'

// ---------------------------------------------------------------------------
// Collect
// ---------------------------------------------------------------------------

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    workspaceCapping: {
      dailyQuotaGb: dailyCapGb
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

module appServiceDiagnostics 'modules/appServiceDiagnostics.bicep' = [
  for app in appsWithDiagnostics: {
    name: '${namePrefix}-monitoring-diagnostics-${app.key}'
    scope: resourceGroup(app.resourceGroup)
    params: {
      siteName: app.siteName
      workspaceId: workspace.id
      categories: app.diagnosticCategories
      settingName: '${namePrefix}-monitoring'
    }
  }
]

// ---------------------------------------------------------------------------
// Alert: who is told
// ---------------------------------------------------------------------------

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${namePrefix}-alerts'
  location: 'global'
  properties: {
    groupShortName: actionGroupShortName
    enabled: true
    emailReceivers: [
      for (email, i) in alertEmails: {
        name: i == 0 ? 'Tech owner' : 'Email ${i + 1}'
        emailAddress: email
        useCommonAlertSchema: true
      }
    ]
    webhookReceivers: empty(teamsWebhookUrl)
      ? []
      : [
          {
            name: 'Teams'
            serviceUri: teamsWebhookUrl
            useCommonAlertSchema: teamsUseCommonAlertSchema
          }
        ]
  }
}

// ---------------------------------------------------------------------------
// Check: is each site up? (availability tests and the "site down" alerts)
// ---------------------------------------------------------------------------

resource healthTests 'Microsoft.Insights/webtests@2022-06-15' = [
  for app in apps: {
    name: '${namePrefix}-health-${app.role}'
    location: location
    // Ties the test to the Application Insights resource (its Availability view).
    tags: {
      'hidden-link:${appInsights.id}': 'Resource'
    }
    kind: 'standard'
    properties: {
      SyntheticMonitorId: '${namePrefix}-health-${app.role}'
      Name: '${app.label} /health'
      Description: 'GET ${app.healthUrl}: 200 means Healthy or Degraded, 503 Unhealthy.'
      Enabled: true
      Kind: 'standard'
      Frequency: availabilityTestFrequency
      Timeout: 30
      // A failed run is tried again before it counts as a failure.
      RetryEnabled: true
      Locations: [for id in availabilityTestLocations: { Id: id }]
      Request: {
        RequestUrl: app.healthUrl
        HttpVerb: 'GET'
        ParseDependentRequests: false
      }
      ValidationRules: {
        ExpectedHttpStatusCode: 200
        SSLCheck: true
        SSLCertRemainingLifetimeCheck: 7
      }
    }
  }
]

resource siteDownAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for (app, i) in apps: {
    name: '${namePrefix}-site-down-${app.role}'
    location: 'global'
    tags: {
      'hidden-link:${appInsights.id}': 'Resource'
      'hidden-link:${healthTests[i].id}': 'Resource'
    }
    properties: {
      description: 'Site down: ${app.label} /health failed from ${availabilityFailedLocationCount} or more test locations. First: Application Insights > Availability and Failures; restart the App Service if it hangs. See docs/MONITORING.md.'
      severity: 1
      enabled: true
      scopes: [
        healthTests[i].id
        appInsights.id
      ]
      evaluationFrequency: 'PT1M'
      windowSize: availabilityWindow
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
        webTestId: healthTests[i].id
        componentId: appInsights.id
        failedLocationCount: availabilityFailedLocationCount
      }
      autoMitigate: true
      actions: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
]

// ---------------------------------------------------------------------------
// Alert: API server errors and exceptions (per cloud role)
// ---------------------------------------------------------------------------
// The queries run against Application Insights, so they use its table names
// (requests, exceptions, traces) and its time column, timestamp.

resource serverErrorRateAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '${namePrefix}-api-5xx-rate'
  location: location
  properties: {
    displayName: 'API server errors: 5xx rate'
    description: 'More than ${serverErrorPercent}% of an app\'s requests returned 5xx over 10 minutes (at least ${serverErrorMinCount} of them). First: Application Insights > Failures; find the trace id. See docs/MONITORING.md.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT10M'
    criteria: {
      allOf: [
        {
          query: 'let minFailed = ${serverErrorMinCount};\n${loadTextContent('queries/api-5xx-rate.kql')}'
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'FailedPercent'
          dimensions: [
            {
              name: 'cloud_RoleName'
              operator: 'Include'
              values: [
                '*'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: serverErrorPercent
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

resource serverErrorBurstAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '${namePrefix}-api-5xx-burst'
  location: location
  properties: {
    displayName: 'API server errors: 5xx burst'
    description: 'An app returned more than ${serverErrorBurstCount} 5xx responses in 5 minutes. First: Application Insights > Failures; find the trace id; restart the App Service if it hangs. See docs/MONITORING.md.'
    severity: 1
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: loadTextContent('queries/api-5xx.kql')
          timeAggregation: 'Count'
          dimensions: [
            {
              name: 'cloud_RoleName'
              operator: 'Include'
              values: [
                '*'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: serverErrorBurstCount
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

resource exceptionsAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '${namePrefix}-api-exceptions'
  location: location
  properties: {
    displayName: 'New exceptions'
    description: 'An app recorded more than ${exceptionCount} exceptions in 10 minutes. First: Application Insights > Failures > Exceptions, grouped by problem id. See docs/MONITORING.md.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT10M'
    criteria: {
      allOf: [
        {
          query: loadTextContent('queries/exceptions.kql')
          timeAggregation: 'Count'
          dimensions: [
            {
              name: 'cloud_RoleName'
              operator: 'Include'
              values: [
                '*'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: exceptionCount
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

// Sends Application Insights' own "Failure Anomalies" detection (an unusual rise in
// failed requests or dependency calls, learnt from the app's normal rate) to the
// action group. The portal makes this rule when it creates an Application Insights
// resource; a template does not, so it is made here.
resource failureAnomalies 'Microsoft.AlertsManagement/smartDetectorAlertRules@2021-04-01' = if (enableSmartDetection) {
  name: 'Failure Anomalies - ${appInsights.name}'
  location: 'global'
  properties: {
    description: 'Failure Anomalies notifies you of an unusual rise in the rate of failed HTTP requests or dependency calls.'
    state: 'Enabled'
    severity: 'Sev3'
    frequency: 'PT1M'
    detector: {
      id: 'FailureAnomaliesDetector'
    }
    scope: [
      appInsights.id
    ]
    actionGroups: {
      groupIds: [
        actionGroup.id
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Alert: the CRM's own monitoring signals (job.silent, outbox.backlog, ...)
// ---------------------------------------------------------------------------
// The app writes "monitoring.alert {Signal} {Count} {Detail}" when a signal goes on,
// again every hour while it stays on, and "monitoring.resolved {Signal}" when it
// clears. The query keeps, per signal, the latest of the two over the last two hours,
// and returns the signal while that latest is an alert. Split by Signal, each signal
// is its own alert: it fires once when the signal goes on (found within 5 minutes)
// and resolves when the app writes monitoring.resolved, or two hours after the last
// monitoring.alert if the app stops without writing it.
//
// Two rules, as the app logs job.silent and payments.failed at Error and the rest at
// Warning: Error ones are severity 1, Warning ones severity 2.

var signalQuery = loadTextContent('queries/monitoring-signals.kql')

resource signalErrorAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '${namePrefix}-monitoring-signal-error'
  location: location
  properties: {
    displayName: 'Monitoring signal (error): job silent or payments failed'
    description: 'The CRM raised a monitoring signal at Error (job.silent, payments.failed). The alert names the Signal. First steps per signal are in docs/MONITORING.md.'
    severity: 1
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT2H'
    criteria: {
      allOf: [
        {
          query: '${signalQuery}| where severityLevel >= 3\n'
          timeAggregation: 'Count'
          dimensions: [
            {
              name: 'Signal'
              operator: 'Include'
              values: [
                '*'
              ]
            }
            {
              name: 'cloud_RoleName'
              operator: 'Include'
              values: [
                '*'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

resource signalWarningAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '${namePrefix}-monitoring-signal-warning'
  location: location
  properties: {
    displayName: 'Monitoring signal (warning): jobs, email, portal'
    description: 'The CRM raised a monitoring signal at Warning (job.failing, outbox.backlog, outbox.failed, handoff.undelivered, portal.ping.failed). The alert names the Signal. First steps per signal are in docs/MONITORING.md.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT2H'
    criteria: {
      allOf: [
        {
          query: '${signalQuery}| where severityLevel < 3\n'
          timeAggregation: 'Count'
          dimensions: [
            {
              name: 'Signal'
              operator: 'Include'
              values: [
                '*'
              ]
            }
            {
              name: 'cloud_RoleName'
              operator: 'Include'
              values: [
                '*'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Alert: the daily cap was reached (monitoring is blind until the cap resets)
// ---------------------------------------------------------------------------

resource dailyCapAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '${namePrefix}-monitoring-daily-cap'
  location: location
  properties: {
    displayName: 'Monitoring data capped for today'
    description: 'The workspace reached its daily cap of ${dailyCapGb} GB: nothing more is collected until the cap resets, so the log alerts cannot fire. First: find what is noisy (docs/MONITORING.md, Costs); raise the cap if it is real traffic.'
    severity: 2
    enabled: true
    scopes: [
      workspace.id
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: loadTextContent('queries/daily-cap.kql')
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: false
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Alert: database under strain (only when the database is known)
// ---------------------------------------------------------------------------

resource sqlUtilizationAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (hasSql) {
  name: '${namePrefix}-db-utilization'
  location: 'global'
  properties: {
    description: 'Database under strain: ${sqlUtilizationMetric} averaged above ${sqlUtilizationPercent}% for 15 minutes. First: Query Performance Insight for slow queries; scale the SQL tier if it persists. See docs/MONITORING.md.'
    severity: 2
    enabled: true
    scopes: [
      sqlDatabaseId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'Utilization'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: sqlUtilizationMetric
          operator: 'GreaterThan'
          threshold: sqlUtilizationPercent
          timeAggregation: 'Average'
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource sqlFailedConnectionsAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (hasSql) {
  name: '${namePrefix}-db-failed-connections'
  location: 'global'
  properties: {
    description: 'Database: more than ${sqlFailedConnections} connections failed with system errors in 15 minutes. First: the SQL database\'s Resource health and /db-status; then the app log. See docs/MONITORING.md.'
    severity: 2
    enabled: true
    scopes: [
      sqlDatabaseId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'FailedConnections'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'connection_failed'
          operator: 'GreaterThan'
          threshold: sqlFailedConnections
          timeAggregation: 'Total'
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource sqlStorageAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (hasSql) {
  name: '${namePrefix}-db-storage'
  location: 'global'
  properties: {
    description: 'Database: more than ${sqlStoragePercent}% of its maximum size is used. When it is full, every write fails. First: raise the maximum size or the tier. See docs/MONITORING.md.'
    severity: 2
    enabled: true
    scopes: [
      sqlDatabaseId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT1H'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'Storage'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'storage_percent'
          operator: 'GreaterThan'
          threshold: sqlStoragePercent
          timeAggregation: 'Maximum'
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// ---------------------------------------------------------------------------

output workspaceName string = workspace.name
output workspaceId string = workspace.id
output appInsightsName string = appInsights.name
// The connection string is read by deploy.sh from the resource, not output here,
// so it is not kept in the deployment history.
output appInsightsId string = appInsights.id
output actionGroupName string = actionGroup.name
output monitoredApps array = [for app in apps: app.role]
output databaseAlerts bool = hasSql
