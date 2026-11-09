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
| `PUT` | `/api/contacts/{id}` | Update contact |
| `DELETE` | `/api/contacts/{id}` | Delete contact (a lead it was made for does not get another) |

### Companies

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/companies` | One page of companies with their counts: `industry`, `hasIndustry`, `size`, `sortBy` (name, createdAt, contacts, deals), `sortDir`, `search` |
| `GET` | `/api/companies/summary` | The figures above the list, and the industries in use |
| `GET` | `/api/companies/export` | Every company the list shows, as CSV |
| `GET` | `/api/companies/{id}` | Get company |
| `POST` | `/api/companies` | Create company |
| `PUT` | `/api/companies/{id}` | Update company |

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
| `PUT` | `/api/deals/{id}` | Update deal |
| `DELETE` | `/api/deals/{id}` | Delete deal |

### Leads

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/leads` | List leads |
| `GET` | `/api/leads/search?q=` | Search leads |
| `POST` | `/api/leads` | Create lead |
| `PUT` | `/api/leads/{id}` | Update lead |
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
own Message-ID, and is written once on the record's timeline when it has gone.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/email/send` | Send from the composer (or `source: "assistant"`): 200 `sent` when the relay took it, 202 `queued` when it did not yet and the outbox keeps trying |
| `POST` | `/api/email/{id}/cancel` | Take back a queued email before it goes (whoever sent it, or a manager); 409 once it has gone |
| `GET` | `/api/organizations/{id}/email` | Who the workspace's email goes out from: its own address, whether its domain is verified, the shared address, whether replies come back |
| `PUT` | `/api/organizations/{id}/email` | `{ sendingAddress }`: set or clear the workspace's own sending address (owner or manager) |
| `GET` | `/api/organizations/{id}/email/suppressions` | Addresses the workspace does not email because the mail service reported them (bounced, invalid, spam report, unsubscribed) |
| `DELETE` | `/api/organizations/{id}/email/suppressions/{suppressionId}` | Take an address off that list so the workspace may email it again (owner or manager) |
| `GET` | `/api/webhook/organizations/{id}/deliveries` | The latest outbound webhook deliveries and what the receiver answered (owner or manager) |
| `POST` | `/api/public/email/inbound/{secret}` | SendGrid Inbound Parse: a reply, matched to its message by reply+token address or In-Reply-To/References, written on the timeline; the person's sequences stop. 404 without `Email:InboundParseSecret` |
| `POST` | `/api/public/email/events/{secret}` | SendGrid Event Webhook (or `/events` with a signature checked against `Email:EventWebhookVerificationKey`): bounces, drops, spam reports and unsubscribes suppress the address and stop sequences; deliveries and opens are recorded |

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
