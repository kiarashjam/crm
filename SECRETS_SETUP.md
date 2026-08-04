# GitHub Secrets & Variables Setup

Complete guide to connect GitHub Actions to Azure (West Europe, cheapest tiers).

---

## 1. Create Azure Resources (West Europe)

Run the script to create a new resource group with all resources in **West Europe** (cheapest pricing):

```powershell
# From repo root
./scripts/azure-create-west-europe.ps1 -SqlAdminPassword 'YourSecurePwd1!'
```

**With Service Principal** (recommended – creates GitHub-ready credentials):

```powershell
./scripts/azure-create-west-europe.ps1 -SqlAdminPassword 'YourSecurePwd1!' -CreateServicePrincipal
```

**Resources created:**

| Resource       | Tier   | Location   |
|----------------|--------|------------|
| Resource Group | -      | West Europe |
| Static Web App | Free   | West Europe |
| SQL Database   | Basic  | West Europe |
| Web App (API)  | F1 Free| West Europe |

**Prerequisites:** Azure CLI installed, logged in (`az login`).

---

## 2. Add GitHub Secrets

Go to **GitHub repo → Settings → Secrets and variables → Actions**.

### Required secrets

| Secret | Where to get it |
|--------|------------------|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Printed by the script; or **Azure Portal → Static Web Apps → \<your-swa\> → Manage deployment token** |
| `AZURE_WEBAPP_NAME` | Printed by the script (e.g. `aci-api-we-xxxxxx`); or **Azure Portal → Web Apps** |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | **Azure Portal → Web Apps → \<your-backend\> → Get publish profile** → copy entire XML |
| `AZURE_CLIENT_ID` | Service principal; printed if you used `-CreateServicePrincipal` |
| `AZURE_TENANT_ID` | Service principal; printed by script or `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | Printed by script or `az account show --query id -o tsv` |
| `AZURE_CLIENT_SECRET` | Service principal; printed only if you used `-CreateServicePrincipal` |

### Create Service Principal manually (if not using `-CreateServicePrincipal`)

```bash
# Replace rg-crm-aci-we with your resource group name
az ad sp create-for-rbac --name "github-aci-crm" --role Contributor --scopes "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/rg-crm-aci-we"
```

Copy `appId` → `AZURE_CLIENT_ID`, `password` → `AZURE_CLIENT_SECRET`, `tenant` → `AZURE_TENANT_ID`, and use your subscription ID for `AZURE_SUBSCRIPTION_ID`.

---

## 3. Add Variable (optional)

For the frontend to call the deployed backend:

| Variable | Value |
|----------|--------|
| `VITE_API_URL` | Backend URL, no trailing slash (e.g. `https://aci-api-we-xxxxxx.azurewebsites.net`) |

Add as **Variable** (not Secret): **Settings → Secrets and variables → Actions → Variables**.

---

## 4. OpenAI API Key (Optional - for Intelligent Sales Writer)

The Intelligent Sales Writer works with templates by default. To enable AI-powered generation:

### Local Development

**Option 1: User Secrets (recommended)**
```bash
cd backend/src/ACI.WebApi
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-key-here"
```

**Option 2: Environment Variable**
```powershell
$env:OpenAI__ApiKey = "sk-proj-your-key-here"
```

### Azure Deployment

Add to **Azure Portal → Web Apps → \<your-backend\> → Configuration → Application settings**:

| Name | Value |
|------|-------|
| `OpenAI__ApiKey` | Your OpenAI API key |
| `OpenAI__Model` | `gpt-4o-mini` (or `gpt-4o` for better quality) |

Or add as GitHub Secret and reference in deployment workflow.

---

## 5. Email / SendGrid (password reset + team invitations)

Transactional email is sent over SMTP, and SendGrid provides an SMTP relay — so no
extra code or package is needed, only configuration. Two features depend on it:

| Feature | What sends |
|---------|------------|
| **Login → "Forgot password?"** | the password-reset link (expires in 1 hour) |
| **Team → Invite member** | the invitation email telling someone to sign in and accept |
| **Task reminders** | a reminder once a task's reminder time passes — checked every 5 minutes in the background |

Task reminders go to the task's **assignee**, or its owner when unassigned, and respect
each person's **Settings → Notifications** toggles (`Email notifications` and
`Email me when a task is due`). Each reminder is sent at most once.

