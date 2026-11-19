#!/usr/bin/env bash
# Sets up, or brings up to date, the Azure monitoring of the CadenceE CRM API and the
# Pavillon 46 backend. The runbook is docs/MONITORING.md.
#
# It deploys main.bicep (workspace, Application Insights, availability tests, action
# group, alert rules, the App Services' logs to the workspace), then does the two
# things the template does not, on each web app:
#   - APPLICATIONINSIGHTS_CONNECTION_STRING, which the apps read to send telemetry;
#   - the App Service health check path, /health.
# Both are written only when they differ from what is there, as each write restarts
# the app: the first run restarts each app (once per change), a later run nothing.
#
# Safe to run again: every resource has a fixed name and is updated in place. Alert
# rules changed by hand in the portal go back to what main.bicep says.
#
# Before changing anything it finds both web apps, the SQL database and the log
# categories each site offers, and stops if a web app is missing.
#
# Needs az (logged in: `az login`, or azure/login in CI) and jq.
#
# Usage:
#   infra/monitoring/deploy.sh --crm-app <web app> --alert-email <address> [options]
#
# Options (each can also come from the environment variable named in brackets):
#   --crm-app NAME           the CRM API web app (CRM_APP_NAME). Required.
#   --alert-email ADDRESS    who is emailed; comma-separated for several (ALERT_EMAIL). Required.
#   --pavillon-app NAME      the Pavillon 46 web app (PAVILLON_APP_NAME; default pavillon46-api)
#   --no-pavillon            leave Pavillon 46 out (INCLUDE_PAVILLON=false)
#   --resource-group NAME    where the monitoring resources go
#                            (MONITORING_RESOURCE_GROUP; default: the CRM web app's)
#   --teams-webhook-url URL  Teams incoming-webhook or Workflows URL (TEAMS_WEBHOOK_URL).
#                            Prefer the variable: arguments can be seen in `ps`.
#   --sql-server NAME        the CRM's SQL server, without .database.windows.net
#   --sql-database NAME      (SQL_SERVER_NAME, SQL_DATABASE_NAME; default: read from
#                            the CRM web app's connection string, or the only server and
#                            database in its resource group)
#   --no-sql                 no database alerts (SQL_ALERTS=false)
#   --daily-cap-gb N         the workspace's daily ingestion cap (DAILY_CAP_GB; default 1)
#   --crm-url URL            the CRM API's public https address, tested at /health
#                            (CRM_PUBLIC_URL; default https://<the app's default host name>)
#   --pavillon-url URL       the same for Pavillon 46 (PAVILLON_PUBLIC_URL)
#   --name-prefix PREFIX     prefix of the resource names (NAME_PREFIX; default cadence)
#   --what-if                show what would change and change nothing (WHAT_IF=true)
#   -h, --help
#
# In GitHub Actions the connection string, the alert email and the SQL server name are
# masked in the log.

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

CRM_APP_NAME="${CRM_APP_NAME:-}"
ALERT_EMAIL="${ALERT_EMAIL:-}"
PAVILLON_APP_NAME="${PAVILLON_APP_NAME:-pavillon46-api}"
INCLUDE_PAVILLON="${INCLUDE_PAVILLON:-true}"
RESOURCE_GROUP="${MONITORING_RESOURCE_GROUP:-}"
TEAMS_WEBHOOK_URL="${TEAMS_WEBHOOK_URL:-}"
SQL_SERVER_NAME="${SQL_SERVER_NAME:-}"
SQL_DATABASE_NAME="${SQL_DATABASE_NAME:-}"
SQL_ALERTS="${SQL_ALERTS:-true}"
DAILY_CAP_GB="${DAILY_CAP_GB:-1}"
CRM_PUBLIC_URL="${CRM_PUBLIC_URL:-}"
PAVILLON_PUBLIC_URL="${PAVILLON_PUBLIC_URL:-}"
NAME_PREFIX="${NAME_PREFIX:-cadence}"
WHAT_IF="${WHAT_IF:-false}"

