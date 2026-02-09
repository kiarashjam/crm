# Contact System — Complete Interaction Report

This document is the single definitive reference for **every interaction** the "Contact" entity has across the entire Cadence CRM codebase (backend and frontend). It covers the entity definition, API layer, business logic, database configuration, UI components, cross-entity relationships, and identifies what is missing.

Last updated: February 9, 2026 (second pass — every backend and frontend file re-read).

---

## Table of Contents

1. [Contact Entity (Backend Domain)](#1-contact-entity-backend-domain)
2. [Backend API Endpoints](#2-backend-api-endpoints)
3. [Backend Business Logic (Services)](#3-backend-business-logic-services)
4. [Backend Data Access (Repositories)](#4-backend-data-access-repositories)
5. [Backend DTOs](#5-backend-dtos)
6. [Database Configuration (EF Core)](#6-database-configuration-ef-core)
7. [Domain Errors](#7-domain-errors)
8. [Cross-Entity Contact Interactions (Backend)](#8-cross-entity-contact-interactions-backend)
9. [Frontend — API Client Layer](#9-frontend--api-client-layer)
10. [Frontend — TypeScript Types](#10-frontend--typescript-types)
11. [Frontend — React Query Hooks](#11-frontend--react-query-hooks)
12. [Frontend — Contacts Page (Main Contact UI)](#12-frontend--contacts-page-main-contact-ui)
13. [Frontend — Pipeline Page (Contact on Deals)](#13-frontend--pipeline-page-contact-on-deals)
14. [Frontend — Activities Page (Contact Filtering)](#14-frontend--activities-page-contact-filtering)
15. [Frontend — Tasks Page (Contact Linking)](#15-frontend--tasks-page-contact-linking)
16. [Frontend — Leads Page (Lead → Contact Conversion)](#16-frontend--leads-page-lead--contact-conversion)
17. [Frontend — Companies Page (Contact Display)](#17-frontend--companies-page-contact-display)
18. [Frontend — Dashboard (Contact as Recipient)](#18-frontend--dashboard-contact-as-recipient)
19. [Frontend — SendToCrm Page (Save Copy to Contact)](#19-frontend--sendtocrm-page-save-copy-to-contact)
20. [Frontend — Global Search (Contacts)](#20-frontend--global-search-contacts)
21. [Frontend — Navigation & Routing](#21-frontend--navigation--routing)
22. [Frontend — Query Keys](#22-frontend--query-keys)
23. [Backend — Email Sequences (Contact Enrollment)](#23-backend--email-sequences-contact-enrollment)
24. [Backend — Copy History & Template Generator (Contact as Recipient)](#24-backend--copy-history--template-generator-contact-as-recipient)
25. [Backend — Lead Conversion (Contact Creation — Detailed Flow)](#25-backend--lead-conversion-contact-creation--detailed-flow)
26. [Backend Unit Tests](#26-backend-unit-tests)
27. [Complete File Inventory](#27-complete-file-inventory)
28. [Complete Relationship Map](#28-complete-relationship-map)
29. [What Is Missing (Source-Verified Gaps Analysis)](#29-what-is-missing-source-verified-gaps-analysis)

---

## 1. Contact Entity (Backend Domain)

**File:** `backend/src/ACI.Domain/Entities/Contact.cs`

The core Contact entity representing a person in the CRM:

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Primary key (from BaseEntity) |
| `UserId` | `Guid` | FK to owning User (required) |
| `OrganizationId` | `Guid?` | FK to Organization (multi-tenant scoping) |
| `Name` | `string` | Contact's full name (required, default `string.Empty`) |
| `Email` | `string` | Contact's email (required, default `string.Empty`) |
| `Phone` | `string?` | Phone number (optional) |
| `JobTitle` | `string?` | Job title (optional) |
| `CompanyId` | `Guid?` | FK to Company (optional) |
| `CreatedAtUtc` | `DateTime` | Creation timestamp |
| `UpdatedAtUtc` | `DateTime?` | Last update timestamp |
| `UpdatedByUserId` | `Guid?` | FK to User who last updated |
| `ConvertedFromLeadId` | `Guid?` | FK to Lead (conversion traceability) |
| `ConvertedAtUtc` | `DateTime?` | When converted from lead |
| `IsArchived` | `bool` | Soft delete flag |
| `ArchivedAtUtc` | `DateTime?` | When archived |
| `ArchivedByUserId` | `Guid?` | FK to User who archived |
| `DoNotContact` | `bool` | Compliance flag — do not contact |
| `PreferredContactMethod` | `string?` | Preferred method of contact (max 32 chars) |
| `Description` | `string?` | Contact description / notes |

**Navigation Properties:**
- `User` — Owner (required)
- `UpdatedByUser` — Last updater (optional)
- `ArchivedByUser` — User who archived (optional)
- `Organization` — Org scope (optional)
- `Company` — Associated company (optional)
- `ConvertedFromLead` — Lead that was converted to this contact (optional, one-to-one)
- `Activities` — `ICollection<Activity>` linked activities

> **Note:** The Contact entity does NOT have a direct `ICollection<Deal>` navigation property. Deals reference contacts via `Deal.ContactId`, but there is no reverse navigation collection on Contact.

---

## 2. Backend API Endpoints

### ContactsController (`backend/src/ACI.WebApi/Controllers/ContactsController.cs`)

All endpoints require `[Authorize]`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/contacts?page=&pageSize=&search=&includeArchived=` | Paginated list (page default 1, pageSize default 20 max 100, optional search, includeArchived default false) |
| `GET` | `/api/contacts/all?includeArchived=` | Get all contacts non-paginated (backward compat) |
| `GET` | `/api/contacts/search?q=&includeArchived=` | Search contacts by name or email (non-paginated) |
| `GET` | `/api/contacts/{id}` | Get single contact by ID |
| `POST` | `/api/contacts` | Create new contact (body: `CreateContactRequest`) — returns 200/400/409 |
| `PUT` | `/api/contacts/{id}` | Update contact — partial update (body: `UpdateContactRequest`) — returns 200/400/404/409 |
| `DELETE` | `/api/contacts/{id}` | Hard delete contact (returns 204) — nullifies FKs in Deals and Activities before deletion |
| `POST` | `/api/contacts/{id}/archive` | Soft delete / archive contact (returns 204) |
| `POST` | `/api/contacts/{id}/unarchive` | Restore archived contact (returns 204) |
| `GET` | `/api/contacts/check-email?email=&excludeId=` | Check if email already exists for another contact (returns `{ exists: bool }`) |

### Other Controllers that reference Contacts

| Controller | Endpoint | Contact Interaction |
|------------|----------|---------------------|
| `ActivitiesController` | `GET /api/activities/contact/{contactId}` | Get activities for a specific contact |
| `ActivitiesController` | `POST /api/activities` | Create activity optionally linked to a contact (`contactId` in body) |
| `TasksController` | `GET /api/tasks?contactId=` | Filter tasks by contact |
| `TasksController` | `GET /api/tasks/by-contact/{contactId}` | Get tasks by contact ID |
| `LeadsController` | `POST /api/leads/{id}/convert` | Convert lead → can create/link a contact |
| `SearchController` | `GET /api/search?q=` | Global search includes contacts |
| `CopyController` | `POST /api/copy/send` | Send copy to a contact record |

---

## 3. Backend Business Logic (Services)

### ContactService (`backend/src/ACI.Application/Services/ContactService.cs`)

Depends on: `IContactRepository`, `IActivityRepository` (for last activity enrichment), `ILogger<ContactService>`.

| Method | Logic |
|--------|-------|
| `GetContactsPagedAsync` | Pagination with search. **Enriches** each contact with `LastActivityAtUtc` via `_activityRepository.GetLastActivityByContactIdsAsync()`. Filters by `UserId` and `OrganizationId`. Excludes archived unless `includeArchived = true`. |
| `GetContactsAsync` | All contacts for user/org (non-paginated). **Enriches** with `LastActivityAtUtc`. |
| `SearchAsync` | Search by name or email (case-insensitive `Contains`). **Enriches** with `LastActivityAtUtc`. Trims the query. |
| `GetByIdAsync` | Single contact lookup. **Enriches** with `LastActivityAtUtc`. Returns `Result<ContactDto>`. |
| `CreateAsync` | Validates: Name non-empty, Email non-empty, Email valid format (`ValidationHelper.IsValidEmail`), Email unique per organization. Sets `CreatedAtUtc = DateTime.UtcNow`, `UserId`, `OrganizationId`. All string fields are trimmed. |
| `UpdateAsync` | Partial update — only non-null fields are patched. Validates email format if provided. Checks email uniqueness if changed (case-insensitive). Sets `UpdatedAtUtc` and `UpdatedByUserId`. |
| `DeleteAsync` | Hard delete (delegated to repository which nullifies FKs in Deals/Activities first). |
| `ArchiveAsync` | Sets `IsArchived = true`, `ArchivedAtUtc`, `ArchivedByUserId`. Returns false if already archived. |
| `UnarchiveAsync` | Sets `IsArchived = false`, clears `ArchivedAtUtc` and `ArchivedByUserId`. Returns false if not archived. |
| `EmailExistsAsync` | Checks email uniqueness per organization, can exclude a specific contact ID (for updates). |

> **Key design detail:** Every contact fetch method calls `GetLastActivityByContactIdsAsync` to enrich contacts with the latest activity timestamp. This means every contact query triggers an additional aggregation query against the Activities table.

### Other Services that reference Contacts

| Service | Method | Contact Interaction |
|---------|--------|---------------------|
| `LeadService` | `ConvertAsync` | Creates new contact from lead data OR links existing contact. Sets `ConvertedFromLeadId` and `ConvertedAtUtc`. Copies Name, Email, Phone, CompanyId from lead. |
| `ActivityService` | `CreateAsync` | Validates that `ContactId` exists if provided (calls `IContactRepository.GetByIdAsync`). |
| `ActivityService` | `GetByContactIdAsync` | Retrieves all activities linked to a specific contact. |
| `GlobalSearchService` | `SearchAsync` | Calls `_contactService.SearchAsync(query)` to include contacts in global search results (max 10 results). |
| `SendToCrmService` | `SendAsync` | Accepts `ObjectType = "contact"` to save generated copy against a contact record. |

**Service Registration:** Registered in `Program.cs` line 138: `services.AddScoped<IContactService, ContactService>()`.

---

## 4. Backend Data Access (Repositories)

### ContactRepository (`backend/src/ACI.Infrastructure/Repositories/ContactRepository.cs`)

| Method | Description |
|--------|-------------|
| `GetPagedAsync` | Paginated query with search and archive filtering, ordered by Name, filtered by `UserId + OrganizationId` |
| `CountAsync` | Count contacts with optional search and archive filtering |
| `GetByUserIdAsync` | All contacts for a user (non-paginated), ordered by Name |
| `SearchAsync` | Search by name or email (case-insensitive `Contains`), falls back to `GetByUserIdAsync` on empty query |
| `GetByIdAsync` | Single contact by ID, validates user/org ownership |
| `GetByEmailAsync` | Single contact by email (case-insensitive), excludes archived |
| `EmailExistsAsync` | Email uniqueness check — scoped by organization, excludes archived, optionally excludes specific contact ID |
| `AddAsync` | Insert new contact |
| `UpdateAsync` | Update contact — reloads from DB first to verify ownership, patches all fields |
| `DeleteAsync` | Hard delete — **nullifies `ContactId` on Deals and Activities** via `ExecuteUpdateAsync` before removing |
| `ArchiveAsync` | Sets `IsArchived = true`, `ArchivedAtUtc`, `ArchivedByUserId`. Returns false if already archived |
| `UnarchiveAsync` | Sets `IsArchived = false`, clears archive fields. Returns false if not archived |

**Filtering:** `FilterByUserAndOrg` ensures `UserId` match and `OrganizationId` match (null handling: if `organizationId` param is null, filters for `OrganizationId == null`).

**Search:** `ApplySearch` searches `Name` and `Email` fields (case-insensitive via `ToLowerInvariant().Contains()`).

**Repository Registration:** Registered in `DependencyInjection.cs` line 27: `services.AddScoped<IContactRepository, ContactRepository>()`.

### ActivityRepository — Contact-related methods

| Method | Description |
|--------|-------------|
| `GetByContactIdAsync` | All activities for a given contact |
| `GetLastActivityByContactIdsAsync` | Batch: latest activity date per contact ID (used by ContactService to enrich contacts with `LastActivityAtUtc`) |

---

## 5. Backend DTOs

### ContactDto (`backend/src/ACI.Application/DTOs/ContactDto.cs`)

```csharp
public record ContactDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string? JobTitle,
    Guid? CompanyId,
    DateTime? LastActivityAtUtc,          // Computed from Activities table
    Guid? ConvertedFromLeadId = null,
    DateTime? ConvertedAtUtc = null,
    bool IsArchived = false,
    bool DoNotContact = false,
    string? PreferredContactMethod = null,
    string? CompanyName = null,           // Enriched from Company navigation property
    DateTime? CreatedAtUtc = null,
    DateTime? UpdatedAtUtc = null,
    string? Description = null
);
```

> **Note:** `LastActivityAtUtc` is NOT stored on the Contact entity — it is computed at query time from the Activities table and injected into the DTO.

### CreateContactRequest (`backend/src/ACI.Application/DTOs/CreateContactRequest.cs`)

| Field | Type | Validation | Required |
|-------|------|------------|----------|
| `Name` | `string` | 1-200 chars | Yes |
| `Email` | `string` | Valid email, max 254 chars | Yes |
| `Phone` | `string?` | Valid phone format, max 50 chars | No |
| `JobTitle` | `string?` | Max 100 chars | No |
| `CompanyId` | `Guid?` | — | No |
| `Description` | `string?` | Max 4000 chars | No |

### UpdateContactRequest (`backend/src/ACI.Application/DTOs/UpdateContactRequest.cs`)

All fields are optional (partial update pattern):

| Field | Type | Validation |
|-------|------|------------|
| `Name` | `string?` | 1-200 chars |
| `Email` | `string?` | Valid email, max 254 chars |
| `Phone` | `string?` | Valid phone format, max 50 chars |
| `JobTitle` | `string?` | Max 100 chars |
| `CompanyId` | `Guid?` | — |
| `DoNotContact` | `bool?` | — |
| `PreferredContactMethod` | `string?` | Max 50 chars |
| `Description` | `string?` | Max 4000 chars |

### Other DTOs referencing Contacts

| DTO | File | Contact Fields |
|-----|------|----------------|
| `DealDto` | `DTOs/DealDto.cs` | `ContactId` — deal's primary contact |
| `CreateDealRequest` | `DTOs/CreateDealRequest.cs` | `ContactId` — optional contact to link |
| `UpdateDealRequest` | `DTOs/UpdateDealRequest.cs` | `ContactId` — optional contact to link |
| `ActivityDto` | `DTOs/ActivityDto.cs` | `ContactId` — activity's contact association |
| `CreateActivityRequest` | `DTOs/CreateActivityRequest.cs` | `ContactId` — optional, link activity to contact |
| `TaskDto` | `DTOs/TaskDto.cs` | `ContactId`, `ContactName` — task's contact association |
| `CreateTaskRequest` | `DTOs/CreateTaskRequest.cs` | `ContactId` — optional, link task to contact |
| `ConvertLeadRequest` | `DTOs/ConvertLeadRequest.cs` | `CreateContact`, `ExistingContactId` — lead conversion options |
| `ConvertLeadResult` | `DTOs/ConvertLeadResult.cs` | `ContactId` — returned when contact created/linked |
| `GlobalSearchResultDto` | `DTOs/GlobalSearchResultDto.cs` | Contains `List<ContactDto> Contacts` |
| `SendToCrmRequest` | `DTOs/SendToCrmRequest.cs` | `ObjectType` can be "contact", `RecordId` is contact ID |

---

## 6. Database Configuration (EF Core)

### ContactConfiguration (`backend/src/ACI.Infrastructure/Persistence/Configurations/ContactConfiguration.cs`)

- **Table:** `"Contacts"`
- `Name`: `MaxLength(256)`, Required
- `Email`: `MaxLength(256)`, Required
- `Phone`: `MaxLength(64)`
- `JobTitle`: `MaxLength(256)`
- `PreferredContactMethod`: `MaxLength(32)`

**Relationships:**
- FK: `UserId` → `User`, via `User.Contacts` collection (required, many-to-one)
- FK: `OrganizationId` → `Organization` (optional, many-to-one)
- FK: `CompanyId` → `Company`, via `Company.Contacts` collection (optional, many-to-one)
- FK: `UpdatedByUserId` → `User` (optional, many-to-one)
- FK: `ArchivedByUserId` → `User` (optional, many-to-one)
- FK: `ConvertedFromLeadId` → `Lead`, via `Lead.ConvertedToContact` (optional, one-to-one)

**Indexes:**
- `OrganizationId` — standard index
- `IsArchived` — standard index
- `DoNotContact` — standard index
- **Unique:** `(Email, OrganizationId)` with filter `[IsArchived] = 0` — unique email per organization, excluding archived contacts

### Related entity configurations

| Configuration File | Contact Relationship |
|-------------------|----------------------|
| `ActivityConfiguration.cs` | `Activity.ContactId` → `Contact` (one-to-many, optional) |
| `DealConfiguration.cs` | `Deal.ContactId` → `Contact` (many-to-one, optional) |
| `TaskConfiguration.cs` | `TaskItem.ContactId` → `Contact` (many-to-one, optional) |
| `LeadConfiguration.cs` | `Lead.ConvertedToContactId` → `Contact` (one-to-one nullable) |

### AppDbContext

- `DbSet<Contact> Contacts` registered in `AppDbContext.cs`

---

## 7. Domain Errors

**File:** `backend/src/ACI.Application/Common/DomainErrors.cs`

| Error Code | Message |
|------------|---------|
| `Contact.NotFound` | "The contact was not found" |
| `Contact.NameRequired` | "Contact name is required" |
| `Contact.EmailRequired` | "Contact email is required" |
| `Contact.EmailInvalid` | "The email format is invalid" |
| `Contact.DuplicateEmail` | "A contact with this email already exists" |
| `Contact.AlreadyArchived` | "The contact is already archived" |
| `Contact.NotArchived` | "The contact is not archived" |
| `Contact.DuplicateEmail` (with value) | "A contact with email '{email}' already exists" |

---

## 8. Cross-Entity Contact Interactions (Backend)

| Entity | Relationship | Direction |
|--------|-------------|-----------|
| **User** | `ICollection<Contact> Contacts` | User owns many Contacts |
| **Company** | `ICollection<Contact> Contacts` | Company has many Contacts |
| **Deal** | `Deal.ContactId` (optional FK) | Deal references a primary Contact |
| **Activity** | `Activity.ContactId` (optional FK) | Activity belongs to a Contact |
| **TaskItem** | `TaskItem.ContactId` (optional FK) | Task linked to a Contact |
| **Lead** | `Lead.ConvertedToContactId` (optional FK) | Lead converted to a Contact (one-to-one) |
| **Organization** | `Contact.OrganizationId` (optional FK) | Contact scoped to an Organization |
| **RecipientType enum** | `Contact` value | AI copy can target a contact as recipient |
| **SendToCrmRequest** | `ObjectType = "contact"` | Generated copy can be saved to a contact record |

### Deletion cascade behavior:
- When a Contact is **hard deleted**: `Deal.ContactId` and `Activity.ContactId` are set to `null` via `ExecuteUpdateAsync` before the Contact row is removed.
- When a Contact is **archived**: Only `IsArchived`, `ArchivedAtUtc`, `ArchivedByUserId` are set. No cascade to related entities.
- Archived contacts are **excluded from email uniqueness checks** (another contact can reuse the same email).

---

## 9. Frontend — API Client Layer

### `src/app/api/contacts.ts`

| Function | API Call | Description |
|----------|----------|-------------|
| `getContactsPaged(params)` | `GET /api/contacts?page=&pageSize=&search=&includeArchived=` | Paginated contacts with search |
| `getContacts(includeArchived)` | `GET /api/contacts/all?includeArchived=` | All contacts non-paginated (backward compat) |
| `searchContacts(query, includeArchived)` | `GET /api/contacts/search?q=&includeArchived=` | Search by name or email |
| `createContact(params)` | `POST /api/contacts` | Create contact. Returns `{ contact, error }` |
| `updateContact(id, params)` | `PUT /api/contacts/{id}` | Update contact (partial). Returns `{ contact, error }` |
| `deleteContact(id)` | `DELETE /api/contacts/{id}` | Hard delete. Returns `boolean` (true = 204) |
| `archiveContact(id)` | `POST /api/contacts/{id}/archive` | Soft delete. Returns `boolean` |
| `unarchiveContact(id)` | `POST /api/contacts/{id}/unarchive` | Restore archived. Returns `boolean` |
| `checkEmailExists(email, excludeId)` | `GET /api/contacts/check-email?email=&excludeId=` | Check email uniqueness |

**Mock mode:** When `isUsingRealApi()` is false, `getContactsPaged` and `getContacts` return mock data from `mockData.ts` with localStorage persistence. Create/update/delete operations return errors in mock mode.

**API response mapping:** `mapContact` function normalizes `null` values to `undefined` and boolean defaults (`isArchived: false`, `doNotContact: false`).

### Other API files that reference Contacts

| File | Function | Contact Interaction |
|------|----------|---------------------|
| `activities.ts` | `getActivitiesByContact(contactId)` | `GET /api/activities/contact/{contactId}` |
| `activities.ts` | `createActivity({contactId})` | `POST /api/activities` with optional `contactId` |
| `tasks.ts` | `getTasksByContact(contactId)` | `GET /api/tasks/by-contact/{contactId}` |
| `tasks.ts` | `createTask({contactId})` | `POST /api/tasks` with optional `contactId` |
| `leads.ts` | `convertLead(id, request)` | Request includes `createContact`, `existingContactId` |
| `search.ts` | `globalSearch(query)` | Returns `{contacts: ContactDto[]}` |
| `copyGenerator.ts` | `generateCopyWithRecipient()` | `recipientType: 'contact'` for AI personalization |
| `index.ts` | Barrel exports | Re-exports all contact functions |

---

## 10. Frontend — TypeScript Types

### `src/app/api/types.ts` — Contact interface (exact from source, lines 45-58)

```typescript
export interface Contact {
  id: string;
  name: string;
  email: string;
  phone?: string;
  jobTitle?: string;
  companyId?: string;
  lastActivityAtUtc?: string;
  convertedFromLeadId?: string;
  convertedAtUtc?: string;
  isArchived?: boolean;
  doNotContact?: boolean;
  preferredContactMethod?: string;
}
```

> **Note:** The frontend Contact interface does NOT include `createdAtUtc`, `updatedAtUtc`, `updatedByUserId`, `archivedAtUtc`, or `archivedByUserId`. These backend fields are not exposed to the frontend.

### `src/app/pages/contacts/types.ts` — Contact form state (exact from source)

```typescript
interface ContactFormState {
  name: string;
  email: string;
  phone: string;
  companyId: string;
  jobTitle: string;
  description: string;   // ⚠️ NOT on backend entity
}
```

> **Note:** `ContactFormState` has a `description` field that is NOT present on the backend `Contact` entity or any DTO. This is a dead field — any value entered is silently discarded.

### Other types referencing Contacts

| Type | File | Contact Fields |
|------|------|----------------|
| `Deal` | `types.ts` | `contactId?: string` |
| `TaskItem` | `types.ts` | `contactId?: string`, `contactName?: string` |
| `Activity` | `types.ts` | `contactId?: string` |
| `CopyHistoryItem` | `types.ts` | `recipientType: 'contact'` |
| `SendToCrmParams` | `types.ts` | `objectType: 'contact'` |

---

## 11. Frontend — React Query Hooks

### `src/app/hooks/queries/useContacts.ts`

| Hook | Query Key | Description |
|------|-----------|-------------|
| `useContacts(includeArchived)` | `['contacts', 'list', { includeArchived }]` | Fetch all contacts |
| `useSearchContacts(query, includeArchived)` | `['contacts', 'list', { search, includeArchived }]` | Search contacts (enabled when query.length > 0) |
| `useContactById(id)` | `['contacts', 'detail', id]` | Get single contact (client-side lookup from useContacts data) |
| `useCreateContact()` | mutation | Create contact, optimistically adds to `['contacts', 'list']` cache, shows toast |
| `useUpdateContact()` | mutation | Update contact, optimistically updates cache, shows toast |
| `useDeleteContact()` | mutation | Delete contact, optimistically removes from cache, shows toast |
| `useArchiveContact()` | mutation | Archive contact, invalidates `['contacts']`, shows toast |
| `useUnarchiveContact()` | mutation | Unarchive contact, invalidates `['contacts']`, shows toast |

---

## 12. Frontend — Contacts Page (Main Contact UI)

**File:** `src/app/pages/Contacts.tsx` — **1248 lines**

### Data Loading (lines 104-121)

On mount and when pagination/search changes:
```
Promise.all([
  getContactsPaged({ page, pageSize, search }),
  getCompanies()
])
```

> **Note:** The Contacts page does NOT load deals, activities, tasks, or leads. It only loads contacts and companies (for the company name lookup and company filter dropdown).

### Statistics Cards (lines 142-168)

| Stat | Description | Calculation |
|------|-------------|-------------|
| Total Contacts | Count of loaded contacts | `contacts.length` |
| With Company | Contacts with `companyId` set | `contacts.filter(c => c.companyId).length` |
| Recently Active (30d) | Contacts with activity in last 30 days | Checks `lastActivityAtUtc` |
| Need Attention | Contacts with no activity in 90+ days | Checks `lastActivityAtUtc` (also counts contacts with NO activity at all) |
| This Week | New contacts this week | Always `0` — ⚠️ `createdAtUtc` is not available on frontend `Contact` type |
| With Phone | Contacts with phone number | `contacts.filter(c => c.phone).length` |

### Filtering (lines 170-236)

| Filter | Options | Implementation |
|--------|---------|----------------|
| Search | Free text | Filters by name, email, phone, job title (client-side on loaded page) |
| Company | All / None / Specific company | `companyId` match |
| Activity | All / Recent (30d) / Inactive (90+d) | `lastActivityAtUtc` comparison |

### Sorting (lines 213-233)

| Sort Option | Field | Direction |
|-------------|-------|-----------|
| Name A-Z | `name` | asc |
| Name Z-A | `name` | desc |
| Recently Active | `lastActivityAtUtc` | desc |
| Newest First | `createdAt` | desc — ⚠️ Falls back to name sort (no `createdAtUtc` on frontend type) |
| Oldest First | `createdAt` | asc — ⚠️ Same fallback |

### Contact Card (lines 822-967)

Each contact is displayed as a card showing:
- **Avatar** with initials and color gradient (based on first char of name)
- **Name** (hover: turns blue)
- **Job Title** with Briefcase icon
- **Company name** with Building2 icon
- **Email** with Mail icon (clickable `mailto:` link)
- **Phone** with Phone icon (clickable `tel:` link)
- **Activity status**: Green dot (active), Amber dot (inactive), Grey dot (no activity)
- **Last activity date** formatted as "Month Day"
- **Inactive indicator**: Amber top bar on cards for contacts with no activity in 90+ days
- **Actions dropdown**: Edit Contact, Send Message, Delete
- **Message button** in card footer → navigates to `/send` with `{ contactId, contactName }` state

### Quick Insights Banner (lines 486-508)

When `inactive > 3`, shows a re-engagement opportunity banner suggesting the user reach out to inactive contacts.

### Create/Edit Dialog (lines 983-1138)

Form fields:
| Field | Type | Required | Placeholder |
|-------|------|----------|-------------|
| Full Name | text input | Yes | "Enter contact's full name" |
| Email Address | email input | Yes | "contact@company.com" |
| Phone | text input | No | "+1 (555) 000-0000" |
| Job Title | text input | No | "e.g. CEO, Manager" |
| Company | select dropdown | No | "Select a company" |

### Delete Confirmation (lines 1141-1156)

AlertDialog confirming hard delete. Message: "This will remove '{name}'. This action cannot be undone."

### Pagination (lines 970-978)

Uses `DataPagination` component with URL-based page/pageSize params. Supports page size selector.

---

## 13. Frontend — Pipeline Page (Contact on Deals)

**File:** `src/app/pages/Pipeline.tsx`

### Contact Interactions (Source-Verified)

1. **Loads contacts on mount**: `getContacts()` via `Promise.all` — used for deal form's contact dropdown
2. **Create Deal linked to Contact**: Deal creation form has a "Contact" `Select` dropdown — sets `contactId`
3. **Edit Deal contact**: Same dropdown in edit mode
4. **Display contact on DealCard**: Shows contact name with User icon below the deal value
5. **Deal Detail Sheet**: Shows associated contact name

> **Note:** Contact name is resolved client-side by joining `deal.contactId` with the loaded contacts array.

---

## 14. Frontend — Activities Page (Contact Filtering)

**File:** `src/app/pages/Activities.tsx`

### Contact Interactions

1. **Loads contacts on mount**: `getContacts()` to populate contact dropdown
2. **Filter by Contact**: "By Contact" tab → `Select` dropdown → calls `getActivitiesByContact(contactId)`
3. **Create Activity linked to Contact**: "Log Activity" dialog has a "Contact" dropdown → sets `form.contactId`
4. **Display contact on activity card**: Each activity card shows a colored badge with contact name when `activity.contactId` is set
5. **Statistics**: Counts activities linked to contacts

---

## 15. Frontend — Tasks Page (Contact Linking)

**File:** `src/app/pages/Tasks.tsx`

### Contact Interactions

1. **Loads contacts on mount**: `getContacts()` to populate contact dropdowns
2. **Create Task linked to Contact**: Task creation form has `contactId` field
3. **Display contact badge**: Task cards show `task.contactName` as a badge
4. **Task Detail Modal**: Shows contact info in "Linked Items" section

---

## 16. Frontend — Leads Page (Lead → Contact Conversion)

**File:** `src/app/pages/Leads.tsx`

### Contact Interactions

1. **Loads contacts on conversion**: `getContacts()` when conversion dialog opens
2. **"Create new contact" toggle**: Creates a new contact from lead data (Name, Email, Phone, CompanyId)
3. **"Attach to existing contact" dropdown**: Shows all existing contacts for linking
4. **Conversion flow visualization**: Visual flow diagram showing Lead → Contact + Company + Deal

---

## 17. Frontend — Companies Page (Contact Display)

**File:** `src/app/pages/Companies.tsx`

### Contact Interactions

1. **Loads contacts on mount**: `getContacts()` to compute per-company contact counts
2. **Per-company contact count**: `contacts.filter(c => c.companyId === companyId).length`
3. **Stats cards**: "Total Contacts" stat card, "With Contact" count
4. **Company card footer**: Shows contact count with Users icon

---

## 18. Frontend — Dashboard (Contact as Recipient)

**File:** `src/app/pages/Dashboard.tsx`

### Contact Interactions

1. **Loads contacts on mount**: `getContacts()` for recipient picker in Sales Writer
2. **Recipient type "Contacts"**: `recipientType` state includes `'contact'`
3. **Contact search/selection**: Search input + contact list in picker
4. **Personalization**: Contact info passed to `generateCopy()` as `RecipientContext`

---

## 19. Frontend — SendToCrm Page (Save Copy to Contact)

**File:** `src/app/pages/SendToCrm.tsx`

### Contact Interactions

1. **Loads contacts on mount**: `getContacts()` via `useEffect`
2. **Object type "Contact"**: Tab button — user selects "Contact" as CRM object type
3. **Record selection**: Scrollable list of contacts showing name + email
4. **Send**: Calls `sendCopyToCrm({ objectType: 'contact', recordId: contact.id, content: generatedCopy })`

---

## 20. Frontend — Global Search (Contacts)

**File:** `src/app/api/search.ts`

- `globalSearch(query)` returns `{contacts: ContactDto[]}` alongside leads, deals, and companies
- Used by the AppHeader search functionality
- Contact results show name and email

---

## 21. Frontend — Navigation & Routing

### AppHeader (`src/app/components/AppHeader.tsx`)
- Navigation item: `{path: '/contacts', label: 'Contacts', icon: UserCircle}`

### Navigation Config (`src/app/config/navigation.ts`)
- `NAV_ITEMS` includes `{path: '/contacts', label: 'Contacts', icon: UserCircle}`

### App Router (`src/app/App.tsx`)
- Route: `/contacts` → lazy-loaded `Contacts` component
- Protected route requiring authentication

---

## 22. Frontend — Query Keys

**File:** `src/app/hooks/queries/queryKeys.ts`

```typescript
contacts: {
  all: ['contacts'],
  lists: () => ['contacts', 'list'],
  list: (filters?) => ['contacts', 'list', filters],
  details: () => ['contacts', 'detail'],
  detail: (id) => ['contacts', 'detail', id],
}
```

---

## 23. Backend — Email Sequences (Contact Enrollment)

**Files:**
- `backend/src/ACI.Application/DTOs/AnalyticsDto.cs` (`EnrollInSequenceRequest`, `EmailSequenceEnrollmentDto`)
**Note:** Email Sequences feature has been removed (February 2026). These files no longer exist.

### How Contacts Participate

Contacts can be **enrolled** in automated email sequences. The `EnrollInSequenceRequest` has:

```csharp
public record EnrollInSequenceRequest
{
    public Guid SequenceId { get; init; }
    public Guid? ContactId { get; init; }   // ← Contact enrollment
    public Guid? LeadId { get; init; }      // ← Lead enrollment (alternative)
    public string? RecipientEmail { get; init; }
    public string? RecipientName { get; init; }
}
```

The enrollment DTO tracks the contact throughout the sequence lifecycle:

```csharp
public record EmailSequenceEnrollmentDto(
    Guid Id,
    Guid SequenceId,
    string? SequenceName,
    Guid? ContactId,       // ← FK to Contact
    Guid? LeadId,
    string? RecipientEmail,
    string? RecipientName,
    int CurrentStep,
    string Status,         // active | paused | completed | unsubscribed
    DateTime EnrolledAtUtc,
    DateTime? LastSentAtUtc,
    DateTime? NextSendAtUtc,
    DateTime? CompletedAtUtc
);
```

### Frontend Email Sequence API

**File:** `src/app/api/emailSequences.ts`

The frontend `EnrollInSequenceRequest` mirrors the backend:

```typescript
export interface EnrollInSequenceRequest {
  sequenceId: string;
  contactId?: string;  // ← Contact enrollment
  leadId?: string;
}
```

Functions: `enrollInSequence()`, `pauseEnrollment()`, `resumeEnrollment()`, `unenroll()`.

Mock enrollments include contacts:
```typescript
{ id: 'e1', sequenceId: '1', contactId: 'c1', status: 'active', currentStep: 2 }
```

### Gap
**Note:** Email Sequences feature has been removed (February 2026). This integration is no longer applicable.
- No way to see from a contact's view which sequences they are enrolled in.
- No visual indicator on contact cards that a contact is in an active email sequence.

---

## 24. Backend — Copy History & Template Generator (Contact as Recipient)

### Copy History Service

**File:** `backend/src/ACI.Application/Services/CopyHistoryService.cs`

When generated copy is "sent to CRM", it is saved as a `CopyHistoryItem` with a `RecipientType`. **Contact is the default recipient type**.

```csharp
private static RecipientType ParseRecipientType(string s) => s?.ToLowerInvariant() switch
{
    "contact" => RecipientType.Contact,  // ← default
    "deal"    => RecipientType.Deal,
    "workflow"=> RecipientType.Workflow,
    "email"   => RecipientType.Email,
    _         => RecipientType.Contact,  // ← fallback is also Contact
};
```

**File:** `backend/src/ACI.Domain/Enums/RecipientType.cs`

```csharp
public enum RecipientType
{
    Contact  = 0,   // ← ordinal 0 = default
    Deal     = 1,
    Workflow = 2,
    Email    = 3,
}
```

### Template Copy Generator (Contact as primary audience)

**File:** `backend/src/ACI.Infrastructure/Services/TemplateCopyGenerator.cs`

All 1,600+ lines of templates use `[First Name]` placeholder. Templates are organized by `[copyTypeId|goal|brandTone]` keys:
- **sales-email** (Schedule a meeting, Product demo, etc.)
- **follow-up** (Check in, Re-engage, etc.)
- **cold-email** (Initial outreach, Book appointment, etc.)
- **linkedin-message**, **sms**, **call-script**

Every template is written addressing a **person** (Contact), e.g.:
```
Hi [First Name],
I hope this email finds you well. I wanted to reach out to see if you'd be
interested in scheduling a brief call to discuss how our solutions can help
[Company Name] achieve your goals.
```

### Frontend — Send To CRM Page (Contact Selection)

**File:** `src/app/pages/SendToCrm.tsx`

This page allows users to save generated copy to a CRM record. It:
1. Loads **all** contacts, deals, and leads on mount via `getContacts()`, `getDeals()`, `getLeads()`
2. Lets user pick object type: `contact | deal | lead | workflow | email`
3. Shows filterable list of records with client-side search
4. For contacts: shows `name` + `email` as subtitle
5. Calls `sendCopyToCrm({ objectType, recordId, recordName, copy, copyTypeLabel })`

```typescript
const filteredContacts = searchQuery.trim()
  ? contacts.filter(c =>
      c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      c.email.toLowerCase().includes(searchQuery.toLowerCase())
    )
  : contacts;
```

### Gap
- `SendToCrm.tsx` loads ALL contacts unpaginated (`getContacts()` returns all). At scale this will be slow — should use paginated search.
- No way to see copy history from a contact's view. Copy is saved to history but not linked back to the contact card or a contact detail page.

---

## 25. Backend — Lead Conversion (Contact Creation — Detailed Flow)

**Files:**
- `backend/src/ACI.Application/Services/LeadService.cs` (`ConvertAsync` method)
- `backend/src/ACI.Application/DTOs/ConvertLeadRequest.cs`
- `backend/src/ACI.Application/DTOs/ConvertLeadResult.cs`
- `src/app/pages/leads/ConvertLeadDialog.tsx`

### Full Conversion Logic (Backend)

The `LeadService.ConvertAsync` method handles the Lead → Contact conversion with these steps:

1. **Validate** lead exists and is not already converted.
2. **Handle Company**: create new or use existing.
3. **Handle Contact Creation**:
   - If `CreateContact == true` and `ExistingContactId` is null:
     - **Check for existing contact by email**: `_contactRepository.GetByEmailAsync(lead.Email, ...)`
     - If a contact with that email already exists:
       - **Reuse** the existing contact (no duplicate created)
       - Update its `CompanyId` if different
     - If no match:
       - Create new `Contact` with fields copied from lead:
         ```csharp
         Name = lead.Name,
         Email = lead.Email,
         Phone = lead.Phone,
         CompanyId = companyId,
         ConvertedFromLeadId = lead.Id,
         ConvertedAtUtc = DateTime.UtcNow,
         ```
   - If `ExistingContactId` is provided: validate it exists, then use it.
4. **Handle Deal**: create new (with `ContactId` linked) or attach to existing deal.
5. **Mark lead as converted**: sets `ConvertedToContactId`, `ConvertedAtUtc`, `IsConverted = true`.

```csharp
public record ConvertLeadRequest
{
    public bool CreateContact { get; init; }
    public bool CreateDeal { get; init; }
    public string? DealName { get; init; }
    public string? DealValue { get; init; }
    public string? DealStage { get; init; }
    public Guid? PipelineId { get; init; }
    public Guid? DealStageId { get; init; }
    public bool CreateNewCompany { get; init; }
    public string? NewCompanyName { get; init; }
    public Guid? ExistingCompanyId { get; init; }
    public Guid? ExistingContactId { get; init; }  // ← attach to existing
    public Guid? ExistingDealId { get; init; }
}

public record ConvertLeadResult(Guid? CompanyId, Guid? ContactId, Guid? DealId);
```

### Frontend Conversion Dialog

**File:** `src/app/pages/leads/ConvertLeadDialog.tsx`

A dedicated dialog component with:
- **Visual flow indicator**: Lead → Contact + Company + Deal (with animated highlights)
- **Contact section** (marked "Typical"):
  - "Create new contact" button (default, pre-filled from lead email)
  - "Or use existing" dropdown — shows all existing contacts with name + email
- **Company section** (marked "Optional"):
  - "Create new company" or "Link to existing" dropdown
- **Deal section** (marked "Optional"):
  - "Create new deal" with name, value, pipeline, stage selectors
  - "Or attach to existing" deal dropdown
- Submit is **disabled** unless `createContact == true` OR an `existingContactId` is selected
- Props: `lead, onClose, convertForm, setConvertForm, convertOptions, converting, onConvert, companies`

### Gap
- After conversion, the user is NOT navigated to the new contact. They stay on the Leads page.
- No toast or link to "View new contact" after successful conversion.
- The newly created contact has `ConvertedFromLeadId` set, but this is **never displayed** in the Contacts UI.

---

## 26. Backend Unit Tests

**File:** `backend/tests/ACI.Application.Tests/Services/ContactServiceTests.cs`

| Test | Method | Assertion |
|------|--------|-----------|
| `GetContactsAsync_ReturnsContacts_WhenContactsExist` | `GetContactsAsync` | Returns 2 contacts with correct names |
| `GetContactsAsync_ReturnsEmptyList_WhenNoContactsExist` | `GetContactsAsync` | Returns empty list |
| `GetByIdAsync_ReturnsContact_WhenContactExists` | `GetByIdAsync` | Returns contact with matching ID and name |
| `GetByIdAsync_ReturnsNotFoundError_WhenContactDoesNotExist` | `GetByIdAsync` | Returns `Contact.NotFound` error |
| `CreateAsync_ReturnsContact_WhenValidRequest` | `CreateAsync` | Creates contact with correct fields |
| `CreateAsync_ReturnsError_WhenNameIsEmpty` | `CreateAsync` | Returns `Contact.NameRequired` error |
| `CreateAsync_ReturnsError_WhenEmailIsEmpty` | `CreateAsync` | Returns `Contact.EmailRequired` error |
| `CreateAsync_ReturnsError_WhenEmailAlreadyExists` | `CreateAsync` | Returns `DuplicateEmail` error |
| `UpdateAsync_ReturnsUpdatedContact_WhenValidRequest` | `UpdateAsync` | Updates name and phone correctly |
| `UpdateAsync_ReturnsNotFoundError_WhenContactDoesNotExist` | `UpdateAsync` | Returns `Contact.NotFound` error |
| `UpdateAsync_ReturnsError_WhenEmailAlreadyExists` | `UpdateAsync` | Returns `DuplicateEmail` error |
| `DeleteAsync_ReturnsSuccess_WhenContactDeleted` | `DeleteAsync` | Returns success |
| `DeleteAsync_ReturnsNotFoundError_WhenContactDoesNotExist` | `DeleteAsync` | Returns `Contact.NotFound` error |
| `ArchiveAsync_ReturnsSuccess_WhenContactArchived` | `ArchiveAsync` | Returns success |
| `ArchiveAsync_ReturnsNotFoundError_WhenContactDoesNotExist` | `ArchiveAsync` | Returns `Contact.NotFound` error |
| `UnarchiveAsync_ReturnsSuccess_WhenContactUnarchived` | `UnarchiveAsync` | Returns success |
| `UnarchiveAsync_ReturnsNotFoundError_WhenContactDoesNotExist` | `UnarchiveAsync` | Returns `Contact.NotFound` error |
| `SearchAsync_ReturnsMatchingContacts_WhenQueryMatches` | `SearchAsync` | Returns 1 contact for query "John" |

### Missing Test Coverage

| Area | What's Missing |
|------|---------------|
| **Archive filtering** | No tests verifying archived contacts are excluded from search/list results |
| **Pagination** | No tests for `GetPagedAsync` (only `GetContactsAsync` tested) |
| **Email uniqueness across orgs** | No tests verifying same email allowed in different organizations |
| **LastActivityAtUtc enrichment** | No tests verifying `LastActivityAtUtc` is correctly populated |
| **Lead conversion integration** | No tests covering contact creation via `LeadService.ConvertAsync` |
| **Email sequence enrollment** | No tests verifying contacts can be enrolled/unenrolled from sequences |
| **Activity creation with ContactId** | No tests verifying `ActivityService.CreateAsync` validates ContactId exists |
| **Global search contact results** | No tests verifying contacts appear in `GlobalSearchService.SearchAsync` results |

---

## 27. Complete File Inventory

### Backend Files (16 core + cross-entity)

| Category | Files |
|----------|-------|
| **Entity** | `Contact.cs` |
| **Service Interface** | `IContactService.cs` |
| **Service Impl** | `ContactService.cs` |
| **Repository Interface** | `IContactRepository.cs` |
| **Repository Impl** | `ContactRepository.cs` |
| **Controller** | `ContactsController.cs` |
| **DTOs** | `ContactDto.cs`, `CreateContactRequest.cs`, `UpdateContactRequest.cs` |
| **DB Config** | `ContactConfiguration.cs` |
| **Domain Errors** | `DomainErrors.cs` (Contact section) |
| **Tests** | `ContactServiceTests.cs` |
| **Cross-entity (entities)** | `Deal.cs` (ContactId FK), `Activity.cs` (ContactId FK), `TaskItem.cs` (ContactId FK), `Lead.cs` (ConvertedToContactId FK), `Company.cs` (Contacts collection), `User.cs` (Contacts collection) |
| **Cross-service** | `LeadService.cs` (ConvertAsync creates/links Contact), `ActivityService.cs` (validates ContactId), `GlobalSearchService.cs` (searches contacts), `CopyHistoryService.cs` (Contact as RecipientType) |
| **Cross-controller** | `ActivitiesController.cs`, `TasksController.cs`, `LeadsController.cs`, `SearchController.cs`, `CopyController.cs` |
| **Cross-config** | `ActivityConfiguration.cs`, `DealConfiguration.cs`, `TaskConfiguration.cs`, `LeadConfiguration.cs` |
| **Template/AI** | `TemplateCopyGenerator.cs` (all templates target Contact as audience via `[First Name]` placeholder) |
| **DI/Setup** | `Program.cs`, `DependencyInjection.cs`, `AppDbContext.cs` |

### Frontend Files (40+ files reference "contact")

| Category | Files |
|----------|-------|
| **API Client** | `contacts.ts`, `activities.ts`, `tasks.ts`, `leads.ts`, `search.ts`, `copyGenerator.ts`, `mockData.ts`, `index.ts`, `types.ts`, `messages.ts` |
| **React Query** | `useContacts.ts`, `useActivities.ts`, `queryKeys.ts`, `index.ts` |
| **Contacts UI** | `Contacts.tsx`, `contacts/types.ts`, `contacts/index.ts` |
| **Pipeline** | `Pipeline.tsx`, `DealCard.tsx`, `DroppableStageColumn.tsx`, `pipeline/types.ts` |
| **Activities** | `Activities.tsx`, `activities/types.ts` |
| **Tasks** | `Tasks.tsx`, `TaskDetailModal.tsx`, `KanbanTaskCard.tsx`, `ListTaskCard.tsx`, `TaskGroupSection.tsx`, `tasks/types.ts` |
| **Leads** | `Leads.tsx`, `ConvertLeadDialog.tsx`, `LeadDetailModal.tsx`, `AddLeadDialog.tsx`, `LeadStats.tsx`, `LeadFilters.tsx`, `leads/types.ts`, `leads/config.ts` |
| **Companies** | `Companies.tsx`, `companies/types.ts` |
| **Dashboard** | `Dashboard.tsx`, `SalesWriter.tsx`, `dashboard/config.ts`, `dashboard/types.ts` |
| **SendToCrm** | `SendToCrm.tsx` |
| **Lead Import** | `LeadImport.tsx`, `leadImport/config.tsx` |
| **Navigation** | `AppHeader.tsx`, `App.tsx`, `navigation.ts`, `ErrorBoundary.tsx` |
| **Config** | `leadConfig.ts`, `homepage/config.ts` |
| **Static** | `Homepage.tsx`, `Terms.tsx`, `Privacy.tsx`, `Help.tsx`, `README.md` |
| **Tests** | `test/mocks/handlers.ts` |

---

## 28. Complete Relationship Map

```
                    ┌──────────────┐
                    │     User     │
                    │   (Owner)    │
                    │ ICollection  │
                    │  <Contact>   │
                    └──────┬───────┘
                           │ owns many
                           ▼
┌──────────┐      ┌──────────────┐      ┌──────────────┐
│ Company  │──────│   Contact    │──────│  Organization │
│          │has   │              │ in   │              │
│ ICol<C>  │many  │  Name        │      │              │
└──────────┘      │  Email       │      └──────────────┘
                  │  Phone       │
                  │  JobTitle    │      ┌──────────────┐
                  │  DoNotContact│      │   Activity   │
                  │  IsArchived  │──────│ ContactId FK │
                  │  Preferred-  │ has  │ (one-to-many)│
                  │  ContactMeth │many  └──────────────┘
                  │  Activities  │
                  │  Converted-  │      ┌──────────────┐
                  │  FromLeadId  │      │  CopyHistory │
                  │  ConvertedAt │      │ RecipientType│
                  │              │ ◄────│  = Contact   │
                  └──────┬───────┘      └──────────────┘
                         │
                         │ referenced by
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│    Deal      │ │   TaskItem   │ │     Lead     │
│ ContactId FK │ │ ContactId FK │ │ Converted    │
│  (optional)  │ │  (optional)  │ │ ToContactId  │
└──────────────┘ └──────────────┘ └──────────────┘
                                         │
                  ┌──────────────┐        │ converts to
                  │  EmailSeq    │        ▼
                  │  Enrollment  │  ┌──────────────┐
                  │ ContactId FK │  │   Contact    │
                  │  (optional)  │  │  (new or     │
                  └──────────────┘  │   existing)  │
                                    └──────────────┘

Frontend Interaction Map:
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│  Contacts   │  │  Pipeline   │  │  Activities  │  │   Tasks     │
│  Page       │  │  Page       │  │  Page        │  │   Page      │
│  (Main UI)  │  │  (Contact   │  │  (Filter by  │  │  (Link to   │
│  CRUD, grid │  │   on deals) │  │   Contact)   │  │   Contact)  │
└─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│  Leads      │  │  Companies  │  │  Dashboard   │  │  SendToCrm  │
│  Page       │  │  Page       │  │  SalesWriter │  │  (Save copy │
│  ConvertLead│  │  (Contact   │  │  (Contact as │  │   to a      │
│  Dialog →   │  │   counts)   │  │   Recipient) │  │   Contact)  │
│  Contact    │  │             │  │              │  │             │
└─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ EmailSeq    │  │ Global      │  │  Template    │
│ Page        │  │ Search      │  │  Generator   │
│ (Enroll     │  │ (Contacts   │  │  (All copy   │
│  Contact)   │  │  returned)  │  │   targets    │
└─────────────┘  └─────────────┘  │  [First Name])│
                                   └─────────────┘
```

---

## 29. What Is Missing — Comprehensive Detailed Analysis

This section provides a thorough breakdown of every missing or incomplete contact-related feature. For each item we explain: **what currently exists** (with exact file/line references), **what is missing**, **why we need it** (business justification), and **what it would take to implement** (technical scope).

### IMPLEMENTATION STATUS (Updated February 9, 2026)

**5 of 7 HIGH PRIORITY items have been implemented or partially implemented.**

| Status | Count |
|--------|-------|
| ✅ Implemented | 4 |
| ⚠️ Partially Done | 1 |
| ❌ Not Started | 2 |

### A. HIGH PRIORITY — Must Be Done

These are bugs, data-loss issues, or fundamental CRM features that directly block normal contact management workflows. Each includes implementation guidance.

---

#### HP-1. Contact Detail Page (`/contacts/:id`) — ✅ IMPLEMENTED

**Effort:** Medium (2-3 days) | **Impact:** Critical

**What exists now:** Contacts are displayed as cards in a paginated grid. The only way to see a contact's full details is through the edit dialog. There is no dedicated page showing all related information.

**Source evidence:** `App.tsx` now has a `/contacts/:id` route (line 118). `ContactDetail.tsx` exists (447 lines) with a full detail view showing linked deals, activities, company, and edit/delete actions.

**Why we need it:**
1. **Deep linking**: Sales managers cannot share a link to a specific contact. "Look at the Jane Smith contact" requires verbal navigation instructions. Every CRM (Salesforce, HubSpot, Pipedrive) has contact detail pages with unique URLs.
2. **360-degree view**: Before calling a contact, a rep needs everything in one place: company info, linked deals (pipeline context), activities (interaction history), tasks (pending follow-ups), email sequence status, copy history. Currently this requires visiting 5+ separate pages.
3. **Relationship context**: A contact could be the decision-maker on a $500K deal, but the Contacts page gives zero sales context — no deal info, no activity history, no task info. The card only shows name, email, phone, and company.
4. **Team handoffs**: When a rep inherits accounts, they need to quickly review each contact's full history. Without a detail page, they must cross-reference across pages for every single contact.

**How to implement:**
- **Frontend**: Create `CompanyDetail.tsx`, add route `/contacts/:id` in `App.tsx`. Fetch contact by ID, linked deals, activities, tasks. Tabbed layout: Overview, Deals, Activities, Tasks. Sidebar with company card and quick actions.
- **Backend**: No changes needed — `GET /api/contacts/{id}` already exists.

---

#### HP-2. Contact → Deal Visibility (Critical Relationship Gap) — ✅ IMPLEMENTED

**Effort:** Low (half day) | **Impact:** Critical

**What exists now:** `Contacts.tsx` imports `getContactsPaged` and `getCompanies` only. It does NOT import `getDeals`, does NOT fetch deals, and shows ZERO deal information anywhere on the page.

**Source evidence:** `Contacts.tsx` now imports `getDealsPaged` and fetches deals. Contact cards display associated deal information. Deal visibility is fully implemented.

**Why we need it:**
1. **Sales is about people AND money**: When a rep looks at a contact, the first question is: "What's the commercial relationship?" A contact linked to three $100K deals is treated very differently than a contact with no deals. Currently, there is ZERO indication of commercial importance on the Contacts page.
2. **Prioritization**: Reps receive inbound calls and need to quickly assess who they're speaking with. A contact on a $500K deal should be handled differently than a contact who was a cold lead. Without deal visibility, every contact appears equal.
3. **Every competitor has this**: Salesforce shows "Opportunities" on contact records. HubSpot shows "Deals" on contact profiles. Pipedrive shows "Deals" on person views. This is table-stakes CRM functionality.

**How to implement:**
- Import `getDeals` and `Deal` type in `Contacts.tsx`
- Load deals on mount alongside companies
- Filter deals by `contactId` for each contact
- Show deal count + total value on contact cards
- Add "Associated Deals" section with deal name, value, stage badge

---

#### HP-3. ContactDto Missing `CreatedAtUtc` (BUG — Multiple Features Broken) — ✅ IMPLEMENTED

**Effort:** Low (half day) | **Impact:** High

**What exists now:** ~~`ContactDto` does NOT include timestamps~~ **FIXED:** `ContactDto` now includes `CreatedAtUtc`, `UpdatedAtUtc`, `CompanyName`, and `Description`. Frontend `Contact` type includes `createdAtUtc` and `updatedAtUtc`.

**Source evidence:** `ContactDto.cs` lines 15-16: `DateTime? CreatedAtUtc = null, DateTime? UpdatedAtUtc = null`. Frontend uses these for "This Week" stat and sorting.

**Why we need it:**
1. **"This Week" stat is permanently zero**: A prominent stat card shows "0" for contacts added this week — even if the user just added 10 contacts today. This is misleading and makes the UI look broken.
2. **Sorting is broken**: "Newest First" and "Oldest First" sort options silently produce alphabetical results. The sort labels lie to users.
3. **Audit visibility**: Users cannot see when a contact was added or last updated. No creation date displayed anywhere.

**How to implement:**
- **Backend**: Add `DateTime CreatedAtUtc` and `DateTime? UpdatedAtUtc` to `ContactDto`. Update `ContactService.Map()`.
- **Frontend**: Add `createdAtUtc`/`updatedAtUtc` to `Contact` type. Fix sorting. Compute "This Week" stat. Show dates on cards.

---

#### HP-4. DoNotContact Flag Not Enforced (Compliance Risk)

**Effort:** Low (few hours) | **Impact:** Critical for compliance

**What exists now:** `Contact.cs` has `DoNotContact` boolean. `UpdateContactRequest` supports it. But:
- No UI to set or display the flag
- `TemplateCopyGenerator` does not check it before generating copy
- `CopyHistoryService` does not check it before saving content
- `ActivityService` does not check it

**Source evidence:** `Contact.cs` has `DoNotContact`. `Contacts.tsx` has zero references to "doNotContact". No service in the codebase reads this field.

**Why we need it:**
1. **Legal compliance**: In many jurisdictions (GDPR in EU, CAN-SPAM in US, DSGVO in Switzerland), contacting someone who has opted out is a legal violation. The `DoNotContact` flag exists specifically for this purpose, but it's decorative — the system will happily generate and send content to flagged contacts.
2. **Business risk**: If a contact explicitly asked not to be contacted (legal threat, complaint, personal request), and a sales rep unknowingly sends them an AI-generated email because the flag is invisible and unchecked, the company faces reputational and legal risk.
3. **User trust**: The field exists in the database, meaning someone designed this feature. Any auditor or compliance officer would assume it's enforced. The gap between "exists" and "enforced" is dangerous.

**How to implement:**
- **Frontend**: Show a prominent "Do Not Contact" badge on contact cards. Add toggle in edit form. Make cards visually distinct (red border, warning icon).
- **Backend**: Check `DoNotContact` in `TemplateCopyGenerator` and `CopyHistoryService`. Return error if flag is set.
- **Frontend**: Block "Send Message" and "Enroll in Sequence" actions for DoNotContact contacts.

---

#### HP-5. Contact Archive/Unarchive UI Missing (Dead Feature) — ⚠️ PARTIAL

**Effort:** Low (few hours) | **Impact:** High

**What exists now:** Full implementation exists end-to-end: backend `archive`/`unarchive` endpoints, frontend API functions (`archiveContact`/`unarchiveContact`), React Query hooks (`useArchiveContact`/`useUnarchiveContact`). But the Contacts page UI only exposes Edit, Send Message, and Delete in the dropdown menu.

**Source evidence:** `Contacts.tsx` dropdown menu has Edit, Send Message, Delete. No Archive option. Hooks exist but are unused.

**Why we need it:**
1. **Data safety**: Without archive, the only way to remove a contact from the active list is hard delete, which is permanent and irreversible. If a user accidentally deletes the wrong contact, the data is gone forever along with all its linked history.
2. **Soft delete is standard**: Every major CRM has archive/soft-delete functionality. Contacts may become inactive (changed companies, left the market) but their historical data is valuable — past activities, deals, and notes provide institutional memory.
3. **Full implementation wasted**: Someone invested significant effort building archive end-to-end (entity field, backend endpoints, frontend API, React Query hooks). All of that work sits unused because a single UI button was never added.

**How to implement:**
- Add "Archive" option to contact card dropdown menu
- Show "Archived" tab or toggle on Contacts page to view archived contacts
- Add "Unarchive" action on archived contact cards
- Optionally show archived contact count in stats

---

#### HP-6. Contact Activity Timeline (No Interaction History on Contact Page) — ✅ IMPLEMENTED

**Effort:** Low-Medium (1-2 days) | **Impact:** High

**What exists now:** Activities can be linked to contacts via `ContactId`. `getActivitiesByContact(contactId)` API exists and works. But `Contacts.tsx` does NOT import or call it. Activities for a contact can only be seen by navigating to the Activities page and filtering "By Contact".

**Why we need it:**
1. **Context before calls**: Before calling a contact, a rep needs to see: "When did we last speak? What was discussed? Did we send the proposal?" Currently, they must leave the Contacts page, go to Activities, filter by contact — 3+ clicks and a mental context switch.
2. **Relationship health**: Seeing "last activity 3 months ago" on a contact card immediately signals the relationship has gone cold. Without this, stale contacts look identical to actively engaged ones.
3. **Activity logging**: There's no way to log a call, email, or meeting directly from the contact context. Users must navigate to the Activities page or a deal's detail sheet. This discourages activity logging because it's too many steps.

**How to implement:**
- Import `getActivitiesByContact` in `Contacts.tsx`
- When viewing a contact detail (or in future detail page), fetch and display activities
- Show "Last Activity: {date}" on contact cards (backend already enriches this via `LastActivityAtUtc`)
- Add inline "Log Activity" form with type selector and notes

---

#### HP-7. Contact Notes / Description — ⚠️ PARTIAL (Backend complete, Frontend incomplete)

**Effort:** Low (half day) | **Impact:** Medium-High

**What exists now:** **Backend is complete:** `Contact.cs` has a `Description` property, `ContactDto` includes it, `CreateContactRequest` and `UpdateContactRequest` accept it. **Frontend is incomplete:** `Contacts.tsx` form does not render a description input field and does not send `description` in the create/update API calls. The `ContactFormState` has a `description` field but it is unused.

**Remaining work:**
- **Frontend**: Add `<Textarea>` for description in the contact create/edit form. Include `description` in `handleSubmit` API calls. Display in contact detail view.

---

### B. MEDIUM PRIORITY — Should Do

These improve the product experience and fill important gaps that affect daily usability. They are not bugs or broken features, but their absence limits the product's value significantly. **Implementation instructions are provided** so these can be picked up immediately after HIGH items are complete.

---

#### MP-1. Contact → Task Visibility

**Effort:** Low (half day) | **Impact:** Medium-High

**What's missing:** `Contacts.tsx` shows zero task information. `ContactId` FK exists on `TaskItem`, and `getTasksByContact(contactId)` works, but is never called from the Contacts page.

**Why we need it:** When viewing a contact, users need to see pending tasks: "Follow up with Jane about the proposal", "Send updated pricing to John." Without this, users must navigate to the Tasks page and filter by contact, or rely on memory. Tasks created for contacts become invisible from the contact's perspective, leading to missed follow-ups and duplicate task creation.

**How to implement — step by step:**

1. **Frontend — `Contacts.tsx`**: Import the tasks API:
   ```typescript
   import { getTasksByContact } from '@/app/api';
   import type { TaskItem } from '@/app/api/types';
   ```

2. **Frontend — Contact detail view**: When a contact is selected for detail view, fetch tasks:
   ```typescript
   const [contactTasks, setContactTasks] = useState<TaskItem[]>([]);
   useEffect(() => {
     if (selectedContact) {
       getTasksByContact(selectedContact.id).then(setContactTasks);
     }
   }, [selectedContact?.id]);
   ```

3. **Frontend — Contact detail**: Render tasks section:
   ```tsx
   <div className="mt-4">
     <h4 className="text-sm font-medium mb-2">Tasks ({contactTasks.length})</h4>
     {contactTasks.length === 0 ? (
       <p className="text-sm text-slate-400">No tasks linked to this contact.</p>
     ) : contactTasks.map(task => (
       <div key={task.id} className="flex items-center gap-2 py-1.5 border-b last:border-0">
         <Badge variant={task.status === 'completed' ? 'default' : 'outline'}>{task.status}</Badge>
         <span className="text-sm flex-1 truncate">{task.title}</span>
         {task.dueDateUtc && <span className="text-xs text-slate-500">{formatDate(task.dueDateUtc)}</span>}
       </div>
     ))}
   </div>
   ```

4. **Frontend — Contact card** (optional): Show pending task count as a small badge: "2 tasks" next to the contact actions.

5. **Test:** Create a task linked to a contact. Open contact detail. Verify task appears with title, status, and due date.

---

#### MP-2. Contact → Email Sequence Visibility

**Effort:** Medium (1 day) | **Impact:** Medium-High

**What's missing:** `Contacts.tsx` has no reference to `emailSequences.ts`. No way to see which sequences a contact is enrolled in, their enrollment step, or whether they've completed the sequence.

**Why we need it:** Email sequences are automated outreach campaigns. A rep looking at a contact needs to know: "Is Jane currently receiving our onboarding email sequence? What step is she on? Should I hold off on manual outreach until the sequence completes?" Without this visibility, reps may manually contact someone who's about to receive an automated email — creating confusion and unprofessional double-contact.

**How to implement — step by step:**

1. **Backend — Check if endpoint exists**: Verify `GET /api/email-sequences/enrollments?contactId={id}` or similar exists. If not, create it in `EmailSequenceController.cs`:
   ```csharp
   [HttpGet("enrollments")]
   public async Task<ActionResult<List<EnrollmentDto>>> GetEnrollmentsByContact([FromQuery] Guid contactId)
   {
       var enrollments = await _sequenceService.GetEnrollmentsByContactAsync(contactId, orgId);
       return Ok(enrollments);
   }
   ```

2. **Backend — `EnrollmentDto`**: Include `SequenceName`, `CurrentStep`, `TotalSteps`, `Status` (Active, Completed, Paused, Unsubscribed), `EnrolledAtUtc`.

3. **Frontend — `emailSequences.ts`**: Add API function:
   ```typescript
   export async function getEnrollmentsByContact(contactId: string): Promise<Enrollment[]> {
     const response = await apiClient.get(`/api/email-sequences/enrollments?contactId=${contactId}`);
     return response.data;
   }
   ```

4. **Frontend — Contact detail view**: Fetch and display:
   ```tsx
   <div className="mt-4">
     <h4 className="text-sm font-medium mb-2">Email Sequences</h4>
     {enrollments.map(e => (
       <div key={e.id} className="flex items-center gap-2 py-1.5">
         <Badge variant={e.status === 'active' ? 'default' : 'secondary'}>{e.status}</Badge>
         <span className="text-sm">{e.sequenceName}</span>
         <span className="text-xs text-slate-500">Step {e.currentStep}/{e.totalSteps}</span>
       </div>
     ))}
   </div>
   ```

5. **Frontend — Contact card** (optional): Show "In sequence" indicator badge on contact cards for contacts currently in active sequences.

6. **Test:** Enroll a contact in a sequence. View contact. Verify sequence name, step progress, and status display correctly.

---

#### MP-3. Contact Import/Export

**Effort:** Medium (2-3 days) | **Impact:** High

**What's missing:** No CSV/Excel import or export for contacts. Leads have `LeadImport.tsx` but contacts do not.

**Why we need it:** CRM migration (importing contacts from another tool) requires bulk import. Reporting needs (sending contact lists to marketing, sharing with partners) require export. Without import, each contact must be manually entered — a deal-breaker for teams with 500+ contacts in their existing system. Without export, teams can't use contacts in external tools (email marketing platforms, event management, etc.). The lead import feature proves the pattern is already established in the codebase.

**How to implement — step by step:**

1. **Import — Frontend — Create `ContactImport.tsx`** (follow `LeadImport.tsx` pattern):
   - File upload zone accepting `.csv` and `.xlsx`
   - Column mapping step: map CSV columns to contact fields (Name, Email, Phone, Job Title, Company)
   - Preview table showing first 10 rows
   - "Import" button that calls the backend

2. **Import — Backend — `ContactsController.cs`**: Add import endpoint:
   ```csharp
   [HttpPost("import")]
   public async Task<ActionResult<ImportResultDto>> ImportContacts([FromBody] List<CreateContactRequest> contacts)
   {
       var results = new ImportResultDto();
       foreach (var contact in contacts)
       {
           try {
               await _contactService.CreateAsync(contact, userId, orgId);
               results.SuccessCount++;
           } catch (DuplicateEmailException) {
               results.Skipped.Add(new SkippedItem(contact.Email, "Duplicate email"));
           }
       }
       return Ok(results);
   }
   ```

3. **Export — Frontend — `Contacts.tsx`**: Add "Export CSV" button in toolbar:
   ```typescript
   const handleExport = () => {
     const csv = contacts.map(c =>
       `"${c.name}","${c.email}","${c.phone ?? ''}","${c.jobTitle ?? ''}","${c.companyName ?? ''}"`
     ).join('\n');
     const header = '"Name","Email","Phone","Job Title","Company"\n';
     downloadFile(header + csv, 'contacts.csv', 'text/csv');
   };
   ```

4. **Export — Backend (optional)**: Add `GET /api/contacts/export?format=csv` endpoint for server-side export with all fields.

5. **Frontend — `Contacts.tsx`**: Add "Import" and "Export" buttons next to "Add Contact":
   ```tsx
   <Button variant="outline" onClick={() => setShowImport(true)}>Import</Button>
   <Button variant="outline" onClick={handleExport}>Export CSV</Button>
   ```

6. **Test:** Export contacts → verify CSV opens in Excel with correct columns. Import a CSV with 50 contacts → verify they appear on Contacts page. Import with duplicate emails → verify skipped with message.

---

#### MP-4. "Enroll in Sequence" from Contact Page

**Effort:** Low-Medium (half day) | **Impact:** Medium

**What's missing:** No "Enroll in Sequence" action on contact cards. Users must navigate to the Email Sequences page to enroll contacts.

**Why we need it:** The natural workflow is: view contact → decide they need nurturing → enroll in sequence. Currently this requires: view contact → remember their name → navigate to Email Sequences → find the right sequence → search for the contact → enroll. This multi-step process discourages sequence usage and breaks workflow.

**How to implement — step by step:**

1. **Frontend — `Contacts.tsx`**: Add "Enroll in Sequence" to the contact card dropdown menu:
   ```tsx
   <DropdownMenuItem onClick={() => setEnrollDialog({ contactId: contact.id, contactName: contact.name })}>
     <Mail className="w-4 h-4 mr-2" />
     Enroll in Sequence
   </DropdownMenuItem>
   ```

2. **Frontend — Create `EnrollInSequenceDialog.tsx`**:
   ```tsx
   <Dialog open={open}>
     <DialogContent>
       <h3>Enroll {contactName} in Email Sequence</h3>
       <Select value={selectedSequenceId} onValueChange={setSelectedSequenceId}>
         {sequences.map(s => <SelectItem key={s.id} value={s.id}>{s.name} ({s.stepCount} steps)</SelectItem>)}
       </Select>
       <Button onClick={() => enrollContact(selectedSequenceId, contactId)}>Enroll</Button>
     </DialogContent>
   </Dialog>
   ```

**Status:** ✅ **REMOVED** — Email Sequences feature has been removed. This integration is no longer needed.

5. **Test:** Click "Enroll in Sequence" on a contact. Select a sequence. Click Enroll. Navigate to Email Sequences page and verify the contact appears as enrolled.

---

#### MP-5. PreferredContactMethod Not Displayed or Used

**Effort:** Low (few hours) | **Impact:** Medium

**What's missing:** `Contact.cs` has `PreferredContactMethod` field, `UpdateContactRequest` supports it, but no UI to set or display it, and `TemplateCopyGenerator` ignores it.

**Why we need it:** If a contact prefers phone calls over email, reps should see this before starting an email sequence. If they prefer LinkedIn over phone, a call would be unwelcome. The data structure exists but is completely invisible to users, making it dead infrastructure.

**How to implement — step by step:**

1. **Frontend — Contact edit form**: Add a dropdown for preferred method:
   ```tsx
   <Select value={formState.preferredContactMethod ?? ''} onValueChange={(v) => setFormState({...formState, preferredContactMethod: v})}>
     <SelectItem value="">No preference</SelectItem>
     <SelectItem value="email">Email</SelectItem>
     <SelectItem value="phone">Phone</SelectItem>
     <SelectItem value="linkedin">LinkedIn</SelectItem>
     <SelectItem value="whatsapp">WhatsApp</SelectItem>
   </Select>
   ```

2. **Frontend — Contact cards**: Display preferred method as a small icon/badge when set:
   ```tsx
   {contact.preferredContactMethod && (
     <Badge variant="outline" className="text-xs">
       Prefers {contact.preferredContactMethod}
     </Badge>
   )}
   ```

3. **Frontend — Warning on non-preferred channel**: When generating copy or enrolling in an email sequence, check if the contact's preference differs:
   ```tsx
   {contact.preferredContactMethod === 'phone' && (
     <div className="text-amber-600 text-xs flex items-center gap-1">
       <AlertTriangle className="w-3 h-3" /> This contact prefers phone calls
     </div>
   )}
   ```

4. **Backend — `TemplateCopyGenerator.cs`**: Include `PreferredContactMethod` in the prompt context so AI-generated copy can reference it.

5. **Frontend — `types.ts`**: Add `preferredContactMethod?: string` to the `Contact` type if not already present.

6. **Test:** Set a contact's preferred method to "phone." View the contact card — verify badge shows. Try generating email copy — verify warning appears.

---

#### MP-6. Email Uniqueness — No Frontend Pre-Validation

**Effort:** Low (few hours) | **Impact:** Medium

**What's missing:** `checkEmailExists` API endpoint exists but `Contacts.tsx` does NOT call it before form submission. Relies only on server-side 409 error.

**Why we need it:** When a user types a duplicate email and clicks Save, they get a generic error instead of a helpful "This email is already associated with {Contact Name}." Real-time validation as the user types (with debounce) would prevent the error entirely and guide the user to the existing contact.

**How to implement — step by step:**

1. **Frontend — `Contacts.tsx`**: Add debounced email validation:
   ```typescript
   const [emailError, setEmailError] = useState<string | null>(null);
   const checkEmail = useMemo(() =>
     debounce(async (email: string) => {
       if (!email || !email.includes('@')) { setEmailError(null); return; }
       try {
         const exists = await checkEmailExists(email);
         setEmailError(exists ? `This email is already used by another contact.` : null);
       } catch { setEmailError(null); }
     }, 500),
   []);
   ```

2. **Frontend — Email input**: Call validation on blur or change:
   ```tsx
   <Input
     type="email"
     value={formState.email}
     onChange={(e) => { setFormState({...f, email: e.target.value}); checkEmail(e.target.value); }}
     className={emailError ? 'border-red-500' : ''}
   />
   {emailError && <p className="text-xs text-red-500 mt-1">{emailError}</p>}
   ```

3. **Frontend — Save button**: Disable when email error exists:
   ```tsx
   <Button disabled={!!emailError} onClick={handleSave}>Save Contact</Button>
   ```

4. **Frontend — Optionally link to existing**: If a duplicate is found, show a link: "View existing contact →" that navigates to the duplicate.

5. **Test:** Type an email that already exists. Verify error message appears within 1 second. Verify Save button is disabled. Change to a unique email. Verify error clears.

---

#### MP-7. Contact Communication History (Unified Timeline)

**Effort:** Medium (2-3 days) | **Impact:** High (depends on HP-1 detail page)

**What's missing:** No single view combining activities, sent copies, email sequence interactions, and task completions for a contact. Each lives in a separate page.

**Why we need it:** A complete contact relationship requires seeing the full story: "Sent intro email (copy) → Logged call (activity) → Enrolled in nurture sequence (sequence) → Created follow-up task (task) → Sent proposal (copy) → Scheduled meeting (activity)." Currently, piecing this together requires visiting 4+ pages. A unified timeline on the contact detail page (HP-1) would show everything chronologically.

**How to implement — step by step:**

1. **Frontend — Create `ContactTimeline.tsx`** component:
   ```typescript
   interface TimelineEvent {
     id: string;
     type: 'activity' | 'copy' | 'sequence' | 'task';
     title: string;
     description?: string;
     timestamp: string;
     icon: React.ReactNode;
   }
   ```

2. **Frontend — Data aggregation**: Fetch from multiple endpoints in parallel:
   ```typescript
   const [activities] = await Promise.all([
     getActivitiesByContact(contactId),
     getCopyHistoryByContact(contactId),  // if endpoint exists
     getEnrollmentsByContact(contactId),
     getTasksByContact(contactId),
   ]);
   ```

3. **Frontend — Merge and sort**: Convert each data type to `TimelineEvent`, merge arrays, sort by `timestamp` descending:
   ```typescript
   const timeline = [
     ...activities.map(a => ({ type: 'activity', title: a.subject, timestamp: a.createdAtUtc, ... })),
     ...copies.map(c => ({ type: 'copy', title: `Generated ${c.templateType}`, timestamp: c.createdAtUtc, ... })),
     ...tasks.map(t => ({ type: 'task', title: t.title, timestamp: t.completedAtUtc ?? t.createdAtUtc, ... })),
   ].sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
   ```

4. **Frontend — Render timeline** with vertical line connecting events:
   ```tsx
   <div className="space-y-4">
     {timeline.map(event => (
       <div key={event.id} className="flex gap-3 relative">
         <div className="w-8 h-8 rounded-full bg-slate-100 flex items-center justify-center shrink-0">
           {event.icon}
         </div>
         <div className="flex-1 pb-4 border-l-2 border-slate-200 pl-4">
           <p className="text-sm font-medium">{event.title}</p>
           <p className="text-xs text-slate-500">{formatRelativeTime(event.timestamp)}</p>
           {event.description && <p className="text-sm text-slate-600 mt-1">{event.description}</p>}
         </div>
       </div>
     ))}
   </div>
   ```

5. **Frontend — Contact detail page (HP-1)**: Include `<ContactTimeline contactId={contact.id} />` as one of the tabs or as the main content area.

6. **Backend (optional optimization)**: Create a dedicated `GET /api/contacts/{id}/timeline` endpoint that aggregates all event types server-side in a single query for better performance.

7. **Test:** Create a contact with 3 activities, 2 tasks, and 1 copy history entry. Open contact detail. Verify all events appear chronologically in the timeline with correct icons and types.

---

### C. LOW PRIORITY — Nice to Have

These features add meaningful value but the contact system is functional without them. They can be planned for future sprints after HIGH and MEDIUM items are addressed.

---

#### LP-1. Contact Tags / Categories

**Effort:** Medium | **Impact:** Medium

**What exists now:** Contacts can only be categorized by Company (dropdown) and Activity status (active/inactive based on `lastActivityAtUtc`). There is no tagging, labeling, or custom categorization system. No `ContactTag` entity, no tag management UI, no tag-based filtering.

**What is missing:** The ability to assign custom tags or categories to contacts (e.g., "VIP", "Decision Maker", "Partner", "Vendor", "At Risk", "Referral Source", "Event Attendee").

**Why we need it:**
1. **Segmentation beyond structure**: Company and activity status are only two dimensions. Sales teams need many more: "Who are my VIP contacts?", "Show all decision makers", "Filter to contacts from last week's trade show." Tags enable dynamic, multi-dimensional segmentation that structured fields can't provide.
2. **Marketing alignment**: Marketing teams segment contacts for campaigns — "Send this email to all 'Product Launch Interest' contacts." Without tags, there's no way to create targeted lists for outreach beyond company/activity filters.
3. **Account-based selling**: Tagging contacts by role in the buying process ("Champion", "Economic Buyer", "Technical Evaluator", "Blocker") helps teams map the decision-making landscape at a company. This is essential for enterprise sales.
4. **CRM standard**: Every commercial CRM (Salesforce, HubSpot, Pipedrive, Zoho) supports contact tagging. Its absence makes the product feel incomplete to experienced CRM users.

**How to implement:**
- Backend: New `Tag` entity (Id, Name, Color, UserId, OrgId) and `ContactTag` junction table (ContactId, TagId). CRUD endpoints for tags. Update `ContactsController` to support tag-based filtering (`GET /api/contacts?tags=VIP,DecisionMaker`).
- Frontend: Tag management in Settings. Tag selector (multi-select chips) in contact create/edit form. Tag badges on contact cards. Tag filter dropdown on Contacts page.

---

#### LP-2. Contact Merge / Deduplication

**Effort:** Medium-High | **Impact:** Medium

**What exists now:** Email uniqueness is enforced per organization (`ContactRepository.EmailExistsAsync` + unique index on `(Email, OrganizationId)` filtered by `IsArchived = 0`). This prevents exact email duplicates. But contacts with different emails but the same person (e.g., "Jane Smith" at jane@acme.com and jane.smith@gmail.com) are not detected.

**What is missing:** Duplicate detection beyond email matching, and a merge workflow to combine duplicate records.

**Why we need it:**
1. **Data grows, duplicates multiply**: At 500+ contacts, duplicates are inevitable. A lead converts to a contact with their work email. Later, the same person submits a form with their personal email. Now you have two contact records for the same person — activities, deals, and tasks are split between them.
2. **Fragmented relationship view**: If Jane Smith has 10 activities on one record and 5 activities on another, no single view shows all 15 interactions. This makes relationship assessment inaccurate and can lead to embarrassing situations ("We already sent Jane that proposal" — "No, that was on her other record").
3. **Pipeline inflation**: Two contacts for the same person can independently be linked to deals, inflating contact counts and skewing analytics.
4. **Merge complexity**: When duplicates are found, simply deleting one loses data. A proper merge workflow needs to: (a) combine activities from both records, (b) re-link deals/tasks to the surviving record, (c) preserve the most complete data fields, (d) create an audit trail of the merge.

**How to implement:**
- Backend: Duplicate detection service that scores potential matches by name similarity (Levenshtein distance), phone number match, and company match. New endpoint: `GET /api/contacts/duplicates` returning pairs with confidence scores. Merge endpoint: `POST /api/contacts/{id}/merge/{sourceId}` that transfers all activities, deals, tasks, and email sequence enrollments from source to target, then archives the source.
- Frontend: "Potential Duplicates" warning panel on Contacts page. Side-by-side comparison view showing both records' fields. "Merge" button with field-by-field resolution (pick which name, email, phone to keep).

---

#### LP-3. Contact Social Profiles

**Effort:** Low | **Impact:** Low-Medium

**What exists now:** `Contact.cs` has Name, Email, Phone, JobTitle, CompanyId. No fields for social media URLs. No LinkedIn, Twitter/X, Facebook, or other social profile fields.

**What is missing:** Structured fields for social media profiles, especially LinkedIn.

**Why we need it:**
1. **LinkedIn is the primary B2B research tool**: Before any B2B sales call, the first thing a rep does is look up the contact on LinkedIn. Currently they must: copy the contact name → open LinkedIn in a new tab → search → find the right profile. A LinkedIn URL on the contact record would be a single click.
2. **Outreach channel**: LinkedIn InMail and connection requests are major B2B outreach channels. Having the profile URL on the contact record makes multi-channel outreach seamless: email, phone, AND LinkedIn from one view.
3. **Profile enrichment**: Social profiles provide context that CRM fields don't: recent posts (topics they care about), job changes (timing for outreach), mutual connections (warm introductions), endorsements (expertise areas).
4. **Professional identity verification**: A LinkedIn profile confirms the contact's current job title, company, and role — helping verify that CRM data is still accurate.

**How to implement:**
- Backend: Add `LinkedInUrl` (string?, MaxLength 512), `TwitterHandle` (string?, MaxLength 100), and optionally a generic `SocialProfiles` JSON field to `Contact.cs`. Add to DTOs. Migration.
- Frontend: Social profile inputs in contact create/edit form (with URL validation). LinkedIn/Twitter icons on contact cards linking to profiles. "View on LinkedIn" quick action.

---

#### LP-4. Contact Address

**Effort:** Low | **Impact:** Low

**What exists now:** `Contact.cs` has no address fields whatsoever. No street, city, state, country, or postal code.

**What is missing:** Structured address fields for physical location.

**Why we need it:**
1. **Visit planning**: Field sales reps need client addresses for in-person meetings. Currently, the address must be stored in notes (which don't exist yet — see HP-7) or looked up externally each time.
2. **Geographic segmentation**: "Show all contacts in the Zürich area" or "All contacts in DACH region" — impossible without location data. Geographic segmentation is essential for territory management, event planning, and regional campaigns.
3. **Timezone awareness**: Knowing a contact is in San Francisco (PST) vs. London (GMT) prevents calling at inappropriate hours. Address data enables timezone calculation.
4. **Shipping/Billing**: If the CRM extends to customer success or account management, physical addresses are needed for shipping, billing, and contract purposes.

**How to implement:**
- Backend: Add address fields to `Contact.cs`: `Street` (string?, 256), `City` (string?, 128), `State` (string?, 128), `Country` (string?, 128), `PostalCode` (string?, 32). Add to DTOs. Migration.
- Frontend: Collapsible "Address" section in contact create/edit form. Address display on contact cards (city, country). Optional: Google Maps link for the address.

---

#### LP-5. Contact Profile Picture / Avatar

**Effort:** Medium | **Impact:** Low

**What exists now:** Contact cards display auto-generated avatars showing the first character of the contact's name in a gradient-colored circle. No way to upload or display actual photos.

**What is missing:** The ability to upload a profile picture for a contact, or auto-fetch one from a public source (e.g., Gravatar based on email).

**Why we need it:**
1. **Visual recognition**: At a conference or trade show, a rep needs to recognize a contact they've only spoken to by phone. A profile photo helps: "That's Jane from Acme Corp — I recognize her from her photo in our CRM."
2. **Faster scanning**: When scrolling through a list of 50+ contacts, names and initials blur together. Photos provide instant visual differentiation, reducing cognitive load and speeding up contact identification.
3. **Professional appearance**: When presenting the CRM in meetings ("Let me pull up our contacts at Acme Corp"), real photos look more professional and complete than colored circles with initials.
4. **Low-cost alternative**: If full file upload infrastructure isn't available, Gravatar integration is free and automatic — just hash the contact's email and check if a Gravatar exists. Many business professionals have Gravatar profiles linked to their work email.

**How to implement:**
- **Option A (Gravatar — low effort)**: Hash the contact's email, fetch from `gravatar.com/avatar/{hash}?d=404`. Display if found, fall back to initials if not.
- **Option B (Upload — medium effort)**: File upload endpoint on `ContactsController`. Store in blob storage. New `AvatarUrl` field on entity. Image resize/crop on upload. Display on contact cards.

---

#### LP-6. Contact-Scoped Analytics

**Effort:** Medium | **Impact:** Medium

**What exists now:** The Contacts page shows simple metadata: `lastActivityAtUtc` (last activity date) and activity-based status (active/inactive). The Activities page shows aggregate stats (total, today, this week, with contacts, with deals). No per-contact analytics exist.

**What is missing:** Detailed analytics for individual contacts: interaction frequency, response rates, copy effectiveness, engagement score, lifecycle stage progression.

**Why we need it:**
1. **Contact scoring / prioritization**: "Jane has responded to 80% of our emails and had 12 calls this quarter — she's highly engaged." vs. "Tom hasn't responded to anything in 6 months — he's cold." Without per-contact analytics, reps treat all contacts equally, wasting time on unresponsive ones.
2. **Engagement trends**: "Activity with Jane has been declining over the past 3 months." This trend signals a relationship at risk — the rep should proactively reach out. Without per-contact time-series data, this decline is invisible.
3. **Channel effectiveness**: "Jane responds best to emails (80% response rate) but rarely answers calls (10%)." This helps reps choose the right outreach channel for each contact, improving response rates.
4. **Copy personalization feedback**: "The persuasive-tone sales email got a response from Jane but the friendly-tone didn't." Per-contact copy analytics help refine AI-generated content over time.

**How to implement:**
- Backend: New `ContactAnalyticsService` that computes: activity count (total, by type, by period), response rates (if email tracking exists), average days between interactions, engagement score (composite metric). New endpoint: `GET /api/contacts/{id}/analytics`.
- Frontend: Analytics section on contact detail page (HP-1). Engagement score badge on contact cards. Sparkline showing activity trend over last 90 days.

---

#### LP-7. Contact Custom Fields

**Effort:** High (infrastructure) | **Impact:** High (for mature teams)

**What exists now:** All contacts share the same fixed fields: Name, Email, Phone, JobTitle, CompanyId, DoNotContact, PreferredContactMethod. There is no mechanism for adding user-defined fields.

**What is missing:** The ability for administrators to define custom fields per organization, with custom data types (text, number, date, dropdown, multi-select, URL, etc.).

**Why we need it:**
1. **Industry-specific data**: A healthcare CRM needs "NPI Number" and "Specialty." A real estate CRM needs "Property Interest" and "Budget Range." A SaaS CRM needs "Contract End Date" and "License Count." No fixed field set can satisfy all industries.
2. **Process-specific data**: One sales team tracks "Lead Source Detail" (specific campaign/event), another tracks "Buying Committee Role", another tracks "Annual IT Budget." These vary not just by industry but by individual sales process.
3. **Competitive requirement**: Custom fields are a fundamental CRM feature. Salesforce, HubSpot, Zoho, Pipedrive, and virtually every competitor supports them. Their absence limits the CRM's applicability to organizations with non-standard data needs.
4. **Avoid description field abuse**: Without custom fields, users stuff structured data into free-text description/notes fields ("Budget: $50K, Timeline: Q2, Decision Process: Board approval"). This data is unsearchable, unfilterable, and unreportable.

**How to implement:**
- Backend: New `CustomFieldDefinition` entity (Id, OrgId, EntityType, FieldName, FieldType, Options, IsRequired, DisplayOrder). New `CustomFieldValue` entity (Id, EntityId, FieldDefinitionId, TextValue, NumberValue, DateValue, etc.). API endpoints for CRUD on definitions and values. Dynamic validation based on field type.
- Frontend: Custom field management UI in Settings (define fields, set types, set options for dropdowns). Dynamic form rendering in contact create/edit based on defined fields. Custom field display on contact cards. Custom field filtering on Contacts page.

---

### D. UI/UX Inconsistencies

| # | Inconsistency | Severity | Details |
|---|---------------|----------|---------|
| 1 | **Dead form state** | LOW | `ContactFormState.description` in `contacts/types.ts` is unused — `Contacts.tsx` uses inline `{ name, email, phone, jobTitle, companyId }`. Dead code, not a user-facing bug. |
| 2 | **Archive vs Delete** | **HIGH** | Backend supports both, frontend only exposes hard delete. No recovery path for accidental deletion. Covered in HP-5. |
| 3 | **DoNotContact not enforced** | **CRITICAL** | Flag exists but is never checked before generating content or enrolling in sequences. Compliance risk. Covered in HP-4. |
| 4 | **Missing createdAtUtc** | **HIGH** | Breaks "This Week" stat and sorting. Covered in HP-3. |
| 5 | **No contact detail view** | **HIGH** | Unlike Deals (detail sheet), Contacts have no inline detail. Only grid cards and edit dialog. Covered in HP-1. |
| 6 | **Contact-Deal disconnect** | **CRITICAL** | Zero deal information on Contacts page. Covered in HP-2. |
| 7 | **SendToCrm unpaginated** | **MEDIUM** | `SendToCrm.tsx` loads ALL contacts via `getContacts()`. Won't scale past 1000+ contacts. |
| 8 | **Template `[First Name]` broken** | **MEDIUM** | Templates use `[First Name]` but Contact entity has `Name` (full name). No first-name extraction logic exists. Placeholder is never replaced. |
| 9 | **Lead conversion silent reuse** | **LOW** | If a contact with matching email exists during lead conversion, it's silently reused instead of creating new. No UI feedback. |

---

### E. Performance & Scalability Concerns

| # | Area | Issue | Impact | Recommendation |
|---|------|-------|--------|----------------|
| 1 | **LastActivityAtUtc enrichment** | Every contact fetch queries Activities table per contact. N contacts × M activities = expensive at scale. | Slow page loads with 500+ contacts. | Materialize `LastActivityAtUtc` on Contact table, update via event/trigger. |
| 2 | **SendToCrm unpaginated** | `getContacts()` loads ALL contacts. No lazy loading. | Memory issues at 1000+ contacts. | Use `getContactsPaged` with debounced search. |
| 3 | **GlobalSearch parallel queries** | 4 parallel queries per keystroke with no backend debounce. | DB load under rapid typing. | Response caching or unified search index. |
| 4 | **No server-side sorting** | Pagination is server-side but sorting is client-side. | Sort results are incorrect across pages. | Add `sortBy`/`sortDirection` API params. |

---

### F. Implementation Priority Order

| Phase | Items | Effort | What You Get | Status |
|-------|-------|--------|-------------|--------|
| **Phase 1: Bug Fixes** | HP-3 (timestamps), HP-4 (DoNotContact), HP-5 (archive UI) | 1-2 days | Sorting works. Stats work. Compliance enforced. Archive available. | ✅ HP-3 Done, ⚠️ HP-5 Partial, ❌ HP-4 Pending |
| **Phase 2: Core Relationships** | HP-2 (deal visibility), HP-6 (activity timeline), HP-7 (notes) | 2-3 days | Sales context on contacts. Interaction history visible. Notes field works. | ✅ HP-2 Done, ✅ HP-6 Done, ❌ HP-7 Pending |
| **Phase 3: Detail Page** | HP-1 (contact detail page) | 2-3 days | Deep-linkable contacts. 360° view. All relationships in one place. | ✅ Complete |
| **Phase 4: Workflows** | MP-1 to MP-7 | 3-5 days | Task visibility. Sequence enrollment. Import/export. Pre-validation. | ❌ Not Started |

**Total estimated effort for remaining HIGH PRIORITY items: 2-3 developer days (HP-4, HP-7, HP-5 completion).**

---

*This document provides a complete source-verified reference for every contact interaction in the Cadence CRM codebase (backend and frontend). All gaps prioritized with detailed explanations, business justifications, and implementation guidance for all priority tiers (HIGH, MEDIUM, and LOW). Last updated: February 9, 2026 (third pass — LOW PRIORITY section expanded with full "why we need it" explanations and implementation plans).*
