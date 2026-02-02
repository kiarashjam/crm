# ACI Backend

ASP.NET Core 8 Web API with **Clean Architecture**: Domain, Application, Infrastructure, and WebApi layers. The database is SQL Server with Entity Framework Core 8.

## Structure

- **ACI.Domain** – Entities, enums, no dependencies
- **ACI.Application** – Use cases, DTOs, interfaces (depends on Domain)
- **ACI.Infrastructure** – EF Core, repositories, JWT, password hashing, copy generator (depends on Application, Domain)
- **ACI.WebApi** – Controllers, auth, Swagger (depends on Application, Infrastructure)

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
| **Connection** | `GET /api/connection/status`, `PUT /api/connection/status` |
| **Settings** | `GET /api/settings`, `PUT /api/settings` |

### Quick test

1. Register: `POST /api/auth/register` with `{ "email": "user@example.com", "password": "YourPassword123!", "name": "Test User" }`
2. Use the returned `token` in the header: `Authorization: Bearer <token>`
3. Call `GET /api/templates` or `GET /api/settings`

## Database

- **Users** – Auth and ownership of data
- **UserSettings** – Company name, brand tone per user
- **CrmConnections** – CRM connection status per user
- **Companies** – Organizations (optional link for contacts/deals/leads)
- **Contacts** – People; optional CompanyId, Phone
- **Deals** – Opportunities; optional CompanyId, ExpectedCloseDateUtc, IsWon
- **Leads** – Potential customers; CompanyId, Source, Status
- **TaskItems** – Tasks; optional LeadId, DealId; DueDateUtc, Completed
- **Activities** – Calls, meetings, emails, notes; optional ContactId, DealId
- **Templates** – Copy templates (system and user)
- **CopyHistoryItems** – Generated/sent copy for history and stats

Migrations: `InitialCreate` plus `SalesCrmCore` (Leads, TaskItems, Activities, Contact.Phone, Deal.ExpectedCloseDateUtc, Deal.IsWon). On startup, `MigrateAsync()` applies pending migrations and seed data runs.

Copy generation is currently template-based; you can replace `ICopyGenerator` in Infrastructure with an AI/LLM integration.
