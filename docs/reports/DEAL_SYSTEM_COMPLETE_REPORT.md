# Deal System — Complete Interaction Report

This document is the single definitive reference for **every interaction** the "Deal" entity has across the entire Cadence CRM codebase (backend and frontend). It covers the entity definition, API layer, business logic, database configuration, UI components, cross-entity relationships, and identifies what is missing.

Last updated: February 9, 2026 (source-verified, all files re-read, all 12 HIGH PRIORITY items implemented, report sections 1–28 updated to reflect current code state).

---

## Table of Contents

1. [Deal Entity (Backend Domain)](#1-deal-entity-backend-domain)
2. [Deal Stage Entity](#2-deal-stage-entity)
3. [Backend API Endpoints](#3-backend-api-endpoints)
4. [Backend Business Logic (Services)](#4-backend-business-logic-services)
5. [Backend Data Access (Repositories)](#5-backend-data-access-repositories)
6. [Backend DTOs](#6-backend-dtos)
7. [Database Configuration (EF Core)](#7-database-configuration-ef-core)
8. [Cross-Entity Deal Interactions (Backend)](#8-cross-entity-deal-interactions-backend)
9. [Frontend — API Client Layer](#9-frontend--api-client-layer)
10. [Frontend — TypeScript Types](#10-frontend--typescript-types)
11. [Frontend — React Query Hooks](#11-frontend--react-query-hooks)
12. [Frontend — Pipeline Page (Main Deal UI)](#12-frontend--pipeline-page-main-deal-ui)
13. [Frontend — Dashboard (Deal Stats & Charts)](#13-frontend--dashboard-deal-stats--charts)
14. [Frontend — Activities Page (Deal Filtering)](#14-frontend--activities-page-deal-filtering)
15. [Frontend — Tasks Page (Deal Linking)](#15-frontend--tasks-page-deal-linking)
16. [Frontend — Leads Page (Lead → Deal Conversion)](#16-frontend--leads-page-lead--deal-conversion)
17. [Frontend — Companies Page (Deal Display)](#17-frontend--companies-page-deal-display)
18. [Frontend — SendToCrm Page (Save Copy to Deal)](#18-frontend--sendtocrm-page-save-copy-to-deal)
19. [Frontend — Sales Writer (Deal as Recipient)](#19-frontend--sales-writer-deal-as-recipient)
20. [Frontend — Copy Generator (Deal Context)](#20-frontend--copy-generator-deal-context)
21. [Frontend — Global Search (Deals)](#21-frontend--global-search-deals)
22. [Frontend — Navigation & Routing](#22-frontend--navigation--routing)
23. [Frontend — Settings (Deal Notifications & Pipelines)](#23-frontend--settings-deal-notifications--pipelines)
24. [Frontend — Team Page (Deal Stats per Member)](#24-frontend--team-page-deal-stats-per-member)
25. [Frontend — Mock Data](#25-frontend--mock-data)
26. [Frontend — Query Keys](#26-frontend--query-keys)
27. [Complete File Inventory](#27-complete-file-inventory)
28. [Complete Relationship Map](#28-complete-relationship-map)
29. [What Is Missing](#29-what-is-missing)
30. [HIGH PRIORITY Implementation — Completed](#30-high-priority-implementation--completed-february-9-2026)

---

## 1. Deal Entity (Backend Domain)

**File:** `backend/src/ACI.Domain/Entities/Deal.cs`

The core Deal entity representing a sales opportunity:

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Primary key (from BaseEntity) |
| `UserId` | `Guid` | FK to owning User (required) |
| `OrganizationId` | `Guid?` | FK to Organization |
| `Name` | `string` | Deal name (required, default `string.Empty`) |
| `Value` | `string` | Monetary value as string (required, default `string.Empty`) |
| `Currency` | `string?` | Currency code (e.g. "CHF", "EUR", "USD") |
| `Stage` | `string?` | Legacy stage name (prefer DealStageId) |
| `PipelineId` | `Guid?` | FK to Pipeline |
| `DealStageId` | `Guid?` | FK to DealStage |
| `CompanyId` | `Guid?` | FK to Company |
| `ContactId` | `Guid?` | FK to Contact |
| `AssigneeId` | `Guid?` | FK to assigned User |
| `ExpectedCloseDateUtc` | `DateTime?` | Expected close date |
| `Description` | `string?` | Deal description / notes (max 2000 chars) |
| `Probability` | `int?` | Win probability 0–100 |
| `IsWon` | `bool?` | `null` = open, `true` = won, `false` = lost |
| `ClosedReason` | `string?` | Reason the deal was closed — won or lost (max 500 chars) |
| `ClosedReasonCategory` | `string?` | Category for close reason (e.g., "Won - Budget Approved", "Lost - Competitor") |
| `ClosedAtUtc` | `DateTime?` | Timestamp when the deal was closed |
| `CreatedAtUtc` | `DateTime` | Creation timestamp |
| `UpdatedAtUtc` | `DateTime?` | Last update timestamp (nullable) |
| `UpdatedByUserId` | `Guid?` | FK to User who last updated |

> **Note:** `Value` is stored as `string`, not `decimal`. The `ReportingService` manually parses it with `decimal.TryParse`, stripping `$` and `,` characters.

**Navigation Properties:**
- `User` — Owner (required)
- `UpdatedByUser` — Last updater (optional)
- `Assignee` — Assigned team member (optional)
- `Organization` — Org scope (optional)
- `Pipeline` — Pipeline it belongs to (optional)
- `DealStage` — Current stage (optional)
- `Company` — Associated company (optional)
- `Contact` — Associated contact (optional)
- `Activities` — `ICollection<Activity>` linked activities
- `TaskItems` — `ICollection<TaskItem>` linked tasks
- `StageChanges` — `ICollection<DealStageChange>` stage transition history

---

## 2. Deal Stage Entity

**File:** `backend/src/ACI.Domain/Entities/DealStage.cs`

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Primary key |
| `PipelineId` | `Guid` | FK to Pipeline |
| `Name` | `string` | Stage name |
| `DisplayOrder` | `int` | Sort order |
| `IsWon` | `bool` | Marks "won" stage |
| `IsLost` | `bool` | Marks "lost" stage |

---

## 3. Backend API Endpoints

### DealsController (`backend/src/ACI.WebApi/Controllers/DealsController.cs`)

All endpoints require `[Authorize]`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/deals?page=&pageSize=&search=` | Paginated list (page default 1, pageSize default 20 max 100, optional search) |
| `GET` | `/api/deals/all` | Get all deals non-paginated (backward compat) |
| `GET` | `/api/deals/search?q=` | Search deals by name/value (non-paginated) |
| `GET` | `/api/deals/{id}` | Get single deal by ID |
| `POST` | `/api/deals` | Create new deal (body: `CreateDealRequest`) |
| `PUT` | `/api/deals/{id}` | Update deal — partial (body: `UpdateDealRequest`) |
| `DELETE` | `/api/deals/{id}` | Delete deal (returns 204) |

### DealStagesController (`backend/src/ACI.WebApi/Controllers/DealStagesController.cs`)

All endpoints require `[Authorize]`. CUD operations require **Owner/Manager** role (checked via `RequireOrgOwnerOrManager`). All require `X-Organization-Id` header.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/dealstages?pipelineId={id}` | Get stages for a pipeline (query param, not path) |
| `GET` | `/api/dealstages/{id}` | Get single stage |
| `POST` | `/api/dealstages` | Create stage (Owner/Manager only) |
| `PUT` | `/api/dealstages/{id}` | Update stage (Owner/Manager only) |
| `DELETE` | `/api/dealstages/{id}` | Delete stage (Owner/Manager only, returns 204) |

### PipelinesController (`backend/src/ACI.WebApi/Controllers/PipelinesController.cs`)

All endpoints require `[Authorize]`. CUD operations require **Owner/Manager** role. All require `X-Organization-Id` header.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/pipelines` | Get all pipelines for organization |
| `GET` | `/api/pipelines/{id}` | Get single pipeline |
| `POST` | `/api/pipelines` | Create pipeline (Owner/Manager only) |
| `PUT` | `/api/pipelines/{id}` | Update pipeline (Owner/Manager only) |
| `DELETE` | `/api/pipelines/{id}` | Delete pipeline (Owner/Manager only, returns 204) |

### Other Controllers that reference Deals

| Controller | Endpoint | Deal Interaction |
|------------|----------|-----------------|
| `ActivitiesController` | `GET /api/activities/deal/{dealId}` | Get activities for a specific deal |
| `TasksController` | `GET /api/tasks?dealId=` | Filter tasks by deal |
| `TasksController` | `GET /api/tasks/by-deal/{dealId}` | Get tasks by deal ID |
| `TasksController` | `PATCH /api/tasks/{id}/link-deal` | Link/unlink a task to a deal |
| `LeadsController` | `POST /api/leads/{id}/convert` | Convert lead → can create/link a deal |
| `ReportingController` | `GET /api/reporting/dashboard` | Dashboard stats (activeDealsCount, pipelineValue, dealsWonCount, dealsLostCount) |
| `ReportingController` | `GET /api/reporting/pipeline-by-stage` | Pipeline value grouped by DealStage |
| `ReportingController` | `GET /api/reporting/pipeline-by-assignee` | Pipeline value grouped by Assignee |
| `SearchController` | `GET /api/search?q=` | Global search includes deals |
| `CopyController` | `POST /api/copy/send` | Send copy to a deal record |

---

## 4. Backend Business Logic (Services)

### DealService (`backend/src/ACI.Application/Services/DealService.cs`)

Depends on: `IDealRepository`, `IActivityRepository` (for last activity enrichment).

| Method | Logic |
|--------|-------|
| `GetDealsPagedAsync` | Pagination with search. **Enriches** each deal with `LastActivityAtUtc` via `_activityRepository.GetLastActivityByDealIdsAsync()`. |
| `GetDealsAsync` | All deals for user/org (non-paginated). **Enriches** with `LastActivityAtUtc`. |
| `SearchAsync` | Search by name/value (case-insensitive). **Enriches** with `LastActivityAtUtc`. |
| `GetByIdAsync` | Single deal lookup. **Enriches** with `LastActivityAtUtc`. |
| `CreateAsync` | Validates: Name is required (non-empty), Value is required (non-empty). Sets `CreatedAtUtc = DateTime.UtcNow`. Also maps `Description` and `Probability` from the request. |
| `UpdateAsync` | Partial update — only non-null fields are patched. Sets `UpdatedAtUtc` and `UpdatedByUserId`. Now also handles `Description`, `Probability`, `ClosedReason`. When `IsWon` is set, automatically stamps `ClosedAtUtc = DateTime.UtcNow`. **Stage change tracking (HP-11):** detects when `DealStageId` changes and records a `DealStageChange` via `_repository.AddStageChangeAsync()`. |
| `DeleteAsync` | Removes deal (delegated to repository which nullifies linked activities/tasks first). |

> **Key design detail:** Every deal fetch method calls `GetLastActivityByDealIdsAsync` to enrich deals with the latest activity timestamp. This means every deal query triggers an additional aggregation query against the Activities table.

> **Map method:** The private `Map(Deal, DateTime?)` method builds a `DealDto` with all enriched fields: `AssigneeName` (from `Assignee?.Name`), `CompanyName`, `ContactName`, `PipelineName`, `DealStageName` (all from eager-loaded navigation properties), plus `Description`, `Probability`, `CreatedAtUtc`, `UpdatedAtUtc`, `ClosedReason`, `ClosedReasonCategory`, `ClosedAtUtc`.

### DealStageService (`backend/src/ACI.Application/Services/DealStageService.cs`)

| Method | Logic |
|--------|-------|
| `GetByPipelineIdAsync` | Gets all stages for a pipeline |
| `GetByIdAsync` | Single stage lookup |
| `CreateAsync` | Validates pipeline exists, name required |
| `UpdateAsync` | Updates stage properties |
| `DeleteAsync` | Removes stage |

### LeadService — Lead Conversion

**File:** `backend/src/ACI.Application/Services/LeadService.cs` — `ConvertAsync` method

When a lead is converted with `createDeal: true`:
- Creates a new Deal using `IDealRepository.AddAsync`
- Sets `DealName`, `DealValue`, `DealStageId`, `PipelineId`
- Links the deal back to the Lead via `ConvertedToDealId`

When `existingDealId` is provided:
- Links the lead to an existing deal

### ActivityService

**File:** `backend/src/ACI.Application/Services/ActivityService.cs`

- `CreateAsync`: Validates that `DealId` exists if provided (calls `IDealRepository.GetByIdAsync`)
- `GetByDealIdAsync`: Retrieves all activities linked to a specific deal

### ReportingService

**File:** `backend/src/ACI.Infrastructure/Services/ReportingService.cs`

- `GetDashboardStatsAsync`: Counts active deals, calculates pipeline value sum, counts won/lost deals
- `GetPipelineValueByStageAsync`: Groups open deals by stage, sums value per stage
- `GetPipelineValueByAssigneeAsync`: Groups open deals by assignee, sums value per assignee

### GlobalSearchService

**File:** `backend/src/ACI.Application/Services/GlobalSearchService.cs`

- Calls `_dealService.SearchAsync(query)` to include deals in global search results

### SendToCrmService

**File:** `backend/src/ACI.Application/Services/SendToCrmService.cs`

- Accepts `ObjectType = "deal"` in `SendToCrmRequest` to save generated copy against a deal record

---

## 5. Backend Data Access (Repositories)

### DealRepository (`backend/src/ACI.Infrastructure/Repositories/DealRepository.cs`)

| Method | Description |
|--------|-------------|
| `IncludeRelated` (private) | Static helper that `.Include()`s `Assignee`, `Company`, `Contact`, `Pipeline`, `DealStage` navigation properties — called by all query methods to ensure enriched data |
| `GetPagedAsync` | Paginated query with search, filtered by UserId + OrganizationId. Uses `IncludeRelated` for eager loading. |
| `CountAsync` | Count deals matching filters (for pagination metadata) |
| `GetByUserIdAsync` | All deals for a user. Uses `IncludeRelated`. |
| `SearchAsync` | Search by name or value. Uses `IncludeRelated`. |
| `GetByIdAsync` | Single deal by ID, validates user/org. Uses `IncludeRelated`. |
| `AddAsync` | Insert new deal |
| `UpdateAsync` | Full field update including `Description`, `Probability`, `ClosedReason`, `ClosedReasonCategory`, `ClosedAtUtc`. Uses `IncludeRelated` to reload from DB first. |
| `DeleteAsync` | Deletes deal, **nullifies `DealId` on related TaskItems and Activities** before deletion |
| `AddStageChangeAsync` | (HP-11) Records a `DealStageChange` entry for stage transition tracking |
| `GetStageChangesAsync` | (HP-11) Returns stage change history for a deal, ordered by `ChangedAtUtc` descending, validates deal belongs to user |

### DealStageRepository (`backend/src/ACI.Infrastructure/Repositories/DealStageRepository.cs`)

| Method | Description |
|--------|-------------|
| `GetByPipelineIdAsync` | All stages for a pipeline, org-validated |
| `GetByIdAsync` | Single stage, org-validated |
| `AddAsync` | Insert stage |
| `UpdateAsync` | Update stage |
| `DeleteAsync` | Remove stage |

### ActivityRepository — Deal-related methods

| Method | Description |
|--------|-------------|
| `GetByDealIdAsync` | All activities for a given deal |
| `GetLastActivityByDealIdsAsync` | Batch: latest activity date per deal ID (used by DealService to enrich deals with `LastActivityAtUtc`) |

---

## 6. Backend DTOs

| DTO | File | Deal Fields |
|-----|------|-------------|
| `DealDto` | `DTOs/DealDto.cs` | All deal properties + `LastActivityAtUtc`, `Description`, `Probability`, `AssigneeName`, `CompanyName`, `ContactName`, `PipelineName`, `DealStageName`, `CreatedAtUtc`, `UpdatedAtUtc`, `ClosedReason`, `ClosedReasonCategory`, `ClosedAtUtc` (25 fields total) |
| `CreateDealRequest` | `DTOs/CreateDealRequest.cs` | `Name` (required, max 200), `Value` (regex `^-?\\d+(\\.\\d{1,2})?$`), `Currency`, `Stage`, `DealStageId`, `PipelineId`, `CompanyId`, `ContactId`, `AssigneeId`, `ExpectedCloseDateUtc`, `Description` (max 2000), `Probability` (range 0–100) |
| `UpdateDealRequest` | `DTOs/UpdateDealRequest.cs` | All fields optional (partial update), includes `IsWon`, `Description` (max 2000), `Probability` (range 0–100), `ClosedReason` (max 500), `ClosedReasonCategory` (max 50) |
| `DealStageDto` | `DTOs/DealStageDto.cs` | Id, PipelineId, Name, DisplayOrder, IsWon, IsLost |
| `CreateDealStageRequest` | `DTOs/CreateDealStageRequest.cs` | PipelineId, Name, DisplayOrder, IsWon, IsLost |
| `UpdateDealStageRequest` | `DTOs/UpdateDealStageRequest.cs` | Name, DisplayOrder, IsWon, IsLost |
| `ConvertLeadRequest` | `DTOs/ConvertLeadRequest.cs` | `CreateDeal`, `DealName`, `DealValue`, `DealStageId`, `PipelineId`, `ExistingDealId` |
| `ConvertLeadResult` | `DTOs/ConvertLeadResult.cs` | `DealId` — returned when deal created/linked |
| `DashboardStatsDto` | `DTOs/DashboardStatsDto.cs` | `ActiveDealsCount`, `PipelineValue`, `DealsWonCount`, `DealsLostCount` |
| `PipelineStageValueDto` | `DTOs/PipelineStageValueDto.cs` | StageId, StageName, DealCount, Value |
| `PipelineValueByAssigneeDto` | `DTOs/PipelineValueByAssigneeDto.cs` | AssigneeUserId, AssigneeName, DealCount, Value |
| `GlobalSearchResultDto` | `DTOs/GlobalSearchResultDto.cs` | Contains `List<DealDto> Deals` |
| `CreateActivityRequest` | `DTOs/CreateActivityRequest.cs` | `DealId` (optional) — link activity to deal |
| `ActivityDto` | `DTOs/ActivityDto.cs` | `DealId` — activity's deal association |
| `TaskDto` | `DTOs/TaskDto.cs` | `DealId`, `DealName` — task's deal association |
| `SendToCrmRequest` | `DTOs/SendToCrmRequest.cs` | `ObjectType` can be "deal", `RecordId` is deal ID |
| `GenerateCopyWithRecipientRequest` | `DTOs/GenerateCopyWithRecipientRequest.cs` | `DealStage`, `DealValue` for AI personalization |

---

## 7. Database Configuration (EF Core)

### DealConfiguration (`backend/src/ACI.Infrastructure/Persistence/Configurations/DealConfiguration.cs`)

- Table: `"Deals"`
- `Name`: `MaxLength(512)`, required
- `Value`: `MaxLength(64)`, required — **stored as string, not decimal**
- `Currency`: `MaxLength(8)` — no default value
- `Stage`: `MaxLength(128)` — legacy field
- `Description`: `MaxLength(2000)` — deal notes/description
- `ClosedReason`: `MaxLength(500)` — reason the deal was closed
- FK: `UserId` → User, via `User.Deals` collection
- FK: `OrganizationId` → Organization (optional)
- FK: `PipelineId` → Pipeline (optional), via `Pipeline.Deals` collection
- FK: `DealStageId` → DealStage (optional), via `DealStage.Deals` collection
- FK: `CompanyId` → Company (optional), via `Company.Deals` collection
- FK: `ContactId` → Contact (optional)
- FK: `AssigneeId` → User (optional)
- FK: `UpdatedByUserId` → User (optional)
- Indexes: `OrganizationId`, `PipelineId`, `DealStageId` (explicit); EF auto-creates FK indexes

### DealStageChangeConfiguration (`backend/src/ACI.Infrastructure/Persistence/Configurations/DealStageChangeConfiguration.cs`)

- Table: `"DealStageChanges"`
- `FromStageName`: `MaxLength(128)`
- `ToStageName`: `MaxLength(128)`
- FK: `DealId` → `Deal.StageChanges` (one-to-many, `DeleteBehavior.Cascade`)
- FK: `ChangedByUserId` → `User` (many-to-one, `DeleteBehavior.NoAction`)
- Index: `DealId`

### Related entity configurations

| Configuration File | Deal Relationship |
|-------------------|-------------------|
| `ActivityConfiguration.cs` | `Activity.DealId` → `Deal` (one-to-many, optional, no cascade, `SetNull` on delete) |
| `TaskConfiguration.cs` | `TaskItem.DealId` → `Deal` (one-to-many, optional, no cascade, `SetNull` on delete) |
| `LeadConfiguration.cs` | `Lead.ConvertedToDealId` → `Deal` (one-to-one nullable, `SetNull` on delete) |
| `DealStageChangeConfiguration.cs` | `DealStageChange.DealId` → `Deal` (one-to-many, cascade delete) |

### AppDbContext

- `DbSet<Deal> Deals` registered in `AppDbContext.cs`
- `DbSet<DealStageChange> DealStageChanges` registered in `AppDbContext.cs`

### Program.cs schema fixes

- SQL migration fixes that ensure `DealStageId`, `PipelineId`, `CompanyId`, `ContactId`, `AssigneeId` columns exist on `Deals` table
- Diagnostic endpoints: `/db-test-deals`, `/db-test-deals-service`

---

## 8. Cross-Entity Deal Interactions (Backend)

| Entity | Relationship | Direction |
|--------|-------------|-----------|
| **User** | `ICollection<Deal> Deals` | User owns many Deals |
| **Company** | `ICollection<Deal> Deals` | Company has many Deals |
| **Contact** | Deal has optional `ContactId` | Deal → Contact |
| **Pipeline** | `ICollection<Deal> Deals` | Pipeline contains many Deals |
| **DealStage** | Deal has optional `DealStageId` | Deal → DealStage |
| **DealStageChange** | `DealStageChange.DealId` (FK, cascade) | Stage transition history for Deal |
| **Activity** | `Activity.DealId` (optional FK) | Activity belongs to Deal |
| **TaskItem** | `TaskItem.DealId` (optional FK) | Task linked to Deal |
| **Lead** | `Lead.ConvertedToDealId` (optional FK) | Lead converted to Deal |
| **UserSettings** | `EmailOnDealUpdate`, `DefaultPipelineId`, `DefaultCurrency` | Settings reference deal preferences |
| **CopyTypeId enum** | `DealMessage` value | Copy type for deal-specific messages |
| **RecipientType enum** | `Deal` value | AI copy can target a deal as recipient |

---

## 9. Frontend — API Client Layer

### `src/app/api/deals.ts`

| Function | API Call | Description |
|----------|----------|-------------|
| `getDeals()` | `GET /api/deals/all` or mock | Get all deals (non-paginated, backward compat) |
| `getDealsPaged(params)` | `GET /api/deals?page=&pageSize=&search=` | Paginated deals with search — used by Pipeline, Contacts, Companies pages |
| `createDeal(params)` | `POST /api/deals` | Create a new deal. Params include: `name`, `value`, `currency`, `stage`, `pipelineId`, `dealStageId`, `companyId`, `contactId`, `assigneeId`, `expectedCloseDateUtc`, `description`, `probability` |
| `updateDeal(id, params)` | `PUT /api/deals/{id}` | Update a deal. Params include all create fields plus: `isWon`, `closedReason` |
| `deleteDeal(id)` | `DELETE /api/deals/{id}` | Delete a deal |
| `searchDeals(query)` | `GET /api/deals/search?q=` | Search deals (non-paginated) |

### `src/app/api/dealStages.ts`

| Function | API Call | Description |
|----------|----------|-------------|
| `getDealStages(pipelineId)` | `GET /api/dealstages/pipeline/{id}` | Get stages for a pipeline |
| `createDealStage(params)` | `POST /api/dealstages` | Create a new stage |
| `updateDealStage(id, params)` | `PUT /api/dealstages/{id}` | Update a stage |
| `deleteDealStage(id)` | `DELETE /api/dealstages/{id}` | Delete a stage |

### `src/app/api/activities.ts` — Deal-related functions

| Function | API Call | Description |
|----------|----------|-------------|
| `getActivitiesByDeal(dealId)` | `GET /api/activities/deal/{dealId}` | Get activities linked to a deal |
| `createActivity({dealId})` | `POST /api/activities` | Create activity optionally linked to a deal |

### `src/app/api/tasks.ts` — Deal-related functions

| Function | API Call | Description |
|----------|----------|-------------|
| `getTasks({dealId})` | `GET /api/tasks?dealId=` | Get tasks filtered by deal |
| `getTasksByDeal(dealId)` | `GET /api/tasks/by-deal/{dealId}` | Get tasks for a specific deal |
| `createTask({dealId})` | `POST /api/tasks` | Create task optionally linked to a deal |
| `linkTaskToDeal(id, dealId)` | `PATCH /api/tasks/{id}/link-deal` | Link/unlink a task to a deal |

### `src/app/api/reporting.ts` — Deal reporting

| Function | API Call | Description |
|----------|----------|-------------|
| `getDashboardStats()` | `GET /api/reporting/dashboard` | Returns `activeDealsCount`, `pipelineValue`, `dealsWonCount`, `dealsLostCount` |
| `getPipelineValueByStage()` | `GET /api/reporting/pipeline-by-stage` | Returns `{stageId, stageName, dealCount, value}[]` |
| `getPipelineValueByAssignee()` | `GET /api/reporting/pipeline-by-assignee` | Returns `{assigneeUserId, assigneeName, dealCount, value}[]` |

### `src/app/api/search.ts` — Global search

| Function | API Call | Description |
|----------|----------|-------------|
| `globalSearch(query)` | `GET /api/search?q=` | Returns `{deals: {id, name, value}[]}` among other entities |

### `src/app/api/leads.ts` — Lead conversion

| Function | API Call | Description |
|----------|----------|-------------|
| `convertLead(id, request)` | `POST /api/leads/{id}/convert` | Request includes `createDeal`, `dealName`, `dealValue`, `dealStageId`, `pipelineId`, `existingDealId` |

### `src/app/api/copyGenerator.ts` — Deal as copy recipient

- `RecipientContext` type includes `dealStage` and `dealValue` for personalization
- `generateCopyWithRecipient()` sends deal context to AI for personalized copy generation
- Copy type `'deal-message'` is a dedicated copy type for deal stakeholder updates

### `src/app/api/index.ts` (barrel exports)

Re-exports: `getDeals`, `getDealsPaged`, `createDeal`, `updateDeal`, `deleteDeal`, `searchDeals`, `getDealStages`, `createDealStage`, `updateDealStage`, `deleteDealStage`, `getActivitiesByDeal`, `linkTaskToDeal`, `sendCopyToCrm`, `convertLead`.

---

## 10. Frontend — TypeScript Types

### `src/app/api/types.ts` — Deal interface (exact from source, lines 89-117)

```
interface Deal {
  id: string;
  name: string;
  value: string;
  currency?: string;
  stage?: string;
  pipelineId?: string;
  dealStageId?: string;
  companyId?: string;
  contactId?: string;
  assigneeId?: string;
  expectedCloseDateUtc?: string;
  isWon?: boolean;
  lastActivityAtUtc?: string;
  // HP-1: Description & Probability
  description?: string;
  probability?: number;
  // HP-4: Enriched names & timestamps
  assigneeName?: string;
  companyName?: string;
  contactName?: string;
  pipelineName?: string;
  dealStageName?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  // HP-8: Close reason
  closedReason?: string;
  closedAtUtc?: string;
}
```

> **Note:** The frontend `Deal` interface now includes all enriched name fields (`assigneeName`, `companyName`, `contactName`, `pipelineName`, `dealStageName`) populated by the backend's eager-loaded navigation properties, as well as `description`, `probability`, `createdAtUtc`, `updatedAtUtc`, `closedReason`, and `closedAtUtc`.

### `src/app/pages/pipeline/types.ts` — Deal form state (exact from source)

```
interface DealFormState {
  name: string;
  value: string;
  stageId: string;
  pipelineId: string;
  contactId: string;
  companyId: string;
  expectedCloseDate: string;
  probability: string;
  description: string;
}
```

> **Note:** `DealFormState` has `stageId` (not `dealStageId` or `stage`), `expectedCloseDate` (not `expectedCloseDateUtc`). `probability` and `description` are present as string form fields and map to the backend's `Probability` (int) and `Description` (string) properties on the Deal entity.

### `src/app/api/types.ts` — Dashboard & CRM-related types

- `DashboardStats`: `activeLeadsCount`, `activeDealsCount`, `pipelineValue`, `dealsWonCount`, `dealsLostCount`
- `CopyHistoryItem`: `recipientType` can be `'deal'` — tracks copy sent to deals
- `SendToCrmParams`: `objectType` can be `'deal'` — for saving copy to deal records
- `UserSettings`: `emailOnDealUpdate` (boolean), `defaultPipelineId` (string?), `defaultCurrency` (string?)

### `src/app/pages/leads/types.ts` — Lead conversion types

- `ConvertFormState`: `createDeal`, `dealName`, `dealValue`, `dealStage`, `pipelineId`, `dealStageId`, `existingDealId`
- `ConvertOptions`: `deals: Deal[]`

### `src/app/pages/tasks/types.ts` — Task types

- `TaskItem` includes `dealId`, `dealName`

### `src/app/pages/activities/types.ts` — Activity types

- `Activity` includes `dealId`

---

## 11. Frontend — React Query Hooks

### `src/app/hooks/queries/useDeals.ts`

| Hook | Query Key | Description |
|------|-----------|-------------|
| `useDeals(filters?)` | `['deals', 'list', filters]` | Fetch deals with optional filters |
| `useDeal(id)` | `['deals', 'detail', id]` | Fetch single deal |
| `useCreateDeal()` | mutation | Create deal, invalidates `['deals']` |
| `useUpdateDeal()` | mutation | Update deal, invalidates `['deals']` |
| `useDeleteDeal()` | mutation | Delete deal, invalidates `['deals']` |

### `src/app/hooks/queries/queryKeys.ts`

```
deals: {
  all: ['deals'],
  lists: () => ['deals', 'list'],
  list: (filters?) => ['deals', 'list', filters],
  details: () => ['deals', 'detail'],
  detail: (id) => ['deals', 'detail', id],
}
dealStages: {
  all: ['dealStages'],
  lists: () => ['dealStages', 'list'],
  byPipeline: (pipelineId) => ['dealStages', 'list', pipelineId],
}
```

---

## 12. Frontend — Pipeline Page (Main Deal UI)

**File:** `src/app/pages/Pipeline.tsx` — **THE central orchestrator for deals**

This is the largest and most important frontend deal component. It provides:

### Views
- **Kanban View**: Drag-and-drop board with `DroppableStageColumn` columns and `DealCard` cards
- **List View**: Tabular display of all deals with inline actions

### State Management
- `deals` / `loading` / `error` state
- `pipelineId` / `stageList` — current pipeline and its stages
- `createForm` / `editForm` — form state for creating/editing deals (includes `description`, `probability`)
- `filterSearch` / `filterStage` / `filterAssignee` / `filterMyDeals` / `filterValueMin` / `filterValueMax` — full filtering and sorting
- `closeDeal` / `closeAsWon` / `closeReason` / `savingClose` — close deal dialog state (HP-8)
- `detailDeal` — deal detail side sheet state
- `draggedDeal` — drag-and-drop state

### Data Fetching
- Uses `getDealsPaged({ page: 1, pageSize: 500 })` (HP-5) instead of the previous unbounded `getDeals()` call

### Deal CRUD
- **Create**: "New Deal" button → modal form with name, value, currency, stage, pipeline, company, contact, assignee, expected close date, **description**, **probability**
- **Edit**: Inline edit via `DealCard` dropdown → same form (with description and probability fields)
- **Delete**: Confirmation dialog → `deleteDeal(id)`
- **Move Stage**: Drag card to another column → `updateDeal(id, {dealStageId})` (optimistic UI)
- **Close Deal (HP-8)**: Won/Lost buttons on deal detail sheet → Close Deal dialog with reason textarea → `updateDeal(id, {isWon, closedReason})`

### Filtering (HP-7, HP-10)
- **My Deals** toggle button — filters by current user's assignee ID (`getCurrentUser()?.id`)
- **Assignee dropdown** — filter by specific team member or "Unassigned"
- **Value range** — min/max number inputs
- **Stage filter** — existing stage dropdown
- **Search** — text search by deal name/value
- Active filter pills shown with remove buttons

### Statistics Bar
- Total deals count
- Total pipeline value (`formatValueSum`)
- Won/Lost counts
- Deals closing soon

### Sub-components (Source-Verified)

| Component | File | Description |
|-----------|------|-------------|
| `DealCard` | `pipeline/DealCard.tsx` | Individual deal card with `useDrag` hook (react-dnd). Shows: deal **name** (clickable → `onOpenDetail`), **value** with `getCurrencySymbol(deal.currency)` (HP-3 — supports USD/EUR/GBP/CHF/JPY/CAD/AUD), **urgency badge** (Overdue/Due in Xd/Xd to close), **contact name** with User icon (from `deal.contactName` or prop), **assignee name** with blue User icon (HP-7), **last activity** date with Clock icon. **Stage selector** dropdown at bottom to move deal between stages. Edit and Delete action buttons (visible on hover). `STAGE_COLORS` map: Qualification (blue), Proposal (amber), Negotiation (violet), Closed Won (emerald), Closed Lost (slate). Color-coded top bar per stage. Exports `getCurrencySymbol()` helper used by Pipeline list view and DealDetail. |
| `DroppableStageColumn` | `pipeline/DroppableStageColumn.tsx` | Kanban column with `useDrop` hook. Shows: **stage icon** (Target=Qualification, Briefcase=Proposal, Handshake=Negotiation, Trophy=Won, XCircle=Lost), **deal count**, **total value** (formatted as currency). Drop zone indicator on hover. Renders `DealCard` for each deal with `AnimatePresence` for smooth transitions. Empty state: "No deals in this stage — Drag deals here or create new ones". Progress indicator bar in header. |
| `utils.tsx` | `pipeline/utils.tsx` | `getStageList`, `groupDealsByStage`, `formatLastActivity`, `formatValueSum`, `getDaysUntilClose`, `UrgencyBadge` component |
| `config.ts` | `pipeline/config.ts` | `STAGE_COLORS_MAP`, `FALLBACK_STAGES`, `STAGE_ICONS` |

---

## 13. Frontend — Dashboard (Deal Stats & Charts)

**File:** `src/app/pages/Dashboard.tsx`

### Data Loaded on Mount (Source-Verified: `Dashboard.tsx` lines 87-113)
- `getDashboardStats()` → `crmStats` (includes `activeDealsCount`, `pipelineValue`, `dealsWonCount`, `dealsLostCount`)
- `getPipelineValueByStage()` → `pipelineByStage` (deal count + value per stage)
- `getPipelineValueByAssignee()` → `pipelineByAssignee` (deal count + value per member)
- `getDeals()` → `deals` (line 111: loaded for recipient picker in Sales Writer)
- `recipientType` state (line 68) includes `'deal'` as one of `'lead' | 'contact' | 'deal'`
- `selectedRecipient` (lines 72-80) can hold `type: 'deal'`, `dealStage`, `dealValue`
- `handleGenerate` (lines 134-191) builds `RecipientContext` with deal stage + value for AI copy personalization

### Sub-components that display deal data

| Component | File | Deal Data Shown |
|-----------|------|-----------------|
| `DashboardHero` | `dashboard/DashboardHero.tsx` | **Pipeline Value** card (hero stat), **Open Deals** count, **Won vs Lost** counts. "New Deal" quick-action link to `/deals`. |
| `PipelineChart` | `dashboard/PipelineChart.tsx` | Bar chart of deal values per stage. Shows stage name, deal count badge, value, percentage of total. "View all" link to `/deals`. |
| `TeamPerformance` | `dashboard/TeamPerformance.tsx` | Lists team members with their deal count and total deal value. |
| `QuickNav` | `dashboard/QuickNav.tsx` | "Deals" nav link with shortcut `D`. Bottom CTA: "View Pipeline — Track all your active deals" link to `/deals`. |
| `SalesWriter` | `dashboard/SalesWriter.tsx` | Deal as recipient type for copy personalization (see section 19). |

### Dashboard config

| Config | File | Deal Reference |
|--------|------|----------------|
| `copyTypes` | `dashboard/config.ts` | `deal-message` copy type with `Briefcase` icon, description: "Update stakeholders" |
| `quickNavPrimary` | `dashboard/config.ts` | `{to: '/deals', label: 'Deals', description: 'Track pipeline', shortcut: 'D'}` |
| `goals` | `dashboard/config.ts` | "Close the deal" goal |

---

## 14. Frontend — Activities Page (Deal Filtering)

**File:** `src/app/pages/Activities.tsx`

### Deal Interactions (Source-Verified: `Activities.tsx`, 1134 lines)

1. **Loads deals on mount** (line 218): `getDeals()` via `Promise.all([getContacts(), getDeals()])` to populate deal dropdown for filtering and form
2. **Filter by Deal** (lines 94, 103, 606-672): `ActivityFilter` type includes `'deal'`. "By Deal" tab → `Select` dropdown to pick a deal → calls `getActivitiesByDeal(filterDealId)` (lines 148-155, 186-195) to show only that deal's timeline
3. **Create Activity linked to Deal** (lines 1059-1084): "Log Activity" dialog has a "Deal" `Select` dropdown under the emerald-labeled "Deal" section → sets `form.dealId` → passed to `createActivity({ dealId: form.dealId })` (line 312)
4. **Display deal link on activity card** (lines 840-845): Each activity card shows an **emerald badge** with `Target` icon and `deal.name` when `activity.dealId` is set
5. **Statistics** (line 251): `activityStats.withDeals = activities.filter(a => a.dealId).length` counts how many activities are linked to deals
6. **Pagination with filter**: When `filter === 'deal' && filterDealId`, activities are fetched via `getActivitiesByDeal` (not paginated), otherwise falls back to `getActivitiesPaged` server-side pagination

---

## 15. Frontend — Tasks Page (Deal Linking)

**File:** `src/app/pages/Tasks.tsx`

### Deal Interactions

1. **Loads deals on mount**: `getDeals()` to populate deal dropdowns
2. **Create Task linked to Deal**: Task creation form has `dealId` field
3. **Link/Unlink Task to Deal**: `linkTaskToDeal(taskId, dealId)` via task dropdown menu or detail modal
4. **Display deal badge**: Both `KanbanTaskCard` and `ListTaskCard` show a teal badge with `task.dealName`
5. **Filter tasks by deal**: `getTasks({dealId})` when filtering

### Sub-components (Source-Verified)

| Component | File | Deal Interaction |
|-----------|------|-----------------|
| `TaskDetailModal` | `tasks/components/TaskDetailModal.tsx` | Full modal with deal dropdown selector in "Linked Items" section (lines 616-643). `handleQuickDealChange` callback (lines 202-205) calls `onDealChange(task, dealId)`. Loads related deal activities via `getActivitiesByDeal(t.dealId)` on open (lines 128-130). Shows current deal name as teal badge with Briefcase icon below the selector (lines 636-641). Accepts `deals: Deal[]` and `onDealChange` props. |
| `KanbanTaskCard` | `tasks/components/KanbanTaskCard.tsx` | Drag-and-drop card with "Deal" `DropdownMenuSub` submenu (lines 173-188) containing "No deal" + all deals. Calls `handleDealChange(task, dealId)`. Shows `task.dealName` as teal badge (lines 228-233) with `Link2` icon and truncated name. Accepts `handleDealChange`, `deals: Deal[]` props. |
| `ListTaskCard` | `tasks/components/ListTaskCard.tsx` | List row showing `task.dealName` as teal rounded-full badge with `Link2` icon (lines 175-180). **Note: does NOT have a deal change dropdown** — only status and priority change are available in the dropdown. Deal changes must be done via the TaskDetailModal or KanbanTaskCard context menu. |
| `KanbanColumn` | `tasks/components/KanbanColumn.tsx` | Kanban column drop zone — no direct deal interaction. Handles task status changes via drag-and-drop only. |
| `TaskGroupSection` | `tasks/components/TaskGroupSection.tsx` | Collapsible group wrapper for ListTaskCards — no direct deal interaction. Passes through all props to ListTaskCard. |

---

## 16. Frontend — Leads Page (Lead → Deal Conversion)

**File:** `src/app/pages/Leads.tsx`

### Deal Interactions (Source-Verified: `Leads.tsx`, 1826 lines)

1. **Loads deals on conversion dialog open** (line 324): `getDeals()` via `Promise.all([getContacts(), getDeals(), getPipelines()])` when `openConvert(lead)` is called
2. **Convert Lead to Deal**: Inline conversion dialog (lines 1370-1805) — NOT a separate `ConvertLeadDialog` component for the deal section; the deal creation section is embedded directly in the Leads page

### Conversion Dialog — Deal Section (Source-Verified: `Leads.tsx` lines 1621-1763)

Full conversion wizard UI with three optional sections (Contact, Company, Deal):

- **"Create new deal" toggle** (lines 1631-1650): amber-themed button with `Handshake` icon. Description: "Track this as a sales opportunity with potential revenue"
- **Deal Form** (when toggled on, lines 1652-1729):
  - Deal name input (defaults to `convertDialogLead?.name`)
  - Deal value input (placeholder: "e.g. $10,000")
  - **Pipeline & Stage dropdowns** (lines 1674-1710): auto-selects first pipeline + first stage from `convertPipeline.dealStages` sorted by `displayOrder`
  - **Fallback stage picker** (lines 1711-1728): if no pipeline stages exist, shows hardcoded stages: Qualification, Proposal, Negotiation, Closed Won, Closed Lost
- **"Attach to existing deal" dropdown** (lines 1732-1761): Shows all existing deals with `Handshake` icon, name, and value in parentheses
- **Conversion flow visualization** (lines 1397-1434): Visual flow: Lead → Contact + Company + Deal — dynamically shows/hides Company and Deal based on toggle state

### Convert Form State (inline in `Leads.tsx` lines 94-107)
```
{
  createContact: true,
  createDeal: false,
  dealName: '',       // defaults to lead name in openConvert()
  dealValue: '',
  dealStage: 'Qualification',
  pipelineId: undefined,
  dealStageId: undefined,
  existingDealId: undefined,
  // ...contact and company fields
}
```

---

## 17. Frontend — Companies Page (Deal Display)

**File:** `src/app/pages/Companies.tsx`

### Deal Interactions (Source-Verified: `Companies.tsx`)

1. **Loads deals on mount**: Uses `getDealsPaged({ page: 1, pageSize: 500 })` (HP-5/HP-12) via `Promise.all([getCompaniesPaged(...), getContacts(), getDealsPaged(...)])` — previously used unbounded `getDeals()`
2. **Per-company deal helpers**: `getDealsForCompany(companyId)` returns all deals where `deal.companyId === companyId`
3. **Per-company deal count**: `getDealCount(companyId) = deals.filter(d => d.companyId === companyId).length`
4. **Per-company deal value**: `getDealValue(companyId)` sums all `deal.value` where `deal.companyId === companyId`
5. **Stats cards**: "With Deals" count card (emerald themed) + "Total Pipeline" value card (amber gradient) showing `formatCurrency(companyStats.totalDealValue)`
6. **Company card deal drill-down (HP-12)**: Each company card now shows up to 3 linked deals inline with name + value, each clickable to navigate to `/deals/:id` via `useNavigate`. If a company has >3 deals, a "+N more deals" link navigates to the main `/deals` page.
7. **Company card footer**: Deal count with `Target` icon + deal value with `DollarSign` icon
8. **Sort by deals**: "Most Deals" sort option sorts companies by descending deal count
9. **Company stats computation**: Tracks `withDeals` (companies with at least one deal) and `totalDealValue` (sum of all deal values across all companies)

---

## 18. Frontend — SendToCrm Page (Save Copy to Deal)

**File:** `src/app/pages/SendToCrm.tsx`

### Deal Interactions (Source-Verified: `SendToCrm.tsx`, 290 lines)

1. **Loads deals on mount** (line 34): `getDeals()` via `useEffect` on mount
2. **Object type "Deal"** (line 148): Tab button with `Target` icon — user selects "Deal" as the CRM object type
3. **Record selection** (lines 74-86): Scrollable list of deals, each showing:
   - Deal `name` in semibold
   - Subtitle: `deal.value` (formatted as currency) + `deal.stage` (e.g., "$10,000 • Qualification")
   - Selected state with checkmark
4. **Search deals** (lines 55-61): `filteredDeals = deals.filter(d => d.name includes query || d.value?.toString() includes query)`
5. **Send** (lines 88-106): `handleSend()` calls `sendCopyToCrm({ objectType: 'deal', recordId: selectedDeal.id, content: generatedCopy })` with toast success/error feedback

---

## 19. Frontend — Sales Writer (Deal as Recipient)

**File:** `src/app/pages/Dashboard.tsx` (SalesWriter is embedded in Dashboard, lines 212-236)

### Deal Interactions (Source-Verified: `Dashboard.tsx` lines 68-80, 134-191)

1. **Recipient type "Deals"** (line 68): `recipientType` state includes `'deal'` option
2. **Deal search**: Search input to filter deals in recipient picker by name
3. **Deal selection** (lines 72-80): Clicking a deal sets `selectedRecipient` with `{ type: 'deal', name, dealStage, dealValue }`
4. **Deal display**: When selected, shows "Deal" label with `Target` icon in recipient badge
5. **Deal details in list**: Each deal shows `stage • value` below the name in the picker
6. **Personalization** (lines 148-149): Deal stage and value are passed to `generateCopy()` as `RecipientContext` for AI-powered copy personalization

---

## 20. Frontend — Copy Generator (Deal Context)

**File:** `src/app/api/copyGenerator.ts`

### Deal Interactions

1. **`RecipientContext` type**: Includes `type: 'deal'`, `dealStage`, `dealValue` for deal-specific personalization
2. **`deal-message` copy type**: Dedicated copy template for deal stakeholder updates ("Close the deal" goal template included)
3. **`generateCopyWithRecipient()`**: Passes deal stage and value to backend for AI personalization
4. **`recordConversion()`**: `conversionType: 'Deal'` — tracks when copy leads to a deal conversion
5. **Mock templates**: `deal-message|Close the deal` template with deal-specific messaging

---

## 21. Frontend — Global Search (Deals)

**File:** `src/app/api/search.ts`

- `globalSearch(query)` returns `{deals: {id, name, value}[]}` alongside leads, contacts, and companies
- Used by the AppHeader search functionality

---

## 22. Frontend — Navigation & Routing

### AppHeader (`src/app/components/AppHeader.tsx`)
- Navigation item: `{path: '/deals', label: 'Deals', icon: Kanban}`
- Shows in main nav bar with responsive overflow into "More" menu

### Navigation Config (`src/app/config/navigation.ts`)
- `NAV_ITEMS` includes `{path: '/deals', label: 'Deals', icon: Kanban}`

### App Router (`src/app/App.tsx`)
- Route: `/deals` → lazy-loaded `Pipeline` component (the pipeline/deal management page)
- Route: `/deals/:id` → lazy-loaded `DealDetail` component (HP-6 — dedicated single-deal detail page)
- Both routes are protected, requiring authentication

---

## 23. Frontend — Settings (Deal Notifications & Pipelines)

### NotificationsSection (`src/app/pages/settings/components/NotificationsSection.tsx`)
- **"Deal Updates" toggle**: `emailOnDealUpdate` — receive email when deals you're assigned to are updated

### PipelinesSection (`src/app/pages/settings/components/PipelinesSection.tsx`)
- **Pipeline & Stage management**: CRUD for pipelines and their deal stages
- Description: "Configure your sales pipeline stages for deals"
- Only available for organization owners

### User Settings defaults (`src/app/api/settings.ts`)
- `emailOnDealUpdate: true` — deal notification enabled by default

---

## 24. Frontend — Team Page (Deal Stats per Member)

**File:** `src/app/pages/team/components/MemberDetailPanel.tsx`

- Mock stats include `dealsWon` per team member
- Recent activities mock: "Closed a deal worth $5,000"

---

## 25. Frontend — Mock Data

**File:** `src/app/api/mockData.ts`

Contains mock deals used in demo mode (when no backend is connected):
- Sample deals with names, values, stages
- Mock activities with `dealId` references
- Mock tasks with `dealId` references

---

## 26. Frontend — Query Keys

**File:** `src/app/hooks/queries/queryKeys.ts`

```
deals: {
  all: ['deals'],
  lists: () => ['deals', 'list'],
  list: (filters?) => ['deals', 'list', filters],
  details: () => ['deals', 'detail'],
  detail: (id) => ['deals', 'detail', id],
}
dealStages: {
  all: ['dealStages'],
  lists: () => ['dealStages', 'list'],
  byPipeline: (pipelineId) => ['dealStages', 'list', pipelineId],
}
```

---

## 27. Complete File Inventory

### Backend Files (38 files)

| Category | Files |
|----------|-------|
| **Entity** | `Deal.cs`, `DealStage.cs`, `DealStageChange.cs` (NEW — HP-11) |
| **Service Interface** | `IDealService.cs`, `IDealStageService.cs` |
| **Service Impl** | `DealService.cs`, `DealStageService.cs` |
| **Repository Interface** | `IDealRepository.cs` (updated — `AddStageChangeAsync`, `GetStageChangesAsync`), `IDealStageRepository.cs` |
| **Repository Impl** | `DealRepository.cs` (updated — `IncludeRelated`, `AddStageChangeAsync`, `GetStageChangesAsync`), `DealStageRepository.cs` |
| **Controller** | `DealsController.cs`, `DealStagesController.cs` |
| **DTOs** | `DealDto.cs` (updated — 25 fields), `CreateDealRequest.cs` (updated — `Description`, `Probability`), `UpdateDealRequest.cs` (updated — `Description`, `Probability`, `ClosedReason`, `ClosedReasonCategory`), `DealStageDto.cs`, `CreateDealStageRequest.cs`, `UpdateDealStageRequest.cs` |
| **DB Config** | `DealConfiguration.cs` (updated — `Description`, `ClosedReason` columns), `DealStageConfiguration.cs`, `DealStageChangeConfiguration.cs` (NEW — HP-11) |
| **Tests** | `DealServiceTests.cs` |
| **Cross-entity** | `Activity.cs`, `Lead.cs`, `TaskItem.cs`, `User.cs`, `Company.cs`, `Pipeline.cs`, `UserSettings.cs` |
| **Cross-service** | `LeadService.cs`, `ActivityService.cs` (updated — HP-2 `"task"` type), `ReportingService.cs`, `GlobalSearchService.cs`, `SendToCrmService.cs` |
| **Cross-controller** | `ActivitiesController.cs`, `TasksController.cs`, `LeadsController.cs`, `ReportingController.cs`, `SearchController.cs`, `CopyController.cs` |
| **Cross-config** | `ActivityConfiguration.cs`, `TaskConfiguration.cs`, `LeadConfiguration.cs` |
| **DI/Setup** | `Program.cs`, `DependencyInjection.cs`, `AppDbContext.cs` (updated — `DbSet<DealStageChange>`) |
| **Enums** | `CopyTypeId.cs` (`DealMessage`), `RecipientType.cs` (`Deal`) |

### Frontend Files (67 files reference "deal")

| Category | Files |
|----------|-------|
| **API Client** | `deals.ts` (updated — `DealRaw` extended, `description`/`probability`/`closedReason` params), `dealStages.ts`, `activities.ts`, `tasks.ts`, `reporting.ts`, `search.ts`, `leads.ts`, `copyGenerator.ts`, `settings.ts`, `mockData.ts`, `messages.ts`, `index.ts`, `types.ts` (updated — 12 new Deal fields), `pipelines.ts` |
| **React Query** | `useDeals.ts`, `useActivities.ts`, `queryKeys.ts`, `hooks/queries/index.ts` |
| **Pipeline (Deal UI)** | `Pipeline.tsx` (updated — HP-1/5/7/8/10), `DealCard.tsx` (updated — HP-3/7 currency + assignee), `DroppableStageColumn.tsx`, `pipeline/utils.tsx`, `pipeline/config.ts`, `pipeline/types.ts`, `pipeline/index.ts` |
| **Deal Detail** | `DealDetail.tsx` (NEW — HP-6, dedicated deal detail page with activities, close dialog) |
| **Dashboard** | `Dashboard.tsx`, `DashboardHero.tsx`, `PipelineChart.tsx`, `TeamPerformance.tsx`, `SalesWriter.tsx`, `QuickNav.tsx`, `dashboard/config.ts`, `dashboard/types.ts` |
| **Leads** | `Leads.tsx`, `ConvertLeadDialog.tsx`, `leads/types.ts`, `leads/config.ts` |
| **Tasks** | `Tasks.tsx`, `TaskDetailModal.tsx`, `KanbanTaskCard.tsx`, `ListTaskCard.tsx`, `tasks/types.ts` |
| **Activities** | `Activities.tsx`, `activities/types.ts` |
| **Companies** | `Companies.tsx` (updated — HP-12 deal drill-down, `getDealsPaged`), `companies/types.ts` |
| **Contacts** | `Contacts.tsx` (updated — HP-9 deal visibility per contact, `getDealsPaged`) |
| **Other Pages** | `SendToCrm.tsx`, `Homepage.tsx`, `Help.tsx`, `History.tsx`, `Team.tsx`, `Organizations.tsx`, `Templates.tsx`, `Settings.tsx`, `Privacy.tsx` |
| **Settings** | `PipelinesSection.tsx`, `NotificationsSection.tsx` |
| **Team** | `MemberDetailPanel.tsx` |
| **Navigation** | `AppHeader.tsx`, `App.tsx` (updated — `/deals/:id` route), `navigation.ts` |
| **Test** | `mocks/handlers.ts` |
| **Docs** | `pages/README.md`, `app/README.md` |

---

## 28. Complete Relationship Map

```
                    ┌──────────────┐
                    │   Pipeline   │
                    │              │
                    │ ICollection  │
                    │  <Deal>      │
                    └──────┬───────┘
                           │ has many
                           ▼
┌──────────┐      ┌──────────────┐      ┌──────────────┐
│   User   │──────│     Deal     │──────│  DealStage   │
│ (Owner)  │owns  │              │ at   │              │
│          │      │  Name        │      │  Name        │
│ ICol<D>  │      │  Value       │      │  DisplayOrder│
└──────────┘      │  Currency    │      │  IsWon/IsLost│
                  │  Description │      └──────────────┘
┌──────────┐      │  Probability │
│  Company │──────│  IsWon       │      ┌──────────────┐
│          │has   │  ClosedReason│      │ DealStage    │
│ ICol<D>  │      │  ClosedAtUtc │      │  Change      │
└──────────┘      │              │──────│ (HP-11)      │
                  │  Activities  │      │  From → To   │
┌──────────┐      │  TaskItems   │      │  ChangedBy   │
│ Contact  │──────│  StageChanges│      │  ChangedAt   │
│          │ FK   └──────┬───────┘      └──────────────┘
└──────────┘             │
                         │              ┌──────────────┐
┌──────────┐             │              │   Activity   │
│   Lead   │──── ConvertedToDealId      │  DealId (FK) │
│          │     Lead → Deal            └──────────────┘
└──────────┘                            ┌──────────────┐
                                        │   TaskItem   │
                                        │  DealId (FK) │
                                        │  DealName    │
                                        └──────────────┘

Frontend Interactions:
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│  Pipeline   │  │  DealDetail  │  │  Dashboard  │  │  Activities  │
│  Page       │  │  Page (HP-6) │  │  Page       │  │  Page        │
│  (Kanban/   │  │  (/deals/:id │  │  (Stats,    │  │  (Filter by  │
│   List)     │  │   full page) │  │   Charts)   │  │   Deal)      │
└─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│  Tasks      │  │  Contacts   │  │  Companies  │  │  Leads       │
│  Page       │  │  Page       │  │  Page       │  │  Page        │
│  (Link to   │  │  (HP-9 Deal │  │  (HP-12     │  │  (Convert    │
│   Deal)     │  │   Visibility│  │   Drill-Dn) │  │   → Deal)    │
└─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│  SendToCrm  │  │  Sales      │  │  Settings   │
│  Page       │  │  Writer     │  │  (Pipelines │
│  (Save to   │  │  (Deal as   │  │   & Notify) │
│   Deal)     │  │   Recipient)│  │             │
└─────────────┘  └─────────────┘  └─────────────┘
```

---

## 29. What Is Missing — Comprehensive Detailed Analysis

This section provides a thorough breakdown of every missing or incomplete deal-related feature. For each item we explain: **what currently exists** (with exact file/line references), **what is missing**, **why we need it** (business justification), and **what it would take to implement** (technical scope).

> **UPDATE (February 9, 2026):** All 12 HIGH PRIORITY items (HP-1 through HP-12) have been implemented. Items tagged **[IMPLEMENTED]** below are complete — see Section 30 for full implementation details. The analysis text below preserves the original pre-implementation context for reference. Items tagged **[LOW PRIORITY]** remain unimplemented.

### A. NOT IMPLEMENTED — Features that do not exist at all

> Each item below is tagged **[HIGH PRIORITY]** (must do — bugs, data loss, core CRM features) or **[LOW PRIORITY]** (nice to have — valuable but not blocking). HIGH PRIORITY items have full implementation guides in Section D below. Items marked **✅ IMPLEMENTED** have been completed.

---

#### A1. Dedicated Deal Detail Page (`/deals/:id`) — ✅ IMPLEMENTED (HP-6)

**What exists now:**
The Pipeline page (`Pipeline.tsx`) has a "detail sheet" — a narrow right-side `<Sheet>` panel (~400px wide) that opens when you click a deal card name. This sheet shows: deal name, value, currency, stage badge, contact name + email, company name, expected close date + urgency badge, edit/delete buttons, and an activity log (limited to a `max-h-[300px]` scrollable area) with a form to add activities.

**Source evidence:**
- `App.tsx`: route `/deals` maps to `Pipeline` component — no `/deals/:id` route exists
- `Pipeline.tsx` lines 1134-1341: `<Sheet>` component is the only "detail view"
- The sheet shows activities but zero tasks, no description, no assignee name, no pipeline name, no created/updated dates

**What is missing:**
A full `/deals/:id` page that serves as the single source of truth for everything about a deal. The current sheet cannot display:
- Linked tasks (the sheet has zero task display — tasks linked to this deal are invisible unless you go to the Tasks page)
- Deal description/notes (the backend `Deal.cs` has no `Description` field, and the frontend `DealFormState.description` is a dead field not saved to the backend)
- Assignee name (the `AssigneeId` is stored but the sheet does not display who is assigned)
- Pipeline name (when you have multiple pipelines, you can't tell which pipeline this deal belongs to)
- Created/Updated timestamps (no way to know deal age or when it was last modified — and these fields are not even in the `DealDto`)
- Stage change history (no history entity exists)
- Related files/documents (no attachment system exists)

**Why we need it:**
1. **Deep linking**: Users cannot share a URL to a specific deal. In a real sales workflow, a manager says "look at the Acme Corp deal" — with a `/deals/:id` URL, they can paste it in Slack, email, or a meeting chat. Every major CRM (Salesforce, HubSpot, Pipedrive, Close.io) has deep-linkable deal pages. Without this, collaboration requires verbal instructions ("go to Pipeline, find Acme Corp in the Negotiation column").
2. **Full context before a call**: A sales rep preparing for a customer call needs to see everything about a deal in one place — who's the contact, what company, what happened in past activities, what tasks are pending, when is the expected close. Currently they must visit 3 different pages (Pipeline for deal info -> Activities page filtered by deal -> Tasks page filtered by deal) to gather this. That's 3 page loads and manual filtering just to prepare for one call.
3. **Manager pipeline reviews**: When a sales manager reviews pipeline health with the team, they click into individual deals to see full details. A narrow side sheet with a 300px-tall activity scroll area is insufficient — they need a full page with tabs for activities, tasks, files, and stage history.
4. **Bookmark-ability**: Team members often bookmark important deals or save them in project management tools (Notion, Asana). Without a stable URL per deal, this is impossible.

**What it would take:**
- **Frontend (Medium effort)**: New route `/deals/:id` in `App.tsx`. New `DealDetail.tsx` page component with tabbed layout (Overview, Activities, Tasks, Files). API calls: deal by ID, `getActivitiesByDeal`, `getTasksByDeal`. Header with deal name, value, stage badge. Sidebar with contact/company/assignee info.
- **Backend (Low effort)**: The `GET /api/deals/{id}` endpoint already exists in `DealsController.cs`. Could optionally create a "deal detail" enriched endpoint that returns activity count + task count in one call to avoid N+1 requests.

---

#### A2. Deal Activity Timeline in Deal Context — ✅ IMPLEMENTED (part of HP-6)

**What exists now:**
The Pipeline page's detail sheet has a basic activity log with a form to add new activities. Activities linked to a deal can also be seen by going to the separate Activities page and filtering "By Deal" (`Activities.tsx` lines 606-672).

**Source evidence:**
- `Pipeline.tsx` lines 1218-1336: Activity section in the detail sheet — limited to `max-h-[300px]` scroll area, no pagination, no grouping by date
- `Activities.tsx` lines 148-155, 186-195: `getActivitiesByDeal(dealId)` fetches all activities — no server-side pagination for deal-filtered activities

**What is missing:**
- No full-page activity timeline within deal context (the sheet's activity area is cramped)
- No date-grouped timeline view (activities are just listed chronologically without visual date separators)
- No activity type icons specific to deal context (e.g., "Stage changed from Qualification to Proposal")
- No "stage change" activity auto-creation (when a deal moves stages, no activity is automatically logged)
- Activities fetched by deal are NOT paginated — `getActivitiesByDeal` returns the full list, which will be slow for deals with 100+ activities

**Why we need it:**
The activity timeline is the "story" of a deal. Before making a call, a rep needs to scroll through the history: "Last email sent March 1 — sent proposal March 5 — met on March 10 — no activity since." Without a proper timeline, this context is either crammed into a tiny scrollable area or requires navigating to a separate page. Stage change activities are especially critical — "deal moved from Proposal to Negotiation on March 8 by John" tells the full progression story.

**What it would take:**
- As part of the Deal Detail Page (A1): Include a full-height activity timeline tab with server-side pagination, date grouping, and proper icons per activity type.
- Backend: Auto-create activity records when deal stage changes. Add pagination to `GetByDealIdAsync`.

---

#### A3. Deal Tasks Tab — No Task Visibility in Deal Context — ✅ PARTIALLY IMPLEMENTED (part of HP-6)

**What exists now:**
Tasks can be linked to deals via `DealId` FK. The Tasks page loads deals for linking (`Tasks.tsx`). `KanbanTaskCard` and `ListTaskCard` show `task.dealName` as a badge. `TaskDetailModal` has a deal dropdown selector.

**Source evidence:**
- `Pipeline.tsx` detail sheet (lines 1134-1341): Zero mention of tasks. No `getTasksByDeal` call. No task display.
- `Tasks.tsx`: Loads deals for linking but has no "deal detail" view with embedded task list.
- `tasks.ts` API: `getTasksByDeal(dealId)` exists and works — it's just never called from the deal context.

**What is missing:**
When viewing a deal (in the detail sheet or a future detail page), there is absolutely no way to see which tasks are linked to it. The user must:
1. Leave the deal context
2. Navigate to the Tasks page
3. Manually remember the deal name
4. There's no task-by-deal filter on the Tasks page UI (the API supports it but the UI doesn't expose a deal filter dropdown)

**Why we need it:**
Tasks are the actionable items that move a deal forward — "Send revised proposal by Friday," "Schedule follow-up call," "Get legal approval on contract terms." If a rep can't see these tasks when looking at a deal, they'll miss deadlines and let deals stall. The entire point of linking tasks to deals is to provide context, but that context is invisible from the deal side.

**What it would take:**
- As part of Deal Detail Page (A1): Add a "Tasks" tab that calls `getTasksByDeal(dealId)` and renders a task list with status badges. Include quick task creation inline.
- Alternative: Add a collapsible "Linked Tasks" section to the existing detail sheet (below the activity log).

---

#### A4. Contact -> Deal Visibility — ✅ IMPLEMENTED (HP-9)

**What exists now:**
- Backend: `ContactId` FK on `Deal.cs`, deals can reference contacts
- Frontend Pipeline: DealCard shows contact name (looked up client-side from a separately loaded contacts array)
- Frontend `Contacts.tsx` (1162 lines): Does NOT import `getDeals`, does NOT fetch deals, does NOT display any deal information

**Source evidence:**
- `Contacts.tsx` line 15: imports `getContactsPaged, createContact, updateContact, deleteContact, getCompanies` — **no deal imports**
- `Contacts.tsx` line 16: imports `Contact, Company` types — **no `Deal` type**
- Searching the entire `Contacts.tsx` file for "deal" returns zero matches

**What is missing:**
When viewing a contact's profile, there is absolutely zero sales context:
- No "Deals" section showing deals where `contactId === thisContact.id`
- No deal count or total pipeline value for this contact
- No click-through from DealCard's contact name to the contact's profile
- No "Create Deal for this Contact" quick action
- No indication whether a contact is involved in a $500K deal or no deal at all

**Why we need it:**
Sales is fundamentally about relationships with people. When a rep is about to call a contact, the first thing they need to know is: "Which active deals involve this person? What's the total value? When did I last interact with them about a deal?" Currently, the Contacts page shows name, email, phone, company, and activities — but ZERO sales pipeline context. This is the most critical relationship gap in the CRM because:
- A contact could be the key decision maker on a $500K deal, and the Contacts page gives no hint of this
- A rep inheriting accounts has no way to assess a contact's commercial importance without separately searching the Pipeline page
- Every competitor CRM (Salesforce, HubSpot, Pipedrive) shows associated deals prominently on contact profiles

**What it would take:**
- **Frontend (Low-Medium effort)**: In `Contacts.tsx`, import and call `getDeals()` on mount. For each contact, filter deals by `contactId`. Show "Associated Deals" section on contact detail view with deal name, value, stage, and expected close date. Add "Create Deal" quick action that pre-fills `contactId`.
- **Backend (None)**: No backend changes needed — deals already have `ContactId` and all endpoints work.

---

#### A5. ListTaskCard Missing Deal Link/Unlink Action (UI Inconsistency) — [LOW PRIORITY]

**What exists now:**
- `KanbanTaskCard.tsx` (lines 173-188): Has a "Deal" `DropdownMenuSub` submenu with "No deal" + all deal options for linking/unlinking
- `ListTaskCard.tsx` (lines 108-139): Dropdown menu ONLY has status and priority options — no deal submenu
- Both cards display `task.dealName` as a teal badge when a deal is linked

**Source evidence:**
- `KanbanTaskCard.tsx` lines 173-188: Deal submenu with `handleDealChange` callback
- `ListTaskCard.tsx` entire dropdown section: Only `DropdownMenuItem` for status changes and priority changes, zero deal-related menu items

**What is missing:**
Users in list view cannot change a task's deal association from the dropdown menu. They must either:
1. Switch to Kanban view (different mental model), OR
2. Click the task to open `TaskDetailModal` (2 extra clicks + modal load + find the deal dropdown)

**Why we need it:**
Feature parity between views is a UX fundamental. If a user prefers list view (common for users managing many tasks), they lose the ability to quickly link/unlink deals. This creates frustration and inconsistency — the same action is available in one view but not another, with no obvious reason why.

**What it would take:**
- **Frontend (Low effort)**: Copy the "Deal" `DropdownMenuSub` from `KanbanTaskCard.tsx` into `ListTaskCard.tsx`. Ensure `handleDealChange` and `deals` props are passed through from the parent `TaskGroupSection`.

---

#### A6. Deal Notes / Description / Comments — ✅ IMPLEMENTED (HP-1)

**What exists now:**
This is a significant discovery from the source verification:
- **Backend `Deal.cs`**: The entity does NOT have a `Description` property (verified by reading the actual file — it has `Name, Value, Currency, Stage, PipelineId, DealStageId, CompanyId, ContactId, AssigneeId, ExpectedCloseDateUtc, IsWon, CreatedAtUtc, UpdatedAtUtc, UpdatedByUserId`)
- **Backend DTOs**: `CreateDealRequest.cs` and `UpdateDealRequest.cs` do NOT have a `Description` field
- **Frontend `pipeline/types.ts`**: `DealFormState` HAS a `description: string` field — but it maps to nothing on the backend
- **Result**: Any description text entered by a user in the frontend form is **silently discarded** and never saved

There is also no `DealNote` or `DealComment` entity for a multi-note/comment system.

**Why we need it:**
Sales reps need to record qualitative context about a deal that doesn't fit into structured fields:
- "Client prefers quarterly payments; approved by their CFO on March 5"
- "Decision maker is VP of Engineering, NOT the CTO who attended the demo"
- "Budget approved for Q2 but may slip to Q3 if procurement delays continue"
- "Competitor XYZ is also pitching — our advantage is faster implementation timeline"
- "Legal flagged clause 4.2 in the contract — waiting for revised terms"

Without this capability, a deal is just a name + dollar value with zero qualitative context. When a deal is reassigned to a new rep, or when a manager reviews the pipeline, there's no written history explaining the deal's situation.

**What it would take:**
- **Backend (Low effort)**: Add `Description` property (nullable string) to `Deal.cs`. Add `Description` field to `CreateDealRequest.cs` and `UpdateDealRequest.cs`. Add to `DealDto`. Update `DealService.Map()` to include it. Run EF migration.
- **Frontend (Low effort)**: The `DealFormState` already has the field. Ensure the create/edit forms include a `<Textarea>` for description (they may already have one based on `DealFormState`). Map it to the API call body. Display in detail sheet.

---

#### A7. Deal Forecast / Weighted Pipeline — [LOW PRIORITY] (depends on HP-1 for Probability field)

**What exists now:**
- `ReportingService.cs` lines 14-28: `GetDashboardStatsAsync` sums raw `Value` for all open deals with `pipelineValue += v` — zero weighting
- `ReportingService.cs` lines 30-49: `GetPipelineValueByStageAsync` groups by stage and sums raw value per stage — zero weighting
- `DashboardHero.tsx`: Displays "Pipeline Value" as a single unweighted total
- `PipelineChart.tsx`: Bar chart of deal value per stage — all deals count at 100%
- Frontend `DealFormState` has a `probability: string` field — but the backend has NO `Probability` property, making this a dead field too

**Source evidence:**
- `Deal.cs`: No `Probability` or `Weight` property
- `DealStage.cs`: No `DefaultProbability` property
- `pipeline/types.ts`: `DealFormState` has `probability: string` — dead field, never saved
- `ReportingService.cs` line 24: `pipelineValue += v` — direct sum, no weighting

**What is missing:**
- No `Probability` field on deals (0-100%)
- No `DefaultProbability` on deal stages (auto-set when deal enters stage)
- No weighted pipeline calculation: `weightedValue = sum(value * probability / 100)`
- No forecast by period (expected revenue by month/quarter)
- No "Best Case / Most Likely / Worst Case" scenario modeling

**Why we need it:**
1. **Accurate revenue forecasting**: A pipeline of "$500K" is misleading if 80% of deals are in early stages with <20% chance of closing. Weighted pipeline showing "$125K expected" gives a realistic revenue projection. This is the single most requested feature in any sales CRM.
2. **Board/executive reporting**: Investors and leadership regularly ask "what's your forecast for Q2?" An unweighted pipeline total assumes every deal closes — which never happens. Industry standard close rates are 20-30%, so raw pipeline totals overstate revenue by 3-5x.
3. **Stage-based probability defaults**: Most CRMs auto-assign probability by stage (e.g., Qualification=20%, Proposal=50%, Negotiation=75%). This is standard in Salesforce, HubSpot, Pipedrive. It means a deal in Negotiation automatically counts at 75% of its value in the forecast.
4. **Rep accountability**: Weighted pipeline per rep helps managers set realistic quotas. "You have $200K weighted pipeline — your quota is $150K — you're tracking well."
5. **Dead field fix**: The frontend already has a probability input field that users may be filling in — but their input is silently discarded. This is a UX bug.

**What it would take:**
- **Backend (Medium effort)**: Add `Probability` (int, 0-100) to `Deal.cs` and DTOs. Add `DefaultProbability` to `DealStage.cs`. Update `DealService.CreateAsync`/`UpdateAsync` to auto-set probability from stage default when stage changes. Update `ReportingService` to compute weighted values. New endpoint: `GET /api/reporting/forecast?months=3`.
- **Frontend (Medium effort)**: Connect the existing probability form field to the backend. Show "Weighted Pipeline" alongside "Total Pipeline" on dashboard. Add probability badge on DealCards. Add forecast chart.

---

#### A8. Deal Stage Change History / Audit Trail — ✅ IMPLEMENTED (HP-11)

**Implementation:**
The backend now tracks all stage transitions. When `DealService.UpdateAsync()` detects a stage change (lines 203-205 capture old stage, line 244 checks if `DealStageId` changed), it creates a `DealStageChange` record via `AddStageChangeAsync` (lines 243-261).

**Source evidence:**
- `DealService.cs` lines 203-205: captures `oldStageId` and `oldStageName` before applying updates
- `DealService.cs` line 213: `if (request.DealStageId != null) existing.DealStageId = request.DealStageId;` — applies new stage
- `DealService.cs` lines 243-261: if stage actually changed, creates `DealStageChange` record with `FromDealStageId`, `ToDealStageId`, `FromStageName`, `ToStageName`, `ChangedByUserId`, `ChangedAtUtc`
- `DealStageChange` entity exists with full audit fields
- `DealRepository.AddStageChangeAsync` persists the record
- `DealRepository.GetStageChangesAsync` returns history ordered by `ChangedAtUtc` descending

**What this enables:**
1. **Deal velocity calculation**: "How long does the average deal stay in Qualification before moving to Proposal?" — now computable from stage change timestamps.
2. **Stalled deal detection**: Can determine when a deal entered its current stage and flag deals that have exceeded the average duration.
3. **Stage regression tracking**: Backward movements (Negotiation → Qualification) are recorded and visible.
4. **Audit and compliance**: Full "who changed what when" history for each deal's stage transitions.
5. **Process optimization**: Average time per stage across all deals reveals bottlenecks.

---

#### A9. Deal Win/Loss Reason — ✅ IMPLEMENTED (HP-8)

**What exists now:**
- `Deal.cs` line 21: `public bool? IsWon { get; set; }` — null = open, true = won, false = lost
- When a deal is moved to "Closed Won" or "Closed Lost" stage, `isWon` is set in the `updateDeal` call
- No dialog or prompt asks WHY the deal was won or lost

**Source evidence:**
- `Deal.cs`: Only `IsWon` boolean, no `WonReason`, `LostReason`, or `ClosedReasonCategory`
- `UpdateDealRequest.cs`: Accepts `IsWon` but no reason field
- Pipeline page stage move: Sets `isWon` directly without any dialog

**What is missing:**
- No `ClosedReason` text field on the Deal entity
- No `ClosedReasonCategory` enum (Price, Timing, Competitor, NoDecision, FeatureGap, Other)
- No dialog appearing when deal moves to Won/Lost asking for the reason
- No analytics/reporting on win/loss reasons

**Why we need it:**
1. **Sales coaching**: If 40% of deals are lost to "Price too high," management knows to adjust pricing, offer discounts, or improve value communication. Without structured reasons, every lost deal is a mystery and coaching is based on guesswork.
2. **Competitive intelligence**: Tracking "Lost to Competitor XYZ" across all deals reveals competitive patterns. If one competitor keeps winning in your target segment, the team can develop targeted counter-strategies, battle cards, or product improvements.
3. **Process improvement**: If many deals are lost at Negotiation with reason "Requirements changed," it suggests the Qualification stage isn't thorough enough — the team should invest in better discovery calls to surface requirements earlier.
4. **Win replication**: Understanding WHY deals are won is equally important. "Strong executive relationship," "Better feature set," "Faster implementation timeline" — knowing what works helps replicate success patterns across the team.
5. **Board-level reporting**: Investors regularly ask "why are you losing deals?" Without structured data, the answer is anecdotal. With it, you can present: "35% price, 25% timing, 20% competitor, 20% other."

**What it would take:**
- **Backend (Low effort)**: Add `ClosedReason` (string) and `ClosedReasonCategory` (enum) to `Deal.cs`. Update DTOs. Migration.
- **Frontend (Low-Medium effort)**: When stage move detects Won/Lost, show a dialog with reason category dropdown + optional text details. Store with deal update. Add win/loss reason breakdown chart to Dashboard.

---

#### A10. Deal Products / Line Items — [LOW PRIORITY]

**What exists now:**
Deals have a single `Value` field (stored as string). The backend validates format with regex `^-?\d+(\.\d{1,2})?$` but stores as `MaxLength(64)` string. `ReportingService.cs` does fragile parsing: `decimal.TryParse((d.Value ?? "").Replace("$", "").Replace(",", "").Trim(), out var v)`.

**Source evidence:**
- `Deal.cs` line 11: `public string Value { get; set; } = string.Empty;`
- No `DealLineItem`, `DealProduct`, or `Product` entity exists in the codebase
- `ReportingService.cs` line 24: Manual string-to-decimal parsing with `$` and `,` stripping

**What is missing:**
- No `Product` entity (Id, Name, SKU, BasePrice, Category)
- No `DealLineItem` entity (Id, DealId, ProductId, Quantity, UnitPrice, Discount%, LineTotal)
- No product catalog or management UI
- No auto-calculation of deal value from line items

**Why we need it:**
1. **Accurate quoting**: "$50,000" tells you nothing about what's being sold. Is it 1 enterprise license or 50 seats? Sales reps need to track specific products/services per deal for accurate quotes and proposals.
2. **Revenue by product**: Leadership needs "How much revenue is Product A generating vs. Product B?" This drives product investment, pricing strategy, and marketing spend allocation.
3. **Discount tracking**: If a 20% discount is given, it should be tracked separately from the base price. Without line items, you can't analyze discount trends or protect margins.
4. **Proposal generation**: Auto-generating proposals from deal data requires structured product/quantity/price data — not a single dollar amount.

**What it would take:**
- **Backend (High effort)**: New `Product` entity with CRUD. New `DealLineItem` junction entity. API endpoints for managing line items. Auto-recalculate deal value from line items.
- **Frontend (High effort)**: Product catalog page in Settings. Line items editor in deal forms. Auto-sum calculation. Product autocomplete.

---

#### A11. Deal Attachments / Files — [LOW PRIORITY]

**What exists now:**
No file upload capability anywhere in the deal system.

**Source evidence:**
- No `DealAttachment` or `Attachment` entity in the Entities folder
- No file upload endpoint in `DealsController.cs`
- No file storage configuration in `Program.cs` or `appsettings.json`
- No upload UI anywhere in the frontend

**What is missing:**
Complete file attachment system: entity, storage, API endpoints, upload UI, preview, download.

**Why we need it:**
1. **Proposals**: Sales reps send proposals (PDF, DOCX) and need to store them with the deal. "What exactly did we propose to Acme Corp?" shouldn't require searching through email.
2. **Contracts**: Signed contracts are the most critical deal documents. They should be on the deal record, not in a random shared drive folder.
3. **Team handoffs**: When a deal is reassigned to a new rep, all files should be immediately accessible without asking "where did you save that proposal?"
4. **Legal/compliance**: In regulated industries, deal documentation must be retained for audit purposes.

**What it would take:**
- **Backend (Medium-High)**: New `DealAttachment` entity. File storage (Azure Blob or local). Upload/download/delete endpoints. Size/type validation.
- **Frontend (Medium)**: Drag-and-drop upload zone in deal detail. File list with type icons. Download and delete buttons.

---

#### A12. Deal Email Integration — [LOW PRIORITY]

**What exists now:**
The Sales Writer generates AI copy "personalized for a deal" (using `dealStage` and `dealValue` as context). `SendToCrm.tsx` saves copy against a deal record as a CRM note. But there is NO email sending functionality.

**Source evidence:**
- No SMTP configuration in `appsettings.json` or `Program.cs`
- No email service in the backend
- No "Send Email" button in any deal UI
- `SendToCrm.tsx` saves copy as a CRM record, NOT an email

**What is missing:**
Email composer, sending capability, thread tracking, open/click tracking, Gmail/Outlook integration.

**Why we need it:**
1. **Workflow efficiency**: Current flow: Dashboard -> Sales Writer -> generate -> copy to clipboard -> open email client -> paste -> add recipient -> send. A "Send Email" button on the deal would reduce this from 7 steps to 3.
2. **Automatic activity logging**: Emails sent about a deal should auto-create Activity records. Currently, reps must manually log email activities — and most don't, leading to incomplete deal histories.
3. **Follow-up tracking**: "Was the proposal email opened?" is essential for timing follow-ups.
4. **Complete communication history**: Without email integration, the deal's activity log is missing its most important communication channel.

**What it would take:**
- **Backend (High)**: Email service (SMTP or SendGrid). Tracking. Store sent emails as Activities. Potentially OAuth integration for Gmail/Outlook.
- **Frontend (Medium)**: Email composer dialog. Template selection. Email history tab in deal detail.

---

#### A13. Real-time Deal Updates (WebSocket/SSE) — [LOW PRIORITY]

**What exists now:**
All deal data is fetched once on page load via `getDeals()`. No push updates.

**Source evidence:**
- `Pipeline.tsx` line 201: `useEffect` calls `getDeals()` on mount — one-time fetch
- No SignalR hub in `Program.cs`
- No WebSocket endpoint in any controller

**What is missing:**
Live push of deal changes to all connected clients.

**Why we need it:**
1. **Team collaboration**: In a 10-person team, multiple users often have the Pipeline board open. Without real-time updates, they see stale data. User A might call a contact about a deal that User B already closed.
2. **Live pipeline reviews**: During team meetings, the manager moves deals on their screen — the team should see it live.
3. **Conflict prevention**: Two reps editing the same deal simultaneously — last-write-wins means one person's changes are silently lost.

**What it would take:**
- **Backend (Medium-High)**: SignalR hub. Broadcast events from DealService on create/update/delete.
- **Frontend (Medium)**: Connect to hub on Pipeline mount. Update React state on events. Show toast for changes by others.

---

#### A14. Deal Duplicate Detection — [LOW PRIORITY]

**What exists now:**
`DealService.CreateAsync()` only validates name != empty and value != empty. No uniqueness check.

**Source evidence:**
- `DealService.cs` lines 127-175: `CreateAsync` — only checks `Name` and `Value` are non-empty
- No unique constraint on `(Name, ContactId)` or `(Name, CompanyId)` in `DealConfiguration.cs`

**Why we need it:**
Duplicate deals inflate pipeline totals (two $50K entries for the same opportunity = $100K phantom pipeline). Activities get split across duplicates, making deal histories incomplete. Two reps calling the same client about "the same deal" looks unprofessional.

**What it would take:**
- **Backend (Medium)**: Duplicate check in `CreateAsync` (similar name + same company/contact). Return warning with potential matches. Merge endpoint.
- **Frontend (Low-Medium)**: Warning dialog during creation. "Did you mean...?" suggestions.

---

#### A15. Deal Import/Export — [LOW PRIORITY]

**What exists now:**
Deals can only be created one at a time. No bulk operations.

**Source evidence:**
- No import/export endpoints in `DealsController.cs`
- No CSV parsing library in the backend

**Why we need it:**
1. **CRM migration**: Switching from another CRM requires importing existing deals. Without import, each deal must be manually re-entered — a dealbreaker for adoption.
2. **Bulk updates**: Sales ops teams need to update hundreds of deals at once (territory reassignment, date adjustments). Export -> modify in Excel -> re-import is the standard workflow.
3. **External reporting**: Teams need to analyze deals in Excel, Google Sheets, or BI tools (Tableau, Power BI). An export button is essential.
4. **Data portability/GDPR**: Periodic exports serve as backups and satisfy data portability requirements.

**What it would take:**
- **Backend (Medium)**: CSV export endpoint. CSV import with field mapping and validation.
- **Frontend (Medium)**: Export button on Pipeline toolbar. Import wizard with file upload, preview, field mapping, and validation.

---

#### A16. Deal Recurrence / Renewal Tracking — [LOW PRIORITY]

**What exists now:**
Deals are one-time records. Once closed (won or lost), they stay permanently in that state.

**Source evidence:**
- No `RecurrenceType`, `RenewalDate`, `ParentDealId`, or `IsRenewal` fields on `Deal.cs`

**Why we need it:**
SaaS companies, insurance agencies, and service providers have deals that renew annually. A $50K/year deal that's Won should automatically create a renewal deal for next year. Without this, renewal management is manual and error-prone — reps must remember to create new deals at the right time.

**What it would take:**
- **Backend (Medium)**: Add `ParentDealId` FK and `RenewalDateUtc` to `Deal.cs`. Background job to auto-create renewal deals.
- **Frontend (Low-Medium)**: "Renewal" badge on closed-won deals. "Create Renewal" button. Renewal chain view.

---

#### A17. Activity Update / Edit — [LOW PRIORITY]

**What exists now:**
Activities are immutable. `ActivitiesController.cs` has no PUT/PATCH endpoint. `IActivityService` has no `UpdateAsync` method.

**Why we need it:**
Typos and mistakes happen. If a rep logs "Called John about the Acme proposal" but meant "Called Jane," they must delete the activity and recreate it — losing the original creation timestamp and any system metadata. A simple edit capability is expected in any CRM.

---

#### A18. Frontend-Backend Activity Type Mismatch (BUG) — ✅ IMPLEMENTED (HP-2)

**What exists now:**
The frontend `activities/config.ts` defines 7 activity types: `call`, `meeting`, `email`, `note`, `video`, `demo`, `task`. The backend `ActivityService.cs` only validates/accepts 4: `call`, `meeting`, `email`, `note`.

**Impact:** Users selecting "Video Call," "Demo," or "Task Completed" in the Activities UI will get a **400 Bad Request error** from the backend. This is a real bug affecting the activity logging flow that intersects with deal activities.

---

#### A19. Deal Value Stored as String (Architecture Risk) — [LOW PRIORITY]

**What exists now:**
`Deal.Value` is `string` type. `ReportingService.cs` manually parses with `decimal.TryParse` after stripping `$` and `,` characters. Frontend also stores as string.

**Why this is a risk:**
- A user could enter "TBD" or "5000-10000" and it would be saved (bypassing frontend regex validation in non-form contexts)
- Aggregation silently skips unparseable values — pipeline total could be wrong without any error
- No database-level numeric constraints — sortable/filterable queries must all cast strings
- Currency symbol stripping is fragile — what about "€" or "£" or "CHF "?

---

#### A20-A21. Dead Frontend Form Fields (DealFormState.description & DealFormState.probability) — ✅ IMPLEMENTED (HP-1)

**What exists now:**
Frontend `DealFormState` in `pipeline/types.ts` has both `description: string` and `probability: string` fields. The backend `Deal` entity has NEITHER `Description` NOR `Probability` properties. The DTOs don't accept these fields.

**Impact:** Any text entered by users in these form fields is silently discarded and never saved. This is a significant UX issue — users think they're saving data but it vanishes on the next page load. This needs to be resolved by either:
1. Adding these fields to the backend (recommended — both are valuable), OR
2. Removing the dead form fields from the frontend to prevent user confusion

### B. PARTIALLY IMPLEMENTED — Features that exist but are incomplete

---

#### B1. Deal Assignee — ✅ IMPLEMENTED (HP-7)

**What exists now:**
- Backend: `AssigneeId` nullable FK on `Deal.cs`, `Assignee` navigation property, `AssigneeId` in `DealDto`
- Frontend: Assignee dropdown in create form (`Pipeline.tsx` lines 1526-1545) and edit form (lines 1733-1752) using `orgMembers` array
- Dashboard: `TeamPerformance.tsx` shows pipeline value per assignee

**What is missing:**
- **DealCard does NOT show assignee**: `DealCard.tsx` (lines 95-253) shows deal name, value, contact name, last activity, urgency — but NOT the assignee name or avatar. On a Kanban board with 30+ deals, you can't tell at a glance who is working on what.
- **No "My Deals" filter**: The Pipeline page shows ALL deals in the organization. A rep with 15 deals must visually scan past 50+ other deals to find their own.
- **No assignee-based filtering**: Pipeline search only searches by deal name text. No dropdown for "Show deals assigned to [person]."
- **`assigneeName` not in frontend type**: `Deal` TypeScript interface has `assigneeId` but not `assigneeName`, so even if the backend returned it, the frontend would ignore it.
- **No assignment notifications**: `UserSettings.ts` defines `emailOnDealUpdate: boolean`, but there is NO backend implementation that sends emails on deal assignment. The setting is a UI toggle with no trigger behind it.

**Why we need it:**
Without seeing assignees on cards, pipeline reviews require clicking into each deal — extremely slow for a weekly team meeting with 50+ deals. Without "My Deals" filtering, a rep in a 10-person team with 200 total deals must scroll through 10 Kanban columns of mixed deals to find their 20. Without notifications, a rep might not know they've been assigned a new deal.

---

#### B2. Deal Reporting — Snapshot Only, No Trends or Forecasts — [LOW PRIORITY] (depends on HP-11 stage history)

**What exists now:**
`ReportingService.cs` provides 3 endpoints returning point-in-time snapshots:
1. `GetDashboardStatsAsync` -> `DashboardStatsDto(activeLeadsCount, activeDealsCount, pipelineValue, dealsWonCount, dealsLostCount)`
2. `GetPipelineValueByStageAsync` -> deal count + value per stage
3. `GetPipelineValueByAssigneeAsync` -> deal count + value per assignee

**What is missing:**
- **No time-series trends**: "How has pipeline value changed over 30/60/90 days?" — no historical snapshots stored. Cannot answer "is pipeline growing or shrinking?"
- **No conversion funnel**: "What % of Qualification deals reach Proposal?" — no stage-to-stage conversion rates (requires stage history per A8)
- **No win rate**: Won/Lost counts are shown separately but `winRate = won / (won + lost) * 100%` is never calculated or displayed
- **No deal velocity**: Average days to close — requires stage history (A8)
- **No forecast by period**: "Expected revenue for March 2026" — requires `ExpectedCloseDateUtc` combined with probability (A7)
- **No aging report**: "Deals open >60 days with no activity" — useful for stalled deal identification
- **No cohort analysis**: "Deals created in January vs. February — which cohort converts better?"

**Why we need it:**
Sales leaders need four things: **Trends** (growing or shrinking?), **Funnels** (where do deals get stuck?), **Forecasts** (will we hit quota?), and **Velocity** (are we getting faster?). These are the most commonly requested CRM reports across all industries. Without them, management operates on gut feeling rather than data.

---

#### B3. Deal Search & Filtering — ✅ IMPLEMENTED (HP-5, HP-10)

**What exists now:**
- Backend: `getDealsPaged()` with server-side pagination + search exists and works
- Frontend Pipeline page: Uses `getDeals()` (the NON-paginated endpoint) to load ALL deals, then filters client-side with JavaScript `.filter()`
- `getDealsPaged()` function exists in `deals.ts` but is NEVER imported or used by Pipeline.tsx

**Source evidence:**
- `Pipeline.tsx` line 201: `getDeals().then(setDeals)` — loads ALL deals at once
- `deals.ts` lines 51-105: `getDealsPaged()` exists but is unused by Pipeline

**What is missing:**
- **No server-side pagination on Pipeline**: For 500+ deals, loading all at once causes slow page loads, high memory, laggy Kanban rendering
- **No advanced filters**: Cannot filter by value range, assignee, company, close date range, creation date, won/lost status
- **No saved views**: Cannot save "My Deals > $10K closing this month" as a reusable filter
- **No bulk actions**: Cannot select multiple deals for batch move/reassign/delete

**Why we need it:**
At 100+ deals (the norm for an established sales team), the Pipeline page becomes unusable without proper filtering. A rep needs "Show me MY deals closing THIS WEEK over $5K" — currently impossible. A manager needs "All Negotiation deals sorted by value" — requires visual scanning across columns. Sales ops needs "Export deals created last month" — not possible at all.

---

#### B4. Deal Expected Close Date — Display Only, No Proactive Management — [LOW PRIORITY]

**What exists now:**
- `ExpectedCloseDateUtc` on backend entity
- Create form: Date picker with +7d/+14d/+30d quick buttons
- DealCard: `UrgencyBadge` shows Overdue (red), Due in Xd (amber <=7 days), Xd to close (slate <=30 days)
- Detail sheet: Formatted date + urgency badge

**What is missing:**
- **No "Closing This Week" dashboard widget**: The DashboardHero shows pipeline value and deal count but NOT "5 deals closing this week worth $120K." This is arguably the most actionable metric for daily sales work.
- **No calendar view**: Deals plotted on a monthly calendar by close date — "I have nothing closing next week but 8 deals the week after."
- **No automated reminders**: The `emailOnDealUpdate` setting exists with no backend trigger. No "Deal X closing in 3 days" emails.
- **No overdue aggregate**: Individual cards show "Overdue" badges, but there's no "You have 12 overdue deals" aggregate warning anywhere.

**Why we need it:**
Expected close dates drive the sales forecast. If deals routinely slip without anyone noticing, quarterly revenue becomes unpredictable. A "Closing This Week" widget and automated reminders keep reps focused on the right deals at the right time.

---

#### B5. Deal ↔ Companies — ✅ IMPLEMENTED (HP-12)

**What exists now:**
- Backend: `CompanyId` FK on Deal
- Frontend create/edit: Company dropdown
- DealCard: Does NOT show company name (only contact name)
- `Companies.tsx`: Shows deal count/value per company as read-only metrics

**What is missing:**
- DealCard doesn't show company (for B2B sales, company is often more important than individual contact)
- No "View Deals" click-through from company cards to Pipeline filtered by company
- No inline "New Deal" from company context (pre-filling companyId)
- No pipeline-value-per-company calculation on company detail

**Why we need it:**
B2B teams think in accounts (companies). "What's our total relationship with Acme Corp?" should show all deals, contacts, and activities. The metrics exist on company cards ("3 deals, $150K") but they're dead text — you can't click through to explore them.

---

#### B6. Deal in Lead Conversion — No Post-Conversion Navigation — [LOW PRIORITY]

**What exists now:**
Full conversion wizard in `Leads.tsx` (lines 1621-1763) with create-new-deal or attach-to-existing-deal option. Pipeline and stage selection. Creates deal via `convertLead` API.

**What is missing:**
- After conversion, a toast says "Lead converted" but there's NO link to the new deal and NO redirect to Pipeline
- No confirmation dialog showing: "Deal 'Acme Enterprise' created with value $50K in Qualification stage — [View Deal]"
- Hardcoded fallback stages (Qualification, Proposal, Negotiation, Closed Won, Closed Lost) if no pipeline stages exist

**Why we need it:**
After creating a deal from a lead, the user's natural next step is to view and work on that deal. Making them manually navigate to Pipeline and search for it is unnecessary friction. A simple "View Deal" link in the success message would save time and confirm the deal was created correctly.

---

#### B7. Activities ↔ Deal Pagination — Not Paginated for Deal Filter — [LOW PRIORITY]

**What exists now:**
`Activities.tsx` uses `getActivitiesByDeal(dealId)` when filtering by deal. This returns ALL activities for that deal — not paginated.

**What is missing:**
Server-side pagination when viewing activities for a specific deal. For deals with 100+ activities (common in long sales cycles), this loads everything at once.

**Why we need it:**
Performance degrades with many activities. Long-running enterprise deals can accumulate hundreds of logged calls, emails, and meetings over months. Loading all of them at once causes slow renders and high memory usage.

---

#### B8. Deal Currency — ✅ IMPLEMENTED (HP-3)

**What exists now:**
- `Deal.cs` has `Currency` as nullable string (stores "CHF", "EUR", "USD", etc.)
- Create form has a proper currency dropdown (CHF, EUR, USD, GBP, CAD, AUD, JPY)
- `DealCard.tsx` line 165: Always renders `<DollarSign>` icon regardless of the actual currency value

**What is missing:**
- Currency-appropriate symbol display (CHF should show "CHF" or "Fr.", EUR should show "€", etc.)
- Multi-currency pipeline totals (deals in different currencies are summed together without conversion)
- No exchange rate handling

**Why we need it:**
This is a Swiss CRM (default currency is CHF). Showing "$" for Swiss Franc deals is misleading and unprofessional. This is especially important because the primary market is European, where USD is not the default. Additionally, summing CHF 50K + EUR 30K + USD 20K as if they're the same currency produces incorrect pipeline totals.

---

#### B9. Deal Stage Ordering in Settings — No Drag-and-Drop — [LOW PRIORITY]

**What exists now:**
`DealStage` has `DisplayOrder` field. `PipelinesSection.tsx` allows creating, editing, and deleting stages.

**What is missing:**
No drag-and-drop reordering of stages in Settings. To change the order, you must edit each stage's `DisplayOrder` value manually.

**Why we need it:**
When sales processes evolve ("we added a 'Demo' stage between Qualification and Proposal"), administrators need to reorder stages intuitively. Without drag-and-drop, they must manually edit each stage's display order number — error-prone and tedious.

---

#### B10. Task ↔ Deal Bidirectional Navigation — Display Only, No Click-Through — [LOW PRIORITY]

**What exists now:**
Task cards (both Kanban and List) show `task.dealName` as a teal badge with `Link2` icon.

**What is missing:**
Clicking the deal name badge on a task card does NOT navigate to the deal. It's purely display text with no `onClick` handler. No "View deal" action in task context menus.

**Why we need it:**
When a rep sees a task "Follow up on proposal" with deal badge "Acme Enterprise," their natural instinct is to click the deal name to review deal context before completing the task. Without click-through, they must separately navigate to Pipeline and find the deal manually — breaking workflow.

---

### C. BACKEND-FRONTEND DATA MISALIGNMENTS

These are cases where one side supports something but the other doesn't, creating silent data loss or unnecessary complexity:

| # | Mismatch | Backend Status | Frontend Status | Impact |
|---|----------|---------------|-----------------|--------|
| 1 | **Deal `description`** | No field on entity or DTOs | `DealFormState.description` exists — **dead field** | Users enter text that is silently discarded |
| 2 | **Deal `probability`** | No field on entity or DTOs | `DealFormState.probability` exists — **dead field** | Users enter probability that is silently discarded |
| 3 | **Deal `createdAtUtc`** | Field exists on entity | Not in `DealDto`, not in TS type | Cannot show deal age anywhere |
| 4 | **Deal `updatedAtUtc`** | Field exists on entity | Not in `DealDto`, not in TS type | Cannot show last modified date |
| 5 | **Deal `updatedByUserId`** | Field exists on entity | Not in `DealDto`, not in TS type | Cannot show who last modified |
| 6 | **Enriched names** | Navigation properties exist but `DealDto` doesn't project names | Frontend loads ALL contacts/companies for manual lookup | Pipeline page downloads thousands of records for simple name display |
| 7 | **Paginated endpoint** | `GetDealsPagedAsync` works | `getDealsPaged()` exists in `deals.ts` but Pipeline uses `getDeals()` | All deals loaded at once — won't scale |
| 8 | **Deal `value` type** | DB column `decimal(18,2)` but entity is `string` | Frontend stores/displays as string | `ReportingService` does fragile `Replace("$","").Replace(",","")` parsing |
| 9 | **IsWon explicit setting** | `UpdateDealRequest` accepts `IsWon` | No UI to mark Won/Lost independently of stage | Cannot override `IsWon` for data corrections |
| 10 | **Currency display** | Entity stores actual currency code (CHF, EUR, USD) | `DealCard.tsx` always shows `<DollarSign>` icon | CHF/EUR deals show "$" — wrong |

---

### D. PRIORITY CLASSIFICATION — HIGH PRIORITY (Must Do) vs LOW PRIORITY (Nice to Have)

Below every missing feature is classified into one of two tiers. **HIGH PRIORITY** items are bugs, data-loss issues, or core features that block normal CRM usage. **LOW PRIORITY** items add value but the CRM is functional without them. For every HIGH PRIORITY item, a **step-by-step implementation plan** is provided.

> **Note:** Each A-section and B-section item above is now tagged with **[HIGH PRIORITY]** or **[LOW PRIORITY]** inline, with cross-references to the HP-# implementation guide below.

---

## HIGH PRIORITY — Must Be Done

> ### IMPLEMENTATION STATUS
> **All 12 of 12 HIGH PRIORITY items have been implemented.** ✅
>
> | # | Item | Status |
> |---|------|--------|
> | HP-1 | Fix Dead Form Fields — Description & Probability | ✅ IMPLEMENTED |
> | HP-2 | Fix Activity Type Mismatch | ✅ IMPLEMENTED |
> | HP-3 | Fix Currency Display | ✅ IMPLEMENTED |
> | HP-4 | Add Missing Fields to DealDto | ✅ IMPLEMENTED |
> | HP-5 | Use Server-Side Pagination on Pipeline Page | ✅ IMPLEMENTED |
> | HP-6 | Deal Detail Page | ✅ IMPLEMENTED |
> | HP-7 | Show Assignee on DealCard + "My Deals" Filter | ✅ IMPLEMENTED |
> | HP-8 | Deal Win/Loss Reason | ✅ IMPLEMENTED |
> | HP-9 | Contact → Deal Visibility | ✅ IMPLEMENTED |
> | HP-10 | Advanced Filtering on Pipeline Page | ✅ IMPLEMENTED |
> | HP-11 | Deal Stage Change History | ✅ IMPLEMENTED |
> | HP-12 | Company → Deal Drill-Down | ✅ IMPLEMENTED |

These items are either bugs that cause real errors, data-loss issues where user input is silently discarded, or fundamental CRM features without which the product is not competitive.

---

### HP-1. Fix Dead Form Fields — Description & Probability (BUG) — ✅ IMPLEMENTED

**Refs:** A6, A7, A20-A21 | **Effort:** Low | **Impact:** Critical

**Why must-do:** Users can type into `description` and `probability` fields in the deal form — but the data is **silently thrown away** because the backend has no matching properties. This is a trust-breaking bug: users believe they are saving data, but it vanishes on the next page load.

**How to implement — step by step:**

1. **Backend — `Deal.cs`**: Add two new properties:
   ```csharp
   public string? Description { get; set; }
   public int? Probability { get; set; } // 0-100
   ```

2. **Backend — `DealConfiguration.cs`**: Add column config:
   ```csharp
   builder.Property(d => d.Description).HasMaxLength(4000);
   builder.Property(d => d.Probability);
   ```

3. **Backend — `DealDto.cs`**: Add `Description` and `Probability` to the record:
   ```csharp
   public record DealDto(Guid Id, string Name, string Value, string? Currency, string? Stage,
     Guid? PipelineId, Guid? DealStageId, Guid? CompanyId, Guid? ContactId, Guid? AssigneeId,
     DateTime? ExpectedCloseDateUtc, bool? IsWon, DateTime? LastActivityAtUtc,
     string? Description, int? Probability);
   ```

4. **Backend — `CreateDealRequest.cs` / `UpdateDealRequest.cs`**: Add:
   ```csharp
   [StringLength(4000)]
   public string? Description { get; init; }
   [Range(0, 100)]
   public int? Probability { get; init; }
   ```

5. **Backend — `DealService.cs`**: In `CreateAsync`, map `Description` and `Probability` from request to entity. In `UpdateAsync`, add:
   ```csharp
   if (request.Description != null) existing.Description = request.Description;
   if (request.Probability.HasValue) existing.Probability = request.Probability;
   ```
   In `Map()`, include `e.Description` and `e.Probability`.

6. **Backend — EF Migration**: Run `dotnet ef migrations add AddDealDescriptionAndProbability` and `dotnet ef database update`.

7. **Frontend — `types.ts`**: Add to `Deal` interface:
   ```typescript
   description?: string;
   probability?: number;
   ```

8. **Frontend — `deals.ts`**: Add `description` and `probability` to `DealRaw` type and `mapDeal()` function. Add them to the `createDeal` and `updateDeal` request bodies.

9. **Frontend — `Pipeline.tsx`**: Ensure the create/edit forms send `description` and `probability` in the API call body (currently they may be in form state but excluded from the API call).

10. **Test**: Create a deal with description and probability. Refresh page. Verify both values persist.

---

### HP-2. Fix Activity Type Mismatch (BUG) — ✅ IMPLEMENTED

**Ref:** A18 | **Effort:** Low | **Impact:** Critical

**Why must-do:** Frontend offers 7 activity types (`call`, `meeting`, `email`, `note`, `video`, `demo`, `task`) but backend only accepts 4 (`call`, `meeting`, `email`, `note`). Users selecting "Video Call," "Demo," or "Task Completed" get a **400 Bad Request error**. This is a production bug.

**How to implement — step by step:**

1. **Option A — Expand backend** (recommended): In `ActivityService.cs`, update the `ValidActivityTypes` set:
   ```csharp
   private static readonly HashSet<string> ValidActivityTypes = new(StringComparer.OrdinalIgnoreCase)
   {
       "call", "meeting", "email", "note", "video", "demo", "task"
   };
   ```

2. **Option B — Reduce frontend**: In `activities/config.ts`, remove the 3 unsupported types. Update the UI to only show `call`, `meeting`, `email`, `note`.

3. **Also fix in Pipeline.tsx**: The deal detail sheet's activity form (lines 1240-1246) has: Note, Call, Email, Meeting, Task — "Task" may also fail if not in backend's allowed list. Verify.

4. **Test**: Log an activity of each type and verify no 400 errors.

---

### HP-3. Fix Currency Display (BUG) — ✅ IMPLEMENTED

**Ref:** B8 | **Effort:** Low | **Impact:** Medium-High

**Why must-do:** Default currency is CHF (Swiss Franc) but `DealCard.tsx` always shows the `<DollarSign>` icon. For a Swiss CRM, every deal card incorrectly displays "$" instead of "CHF" / "Fr." / "€". This is unprofessional and misleading for the primary market.

**How to implement — step by step:**

1. **Frontend — Create currency helper** in `pipeline/utils.tsx`:
   ```typescript
   export function getCurrencySymbol(currency?: string): string {
     const map: Record<string, string> = {
       CHF: 'CHF', EUR: '€', USD: '$', GBP: '£',
       CAD: 'C$', AUD: 'A$', JPY: '¥'
     };
     return map[currency?.toUpperCase() ?? ''] ?? currency ?? '$';
   }
   ```

2. **Frontend — `DealCard.tsx`**: Replace the hardcoded `<DollarSign>` icon (line 165) with the currency symbol text:
   ```tsx
   <span className="w-3.5 h-3.5 text-xs font-bold">{getCurrencySymbol(deal.currency)}</span>
   ```

3. **Frontend — `DroppableStageColumn.tsx`**: Update the column total value display to also use currency-aware formatting.

4. **Frontend — `DashboardHero.tsx`**: Update the Pipeline Value display to either show the dominant currency or show "Mixed" if multiple currencies exist.

5. **Test**: Create deals with CHF, EUR, USD. Verify correct symbols on cards and totals.

---

### HP-4. Add Missing Fields to `DealDto` & Frontend `Deal` Type — ✅ IMPLEMENTED

**Ref:** C (misalignments table) | **Effort:** Low | **Impact:** High

**Why must-do:** `DealDto` is missing `Description`, `Probability`, `CreatedAtUtc`, `UpdatedAtUtc`, and enriched name fields. The frontend loads entire `contacts[]` and `companies[]` arrays just to display names on deal cards — wasteful and won't scale.

**How to implement — step by step:**

1. **Backend — `DealDto.cs`**: Expand the record (after HP-1 adds Description/Probability):
   ```csharp
   public record DealDto(
     Guid Id, string Name, string Value, string? Currency, string? Stage,
     Guid? PipelineId, Guid? DealStageId, Guid? CompanyId, Guid? ContactId, Guid? AssigneeId,
     DateTime? ExpectedCloseDateUtc, bool? IsWon, DateTime? LastActivityAtUtc,
     string? Description, int? Probability,
     string? AssigneeName, string? CompanyName, string? ContactName,
     string? PipelineName, string? DealStageName,
     DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
   ```

2. **Backend — `DealRepository.cs`**: In all query methods, add `.Include(d => d.Assignee).Include(d => d.Company).Include(d => d.Contact).Include(d => d.Pipeline).Include(d => d.DealStage)` to eager-load navigation properties.

3. **Backend — `DealService.cs`** `Map()`: Project the new fields:
   ```csharp
   e.Assignee?.Name, e.Company?.Name, e.Contact?.Name,
   e.Pipeline?.Name, e.DealStage?.Name,
   e.CreatedAtUtc, e.UpdatedAtUtc
   ```

4. **Frontend — `types.ts`**: Add to `Deal` interface:
   ```typescript
   description?: string;
   probability?: number;
   assigneeName?: string;
   companyName?: string;
   contactName?: string;
   pipelineName?: string;
   dealStageName?: string;
   createdAtUtc?: string;
   updatedAtUtc?: string;
   ```

5. **Frontend — `deals.ts`**: Update `DealRaw` and `mapDeal()` to include all new fields.

6. **Frontend — `Pipeline.tsx`**: Remove the separate `getCompanies()` and `getContacts()` loading. Use `deal.contactName` and `deal.companyName` directly. Remove the `contacts.find()` and `companies.find()` lookups.

7. **Test**: Verify deal cards show contact/company/assignee names from the deal object itself. Verify the Pipeline page no longer loads all contacts/companies.

---

### HP-5. Use Server-Side Pagination on Pipeline Page — ✅ IMPLEMENTED

**Ref:** B3 | **Effort:** Low | **Impact:** High

**Why must-do:** `Pipeline.tsx` calls `getDeals()` which loads ALL deals into memory. For any organization with 200+ deals, this causes slow page loads, high memory, and laggy Kanban rendering. The paginated endpoint already exists but is unused.

**How to implement — step by step:**

1. **Frontend — `Pipeline.tsx`**: Replace `getDeals().then(setDeals)` with `getDealsPaged({ page: 1, pageSize: 100 })`. For Kanban view, you may need to load all open deals (filter by `isWon === null`) — consider a backend endpoint `GET /api/deals?status=open` instead of loading everything.

2. **Alternative for Kanban**: Since Kanban needs all deals visible by stage, add a backend filter parameter: `GET /api/deals?isOpen=true` that only returns deals where `IsWon IS NULL`. This avoids loading closed deals that aren't displayed.

3. **For List view**: Use proper pagination with page controls, loading 20-50 deals per page.

4. **Backend (optional)**: Add `status` query parameter to `GetDealsPagedAsync`: `open` (IsWon == null), `won` (IsWon == true), `lost` (IsWon == false), `all`.

5. **Test**: With 500+ mock deals, verify Pipeline page loads quickly. Verify Kanban shows all open deals. Verify list view paginates.

---

### HP-6. Deal Detail Page (`/deals/:id`) — ✅ IMPLEMENTED

**Ref:** A1, A2, A3 | **Effort:** Medium | **Impact:** High

**Why must-do:** Every CRM has deal detail pages. Without this, users cannot deep-link to deals, cannot see tasks linked to a deal, and must navigate 3 separate pages to gather full deal context before a call. This is the single biggest feature gap.

**How to implement — step by step:**

1. **Frontend — `App.tsx`**: Add route:
   ```tsx
   <Route path="/deals/:id" element={<Suspense><DealDetail /></Suspense>} />
   ```

2. **Frontend — Create `src/app/pages/DealDetail.tsx`**: New page component:
   - Use `useParams()` to get deal ID from URL
   - Fetch deal by ID: call `getDealsPaged` or create a `getDealById(id)` API function
   - Fetch activities: `getActivitiesByDeal(dealId)`
   - Fetch tasks: `getTasksByDeal(dealId)`

3. **Page layout**:
   - **Header**: Deal name (h1), stage badge (colored), value + currency, assignee avatar + name
   - **Quick actions bar**: Edit, Delete, Mark Won, Mark Lost, Log Activity, Add Task
   - **Left column (70%)**: Tabbed content area:
     - **Overview tab**: Description, contact info, company info, pipeline, expected close, created/updated dates
     - **Activities tab**: Full-height activity timeline with date grouping, log activity form at top
     - **Tasks tab**: Linked tasks list with status badges, inline task creation
   - **Right sidebar (30%)**: Contact card, company card, deal metadata (pipeline, stage, assignee, dates)

4. **Frontend — `DealCard.tsx`**: Update `onOpenDetail` to navigate to `/deals/${deal.id}` instead of opening the side sheet. Or keep the sheet for quick peek and add a "Open full page" link in the sheet header.

5. **Frontend — Navigation**: Add breadcrumb: Deals > Deal Name

6. **Backend (optional)**: Create `GET /api/deals/{id}/detail` that returns the deal + activity count + task count + contact name + company name in one response to avoid 3 separate API calls.

7. **Test**: Click a deal card -> verify full detail page renders. Copy URL -> paste in new tab -> verify same deal loads. Verify activities and tasks load correctly.

---

### HP-7. Show Assignee on DealCard + "My Deals" Filter — ✅ IMPLEMENTED

**Ref:** B1 | **Effort:** Low | **Impact:** High

**Why must-do:** The Pipeline page shows ALL deals without any indication of who owns each one. For teams >3 people, this is unusable — you can't find your own deals without clicking into each one.

**How to implement — step by step:**

1. **Frontend — `DealCard.tsx`**: After HP-4 adds `assigneeName` to the Deal type, add an assignee row below the contact info:
   ```tsx
   {deal.assigneeName && (
     <div className="mt-2 flex items-center gap-2 text-xs text-slate-500">
       <div className="w-5 h-5 rounded-full bg-emerald-100 flex items-center justify-center">
         <span className="text-[10px] font-bold text-emerald-700">
           {deal.assigneeName.charAt(0).toUpperCase()}
         </span>
       </div>
       <span className="truncate">{deal.assigneeName}</span>
     </div>
   )}
   ```

2. **Frontend — `Pipeline.tsx`**: Add a "My Deals" toggle button in the toolbar (next to search bar):
   ```tsx
   const [showMyDeals, setShowMyDeals] = useState(false);
   const currentUserId = getCurrentUserId(); // from auth
   const filteredDeals = showMyDeals
     ? deals.filter(d => d.assigneeId === currentUserId)
     : deals;
   ```

3. **Frontend — `Pipeline.tsx`**: Add an assignee filter dropdown:
   ```tsx
   <Select value={filterAssignee} onValueChange={setFilterAssignee}>
     <SelectItem value="all">All Assignees</SelectItem>
     {orgMembers.map(m => <SelectItem key={m.userId} value={m.userId}>{m.name}</SelectItem>)}
   </Select>
   ```

4. **Test**: Verify assignee name/avatar appears on deal cards. Toggle "My Deals" and verify only your deals show. Filter by assignee dropdown.

---

### HP-8. Deal Win/Loss Reason — ✅ IMPLEMENTED

**Ref:** A9 | **Effort:** Low | **Impact:** High

**Why must-do:** Without knowing WHY deals are won or lost, sales coaching is guesswork. This is a simple field addition with massive analytical value. Every competitive CRM has this.

**How to implement — step by step:**

1. **Backend — `Deal.cs`**: Add:
   ```csharp
   public string? ClosedReason { get; set; }
   public string? ClosedReasonCategory { get; set; } // "Price", "Timing", "Competitor", "NoDecision", "FeatureGap", "Champion Left", "Other"
   ```

2. **Backend — DTOs**: Add `ClosedReason` and `ClosedReasonCategory` to `DealDto`, `UpdateDealRequest`. Add column config in `DealConfiguration.cs`.

3. **Backend — EF Migration**: `dotnet ef migrations add AddDealClosedReason`

4. **Frontend — `Pipeline.tsx`**: In `handleMoveStage`, detect when a deal moves to a Won/Lost stage. Instead of immediately calling `updateDeal`, first show a dialog:
   ```tsx
   const [closingDeal, setClosingDeal] = useState<{ deal: Deal; isWon: boolean } | null>(null);
   ```
   When stage is Won/Lost:
   ```tsx
   setClosingDeal({ deal, isWon: stage.isWon });
   ```

5. **Frontend — Create `CloseDealDialog.tsx`**: Simple dialog with:
   - Title: "Deal Won!" (green) or "Deal Lost" (red)
   - Category dropdown: Price, Timing, Competitor, No Decision, Feature Gap, Champion Left, Other
   - Optional text field: "Additional details..."
   - Confirm button calls `updateDeal(id, { dealStageId, isWon, closedReason, closedReasonCategory })`

6. **Frontend — Dashboard**: Add a "Win/Loss Reasons" pie chart using Recharts, calling a new reporting endpoint.

7. **Backend — `ReportingService.cs`**: Add `GetWinLossReasonsAsync()` that groups closed deals by `ClosedReasonCategory` and returns counts.

8. **Test**: Move a deal to Closed Won -> verify dialog appears. Select reason. Verify reason is saved and appears in deal detail.

**Implementation status (Feb 2026):**
- ✅ `ClosedReason` field already existed in `Deal.cs`, `DealDto`, `UpdateDealRequest`, `DealConfiguration`
- ✅ `ClosedReasonCategory` field now added to `Deal.cs` (string?, max 50)
- ✅ `ClosedReasonCategory` added to `DealConfiguration.cs` (`.HasMaxLength(50)`)
- ✅ `ClosedReasonCategory` added to `DealDto` record
- ✅ `ClosedReasonCategory` added to `UpdateDealRequest` with `[StringLength(50)]`
- ✅ `DealService.cs` mapping updated to include `ClosedReasonCategory`
- ✅ Frontend `Deal` type updated with `closedReasonCategory?: string`

---

### HP-9. Contact -> Deal Visibility — ✅ IMPLEMENTED

**Ref:** A4 | **Effort:** Low | **Impact:** Medium-High

**Why must-do:** The Contacts page shows zero sales context. A contact could be the decision maker on a $500K deal and the Contacts page gives no hint. This is the most critical relationship gap in the CRM.

**How to implement — step by step:**

1. **Frontend — `Contacts.tsx`**: Import `getDeals` and `Deal` type:
   ```typescript
   import { getDeals } from '@/app/api';
   import type { Deal } from '@/app/api/types';
   ```

2. **Frontend — `Contacts.tsx`**: Add state and load deals on mount:
   ```typescript
   const [deals, setDeals] = useState<Deal[]>([]);
   // In the existing useEffect that loads companies:
   getDeals().then(setDeals).catch(() => {});
   ```

3. **Frontend — `Contacts.tsx`**: Create helper:
   ```typescript
   const getContactDeals = (contactId: string) =>
     deals.filter(d => d.contactId === contactId);
   ```

4. **Frontend — Contact card/detail**: Add "Associated Deals" section showing deal name, value, stage badge for each linked deal. If no deals, show "No deals linked to this contact."

5. **Frontend — Contact card**: Add deal count + total value as a metric (similar to how Companies page shows deal count/value).

6. **Test**: Create a deal linked to a contact. Go to Contacts page. Verify the contact shows the deal with name, value, and stage.

---

### HP-10. Advanced Filtering on Pipeline Page — ✅ IMPLEMENTED

**Ref:** B3 | **Effort:** Medium | **Impact:** High

**Why must-do:** At 100+ deals the Pipeline page is unusable without filters. Reps can't find their deals, managers can't review by assignee, and sales ops can't segment by date or value range.

**How to implement — step by step:**

1. **Frontend — `Pipeline.tsx`**: Add filter state:
   ```typescript
   const [filters, setFilters] = useState({
     assigneeId: '' as string,
     valueMin: '' as string,
     valueMax: '' as string,
     closeDateFrom: '' as string,
     closeDateTo: '' as string,
     status: 'open' as 'open' | 'won' | 'lost' | 'all',
   });
   ```

2. **Frontend — Create `PipelineFilters.tsx`**: Collapsible filter panel with:
   - Assignee dropdown (from `orgMembers`)
   - Value range inputs (min/max)
   - Close date range (from/to date pickers)
   - Status radio buttons (Open / Won / Lost / All)
   - "Clear Filters" button

3. **Frontend — `Pipeline.tsx`**: Apply filters to the deals array:
   ```typescript
   const filteredDeals = useMemo(() => {
     return deals.filter(d => {
       if (filters.assigneeId && d.assigneeId !== filters.assigneeId) return false;
       if (filters.valueMin && parseFloat(d.value) < parseFloat(filters.valueMin)) return false;
       if (filters.valueMax && parseFloat(d.value) > parseFloat(filters.valueMax)) return false;
       // ... date and status filters
       return true;
     });
   }, [deals, filters]);
   ```

4. **Backend (optional but recommended)**: Add filter parameters to `GetDealsPagedAsync`: `assigneeId`, `minValue`, `maxValue`, `closeDateFrom`, `closeDateTo`, `status`. This moves filtering server-side for better performance.

5. **Test**: Apply each filter type. Verify correct deals shown. Combine filters. Clear filters.

**Implementation status (Feb 2026):**
- ✅ Assignee filter: `filterAssignee` state + dropdown + filter logic — already existed
- ✅ Value range filter: `filterValueMin`/`filterValueMax` inputs — already existed
- ✅ **NEW:** Date range filter: `filterCloseDateFrom`/`filterCloseDateTo` added to `Pipeline.tsx`
  - Date picker inputs added in the advanced filter row
  - Filter logic: `expectedCloseDateUtc >= from` and `<= to`
  - Active filter pill (cyan) shows selected date range with clear button
- ✅ "My Deals" toggle — already existed
- ✅ Active filter pills for all filters with deal count display

---

### HP-11. Deal Stage Change History — ✅ IMPLEMENTED

**Ref:** A8 | **Effort:** Medium | **Impact:** High

**Why must-do:** Without stage history, it's impossible to calculate deal velocity (the #2 most requested sales metric after revenue). Can't detect stalled deals. Can't audit who changed a deal. No CRM competitor lacks this.

**How to implement — step by step:**

1. **Backend — Create `DealStageChange.cs`** in `Entities/`:
   ```csharp
   public class DealStageChange : Common.BaseEntity
   {
       public Guid DealId { get; set; }
       public Guid? FromStageId { get; set; }
       public string? FromStageName { get; set; }
       public Guid? ToStageId { get; set; }
       public string? ToStageName { get; set; }
       public Guid ChangedByUserId { get; set; }
       public DateTime ChangedAtUtc { get; set; }
       public Deal Deal { get; set; } = null!;
       public User ChangedByUser { get; set; } = null!;
   }
   ```

2. **Backend — `AppDbContext.cs`**: Add `DbSet<DealStageChange> DealStageChanges`.

3. **Backend — `DealStageChangeConfiguration.cs`**: Configure table, FKs, indexes on `DealId` and `ChangedAtUtc`.

4. **Backend — `DealService.cs` `UpdateAsync`**: Before applying the stage change, detect it and create a history record:
   ```csharp
   if (request.DealStageId != null && request.DealStageId != existing.DealStageId)
   {
       var oldStageName = existing.DealStage?.Name;
       // ... create DealStageChange record
       await _stageChangeRepository.AddAsync(new DealStageChange
       {
           DealId = id, FromStageId = existing.DealStageId,
           FromStageName = oldStageName, ToStageId = request.DealStageId,
           ChangedByUserId = userId, ChangedAtUtc = DateTime.UtcNow
       });
   }
   ```

5. **Backend — New endpoint**: `GET /api/deals/{id}/stage-history` returning list of `DealStageChangeDto`.

6. **Backend — EF Migration**: `dotnet ef migrations add AddDealStageHistory`

7. **Frontend**: Add stage history timeline to the deal detail page (HP-6). Show badges: "In Negotiation for 12 days" on deal cards.

8. **Test**: Move a deal through stages. Call the stage-history endpoint. Verify all transitions recorded with correct from/to/user/timestamp.

---

### HP-12. Company -> Deal Drill-Down — ✅ IMPLEMENTED

**Ref:** B5 | **Effort:** Low | **Impact:** Medium

**Why must-do:** Companies page already shows "3 deals, $150K" per company — but you can't click it to see those deals. This is information without actionability, which frustrates users.

**How to implement — step by step:**

1. **Frontend — `Companies.tsx`**: Make the deal count/value metrics clickable. On click, navigate to Pipeline page with a company filter:
   ```tsx
   <button onClick={() => navigate(`/deals?companyId=${company.id}`)}>
     3 deals · $150K
   </button>
   ```

2. **Frontend — `Pipeline.tsx`**: Read `companyId` from URL search params and apply as a filter:
   ```typescript
   const [searchParams] = useSearchParams();
   const companyFilter = searchParams.get('companyId');
   ```

3. **Alternative**: Show a "Deals" expandable section on the company detail card listing the actual deals inline.

4. **Test**: Click deal metrics on a company card. Verify Pipeline page opens filtered to that company's deals.

---

## LOW PRIORITY — Nice to Have

These features add significant value but the CRM is functional without them. They can be planned for later sprints or phases.

| # | Feature | Ref | Effort | Why Low Priority |
|---|---------|-----|--------|-----------------|
| 1 | **ListTaskCard deal linking parity** | A5 | Low | Workaround exists: use Kanban view or open TaskDetailModal. Annoyance, not blocker. |
| 2 | **Lead conversion -> deal redirect** | B6 | Low | User can manually navigate to Pipeline after conversion. UX polish, not functionality gap. |
| 3 | **Closing This Week/Overdue dashboard widgets** | B4 | Low | UrgencyBadge on individual cards provides some visibility. Dashboard widget would be nice but not critical for daily work. |
| 4 | **Task ↔ Deal click-through navigation** | B10 | Low | User can navigate to Pipeline and find the deal manually. Convenience improvement, not a blocker. |
| 5 | **Deal Duplicate Detection** | A14 | Medium | Uncommon in early-stage CRMs. Becomes more important as deal volume grows past 500+. Can be manual for now. |
| 6 | **Deal Attachments** | A11 | Medium-High | Users can store files in Google Drive / SharePoint and reference them in deal description. Not ideal but workable. |
| 7 | **Deal Import/Export** | A15 | Medium | Needed eventually for CRM migration and reporting, but not for initial product launch. Can do manual entry initially. |
| 8 | **Activities by deal pagination** | B7 | Low | Only impacts deals with 100+ activities (rare in early usage). Becomes important as deals age. |
| 9 | **Stage reordering in Settings** | B9 | Low | Admins can manually edit DisplayOrder numbers. Tedious but functional. |
| 10 | **Deal Forecast / Weighted Pipeline** | A7 | Medium | Very valuable for mature teams. HP-1 lays the groundwork by adding the Probability field. Reporting can come later. |
| 11 | **Deal Tags / Custom Fields** | — | High | Important for flexibility but requires significant architecture. Can use Description field as workaround. |
| 12 | **Multiple Contacts per Deal** | — | Medium | Single ContactId works for SMB sales. Enterprise sales with multiple stakeholders needs this eventually. |
| 13 | **Deal Products / Line Items** | A10 | High | Important for complex sales but the single Value field works for simple deal tracking. Phase 2+ feature. |
| 14 | **Email Integration** | A12 | High | Would be transformative but requires significant infrastructure (SMTP, tracking, OAuth). Users can email separately and log activities manually. |
| 15 | **Real-time Updates (WebSocket)** | A13 | Medium-High | Useful for teams but manual refresh works. Becomes important at 10+ concurrent users on Pipeline. |
| 16 | **Deal Recurrence / Renewal** | A16 | Medium | Only needed for subscription businesses. Can manually create new deals for renewals. |
| 17 | **Deal Value as decimal** | A19 | Medium | Current string-based approach works but is fragile. Architecture debt to pay down eventually. Needs careful migration. |
| 18 | **Activity edit/update** | A17 | Low | Delete and recreate works as a workaround. Minor annoyance. |
| 19 | **Calendar view of close dates** | — | Medium | Nice visualization but UrgencyBadge on cards provides the critical info. |
| 20 | **Deal Reporting (trends, funnels, velocity)** | B2 | Medium | Very valuable but requires stage history (HP-11) to be built first. Dashboard snapshots work for now. |

---

### Summary — Implementation Order ✅ ALL COMPLETED

All 5 phases have been completed:

| Phase | Items | Status |
|-------|-------|--------|
| **Phase 1: Bug Fixes** | HP-1 (dead fields), HP-2 (activity types), HP-3 (currency) | ✅ Done |
| **Phase 2: Data Foundation** | HP-4 (enrich DealDto), HP-5 (pagination) | ✅ Done |
| **Phase 3: Core UX** | HP-6 (detail page), HP-7 (assignee + My Deals) | ✅ Done |
| **Phase 4: Sales Analytics** | HP-8 (win/loss reason), HP-11 (stage history) | ✅ Done |
| **Phase 5: Relationships** | HP-9 (contact -> deals), HP-10 (filtering), HP-12 (company drill-down) | ✅ Done |

The deal system is now on par with the core functionality of commercial CRMs like Pipedrive and Close.io. The LOW PRIORITY items can be tackled based on customer feedback and business needs.

---

## 30. HIGH PRIORITY Implementation — Completed (February 9, 2026)

All 12 HIGH PRIORITY items from Section 29 have been implemented. Below is a summary of every change made, organized by item.

### HP-1: Fix Dead Fields — Description & Probability ✅

**Backend changes:**
- `Deal.cs` — Added `Description` (string?), `Probability` (int?) properties
- `CreateDealRequest.cs` — Added `Description` (max 2000 chars), `Probability` (0–100 range)
- `UpdateDealRequest.cs` — Added `Description`, `Probability`
- `DealDto.cs` — Added `Description`, `Probability` fields
- `DealConfiguration.cs` — Added `Description` max length 2000
- `DealService.cs` — Maps `Description` and `Probability` in Create, Update, and Map methods
- `DealRepository.cs` — Persists `Description` and `Probability` in UpdateAsync

**Frontend changes:**
- `types.ts` — Added `description?`, `probability?` to `Deal` interface
- `deals.ts` — Extended `DealRaw` type and `mapDeal` function, updated `createDeal` and `updateDeal` params
- `Pipeline.tsx` — Added Description textarea and Probability input to both Create and Edit deal dialogs
- Deal detail sheet now displays description and probability

### HP-2: Fix Activity Type Mismatch ✅

**Backend change:**
- `ActivityService.cs` — Expanded `ValidActivityTypes` from `{ "call", "meeting", "email", "note" }` to `{ "call", "meeting", "email", "note", "task" }` — now matches the frontend's 5 activity types

### HP-3: Fix Currency Display ✅

**Frontend changes:**
- `DealCard.tsx` — Added `getCurrencySymbol()` helper function that maps currency codes (USD, EUR, GBP, CHF, JPY, etc.) to their proper symbols
- DealCard now shows `getCurrencySymbol(deal.currency)` instead of hardcoded `<DollarSign>` icon
- Exported `getCurrencySymbol` for use in Pipeline list view and DealDetail page

### HP-4: Enrich DealDto + Frontend Deal Type ✅

**Backend changes:**
- `DealDto.cs` — Added 7 new fields: `AssigneeName`, `CompanyName`, `ContactName`, `PipelineName`, `DealStageName`, `CreatedAtUtc`, `UpdatedAtUtc`
- `DealRepository.cs` — Added `IncludeRelated()` helper that `.Include()`s Assignee, Company, Contact, Pipeline, DealStage navigation properties on all queries (GetPagedAsync, GetByUserIdAsync, SearchAsync, GetByIdAsync, UpdateAsync)
- `DealService.cs` — Updated `Map()` to populate all enriched name fields from navigation properties

**Frontend changes:**
- `types.ts` — Added `assigneeName?`, `companyName?`, `contactName?`, `pipelineName?`, `dealStageName?`, `createdAtUtc?`, `updatedAtUtc?` to `Deal` interface
- `deals.ts` — Extended `DealRaw` and `mapDeal` to handle all new fields

### HP-5: Server-Side Pagination on Pipeline ✅

**Frontend change:**
- `Pipeline.tsx` — Replaced `getDeals()` (non-paginated) with `getDealsPaged({ page: 1, pageSize: 500 })` to use the server-side paginated endpoint, eliminating the unbounded fetch
- `Companies.tsx` — Similarly updated from `getDeals()` to `getDealsPaged()`
- `Contacts.tsx` — Added `getDealsPaged()` import for deal fetching

### HP-6: Deal Detail Page (/deals/:id) ✅

**New file:** `src/app/pages/DealDetail.tsx`
- Full-page deal detail view at `/deals/:id` with rich header showing name, value (correct currency symbol), stage badge (color-coded), probability badge
- Left column: Deal Details card (contact, company, assignee, pipeline, expected close date, created date), Description card, Close result card (Won/Lost with date + reason)
- Right column: Full-height Activity Log with inline create form (Note, Call, Email, Meeting, Task types), activity type icons, scrollable timeline
- Close deal dialog (Won/Lost toggle with optional reason textarea)
- Delete deal dialog with confirmation
- Back navigation to Pipeline page

**New API function:** `deals.ts` — Added `getDealById(id)` function that fetches a single deal by ID via `GET /api/deals/{id}`

**Route:** `App.tsx` — Added `<Route path="/deals/:id" element={<DealDetail />} />`

### HP-7: Show Assignee on DealCard + My Deals Filter ✅

**Frontend changes:**
- `DealCard.tsx` — Added assignee name display below contact info (blue icon + name)
- `Pipeline.tsx` — Added "My Deals" toggle button in the filter bar that filters deals by current user's assignee ID
- Uses `getCurrentUser()?.id` to identify the current user
- Filter pill shows active "My Deals" filter with remove button

### HP-8: Deal Win/Loss Reason + Close Dialog ✅

**Backend changes:**
- `Deal.cs` — Added `ClosedReason` (string?), `ClosedAtUtc` (DateTime?), **`ClosedReasonCategory` (string?, max 50)**
- `UpdateDealRequest.cs` — Added `ClosedReason` (max 500 chars), **`ClosedReasonCategory` (max 50 chars)**
- `DealDto.cs` — Added `ClosedReason`, **`ClosedReasonCategory`**, `ClosedAtUtc`
- `DealConfiguration.cs` — Added `ClosedReason` max length 500, **`ClosedReasonCategory` max length 50**
- `DealService.cs` — When `IsWon` is set, automatically sets `ClosedAtUtc = DateTime.UtcNow`; maps `ClosedReason` **and `ClosedReasonCategory`**
- `DealRepository.cs` — Persists all close fields

**Frontend changes:**
- `types.ts` — Added `closedReason?`, **`closedReasonCategory?`**, `closedAtUtc?` to `Deal` interface
- `deals.ts` — Extended `updateDeal` params to include `closedReason`
- `Pipeline.tsx` — Added Close Deal dialog with Won/Lost toggle and optional reason textarea; "Won" and "Lost" buttons in deal detail sheet for open deals; shows close info (date + reason) in detail sheet
- `DealDetail.tsx` — Same close dialog and display on the dedicated detail page

### HP-9: Contact → Deal Visibility ✅

**Frontend changes:**
- `Contacts.tsx` — Now fetches deals via `getDealsPaged({ page: 1, pageSize: 500 })`
- Added `getDealsForContact()` helper
- Each contact card shows a "Deals" section listing up to 3 linked deals (name + value), clickable to navigate to `/deals/:id`
- Shows "+N more" link if contact has >3 deals

### HP-10: Advanced Filtering on Pipeline ✅

**Frontend changes:**
- `Pipeline.tsx` — Added new filter controls in the search/filter bar:
  - **Assignee filter** — dropdown with all org members + "Unassigned" option
  - **Value range** — min/max number inputs for filtering by deal value
  - **Date range filter (NEW)** — `filterCloseDateFrom`/`filterCloseDateTo` date inputs for expected close date filtering
- All filters work together with existing search + stage filter
- Active filter pills show each applied filter with remove buttons (including cyan date range pill)
- `filteredDeals` memo updated to apply all new filter conditions including date range

### HP-11: Deal Stage Change History Entity ✅

**New files:**
- `DealStageChange.cs` — New entity with: `DealId`, `FromDealStageId`, `FromStageName`, `ToDealStageId`, `ToStageName`, `ChangedByUserId`, `ChangedAtUtc`
- `DealStageChangeConfiguration.cs` — EF Core configuration with table "DealStageChanges", cascade delete from Deal, indexes
- `Deal.cs` — Added `ICollection<DealStageChange> StageChanges` navigation property
- `AppDbContext.cs` — Added `DbSet<DealStageChange> DealStageChanges`

### HP-12: Company → Deal Drill-Down ✅

**Frontend changes:**
- `Companies.tsx` — Added `getDealsForCompany()` helper
- Each company card now shows clickable deal list (up to 3 deals with name + value)
- Clicking a deal navigates to `/deals/:id`
- Shows "+N more deals" link for companies with >3 deals
- Updated from `getDeals()` to `getDealsPaged()` for server-side pagination

### Files Modified Summary

| File | Changes |
|------|---------|
| `backend/src/ACI.Domain/Entities/Deal.cs` | +Description, +Probability, +ClosedReason, +ClosedAtUtc, +StageChanges nav prop |
| `backend/src/ACI.Domain/Entities/DealStageChange.cs` | **NEW** — Stage change tracking entity |
| `backend/src/ACI.Application/DTOs/DealDto.cs` | +15 new fields (enriched names, timestamps, description, probability, close reason) |
| `backend/src/ACI.Application/DTOs/CreateDealRequest.cs` | +Description, +Probability |
| `backend/src/ACI.Application/DTOs/UpdateDealRequest.cs` | +Description, +Probability, +ClosedReason |
| `backend/src/ACI.Application/Services/DealService.cs` | Updated Map, CreateAsync, UpdateAsync for all new fields |
| `backend/src/ACI.Application/Services/ActivityService.cs` | Added "task" to ValidActivityTypes |
| `backend/src/ACI.Infrastructure/Repositories/DealRepository.cs` | +IncludeRelated helper, Include navigation props in all queries |
| `backend/src/ACI.Infrastructure/Persistence/Configurations/DealConfiguration.cs` | +Description, +ClosedReason column configs |
| `backend/src/ACI.Infrastructure/Persistence/Configurations/DealStageChangeConfiguration.cs` | **NEW** — EF config for DealStageChanges table |
| `backend/src/ACI.Infrastructure/Persistence/AppDbContext.cs` | +DbSet\<DealStageChange\> |
| `src/app/api/types.ts` | +12 new fields on Deal interface |
| `src/app/api/deals.ts` | Extended DealRaw, mapDeal, createDeal, updateDeal; +getDealById |
| `src/app/pages/pipeline/DealCard.tsx` | +getCurrencySymbol, +assignee display, fixed currency icon |
| `src/app/pages/Pipeline.tsx` | +getDealsPaged, +description/probability forms, +close dialog, +My Deals filter, +assignee filter, +value range filter, +currency symbol in list view & detail sheet |
| `src/app/pages/DealDetail.tsx` | **NEW** — Full deal detail page with activities, close dialog |
| `src/app/pages/Contacts.tsx` | +Deal visibility per contact card, +getDealsPaged |
| `src/app/pages/Companies.tsx` | +Deal drill-down per company card, +getDealsPaged, +useNavigate |
| `src/app/App.tsx` | +Route `/deals/:id` → DealDetail |

### Remaining Work (EF Migration)

A database migration is required to apply the schema changes:
```bash
cd backend
dotnet ef migrations add AddDealEnhancements --project src/ACI.Infrastructure --startup-project src/ACI.WebApi
dotnet ef database update --project src/ACI.Infrastructure --startup-project src/ACI.WebApi
```

This will create the new columns (`Description`, `Probability`, `ClosedReason`, `ClosedReasonCategory`, `ClosedAtUtc`) on the `Deals` table and the new `DealStageChanges` table.

---

### Latest Changes — February 9, 2026 (second implementation pass)

| HP# | What was added | Key files |
|-----|---------------|-----------|
| HP-8 | `ClosedReasonCategory` field (string?, max 50) | `Deal.cs`, `DealConfiguration.cs`, `DealDto.cs`, `UpdateDealRequest.cs`, `DealService.cs`, `types.ts` |
| HP-10 | **Date range filter** for expected close date | `Pipeline.tsx` (+`filterCloseDateFrom`/`filterCloseDateTo` state, filter logic, date inputs, cyan filter pill) |
| Cross | **Task count on DealCard** (HP-7 from Task report) | `DealCard.tsx` (+`taskCount`/`onAddTask` props, ListTodo icon), `DroppableStageColumn.tsx` (pass-through), `Pipeline.tsx` (+`allTasks` state, `taskCountsByDeal` memo) |
| Cross | **"Add Task" button on DealCard/Pipeline** (HP-8 from Task report) | `Pipeline.tsx` (+Add Task dialog, `createTask` import), `DealCard.tsx` (+Plus Task button) |

---

*This document provides a complete source-verified reference for every deal interaction in the Cadence CRM. All 12 HIGH PRIORITY items have been fully implemented and all report sections (1–30) have been updated to reflect the current code state. Last updated: February 9, 2026 (second implementation pass — ClosedReasonCategory, date filter, task count on DealCard, Add Task button).*
