# Monitoring runbook

How the CadenceE CRM API and the Pavillon 46 backend are watched in Azure, where to look
when something is wrong, and what to do first when an alert arrives.

Everything here is defined in [`infra/monitoring/`](../infra/monitoring/) and set up by
the **Monitoring — set up Azure monitoring** workflow
([`.github/workflows/monitoring-setup.yml`](../.github/workflows/monitoring-setup.yml)).
Change it there, not in the portal: the next run puts the portal back to what the files
say.

| App | Web app | Cloud role name (in Application Insights) |
|---|---|---|
| CadenceE CRM API | the `AZURE_WEBAPP_NAME` secret | `cadence-api` |
| Pavillon 46 backend | `pavillon46-api` (or the `PAVILLON_WEBAPP_NAME` variable) | `pavillon46-api` |

## What is monitored

| What | How | Where to see it |
|---|---|---|
| Requests, failures, exceptions, dependencies (SQL, HTTP) and logs of both APIs | Each app sends to one Application Insights resource, `cadence-insights`, when its `APPLICATIONINSIGHTS_CONNECTION_STRING` setting is set | Application Insights |
| App Service console output and HTTP access log | Diagnostic setting `cadence-monitoring` on each web app, to the Log Analytics workspace `cadence-logs` | Workspace › Logs: `AppServiceConsoleLogs` (Linux plans only), `AppServiceHTTPLogs` |
| Is each site up? | A standard availability test on each app's `/health`, from 3 locations every 5 minutes; App Service's own health check on `/health` | Application Insights › Availability; web app › Health check |
| The CRM's own signals: jobs, email outbox, portal handoffs, payments | The CRM logs `monitoring.alert {Signal} {Count} {Detail}`; an alert rule watches for it | The alert; the KQL below; Settings › System health in the CRM |
| The CRM's database | Azure SQL metrics: DTU or CPU, failed connections, storage | SQL database › Metrics, Query Performance Insight |
| Each deploy of the CRM API | A smoke test in the deploy workflow polls `/health` and `/db-status` | The GitHub Actions run |
| What needs a human each day | The CRM's daily digest email (failed automations, bounces, new suppressions) | Club ops' inbox |

The monitoring resources live in the CRM web app's resource group (or the
`MONITORING_RESOURCE_GROUP` variable's), all named with the prefix `cadence-`:
`cadence-logs` (workspace), `cadence-insights` (Application Insights), `cadence-alerts`
(action group), `cadence-health-<role>` (availability tests) and the alert rules below.

## Where to look

**Application Insights** (portal › resource group › `cadence-insights`). Filter by cloud
role (`cadence-api`, `pavillon46-api`) wherever a view offers it.

- **Failures**: failed requests and exceptions, grouped by operation and by exception
  type. Open a sample to see the whole request end to end, with its SQL calls and logs.
- **Availability**: the `/health` tests, location by location, with what each run got.
- **Transaction search**: find one request by its trace id (or use the KQL below).
- **Live metrics**: requests, failures and logs as they happen, while the app is sending.
- **Performance**: slow operations and slow dependencies.
- **Logs**: KQL over everything (snippets below).

**The web app** (portal › the App Service).

- **Log stream** (Monitoring › Log stream): the app's live output. On a Windows plan it
  shows application logs once App Service logs › Application logging (Filesystem) is on;
  that switch turns itself off after a while.
- **Health check**: how each instance answers `/health`.
- **Diagnose and solve problems**: restarts, crashes, high CPU or memory, failed starts.
- **Restart** (Overview): the first thing to try when the app hangs.

**The CRM itself.** Settings › System health shows the same checks as `/health`
(database, startup, jobs, outbox), readable by anyone who can open Settings, without
Azure access. `/health` and `/db-status` can also be opened in a browser.

**Outside Azure.** Stripe (Developers › Events, Webhooks) for payments; SendGrid
(Activity, Suppressions, and its status page) for email.

## Alerts and first steps

Alerts go to the action group `cadence-alerts`: an email to the tech owner (the address
given when the workflow is run), and a Teams channel when a webhook is set up. Most
alerts send a second message when they resolve (the daily-cap one does not: it fires
once).

