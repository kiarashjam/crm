# Cadence Backend

ASP.NET Core 8 Web API with Clean Architecture. SQL Server + Entity Framework Core 8.

---

## Quick Start (5 minutes)

### 1. Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server or LocalDB (`(localdb)\mssqllocaldb`)

### 2. Configure Database
Edit `src/ACI.WebApi/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CadenceCRM;Trusted_Connection=True;"
  }
}
```

### 3. Run
```bash
cd backend
dotnet run --project src/ACI.WebApi
```

**That's it!** Database migrations run automatically on first start.

- **API**: https://localhost:7xxx  
- **Swagger UI**: https://localhost:7xxx/swagger

---

## Common Tasks

### Create a New Migration
```bash
cd backend
dotnet ef migrations add YourMigrationName \
  --project src/ACI.Infrastructure \
  --startup-project src/ACI.WebApi
```

### Apply Migrations Manually
```bash
dotnet ef database update \
  --project src/ACI.Infrastructure \
  --startup-project src/ACI.WebApi
```

### Run Tests
```bash
cd backend
dotnet test
```

### Build for Production
```bash
dotnet publish src/ACI.WebApi -c Release -o ./publish
```

---

## API Authentication

All endpoints (except `/api/auth/*`) require a JWT token.

### Register a User
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "YourPassword123!",
  "name": "Test User"
}
```

### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "YourPassword123!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "userId": "abc123..."
}
```

### Use the Token
Add to all subsequent requests:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### Multi-Tenant Organization
For org-scoped data, include:
```
X-Organization-Id: your-org-guid
```

### Editing a lead, deal, contact or company

Every read of one of these returns its `version`, and `GET /api/{records}/{id}` and the
`PUT` answer carry it as the `ETag` too. Every save gives the record a new version.

- **An edit made from an older read is refused.** Send the version the edit was made from,
  as `version` in the body or as `If-Match: "<version>"`. When anyone has saved the record
  since, the `PUT` answers `409` with `errorCode` `Lead.Changed.Conflict` (or `Deal.`,
  `Contact.`, `Company.`) and a sentence telling the person to reload, and nothing is
  written. The edit forms send it. Leave it out for a change to a field that reads nothing
  else, such as moving a deal to another stage: that change is applied to the record as it
  is now. Two saves made from the same read never both win; the second is refused even
  without a version. A save that loses that race where no service words it answers `409`
  with `errorCode` `Record.Changed.Conflict`.
- **A field sent as `null` is cleared; a field left out is kept** (JSON merge-patch
  semantics): a contact's `companyId`, `phone`, `jobTitle`, `preferredContactMethod` and
  `description`; a deal's `companyId`, `contactId`, `assigneeId`, `expectedCloseDateUtc`,
  `probability` and `description`; a company's fields other than `name` (an empty string
  clears them too); a lead's `companyId`, `phone`, `description`, `lifecycleStage`,
  `jobTitle`, `country`, `industry` and `companySize`; and a club member's `phone` and
  `notes` (`PUT /api/members/{id}`). A `null` for a required field is ignored.
- **`pipelineState` on `PUT /api/leads/{id}` is a change, not the whole pipeline.** It is a
  JSON object merged into the stored pipeline: the steps sent replace the stored ones, a
  step sent as `null` is removed, and the rest are kept. An empty string clears the whole
  pipeline; anything but a JSON object is refused with 400.

---

## API Endpoints Reference

