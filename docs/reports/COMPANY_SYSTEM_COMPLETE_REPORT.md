# Company System ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Complete Interaction Report

This document is the single definitive reference for **every interaction** the "Company" entity has across the entire Cadence CRM codebase (backend and frontend). It covers the entity definition, API layer, business logic, database configuration, UI components, cross-entity relationships, and identifies what is missing ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â prioritized by importance with detailed explanations of **why** each item matters and implementation guidance for high-priority items.

Last updated: February 9, 2026 (sixth pass ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â ALL HIGH PRIORITY items implemented, report updated with implementation status).

---

## Table of Contents

1. [Company Entity (Backend Domain)](#1-company-entity-backend-domain)
2. [Backend API Endpoints](#2-backend-api-endpoints)
3. [Backend Business Logic (Services)](#3-backend-business-logic-services)
4. [Backend Data Access (Repositories)](#4-backend-data-access-repositories)
5. [Backend DTOs](#5-backend-dtos)
6. [Backend Validation & Error Handling](#6-backend-validation--error-handling)
7. [Backend Validation Helper ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Domain Regex](#7-backend-validation-helper--domain-regex)
8. [Database Configuration (EF Core)](#8-database-configuration-ef-core)
9. [Cross-Entity Company Interactions (Backend)](#9-cross-entity-company-interactions-backend)
10. [Backend Unit Tests ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â CompanyServiceTests](#10-backend-unit-tests--companyservicetests)
11. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â API Client Layer](#11-frontend--api-client-layer)
12. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â TypeScript Types](#12-frontend--typescript-types)
13. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â React Query Hooks](#13-frontend--react-query-hooks)
14. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Companies Page (Main Company UI)](#14-frontend--companies-page-main-company-ui)
15. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Company Detail Page](#15-frontend--company-detail-page)
16. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Contacts Page (Company Linking)](#16-frontend--contacts-page-company-linking)
17. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Contact Detail Page (Company Link)](#17-frontend--contact-detail-page-company-link)
18. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Pipeline Page (Company in Deals)](#18-frontend--pipeline-page-company-in-deals)
19. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Leads Page (Company in Conversion)](#19-frontend--leads-page-company-in-conversion)
20. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Dashboard (Company in Sales Writer)](#20-frontend--dashboard-company-in-sales-writer)
21. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Dashboard Config (Company Shortcut)](#21-frontend--dashboard-config-company-shortcut)
22. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â SendToCrm Page](#22-frontend--sendtocrm-page)
23. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Global Search (Companies)](#23-frontend--global-search-companies)
24. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Copy Generator (Company Name in Templates)](#24-frontend--copy-generator-company-name-in-templates)
25. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Settings (Company Name in Profile)](#25-frontend--settings-company-name-in-profile)
26. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Homepage (Company Feature Showcase)](#26-frontend--homepage-company-feature-showcase)
27. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Lead Webhook (CompanyName Field)](#27-frontend--lead-webhook-companyname-field)
28. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Messages (Company Toast Messages)](#28-frontend--messages-company-toast-messages)
29. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Navigation & Routing](#29-frontend--navigation--routing)
30. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Mock Data](#30-frontend--mock-data)
31. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Query Keys](#31-frontend--query-keys)
32. [Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Test Mock Handlers](#32-frontend--test-mock-handlers)
33. [Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â DI Registration & Diagnostic Endpoints](#33-backend--di-registration--diagnostic-endpoints)
34. [Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â UserSettings (CompanyName)](#34-backend--usersettings-companyname)
35. [Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â TemplateCopyGenerator (Company in AI Copy)](#35-backend--templatecopygenerator-company-in-ai-copy)
36. [Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â CopyGeneratorService (Company in AI Copy Orchestration)](#36-backend--copygeneratorservice-company-in-ai-copy-orchestration)
37. [Complete File Inventory](#37-complete-file-inventory)
38. [Complete Relationship Map](#38-complete-relationship-map)
39. [What Is Missing ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Prioritized](#39-what-is-missing--prioritized)

---

## 1. Company Entity (Backend Domain)

**File:** `backend/src/ACI.Domain/Entities/Company.cs` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **25 lines**

The core Company entity representing a business account/organization in the CRM:

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Primary key (from BaseEntity) |
| `UserId` | `Guid` | FK to owning User (required) |
| `OrganizationId` | `Guid?` | FK to Organization (when set, data is scoped by organization; when null, legacy user-scoped) |
| `Name` | `string` | Company name (required, default `string.Empty`) |
| `Domain` | `string?` | Company domain/website (e.g., "acme.com") |
| `Industry` | `string?` | Industry sector (e.g., "Technology", "Healthcare") |
| `Size` | `string?` | Company size (e.g., "1-10", "51-200") |
| `CreatedAtUtc` | `DateTime` | Creation timestamp |
| `UpdatedAtUtc` | `DateTime?` | Last update timestamp (nullable) |
| `UpdatedByUserId` | `Guid?` | FK to User who last updated |

**Navigation Properties:**
- `User` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Owner of this company record (required)
- `UpdatedByUser` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Last updater (optional)
- `Organization` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Org scope (optional)
- `Contacts` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â `ICollection<Contact>` contacts linked to this company
- `Deals` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â `ICollection<Deal>` deals linked to this company
- `Leads` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â `ICollection<Lead>` leads linked to this company

> **CRITICAL: Backend entity has only 4 user-facing data fields**: `Name`, `Domain`, `Industry`, `Size`. There is NO `Description`, `Website`, `Location`, `Phone`, `Email`, `Address`, `Revenue`, or `Logo` field on the backend entity. However, the **frontend sends** `description`, `website`, and `location` to the backend API ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â these fields are silently ignored by the backend because the `CreateCompanyRequest`/`UpdateCompanyRequest` DTOs don't include them, and C# model binding ignores unknown JSON properties.

---

## 2. Backend API Endpoints

### CompaniesController (`backend/src/ACI.WebApi/Controllers/CompaniesController.cs`) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **230 lines**

All endpoints require `[Authorize]`. All are scoped to the authenticated user's `UserId` and `OrganizationId` (from `ICurrentUserService`). Produces `application/json`.

| Method | Endpoint | Description | Response |
|--------|----------|-------------|----------|
| `GET` | `/api/companies?page=&pageSize=&search=` | Paginated list (page default 1, pageSize default 20, max 100, optional search) | `PagedResult<CompanyDto>` |
| `GET` | `/api/companies/all` | Get all companies non-paginated (backward compat) | `IReadOnlyList<CompanyDto>` |
| `GET` | `/api/companies/search?q=` | Search companies by name/domain/industry (non-paginated, max 20 results) | `IReadOnlyList<CompanyDto>` |
| `GET` | `/api/companies/{id}` | Get single company by ID | `CompanyDto` |
| `POST` | `/api/companies` | Create new company (body: `CreateCompanyRequest`) | `CompanyDto` |
| `PUT` | `/api/companies/{id}` | Update company ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â partial (body: `UpdateCompanyRequest`) | `CompanyDto` |
| `DELETE` | `/api/companies/{id}` | Delete company (returns 204) | No content |

**Controller notes:**
- `pageSize` has no max enforcement in the controller ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â the default is 20 but the user can pass any value (the controller comment says "max 100" but there is no validation). The frontend's `CompanyDetail.tsx` passes `pageSize: 1000` as a fallback, which works.
- `Result.ToActionResult()` and `Result.ToNoContentResult()` extension methods handle error-to-HTTP mapping.

### Other Controllers that reference Companies

| Controller | Endpoint | Company Interaction |
|------------|----------|---------------------|
| `LeadsController` | `POST /api/leads/{id}/convert` | Convert lead ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ can create/link a company |
| `SearchController` | `GET /api/search?q=` | Global search includes companies (via `GlobalSearchService`) |

---

## 3. Backend Business Logic (Services)

### CompanyService (`backend/src/ACI.Application/Services/CompanyService.cs`) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **234 lines**

Depends on: `ICompanyRepository`, `ILogger<CompanyService>`.

| Method | Logic |
|--------|-------|
| `GetCompaniesPagedAsync` | Calculates `skip = (page - 1) * pageSize`, delegates to `_repository.GetPagedAsync()`. Maps entities to DTOs via `Map()`. |
| `GetCompaniesAsync` | All companies for user/org (non-paginated). Ordered by name. Maps to DTOs. |
| `SearchAsync` | Search by name/domain/industry (case-insensitive). Delegates to `_repository.SearchAsync()`. Passes empty string if query is null. Maps to DTOs. |
| `GetByIdAsync` | Single company lookup. Returns `DomainErrors.Company.NotFound` if not found. |
| `CreateAsync` | **Validates:** (1) Name is required (non-empty/whitespace), (2) Domain format is validated via `ValidationHelper.IsValidDomain()` if provided. Sets `CreatedAtUtc = DateTime.UtcNow`. Trims all string inputs via `.Trim()`. Wraps in try/catch returning `DomainErrors.General.ServerError` on exception. |
| `UpdateAsync` | **Partial update** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â only non-null fields from `UpdateCompanyRequest` are patched onto the existing entity. Re-validates domain format if provided. Sets `UpdatedAtUtc` and `UpdatedByUserId` via repository layer. Wraps in try/catch. |
| `DeleteAsync` | Removes company (delegated to repository which nullifies linked FKs first). Wraps in try/catch. |

> **Key design detail ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Map() only returns 5 fields:** `CompanyService.Map()` (line 227-233) maps `Id`, `Name`, `Domain`, `Industry`, `Size` to `CompanyDto`. It does **NOT** include `CreatedAtUtc`, `UpdatedAtUtc`, or any computed fields like contact count or deal count. This means even though the entity has timestamps, they are never returned to the frontend via the API.

> **Why this matters:** The frontend TypeScript type `Company` has `createdAtUtc` and `updatedAtUtc` fields, and the `CompanyDetail.tsx` tries to display them (lines 269-271), but they will always be `undefined` because the backend never sends them. The "Newest First" sort and "This Week" stat are broken for the same reason.

### LeadService ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Lead Conversion (Company creation/linking)

**File:** `backend/src/ACI.Application/Services/LeadService.cs` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â `ConvertAsync` method

When a lead is converted with `CreateNewCompany: true`:
- Creates a new `Company` entity via `_companyRepository.AddAsync()`
- Uses `NewCompanyName` from request, falling back to `lead.Company?.Name` or `lead.Name`
- Sets `UserId` and `OrganizationId` from the current user
- Links the newly created company to the lead via `ConvertedToCompanyId`
- Also sets `CompanyId` on any newly created Contact and Deal during conversion

When `ExistingCompanyId` is provided:
- Validates the existing company belongs to the user/org via `_companyRepository.GetByIdAsync()`
- If not found, returns `DomainErrors.Company.NotFound`
- Uses it as the `companyId` for the conversion (shared with new contact/deal)

When neither is specified:
- Uses `lead.CompanyId` (the lead's existing company association, if any)

**Cross-entity impact during conversion:** The `companyId` from conversion is propagated to:
1. Existing contact (if email matches): updates `existingContactByEmail.CompanyId = companyId`
2. New contact: sets `CompanyId = companyId`
3. New deal: sets `CompanyId = companyId`

### GlobalSearchService

**File:** `backend/src/ACI.Application/Services/GlobalSearchService.cs`

- Calls `_companyService.SearchAsync(query)` in parallel with leads, contacts, and deals using `Task.WhenAll`
- Results are limited to `MaxPerType` (10 per entity type) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â however, this limit is applied per entity type
- Companies appear as `IReadOnlyList<CompanyDto>` in `GlobalSearchResultDto`
- **Note:** The `SearchAsync` delegate in the repository already has its own `.Take(20)` limit, so the effective max is `min(20, MaxPerType)` = 10

### ContactService ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Company FK

**File:** `backend/src/ACI.Application/Services/ContactService.cs`

- `CreateAsync`: Sets `CompanyId = request.CompanyId` on the new Contact entity
- `UpdateAsync`: `if (request.CompanyId.HasValue) existing.CompanyId = request.CompanyId` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â only updates if provided
- `Map()`: Includes `e.CompanyId` in the `ContactDto`

### DealService ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Company FK

**File:** `backend/src/ACI.Application/Services/DealService.cs`

- `CreateAsync`: Sets `CompanyId = request.CompanyId` on the new Deal entity
- `UpdateAsync`: `if (request.CompanyId.HasValue) existing.CompanyId = request.CompanyId` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â only updates if provided
- `Map()`: Includes `e.CompanyId` and `e.Company?.Name` in the `DealDto`

### SettingsService ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â CompanyName (UserSettings)

**File:** `backend/src/ACI.Application/Services/SettingsService.cs`

- `UpdateSettingsAsync`: `if (request.CompanyName != null) settings.CompanyName = request.CompanyName;`
- Default: `CompanyName = "My Company"`
- Maps `s.CompanyName` to `UserSettingsDto.CompanyName`
- This is the user's own business name for branding, NOT a CRM Company entity

---

## 4. Backend Data Access (Repositories)

### CompanyRepository (`backend/src/ACI.Infrastructure/Repositories/CompanyRepository.cs`) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **110 lines**

| Method | Description | Line |
|--------|-------------|------|
| `FilterByUserAndOrg` | Static helper: filters by `UserId` + `OrganizationId` dual scoping | 14-15 |
| `ApplySearch` | Static helper: case-insensitive search on `Name`, `Domain`, or `Industry` using `.ToLowerInvariant()` / `.ToLower().Contains()` | 17-24 |
| `GetPagedAsync` | Paginated query with search, filtered by user/org, ordered by `Name`, with `Skip`/`Take` | 26-45 |
| `CountAsync` | Count companies matching user/org and optional search | 47-52 |
| `GetByUserIdAsync` | All companies for a user/org, ordered by `Name` (no limit) | 54-57 |
| `SearchAsync` | Search by `Name`, `Domain`, or `Industry` (case-insensitive, `ToLowerInvariant`). **Hardcoded `.Take(20)` limit.** If query is blank, falls back to `GetByUserIdAsync` (no limit). | 59-69 |
| `GetByIdAsync` | Single company by ID, validates user/org ownership | 71-73 |
| `AddAsync` | Insert new company, calls `SaveChangesAsync` | 75-80 |
| `UpdateAsync` | Re-fetches entity to verify ownership, updates `Name`, `Domain`, `Industry`, `Size`, sets `UpdatedAtUtc = DateTime.UtcNow` and `UpdatedByUserId = userId` | 82-95 |
| `DeleteAsync` | **Critical cascade logic:** see below | 97-109 |

### Delete Cascade ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â FK Nullification (Lines 97-109)

When a company is deleted, the repository does **NOT** cascade-delete related Contacts, Deals, or Leads. Instead, it performs three `ExecuteUpdateAsync` calls to set their `CompanyId` to `null`:

```csharp
await _db.Contacts.Where(c => c.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.CompanyId, (Guid?)null), ct);
await _db.Deals.Where(d => d.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.CompanyId, (Guid?)null), ct);
await _db.Leads.Where(l => l.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.CompanyId, (Guid?)null), ct);
_db.Companies.Remove(entity);
await _db.SaveChangesAsync(ct);
```

**Why this matters:**
- Contacts remain but lose their company association ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â users will see contacts suddenly "unlinked" from any company.
- Deals remain but lose their company association ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â pipeline reports by company will change.
- Leads remain but lose their company association ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â lead context about the source organization is lost.
- The `ConvertedToCompanyId` FK on Leads is NOT nullified ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â if a lead was converted and the resulting company is deleted, the lead still points to a non-existent company. This is a referential integrity issue.
- **No user warning:** The delete confirmation dialog on the frontend says "This action cannot be undone" but does not mention the unlinking side effects.

### Repository Filtering Logic

The `FilterByUserAndOrg` helper applies dual scoping:
```csharp
c.UserId == userId && (organizationId == null ? c.OrganizationId == null : c.OrganizationId == organizationId)
```
This means:
- If `organizationId` is `null`, it only returns companies where `OrganizationId IS NULL` (legacy user-scoped data)
- If `organizationId` is set, it returns companies for that specific organization
- This prevents data leakage between organizations

---

## 5. Backend DTOs

| DTO | File | Fields |
|-----|------|--------|
| `CompanyDto` | `DTOs/CompanyDto.cs` | `record CompanyDto(Guid Id, string Name, string? Domain, string? Industry, string? Size)` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **5 fields only** |
| `CreateCompanyRequest` | `DTOs/CreateCompanyRequest.cs` | `Name` (required, 1-200 chars), `Domain` (optional, 253 chars, regex validated), `Industry` (optional, 100 chars), `Size` (optional, 50 chars) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **4 fields only** |
| `UpdateCompanyRequest` | `DTOs/UpdateCompanyRequest.cs` | All fields optional (partial update), same validations as Create ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **4 fields only** |
| `ConvertLeadRequest` | `DTOs/ConvertLeadRequest.cs` | `CreateNewCompany` (bool), `NewCompanyName` (optional, 200 chars), `ExistingCompanyId` (Guid?) |
| `ConvertLeadResult` | `DTOs/ConvertLeadResult.cs` | `record ConvertLeadResult(Guid? CompanyId, Guid? ContactId, Guid? DealId)` |
| `GlobalSearchResultDto` | `DTOs/GlobalSearchResultDto.cs` | Contains `IReadOnlyList<CompanyDto> Companies` |

### CreateCompanyRequest ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Validation Details

```csharp
[Required] [StringLength(200, MinimumLength = 1)] Name       // Required
[StringLength(253)] [RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z]{2,})+$")] Domain  // Regex: valid domain format
[StringLength(100)] Industry   // Free text, no enum validation
[StringLength(50)] Size        // Free text, no enum validation
```

> **CRITICAL BUG ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Frontend sends fields the backend ignores:** The frontend `createCompany()` and `updateCompany()` API functions send `description`, `website`, and `location` in the JSON body. Since `CreateCompanyRequest` and `UpdateCompanyRequest` do not have these properties, C# model binding silently ignores them. The data the user types into the Description, Website, and Location form fields is sent to the backend, received, and **thrown away without error**. The user has no way of knowing their data was lost.

> **Note on Domain Regex:** The regex validates DNS-style domain format (e.g., `acme.com`, `my-company.co.uk`). It does NOT accept URLs like `https://acme.com` or `www.acme.com` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â only bare domain names. This is also validated server-side in `CompanyService` via `ValidationHelper.IsValidDomain()`.

---

## 6. Backend Validation & Error Handling

**File:** `backend/src/ACI.Application/Common/DomainErrors.cs`

Company-specific domain errors:

| Error | Code | Message | Used? |
|-------|------|---------|-------|
| `Company.NotFound` | `"Company.NotFound"` | "The company was not found" | **Yes** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â in `CompanyService.GetByIdAsync`, `UpdateAsync`, `DeleteAsync`, and `LeadService.ConvertAsync` |
| `Company.NameRequired` | `"Company.NameRequired"` | "Company name is required" | **Yes** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â in `CompanyService.CreateAsync` |
| `Company.DuplicateName` | `"Company.DuplicateName"` | "A company with this name already exists" | **NO ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â never used**. There is no duplicate-name check in create or update logic. |
| `Company.DomainInvalid` | `"Company.DomainInvalid"` | "The domain format is invalid" | **Yes** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â in `CompanyService.CreateAsync` and `UpdateAsync` |

> **Why `DuplicateName` matters:** Without duplicate detection, users can create multiple companies with identical names (e.g., "Acme Corp" twice). This leads to contacts and deals being split across duplicate records, fragmenting the data. Finding and merging these duplicates is manual and error-prone.

---

## 7. Backend Validation Helper ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Domain Regex

**File:** `backend/src/ACI.Application/Common/ValidationHelper.cs` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **93 lines**

The `IsValidDomain()` method (lines 57-66) uses a source-generated regex:
```csharp
[GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z]{2,})+$", RegexOptions.Compiled)]
private static partial Regex DomainRegex();
```

This validates:
- Starts with alphanumeric
- Allows hyphens in middle (up to 61 chars per label)
- Must have at least one dot with a 2+ char TLD
- Examples that pass: `acme.com`, `my-company.co.uk`
- Examples that fail: `https://acme.com`, `www.acme.com` (no subdomain support), `acme` (no TLD)

The same class also provides `IsValidEmail()` and `IsValidPhone()` used by other entities.

---

## 8. Database Configuration (EF Core)

### CompanyConfiguration (`backend/src/ACI.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs`) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **22 lines**

- Table: `"Companies"`
- `Name`: `MaxLength(256)`, required
- `Domain`: `MaxLength(256)` (not required)
- `Industry`: `MaxLength(128)` (not required)
- `Size`: `MaxLength(64)` (not required)
- FK: `UserId` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ `User`, via `User.Companies` collection (one-to-many)
- FK: `OrganizationId` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ `Organization` (optional, `IsRequired(false)`)
- FK: `UpdatedByUserId` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ `User` (optional, `IsRequired(false)`)
- Index: `OrganizationId` (explicit index for query performance)

> **Note on MaxLength discrepancy:** EF config has `Name.MaxLength(256)` but `CreateCompanyRequest` has `[StringLength(200)]`. This means EF allows 256 chars in the database but the DTO validation rejects anything over 200. Similarly, `Domain` is 256 in EF vs 253 in DTO. `Industry` is 128 in EF vs 100 in DTO. `Size` is 64 in EF vs 50 in DTO. The DTO constraints are more restrictive ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â so the EF limits are effectively unused. This is fine but could cause confusion if someone bypasses DTO validation.

### Related entity configurations referencing Company

| Configuration File | Company Relationship | OnDelete Behavior |
|-------------------|----------------------|-------------------|
| `ContactConfiguration.cs` (line 22) | `Contact.CompanyId` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ `Company` (one-to-many, via `Company.Contacts`) | Default (ClientSetNull) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â but repository manually nullifies before delete |
| `LeadConfiguration.cs` (line 24) | `Lead.CompanyId` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ `Company` (one-to-many, via `Company.Leads`) | Default (ClientSetNull) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â but repository manually nullifies before delete |
| `DealConfiguration.cs` (line 23) | `Deal.CompanyId` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ `Company` (one-to-many, via `Company.Deals`) | Default (ClientSetNull) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â but repository manually nullifies before delete |

### AppDbContext

- `DbSet<Company> Companies` registered in `AppDbContext.cs`

---

## 9. Cross-Entity Company Interactions (Backend)

| Entity | Relationship | Direction | Details |
|--------|-------------|-----------|---------|
| **User** | `ICollection<Company> Companies` | User owns many Companies | Each company belongs to one user |
| **Organization** | `Company.OrganizationId` (optional FK) | Company ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Organization | Multi-tenancy scoping |
| **Contact** | `Contact.CompanyId` (optional FK) | Contact belongs to Company | `Company.Contacts` navigation collection. FK manually nullified in `CompanyRepository.DeleteAsync`. `ContactService` sets/updates `CompanyId` on create/update. |
| **Deal** | `Deal.CompanyId` (optional FK) | Deal linked to Company | `Company.Deals` navigation collection. FK manually nullified in `CompanyRepository.DeleteAsync`. `DealService` sets/updates `CompanyId` on create/update. `DealDto` includes `Company?.Name`. |
| **Lead** | `Lead.CompanyId` (optional FK) | Lead linked to Company | `Company.Leads` navigation collection. FK manually nullified in `CompanyRepository.DeleteAsync`. |
| **Lead (Conversion)** | `Lead.ConvertedToCompanyId` (optional FK) | Lead converted ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Company | Tracks which company was created during lead conversion. **NOT nullified on company delete** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â potential dangling FK. |
| **Activity** | No relationship | None | Activities have no `CompanyId` field ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â they link to `Contact`, `Lead`, and `Deal` only |

---

## 10. Backend Unit Tests ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â CompanyServiceTests

**File:** `backend/tests/ACI.Application.Tests/Services/CompanyServiceTests.cs` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **529 lines**

Comprehensive test coverage using Moq and FluentAssertions:

| Test Region | Test Count | Coverage |
|-------------|-----------|----------|
| **GetCompaniesAsync** | 2 | Returns companies when they exist; returns empty list when none exist |
| **GetByIdAsync** | 2 | Returns company when found; returns `Company.NotFound` when not found |
| **CreateAsync** | 4 | Valid request returns company; empty name returns `Company.NameRequired`; invalid domain returns `Company.DomainInvalid`; null domain is accepted; trims whitespace from inputs |
| **UpdateAsync** | 4 | Valid request returns updated company; not found returns `Company.NotFound`; invalid domain returns `Company.DomainInvalid`; partial update ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â only provided fields are patched (null fields preserve original values) |
| **DeleteAsync** | 2 | Successful delete returns success; not found returns `Company.NotFound` |
| **SearchAsync** | 2 | Returns matching companies; returns empty list when no matches |
| **GetCompaniesPagedAsync** | 1 | Returns `PagedResult` with correct pagination (items, totalCount, page, pageSize, totalPages) |

> **What's NOT tested:**
> - `DuplicateName` error (because it's never used in the service)
> - Exception handling paths (the `catch (Exception ex)` blocks)
> - Domain validation edge cases at the regex level (tested indirectly via `Company.DomainInvalid`)
> - The repository's delete cascade behavior (FK nullification)
> - `CountAsync` repository method

---

## 11. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â API Client Layer

### `src/app/api/companies.ts` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **149 lines**

| Function | API Call | Description |
|----------|----------|-------------|
| `getCompaniesPaged(params)` | `GET /api/companies?page=&pageSize=&search=` | Paginated companies with search. Maps `CompanyRaw` (10 fields including `description`, `website`, `location`, `createdAtUtc`, `updatedAtUtc` with nullable-to-undefined mapping) to `Company`. Defaults: page=1, pageSize=20. |
| `getCompanies()` | `GET /api/companies/all` | Get all companies (non-paginated). Falls back to `mockCompanies` in demo mode. |
| `createCompany(params)` | `POST /api/companies` | Create a company. Sends `{name, domain?, industry?, size?, description?, website?, location?}` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **7 fields**. Backend only processes 4 (`name, domain, industry, size`). |
| `updateCompany(id, params)` | `PUT /api/companies/{id}` | Update a company. Sends partial fields (same 7). Backend only processes 4. |
| `deleteCompany(id)` | `DELETE /api/companies/{id}` | Delete a company. Returns `true` on 204 status. |

**CompanyRaw type (lines 9-20):**
```typescript
type CompanyRaw = {
  id: string;
  name: string;
  domain?: string | null;
  industry?: string | null;
  size?: string | null;
  description?: string | null;
  website?: string | null;
  location?: string | null;
  createdAtUtc?: string | null;
  updatedAtUtc?: string | null;
};
```

> **BUG ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Data loss on create/update:** The `createCompany` and `updateCompany` functions send `description`, `website`, and `location` to the backend. The backend `CreateCompanyRequest` and `UpdateCompanyRequest` DTOs don't have these properties, so C# model binding silently drops them. **The user types data that gets sent over the network and then discarded.** No error is returned ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â the API succeeds but the extra fields vanish. On the next page load, `description`, `website`, and `location` will be `undefined`.

**Mock data fallback:** When `isUsingRealApi()` returns false (no backend), read operations return `mockCompanies` from `mockData.ts`. Create, update, and delete operations return `null`/`false` in mock mode (no local persistence for mock companies).

### `src/app/api/search.ts` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Global search

| Function | API Call | Description |
|----------|----------|-------------|
| `globalSearch(query)` | `GET /api/search?q=` | Returns `{companies: {id, name}[]}` alongside leads, contacts, and deals |

> **DEAD CODE:** The `globalSearch` function is defined in `search.ts` but is **never imported or used anywhere** in the frontend codebase. No component calls it. The `AppHeader` has no search functionality ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â it only contains navigation links. The existing report incorrectly stated this was "used by the AppHeader search functionality."

### `src/app/api/index.ts` (barrel exports)

Re-exports: `getCompaniesPaged`, `getCompanies`, `createCompany`, `updateCompany`, `deleteCompany`.

---

## 12. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â TypeScript Types

### `src/app/api/types.ts` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Company interface (lines 64-75)

```typescript
export interface Company {
  id: string;
  name: string;
  domain?: string;
  industry?: string;
  size?: string;
  description?: string;
  website?: string;
  location?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}
```

> **The frontend `Company` type has 11 fields** (including `id`). However, the backend `CompanyDto` only returns 5 fields (`Id`, `Name`, `Domain`, `Industry`, `Size`). This means `description`, `website`, `location`, `createdAtUtc`, and `updatedAtUtc` will always be `undefined` when data comes from the real API. The `CompanyDetail.tsx` page tries to display these fields, but they will never have values.

### Related types with Company references

| Type | File | Company Field |
|------|------|---------------|
| `Contact` | `types.ts` | `companyId?: string`, `companyName?: string` |
| `Deal` | `types.ts` | `companyId?: string` |
| `Lead` | `types.ts` | `companyId?: string` |
| `GlobalSearchResult` | `search.ts` | `companies: {id: string; name: string}[]` |

---

## 13. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â React Query Hooks

### `src/app/hooks/queries/useCompanies.ts` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **101 lines**

| Hook | Query Key | Description |
|------|-----------|-------------|
| `useCompanies()` | `['companies', 'list']` | Fetch all companies via `getCompanies()` (non-paginated `/api/companies/all`) |
| `useCompanyById(id)` | `['companies', 'detail', id]` | Find company from cached list (client-side lookup, NOT a dedicated API call) |
| `useCreateCompany()` | mutation | Create company, optimistically appends to cache, toast on success/error |
| `useUpdateCompany()` | mutation | Update company, optimistically updates cache, toast on success/error |
| `useDeleteCompany()` | mutation | Delete company, optimistically removes from cache, toast on success/error |

> **Design note ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â `useCompanyById` is a client-side lookup:** It does NOT call `GET /api/companies/{id}`. It first fetches the full companies list via `useCompanies()`, then finds the matching company client-side using `companies?.find((c) => c.id === id)`. This means it depends on the full list being loaded first.

> **Note ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â These hooks are NOT used by `Companies.tsx`:** The main Companies page manages its own state with `useState` + `useCallback` + `fetchCompanies()`. The React Query hooks exist for use by other pages (like `Contacts.tsx`, `Pipeline.tsx`) that need company dropdown data.

### Cache Invalidation Strategy

- `useCreateCompany` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ **Optimistic update**: appends new company to `queryKeys.companies.lists()` cache
- `useUpdateCompany` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ **Optimistic update**: replaces matching company in `queryKeys.companies.lists()` cache
- `useDeleteCompany` ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ **Optimistic update**: filters out deleted company from `queryKeys.companies.lists()` cache
- All mutations show toast notifications on success ("Company created/updated/deleted successfully") and error ("Failed to create/update/delete company")

---

## 14. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Companies Page (Main Company UI)

**File:** `src/app/pages/Companies.tsx` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **1196 lines** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â THE central company management page

### Views
- **Card Grid View**: Responsive grid (1/2/3 columns) of company cards with logos, details, and stats

### Data Loaded on Mount (lines 130-148)
```typescript
const [pagedResult, contactsData, dealsData] = await Promise.all([
  getCompaniesPaged({ page, pageSize, search }),
  getContacts(),      // All contacts ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â to compute per-company contact count
  getDeals(),         // All deals ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â to compute per-company deal count & value
]);
```

> **Performance concern:** Every page load fetches ALL contacts and ALL deals (non-paginated) to compute per-company stats client-side. This will scale poorly with large datasets. With 10,000 contacts and 5,000 deals, every company page load transfers all that data just to count how many belong to each company.

> **Why this matters:** This is not just a performance issue ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â it's an architectural anti-pattern. The backend should provide aggregated counts (e.g., `GET /api/companies/{id}/stats` or include counts in `CompanyDto`). Currently, the frontend downloads the entire CRM dataset just to show a number on each company card.

### State Management
- URL-driven pagination via `useSearchParams` (page, pageSize, search in URL)
- Debounced search input (300ms debounce) that updates URL
- Client-side filters: `filterIndustry`, `filterSize`
- Client-side sorting: `sortField` (name, createdAt, contacts, deals), `sortDirection` (asc, desc)
- Form state includes dead fields: `form = { name, domain, industry, size, description, website, location }`

### Statistics Bar (lines 436-512)
When companies exist, shows 6 stat cards:

| Stat | Source | Theme | Notes |
|------|--------|-------|-------|
| **Total Companies** | `companies.length` | Slate | Only counts current page items, not total |
| **With Contacts** | Companies where at least one contact has matching `companyId` | Blue | Computed from ALL contacts loaded client-side |
| **With Deals** | Companies where at least one deal has matching `companyId` | Emerald | Computed from ALL deals loaded client-side |
| **Total Pipeline** | Sum of all `deal.value` where `deal.companyId` is not null | Amber gradient | Parses string values, strips non-numeric chars |
| **This Week** | Hardcoded `0` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â `createdAtUtc` not available from API | Purple | **Always shows 0 ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â broken stat** |
| **Top Industry** | Most frequent industry from company list (clickable ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ filters by that industry) | Cyan | |

> **BUG ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â "Total Companies" stat is misleading:** It shows `companies.length` which is the number of companies on the current page (max `pageSize`), NOT the total count across all pages. If you have 100 companies and page size is 20, this stat says "20". The `totalCount` from pagination is available but not used for this stat card.

### Quick Insights Banner (lines 516-530)
Shows an "Opportunity to Expand" alert when >50% of companies don't have contacts.

### Filters & Sorting (lines 532-757)
- **Search**: By name, domain, or industry (dual execution: server-side via `search` param to `getCompaniesPaged` + additional client-side filter on paginated results)
- **Industry Filter**: Dropdown with 10 preset industries (Technology, Healthcare, Finance, Manufacturing, Retail, Education, Real Estate, Consulting, Marketing, Other)
- **Size Filter**: Dropdown with 6 size options (1-10, 11-50, 51-200, 201-500, 501-1000, 1000+)
- **Sort Options**: Name A-Z, Name Z-A, Most Contacts, Most Deals, Newest First
- Active filter pills shown below the filter bar

> **BUG ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â "Newest First" sort is broken (lines 249-251):** The `createdAt` sort case falls back to sorting by name because `createdAtUtc` is NOT included in the API response:
> ```typescript
> case 'createdAt':
>   // Note: createdAtUtc is not available on Company type, so sorting by name instead
>   comparison = a.name.localeCompare(b.name);
> ```

> **BUG ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Search dual execution:** Server-side search happens via `getCompaniesPaged({search})`, then an additional client-side search filter runs on the paginated results (lines 222-230). This means client-side search only filters within the already-server-filtered page. If you type fast enough before debounce, the client-side filter may show different results than what the server would return.

### Company Card (lines 832-958)
Each card displays:
- **Company initials** (first letters of name words) in a gradient avatar, or Building2 icon
- **Company name** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **clickable**, navigates to `/companies/${company.id}` (line 862)
- **Industry badge** if set
- **Domain link** with Globe icon (opens in new tab as `https://{domain}`)
- **Size** with Users icon (shows "{size} employees")
- **Contact count** with UserCircle icon (click does not navigate ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â display only)
- **Deal count** with Target icon (click does not navigate ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â display only)
- **Deal value** with DollarSign icon (only shown when > 0)
- **Dropdown menu**: Edit Company, Visit Website (if domain set), Delete

### CRUD Operations

**Create Company Dialog (lines 974-1173):**
- Gradient violet header with Building2/Pencil icon
- Form fields (7 total, but only 4 are saved):
  - **Name** (required, `Building2` icon) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **saved to API**
  - **Domain** (`Globe` icon, placeholder "e.g. acme.com") ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **saved to API**
  - **Website** (`Link2` icon, placeholder "https://...") ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **sent to API but silently ignored by backend**
  - **Industry** (`Factory` icon, dropdown with 10 presets) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **saved to API**
  - **Company Size** (`Users` icon, dropdown with 6 options) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **saved to API**
  - **Location** (`MapPin` icon, placeholder "e.g. San Francisco, CA") ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **sent to API but silently ignored by backend**
  - **Description** (`Briefcase` icon, textarea) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **sent to API but silently ignored by backend**
- Submit: calls `createCompany({name, domain, industry, size, description, website, location})` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â 7 fields sent, only 4 processed

**Edit Company Dialog:** Same form, pre-populated. Lines 292-294: `description`, `website`, `location` are initialized to empty strings since they don't exist in the API response.

**Delete Company Dialog (lines 1175-1190):**
- AlertDialog with "Delete company?" title
- Description: 'This will remove "{company.name}". This action cannot be undone.'
- **No warning** about linked contacts/deals/leads being unlinked

### Pagination (lines 962-970)
- `DataPagination` component
- URL-driven page/pageSize changes

---

## 15. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Company Detail Page

**File:** `src/app/pages/CompanyDetail.tsx` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **512 lines**

A dedicated detail view for a single company, accessible at `/companies/:id`.

### Data Loading (lines 58-110)
1. **Company**: Calls `authFetchJson<CompanyRaw>('/api/companies/${id}')` directly. If that fails, falls back to `getCompaniesPaged({ page: 1, pageSize: 1000 })` and finds the company client-side.
2. **Contacts**: Calls `getContactsPaged({ page: 1, pageSize: 100 })`, then filters client-side for `c.companyId === id`.
3. **Deals**: Calls `getDealsPaged({ page: 1, pageSize: 100 })`, then filters client-side for `d.companyId === id`.

> **BUG ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Client-side filtering with hard-coded page size:** If a company has more than 100 contacts or 100 deals, some linked entities won't appear on the detail page because `pageSize: 100` is hardcoded. The backend doesn't have a `GET /api/contacts?companyId={id}` endpoint, so the frontend has to load a page of all contacts and filter.

> **BUG ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Displays fields the backend never sends:** Lines 238-271 attempt to display `company.location`, `company.website`, `company.description`, `company.createdAtUtc`, and `company.updatedAtUtc`. Since the backend `CompanyDto` only returns `Id, Name, Domain, Industry, Size`, these fields will always be `undefined` and the corresponding UI sections will never render. Users see a company detail page without timestamps, location, website, or description ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â even though those fields exist in the form.

### Sections Displayed
1. **Company Header** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Name, Domain badge, Industry badge, Size badge, Location badge (if present), Website link (if present), Description (if present), Created/Updated timestamps (if present)
2. **Overview Card** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Grid showing Name, Domain, Industry, Size, Location, Website
3. **Contacts Card** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â List of linked contacts with name, email, job title. Each contact links to `/contacts/${contact.id}`
4. **Deals Card** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â List of linked deals with name, stage badge, and value. Each deal links to `/deals/${deal.id}`

### Edit/Delete
- **Edit Dialog** (lines 418-497) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Form with 7 fields (name, domain, industry, size, location, website, description). Calls `updateCompany()` sending all 7 ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â but backend only saves 4.
- **Delete Dialog** (lines 499-509) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Simple confirmation. On delete, navigates to `/companies`.

> **Why the Detail Page matters:** This page is the primary way to see a company's full context ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â its linked contacts, linked deals, and descriptive information. However, because the backend doesn't include `createdAtUtc`, `updatedAtUtc`, `description`, `website`, or `location` in the DTO, the page appears sparse. Users cannot see when the company was created or last updated, and any description/website/location they entered previously has been lost.

---

## 16. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Contacts Page (Company Linking)

**File:** `src/app/pages/Contacts.tsx` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **1162+ lines**

### Company Interactions

1. **Loads companies on mount**: `getCompanies()` via `Promise.all` to populate company dropdown
2. **Company dropdown in Create/Edit Contact**: Contact form has a `companyId` field with a `Select` dropdown listing all companies by name
3. **Company name display on contact cards**: Contact cards show the associated company name (looked up from the loaded companies list via `companyName()` helper function, line 334)
4. **Company name is clickable** (line 885): Clicking the company name on a contact card navigates to `/companies/${contact.companyId}`
5. **Company filter**: Dropdown filter with options "All companies", "No company", and each loaded company by name (line 636-652)
6. **"With Company" stat**: Shows count of contacts that have a `companyId` (line 144)

> **Cross-entity navigation works:** Users can click a company name on a contact card and be taken to the Company Detail page. This is a functional drill-down path.

---

## 17. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Contact Detail Page (Company Link)

**File:** `src/app/pages/ContactDetail.tsx`

### Company Interactions (line 276-278)

- Displays the contact's company name as a clickable link
- Links to `/companies/${contact.companyId}` if `companyId` exists, otherwise links to `/companies`
- Shows a `Building2` icon next to the company label

---

## 18. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Pipeline Page (Company in Deals)

**File:** `src/app/pages/Pipeline.tsx`

### Company Interactions

1. **Loads companies on mount**: `getCompanies()` in parallel with other data (line 97: `companies` state)
2. **Company dropdown in Create Deal dialog** (line 1489-1492): "Company" `Select` dropdown to associate a deal with a company
3. **Company dropdown in Edit Deal dialog** (line 1715-1718): Can change or set company association
4. **Company name display in Deal Detail Sheet** (lines 1168-1172): Shows `detailCompany.name` when deal has a `companyId` (resolved via line 467: `const detailCompany = detailDeal?.companyId ? companies.find(...)`)
5. **Company name on DealCard**: Not directly shown ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â company info is in the detail sheet

---

## 19. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Leads Page (Company in Conversion)

**File:** `src/app/pages/Leads.tsx` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **1826+ lines**

### Company Interactions

1. **Leads have `companyId`**: Lead entity can be linked to a company
2. **Company name display**: Lead cards show company name (line 1171: `companyName(lead.companyId)`)
3. **Lead Conversion ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Company Section** (lines 1544+):
   - **"Create new company" toggle**: Purple-themed toggle button with Building2 icon (line 1556-1572)
   - **Company name input**: Defaults to `companies.find((c) => c.id === lead.companyId)?.name` or empty (line 319)
   - **"Attach to existing company" option**: (Not explicitly shown in search results ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â the toggle controls create-new vs skip)
   - **Conversion flow visualization**: Visual flow shows Lead ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Contact + Company + Deal, dynamically showing/hiding based on toggle state (line 1412-1419)
4. **Company assignment via `ConvertLeadRequest`**: Sends `createNewCompany`, `newCompanyName`, or `existingCompanyId` to the backend

---

## 20. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Dashboard (Company in Sales Writer)

**File:** `src/app/pages/Dashboard.tsx`

### Company Interactions (Source-Verified)

1. **`selectedRecipient.company`** (line 77): When a lead/contact is selected as a recipient for AI copy generation, their `company` name is stored as part of the recipient context
2. **`companyName` in copy generation** (lines 160, 170): The user's `settings.companyName` (from UserSettings) is passed to `generateCopy()` and `generateCopyWithRecipient()` as a parameter ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â this is the user's OWN company name used in email signatures and templates, NOT a CRM Company entity
3. **No direct CRM Company entity usage**: The Dashboard does NOT load `getCompanies()` and does not display CRM company data

> **Important distinction:** The Dashboard uses `companyName` from `UserSettings` (the user's own business name for branding), NOT from the CRM `Company` entity. The `selectedRecipient.company` field is a free text string from the lead/contact, not a foreign key to a Company record.

---

## 21. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Dashboard Config (Company Shortcut)

**File:** `src/app/pages/dashboard/config.ts` (line 48)

The dashboard quick-access grid includes a "Companies" shortcut card:
```typescript
{ to: '/companies', icon: Building2, label: 'Companies', description: 'Organizations', gradient: 'from-cyan-500 to-blue-600', glow: 'shadow-cyan-500/40', bgHover: 'hover:bg-cyan-50', shortcut: 'O' }
```

This provides a one-click navigation to the Companies page from the dashboard, with keyboard shortcut "O".

---

## 22. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â SendToCrm Page

**File:** `src/app/pages/SendToCrm.tsx` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **290 lines**

### Company Interactions

- **Company is NOT an object type in SendToCrm.** The `ObjectType` union is `'contact' | 'deal' | 'lead' | 'workflow' | 'email'`.
- There is no way to save generated copy to a company record.

> **Why this matters:** Users can generate AI copy about a company (using company name in templates) but cannot save that copy to the company record. If they write a proposal or follow-up email targeting a specific company, there's no way to associate that content with the company for future reference.

---

## 23. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Global Search (Companies)

**File:** `src/app/api/search.ts` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â **20 lines**

```typescript
export interface GlobalSearchResult {
  leads: { id: string; name: string; email: string; status?: string }[];
  contacts: { id: string; name?: string; email?: string }[];
  companies: { id: string; name: string }[];
  deals: { id: string; name: string; value?: string }[];
}
```

- `globalSearch(query)` returns `{companies: {id: string; name: string}[]}` alongside leads, contacts, and deals
- **DEAD CODE:** This function is defined but **never imported or called** by any frontend component. The `AppHeader` component does not have a search bar or search functionality ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â it only contains navigation links and user menu.

> **Why this matters:** The backend `SearchController` and `GlobalSearchService` are fully functional, but the frontend has no UI to trigger global search. This means users cannot search across all entity types from a single search bar ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â they must navigate to each page (Companies, Contacts, Deals, Leads) and use the page-specific search. Implementing a global search UI in the header would significantly improve discoverability.

---

## 24. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Copy Generator (Company Name in Templates)

**File:** `src/app/api/copyGenerator.ts`

### Company Interactions (Source-Verified)

1. **`RecipientContext.company`** (line 20): The recipient context type includes `company?: string` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â a free text field for the contact/lead's company name
2. **`[Company Name]` template variable**: All copy templates use `[Company Name]` as a placeholder that gets replaced with:
   - `params.companyName` (from UserSettings ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â the user's own company) when no recipient is set (line 210-211)
   - `params.recipient.company` (from the selected lead/contact's company) when a recipient is selected (line 253-254)
3. **Email subject generation** (lines 261-264): Company name is used in auto-generated email subjects:
   - `"Meeting request: {company} partnership"` (default tone)
   - `"Following up: {company}"` (alternative)
4. **Template examples that reference companies:**
   - `'sales-email|Schedule a meeting'`: "I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name]"
   - `'deal-message|Close the deal'`: "I wanted to provide you with a quick update on our proposal for [Company Name]"
   - `'linkedin-connect|Schedule a meeting'`: "I noticed your work at [Company Name]"
   - `'cold-outreach|General introduction'`: "I've been following [Company Name]'s growth and believe we could add significant value"
   - `'call-script|Gatekeeper'`: "I'm reaching out because we've helped companies like [Company Name] [key benefit]"

---

## 25. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Settings (Company Name in Profile)

**File:** `src/app/pages/Settings.tsx` and `src/app/pages/settings/components/ProfileSection.tsx`

### Company Interactions (Source-Verified)

1. **`settings.companyName`** (Settings.tsx): Default value is `'My Company'` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â this is the user's OWN company name, not a CRM Company entity
2. **ProfileSection** (ProfileSection.tsx lines 30, 43-50): Displays and edits `companyName`:
   - Avatar shows first character of `settings.companyName` (line 30)
   - Input field labeled "Company Name" with id `companyName` allows editing (lines 43-50)
   - Changes are saved to `UserSettings` via `updateSettings()` callback

**File:** `src/app/api/settings.ts` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â default: `companyName: 'My Company'`

> **Important:** This `companyName` is in `UserSettings` and represents the user's own business name for branding/copy generation. It is NOT linked to the CRM `Company` entity. This is a common source of confusion ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â "Company Name" in Settings means "your business name for email signatures," while "Companies" in the nav means "CRM accounts you're tracking."

---

## 26. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Homepage (Company Feature Showcase)

**File:** `src/app/pages/Homepage.tsx`

### Company Mentions (Source-Verified)

1. **Feature showcase** (lines 1178-1190): "Company Accounts" card ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â "Link contacts and deals to organizations"
2. **How-It-Works section** (lines 1883-1884): Step 2 "Set Brand Voice" ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â "Configure your company name and choose your tone"
3. **How-It-Works section** (lines 1883-1884): Step 3 "Add Leads" ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â "Import your leads, create contacts & companies. Log calls, meetings, and notes."
4. **Footer** (line 2497): "Company" section in footer links (About, Blog, Careers, Contact) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â this is the marketing "Company" section, not CRM-related
5. **FAQ**: References "Full pipeline: Leads, Deals (Kanban), Contacts, Companies, Tasks, and Activities"

---

## 27. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Lead Webhook (CompanyName Field)

**File:** `src/app/pages/LeadWebhook.tsx`

### Company Interactions (Source-Verified)

- The webhook payload examples show `companyName: 'TechCorp'` as a field in the lead creation webhook (lines 33, 46, 64, 76)
- This is documentation/example code showing webhook integration syntax (cURL, JavaScript, Python, PHP)
- The `companyName` field in the webhook maps to the lead's company association, not directly to the Company entity
- The field reference section (line 451-454) documents `companyName` as type `string` with description "Company/organization"

---

## 28. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Messages (Company Toast Messages)

**File:** `src/app/api/messages.ts`

```typescript
success: {
  companyCreated: 'Company created.',
  companyUpdated: 'Company updated.',
  companyDeleted: 'Company deleted.',
}
```

These are used in `Companies.tsx` for toast notifications on CRUD operations.

---

## 29. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Navigation & Routing

### AppHeader (`src/app/components/AppHeader.tsx`)
- Navigation item (line 50): `{path: '/companies', label: 'Companies', icon: Building2}`
- Shows in main nav bar between Activities and Team
- **No search functionality** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â AppHeader only contains navigation links

### Navigation Config (`src/app/config/navigation.ts`)
- Navigation item (line 37): `{ path: '/companies', label: 'Companies', icon: Building2 }`
- Position: 7th in the nav items list (after Activities, before Team)
- Uses responsive layout: items that don't fit go into a "More" overflow menu

### App Router (`src/app/App.tsx`)
- Lazy import (line 35): `const Companies = lazy(() => import('@/app/pages/Companies'));`
- Routes:
  - `<Route path="/companies" element={<Companies />} />` (line 117)
  - `<Route path="/companies/:id" element={<CompanyDetail />} />` (line 118)
- Both routes are protected (require authentication)

---

## 30. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Mock Data

**File:** `src/app/api/mockData.ts`

Contains mock companies used in demo mode:
```typescript
export const mockCompanies: Company[] = [
  { id: '1', name: 'Acme Corp' },
  { id: '2', name: 'TechStart Inc' },
  { id: '3', name: 'Global Solutions' },
];
```

Mock contacts reference these company IDs: `companyId: '1'`, `'2'`, `'1'`, `'3'`, `'2'`
Mock deals reference these company IDs: `companyId: '1'`, `'2'`, `'3'`, `'1'`
Mock leads reference these company IDs: `companyId: '1'`, `'2'`, `'3'`, `'1'`, `'2'`

> **Note:** Mock companies have NO `domain`, `industry`, `size`, `description`, `website`, `location`, `createdAtUtc`, or `updatedAtUtc` fields ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â only `id` and `name`. This makes demo mode show very sparse company cards.

---

## 31. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Query Keys

**File:** `src/app/hooks/queries/queryKeys.ts` (lines 18-25)

```typescript
companies: {
  all: ['companies'] as const,
  lists: () => [...queryKeys.companies.all, 'list'] as const,
  list: (filters?: Record<string, unknown>) => [...queryKeys.companies.lists(), filters] as const,
  details: () => [...queryKeys.companies.all, 'detail'] as const,
  detail: (id: string) => [...queryKeys.companies.details(), id] as const,
}
```

---

## 32. Frontend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Test Mock Handlers

**File:** `src/test/mocks/handlers.ts` (lines 18-23)

MSW mock handler for tests:
```typescript
http.get('/api/companies', () => {
  return HttpResponse.json([
    { id: '1', name: 'Test Company', industry: 'Technology' },
  ]);
}),
```

> **Note:** This mock returns an array (matching the `/api/companies/all` format), not a `PagedResult`. Tests hitting the paginated endpoint would need different mocking.

---

## 33. Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â DI Registration & Diagnostic Endpoints

### DI Registration

**File:** `backend/src/ACI.WebApi/Program.cs` (line 141)
```csharp
builder.Services.AddScoped<ICompanyService, CompanyService>();
```

**File:** `backend/src/ACI.Infrastructure/DependencyInjection.cs` (line 34)
```csharp
services.AddScoped<ICompanyRepository, CompanyRepository>();
```

### Diagnostic Endpoint

**File:** `backend/src/ACI.WebApi/Program.cs` (lines 1014-1022)
```csharp
app.MapGet("/db-test-companies-service", async (AppDbContext db, ICompanyService companyService) => {
    var firstUser = await db.Users.FirstOrDefaultAsync();
    var companies = await companyService.GetCompaniesPagedAsync(firstUser.Id, null, 1, 10);
    return Results.Ok(new { status = "ok", totalCount = companies.TotalCount });
});
```

### Schema Migrations

**File:** `backend/src/ACI.WebApi/Program.cs` (lines 508-509)
- Migration ensures `ConvertedToCompanyId` column exists on `Leads` table

---

## 34. Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â UserSettings (CompanyName)

**File:** `backend/src/ACI.Domain/Entities/UserSettings.cs` (line 13)

```csharp
public string CompanyName { get; set; } = string.Empty;
```

- This is the user's OWN company name (for profile/branding), stored in `UserSettings`
- It is a `nvarchar(256)` column in the database (configured in Program.cs line 289)
- Used by `TemplateCopyGenerator` to replace `[Company Name]` in AI-generated copy
- Default value: `"My Company"` (set in `SettingsService`)
- **NOT** a foreign key to the CRM Company entity ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â purely a text field in user settings

---

## 35. Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â TemplateCopyGenerator (Company in AI Copy)

**File:** `backend/src/ACI.Infrastructure/Services/TemplateCopyGenerator.cs`

### Company Interactions (Source-Verified)

1. **`companyName` parameter** (lines 1332, 1447, 1496): Accepted as parameter in `GenerateAsync`, `GenerateWithRecipientAsync`, and `TranslateAsync` methods
2. **`[Company Name]` replacement** (lines 1355-1358): All occurrences of `[Company Name]` in templates are replaced with the user's `companyName` from settings
3. **`recipient.Company` usage** (line 1527): When generating copy with a recipient, uses `recipient.Company` (the lead/contact's company name) for email subject generation
4. **Email subject generation** (lines 1534-1536):
   - `"15 minutes that could change {companyName}'s trajectory"` (persuasive tone)
   - `"Meeting request: {companyName} partnership"` (default tone)

---

## 36. Backend ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â CopyGeneratorService (Company in AI Copy Orchestration)

**File:** `backend/src/ACI.Application/Services/CopyGeneratorService.cs`

- `GenerateAsync` (line 38): Passes `request.CompanyName` to template generator
- `GenerateWithRecipientAsync` (lines 67, 82): Passes `request.Recipient.Company` to `RecipientContext` and `request.CompanyName` to template generator
- `GenerateFromCrmObjectAsync` (line 145): Passes `null` for companyName (CRM object context doesn't include the user's company name)

---

## 37. Complete File Inventory

### Backend Files (25+ files reference Company)

| Category | Files |
|----------|-------|
| **Entity** | `Company.cs` |
| **Service Interface** | `ICompanyService.cs` |
| **Service Impl** | `CompanyService.cs` |
| **Repository Interface** | `ICompanyRepository.cs` |
| **Repository Impl** | `CompanyRepository.cs` |
| **Controller** | `CompaniesController.cs` |
| **DTOs** | `CompanyDto.cs`, `CreateCompanyRequest.cs`, `UpdateCompanyRequest.cs`, `ConvertLeadRequest.cs`, `ConvertLeadResult.cs`, `GlobalSearchResultDto.cs` |
| **DB Config** | `CompanyConfiguration.cs` |
| **Errors** | `DomainErrors.cs` (Company section) |
| **Validation** | `ValidationHelper.cs` (IsValidDomain) |
| **Cross-entity services** | `LeadService.cs` (ConvertAsync), `ContactService.cs`, `DealService.cs`, `GlobalSearchService.cs`, `SettingsService.cs`, `CopyGeneratorService.cs`, `TemplateCopyGenerator.cs` |
| **Cross-entity entities** | `Contact.cs`, `Deal.cs`, `Lead.cs`, `User.cs`, `UserSettings.cs` |
| **Cross-controllers** | `LeadsController.cs` (convert endpoint), `SearchController.cs` |
| **Cross-config** | `ContactConfiguration.cs`, `LeadConfiguration.cs`, `DealConfiguration.cs` |
| **DI/Setup** | `Program.cs` (DI + diagnostics + schema migrations), `DependencyInjection.cs`, `AppDbContext.cs` |
| **Tests** | `CompanyServiceTests.cs` (15 tests, 529 lines) |

### Frontend Files (25+ files reference Company)

| Category | Files |
|----------|-------|
| **API Client** | `companies.ts`, `search.ts` (dead code), `mockData.ts`, `index.ts`, `types.ts`, `copyGenerator.ts`, `settings.ts`, `messages.ts` |
| **React Query** | `useCompanies.ts`, `queryKeys.ts` |
| **Pages** | `Companies.tsx`, `CompanyDetail.tsx` |
| **Cross-entity pages** | `Contacts.tsx`, `ContactDetail.tsx`, `Pipeline.tsx`, `Leads.tsx`, `Dashboard.tsx`, `Settings.tsx`, `Homepage.tsx`, `LeadWebhook.tsx` |
| **Settings components** | `ProfileSection.tsx` |
| **Navigation** | `AppHeader.tsx`, `App.tsx`, `navigation.ts` |
| **Dashboard** | `dashboard/config.ts` |
| **Tests** | `test/mocks/handlers.ts` |

---

## 38. Complete Relationship Map

```
                    ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
                    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡     User     ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ ICollection  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  <Company>   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“
                           ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ owns many
                           ÃƒÂ¢Ã¢â‚¬â€œÃ‚Â¼
ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â   ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â   ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ Organization ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Company    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Contact    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ Multi-tenant ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Name        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  CompanyId   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ scoping      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Domain      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (FK, opt)   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Industry    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“
                   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Size        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ navigates to
                   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ /companies/:id
                   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Contacts[ ] ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡          ÃƒÂ¢Ã¢â‚¬â€œÃ‚Â¼
                   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Deals[ ]    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
                   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Leads[ ]    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ContactDetail ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Company linkÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
                          ÃƒÂ¢Ã¢â‚¬ÂÃ…â€œÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡    Deal      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  CompanyId   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (FK, opt)   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Company.NameÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡           ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
                          ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡    Lead      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  CompanyId   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (FK, opt)   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ Converted    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ ToCompanyId  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (FK, opt)   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
                                      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“

Frontend Page Interactions:
ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Companies   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ CompanyDetailÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Contacts    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ContactDetail ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Page        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Page        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Page        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Page        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Full CRUD, ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Overview,  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Company    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Company    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Stats,     ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Contacts,  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   dropdown,  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   link to    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Filters,   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Deals,     ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   name click ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   /companies ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Card Grid) ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Edit/Del)  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   navigates) ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   /:id)      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“
       ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡ click name
       ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬â€œÃ‚Âº/companies/:id
                   
ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Pipeline    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Leads       ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Dashboard   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Settings    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Page        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Page        ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Company    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (CompanyNameÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Company    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Company    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   name in    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   in profile ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   dropdown   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   creation/  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   recipient  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   settings)  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   in deal    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   linking in ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   context,   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   form)      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   conversion)ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   shortcuts) ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“
ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â  ÃƒÂ¢Ã¢â‚¬ÂÃ…â€™ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‚Â
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Copy Gen    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  Homepage    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  LeadWebhook ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ([Company   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (Feature    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  (companyNameÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Name]      ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   showcase:  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   webhook    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   template   ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Company    ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   field)     ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   variable)  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡   Accounts)  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡              ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬Å¡
ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“  ÃƒÂ¢Ã¢â‚¬ÂÃ¢â‚¬ÂÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ‹Å“
```

---

## 39. What Is Missing â€” Prioritized Analysis (Updated After Implementation)

> **Seventh-pass update (February 9, 2026):** All 4 HIGH PRIORITY items have been **implemented**. Every change verified against the live codebase. This section now reflects the post-implementation state.

---

### IMPLEMENTATION STATUS â€” Seventh Pass (February 9, 2026)

**ALL HIGH PRIORITY ITEMS ARE NOW IMPLEMENTED.** Only LOW PRIORITY (nice-to-have) improvements remain.

| Category | Count |
|----------|-------|
| âœ… Originally missing â€” NOW FIXED | 15 |
| âœ… HIGH PRIORITY items â€” NOW IMPLEMENTED | 4 |
| ðŸŸ¡ LOW PRIORITY â€” Nice to Have (improvements) | 10 |

---

### Previously Reported Items â€” Now VERIFIED as FIXED

These items were listed as missing or buggy in earlier passes but are **now confirmed implemented** in the current codebase:

| # | Item | Evidence |
|---|------|----------|
| 1 | Description/Website/Location fields | `Company.cs` lines 15-17, `CompanyDto.cs` lines 9-13, `CreateCompanyRequest.cs` lines 38-53, `UpdateCompanyRequest.cs` lines 36-53 |
| 2 | CreatedAtUtc/UpdatedAtUtc in DTO | `CompanyDto.cs` lines 12-13, `CompanyDetail.tsx` lines 271-273 |
| 3 | Delete warning with entity counts | `Companies.tsx` lines 1212-1245 âœ… AND `CompanyDetail.tsx` lines 509-524 âœ… (both pages) |
| 4 | Duplicate name detection | `CompanyService.cs` â€” now uses efficient `ExistsByNameAsync` (HIGH-2 fix) |
| 5 | Company Detail Page | `CompanyDetail.tsx` (530+ lines), route `/companies/:id` in `App.tsx` |
| 6 | Deal drill-down on cards | Company cards show clickable deals |
| 7 | pageSize clamp | `CompaniesController.cs` line 53: `Math.Clamp(pageSize, 1, 100)` |
| 8 | NormalizeNullable in CreateAsync | `CompanyService.cs` lines 141-146 |
| 9 | NormalizeNullable in UpdateAsync | `CompanyService.cs` lines 200-213 |
| 10 | Location card display | Cards show `company.location` correctly |
| 11 | Search deduplication | `CompanyRepository.SearchAsync` reuses `ApplySearch()` |
| 12 | Contactâ†’Company link | `ContactDetail.tsx` links company name to `/companies/:companyId` |
| 13 | CompanyDetail delete dialog unlink warning | `CompanyDetail.tsx` lines 509-524 |
| 14 | Server-Side Company Stats API | `CompanyRepository.GetStatsAsync()` with SQL GROUP BY, `GET /api/companies/stats` endpoint |
| 15 | Global Search UI | `AppHeader.tsx`: `GlobalSearchBar` component with Cmd+K, debounced search, categorized results |

---

### ===== âœ… HIGH PRIORITY â€” ALL IMPLEMENTED =====

All 4 HIGH PRIORITY items that were identified in the sixth pass have now been implemented. Here is the summary of what was done:

---

#### âœ… HIGH-1. CompanyDetail.tsx â€” Server-Side Filtering by companyId (IMPLEMENTED)

**Problem:** `CompanyDetail.tsx` was loading ALL contacts/deals in the system and filtering client-side, causing silent data truncation at scale.

**What was changed (13 files across backend + frontend):**

| Layer | File | Change |
|-------|------|--------|
| Interface | `IContactRepository.cs` | Added `Guid? companyId = null` parameter to `GetPagedAsync` |
| Interface | `IDealRepository.cs` | Added `Guid? companyId = null` parameter to `GetPagedAsync` |
| Interface | `IContactService.cs` | Added `Guid? companyId = null` parameter to `GetContactsPagedAsync` |
| Interface | `IDealService.cs` | Added `Guid? companyId = null` parameter to `GetDealsPagedAsync` |
| Repository | `ContactRepository.cs` | Added `if (companyId.HasValue) query = query.Where(c => c.CompanyId == companyId.Value)` in `GetPagedAsync` |
| Repository | `DealRepository.cs` | Added `if (companyId.HasValue) query = query.Where(d => d.CompanyId == companyId.Value)` in `GetPagedAsync` |
| Service | `ContactService.cs` | Passes `companyId` through to repository |
| Service | `DealService.cs` | Passes `companyId` through to repository |
| Controller | `ContactsController.cs` | Added `[FromQuery] Guid? companyId = null` parameter to `GetContacts` endpoint |
| Controller | `DealsController.cs` | Added `[FromQuery] Guid? companyId = null` parameter to `GetDeals` endpoint |
| Frontend API | `contacts.ts` | Added `companyId` to `getContactsPaged` params and query string |
| Frontend API | `deals.ts` | Added `companyId` to `getDealsPaged` params and query string |
| Frontend Page | `CompanyDetail.tsx` | Changed to `getContactsPaged({ page: 1, pageSize: 100, companyId: id })` â€” server-side filtered, no client-side filtering |

**Result:** CompanyDetail now uses `GET /api/contacts?companyId={id}&page=1&pageSize=100` and `GET /api/deals?companyId={id}&page=1&pageSize=100`. The database filters by `CompanyId` in SQL (uses index), returns only relevant records. No more loading thousands of unrelated records.

---

#### âœ… HIGH-2. Duplicate Name Check â€” ExistsByNameAsync (IMPLEMENTED)

**Problem:** `CompanyService.CreateAsync` loaded ALL companies into memory to check for duplicate names â€” O(n) memory for an O(1) operation.

**What was changed (3 files):**

| File | Change |
|------|--------|
| `ICompanyRepository.cs` | Added `Task<bool> ExistsByNameAsync(string name, Guid userId, Guid? organizationId, CancellationToken ct)` |
| `CompanyRepository.cs` | Implemented `ExistsByNameAsync` using `AnyAsync(c => c.Name.ToLower() == trimmedName)` â€” generates a single SQL `EXISTS` query |
| `CompanyService.cs` | Replaced `GetByUserIdAsync` + `.FirstOrDefault()` with `ExistsByNameAsync`. Also removed the `try/catch` wrapper that silently swallowed errors |

**Result:** Duplicate name check now executes a single `SELECT CASE WHEN EXISTS(SELECT 1 FROM Companies WHERE LOWER(Name) = @name ...) THEN 1 ELSE 0 END` query. O(1) instead of O(n). No entities loaded into memory.

---

#### âœ… HIGH-3. CompanyDetail.tsx â€” Wasteful Fallback Removed (IMPLEMENTED)

**Problem:** When the direct `GET /api/companies/{id}` failed, `CompanyDetail.tsx` fell back to loading 1000 companies and searching in-memory. The fallback was also broken because the backend clamps `pageSize` to 100.

**What was changed (1 file):**

| File | Change |
|------|--------|
| `CompanyDetail.tsx` | Removed the entire `getCompaniesPaged({ page: 1, pageSize: 1000 })` fallback. Now uses only the direct `GET /api/companies/{id}` endpoint. On failure, shows an error toast. Also removed the unused `getCompaniesPaged` import. |

**Result:** Clean, single-endpoint fetch. No more downloading 100 companies to find one. Errors are surfaced to the user instead of silently failing.

---

#### âœ… HIGH-4. DealDetail.tsx â€” Company Name Now Clickable (IMPLEMENTED)

**Problem:** On `DealDetail.tsx`, the company name was plain text, while on `ContactDetail.tsx` it was a clickable link â€” inconsistent navigation.

**What was changed (1 file):**

| File | Change |
|------|--------|
| `DealDetail.tsx` | Added `Link` import from `react-router-dom`. Replaced plain `<p>{deal.companyName}</p>` with `<Link to={/companies/${deal.companyId}} className="text-sm font-medium text-emerald-600 hover:underline">{deal.companyName}</Link>`. Falls back to plain text if `companyId` is null. |

**Result:** Company names on deal detail pages are now clickable links, consistent with contact detail pages. Users can navigate to the company detail page with a single click from any entity.

---

### ===== ðŸŸ¡ LOW PRIORITY â€” NICE TO HAVE =====

These are improvements that would enhance the product but are **not blocking production use.** No data is lost or corrupted without them. Implement when time allows.

---

#### ðŸŸ¡ LOW-1. CompanyDetail â€” Missing Leads Section

**Effort:** 2 hours | **Impact:** Incomplete 360Â° view

**What's missing:** `CompanyDetail.tsx` shows Contacts and Deals sections but NO Leads section. The backend `Company.cs` has `ICollection<Lead> Leads` (line 27), confirming the relationship exists. The new `companyId` filter pattern (HIGH-1) can be reused for leads.

**Why it would help:** In B2B sales, the lead-to-customer journey starts with leads. Without seeing leads on the company page, incoming marketing leads are invisible to sales reps viewing the account.

---

#### ðŸŸ¡ LOW-2. CompanyDetail â€” Missing Activity Timeline

**Effort:** 1 day | **Impact:** No interaction history

**What's missing:** No activity section on `CompanyDetail.tsx`. The Activity entity has `ContactId` and `DealId` but no direct `CompanyId`.

**Why it would help:** The detail page shows structure (contacts, deals) but not what happened â€” last call, last email, next meeting. Competitor standard: Salesforce, HubSpot, and Pipedrive all show unified activity timelines on account pages.

---

#### ðŸŸ¡ LOW-3. React Query Hooks Are Dead Code (108 Lines)

**Effort:** 10 min (delete) or 1-2 days (refactor) | **Impact:** Code maintenance

**What's missing:** `useCompanies.ts` defines 5 React Query hooks. None are imported or used by any component. Either delete the file (10 minutes) or refactor pages to use the hooks (1-2 days).

---

#### ðŸŸ¡ LOW-4. Companies.tsx â€” Still Fetches Deals Separately Despite Stats API

**Effort:** 1-2 hours | **Impact:** Unnecessary bandwidth

**What's happening:** `Companies.tsx` calls both `getCompanyStats()` and `getDealsPaged({ page: 1, pageSize: 500 })`. The deals data is only used for the deal drill-down feature on cards (showing deal names). Stats provide the counts. The deal fetch could be removed if the card drill-down links to CompanyDetail instead.

---

#### ðŸŸ¡ LOW-5. SendToCrmParams Missing 'company' Type

**Effort:** 30 min | **Impact:** Minor feature gap

**What's missing:** `types.ts` line 251: `objectType: 'contact' | 'deal' | 'lead' | 'workflow' | 'email'` â€” no `'company'`.

---

#### ðŸŸ¡ LOW-6. Company Phone/Email Fields

**Effort:** 1 day | **Impact:** Missing standard CRM fields

**What's missing:** No company-level phone or email fields. Only individual contacts have phone/email.

---

#### ðŸŸ¡ LOW-7. Company Reporting/Analytics in ReportingService

**Effort:** 2-3 days | **Impact:** No account-level metrics on dashboard

**What's missing:** `ReportingService.cs` has zero company-related methods. The dashboard is entirely deal/lead focused.

---

#### ðŸŸ¡ LOW-8. Search Result Limit â€” Silent `.Take(20)`

**Effort:** 1-2 hours | **Impact:** Users miss search results

**What's missing:** `CompanyRepository.SearchAsync()` line 67: `.Take(20)` silently truncates results.

---

#### ðŸŸ¡ LOW-9. Company Notes/Comments System

**Effort:** 2 days | **Impact:** No qualitative account data

**What's missing:** No notes or comments system for companies.

---

#### ðŸŸ¡ LOW-10. Future Roadmap Items

| # | Feature | Why It Would Help | Effort |
|---|---------|-------------------|--------|
| a | **Company Logo** | Visual identification on cards. Clearbit/Logo.dev integration. | Medium |
| b | **Tags / Labels** | Custom categorization: "Key Account", "Partner", "At Risk". | Medium |
| c | **Revenue Field** | Company revenue for prioritization. | Low |
| d | **Structured Address** | Geographic segmentation, territory management. | Low |
| e | **Import / Export** | CSV bulk operations for CRM migration. | Medium |
| f | **Social Links** | LinkedIn, Twitter for research and social selling. | Low |
| g | **Account Owner** | Separate "owner" from "creator" for territory assignments. | Low |
| h | **Hierarchy** | Parent-child companies for corporate structures. | Medium |
| i | **Merge Duplicates** | Merge duplicate companies, reassign all linked entities. | Medium |
| j | **Audit Log** | Full change history. Who changed what and when. | Medium |
| k | **Custom Fields** | User-defined fields for industry-specific needs. | High |
| l | **Mock Data** | Expanded mock data for demo mode. | Low |

---

### UI/UX Inconsistencies (Updated Status)

| # | Inconsistency | Status | Details |
|---|---------------|--------|---------|
| 1 | Dead form fields | âœ… FIXED | Description, Website, Location persisted end-to-end. |
| 2 | "Newest First" sort broken | âœ… FIXED | `createdAtUtc` now in DTO. |
| 3 | "This Week" stat always 0 | âœ… FIXED | Uses real timestamps. |
| 4 | Delete warning incomplete | âœ… FIXED on BOTH pages | Both `Companies.tsx` and `CompanyDetail.tsx` show amber warning. |
| 5 | Search dual execution | âš ï¸ STILL PRESENT | Server + client search on paginated results. |
| 6 | Contact/Deal counts client-side | âœ… FIXED | `getCompanyStats()` uses server-side SQL GROUP BY. |
| 7 | Industry/Size filters client-side | âš ï¸ STILL PRESENT | Only filters current page. Low priority. |
| 8 | Pipeline lazy-loads companies | âš ï¸ STILL PRESENT | Company names may not appear on deal cards. |
| 9 | DealDetail company not clickable | âœ… FIXED | HIGH-4 implemented â€” now a `<Link>` to `/companies/:companyId`. |

---

### TOTAL EFFORT SUMMARY

| Tier | Items | Status |
|------|-------|--------|
| âœ… **HIGH PRIORITY** (must do) | 4 bugs | **ALL IMPLEMENTED** |
| ðŸŸ¡ **LOW PRIORITY** (nice to have) | 10 improvements | ~6-10 days when time allows |
| ðŸ“‹ **Roadmap** | 12 future items | TBD |

**Current state:** All critical bugs are fixed. The company system has:
- Server-side filtering by `companyId` on contacts and deals endpoints (no more data truncation)
- Efficient O(1) duplicate name detection (no more loading entire table)
- Clean single-endpoint company detail fetch (no more wasteful fallback)
- Consistent cross-entity navigation (company names clickable from all entity pages)
- Server-side statistics API (`GET /api/companies/stats`)
- Global search UI with Cmd+K keyboard shortcut

**Remaining work is all LOW PRIORITY** â€” leads section, activity timeline, dead code cleanup, reporting, and future roadmap items.

---

*This section provides a complete, source-verified analysis of the Cadence CRM company system. All backend and frontend files re-read on February 9, 2026 (seventh pass). 19 originally-missing items confirmed as implemented (including all 4 HIGH PRIORITY items implemented in this session with exact file/change documentation). 10 LOW PRIORITY nice-to-have items remain for future sprints. Last updated: February 9, 2026.*