Severity says how soon to look: **Sev 1** now, **Sev 2** today, **Sev 3** when you can.

| Alert | Who | First thing to do |
|---|---|---|
| Site down or API errors | tech owner | App Insights › Failures; find the trace id; restart the App Service if it hangs |
| Background job stopped | tech owner | check that job's last error in the logs; restart the app |
| Email stuck or failed | tech owner | check SendGrid status and the message's last error |
| Portal welcome failing | tech owner + club ops | check the portal address and secret in Welcome & portal and the Pavillon 46 app settings; then Send the welcome again |
| Payment webhook failed | tech owner + finance | open the event in Stripe; resend it |
| Database under strain | tech owner | check slow queries; scale the SQL tier if it persists |
| Daily digest | club ops | failed automations, bounces and new suppressions to review |

The alert rules behind each line:

| Alert rule | Fires when | Sev |
|---|---|---|
| `cadence-site-down-cadence-api`, `cadence-site-down-pavillon46-api` | `/health` fails (not 200, or no answer in 30 s, after a retry) from 2 or more of the 3 test locations | 1 |
| `cadence-api-5xx-burst` | an app returns more than 20 responses with 5xx in 5 minutes | 1 |
| `cadence-api-5xx-rate` | more than 2% of an app's requests return 5xx over 10 minutes, and at least 3 of them | 2 |
| `cadence-api-exceptions` | an app records more than 10 exceptions in 10 minutes | 2 |
| `Failure Anomalies - cadence-insights` | Application Insights' smart detection sees an unusual rise in failed requests or dependency calls | 3 |
| `cadence-monitoring-signal-error` | the CRM raises `job.silent` or `payments.failed` (logged at Error) | 1 |
| `cadence-monitoring-signal-warning` | the CRM raises `job.failing`, `outbox.backlog`, `outbox.failed`, `handoff.undelivered` or `portal.ping.failed` (logged at Warning) | 2 |
| `cadence-db-utilization` | the SQL database's DTU (or, on vCore, CPU) averages above 80% for 15 minutes | 2 |
| `cadence-db-failed-connections` | more than 5 connections to the database fail with system errors in 15 minutes | 2 |
| `cadence-db-storage` | the database is more than 85% full | 2 |
| `cadence-monitoring-daily-cap` | the workspace reached its daily cap, so nothing more is collected today | 2 |

The rules on requests and exceptions fire per cloud role, so the alert names the app.
The database rules exist only when the setup found the CRM's Azure SQL database.

### Site down

1. Open `https://<the app>/health` in a browser. A 503 names the check that is
   Unhealthy; no answer at all means the app is down or hanging.
2. Application Insights › Availability: which locations fail, and what each run got.
3. If a check is Unhealthy: **database**: `/db-status` and the database alerts; **startup**:
   start-up failed, see the startup log (KQL below); **jobs** or **outbox**: see the
   monitoring signals below.
4. If there is no answer or it times out: web app › Diagnose and solve problems, then
   **Restart**. If it started after a deploy, see [Deploy smoke test failed](#deploy-smoke-test-failed).
5. Pavillon 46 down: the CRM's portal welcomes will fail too (`handoff.undelivered`,
   `portal.ping.failed`).

### API server errors (5xx) and new exceptions

1. Application Insights › Failures, filtered to the app's cloud role, over the last hour.
   The top failing operation and top exception type are usually the whole story.
