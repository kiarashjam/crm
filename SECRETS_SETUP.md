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
| Web App (API)  | B1, Always On | West Europe |

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

### Admin workflows (org copy, ensure member, lead status audit)

These run against the production database, and this repository's Actions logs are public.

- They run in a GitHub environment called **production**. GitHub creates it on the first run with no rules, so add them: **Settings → Environments → production → Required reviewers** (and, under **Deployment branches**, allow only `main`). Until then they run without approval.
- A reviewer gates the job, not the secrets. A **repository** secret can be read by any workflow file on any branch, and anyone who can push a branch can write one that leaves out `environment: production`. So the approval only holds once the credentials these workflows use are **environment** secrets of `production` and not repository secrets. The backend deploy uses the same `AZURE_*` service principal and does not run in that environment, so either give the admin workflows a service principal of their own as `production` environment secrets (and keep the deploy's elsewhere), or move the deploy into the environment too, in which case every deploy waits for a reviewer.
- `ADMIN_REPORT_PASSPHRASE` (secret): the lead status audit and the org copy's table-by-table report are encrypted with it before they are kept as the run's artifact, because a public repository's artifacts can be downloaded by anyone signed in to GitHub. The two workflows refuse to run without it. To read one, download the artifact, unzip it, and run `openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -in <file>.enc -out <file>`; it asks for the passphrase.

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
| From address | `info@pavillon46.ch` — and it must be **verified** in SendGrid (see below) |

### Verify the sender first (otherwise nothing sends)

SendGrid refuses mail from an unverified sender, so do this once before testing:

**SendGrid → Settings → Sender Authentication**, then either

- **Single Sender Verification** — quickest: add `info@pavillon46.ch`, click the link in the
  confirmation email SendGrid sends to it; or
- **Authenticate Your Domain** (`pavillon46.ch`) — more DNS work, but far better
  deliverability because the mail is then SPF/DKIM-signed and much less likely to land in spam.

A `403 Forbidden` from the relay almost always means the from-address is not verified.

### Azure deployment

Host, username and sender are already committed as defaults in `appsettings.json`
(`smtp.sendgrid.net` / `apikey` / `info@pavillon46.ch`), so in practice only the
**API key** and the **SPA URL** need setting.

Add them in **Azure Portal → Web Apps → \<your-backend\> → Configuration → Application
settings** (the `__` double underscore is how .NET maps `Email:SmtpHost` to an
environment variable):

| Name | Value | Required? |
|------|-------|-----------|
| `Email__SmtpPassword` | your SendGrid API key | **yes** — never committed |
| `Email__FrontendBaseUrl` | SPA origin, no trailing slash, e.g. `https://mango-moss-0804bb403.1.azurestaticapps.net` | **yes** in production (default points at localhost) |
| `Email__FromAddress` | overrides the committed default `info@pavillon46.ch` | only to change the sender |
| `Email__SmtpHost` | overrides `smtp.sendgrid.net` | only for a different relay |
| `Email__SmtpUser` | overrides `apikey` | only for a different relay |
| `Email__FromName` | overrides `Cadence` | optional |
| `Email__UseSsl` | `true` (default) | no |
| `Email__SmtpPort` | `587` (default) | no |

`Email__FrontendBaseUrl` matters: it builds the links in all three emails, and the
unsubscribe link at the foot of every sequence, automation and composer email. If it is
empty or not an absolute http(s) URL, password reset is skipped and logged, and outreach
goes out without its unsubscribe link (a warning is logged each time).

Workspace email (composer, assistant, sequences, automations) is sent from
`Email__FromAddress` — or the workspace's own address once its domain is verified, below —
under the workspace's name, with Reply-To set to the person behind it, so replies reach
that person rather than the shared sender inbox. `Email__FromName` names the product's own
messages (password reset, invitations, reminders, contracts), and workspace email only
when the workspace has no name.

### The outbox, replies, bounces and a workspace's own sender (optional)

Workspace email and outbound webhooks are queued in the database and sent by a background
job that retries for about two days (see `backend/README.md`, "Email and the outbox"). That
needs nothing new. Three more things can be switched on, each by settings alone; until
then everything works as before.

| Name | Value | What it switches on |
|------|-------|---------------------|
| `Email__VerifiedSendingDomains` | each domain SendGrid has authenticated with the workspace it belongs to, comma separated, e.g. `acme.ch=3f2b…-…, club.example=9a1c…-…` | A workspace whose own sending address (Settings → Organization → Email sender) is on a domain listed for that workspace sends from it. Add a domain only once SendGrid shows it as verified: the relay refuses mail from a domain it has not verified. The workspace id is required: anyone can sign up and save any address, so a domain listed on its own would let every workspace send as it, and is ignored. |
| `Email__InboundReplyDomain` | e.g. `reply.yourdomain.ch` | With `Email__InboundParseSecret`: every workspace email also names `reply+<token>@` this domain in Reply-To, so a reply reaches the person and comes back into the CRM. |
| `Email__InboundParseSecret` | a long random string, e.g. `openssl rand -hex 24` | Turns on `POST /api/public/email/inbound/<secret>`. |
| `Email__EventWebhookSecret` | a long random string | Turns on `POST /api/public/email/events/<secret>`. |
| `Email__EventWebhookVerificationKey` | the verification key SendGrid shows for a signed Event Webhook | Event posts must carry a valid signature (with the secret, or instead of it at `/api/public/email/events`). |

**Replies (SendGrid Inbound Parse).** Pick a subdomain nobody receives mail on, e.g.
`reply.yourdomain.ch`, and give it an MX record `mx.sendgrid.net` (priority 10). In
**SendGrid → Settings → Inbound Parse → Add Host & URL**, enter that domain and the URL
`https://<your-api>/api/public/email/inbound/<Email__InboundParseSecret>`. Leave "POST the raw,
full MIME message" either way; both are read. Then set the two settings above. A reply is
matched by its reply address or by the Message-ID it answers, goes on the record's
timeline, stops that person's sequences and notifies whoever works the record. An
automatic reply (an out-of-office, marked so in its headers) goes on the timeline only.
Mail that answers nothing the workspaces sent is dropped.

**Bounces and spam reports (SendGrid Event Webhook).** In **SendGrid → Settings → Mail
Settings → Event Webhook**, set the URL to
`https://<your-api>/api/public/email/events/<Email__EventWebhookSecret>`, select at least
Bounced, Dropped, Spam Reports and Unsubscribes (Delivered, Deferred and Opened are
recorded too), and enable it. For more protection, switch on **Signed Event Webhook** and
put its verification key in `Email__EventWebhookVerificationKey`. A bounce, a spam report or
an unsubscribe puts the address on that workspace's suppression list and stops the
sequences writing to it; the composer then refuses the address and says why.

**A workspace's own domain.** In **SendGrid → Settings → Sender Authentication →
Authenticate Your Domain**, add the workspace's domain and give its owner the DNS records
to publish. Once SendGrid shows it as verified, add `domain=<workspace id>` to
`Email__VerifiedSendingDomains`; the workspace's Settings page shows its id while its
address is not in use yet, and says which address is in use. Check that the domain really
belongs to that workspace's organization before listing it.

Both public endpoints answer 404 until their secret or key is set, and their secrets are
kept out of the request log.

**Club campaigns and member email.** They use the same relay and outbox; nothing new is
needed to send them. Three settings make them complete:

- `Email__ApiBaseUrl`: the API's public https origin, e.g. `https://<your-api>.azurewebsites.net`.
  A member's marketing email then carries a one-click unsubscribe (`List-Unsubscribe` with
  `List-Unsubscribe-Post`, which Gmail and Yahoo expect from bulk senders). Left at its
  localhost default, the header names the unsubscribe page instead, which still works but
  is not one-click.
