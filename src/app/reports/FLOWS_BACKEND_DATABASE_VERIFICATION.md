# Flows — Backend & Database Verification (Deep Dive)

Verification of all app flows against backend (API, services, repositories), database (entities, DbContext, EF configurations, migrations), and frontend API usage. Includes exact schema, request/response shapes, and data flow.

---

## Table of contents

1. [Executive summary](#1-executive-summary)
2. [Database schema deep dive](#2-database-schema-deep-dive)
3. [Entity reference](#3-entity-reference)
4. [EF configurations](#4-ef-configurations)
5. [API contract reference](#5-api-contract-reference)
6. [Frontend ↔ backend alignment](#6-frontend--backend-alignment)
7. [Service → repository data flow](#7-service--repository-data-flow)
8. [Key user journey flows](#8-key-user-journey-flows)
9. [Startup and dependency injection](#9-startup-and-dependency-injection)
10. [Gaps and remediation](#10-gaps-and-remediation)

---

## 1. Executive summary

| Flow | Frontend | Backend API | Service | Repository | Entity | DbSet | Migration |
|------|----------|-------------|---------|------------|--------|-------|-----------|
| **Auth** | ✅ | AuthController | AuthService, UserRepository | UserRepository | User | Users | ✅ InitialCreate |
| **Settings** | ✅ | SettingsController | SettingsService | (UserSettings in User) | UserSettings | UserSettings | ✅ InitialCreate |
| **Connection** | ✅ | ConnectionController | ConnectionService | (CrmConnection) | CrmConnection | CrmConnections | ✅ InitialCreate |
| **Templates** | ✅ | TemplatesController | TemplateService | TemplateRepository | Template | Templates | ✅ InitialCreate |
| **Copy / Generate** | ✅ | CopyController | CopyGeneratorService | — | — | — | N/A |
| **Copy history** | ✅ | CopyHistoryController | CopyHistoryService | CopyHistoryRepository | CopyHistoryItem | CopyHistoryItems | ✅ InitialCreate |
| **Send to CRM** | ✅ | CopyController (POST /api/copy/send) | SendToCrmService | CopyHistoryRepository | CopyHistoryItem | CopyHistoryItems | ✅ InitialCreate |
| **Contacts** | ✅ | ContactsController | ContactService | ContactRepository | Contact | Contacts | ⚠️ **Missing column: Phone** |
| **Companies** | ✅ | CompaniesController | CompanyService | CompanyRepository | Company | Companies | ✅ InitialCreate |
| **Deals** | ✅ | DealsController | DealService | DealRepository | Deal | Deals | ⚠️ **Missing: ExpectedCloseDateUtc, IsWon** |
| **Leads** | ✅ | LeadsController | LeadService | LeadRepository | Lead | Leads | ❌ **Table not in migration** |
| **Tasks** | ✅ | TasksController | TaskService | TaskRepository | TaskItem | TaskItems | ❌ **Table not in migration** |
| **Activities** | ✅ | ActivitiesController | ActivityService | ActivityRepository | Activity | Activities | ❌ **Table not in migration** |
| **Reporting** | ✅ | ReportingController | ReportingService | (direct DbContext) | — | Leads, Deals | ❌ **Depends on Leads + Deal columns** |

**Critical:** Until a new migration is added and applied, any request that touches **Leads**, **TaskItems**, or **Activities** (or **Deals** with `ExpectedCloseDateUtc`/`IsWon`, or **Contacts** with `Phone`) will fail at runtime with database errors. `ReportingService.GetDashboardStatsAsync` queries `_db.Leads` and `d.IsWon` and will also fail.

---

## 2. Database schema deep dive

### 2.1 What the InitialCreate migration actually created

Migration file: `ACI.Infrastructure/Migrations/20260128122320_InitialCreate.cs`.

| Table | Columns | Primary key | Foreign keys | Indexes |
|-------|---------|-------------|--------------|---------|
| **Users** | Id, Name (256), Email (256, unique), PasswordHash (500), TwoFactorEnabled, TwoFactorSecretProtected (2000), TwoFactorEnabledAtUtc, CreatedAtUtc, LastLoginAtUtc | Id | — | IX_Users_Email (unique) |
| **UserSettings** | UserId, CompanyName (256), BrandTone (int), UpdatedAtUtc | UserId | FK → Users.Id (Cascade) | — |
| **CrmConnections** | UserId, Connected, AccountEmail (256), EncryptedToken (2000), ConnectedAtUtc, UpdatedAtUtc | UserId | FK → Users.Id (Cascade) | — |
| **Companies** | Id, UserId, Name (256), CreatedAtUtc | Id | FK → Users.Id (Cascade) | IX_Companies_UserId |
| **Contacts** | Id, UserId, Name (256), Email (256), **no Phone**, CompanyId, CreatedAtUtc | Id | FK → Users.Id (Cascade), FK → Companies.Id | IX_Contacts_UserId, IX_Contacts_CompanyId |
| **Deals** | Id, UserId, Name (512), Value (64), Stage (128), CompanyId, CreatedAtUtc, **no ExpectedCloseDateUtc, no IsWon** | Id | FK → Users.Id (Cascade), FK → Companies.Id | IX_Deals_UserId, IX_Deals_CompanyId |
| **CopyHistoryItems** | Id, UserId, Type (128), Copy (max), RecipientName (256), RecipientType (int), RecipientId (128), CreatedAtUtc | Id | FK → Users.Id (Cascade) | IX_CopyHistoryItems_UserId |
| **Templates** | Id, Title (256), Description (1000), Category (128), CopyTypeId (int), Goal (512), UseCount, UserId, CreatedAtUtc | Id | FK → Users.Id | IX_Templates_UserId |

**Not in InitialCreate:** Tables **Leads**, **TaskItems**, **Activities** do not exist in the migration. They are defined in the domain and DbContext and have EF configurations, but no migration was ever generated for them.

### 2.2 Entity vs current database (column-level)

| Entity | Property | In InitialCreate? | Notes |
|--------|----------|-------------------|-------|
| **Contact** | Id, UserId, Name, Email, CompanyId, CreatedAtUtc | ✅ | All present |
| **Contact** | Phone | ❌ | In entity and ContactConfiguration; **not in migration**. |
| **Deal** | Id, UserId, Name, Value, Stage, CompanyId, CreatedAtUtc | ✅ | All present |
| **Deal** | ExpectedCloseDateUtc | ❌ | In entity and DealDto/UpdateDealRequest; **not in migration**. |
| **Deal** | IsWon | ❌ | In entity and DealDto/UpdateDealRequest; **not in migration**. |
| **Lead** | (entire entity) | ❌ | **Table Leads does not exist.** LeadConfiguration maps to "Leads". |
| **TaskItem** | (entire entity) | ❌ | **Table TaskItems does not exist.** TaskConfiguration maps to "TaskItems". |
| **Activity** | (entire entity) | ❌ | **Table Activities does not exist.** ActivityConfiguration maps to "Activities". |

### 2.3 What happens at runtime without a new migration

- **LeadsController** → LeadService → LeadRepository → `_db.Leads` → **SqlException**: Invalid object name 'Leads'.
- **TasksController** → TaskService → TaskRepository → `_db.TaskItems` → **SqlException**: Invalid object name 'TaskItems'.
- **ActivitiesController** → ActivityService → ActivityRepository → `_db.Activities` → **SqlException**: Invalid object name 'Activities'.
- **ReportingController** (dashboard) → ReportingService → `_db.Leads.CountAsync(...)`, `_db.Deals.Where(...)`, then `d.IsWon` → **SqlException** (Leads table missing) or missing column when Deals is queried with IsWon.
- **DealsController** (create/update with ExpectedCloseDateUtc or IsWon) → DealService → DealRepository → EF may ignore unmapped columns or throw depending on EF version when saving.
- **ContactsController** (create/update with Phone) → ContactService → ContactRepository → **Phone not persisted** (column missing); reads will not return Phone.

---

## 3. Entity reference

### 3.1 Base entity

- **BaseEntity** (Domain/Common): `Id` (Guid). All domain entities extend this.

### 3.2 Core entities (with table name and key relationships)

| Entity | Table | Key properties | FKs / navigations |
|--------|-------|----------------|-------------------|
| **User** | Users | Id, Name, Email, PasswordHash, TwoFactorEnabled, TwoFactorSecretProtected, TwoFactorEnabledAtUtc, CreatedAtUtc, LastLoginAtUtc | Settings (1:1), CrmConnection (1:1), Companies, Contacts, Deals, Leads, TaskItems, Activities, CopyHistory |
| **UserSettings** | UserSettings | UserId (PK), CompanyName, BrandTone, UpdatedAtUtc | User |
| **CrmConnection** | CrmConnections | UserId (PK), Connected, AccountEmail, EncryptedToken, ConnectedAtUtc, UpdatedAtUtc | User |
| **Company** | Companies | Id, UserId, Name, CreatedAtUtc | User; Contacts, Deals, Leads |
| **Contact** | Contacts | Id, UserId, Name, Email, **Phone**, CompanyId, CreatedAtUtc | User, Company; Activities |
| **Deal** | Deals | Id, UserId, Name, Value, Stage, CompanyId, **ExpectedCloseDateUtc**, **IsWon**, CreatedAtUtc | User, Company; Activities, TaskItems |
| **Lead** | Leads | Id, UserId, Name, Email, Phone, CompanyId, Source, Status, CreatedAtUtc | User, Company; TaskItems |
| **TaskItem** | TaskItems | Id, UserId, Title, Description, DueDateUtc, Completed, LeadId, DealId, CreatedAtUtc | User, Lead, Deal |
| **Activity** | Activities | Id, UserId, Type, Subject, Body, ContactId, DealId, CreatedAtUtc | User, Contact, Deal |
| **Template** | Templates | Id, Title, Description, Category, CopyTypeId, Goal, UseCount, UserId?, CreatedAtUtc | User (optional) |
| **CopyHistoryItem** | CopyHistoryItems | Id, UserId, Type, Copy, RecipientName, RecipientType (enum), RecipientId, CreatedAtUtc | User |

### 3.3 Enums used in entities

- **RecipientType**: Contact = 0, Deal = 1, Workflow = 2, Email = 3 (stored as int in DB).
- **BrandTone**: int in UserSettings (Professional, Friendly, Persuasive).
- **CopyTypeId**: int in Template (SalesEmail, FollowUp, CrmNote, DealMessage, WorkflowMessage).

---

## 4. EF configurations

All in `ACI.Infrastructure/Persistence/Configurations/`. Applied via `ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`.

| Configuration | Table | Key mappings | Max lengths (selected) |
|---------------|-------|--------------|------------------------|
| UserConfiguration | (convention) | — | — |
| UserSettingsConfiguration | UserSettings | PK UserId | CompanyName 256, BrandTone int |
| CrmConnectionConfiguration | CrmConnections | PK UserId | AccountEmail 256, EncryptedToken 2000 |
| CompanyConfiguration | Companies | PK Id, FK UserId | Name 256 |
| ContactConfiguration | Contacts | PK Id, FK UserId, FK CompanyId | Name 256, Email 256, **Phone 64** |
| DealConfiguration | Deals | PK Id, FK UserId, FK CompanyId | Name 512, Value 64, Stage 128 |
| LeadConfiguration | Leads | PK Id, FK UserId, FK CompanyId | Name 256, Email 256, Phone 64, Source 64, Status 64 |
| TaskConfiguration | TaskItems | PK Id, FK UserId, FK LeadId, FK DealId | Title 512, Description 2048 |
| ActivityConfiguration | Activities | PK Id, FK UserId, FK ContactId, FK DealId | Type 32, Subject 512, Body 8000 |
| TemplateConfiguration | Templates | PK Id, FK UserId (optional) | Title 256, Description 1000, Category 128, Goal 512 |
| CopyHistoryItemConfiguration | CopyHistoryItems | PK Id, FK UserId | Type 128, RecipientName 256, RecipientId 128 |

Note: Lead, TaskItem, and Activity configurations are applied at build time; the migration that creates their tables has not been generated.

---

## 5. API contract reference

### 5.1 Auth (AuthController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| POST | /api/auth/register | `{ name, email, password }` | `{ token, user }` or `{ requiresTwoFactor, twoFactorToken }` | No |
| POST | /api/auth/login | `{ email, password }` | `{ token, user }` or `{ requiresTwoFactor, twoFactorToken }` | No |
| POST | /api/auth/2fa | `{ twoFactorToken, code }` | `{ token, user }` | No |

### 5.2 Settings (SettingsController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/settings | — | UserSettingsDto (CompanyName, BrandTone) | Yes |
| PUT | /api/settings | UserSettingsDto | 200 | Yes |

### 5.3 Connection (ConnectionController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/connection/status | — | `{ connected, accountEmail? }` | Yes |
| PUT | /api/connection/status | `{ connected, accountEmail? }` | 200 | Yes |

### 5.4 Templates (TemplatesController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/templates | — | TemplateDto[] | Yes |
| GET | /api/templates/{id} | — | TemplateDto | Yes |

### 5.5 Copy (CopyController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| POST | /api/copy/generate | GenerateCopyRequest (CopyTypeId, Goal, Context, Length, CompanyName, BrandTone) | `{ copy }` | Yes |
| POST | /api/copy/send | SendToCrmRequest (ObjectType, RecordId, RecordName, Copy, CopyTypeLabel) | SendToCrmResult | Yes |

### 5.6 Copy history (CopyHistoryController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/copyhistory | — | CopyHistoryItemDto[] | Yes |
| GET | /api/copyhistory/stats | — | `{ sentThisWeek, totalSent }` | Yes |

### 5.7 Contacts (ContactsController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/contacts | — | ContactDto[] | Yes |
| GET | /api/contacts/search?q= | — | ContactDto[] | Yes |

### 5.8 Companies (CompaniesController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/companies | — | CompanyDto[] | Yes |
| POST | /api/companies | CreateCompanyRequest (Name) | CompanyDto | Yes |
| PUT | /api/companies/{id} | UpdateCompanyRequest (Name) | CompanyDto | Yes |

### 5.9 Deals (DealsController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/deals | — | DealDto[] | Yes |
| GET | /api/deals/search?q= | — | DealDto[] | Yes |
| GET | /api/deals/{id} | — | DealDto | Yes |
| POST | /api/deals | CreateDealRequest (Name, Value, Stage?, CompanyId?, ExpectedCloseDateUtc?) | DealDto | Yes |
| PUT | /api/deals/{id} | UpdateDealRequest (Name?, Value?, Stage?, CompanyId?, ExpectedCloseDateUtc?, IsWon?) | DealDto | Yes |
| DELETE | /api/deals/{id} | — | 204 No Content | Yes |

### 5.10 Leads (LeadsController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/leads | — | LeadDto[] | Yes |
| GET | /api/leads/search?q= | — | LeadDto[] | Yes |
| GET | /api/leads/{id} | — | LeadDto | Yes |
| POST | /api/leads | CreateLeadRequest (Name, Email, Phone?, CompanyId?, Source?, Status) | LeadDto | Yes |
| PUT | /api/leads/{id} | UpdateLeadRequest (Name?, Email?, Phone?, CompanyId?, Source?, Status?) | LeadDto | Yes |
| DELETE | /api/leads/{id} | — | 204 No Content | Yes |

### 5.11 Tasks (TasksController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/tasks?overdueOnly=true\|false | — | TaskDto[] | Yes |
| POST | /api/tasks | CreateTaskRequest (Title, Description?, DueDateUtc?, LeadId?, DealId?) | TaskDto | Yes |
| PUT | /api/tasks/{id} | UpdateTaskRequest (Title?, Description?, DueDateUtc?, LeadId?, DealId?, Completed?) | TaskDto | Yes |

### 5.12 Activities (ActivitiesController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/activities | — | ActivityDto[] | Yes |
| GET | /api/activities/contact/{id} | — | ActivityDto[] | Yes |
| GET | /api/activities/deal/{id} | — | ActivityDto[] | Yes |
| POST | /api/activities | CreateActivityRequest (Type, Subject?, Body?, ContactId?, DealId?) | ActivityDto | Yes |

### 5.13 Reporting (ReportingController)

| Method | Route | Request | Response | Auth |
|--------|-------|---------|----------|------|
| GET | /api/reporting/dashboard | — | DashboardStatsDto (ActiveLeadsCount, ActiveDealsCount, PipelineValue, DealsWonCount, DealsLostCount) | Yes |

---

## 6. Frontend ↔ backend alignment

| Frontend module | Functions | HTTP calls | Backend |
|-----------------|-----------|------------|---------|
| auth / authApi | login, register, loginWithTwoFactor | POST /api/auth/login, register, 2fa | AuthController |
| settings | getUserSettings, saveUserSettings | GET/PUT /api/settings | SettingsController |
| connection | getConnectionStatus, setConnectionStatus | GET/PUT /api/connection/status | ConnectionController |
| templates | getTemplates, getTemplateById | GET /api/templates, /api/templates/{id} | TemplatesController |
| copyHistory | getCopyHistory, getCopyHistoryStats, sendCopyToCrm | GET /api/copyhistory, GET stats, POST /api/copy/send | CopyHistoryController, CopyController |
| copyGenerator | generateCopy | POST /api/copy/generate | CopyController |
| contacts | getContacts | GET /api/contacts, GET /api/contacts/search?q= | ContactsController |
| companies | getCompanies, createCompany, updateCompany | GET, POST, PUT /api/companies/{id} | CompaniesController |
| leads | getLeads, searchLeads, createLead, updateLead, deleteLead | GET, GET search?q=, POST, PUT /api/leads/{id}, DELETE /api/leads/{id} | LeadsController |
| deals | getDeals, searchDeals, createDeal, updateDeal, deleteDeal | GET, GET search?q=, POST, PUT /api/deals/{id}, DELETE /api/deals/{id} | DealsController |
| tasks | getTasks, createTask, updateTask | GET /api/tasks?overdueOnly=, POST, PUT /api/tasks/{id} | TasksController |
| activities | getActivities, getActivitiesByContact, getActivitiesByDeal, createActivity | GET /api/activities, GET contact/{id}, GET deal/{id}, POST | ActivitiesController |
| reporting | getDashboardStats | GET /api/reporting/dashboard | ReportingController |

**Note:** Frontend does not call GET /api/leads/{id} or GET /api/deals/{id} from the current pages; those endpoints exist for future use or direct API consumers. Create/update/delete request body shapes match backend DTOs (e.g. CreateLeadRequest: Name, Email, Phone?, CompanyId?, Source?, Status).

---

## 7. Service → repository data flow

### 7.1 Create lead (example)

1. **LeadsController.Create** → `_leadService.CreateAsync(userId, request, ct)`.
2. **LeadService.CreateAsync** → builds `Lead` entity (Id = NewGuid(), UserId, Name, Email, Phone, CompanyId, Source, Status, CreatedAtUtc) → `_repository.AddAsync(entity, ct)`.
3. **LeadRepository.AddAsync** → `_db.Leads.Add(lead)` → `_db.SaveChangesAsync(ct)`.  
   **Fails at runtime** if table Leads does not exist.
4. **LeadService** maps entity to LeadDto and returns to controller → 200 OK with LeadDto.

### 7.2 Update deal (stage / IsWon)

1. **DealsController.Update** → `_dealService.UpdateAsync(id, userId, request, ct)`.
2. **DealService.UpdateAsync** → loads entity via `_repository.GetByIdAsync`; sets Stage, ExpectedCloseDateUtc, IsWon from request; `_repository.UpdateAsync(existing, userId, ct)`.
3. **DealRepository.UpdateAsync** → updates entity in memory, `_db.SaveChangesAsync(ct)`.  
   If columns ExpectedCloseDateUtc or IsWon are not in the database, EF may omit them (no column) or throw depending on configuration.
4. Returns DealDto. Frontend sends `stage` and optionally `isWon` (Pipeline page: move to Closed Won/Lost).

### 7.3 Dashboard stats (reporting)

1. **ReportingController.GetDashboardStats** → `_reportingService.GetDashboardStatsAsync(userId, ct)`.
2. **ReportingService.GetDashboardStatsAsync** →  
   - `_db.Leads.CountAsync(l => l.UserId == userId)` → **fails** if Leads table missing.  
   - `_db.Deals.Where(d => d.UserId == userId).ToListAsync()` → loads deals; then in-memory: `activeDeals = deals.Where(d => d.IsWon == null)`, `wonCount = deals.Count(d => d.IsWon == true)`, etc.  
   If Deals table exists but has no IsWon column, EF may not map it (default null); pipeline value is computed from Deal.Value (string parsed as decimal).
3. Returns DashboardStatsDto(ActiveLeadsCount, ActiveDealsCount, PipelineValue, DealsWonCount, DealsLostCount).  
   **Fails** as soon as Leads is queried if migration not applied.

---

## 8. Key user journey flows

### 8.1 New user: sign up → connect → onboarding → dashboard

1. **Register** → POST /api/auth/register → AuthService → UserRepository (insert User, UserSettings) → JWT + user.
2. **Connect** → PUT /api/connection/status { connected: true, accountEmail } → ConnectionService → CrmConnection (upsert by UserId).
3. **Onboarding** → PUT /api/settings { companyName, brandTone } → SettingsService → UserSettings update.
4. **Dashboard** → GET /api/reporting/dashboard, GET /api/copyhistory/stats, GET /api/copyhistory, GET /api/templates, GET /api/connection/status → all hit DB. Reporting fails if Leads table missing.

### 8.2 Create lead → use in pipeline / tasks

1. **Leads page** → POST /api/leads (Name, Email, Phone?, CompanyId?, Source?, Status) → LeadService → LeadRepository.AddAsync → **fails** if Leads table missing.
2. **Pipeline** → GET /api/deals, POST /api/deals, PUT /api/deals/{id} (stage, isWon). Deals table exists; ExpectedCloseDateUtc and IsWon columns missing until migration.
3. **Tasks** → GET /api/tasks?overdueOnly=, POST /api/tasks (LeadId?, DealId?) → **fails** if TaskItems table missing.

### 8.3 Generate copy → send to CRM

1. **Dashboard** → POST /api/copy/generate (CopyTypeId, Goal, Context, Length, CompanyName, BrandTone) → CopyGeneratorService (no DB for generate).
2. **Send to CRM** → POST /api/copy/send (ObjectType, RecordId, RecordName, Copy, CopyTypeLabel) → SendToCrmService → CopyHistoryService (insert CopyHistoryItem) → CopyHistoryRepository → CopyHistoryItems table (exists).

---

## 9. Startup and dependency injection

### 9.1 Program.cs (WebApi)

- **CORS:** Allowed origins from config (default localhost:5173, 3000); AllowCredentials, any header/method.
- **DbContext:** Registered via `AddInfrastructure`; connection string from config or default LocalDB.
- **Auth:** JWT Bearer; secret/issuer/audience from config; Swagger security definition for Bearer.
- **On startup:** `db.Database.MigrateAsync()` (applies pending migrations); `SeedData.SeedAsync(db, passwordHasher)` (ensures one demo user and templates if empty).  
  If a new migration is not applied, `MigrateAsync()` does nothing new; SeedData does not touch Leads, Deals, TaskItems, or Activities.

### 9.2 DependencyInjection (Infrastructure)

- **Repositories:** User, Contact, Deal, Lead, Company, Task, Activity, Template, CopyHistory — all scoped.
- **ReportingService:** Scoped; uses AppDbContext directly (no dedicated repository).
- **Singletons:** IPasswordHasher, ICopyGenerator, ISecretProtector, ITokenService.
- **Application services:** Registered in Program.cs (Auth, Contact, Deal, Lead, Company, Task, Activity, Template, CopyHistory, Settings, Connection, CopyGenerator, SendToCrm).

---

## 10. Gaps and remediation

### 10.1 Summary of gaps

| Gap | Impact | Fix |
|-----|--------|-----|
| **Leads table missing** | LeadsController, LeadService, LeadRepository, ReportingService (dashboard) fail. | Add migration creating Leads with UserId, Name, Email, Phone, CompanyId, Source, Status, CreatedAtUtc; FK to Users and Companies. |
| **TaskItems table missing** | TasksController, TaskService, TaskRepository fail. | Add migration creating TaskItems with UserId, Title, Description, DueDateUtc, Completed, LeadId, DealId, CreatedAtUtc; FK to Users, Leads, Deals. |
| **Activities table missing** | ActivitiesController, ActivityService, ActivityRepository fail. | Add migration creating Activities with UserId, Type, Subject, Body, ContactId, DealId, CreatedAtUtc; FK to Users, Contacts, Deals. |
| **Contacts.Phone column missing** | Contact create/update with Phone not persisted; GET may not return Phone. | Add migration adding Phone (nvarchar 64, nullable) to Contacts. |
| **Deals.ExpectedCloseDateUtc, IsWon missing** | Deal create/update with these fields not persisted; ReportingService IsWon logic wrong. | Add migration adding ExpectedCloseDateUtc (datetime2, nullable), IsWon (bit, nullable) to Deals. |

### 10.2 Steps to fix (single new migration)

1. Stop the running API (so EF tooling can use the startup project).
2. From repo root:
   ```powershell
   cd backend
   dotnet ef migrations add SalesCrmCore --project src/ACI.Infrastructure/ACI.Infrastructure.csproj --startup-project src/ACI.WebApi/ACI.WebApi.csproj
   dotnet ef database update --project src/ACI.Infrastructure/ACI.Infrastructure.csproj --startup-project src/ACI.WebApi/ACI.WebApi.csproj
   ```
3. The new migration will:
   - Create **Leads** (Id, UserId, Name, Email, Phone, CompanyId, Source, Status, CreatedAtUtc; FK UserId, CompanyId).
   - Create **TaskItems** (Id, UserId, Title, Description, DueDateUtc, Completed, LeadId, DealId, CreatedAtUtc; FK UserId, LeadId, DealId).
   - Create **Activities** (Id, UserId, Type, Subject, Body, ContactId, DealId, CreatedAtUtc; FK UserId, ContactId, DealId).
   - Alter **Contacts** add **Phone** (nvarchar(64), nullable).
   - Alter **Deals** add **ExpectedCloseDateUtc** (datetime2, nullable), **IsWon** (bit, nullable).
4. (Optional) Extend **SeedData.SeedAsync** to insert sample Leads, Deals, TaskItems, Activities for the seeded user after ensuring the user exists.

### 10.3 Flow-by-flow checklist (after migration)

| Flow | Backend + DB status | Notes |
|------|---------------------|-------|
| Login / Register / 2FA | ✅ Complete | User, UserSettings, AuthController, AuthService. |
| Settings (brand, 2FA) | ✅ Complete | UserSettings, SettingsController. |
| Connection | ✅ Complete | CrmConnection, ConnectionController. |
| Templates | ✅ Complete | Template, TemplatesController. |
| Generate copy | ✅ Complete | CopyController, CopyGeneratorService (no DB). |
| Copy history | ✅ Complete | CopyHistoryItem, CopyHistoryController. |
| Send to CRM | ✅ Complete | Writes to CopyHistoryItem. |
| Contacts list/search | ✅ Complete | Contact; **add migration for Phone**. |
| Companies | ✅ Complete | Company, CompaniesController. |
| Deals list/create/update/delete | ✅ Complete | Deal; **add migration for ExpectedCloseDateUtc, IsWon**. |
| Leads list/create/update/delete | ✅ Complete | Lead; **add migration for Leads table**. |
| Tasks list/create/update (overdue) | ✅ Complete | TaskItem; **add migration for TaskItems table**. |
| Activities list/create (by contact/deal) | ✅ Complete | Activity; **add migration for Activities table**. |
| Dashboard stats | ✅ Complete | ReportingService; **depends on Leads + Deals columns**. |

---

**Related:** [USER_FLOWS_REPORT.md](USER_FLOWS_REPORT.md) (user journeys), [FRONTEND_PAGES_REPORT.md](FRONTEND_PAGES_REPORT.md) (routes and pages).

**Implementation verification (Jan 2026):**
- **Lead/Deal delete:** `LeadRepository.DeleteAsync` nulls out `TaskItem.LeadId` on linked tasks before removing the lead; `DealRepository.DeleteAsync` nulls out `TaskItem.DealId` and `Activity.DealId` before removing the deal (avoids FK violations).
- **Company create:** `CompanyService.CreateAsync` returns `null` when `request.Name` is null or whitespace; controller returns `BadRequest()` in that case.
- **Builds:** Frontend (`npm run build`) and backend libraries (Domain, Application, Infrastructure) build successfully; WebApi build can fail if the API process is running (file lock).
- **SalesCrmCore migration:** Migration `20260129120000_SalesCrmCore` was added: creates Leads, TaskItems, Activities; adds Contact.Phone, Deal.ContactId, Deal.ExpectedCloseDateUtc, Deal.IsWon. Program.cs runs `MigrateAsync()` on startup, so the migration applies when the API is started (stop API first if you need to rebuild).
- **Frontend–backend alignment:** (1) API client now uses `getAuthToken()` from `lib/auth` (aci_token) so authenticated calls send the token set at login. (2) `createDeal` accepts and sends `contactId`; Pipeline “New deal” form passes `contactId` so deal–contact link is persisted.

**Related:** [USER_FLOWS_REPORT.md](USER_FLOWS_REPORT.md), [FRONTEND_PAGES_REPORT.md](FRONTEND_PAGES_REPORT.md), [PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md) (every aspect).

*Verified against: backend Domain, Application, Infrastructure, WebApi (entities, DTOs, interfaces, services, repositories, configurations, migrations, Program.cs, DependencyInjection); frontend api/* and page usage. Last updated: February 2026.*