HEALTH_PATH=/health
SETTING=APPLICATIONINSIGHTS_CONNECTION_STRING
# The log categories asked for; each site gets the ones it offers.
WANTED_CATEGORIES="AppServiceConsoleLogs AppServiceHTTPLogs"

# ---------------------------------------------------------------------------
# Output helpers (GitHub Actions annotations when running there)
# ---------------------------------------------------------------------------

in_actions() { [ "${GITHUB_ACTIONS:-}" = "true" ]; }
log()  { printf '%s\n' "$*"; }
warn() { if in_actions; then printf '::warning::%s\n' "$*"; else printf 'WARNING: %s\n' "$*" >&2; fi; }
fail() { if in_actions; then printf '::error::%s\n' "$*"; else printf 'ERROR: %s\n' "$*" >&2; fi; exit 1; }
mask() { if in_actions && [ -n "${1:-}" ]; then printf '::add-mask::%s\n' "$1"; fi; }
usage() { sed -n '2,/^$/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; }

is_true() {
  case "$(printf '%s' "${1:-}" | tr '[:upper:]' '[:lower:]')" in
    true|1|yes|y) return 0 ;;
    *) return 1 ;;
  esac
}

# ---------------------------------------------------------------------------
# Arguments
# ---------------------------------------------------------------------------

while [ $# -gt 0 ]; do
  case "$1" in
    --crm-app) CRM_APP_NAME="${2:?--crm-app needs a value}"; shift 2 ;;
    --alert-email) ALERT_EMAIL="${2:?--alert-email needs a value}"; shift 2 ;;
    --pavillon-app) PAVILLON_APP_NAME="${2:?--pavillon-app needs a value}"; shift 2 ;;
    --no-pavillon) INCLUDE_PAVILLON=false; shift ;;
    --resource-group) RESOURCE_GROUP="${2:?--resource-group needs a value}"; shift 2 ;;
    --teams-webhook-url) TEAMS_WEBHOOK_URL="${2:?--teams-webhook-url needs a value}"; shift 2 ;;
    --sql-server) SQL_SERVER_NAME="${2:?--sql-server needs a value}"; shift 2 ;;
    --sql-database) SQL_DATABASE_NAME="${2:?--sql-database needs a value}"; shift 2 ;;
    --no-sql) SQL_ALERTS=false; shift ;;
    --daily-cap-gb) DAILY_CAP_GB="${2:?--daily-cap-gb needs a value}"; shift 2 ;;
    --crm-url) CRM_PUBLIC_URL="${2:?--crm-url needs a value}"; shift 2 ;;
    --pavillon-url) PAVILLON_PUBLIC_URL="${2:?--pavillon-url needs a value}"; shift 2 ;;
    --name-prefix) NAME_PREFIX="${2:?--name-prefix needs a value}"; shift 2 ;;
    --what-if) WHAT_IF=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) usage >&2; fail "Unknown option: $1" ;;
  esac
done

if is_true "$INCLUDE_PAVILLON"; then INCLUDE_PAVILLON=true; else INCLUDE_PAVILLON=false; fi
if is_true "$SQL_ALERTS"; then SQL_ALERTS=true; else SQL_ALERTS=false; fi
if is_true "$WHAT_IF"; then WHAT_IF=true; else WHAT_IF=false; fi
DEPLOYMENT_NAME="${NAME_PREFIX}-monitoring"

command -v az >/dev/null 2>&1 || fail "The Azure CLI (az) is required: https://learn.microsoft.com/cli/azure/install-azure-cli"
command -v jq >/dev/null 2>&1 || fail "jq is required."

[ -n "$CRM_APP_NAME" ] || fail "The CRM web app name is required (--crm-app or CRM_APP_NAME)."
[ -n "$ALERT_EMAIL" ] || fail "An alert email is required (--alert-email or ALERT_EMAIL)."
mask "$ALERT_EMAIL"
IFS=',' read -r -a EMAILS <<<"$ALERT_EMAIL"
for email in "${EMAILS[@]}"; do
  email=$(printf '%s' "$email" | tr -d '[:space:]')
  case "$email" in
    ?*@?*.?*) mask "$email" ;;
    "") ;;
    *) fail "'$email' does not look like an email address." ;;
  esac