- The Event Webhook above, with **Delivered** and **Opened** selected as well as the
  refusals: each member's message moves from sent to delivered, opened or bounced from
  those events, and the campaign totals follow.
- **SendGrid → Settings → Tracking → Open Tracking** on, for "opened" to mean anything.
  Without it messages stop at delivered.

Members have no consent recorded until someone records it (on the member's page, Consent
tab), so a marketing campaign reaches nobody at first; service campaigns are not affected.

Unsubscribe links are signed with ASP.NET Data Protection, the same key ring that keeps
two-factor secrets. On Azure App Service that ring is kept under `%HOME%` and survives
restarts and deploys; if the API ever runs where it is not persisted, old unsubscribe
links stop working (their page says the link is not valid).

### Option: let the deploy configure Azure for you (one GitHub secret)

Instead of typing the settings into the Azure Portal, add a single repository secret
and the backend deploy applies them on every run:

**GitHub → Settings → Secrets and variables → Actions → New repository secret**

| Secret | Value |
|--------|-------|
| `SENDGRID_API_KEY` | your SendGrid API key |

The `Configure email settings (SendGrid)` step in `.github/workflows/deploy-backend.yml`
then sets `Email__SmtpPassword` and `Email__FrontendBaseUrl` on the Web App, using the
Azure service principal the workflow already logs in with. Notes:

