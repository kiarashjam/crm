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
| `GET` | `/api/contacts` | List all contacts |
| `GET` | `/api/contacts/search?q=john` | Search contacts |
| `GET` | `/api/contacts/{id}` | Get single contact |
| `POST` | `/api/contacts` | Create contact |
| `PUT` | `/api/contacts/{id}` | Update contact |
| `DELETE` | `/api/contacts/{id}` | Delete contact |

### Companies

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/companies` | List companies |
| `GET` | `/api/companies/{id}` | Get company |
| `POST` | `/api/companies` | Create company |
| `PUT` | `/api/companies/{id}` | Update company |

### Deals

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/deals` | List deals |
| `GET` | `/api/deals/search?q=` | Search deals |
| `GET` | `/api/deals/{id}` | Get deal |
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
| `GET/POST/PUT/DELETE` | `/api/pipelines` | Manage pipelines |
| `GET/POST/PUT/DELETE` | `/api/dealstages` | Manage deal stages |
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