done
case "$DAILY_CAP_GB" in
  ''|*[!0-9]*) fail "The daily cap must be a whole number of GB (got '$DAILY_CAP_GB')." ;;
esac
[ "$DAILY_CAP_GB" -ge 1 ] || fail "The daily cap must be at least 1 GB."
if [ -n "$TEAMS_WEBHOOK_URL" ]; then
  mask "$TEAMS_WEBHOOK_URL"
  case "$TEAMS_WEBHOOK_URL" in
    https://*) ;;
    *) fail "The Teams webhook URL must start with https://." ;;
  esac
fi

az account show --query id -o none 2>/dev/null || fail "Not logged in to Azure: run 'az login' (in CI, azure/login)."

# ---------------------------------------------------------------------------
# Find the web apps (the way deploy-backend.yml does)
# ---------------------------------------------------------------------------

# Prints the resource group of the web app named $1, or nothing.
app_resource_group() {
  local rg
  rg=$(az webapp list --query "[?name=='$1'].resourceGroup | [0]" -o tsv)
  [ "$rg" = "None" ] && rg=""
  printf '%s' "$rg"
}

# Sets the variable named $1 to the origin (scheme and host) of $2 when it is an https
# address, else to https://$3 (the app's default host name).
set_origin() {
  local given="${2%/}" origin
  case "$given" in
    https://*) origin=$(printf '%s' "$given" | sed -E 's#^(https://[^/]+).*#\1#') ;;
    "") origin="https://$3" ;;
    *) warn "'$given' is not an https address; testing https://$3 instead."; origin="https://$3" ;;
  esac
  printf -v "$1" '%s' "$origin"
}

# Sets <PREFIX>_CATEGORIES (a JSON array) to the wanted log categories the site offers.
supported_categories() { # $1 = resource id, $2 = app name, $3 = variable prefix
  local available chosen=() category
  if available=$(az monitor diagnostic-settings categories list --resource "$1" \
                   --query "[?categoryType=='Logs'].name" -o tsv 2>/dev/null); then
    for category in $WANTED_CATEGORIES; do
      if printf '%s\n' "$available" | grep -qx "$category"; then
        chosen+=("$category")
      else
        log "Note: $2 does not offer the $category log category, so it is left out (App Service offers console logs on Linux plans; on Windows, see the Log stream)."
      fi
    done
  else
    warn "Could not list the log categories of $2; sending only AppServiceHTTPLogs."
    chosen=(AppServiceHTTPLogs)
  fi
  printf -v "${3}_CATEGORIES" '%s' "$(printf '%s\n' "${chosen[@]+"${chosen[@]}"}" | jq -R . | jq -cs 'map(select(length > 0))')"
}

log "Looking for the web apps ..."
CRM_RG=$(app_resource_group "$CRM_APP_NAME")
[ -n "$CRM_RG" ] || fail "No web app named '$CRM_APP_NAME' is visible to this login."
CRM_JSON=$(az webapp show --name "$CRM_APP_NAME" --resource-group "$CRM_RG" --query "{id: id, host: defaultHostName}" -o json)
CRM_ID=$(jq -r .id <<<"$CRM_JSON")
set_origin CRM_ORIGIN "$CRM_PUBLIC_URL" "$(jq -r .host <<<"$CRM_JSON")"
supported_categories "$CRM_ID" "$CRM_APP_NAME" CRM
log "CRM API: $CRM_APP_NAME in $CRM_RG, tested at $CRM_ORIGIN$HEALTH_PATH."