- The step is skipped entirely while the secret is absent, so nothing changes until you add it.
- It **merges** settings — only those two keys are touched, so the database connection
  string and every other setting are left alone.
- The SPA origin defaults to the current Static Web App URL. To point somewhere else, add a
  repository **variable** named `SPA_BASE_URL`.
- Re-deploy (or **Actions → Build and deploy to Azure Static Web Apps → Run workflow**) to apply it. Rotating
  the key later means updating the secret and re-running — nothing else.

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

## 5b. The privacy notice: who runs the app

The public privacy page (`/privacy`) and the terms print who runs the app from these
Application settings. Without them the page says the operator has not entered its details
yet; it never invents any. Each club enters its own details (the controller), its consent
mode and its retention periods on **Privacy requests -> Settings** in the app.

| Name | Value |
|------|-------|
| `Privacy__OperatorName` | The company that runs this deployment |
| `Privacy__OperatorAddress` | Its postal address (line breaks allowed) |
| `Privacy__OperatorEmail` | Where privacy questions about the app itself go |
| `Privacy__DpoContact` | Optional: a data protection officer or advisor, or an EU representative |
| `Privacy__Hosting` | Optional: where the app and database run. Defaults to Microsoft Azure, West Europe (the Netherlands); set it if the resources are elsewhere |

The page lists only the services actually configured: SendGrid when `Email__SmtpHost` is
SendGrid's, OpenAI (in the United States) when `OpenAI__ApiKey` is set, and for a club's own
page Stripe and Lightspeed once that club connected them. The session length it states is
`Jwt__ExpiryMinutes`.

---

## 5c. Sign in with Google or Microsoft, and with passkeys (optional)

All three are off until set. Nothing else changes: passwords keep working, and every way in
ends in the same session. Below, `<api>` is the API's public https origin (for example
`https://<your-api>.azurewebsites.net`) and `<app>` the SPA's (the `Email__FrontendBaseUrl`
value, for example `https://mango-moss-0804bb403.1.azurestaticapps.net` or your own domain).

### Both providers need these addresses

| Name | Value |
|------|-------|
| `Sso__ApiBaseUrl` | `<api>`, no trailing slash. Without it `Email__ApiBaseUrl` is used. Set one of them: left at its default (`http://localhost:5160`), the buttons still show but Google and Microsoft are sent a localhost callback and refuse the sign-in. |
| `Email__FrontendBaseUrl` | `<app>` (already set for the emails). The API sends people back to `<app>/auth/sso`. |

### Google