2. Open a sample: it shows the request, its exception and its SQL calls. Its operation
   id is the trace id; a user who saw an error may have it too ("Everything about one
   request" below).
3. If every request fails or hangs: **Restart** the web app.
4. If it began with a deploy, roll back by re-running the last good run of **Build and
   deploy to Azure Static Web Apps** (a re-run deploys that run's commit), then fix
   forward.
5. `Failure Anomalies` is Application Insights' learned version of the same: read it the
   same way. Its other smart detection rules (for example a rise in exception volume)
   email the subscription's owners; see Application Insights › Smart detection.

### Monitoring signals (the CRM's own alerts)

The CRM checks its jobs, email outbox, portal handoffs and payments itself and, when one
goes wrong, logs `monitoring.alert {Signal} {Count} {Detail}`, again every hour while it
stays wrong, and `monitoring.resolved {Signal}` when it clears. Each signal is its own
alert: it fires within about 5 minutes, stays open while the CRM keeps logging it, and
resolves when the CRM logs `monitoring.resolved` (or two hours after the last
`monitoring.alert`, if the app stopped without saying so).

The alert's dimensions name the **Signal** (and the cloud role). The **Detail** (which
job, which message, how many) is in the alert's query results ("View query results" in
the email), or run "monitoring.alert history" below.

| Signal | Sev | Who | First steps |
|---|---|---|---|
| `job.silent` | 1 | tech owner | A background job has not run when it should. Check that job's last error (KQL below), then restart the app. If jobs stop whenever the site is idle, check that Always On is on (see [DEPLOY.md](../DEPLOY.md)). |
| `job.failing` | 2 | tech owner | A job runs but keeps failing. Check that job's last error; a restart helps only if the error is about the app's state, not the data. |
| `outbox.backlog` | 2 | tech owner | Email is waiting to be sent. Check SendGrid's status page and that the API key is valid (the `SENDGRID_API_KEY` secret, applied as `Email__SmtpPassword`), then the message's last error. |
| `outbox.failed` | 2 | tech owner | Email failed for good. Check the message's last error, and SendGrid Activity and Suppressions for that address. |
| `handoff.undelivered` | 2 | tech owner + club ops | A member portal welcome did not reach Pavillon 46. Check the portal address and secret in Welcome & portal and the Pavillon 46 app settings, and that Pavillon 46 is up; then Send the welcome again. |
| `portal.ping.failed` | 2 | tech owner + club ops | The CRM could not reach the member portal. As for `handoff.undelivered`. |
| `payments.failed` | 1 | tech owner + finance | A Stripe webhook could not be processed. Open the event in Stripe (Developers › Events), fix the cause from the log, then resend it from Stripe. |

### Database under strain

1. SQL database › Query Performance Insight (or Intelligent Performance): the queries
   using the most DTU or CPU, and when they started.
2. Slow SQL calls from the app: the KQL below.
3. If it persists, scale the database up a tier (SQL database › Compute + storage). A
   Basic database is small; Standard S0 or S1 is the usual next step.
4. **Failed connections**: SQL database › Resource health; `/db-status`; the app log for
   connection errors. A changed password or firewall rule shows up as failed logins.
5. **Storage**: when the database is full, every write fails. Raise the maximum size or
   the tier (a Basic database cannot grow past 2 GB), or clear data the retention job
   should have removed.

### Monitoring data capped

The workspace collects at most the daily cap (1 GB unless set otherwise). Once reached,
nothing more is collected until the cap resets, so **the log alerts cannot fire** for the
rest of that day.

1. Find what is noisy: "What fills the workspace" below, in the workspace's Logs.
2. If it is a fault (a log line in a tight loop, a client retrying), fix that.
3. If it is real traffic, raise the cap: run the workflow again with a larger
   `daily_cap_gb`.

### Deploy smoke test failed

After **Deploy to Azure Web App**, the deploy job polls `/health` and `/db-status` for up
to 3 minutes and fails unless `/health` answers 200 and `/db-status` says `ok`, twice in
a row. It prints the last answers.

- The new version **is** deployed when this fails; the frontend deploy after it is
  skipped.
- `/health` names the Unhealthy check. `/db-status` `error` means start-up database work
  failed: the reason is in the log, not in `/db-status` (KQL "Start-up and migration"
  below).
- Fix forward, or re-run the last good deploy run to put the previous version back.

The Pavillon 46 backend's deploy workflow has its own smoke test, in its own repository.

### Daily digest

The CRM emails club ops a daily digest of failed automations, bounces and new
suppressions to review. It is the app's, not Azure's: nothing to set up here.

## Set up

### What it needs