PAV_RG=""
PAV_ORIGIN=""
PAV_CATEGORIES="[]"
if [ "$INCLUDE_PAVILLON" = true ]; then
  PAV_RG=$(app_resource_group "$PAVILLON_APP_NAME")
  [ -n "$PAV_RG" ] || fail "No web app named '$PAVILLON_APP_NAME' is visible to this login. If it is in another resource group or subscription, give this login access to it (docs/MONITORING.md, Set up), or leave Pavillon 46 out (--no-pavillon; in the workflow, untick include_pavillon)."
  PAV_JSON=$(az webapp show --name "$PAVILLON_APP_NAME" --resource-group "$PAV_RG" --query "{id: id, host: defaultHostName}" -o json)
  PAV_ID=$(jq -r .id <<<"$PAV_JSON")
  set_origin PAV_ORIGIN "$PAVILLON_PUBLIC_URL" "$(jq -r .host <<<"$PAV_JSON")"
  supported_categories "$PAV_ID" "$PAVILLON_APP_NAME" PAV
  log "Pavillon 46: $PAVILLON_APP_NAME in $PAV_RG, tested at $PAV_ORIGIN$HEALTH_PATH."
else
  log "Pavillon 46 is left out."
fi

[ -n "$RESOURCE_GROUP" ] || RESOURCE_GROUP="$CRM_RG"
az group show --name "$RESOURCE_GROUP" --query id -o none 2>/dev/null \
  || fail "Resource group '$RESOURCE_GROUP' does not exist or is not visible to this login."
log "Monitoring resources go in $RESOURCE_GROUP."

# ---------------------------------------------------------------------------
# Find the CRM's SQL database (for the database alerts)
# ---------------------------------------------------------------------------

# Prints "<server host><TAB><database>" from the connection string $1. The string
# itself, which holds the password, is never printed.
parse_connection_string() {
  local parts part key value server="" database=""
  IFS=';' read -r -a parts <<<"$1"
  for part in "${parts[@]}"; do
    case "$part" in *=*) ;; *) continue ;; esac
    key=$(printf '%s' "${part%%=*}" | tr '[:upper:]' '[:lower:]' | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')
    value=$(printf '%s' "${part#*=}" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')
    case "$key" in
      server|"data source"|address|addr|"network address") server="$value" ;;
      "initial catalog"|database) database="$value" ;;
    esac
  done
  server="${server#tcp:}"
  server="${server%%,*}"
  printf '%s\t%s' "$server" "$database"
}

find_sql_from_app() {
  local conn host database
  conn=$(az webapp config connection-string list --name "$CRM_APP_NAME" --resource-group "$CRM_RG" -o json 2>/dev/null \
           | jq -r '[.[] | select(.name == "DefaultConnection")
                     | (if (.value | type) == "object" then .value.value else .value end)]
                    | first // empty' 2>/dev/null || true)
  if [ -z "$conn" ]; then
    conn=$(az webapp config appsettings list --name "$CRM_APP_NAME" --resource-group "$CRM_RG" \
             --query "[?name=='ConnectionStrings__DefaultConnection'].value | [0]" -o tsv 2>/dev/null || true)
  fi
  [ "$conn" = "None" ] && conn=""
  [ -n "$conn" ] || return 0
  mask "$conn"
  IFS=$'\t' read -r host database <<<"$(parse_connection_string "$conn")"
  case "$host" in
    *.database.windows.net)
      SQL_SERVER_NAME="${host%%.*}"
      SQL_DATABASE_NAME="$database"
      SQL_SOURCE="the CRM web app's connection string"
      ;;
  esac
}

find_only_sql_in_group() {
  local servers databases
  servers=$(az sql server list --resource-group "$CRM_RG" --query "[].name" -o tsv 2>/dev/null || true)
  [ "$(printf '%s' "$servers" | grep -c .)" = 1 ] || return 0
  databases=$(az sql db list --resource-group "$CRM_RG" --server "$servers" --query "[?name!='master'].name" -o tsv 2>/dev/null || true)
  [ "$(printf '%s' "$databases" | grep -c .)" = 1 ] || return 0
  SQL_SERVER_NAME="$servers"
  SQL_DATABASE_NAME="$databases"
  SQL_SOURCE="the only SQL server and database in $CRM_RG"
}

SQL_RG=""
SQL_METRIC=dtu_consumption_percent
SQL_SOURCE="the options given"
if [ "$SQL_ALERTS" = true ]; then
  if [ -z "$SQL_SERVER_NAME" ] && [ -z "$SQL_DATABASE_NAME" ]; then
    find_sql_from_app
    [ -n "$SQL_SERVER_NAME" ] || find_only_sql_in_group
  elif [ -z "$SQL_SERVER_NAME" ] || [ -z "$SQL_DATABASE_NAME" ]; then
    fail "Give both --sql-server and --sql-database, or neither."
  fi
