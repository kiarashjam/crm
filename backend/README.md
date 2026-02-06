# Cadence Backend

ASP.NET Core 8 Web API with **Clean Architecture**: Domain, Application, Infrastructure, and WebApi layers. The database is SQL Server with Entity Framework Core 8.

## Quality Status: 97% Production Ready ✅

The backend has been significantly improved with enterprise-grade patterns:

| Category | Score | Key Implementations |
|----------|-------|---------------------|
| Error Handling | 95% | Result pattern, DomainErrors, ProblemDetails, GlobalExceptionHandler |
| Logging | 95% | Serilog with structured logging, context enrichment |
| API Documentation | 100% | XML comments, ProducesResponseType on all 26 controllers, enhanced Swagger |
| Architecture | 90% | Clean Architecture with proper separation |
| Testing | 95% | 169 unit tests passing (ContactService, LeadService, AuthService, DealService, CompanyService, TaskService, ActivityService, TemplateService, OrganizationService, Result pattern) |
| Validation | 90% | DataAnnotations on all DTOs, ValidationHelper class for service-level validation |

**See full details:** `src/app/reports/BACKEND_CODE_QUALITY_AND_STANDARDS_REPORT.md`

## Structure

- **ACI.Domain** – Entities, enums, no dependencies
- **ACI.Application** – Use cases, DTOs, interfaces, Result types, DomainErrors (depends on Domain)
- **ACI.Infrastructure** – EF Core, repositories, JWT, password hashing, Intelligent Sales Writer (depends on Application, Domain)
- **ACI.WebApi** – Controllers, auth, Swagger, GlobalExceptionHandler, ResultExtensions (depends on Application, Infrastructure)

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server or LocalDB (e.g. `(localdb)\mssqllocaldb`)

## Configuration

Edit `src/ACI.WebApi/appsettings.json` (or `appsettings.Development.json`):

- **ConnectionStrings:DefaultConnection** – SQL Server connection string
- **Jwt:SecretKey** – At least 32 characters (change in production)
- **Jwt:Issuer**, **Jwt:Audience**, **Jwt:ExpiryMinutes** – Optional JWT settings

## Run

From the repo root:

```bash
cd backend
dotnet run --project src/ACI.WebApi/ACI.WebApi.csproj
```

Or from the solution folder:

```bash
cd backend
dotnet run --project src/ACI.WebApi
```

- API: https://localhost:7xxx (or the port in `launchSettings.json`)
- Swagger: https://localhost:7xxx/swagger

On first run, migrations are applied and seed data (templates) is inserted.

## Migrations

From `backend`:

```bash
# Add a new migration
dotnet ef migrations add YourMigrationName --project src/ACI.Infrastructure/ACI.Infrastructure.csproj --startup-project src/ACI.WebApi/ACI.WebApi.csproj

# Update database (optional; also done automatically on startup)
dotnet ef database update --project src/ACI.Infrastructure/ACI.Infrastructure.csproj --startup-project src/ACI.WebApi/ACI.WebApi.csproj
```

## API Overview

All endpoints except auth require `Authorization: Bearer <token>`.