1. In the [Google Cloud console](https://console.cloud.google.com/), pick or create a project
   for Cadence. **APIs & Services → OAuth consent screen**: user type *External* (or
   *Internal* if only your Google Workspace's accounts should sign in), app name, support
   email, and the scopes `openid`, `.../auth/userinfo.email`, `.../auth/userinfo.profile`.
   Publish the app (status *In production*) so it is not limited to test users.
2. **APIs & Services → Credentials → Create credentials → OAuth client ID**, type *Web
   application*. Under **Authorized redirect URIs** add exactly
   `<api>/api/auth/sso/google/callback` (no JavaScript origins are needed: the browser never
   talks to Google's APIs itself).
3. Set on the Web App:

| Name | Value |
|------|-------|
| `Sso__Google__ClientId` | The client ID (`....apps.googleusercontent.com`) |
| `Sso__Google__ClientSecret` | The client secret |

Any Google account whose address Google has verified can then sign in as the Cadence account
with that address (or become a new account with no workspace until someone invites it).

### Microsoft Entra ID

1. In the [Microsoft Entra admin center](https://entra.microsoft.com/), **Applications → App
   registrations → New registration**: name *Cadence*; **Supported account types**: *Accounts
   in any organizational directory* (multitenant, matching the default
   `Sso__Microsoft__Tenant=organizations`), or *this organizational directory only* if only
   your own tenant may sign in (then set the tenant id below). **Redirect URI**: platform
   *Web*, value exactly `<api>/api/auth/sso/microsoft/callback`.
2. **Token configuration → Add optional claim → ID**: tick `email` and `xms_edov` (accept
   the prompt to add the Microsoft Graph `email` permission). Without `xms_edov` ("email
   domain owner verified") Cadence believes no Microsoft address and matches nobody by
   address, because a tenant's admin can type any address into a user's profile.
3. **API permissions**: Microsoft Graph delegated `openid`, `email`, `profile` (and the
   default `User.Read`). Grant admin consent if your tenant requires it.
4. **Certificates & secrets → New client secret**. Copy its **Value** (not the Secret ID) at
   once; note the expiry date and put a new one in before it lapses, or Microsoft sign-in
   stops (passwords keep working).
5. Set on the Web App:

| Name | Value |
|------|-------|
| `Sso__Microsoft__ClientId` | The **Application (client) ID** from the Overview page |
| `Sso__Microsoft__ClientSecret` | The secret's Value |
| `Sso__Microsoft__Tenant` | `organizations` (default: any work or school account), `common` (those and personal Microsoft accounts), `consumers` (personal only), or your **Directory (tenant) ID** to admit only your own directory |

### Passkeys

Passkeys need no outside account; they belong to one site, the **relying party**. With nothing
set, it is the host of `Email__FrontendBaseUrl` and its origin the only one allowed, which is
right when the app is served from one address.

| Name | Value |
|------|-------|
| `Passkeys__RelyingPartyId` | The app's host (`app.example.ch`), or a domain it is under (`example.ch`) so passkeys work on every subdomain. Never a domain every Azure static site shares (`azurestaticapps.net`, `1.azurestaticapps.net`): on the default address, use its full host. |
| `Passkeys__Origins` | The app's origins, comma-separated (`https://app.example.ch`); each must be the relying party or under it. https only (http only for localhost). |
| `Passkeys__RelyingPartyName` | Optional: the name passkey prompts show (default `Cadence`). |

Decide the relying party **before people add passkeys**: changing it later (for example
moving from the `*.azurestaticapps.net` address to your own domain) makes every passkey already
made stop working, and people add new ones in **Settings → Security**.

### What a workspace owner then does

Under **Settings → Team → Single sign-on** the owner can require the team to sign in with
Google or Microsoft, for the workspace's email domains (for example `pavillon46.ch`) or for
everyone. Members it holds who signed in another way are asked to sign in again with a provider
before they can work in that workspace; the owner is never held to it.

---

## 6. Quick check

- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN` added  
- [ ] `AZURE_WEBAPP_NAME` added  
- [ ] `AZURE_WEBAPP_PUBLISH_PROFILE` added (full XML)  
- [ ] `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_CLIENT_SECRET` added  
- [ ] `VITE_API_URL` added as **variable** (optional)
- [ ] `OpenAI__ApiKey` added (optional, for Intelligent Sales Writer)
- [ ] `Email__SmtpPassword` set in Azure **or** `SENDGRID_API_KEY` added as a GitHub secret, and `info@pavillon46.ch` verified in SendGrid
- [ ] `Email__FrontendBaseUrl` set to the deployed SPA origin
- [ ] `Privacy__OperatorName`, `Privacy__OperatorAddress` and `Privacy__OperatorEmail` set, so the privacy notice names who runs the app
- [ ] Optional: `Sso__ApiBaseUrl` plus `Sso__Google__*` and/or `Sso__Microsoft__*` set (section 5c) for Google or Microsoft sign-in; `Passkeys__RelyingPartyId` and `Passkeys__Origins` set before anyone adds a passkey

---

## 7. Trigger deploy

Push to `main` or run **Actions → Build and deploy to Azure Static Web Apps → Run workflow** (it runs the checks, then deploys the backend, then the frontend).

---

## 8. Legacy / other regions

- **East US 2:** `./scripts/azure-create.ps1 -SqlAdminPassword '...'`
- **Web App only** (existing RG/SQL): `./scripts/azure-create-webapp-only.ps1 -SqlAdminPassword '...' -SqlServerName "aci-sql-xxx" -WebAppName "aci-api-xxx"`