fi
if [ "$SQL_ALERTS" = true ] && [ -n "$SQL_SERVER_NAME" ] && [ -n "$SQL_DATABASE_NAME" ]; then
  mask "$SQL_SERVER_NAME"
  SQL_RG=$(az sql server list --query "[?name=='$SQL_SERVER_NAME'].resourceGroup | [0]" -o tsv 2>/dev/null || true)
  [ "$SQL_RG" = "None" ] && SQL_RG=""
  SQL_DB_JSON=""
  if [ -n "$SQL_RG" ]; then
    SQL_DB_JSON=$(az sql db show --resource-group "$SQL_RG" --server "$SQL_SERVER_NAME" --name "$SQL_DATABASE_NAME" -o json 2>/dev/null || true)
  fi
  if [ -z "$SQL_DB_JSON" ]; then
    warn "The SQL database from $SQL_SOURCE is not visible to this login: no database alerts. Give --sql-server and --sql-database, or this login access to the server's resource group."
    SQL_SERVER_NAME=""; SQL_DATABASE_NAME=""; SQL_RG=""
  else
    SQL_TIER=$(jq -r '.sku.tier // .currentSku.tier // ""' <<<"$SQL_DB_JSON")
    SQL_POOL=$(jq -r '.elasticPoolId // ""' <<<"$SQL_DB_JSON")
    # DTU tiers report DTU use; vCore, serverless and pooled databases report CPU.
    SQL_METRIC=cpu_percent
    case "$SQL_TIER" in
      Basic|Standard|Premium) if [ -z "$SQL_POOL" ]; then SQL_METRIC=dtu_consumption_percent; fi ;;
    esac
    log "Database: $SQL_DATABASE_NAME on server $SQL_SERVER_NAME ($SQL_RG), tier ${SQL_TIER:-unknown}, from $SQL_SOURCE; alerting on $SQL_METRIC."
  fi
elif [ "$SQL_ALERTS" = true ]; then
  warn "No SQL database found (not in the CRM web app's connection string, nor a single one in $CRM_RG): no database alerts. Give --sql-server and --sql-database to add them."
else
  log "Database alerts are left out."
fi

# ---------------------------------------------------------------------------
# Resource providers (a new subscription may not have them registered)
# ---------------------------------------------------------------------------

for namespace in Microsoft.OperationalInsights Microsoft.Insights Microsoft.AlertsManagement; do
  state=$(az provider show --namespace "$namespace" --query registrationState -o tsv 2>/dev/null || echo unknown)
  case "$state" in
    Registered) ;;
    unknown) warn "Could not read whether $namespace is registered; carrying on." ;;
    *)
      if [ "$WHAT_IF" = true ]; then
        warn "$namespace is $state in this subscription; a real run registers it."
      else
        log "Registering the $namespace resource provider ..."
        az provider register --namespace "$namespace" --wait --output none 2>/dev/null \
          || fail "$namespace is not registered in this subscription and this login cannot register it. A subscription owner can: az provider register --namespace $namespace"
      fi
      ;;
  esac
done

# ---------------------------------------------------------------------------
# Deploy main.bicep
# ---------------------------------------------------------------------------

PARAMS_FILE=$(mktemp)
trap 'rm -f "$PARAMS_FILE"' EXIT
chmod 600 "$PARAMS_FILE"