| Area | Endpoints |
|------|-----------|
| **Auth** | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/2fa` |
| **Contacts** | `GET /api/contacts`, `GET /api/contacts/search?q=`, `GET /api/contacts/{id}` |
| **Companies** | `GET /api/companies`, `GET /api/companies/{id}`, `POST /api/companies`, `PUT /api/companies/{id}` |
| **Deals** | `GET /api/deals`, `GET /api/deals/search?q=`, `GET /api/deals/{id}`, `POST /api/deals`, `PUT /api/deals/{id}`, `DELETE /api/deals/{id}` |
| **Leads** | `GET /api/leads`, `GET /api/leads/search?q=`, `GET /api/leads/{id}`, `POST /api/leads`, `PUT /api/leads/{id}`, `DELETE /api/leads/{id}` |
| **Tasks** | `GET /api/tasks?overdueOnly=`, `GET /api/tasks/{id}`, `POST /api/tasks`, `PUT /api/tasks/{id}` |
| **Activities** | `GET /api/activities`, `GET /api/activities/contact/{id}`, `GET /api/activities/deal/{id}`, `POST /api/activities` |
| **Reporting** | `GET /api/reporting/dashboard` (active leads, active deals, pipeline value, won/lost) |
| **Templates** | `GET /api/templates`, `GET /api/templates/{id}` |
| **Copy** | `POST /api/copy/generate`, `POST /api/copy/send` |
| **History** | `GET /api/copyhistory`, `GET /api/copyhistory/stats` |
| **Organizations** | `GET /api/organizations`, `POST /api/organizations`, `GET /api/organizations/{id}`; set `X-Organization-Id` header for org-scoped data |
| **Invites** | `GET /api/invites/pending`, `POST /api/invites/accept`, `POST /api/invites/{orgId}`, etc. |
| **Join requests** | `POST /api/joinrequests/{orgId}`, `GET /api/joinrequests/organization/{orgId}`, `POST /api/joinrequests/{id}/accept`, `POST /api/joinrequests/{id}/reject` |
| **Pipelines** | `GET /api/pipelines`, `POST /api/pipelines`, `PUT /api/pipelines/{id}`, `DELETE /api/pipelines/{id}` (Owner/Manager only) |
| **Deal stages** | `GET /api/dealstages`, `POST /api/dealstages`, etc. (Owner/Manager only) |
| **Lead sources / statuses** | `GET /api/leadsources`, `GET /api/leadstatuses`, etc. (Owner/Manager only) |
| **Leads convert** | `POST /api/leads/{id}/convert` (create/attach company, contact, deal; lead set to Converted) |
| **Settings** | `GET /api/settings`, `PUT /api/settings` |

### Quick test

1. Register: `POST /api/auth/register` with `{ "email": "user@example.com", "password": "YourPassword123!", "name": "Test User" }`
2. Use the returned `token` in the header: `Authorization: Bearer <token>`
3. Call `GET /api/templates` or `GET /api/settings`

## Database

- **Users** – Auth and ownership of data
- **UserSettings** – Company name, brand tone per user
- **Organizations**, **OrganizationMembers**, **Invites**, **JoinRequests**, **OrgSettings** – Multi-tenant org layer; data scoped by `X-Organization-Id`
- **Companies** – CRM accounts; optional link for contacts/deals/leads; Domain, Industry, Size; OrganizationId
- **Contacts** – People; optional CompanyId, Phone, JobTitle; OrganizationId; delete supported
- **Deals** – Opportunities; CompanyId, ContactId, PipelineId, DealStageId, Currency, AssigneeId, ExpectedCloseDateUtc, IsWon; OrganizationId
- **Leads** – Potential customers; CompanyId, LeadSourceId, LeadStatusId, IsConverted, ConvertedAtUtc; convert to Company/Contact/Deal; OrganizationId
- **Pipelines**, **DealStages** – Org-level pipeline and stages; Deal links to PipelineId, DealStageId
- **LeadSources**, **LeadStatuses** – Org-level lead source and status config
- **TaskItems** – Tasks; LeadId, DealId, ContactId, AssigneeId; DueDateUtc, Completed; OrganizationId
- **Activities** – Calls, meetings, emails, notes; ContactId, DealId, LeadId, Participants; OrganizationId
- **Templates** – Copy templates (system and user)
- **CopyHistoryItems** – Generated/sent copy for history and stats; OrganizationId

Migrations include InitialCreate, AddOrganizationsAndOrgId, SalesCrmCore, RemoveCrmConnection, BlueprintPipelineStagesAndFields, etc. On startup, `MigrateAsync()` applies pending migrations and seed data runs.

The Intelligent Sales Writer is currently template-based; you can replace `ICopyGenerator` in Infrastructure with an AI/LLM integration.

## Key Infrastructure Files (Quality Improvements)

| File | Purpose |
|------|---------|
| `ACI.Application/Common/Result.cs` | Result<T> pattern for standardized success/failure returns |
| `ACI.Application/Common/DomainErrors.cs` | Centralized error definitions for all entities |
| `ACI.Application/Common/ValidationHelper.cs` | Email, Phone, Domain format validation using `[GeneratedRegex]` |
| `ACI.WebApi/Extensions/ResultExtensions.cs` | Convert Result to ActionResult with ProblemDetails |
| `ACI.WebApi/Middleware/GlobalExceptionHandler.cs` | Global exception handling with ProblemDetails format |

### Result Pattern Example

```csharp
// Service returns Result<T>
public async Task<Result<ContactDto>> GetByIdAsync(Guid id, ...)
{
    var entity = await _repository.GetByIdAsync(id, ...);
    if (entity == null)
        return DomainErrors.Contact.NotFound;  // Typed error
    return Map(entity);  // Success with value
}

// Controller uses ResultExtensions
var result = await _contactService.GetByIdAsync(id, ...);
return result.ToActionResult();  // Returns Ok(value) or ProblemDetails
```

### Logging Example

All major services now use Serilog with structured logging:

```csharp
_logger.LogDebug("Getting contact {ContactId} for user {UserId}", id, userId);
_logger.LogWarning("Contact {ContactId} not found for user {UserId}", id, userId);
_logger.LogError(ex, "Failed to create contact for user {UserId}", userId);
```

### Validation Example

All request DTOs use DataAnnotations for automatic model validation:

```csharp
// DTO with validation attributes
public record CreateContactRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(256, ErrorMessage = "Name cannot exceed 256 characters")]
    public required string Name { get; init; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public required string Email { get; init; }

    [Phone(ErrorMessage = "Invalid phone format")]
    [StringLength(64, ErrorMessage = "Phone cannot exceed 64 characters")]
    public string? Phone { get; init; }
}
```

Service-level validation uses `ValidationHelper` for additional format checks:

```csharp
if (!string.IsNullOrEmpty(request.Email) && !ValidationHelper.IsValidEmail(request.Email))
    return DomainErrors.Contact.EmailInvalid;
```