- **The Azure login the deploy uses**: the `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`,
  `AZURE_SUBSCRIPTION_ID` and `AZURE_TENANT_ID` secrets, and `AZURE_WEBAPP_NAME`.
- **Permissions of that service principal**:
  - **Contributor** on the resource group that receives the monitoring resources (by
    default the CRM web app's), on the CRM web app's, and on the Pavillon 46 web app's
    resource group. It writes alert rules, the action group, the workspace, Application
    Insights, availability tests and diagnostic settings, and the web apps' settings.
    Contributor covers what Monitoring Contributor grants, so that role is not needed as
    well. If Pavillon 46 is in another resource group, add it:
    `az role assignment create --assignee <AZURE_CLIENT_ID> --role Contributor --scope /subscriptions/<subscription>/resourceGroups/<pavillon resource group>`.
    (A narrower option for Pavillon 46's group: Website Contributor plus Monitoring
    Contributor on that web app.)
  - At least **Reader** on the SQL server's resource group, if it is not one of those, so
    the database can be found and its metrics alerted on.
  - The resource providers `Microsoft.OperationalInsights`, `Microsoft.Insights` and
    `Microsoft.AlertsManagement` registered in the subscription. The setup registers them
    when it may; a service principal limited to a resource group may not, and the run
    then stops and says so. A subscription owner runs
    `az provider register --namespace <name>` once.
- **The apps' side**: the API code that sends to Application Insights, deployed. Until
  then the alert rules exist but the log-based ones see no data.

Optional: the secret `MONITORING_TEAMS_WEBHOOK_URL` ([Teams](#teams)); the variables
`PAVILLON_WEBAPP_NAME` (if not `pavillon46-api`) and `MONITORING_RESOURCE_GROUP` (to put
the monitoring resources elsewhere). The CRM API is tested at the address in
`API_BASE_URL` or `VITE_API_URL`, as the deploy uses, else at the web app's default host
name.

### Run the workflow

Actions › **Monitoring — set up Azure monitoring** › Run workflow.

| Input | Default | Meaning |
|---|---|---|
| `alert_email` | (required) | Who is emailed: the tech owner. Several addresses, comma-separated. |
| `include_pavillon` | ticked | Also monitor the Pavillon 46 backend. The run stops if that web app is not visible to the login. |
| `daily_cap_gb` | 1 | The workspace's daily ingestion cap, in whole GB. |
| `preview` | unticked | Show what would change (Azure's what-if) and change nothing. |

Run it with **preview** first, read the what-if, then run it again without.

What a run does:

1. Finds the CRM web app (and Pavillon 46's) the way the deploy does, the log
   categories each offers, and the CRM's SQL database (from the web app's connection
   string, or the only one in its resource group). It stops before changing anything if a
   web app is missing.
2. Deploys `infra/monitoring/main.bicep`: the workspace with its daily cap, Application
   Insights, the App Services' logs to the workspace, the availability tests, the action
   group and the alert rules.
3. On each web app, sets the health check path to `/health` and
   `APPLICATIONINSIGHTS_CONNECTION_STRING`, each only if it differs. **Each change
   restarts the app**, so the first run restarts each app (up to twice); run it at a
   quiet time. A later run with the same inputs restarts nothing.

Afterwards:

- Azure emails each address that it was added to the action group.
- Test the notifications: Monitor › Alerts › Action groups › `cadence-alerts` › Test.
- Check that both apps send, a few minutes after they restart ("Is each app sending?"
  below).

Run it again whenever the files in `infra/monitoring/` change, to change the alert email,
or after adding or removing the Teams secret. Without the Teams secret, a run removes a
Teams receiver that an earlier run added.

**From a computer instead of GitHub** (az CLI and jq):

```bash
az login
az account set --subscription <subscription id>
infra/monitoring/deploy.sh --crm-app <CRM web app> --alert-email you@example.com --what-if
infra/monitoring/deploy.sh --crm-app <CRM web app> --alert-email you@example.com
```

`infra/monitoring/deploy.sh --help` lists the options: another resource group, the SQL
server and database by name, no Pavillon 46, the daily cap, the public addresses.

### Teams

Alerts reach Teams through the action group's webhook receiver: Azure POSTs the alert to
the URL in `MONITORING_TEAMS_WEBHOOK_URL` (with the common alert schema). Teams has two
kinds of incoming URL:

- **Workflows** (recommended). In Teams: the channel › ⋯ › Workflows, and start from
  "Post to a channel when a webhook request is received". That template expects an
  Adaptive Card, which Azure does not send, so change the flow: after the trigger, add
  **Parse JSON** with the common alert schema, then **Post card in a chat or channel**
  showing `data.essentials.alertRule`, `severity`, `monitorCondition` (Fired or
  Resolved), `firedDateTime` and `description`. For the monitoring signals, the Signal
  is in `data.alertContext.condition.allOf[0].dimensions`. Copy the flow's URL into the
  secret. The common alert schema is on for this receiver (main.bicep,
  `teamsUseCommonAlertSchema`), which is what the parsing relies on.
- **Incoming Webhook connector** (an Office 365 connector, URL on
  `…webhook.office.com`). It takes only a message card with a `text`, and Azure's alert
  JSON is not one, so Teams is likely to refuse the POST. Microsoft is retiring these
  connectors in favour of Workflows; check its current notice before using one.

The URL carries its own signature: keep it in the secret, never in the repository. Test
it from the action group (Test › Webhook) after the run.

## KQL for common questions

Run these in Application Insights › Logs (the table names are Application Insights'
own). The workspace's Logs holds the same data as `AppRequests`, `AppTraces`,
`AppExceptions` and so on, plus the App Service tables and `Usage`.

**Everything about one request, by trace id.** Paste the id from the error. It may be a
W3C trace id (32 hex characters, Application Insights' `operation_Id`; from a full
`00-<trace id>-<span id>-01`, take the middle part) or ASP.NET's request id (like
`0HN7…:00000002`); both are found.

```kusto
let id = "<trace id>";
let operations = union requests, traces, exceptions
    | where timestamp > ago(7d)
    | where operation_Id == id
        or tostring(customDimensions.TraceId) == id
        or tostring(customDimensions.RequestId) == id
    | distinct operation_Id;
union requests, dependencies, exceptions, traces
| where timestamp > ago(7d)
| where operation_Id in (operations)
| project timestamp, itemType, cloud_RoleName, name, resultCode, severityLevel, message, outerMessage, problemId, operation_Id
| order by timestamp asc
```

**A job's last errors.** Use part of the job's class name, for example
`OutboxDispatcher`, `TaskReminder`, `ClubPayments`, `CampaignScheduler`,
`AutomationRunner` (a `job.silent` or `job.failing` Detail names the job).

```kusto
let job = "OutboxDispatcher";
union traces, exceptions
| where timestamp > ago(3d)
| where cloud_RoleName == "cadence-api" and severityLevel >= 2
| where tostring(customDimensions.SourceContext) contains job or message contains job or outerMessage contains job
| project timestamp, itemType, severityLevel, text = coalesce(message, outerMessage), type, SourceContext = tostring(customDimensions.SourceContext)
| order by timestamp desc
| take 50
```

**Browser errors** (sent by the CRM's frontend through the API):

```kusto
traces
| where timestamp > ago(7d)
| where message startswith "monitoring.browser.error"
| extend Error = tostring(customDimensions.Message), Page = tostring(customDimensions.Url)
| summarize Count = count(), Last = max(timestamp) by Error, Page
| order by Count desc
```

**monitoring.alert history** (and what is on now: the latest event per Signal is
`raised`):

```kusto
traces
| where timestamp > ago(30d)
| where message startswith "monitoring.alert" or message startswith "monitoring.resolved"
| extend Signal = tostring(customDimensions.Signal),
         Event = iff(message startswith "monitoring.alert", "raised", "resolved"),
         Count = tostring(customDimensions.Count),
         Detail = tostring(customDimensions.Detail)
| project timestamp, Signal, Event, Count, Detail, severityLevel
| order by timestamp desc
```

**5xx by endpoint:**

```kusto
requests
| where timestamp > ago(1h)
| where toint(resultCode) >= 500
| summarize Failed = count(), Example = take_any(operation_Id) by cloud_RoleName, name, resultCode
| order by Failed desc
```

**Slow SQL calls:**

```kusto
dependencies
| where timestamp > ago(1h)
| where type == "SQL"
| summarize Calls = count(), P95ms = percentile(duration, 95), MaxMs = max(duration) by cloud_RoleName, target, name
| order by P95ms desc
```

**Start-up and migration** (when `/db-status` says `error`):

```kusto
union traces, exceptions
| where timestamp > ago(1d)
| where cloud_RoleName == "cadence-api"
| where message has "Database" or message has "migration" or itemType == "exception"
| project timestamp, severityLevel, text = coalesce(message, outerMessage), type
| order by timestamp desc
| take 100
```

**Is each app sending?**

```kusto
union requests, traces, exceptions
| where timestamp > ago(1h)
| summarize Items = count(), Last = max(timestamp) by cloud_RoleName, itemType
```

**Availability over the last day:**

```kusto
availabilityResults
| where timestamp > ago(1d)
| summarize Runs = count(), Failed = countif(tostring(success) in ("0", "false", "False")) by name, location
```

**5xx as App Service saw them**, including when the app could not answer at all (in the
workspace's Logs):

```kusto
AppServiceHTTPLogs
| where TimeGenerated > ago(1h)
| where ScStatus >= 500
| summarize Count = count() by _ResourceId, CsUriStem, ScStatus
| order by Count desc
```

**What fills the workspace** (in the workspace's Logs; `Quantity` is in MB):

```kusto
Usage
| where TimeGenerated > ago(30d)
| where IsBillable == true
| summarize GB = round(sum(Quantity) / 1024, 3) by bin(TimeGenerated, 1d), DataType
| order by TimeGenerated desc, GB desc
```

## Tuning

Every threshold is a parameter at the top of
[`infra/monitoring/main.bicep`](../infra/monitoring/main.bicep). Change its default
there, commit, and run the workflow again. A change made in the portal lasts only until
the next run.

| Parameter | Default | Rule |
|---|---|---|
| `serverErrorPercent`, `serverErrorMinCount` | 2 (%), 3 | `cadence-api-5xx-rate`, over 10 minutes |
| `serverErrorBurstCount` | 20 | `cadence-api-5xx-burst`, in 5 minutes |
| `exceptionCount` | 10 | `cadence-api-exceptions`, in 10 minutes |
| `availabilityTestLocations` | West Europe, North Europe, France Central | the availability tests |
| `availabilityTestFrequency` | 300 (s) | 300, 600 or 900 |
| `availabilityFailedLocationCount` | 2 | the site-down alerts |
| `sqlUtilizationPercent` | 80 | `cadence-db-utilization`, average over 15 minutes |
| `sqlFailedConnections` | 5 | `cadence-db-failed-connections`, in 15 minutes |
| `sqlStoragePercent` | 85 | `cadence-db-storage` |
| `enableSmartDetection` | true | `Failure Anomalies` |
| `dailyCapGb` | 1 | the workspace's cap (the workflow's `daily_cap_gb` input) |
| `retentionInDays` | 30 | how long the workspace keeps data |

The queries are in [`infra/monitoring/queries/`](../infra/monitoring/queries/), and
severities and windows are on each rule in `main.bicep`.

- **Too noisy?** Raise the minimum counts before the percentages: on a quiet evening a
  handful of requests make any rate jumpy. For the site-down alerts, Microsoft suggests
  alerting at (locations − 2) failed locations with at least five locations; that costs
  more test runs (see Costs).
- **When a signal goes on** (how big a backlog, how long a job may be silent) is decided
  by the CRM, not here; tune it in the app.
- **For maintenance**, add an alert processing rule (Monitor › Alerts › Alert processing
  rules) that suppresses notifications for a time window. It is not part of the
  template, so a run leaves it alone.
- **Sev and recipients**: every rule uses the one action group. To send, say, payment
  alerts to finance as well, add an address to `alert_email`, or a second action group
  in `main.bicep` for that rule.

## Costs

What is billed, and how to keep it bounded. Prices change and differ by region: check
the [Azure Monitor pricing page](https://azure.microsoft.com/pricing/details/monitor/)
rather than trusting a number here.

- **Data ingested** into the workspace (Application Insights telemetry and the App
  Service logs) is billed per GB, after a free monthly allowance; confirm the allowance
  and the price per GB on the pricing page. Data is kept 30 days.
- **The daily cap** (`daily_cap_gb`, 1 GB by default) bounds it: once reached, nothing
  more is collected until the cap resets, and `cadence-monitoring-daily-cap` says so.
  Application Insights has its own daily cap as well (Application Insights › Usage and
  estimated costs); the workspace's is the one set here.
- **Sampling.** The Application Insights SDK in an ASP.NET Core app samples adaptively by
  default: under heavy traffic it keeps a share of the telemetry and counts it up
  (`itemCount`), so the Failures numbers stay right while less is stored. A small CRM is
  rarely sampled. Whoever changes sampling in the app must keep the `monitoring.*` traces
  out of it, or a signal can be dropped. Leave Application Insights' own ingestion
  sampling at 100%.
- **Availability tests** are billed per run. Runs a month = tests × locations × runs an
  hour × 24 × 30: with 2 tests, 3 locations and a run every 5 minutes, 2 × 3 × 12 × 720
  = 51,840. The Azure retail price list showed USD 0.000645 a run in West Europe in
  November 2026, about USD 33 a month; check it. Fewer locations or a 10- or 15-minute
  frequency cut it proportionally.
- **Alert rules**: log alert rules are billed monthly per time series they watch (here,
  per cloud role or per Signal), more for more frequent checks; metric alert rules much
  less. Expect a small monthly amount; the pricing page has the figures.
- See it in the workspace › Usage and estimated costs, and Cost Management for the
  resource group.

To collect less: drop the HTTP logs (remove `AppServiceHTTPLogs` from
`WANTED_CATEGORIES` in `infra/monitoring/deploy.sh`), keep the app's log level at
Information or above in production, and lower the availability tests' locations or
frequency.

## Pavillon 46

- Its web app gets the same setup: the connection string, the health check path, its
  console and HTTP logs, an availability test on `/health` and a site-down alert. The
  5xx and exception rules cover it by cloud role `pavillon46-api`.
- The app must read `APPLICATIONINSIGHTS_CONNECTION_STRING`, report the cloud role
  `pavillon46-api`, and answer `/health` with 200 (Healthy or Degraded) or 503.
- The monitoring signals (`monitoring.alert`) are the CRM's.
- Its deploy workflow has its own smoke test, in its own repository.

## Turning it off

- **Pause notifications**: an alert processing rule (above), or disable the action group
  `cadence-alerts`.
- **Remove it**: delete the `cadence-*` resources from the resource group (the alert
  rules, `cadence-alerts`, the `cadence-health-*` tests, `cadence-insights`,
  `cadence-logs`), the diagnostic setting `cadence-monitoring` on each web app, and the
  `APPLICATIONINSIGHTS_CONNECTION_STRING` setting (removing it restarts the app). Unset,
  the apps send nothing.

## Files

| File | What |
|---|---|
| [`infra/monitoring/main.bicep`](../infra/monitoring/main.bicep) | The resources and alert rules |
| [`infra/monitoring/modules/appServiceDiagnostics.bicep`](../infra/monitoring/modules/appServiceDiagnostics.bicep) | A web app's logs to the workspace |
| [`infra/monitoring/queries/`](../infra/monitoring/queries/) | The log alert queries |
| [`infra/monitoring/deploy.sh`](../infra/monitoring/deploy.sh) | Finds the apps and database, deploys, sets the apps' settings |
| [`.github/workflows/monitoring-setup.yml`](../.github/workflows/monitoring-setup.yml) | Runs `deploy.sh` from GitHub |
| [`.github/workflows/deploy-backend.yml`](../.github/workflows/deploy-backend.yml) | The health check path and the smoke test on each deploy |