jq -n \
  --arg namePrefix "$NAME_PREFIX" \
  --arg crmAppName "$CRM_APP_NAME" \
  --arg crmAppResourceGroup "$CRM_RG" \
  --arg crmHealthUrl "$CRM_ORIGIN$HEALTH_PATH" \
  --argjson crmDiagnosticCategories "$CRM_CATEGORIES" \
  --argjson includePavillon "$INCLUDE_PAVILLON" \
  --arg pavillonAppName "$PAVILLON_APP_NAME" \
  --arg pavillonAppResourceGroup "${PAV_RG:-$RESOURCE_GROUP}" \
  --arg pavillonHealthUrl "${PAV_ORIGIN:+$PAV_ORIGIN$HEALTH_PATH}" \
  --argjson pavillonDiagnosticCategories "$PAV_CATEGORIES" \
  --arg alertEmail "$ALERT_EMAIL" \
  --arg teamsWebhookUrl "$TEAMS_WEBHOOK_URL" \
  --argjson dailyCapGb "$DAILY_CAP_GB" \
  --arg sqlServerName "$SQL_SERVER_NAME" \
  --arg sqlDatabaseName "$SQL_DATABASE_NAME" \
  --arg sqlResourceGroup "${SQL_RG:-$RESOURCE_GROUP}" \
  --arg sqlUtilizationMetric "$SQL_METRIC" \
  '{
     "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
     contentVersion: "1.0.0.0",
     parameters: ($ARGS.named | with_entries(.value = {value: .value}))
   }' >"$PARAMS_FILE"

DEPLOY_ARGS=(
  --resource-group "$RESOURCE_GROUP"
  --name "$DEPLOYMENT_NAME"
  --template-file "$SCRIPT_DIR/main.bicep"
  --parameters "@$PARAMS_FILE"
  --only-show-errors
)

# Prints what would change in an app's settings and health check path.
preview_app() { # $1 = resource group, $2 = app name
  local has_setting path
  has_setting=$(az webapp config appsettings list --name "$2" --resource-group "$1" \
                  --query "length([?name=='$SETTING'])" -o tsv)
  path=$(az webapp config show --name "$2" --resource-group "$1" --query healthCheckPath -o tsv)
  [ "$path" = "None" ] && path=""
  if [ "$has_setting" = "0" ]; then
    log "  $2: $SETTING would be added (the app restarts)."
  else
    log "  $2: $SETTING would be set to this workspace's Application Insights if it points elsewhere (the app restarts)."
  fi
  if [ "$path" = "$HEALTH_PATH" ]; then
    log "  $2: the health check path is already $HEALTH_PATH."
  else
    log "  $2: the health check path would change from '${path:-not set}' to $HEALTH_PATH (the app restarts)."
  fi
}

if [ "$WHAT_IF" = true ]; then
  log ""
  log "What-if: the changes main.bicep would make (nothing is changed):"
  az deployment group what-if "${DEPLOY_ARGS[@]}"
  log ""
  log "And on the web apps:"
  preview_app "$CRM_RG" "$CRM_APP_NAME"
  if [ "$INCLUDE_PAVILLON" = true ]; then preview_app "$PAV_RG" "$PAVILLON_APP_NAME"; fi
  exit 0
fi

log "Deploying main.bicep as '$DEPLOYMENT_NAME' to $RESOURCE_GROUP ..."
if ! az deployment group create "${DEPLOY_ARGS[@]}" --output none; then
  # A workspace or Application Insights resource made seconds earlier can take a
  # minute before alert rules are allowed to query it.
  warn "The deployment failed (the error is above). Trying once more in 90 seconds, in case a resource it just made was not ready yet."
  sleep 90
  az deployment group create "${DEPLOY_ARGS[@]}" --output none \
    || fail "The deployment failed twice; the error is above."
fi

OUTPUTS=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" \
            --query properties.outputs -o json)
AI_ID=$(jq -r .appInsightsId.value <<<"$OUTPUTS")
AI_NAME=$(jq -r .appInsightsName.value <<<"$OUTPUTS")
WORKSPACE_NAME=$(jq -r .workspaceName.value <<<"$OUTPUTS")
ACTION_GROUP_NAME=$(jq -r .actionGroupName.value <<<"$OUTPUTS")

CONNECTION_STRING=$(az resource show --ids "$AI_ID" --query properties.ConnectionString -o tsv)
[ -n "$CONNECTION_STRING" ] && [ "$CONNECTION_STRING" != "None" ] \
  || fail "Could not read the connection string of $AI_NAME."
mask "$CONNECTION_STRING"

# ---------------------------------------------------------------------------
# The web apps: health check path and connection string, each only when different
# ---------------------------------------------------------------------------