> If email is **not** configured, all three still behave safely: the reset endpoint returns
> a generic success without sending (and the token is not stored), an invitation is still
> created and visible in-app, and a reminder is still marked processed — only the
> notification email is skipped.

### Values to use with SendGrid

| Setting | Value |
|---------|-------|
| Host | `smtp.sendgrid.net` |
| Port | `587` |
| Username | the literal string `apikey` (**not** your email) |
| Password | your SendGrid API key |
| From address | `kia@bonapp.group` — and it must be **verified** in SendGrid (see below) |

### Verify the sender first (otherwise nothing sends)

SendGrid refuses mail from an unverified sender, so do this once before testing:

**SendGrid → Settings → Sender Authentication**, then either

- **Single Sender Verification** — quickest: add `kia@bonapp.group`, click the link in the
  confirmation email SendGrid sends to it; or
- **Authenticate Your Domain** (`bonapp.group`) — more DNS work, but far better
  deliverability because the mail is then SPF/DKIM-signed and much less likely to land in spam.

A `403 Forbidden` from the relay almost always means the from-address is not verified.

### Azure deployment

Host, username and sender are already committed as defaults in `appsettings.json`
(`smtp.sendgrid.net` / `apikey` / `kia@bonapp.group`), so in practice only the
**API key** and the **SPA URL** need setting.

Add them in **Azure Portal → Web Apps → \<your-backend\> → Configuration → Application
settings** (the `__` double underscore is how .NET maps `Email:SmtpHost` to an
environment variable):

| Name | Value | Required? |
|------|-------|-----------|
| `Email__SmtpPassword` | your SendGrid API key | **yes** — never committed |
| `Email__FrontendBaseUrl` | SPA origin, no trailing slash, e.g. `https://mango-moss-0804bb403.1.azurestaticapps.net` | **yes** in production (default points at localhost) |
| `Email__FromAddress` | overrides the committed default `kia@bonapp.group` | only to change the sender |
| `Email__SmtpHost` | overrides `smtp.sendgrid.net` | only for a different relay |
| `Email__SmtpUser` | overrides `apikey` | only for a different relay |
| `Email__FromName` | overrides `Cadence` | optional |
| `Email__UseSsl` | `true` (default) | no |
| `Email__SmtpPort` | `587` (default) | no |

`Email__FrontendBaseUrl` matters: it builds the links in all three emails. If it is empty
or not an absolute http(s) URL, password reset is skipped and logged.

### Local development

By default local development **does not send email**: `appsettings.Development.json`
blanks `Email:SmtpHost`, which makes the sender log the link instead. To test real
delivery locally, supply the key and re-enable the host:

```bash
cd backend/src/ACI.WebApi
dotnet user-secrets set "Email:SmtpHost" "smtp.sendgrid.net"
dotnet user-secrets set "Email:SmtpPassword" "<your-sendgrid-api-key>"
```

**Never commit the API key.** Keep it in Application settings / user-secrets only; this
repository is public. If a key is ever pasted into a chat, an issue, a screenshot or a
commit, treat it as compromised and rotate it in the SendGrid dashboard
(Settings → API Keys → delete and create a new one).

---

## 6. Quick check

- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN` added  
- [ ] `AZURE_WEBAPP_NAME` added  
- [ ] `AZURE_WEBAPP_PUBLISH_PROFILE` added (full XML)  
- [ ] `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_CLIENT_SECRET` added  
- [ ] `VITE_API_URL` added as **variable** (optional)
- [ ] `OpenAI__ApiKey` added (optional, for Intelligent Sales Writer)
- [ ] `Email__SmtpPassword` (SendGrid API key) added, and `kia@bonapp.group` verified in SendGrid
- [ ] `Email__FrontendBaseUrl` set to the deployed SPA origin

---

## 7. Trigger deploy

Push to `main` or run **Actions → Build and deploy backend to Azure Web App → Run workflow**.

---

## 8. Legacy / other regions

- **East US 2:** `./scripts/azure-create.ps1 -SqlAdminPassword '...'`
- **Web App only** (existing RG/SQL): `./scripts/azure-create-webapp-only.ps1 -SqlAdminPassword '...' -SqlServerName "aci-sql-xxx" -WebAppName "aci-api-xxx"`