### Core CRM

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/contacts` | One page of contacts: `status` (active, archived, all), `companyId`, `hasCompany`, `activity` (recent, quiet), `sortBy`, `sortDir`, `search`, applied to the whole workspace |
| `GET` | `/api/contacts/stats` | The figures above the list, over the whole workspace |
| `GET` | `/api/contacts/export` | Every contact the list shows with the same parameters, as CSV (up to 50,000) |
| `GET` | `/api/contacts/search?q=john` | Search contacts |
| `GET` | `/api/contacts/{id}` | Get single contact |
| `GET` | `/api/contacts/{id}/lookalikes` | Contacts that look like this one (the duplicate rules), less those marked not the same |
| `POST` | `/api/contacts` | Create contact |
| `PUT` | `/api/contacts/{id}` | Update contact (see [Editing](#editing-a-lead-deal-contact-or-company)) |
| `DELETE` | `/api/contacts/{id}` | Delete contact (a lead it was made for does not get another) |

### Companies

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/companies` | One page of companies with their counts: `industry`, `hasIndustry`, `size`, `sortBy` (name, createdAt, contacts, deals), `sortDir`, `search` |
| `GET` | `/api/companies/summary` | The figures above the list, and the industries in use |
| `GET` | `/api/companies/export` | Every company the list shows, as CSV |
| `GET` | `/api/companies/{id}` | Get company |
| `POST` | `/api/companies` | Create company |
| `PUT` | `/api/companies/{id}` | Update company (see [Editing](#editing-a-lead-deal-contact-or-company)) |

### Duplicates

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/duplicates/contacts` | Duplicate contacts: same email, same phone however written, or same name when phone and company do not disagree |
| `GET` | `/api/duplicates/companies` | Duplicate companies: same web domain, or same name when the domains do not disagree |
| `GET` | `/api/duplicates/{kind}/not-same` | Pairs the team said are not the same (contacts, companies, leads, deals, members, households) |
| `POST` | `/api/duplicates/{kind}/not-same` | `{ recordIds }`: keep every pair among them apart, for the whole workspace |
| `POST` | `/api/duplicates/{kind}/not-same/clear` | `{ recordIds }`: offer them again |

### Deals

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/deals` | List deals |
| `GET` | `/api/deals/search?q=` | Search deals |
| `GET` | `/api/deals/{id}` | Get deal (with time in stage and whether it is stale) |
| `GET` | `/api/deals/board?pipelineId=&take=&sort=&search=&mine=&assigneeId=&unassigned=&minAmount=&maxAmount=&closeFrom=&closeTo=&staleOnly=` | The board: a pipeline's stages with the first cards of each, per-stage counts, money per currency, weighted value and stale deals, and the pipeline's figures |
| `GET` | `/api/deals/board/column?column=&skip=` (+ the board's filters) | The next cards of one board column |
| `GET` | `/api/deals/{id}/stage-history` | The stages a deal has been in, newest first |
| `POST` | `/api/deals` | Create deal |
| `PUT` | `/api/deals/{id}` | Update deal (see [Editing](#editing-a-lead-deal-contact-or-company)) |
| `DELETE` | `/api/deals/{id}` | Delete deal |

### Leads

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/leads` | List leads |
| `GET` | `/api/leads/search?q=` | Search leads |
| `POST` | `/api/leads` | Create lead |
| `PUT` | `/api/leads/{id}` | Update lead; `pipelineState` is merged (see [Editing](#editing-a-lead-deal-contact-or-company)) |
| `DELETE` | `/api/leads/{id}` | Delete lead |
| `POST` | `/api/leads/{id}/convert` | Convert to Contact/Company/Deal |

### Imports (leads, contacts, companies)

The file travels with each request, base64-encoded, up to 5 MB and 2,000 rows.
An import still running cannot be undone: both undo calls answer 409 until it has finished.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/imports/analyze` | Read a file: separator, columns, a suggested field for each |
| `POST` | `/api/imports/preview` | Dry run: what each row would do; nothing is written |
| `POST` | `/api/imports` | Import the file as one batch |
| `GET` | `/api/imports?kind=lead` | The workspace's latest imports |
| `GET` | `/api/imports/{id}/undo` | What undoing would remove, and what it keeps and why |
| `POST` | `/api/imports/{id}/undo` | Remove what the import created that nobody has touched since |

### Tasks

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/tasks` | List all tasks |
| `GET` | `/api/tasks?overdueOnly=true` | List overdue tasks only |
| `GET` | `/api/tasks/{id}` | Get task |
| `POST` | `/api/tasks` | Create task |
| `PUT` | `/api/tasks/{id}` | Update task |

### Activities

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/activities` | List activities |
| `GET` | `/api/activities/contact/{id}` | Activities for a contact |
| `GET` | `/api/activities/deal/{id}` | Activities for a deal |
| `POST` | `/api/activities` | Log an activity |

### Email and the outbox

Every email and webhook event a workspace sends goes through one outbox
(`OutboundMessages`, `OutboxService`): the composer, the sales assistant, sequence steps,
automation emails and outbound webhooks. A background dispatcher sends it, retrying what
did not go after 1, 5 and 15 minutes, then 1, 3, 6, 12 and 24 hours. Each email is checked
against the suppression list (do not contact, unsubscribes, and addresses the mail service
reported as bounced or complaining) when it is queued and again when it goes, has its
placeholders filled (`{{firstName}}`, `{{lastName}}`, `{{name}}`, `{{email}}`, `{{company}}`,
`{{senderName}}`, `{{workspace}}`, each with an optional `{{name|fallback}}`), carries its
own Message-ID, and is written once on the record's timeline when it has gone. The lead,
contact or deal an email is about must be one of the workspace's that the sender can open;
an email whose lead or contact is deleted while it waits is cancelled, not sent.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/email/send` | Send from the composer (or `source: "assistant"`): 200 `sent` when the relay took it, 202 `queued` when it did not yet and the outbox keeps trying |
| `POST` | `/api/email/{id}/cancel` | Take back a queued email before it goes (whoever sent it, or a manager); 409 once it has gone |
| `GET` | `/api/organizations/{id}/email` | Who the workspace's email goes out from: its own address, whether its domain is verified, the shared address, whether replies come back |
| `PUT` | `/api/organizations/{id}/email` | `{ sendingAddress }`: set or clear the workspace's own sending address (owner or manager) |
| `GET` | `/api/organizations/{id}/email/suppressions` | Addresses the workspace does not email because the mail service reported them (bounced, invalid, spam report, unsubscribed) |
| `DELETE` | `/api/organizations/{id}/email/suppressions/{suppressionId}` | Take an address off that list so the workspace may email it again (owner or manager) |
| `GET` | `/api/webhook/organizations/{id}/deliveries` | The latest outbound webhook deliveries and what the receiver answered (owner or manager) |
| `POST` | `/api/public/email/inbound/{secret}` | SendGrid Inbound Parse: a reply, matched to its message by reply+token address or In-Reply-To/References, written on the timeline; the person's sequences stop (not for an automatic reply such as an out-of-office). 404 without `Email:InboundParseSecret` |
| `POST` | `/api/public/email/events/{secret}` | SendGrid Event Webhook (or `/events` with a signature checked against `Email:EventWebhookVerificationKey`): bounces, drops, spam reports and unsubscribes suppress the address and stop sequences; deliveries and opens are recorded |

### Club campaigns, member messages and consent

Every email to a club member goes through the same outbox, one message per member, and
each says what came of it: `queued`, `sent`, `delivered`, `opened` (where open tracking is
on), `bounced`, or not sent and why (`suppressed`: no consent to marketing, an unsubscribe,
do not contact; `skipped`: no usable address, or another member at the same address gets
this campaign, the one who joined first). A message says what it is for. **Marketing** goes only to members whose
latest answer for marketing by email is yes and carries an unsubscribe link (with
`List-Unsubscribe` and `List-Unsubscribe-Post` headers); **service** (statements,
bookings, their application) goes whatever they said about marketing.

A campaign is sent once. Sending, whether someone pressed Send or the scheduler found it
due (it looks every 30 seconds), starts with one conditional update from Draft or
Scheduled to Sending, so a double click, a retry or two instances send it once; from then
on it cannot be edited, rescheduled, sent again or deleted. A sending that stops part-way
is taken up again once its claim runs out (10 minutes), and the unique index on
(campaign, member) means nobody is emailed twice. "Send a test to me" emails the signed-in
person alone, marked as a test, and counts in none of the figures. SMS and push are
refused: there is no provider behind them.

Consent is kept per channel (`email`, `sms`, `post`) and purpose (`marketing`, `service`)
with its source (`form`, `import`, `staff`, `member`, `unsubscribe`), who recorded it and
when; every answer is kept and the latest stands. The rows name their subject
(`SubjectType`, `SubjectId`) so leads can carry consent later, and export and erasure can
find everything about one person. Merging two members moves the duplicate's consent and
messages to the kept member.

Reading needs a workspace member; writing needs `club.members` (and `records.delete` to
delete).

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/campaigns` | Campaigns, newest first, each with its totals |
| `GET` | `/api/campaigns/audience?purpose=&tiers=&statuses=` | Who a campaign for that segment would reach now: matching, with an address, agreed to marketing, emails it makes |
| `GET` | `/api/campaigns/{id}/recipients` | Every member it was for and where their message stands |
| `POST` | `/api/campaigns` | A draft, or a scheduled campaign with `scheduledAtUtc` |
| `PUT` | `/api/campaigns/{id}` | Change a draft or scheduled one (merge-patch; `scheduledAtUtc: null` unschedules); 409 once it has started sending or changed meanwhile |
| `DELETE` | `/api/campaigns/{id}` | Delete a draft or scheduled one; 409 once it has started sending |
| `POST` | `/api/campaigns/{id}/send` | Send it now, once (`If-Match` with its version); 409 when it is being sent or has been |
| `POST` | `/api/campaigns/{id}/test` | A copy marked as a test to the signed-in person |
| `GET` | `/api/communications?memberId=` | The communications log, the club's or one member's |
| `POST` | `/api/communications/send` | Email one member: `{ memberId, category, purpose, subject, body }`; a refusal comes back as a `suppressed` message with its reason |
| `POST` | `/api/communications` | Log a message sent another way (a text, a letter); it reads `logged` |
| `DELETE` | `/api/communications/{id}` | Delete a logged message; what was sent, or refused, is kept (409) |
| `GET` | `/api/members/{id}/consent` | Where each channel and purpose stands, and the history |
| `POST` | `/api/members/{id}/consent` | Record an answer: `{ channel, purpose, granted, source, detail }` (`form`, `member` or `staff`) |
| `GET`/`POST` | `/api/public/unsubscribe/{token}` | The unsubscribe page's read and its button; a member's link withdraws marketing by email (source `unsubscribe`) for every member at that address, and marketing already queued to them is stopped as it goes; their service messages still come |
| `POST` | `/api/public/unsubscribe/{token}/one-click` | RFC 8058 one-click, from a mail app's own Unsubscribe button; a `GET` there goes to the page instead and changes nothing |
Every outbound webhook with a secret is signed twice: `X-Cadence-Signature` (hex HMAC-SHA256
of the body, as it always was) and `X-Cadence-Timestamp` (Unix seconds) with
`X-Cadence-Signature-256: sha256=<hex HMAC-SHA256 of "{timestamp}.{body}">`, which lets a
receiver refuse a post replayed later.

### Welcome & portal (onboarding handoff)

When a lead becomes a deal, the deal's contact can be handed to the workspace's own member
portal (Pavillon 46's, or any system that takes the contract below). The portal opens their
account, sends the welcome email with a link to set a password, and reports each step back,
which the deal's timeline shows. CadenceE never sends the welcome email itself. With
"add to club members" on, the person is also added to the club's members here, with their
start date or "to be decided".

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/onboarding/settings` | The portal address, whether there is a signing secret (never the secret), the welcome template and defaults |
| `PUT` | `/api/onboarding/settings` | `{ webhookUrl, enabled, emailSubject, emailHtml, defaultLanguage, addToClubMembers, senderName, senderEmail, replyTo }` (Manage settings). The address must be public https |
| `POST` | `/api/onboarding/settings/secret` | Makes a new signing secret and returns it this once (Manage settings) |
| `POST` | `/api/onboarding/settings/test` | Posts a signed `onboarding.ping` now and returns what the portal answered, with who it sends from when it says (`{ from: { name, email } }`) (Manage settings) |
| `POST` | `/api/onboarding/preview` | The welcome email filled for a deal's contact (or a name and email), the portal's links as stand-ins; lists placeholders nothing fills |
| `POST` | `/api/deals/{dealId}/onboarding` | Hands the deal's contact over: `{ welcomeMessage, startsOnUtc \| startUndetermined, language, emailSubject?, emailHtml?, addToClubMembers?, leadId? }`. One per deal (409 after) |
| `GET` | `/api/deals/{dealId}/onboarding` | The deal's handoffs with their steps and delivery state |
| `POST` | `/api/onboarding/{id}/resend` | Hands it over again (after a failure, or to send the welcome once more) |
| `POST` | `/api/public/onboarding/{id}/events` | The portal's reports, `Authorization: Bearer <callback.token>`: `{ event, accountId?, message?, occurredAtUtc? }` with `account_created`, `account_existing`, `email_sent`, `email_failed`, `password_set` or `failed`. A repeat changes nothing |

The handoff is a `deal.onboarding` webhook through the outbox (retried like any other),
posted to the portal's address, signed with its secret as above. Its `payload`:
`{ version: 1, onboardingId, deal { id, name, amount, currency, stage }, contact { id, name,
firstName, lastName, email, phone, company }, membership { startsOn: "yyyy-MM-dd" | null,
startUndetermined, clubMemberId }, language, welcomeMessage, email { subject, html } | null,
callback { url, token }, sentBy { name }, workspace { id, name }, from { name, email, replyTo } }`. In `email`, CadenceE's
placeholders (`{{firstName}}`, `{{lastName}}`, `{{name}}`, `{{email}}`, `{{company}}`,
`{{dealName}}`, `{{welcomeMessage}}`, `{{startDate}}`, `{{senderName}}`, `{{workspace}}`) are
filled and HTML-encoded; `{{setPasswordUrl}}`, `{{portalUrl}}` and `{{loginEmail}}` are left
for the portal. The callback token is derived from the secret, so making a new secret
retires the tokens of earlier handoffs. A copied organization keeps the template but not the
portal address or secret.

The welcome is from the organization: `from.name` is the sender name set in Settings (else the
organization's name), `from.replyTo` the reply-to set there (else the organization's sending
address), and `from.email` a from address, if one is set. The portal sends through its own
mail service, so it uses that address only on a domain it may send for (Pavillon 46: the domain
of its `FROM_EMAIL`), and otherwise its own address under the organization's name, with replies
to the organization. Its `email_sent` report says which address it used, on the deal's timeline.

Images for the welcome email are kept by the workspace and served at public addresses,
because mail apps show only images they can fetch (most block embedded `data:` images, and
Pavillon 46 keeps only https ones):

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/email-images` | The workspace's images, newest first, each with its public `url` |
| `POST` | `/api/email-images` | Multipart `file` (and optionally `width`, `height` in pixels): PNG, JPEG, GIF or WebP, read from its bytes, up to 3 MB; 300 per workspace (Edit records) |
| `DELETE` | `/api/email-images/{id}` | Removes one; emails already sent with it show it no more (Edit records) |
| `GET` | `/api/public/email-images/{id}.{ext}` | The image, for any mail app: anonymous, cached for a year, `nosniff` |

The editor in Settings › Welcome & portal (and in the convert dialog's own-email box) adds an
image at the cursor, takes one pasted or dropped on the HTML, hosts embedded images in one
click, and flags images that point at a computer or plain http.

`PortalHandoffEndToEndTests` runs the whole round trip against a real portal when
`CADENCE_E2E_PORTAL` (its handoff address, e.g. a local Pavillon 46 in Development) and
`CADENCE_E2E_PORTAL_SECRET` (its `CADENCE_ONBOARDING_SECRET`) are set; otherwise it is skipped.

### Organizations & Team

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/organizations` | List user's orgs |
| `POST` | `/api/organizations` | Create org |
| `POST` | `/api/invites/{orgId}` | Invite user to org |
| `GET` | `/api/invites/pending` | List pending invites |
| `POST` | `/api/invites/accept` | Accept invite |
| `POST` | `/api/joinrequests/{orgId}` | Request to join org |

### Configuration (Owner/Manager only)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET/POST/PUT/DELETE` | `/api/pipelines` | Manage pipelines (with their own stale-deal days) |
| `GET/POST/PUT/DELETE` | `/api/dealstages` | Manage deal stages (won or lost, and a default probability). Making a stage won, lost or open again gives the deals that had its old outcome the new one |
| `GET/POST/PUT/DELETE` | `/api/leadsources` | Manage lead sources |
| `GET/POST/PUT/DELETE` | `/api/leadstatuses` | Manage lead statuses |

### Other

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/reporting/dashboard` | Dashboard stats |
| `GET` | `/api/templates` | Email templates |
| `POST` | `/api/copy/generate` | Generate sales copy |
| `GET` | `/api/settings` | User settings |
| `PUT` | `/api/settings` | Update settings |

---

## Project Structure

```
backend/
├── src/
│   ├── ACI.Domain/           # Entities, enums (no dependencies)
│   ├── ACI.Application/      # Use cases, DTOs, interfaces, Result types
│   ├── ACI.Infrastructure/   # EF Core, repositories, JWT, external services
│   └── ACI.WebApi/           # Controllers, middleware, Swagger
└── tests/
    └── ACI.Application.Tests/  # Unit tests
```

### Key Files

| File | What it does |
|------|--------------|
| `Application/Common/Result.cs` | Result<T> pattern for success/failure |
| `Application/Common/DomainErrors.cs` | Centralized error definitions |
| `Application/Common/ValidationHelper.cs` | Email, phone, domain validation |
| `WebApi/Extensions/ResultExtensions.cs` | Convert Result → ActionResult |
| `WebApi/Middleware/GlobalExceptionHandler.cs` | Global exception handling |

---

## Configuration Reference

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;..."
  },
  "Jwt": {
    "SecretKey": "your-secret-key-at-least-32-characters-long",
    "Issuer": "CadenceCRM",
    "Audience": "CadenceCRM",
    "ExpiryMinutes": 60
  }
}
```

> **Important:** Change `Jwt:SecretKey` in production. Must be at least 32 characters.

---

## Database Schema

### Core Entities

| Table | Description |
|-------|-------------|
| `Users` | Authentication and data ownership |
| `UserSettings` | Per-user preferences |
| `Organizations` | Multi-tenant organizations |
| `OrganizationMembers` | User-org membership + roles |

### CRM Entities

| Table | Description |
|-------|-------------|
| `Companies` | Accounts/businesses |
| `Contacts` | People (linked to companies) |
| `Deals` | Sales opportunities |
| `Leads` | Potential customers (convertible) |
| `TaskItems` | To-dos linked to leads/deals/contacts |
| `Activities` | Calls, meetings, emails, notes |

### Configuration Entities

| Table | Description |
|-------|-------------|
| `Pipelines` | Sales pipelines per org |
| `DealStages` | Stages within pipelines |
| `LeadSources` | Where leads come from |
| `LeadStatuses` | Lead lifecycle statuses |

---

## Code Patterns

### Result Pattern

Services return `Result<T>` instead of throwing exceptions:

```csharp
// Service
public async Task<Result<ContactDto>> GetByIdAsync(Guid id)
{
    var entity = await _repository.GetByIdAsync(id);
    if (entity == null)
        return DomainErrors.Contact.NotFound;
    
    return Map(entity);
}

// Controller
var result = await _contactService.GetByIdAsync(id);
return result.ToActionResult();  // Ok(data) or ProblemDetails
```

### Logging

Structured logging with Serilog:

```csharp
_logger.LogDebug("Getting contact {ContactId} for user {UserId}", id, userId);
_logger.LogWarning("Contact {ContactId} not found", id);
_logger.LogError(ex, "Failed to create contact for user {UserId}", userId);
```

### Validation

DTOs use DataAnnotations:

```csharp
public record CreateContactRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(256)]
    public required string Name { get; init; }

    [Required, EmailAddress]
    public required string Email { get; init; }

    [Phone]
    public string? Phone { get; init; }
}
```

---

## Troubleshooting

### Database connection fails
- Check SQL Server is running
- Verify connection string in `appsettings.Development.json`
- For LocalDB: `sqllocaldb info mssqllocaldb`

### Migrations fail
```bash
# Reset and recreate
dotnet ef database drop --project src/ACI.Infrastructure --startup-project src/ACI.WebApi
dotnet ef database update --project src/ACI.Infrastructure --startup-project src/ACI.WebApi
```

### JWT token invalid
- Ensure `Jwt:SecretKey` is at least 32 characters
- Check token hasn't expired
- Verify `Authorization: Bearer <token>` header format

### Port already in use
Edit `src/ACI.WebApi/Properties/launchSettings.json` to change the port.

---

## Testing

169 unit tests covering:
- ContactService, LeadService, AuthService
- DealService, CompanyService, TaskService
- ActivityService, TemplateService, OrganizationService
- Result pattern

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ContactServiceTests"
```

---

## Quality Metrics

| Category | Status |
|----------|--------|
| Error Handling | ✅ Result pattern, DomainErrors, ProblemDetails |
| Logging | ✅ Serilog with structured logging |
| API Documentation | ✅ XML comments, Swagger |
| Architecture | ✅ Clean Architecture |
| Testing | ✅ 169 unit tests |
| Validation | ✅ DataAnnotations + ValidationHelper |

Full report: `src/app/reports/BACKEND_CODE_QUALITY_AND_STANDARDS_REPORT.md`