configure_app() { # $1 = resource group, $2 = app name
  local path settings current agent
  path=$(az webapp config show --name "$2" --resource-group "$1" --query healthCheckPath -o tsv)
  [ "$path" = "None" ] && path=""
  if [ "$path" = "$HEALTH_PATH" ]; then
    log "$2: the health check path is already $HEALTH_PATH."
  else
    az webapp config set --name "$2" --resource-group "$1" \
      --generic-configurations "{\"healthCheckPath\": \"$HEALTH_PATH\"}" --output none
    log "$2: set the health check path to $HEALTH_PATH (it was '${path:-not set}'); the app restarts."
  fi

  # The settings hold secrets: they stay in this variable and are never printed.
  settings=$(az webapp config appsettings list --name "$2" --resource-group "$1" -o json)
  current=$(jq -r --arg n "$SETTING" '[.[] | select(.name == $n) | .value] | first // ""' <<<"$settings")
  if [ "$current" = "$CONNECTION_STRING" ]; then
    log "$2: $SETTING already points at $AI_NAME."
  else
    # appsettings set merges: only this key changes.
    az webapp config appsettings set --name "$2" --resource-group "$1" \
      --settings "$SETTING=$CONNECTION_STRING" --output none
    if [ -n "$current" ]; then
      log "$2: $SETTING now points at $AI_NAME (it pointed at another resource); the app restarts."
    else
      log "$2: set $SETTING to $AI_NAME; the app restarts."
    fi
  fi
  agent=$(jq -r '[.[] | select(.name == "ApplicationInsightsAgent_EXTENSION_VERSION") | .value] | first // ""' <<<"$settings")
  case "$agent" in
    ""|disabled) ;;
    *) warn "$2 also has the portal's Application Insights agent on (ApplicationInsightsAgent_EXTENSION_VERSION=$agent). The app sends its own telemetry now; see docs/MONITORING.md, Set up, before turning the agent off." ;;
  esac
}

configure_app "$CRM_RG" "$CRM_APP_NAME"
if [ "$INCLUDE_PAVILLON" = true ]; then configure_app "$PAV_RG" "$PAVILLON_APP_NAME"; fi

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

EMAIL_COUNT=$(printf '%s' "$ALERT_EMAIL" | tr ',' '\n' | grep -c '[^[:space:]]' || true)
TEAMS="no (no webhook URL given)"
if [ -n "$TEAMS_WEBHOOK_URL" ]; then TEAMS="yes"; fi
MONITORED="$CRM_APP_NAME (cadence-api)"
if [ "$INCLUDE_PAVILLON" = true ]; then MONITORED="$MONITORED, $PAVILLON_APP_NAME (pavillon46-api)"; fi
DATABASE="no database alerts"
if [ -n "$SQL_RG" ]; then DATABASE="alerts on $SQL_METRIC, failed connections and storage"; fi

log ""
log "Monitoring is set up in $RESOURCE_GROUP:"
log "  Application Insights: $AI_NAME (workspace $WORKSPACE_NAME, daily cap $DAILY_CAP_GB GB)"
log "  Monitored:            $MONITORED"
log "  Action group:         $ACTION_GROUP_NAME, $EMAIL_COUNT email address(es); Teams: $TEAMS"
log "  Database:             $DATABASE"
log "Azure emails each address to say it was added to the action group. The runbook is docs/MONITORING.md."

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "## Monitoring set up"
    echo ""
    echo "| | |"
    echo "|---|---|"
    echo "| Resource group | $RESOURCE_GROUP |"
    echo "| Application Insights | $AI_NAME |"
    echo "| Workspace | $WORKSPACE_NAME, daily cap $DAILY_CAP_GB GB |"
    echo "| Monitored | $MONITORED |"
    echo "| Action group | $ACTION_GROUP_NAME: $EMAIL_COUNT email address(es); Teams: $TEAMS |"
    echo "| Database | $DATABASE |"
    echo ""
    echo "Runbook: docs/MONITORING.md"
  } >>"$GITHUB_STEP_SUMMARY"
fi
