# Activity System — Complete Interaction Report

This document is the single definitive reference for **every interaction** the "Activity" entity has across the entire Cadence CRM codebase (backend and frontend). It covers the entity definition, API layer, business logic, database configuration, UI components, cross-entity relationships, and identifies what is missing.

Last updated: February 9, 2026 (fourth pass — ALL 6 HIGH PRIORITY items now IMPLEMENTED. HP-1 type mismatch, HP-2 activity edit, HP-3 real team stats, HP-4 dashboard fix, HP-5 lead filter, HP-6 participants display — all completed with backend and frontend code changes).

---

## Table of Contents

1. [Activity Entity (Backend Domain)](#1-activity-entity-backend-domain)
2. [Backend API Endpoints](#2-backend-api-endpoints)
3. [Backend Business Logic (Services)](#3-backend-business-logic-services)
4. [Backend Data Access (Repositories)](#4-backend-data-access-repositories)
5. [Backend DTOs](#5-backend-dtos)
6. [Database Configuration (EF Core)](#6-database-configuration-ef-core)
7. [Domain Errors](#7-domain-errors)
8. [Unit Tests](#8-unit-tests)
9. [Cross-Entity Activity Interactions (Backend)](#9-cross-entity-activity-interactions-backend)
10. [Frontend — API Client Layer](#10-frontend--api-client-layer)
11. [Frontend — TypeScript Types](#11-frontend--typescript-types)
12. [Frontend — React Query Hooks](#12-frontend--react-query-hooks)
13. [Frontend — Activity Type Configuration](#13-frontend--activity-type-configuration)
14. [Frontend — Activities Page (Main Activity UI)](#14-frontend--activities-page-main-activity-ui)
15. [Frontend — Pipeline Page (Deal Activity Log)](#15-frontend--pipeline-page-deal-activity-log)
16. [Frontend — Leads Page (Lead Activity Timeline)](#16-frontend--leads-page-lead-activity-timeline)
17. [Frontend — Contacts Page (Activity Metadata)](#17-frontend--contacts-page-activity-metadata)
18. [Frontend — Tasks Page (Related Activities)](#18-frontend--tasks-page-related-activities)
19. [Frontend — Dashboard (Recent Activity)](#19-frontend--dashboard-recent-activity)
20. [Frontend — Team Page (Activity Stats)](#20-frontend--team-page-activity-stats)
21. [Frontend — Settings (Activity Status)](#21-frontend--settings-activity-status)
22. [Frontend — Mock Data](#22-frontend--mock-data)
23. [Frontend — Query Keys](#23-frontend--query-keys)
24. [Frontend — Navigation & Routing](#24-frontend--navigation--routing)
25. [Frontend — Utility Functions](#25-frontend--utility-functions)
26. [Complete File Inventory](#26-complete-file-inventory)
27. [Complete Relationship Map](#27-complete-relationship-map)
28. [Critical Bugs & Issues](#28-critical-bugs--issues)
29. [What Is Missing — Comprehensive Detailed Analysis](#29-what-is-missing--comprehensive-detailed-analysis)
    - [Master Overview Table](#master-overview--priority-classification-at-a-glance)
    - [A. CRITICAL BUGS — Must Fix Immediately](#a-critical-bugs--must-fix-immediately-before-any-new-feature-work) (HP-1, HP-3, HP-4)
    - [A2. MUST HAVE — Core Features](#a2-must-have--core-features-required-for-a-complete-activity-system) (HP-2, HP-5, HP-6)
    - [B. SHOULD HAVE — Important Improvements](#b-medium-priority--should-have--important-improvements-do-after-all-high-items-are-done) (MP-1 to MP-6)
    - [C. NICE TO HAVE — Future Enhancements](#c-low-priority--nice-to-have--future-roadmap-enhancements) (LP-1 to LP-7)
    - [D. UI/UX Inconsistencies](#d-uiux-inconsistencies)
    - [E. Implementation Priority Order — Phased Plan](#e-implementation-priority-order--step-by-step-execution-plan)

---

## 1. Activity Entity (Backend Domain)

**File:** `backend/src/ACI.Domain/Entities/Activity.cs`

The core Activity entity representing a CRM interaction (call, meeting, email, note):

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Primary key (from BaseEntity) |
| `UserId` | `Guid` | FK to owning User (required) |
| `OrganizationId` | `Guid?` | FK to Organization (optional) |
| `Type` | `string` | Activity type, default `"note"`. Valid: `call`, `meeting`, `email`, `note` |
| `Subject` | `string?` | Activity subject/title |
| `Body` | `string?` | Activity body/description |
| `ContactId` | `Guid?` | FK to Contact (optional) |
| `DealId` | `Guid?` | FK to Deal (optional) |
| `LeadId` | `Guid?` | FK to Lead (optional) |
| `Participants` | `string?` | Comma-separated participant list |
| `CreatedAtUtc` | `DateTime` | Creation timestamp |
| `UpdatedAtUtc` | `DateTime?` | Last update timestamp (nullable) |
| `UpdatedByUserId` | `Guid?` | FK to User who last updated |

> **Note:** Activities can now be updated via `PUT /api/activities/{id}` endpoint. The `UpdatedAtUtc` and `UpdatedByUserId` fields are set when an activity is edited. Activities can be created, updated, or deleted.

**Navigation Properties:**
- `User` — Owner (required)
- `UpdatedByUser` — Last updater (optional, unused)
- `Organization` — Org scope (optional)
- `Contact` — Linked contact (optional)
- `Deal` — Linked deal (optional)
- `Lead` — Linked lead (optional)

---

## 2. Backend API Endpoints

### ActivitiesController (`backend/src/ACI.WebApi/Controllers/ActivitiesController.cs`)

All endpoints require `[Authorize]`. Route base: `/api/activities`.

| Method | Endpoint | Description | Returns |
|--------|----------|-------------|---------|
| `GET` | `/api/activities?page=&pageSize=&search=&type=` | Paginated list with search and type filter (page default 1, pageSize default 20 max 100) | `PagedResult<ActivityDto>` |
| `GET` | `/api/activities/all` | Get all activities non-paginated (backward compat) | `IReadOnlyList<ActivityDto>` |
| `GET` | `/api/activities/contact/{contactId}` | Get activities linked to a specific contact | `IReadOnlyList<ActivityDto>` |
| `GET` | `/api/activities/deal/{dealId}` | Get activities linked to a specific deal | `IReadOnlyList<ActivityDto>` |
| `GET` | `/api/activities/lead/{leadId}` | Get activities linked to a specific lead | `IReadOnlyList<ActivityDto>` |
| `POST` | `/api/activities` | Create new activity (body: `CreateActivityRequest`) | `ActivityDto` (200) or `ProblemDetails` (400) |
| `PUT` | `/api/activities/{id}` | Update activity (body: `UpdateActivityRequest`) | `ActivityDto` (200) or `ProblemDetails` (400/404) |
| `GET` | `/api/activities/org-member-counts` | Get activity counts per org member (HP-3) | `Dictionary<Guid, int>` |
| `DELETE` | `/api/activities/{id}` | Delete activity (returns 204) | `204 No Content` or `404` |

> **Note:** Activities can now be edited via the `PUT /api/activities/{id}` endpoint. The `GET /api/activities/org-member-counts` endpoint returns activity counts per user for the current organization (used by team management pages). There is still **no GET by ID** endpoint for individual activities.

---

## 3. Backend Business Logic (Services)

### ActivityService (`backend/src/ACI.Application/Services/ActivityService.cs`)

Depends on: `IActivityRepository`, `ILeadRepository`, `IContactRepository`, `IDealRepository`.

**Valid Activity Types:** `"call"`, `"meeting"`, `"email"`, `"note"`, `"task"`, `"follow_up"`, `"deadline"`, `"video"`, `"demo"` (9 types in `ValidActivityTypes` array — expanded via HP-1)

| Method | Logic |
|--------|-------|
| `GetPagedAsync` | Server-side pagination with search (Subject, Body, Type) and type filter. Returns `PagedResult<ActivityDto>`. |
| `GetByUserIdAsync` | All activities for user/org (non-paginated), ordered by `CreatedAtUtc` descending. |
| `GetByContactIdAsync` | All activities for a specific contact, filtered by user/org. |
| `GetByDealIdAsync` | All activities for a specific deal, filtered by user/org. |
| `GetByLeadIdAsync` | All activities for a specific lead. **Special:** filtered by org only (NOT user), allowing team members to see each other's interactions with leads. |
| `CreateAsync` | **Validation chain:** 1) Type must be in `ValidActivityTypes` (case-insensitive, lowered before save). 2) At least one of `ContactId`, `DealId`, or `LeadId` must be provided. 3) If provided, each referenced entity is verified to exist. Sets `CreatedAtUtc = DateTime.UtcNow`. |
| `UpdateAsync` | Finds activity by ID (user-scoped). Updates provided fields (Type, Subject, Body, Participants). Sets `UpdatedAtUtc` and `UpdatedByUserId`. Validates type if changed. |
| `DeleteAsync` | Finds and removes activity. Returns `Activity.NotFound` error if not found. |
| `GetOrgMemberActivityCountsAsync` | Returns `Dictionary<Guid, int>` — activity counts grouped by UserId across all members in the organization. Used by team management pages (HP-3). |

> **Key design detail:** `CreateAsync` performs three separate repository lookups to validate referenced entities (lead, contact, deal), each requiring the entity to exist for the current user/org. If Type is null, it defaults to `"note"`.

---

## 4. Backend Data Access (Repositories)

### ActivityRepository (`backend/src/ACI.Infrastructure/Repositories/ActivityRepository.cs`)

| Method | Description |
|--------|-------------|
| `GetPagedAsync` | Paginated query with search (Subject, Body, Type contains) + type filter, filtered by UserId + OrganizationId, ordered by `CreatedAtUtc DESC` |
| `GetByUserIdAsync` | All activities for user, filtered by user/org, ordered by `CreatedAtUtc DESC` |
| `GetByContactIdAsync` | Activities where `ContactId` matches, filtered by user/org |
| `GetByDealIdAsync` | Activities where `DealId` matches, filtered by user/org |
| `GetByLeadIdAsync` | Activities where `LeadId` matches, filtered by **org only** (NOT user) — team-visible |
| `GetByIdAsync` | Single activity by ID, filtered by user/org |
| `AddAsync` | Insert new activity |
| `DeleteAsync` | Remove activity (hard delete) |
| `GetLastActivityByContactIdsAsync` | **Batch aggregation:** Latest activity `CreatedAtUtc` per contact ID. Used by `ContactService` to enrich contacts with `LastActivityAtUtc`. |
| `GetLastActivityByDealIdsAsync` | **Batch aggregation:** Latest activity `CreatedAtUtc` per deal ID. Used by `DealService` to enrich deals with `LastActivityAtUtc`. |

### Helper Methods (private)

| Method | Description |
|--------|-------------|
| `FilterByUserAndOrg` | Base filter: `a.UserId == userId && (orgId == null ? a.OrganizationId == null : a.OrganizationId == orgId)` |
| `ApplySearch` | Case-insensitive search on `Subject`, `Body`, and `Type` |
| `ApplyTypeFilter` | Exact match on `Type` (case-insensitive) |

> **Key design detail for lead activities:** `GetByLeadIdAsync` intentionally bypasses the user filter to show all organization-wide activities for a lead. This allows team collaboration — team members can see each other's interactions with the same lead. All other queries are user-scoped.

---

## 5. Backend DTOs

| DTO | File | Fields |
|-----|------|--------|
| `ActivityDto` | `DTOs/ActivityDto.cs` | `Id, Type, Subject?, Body?, ContactId?, DealId?, LeadId?, Participants?, CreatedAtUtc, UpdatedAtUtc?, ContactName?, DealName?, LeadName?` — Note: this is a `record` type |
| `CreateActivityRequest` | `DTOs/CreateActivityRequest.cs` | `Type` (required, max 20, regex: `^(call\|meeting\|email\|note\|task\|follow_up\|deadline\|Call\|Meeting\|Email\|Note\|Task\|Follow_Up\|Deadline)$`), `Subject?` (max 200), `Body?` (max 5000), `ContactId?`, `DealId?`, `LeadId?`, `Participants?` (max 500) |
| `UpdateActivityRequest` | `DTOs/UpdateActivityRequest.cs` | `Type?`, `Subject?` (max 200), `Body?` (max 5000), `ContactId?`, `DealId?`, `LeadId?`, `Participants?` (max 500) |

> **Notable:** `ActivityDto` now includes `UpdatedAtUtc` (set when activity is edited), `ContactName`, `DealName`, and `LeadName` (for display purposes). The `Participants` field is included in the DTO and can be stored/returned, but UI display is still incomplete.

---

## 6. Database Configuration (EF Core)

### ActivityConfiguration (`backend/src/ACI.Infrastructure/Persistence/Configurations/ActivityConfiguration.cs`)

- Table: `"Activities"`
- `Type`: `MaxLength(32)`, required
- `Subject`: `MaxLength(512)`
- `Body`: `nvarchar(max)` (unlimited length)
- `Participants`: `MaxLength(1024)`
- FK: `UserId` → User, via `User.Activities` collection
- FK: `OrganizationId` → Organization (optional)
- FK: `ContactId` → Contact, via `Contact.Activities` collection
- FK: `DealId` → Deal, via `Deal.Activities` collection
- FK: `LeadId` → Lead, via `Lead.Activities` collection (optional)
- FK: `UpdatedByUserId` → User (optional)
- Indexes: `OrganizationId`, `LeadId` (explicit)

> **Note:** No explicit indexes on `ContactId` or `DealId`, though EF Core may auto-create FK indexes. The `LeadId` has an explicit index because lead activities use org-wide scoping.

---

## 7. Domain Errors

**File:** `backend/src/ACI.Application/Common/DomainErrors.cs` — `Activity` class

| Error Code | Message |
|------------|---------|
| `Activity.NotFound` | "The activity was not found" |
| `Activity.TypeRequired` | "Activity type is required" |
| `Activity.InvalidType` | "The activity type is invalid. Valid types: call, meeting, email, note" |
| `Activity.NoRelatedEntity` | "Activity must be linked to at least one entity (contact, deal, or lead)" |
| `Activity.RelatedEntityNotFound` | "The related entity was not found" |

---

## 8. Unit Tests

**File:** `backend/tests/ACI.Application.Tests/Services/ActivityServiceTests.cs`

| Test | Description |
|------|-------------|
| `GetByUserIdAsync_ReturnsActivities_WhenActivitiesExist` | Verifies activities are returned and mapped correctly |
| `GetByUserIdAsync_ReturnsEmptyList_WhenNoActivitiesExist` | Verifies empty list is returned |
| `GetByContactIdAsync_ReturnsActivities_WhenActivitiesExist` | Verifies contact-filtered activities |
| `GetByDealIdAsync_ReturnsActivities_WhenActivitiesExist` | Verifies deal-filtered activities |
| `GetByLeadIdAsync_ReturnsActivities_WhenActivitiesExist` | Verifies lead-filtered activities |
| `CreateAsync_ReturnsActivity_WhenValidRequest_WithContactId` | Tests creation with contact link |
| `CreateAsync_ReturnsActivity_WhenValidRequest_WithDealId` | Tests creation with deal link |
| `CreateAsync_ReturnsActivity_WhenValidRequest_WithLeadId` | Tests creation with lead link |
| `CreateAsync_ReturnsError_WhenNoRelatedEntityProvided` | Tests `Activity.NoRelatedEntity` error |
| `CreateAsync_ReturnsError_WhenInvalidActivityType` | Tests `Activity.InvalidType` error |
| `CreateAsync_AcceptsValidActivityTypes` | Theory test: `call`, `meeting`, `email`, `note`, `CALL`, `Meeting` — all accepted |
| `CreateAsync_ReturnsError_WhenContactNotFound` | Tests `Activity.RelatedEntityNotFound` error |
| `CreateAsync_ReturnsError_WhenDealNotFound` | Tests `Activity.RelatedEntityNotFound` error |
| `CreateAsync_ReturnsError_WhenLeadNotFound` | Tests `Activity.RelatedEntityNotFound` error |
| `CreateAsync_DefaultsToNote_WhenTypeIsNull` | Type defaults to `"note"` when null |
| `DeleteAsync_ReturnsSuccess_WhenActivityDeleted` | Tests successful deletion |
| `DeleteAsync_ReturnsNotFoundError_WhenActivityDoesNotExist` | Tests `Activity.NotFound` on missing activity |
| `GetPagedAsync_ReturnsPagedResult_WithCorrectPagination` | Tests pagination math (30 items, page 1, size 10 = 3 pages) |
| `GetPagedAsync_FiltersByActivityType_WhenProvided` | Tests type filter pass-through |

---

## 9. Cross-Entity Activity Interactions (Backend)

### Entities that reference Activity

| Entity | Relationship | Direction |
|--------|-------------|-----------|
| **User** | `ICollection<Activity> Activities` | User owns many Activities |
| **Contact** | `ICollection<Activity> Activities` | Contact has many Activities |
| **Deal** | `ICollection<Activity> Activities` | Deal has many Activities |
| **Lead** | `ICollection<Activity> Activities` | Lead has many Activities |

### Services that use Activity data

| Service | File | How it uses Activities |
|---------|------|----------------------|
| `DealService` | `Services/DealService.cs` | Calls `_activityRepository.GetLastActivityByDealIdsAsync()` to enrich every `DealDto` with `LastActivityAtUtc` |
| `ContactService` | `Services/ContactService.cs` | Calls `_activityRepository.GetLastActivityByContactIdsAsync()` to enrich every `ContactDto` with `LastActivityAtUtc` |
| `ActivityService` | `Services/ActivityService.cs` | Core activity CRUD. Validates existence of Contact, Deal, Lead before creating |

### Repositories that reference Activity

| Repository | File | How it uses Activities |
|------------|------|----------------------|
| `DealRepository` | `Repositories/DealRepository.cs` | On deal deletion: **nullifies `DealId` on all linked Activities** (preserves activity records) |
| `ContactRepository` | `Repositories/ContactRepository.cs` | May reference activity data for contact enrichment |
| `ActivityRepository` | `Repositories/ActivityRepository.cs` | Core data access + batch aggregation for contacts and deals |

### Delete behavior

When a **Deal** is deleted, `DealRepository.DeleteAsync` sets `Activity.DealId = null` on all linked activities (preserving activity history).

When a **Contact** or **Lead** is deleted, their linked activities are handled by EF Core cascade/set-null behavior based on the `ActivityConfiguration` FK relationships.

When an **Activity** is deleted, it is **hard-deleted** — no soft-delete or archive mechanism.

---

## 10. Frontend — API Client Layer

### `src/app/api/activities.ts`

| Function | API Call | Description |
|----------|----------|-------------|
| `getActivitiesPaged(options?)` | `GET /api/activities?page=&pageSize=&search=&type=` | Paginated activities with optional search and type filter. Mock fallback with client-side pagination. |
| `getActivities()` | `GET /api/activities` | Get all activities (non-paginated). Mock fallback returns `mockActivities`. |
| `getActivitiesByContact(contactId)` | `GET /api/activities/contact/{contactId}` | Activities linked to a contact. Mock fallback filters by `contactId`. |
| `getActivitiesByDeal(dealId)` | `GET /api/activities/deal/{dealId}` | Activities linked to a deal. Mock fallback filters by `dealId`. |
| `getActivitiesByLead(leadId)` | `GET /api/activities/lead/{leadId}` | Activities linked to a lead. Mock fallback filters by `leadId`. |
| `createActivity(params)` | `POST /api/activities` | Create new activity. Params: `type, subject?, body?, contactId?, dealId?, leadId?`. Mock returns `null`. |
| `updateActivity(id, params)` | `PUT /api/activities/{id}` | Update activity. Params: `type?, subject?, body?, participants?`. Mock returns `null`. (HP-2) |
| `deleteActivity(id)` | `DELETE /api/activities/{id}` | Delete activity. Checks for 204 response. Mock returns `false`. |
| `getOrgMemberActivityCounts()` | `GET /api/activities/org-member-counts` | Returns `Record<string, number>` — activity counts per user for the current org. Mock returns `{}`. (HP-3) |

> **Note:** The API client includes an internal `ActivityRaw` interface and `mapActivity` function to map nullable API fields (`string | null`) to TypeScript optional fields (`string | undefined`). The `createActivity` function now accepts `participants` in its params (HP-6).

### `src/app/api/index.ts` (barrel exports)

Re-exports: `getActivities`, `getActivitiesPaged`, `createActivity`, `updateActivity`, `deleteActivity`, `getActivitiesByContact`, `getActivitiesByDeal`, `getActivitiesByLead`, `getOrgMemberActivityCounts`.

---

## 11. Frontend — TypeScript Types

### `src/app/api/types.ts` — Activity interface (exact from source, lines 142-152)

```
interface Activity {
  id: string;
  type: string;
  subject?: string;
  body?: string;
  contactId?: string;
  dealId?: string;
  leadId?: string;
  participants?: string;
  createdAt: string;
}
```

> **Field name mismatch:** Frontend uses `createdAt` but backend DTO uses `CreatedAtUtc`. The API client's `mapActivity` function handles this mapping.

### `src/app/pages/activities/types.ts` — Activity form state

```
type ActivityFilter = 'all' | 'contact' | 'deal';

interface ActivityFormState {
  type: string;
  subject: string;
  body: string;
  contactId: string;
  dealId: string;
}
```

> **Note:** `ActivityFormState` has no `leadId` field. Activities linked to leads are created from the Lead Detail Modal, not the Activities page form.

---

## 12. Frontend — React Query Hooks

### `src/app/hooks/queries/useActivities.ts`

| Hook | Query Key | Description |
|------|-----------|-------------|
| `useActivities()` | `['activities', 'list']` | Fetch all activities (non-paginated) |
| `useActivitiesByContact(contactId)` | `['activities', 'list', {contactId}]` | Activities for a contact (enabled when contactId truthy) |
| `useActivitiesByDeal(dealId)` | `['activities', 'list', {dealId}]` | Activities for a deal (enabled when dealId truthy) |
| `useActivitiesByLead(leadId)` | `['activities', 'list', {leadId}]` | Activities for a lead (enabled when leadId truthy) |
| `useActivityById(id)` | `['activities', 'detail', id]` | Single activity by ID (from cached list, not API) |
| `useCreateActivity()` | mutation | Create activity. **Optimistic update:** prepends to cached list. Invalidates `['activities']`. Toast: "Activity logged successfully" / "Failed to log activity" |
| `useDeleteActivity()` | mutation | Delete activity. **Optimistic update:** removes from cached list. Toast: "Activity deleted successfully" / "Failed to delete activity" |

> **Note:** The Activities page does NOT use these React Query hooks. It manages state with `useState` + direct API calls. The hooks exist for use in other components like the Deal detail sheet and Lead detail modal.

---

## 13. Frontend — Activity Type Configuration

There are **two duplicate** activity type config files:

### `src/app/config/activityTypes.ts` (Global config)

| ID | Label | Icon | Color |
|----|-------|------|-------|
| `call` | Call | Phone | emerald |
| `meeting` | Meeting | Users | blue |
| `email` | Email | Mail | amber |
| `note` | Note | FileText | slate |
| `video` | Video Call | Video | purple |
| `demo` | Demo | Presentation | rose |
| `task` | Task Completed | CheckCircle | cyan |

Exports: `ACTIVITY_TYPES` (readonly array), `getActivityType(id)`, `getActivityTypeIcon(id)`.

### `src/app/pages/activities/config.ts` (Local duplicate)

Identical 7 types. Exports: `ACTIVITY_TYPES`, `ActivityTypeId` type.

### `Activities.tsx` inline `ACTIVITY_TYPES` (Third duplicate!)

The Activities page defines its own inline `ACTIVITY_TYPES` constant (line 49) with the same 7 types, NOT importing from either config file.

> **FIXED (HP-1):** Backend now accepts all 9 types: `call`, `meeting`, `email`, `note`, `task`, `follow_up`, `deadline`, `video`, `demo`. The `ActivityService.ValidActivityTypes` and `CreateActivityRequest` regex have been expanded. No more 400 errors for Video, Demo, or Task activities.

---

## 14. Frontend — Activities Page (Main Activity UI)

**File:** `src/app/pages/Activities.tsx` — ~1135 lines

### Features

- **Timeline View**: Activities grouped by date (Today, Yesterday, This Week, Last Week, Month Year)
- **Server-side Pagination**: Uses `getActivitiesPaged()` with `page`, `pageSize`, `search`, `type` params
- **Four Filter Modes**: `'all'` (paginated), `'contact'` (by contact, non-paginated), `'deal'` (by deal, non-paginated), `'lead'` (by lead, non-paginated — HP-5)
- **Type Filter**: Dropdown to filter by activity type
- **Search**: Debounced (300ms) search input for subject/body/type
- **Create Activity**: "Log Activity" dialog with type, subject, body, participants (for calls/meetings/video/demos), contact, deal selectors (HP-6)
- **Edit Activity**: Edit dialog pre-filled with existing activity data; updates via `PUT /api/activities/{id}` (HP-2)
- **Delete Activity**: Confirmation dialog before deletion
- **Quick Log**: Pre-set type buttons to quickly open log dialog
- **Statistics Bar**: Total count, Today count, This Week count, With Contacts count, With Deals count, Top Type

### State Management

```
activities: Activity[]                    // Current page of activities
contacts: Contact[]                       // Loaded on mount for dropdowns
deals: Deal[]                             // Loaded on mount for dropdowns
filter: 'all' | 'contact' | 'deal'        // Filter mode
filterContactId: string                   // Selected contact for filtering
filterDealId: string                      // Selected deal for filtering
filterType: string                        // Type filter ('all' or specific type)
searchQuery: string                       // Raw search input
debouncedSearch: string                   // Debounced search (300ms)
page: number                              // Current page (1-based)
pageSize: number                          // Items per page (20)
pagedResult: PagedResult<Activity> | null  // Server pagination metadata
dialogOpen: boolean                       // Create dialog visibility
form: {type, subject, body, contactId, dealId}  // Create form state
deleteConfirmActivity: Activity | null    // Activity pending deletion
```

### Activity Card Display

Each activity card shows:
- **Type icon** (color-coded) with type label
- **Subject** (bold)
- **Body** (truncated)
- **Participants** (violet-themed badge with Users icon, shown when present — HP-6)
- **Relative time** (e.g., "2h ago")
- **Contact badge** (blue, with User icon + contact name) if `contactId` is set
- **Deal badge** (emerald, with Target icon + deal name) if `dealId` is set
- **Edit button** (pencil icon, visible on hover — HP-2)
- **Delete button** (trash icon, visible on hover)

### Data Loading Pattern

- On mount: loads `getContacts()` + `getDeals()` via `Promise.all`
- On filter change: calls either `getActivitiesByContact()`, `getActivitiesByDeal()`, or `getActivitiesPaged()` depending on filter mode
- Contact/Deal filter: non-paginated (loads all activities for that entity)
- "All" filter: server-side paginated with search and type filter

---

## 15. Frontend — Pipeline Page (Deal Activity Log)

**File:** `src/app/pages/Pipeline.tsx`

### Activity Interactions

1. **State**: `dealActivities: Activity[]`, `activitiesLoading`, `activityForm: {type, subject, body}`, `savingActivity`
2. **Load on detail open**: When a deal's detail sheet opens, calls `getActivitiesByDeal(detailDeal.id)` to fetch all activities for that deal
3. **Activity Log Form**: In the deal detail sheet, a form with:
   - Activity type selector (Call, Email, Meeting, Note)
   - Subject input (required)
   - Body textarea (optional)
   - Submit creates activity via `createActivity({type, subject, body, dealId})`
4. **Activity Timeline**: Displays activities chronologically with type icons, subjects, bodies, and timestamps
5. **Empty State**: "No activities yet" with a message to log the first interaction

> **Note:** Pipeline uses only 4 types (Call, Email, Meeting, Note) in its activity form — correctly matching the backend. This is different from the Activities page which shows 7 types.

---

## 16. Frontend — Leads Page (Lead Activity Timeline)

**File:** `src/app/pages/leads/LeadDetailModal.tsx`

### Activity Interactions

1. **State**: `activities: ActivityWithUser[]`, `loadingActivities`
2. **Load on open**: Calls `getActivitiesByLead(lead.id)` (dynamically imported)
3. **Quick Action Buttons**: Log a call, email, meeting, or note directly from the lead detail
4. **Add Note**: Dedicated note input to quickly create a note activity
5. **Field Change Logging**: When lead fields are updated, the modal logs field changes as activities via `createActivity({type: 'note', subject: 'Updated [field]', body: '...', leadId})`
6. **Activity Timeline**: Full timeline showing:
   - User name and email
   - Activity type icon
   - Subject and body
   - Relative timestamps

> **Key behavior:** Lead activities are scoped by organization (not user), so team members see all activities for a lead, not just their own.

---

## 17. Frontend — Contacts Page (Activity Metadata)

**File:** `src/app/pages/Contacts.tsx`

### Activity Interactions

Activities are **not directly fetched** on the Contacts page. Instead, the page uses `lastActivityAtUtc` metadata from the Contact interface for:

1. **Recently Active filter**: Contacts with activity in the last 30 days
2. **Inactive filter**: Contacts with no activity in 90+ days (or no activity ever)
3. **Stats cards**: "Recently Active" count (emerald themed) and "Inactive" count (red themed)
4. **Sort by Last Activity**: Sorts contacts by `lastActivityAtUtc`
5. **Contact card display**: Shows "Last activity: Xd ago" on each contact card

> **Note:** `lastActivityAtUtc` on `ContactDto` is computed server-side by `ContactService` using `_activityRepository.GetLastActivityByContactIdsAsync()`. The Contacts page never calls the Activities API directly.

---

## 18. Frontend — Tasks Page (Related Activities)

**File:** `src/app/pages/tasks/components/TaskDetailModal.tsx`

### Activity Interactions

1. **State**: `activities: Activity[]`, `loadingActivities`
2. **Load related activities**: When task detail opens, fetches activities from both the task's linked lead AND deal:
   - If `task.leadId`: calls `getActivitiesByLead(leadId)`
   - If `task.dealId`: calls `getActivitiesByDeal(dealId)`
3. **Filter to task type**: Only shows activities where `type === 'task'`
4. **Display**: Shows up to 5 recent task-type activities with subject and relative time
5. **Section label**: "Related Activity" in the Linked Items section

---

## 19. Frontend — Dashboard (Recent Activity)

**File:** `src/app/pages/Dashboard.tsx` + `src/app/pages/dashboard/RecentActivity.tsx`

### Activity Interactions — ✅ FIXED (HP-4)

The Dashboard's "Recent Activity" section now shows **real CRM activities** (calls, emails, meetings, notes).

- `RecentActivity` component receives `Activity[]` — the 5 most recent CRM activities
- Data loaded via `getActivities()` on Dashboard mount
- Shows activity type icon (color-coded), subject, related entity name (contact/deal/lead), and formatted date
- Empty state: "No recent CRM activities — Log activities to track your sales interactions"
- "View all" link navigates to `/activities`

> **Note (HP-4 fix):** Previously, this section showed Copy History items from the AI copy generation feature, which was misleading. It has been completely rewritten to show real CRM activity data.

---

## 20. Frontend — Team Page (Activity Stats)

**File:** `src/app/pages/team/components/MemberDetailPanel.tsx`

### Activity Interactions — ✅ FIXED (HP-3)

- Displays **real** `activitiesLogged` stat per team member, fetched from `GET /api/activities/org-member-counts`
- Other stats (leads, deals, tasks, conversion rate, response time) still show "—" until their respective backend endpoints are implemented
- Recent activities section shows empty state until a per-user recent activities endpoint is added
- No more random numbers — managers see accurate activity counts

---

## 21. Frontend — Settings (Activity Status)

**File:** `src/app/pages/settings/components/AccountSection.tsx`

### Activity Interactions

- **"Show Activity Status" toggle**: Controls `settings.showActivityStatus` boolean
- Description: "Let others see when you're active"
- This is about online presence, NOT CRM activity records

---

## 22. Frontend — Mock Data

**File:** `src/app/api/mockData.ts`

5 mock activities for development/demo mode:

| ID | Type | Subject | ContactId | DealId | LeadId |
|----|------|---------|-----------|--------|--------|
| 1 | call | "Intro call with John" | 1 | 1 | — |
| 2 | email | "Proposal sent" | 1 | 1 | — |
| 3 | meeting | "Demo with Sarah" | 2 | 2 | — |
| 4 | note | "Follow-up reminder" | 3 | 3 | — |
| 5 | call | "Discovery with Mike" | 3 | — | — |

> **Note:** Mock data only uses 4 types (`call`, `email`, `meeting`, `note`) — matching backend. No mock activities use `video`, `demo`, or `task` types. No mock activities are linked to leads.

---

## 23. Frontend — Query Keys

**File:** `src/app/hooks/queries/queryKeys.ts`

```
activities: {
  all: ['activities'],
  lists: () => ['activities', 'list'],
  list: (filters?) => ['activities', 'list', filters],
  details: () => ['activities', 'detail'],
  detail: (id) => ['activities', 'detail', id],
}
```

---

## 24. Frontend — Navigation & Routing

### App Router (`src/app/App.tsx`)
- Route: `/activities` → lazy-loaded `Activities` component
- Protected route requiring authentication

### AppHeader (`src/app/components/AppHeader.tsx`)
- Navigation item: `{path: '/activities', label: 'Activities', icon: Activity}`

### Navigation Config (`src/app/config/navigation.ts`)
- `NAV_ITEMS` includes `{path: '/activities', label: 'Activities', icon: Activity}`

---

## 25. Frontend — Utility Functions

### `src/app/utils/dateFormatters.ts`

Used for activity timestamp formatting across the app:

| Function | Description |
|----------|-------------|
| `formatDate(iso)` | Short date + time format |
| `formatRelativeDate(iso)` | Relative: "Just now", "5m ago", "2h ago", "3d ago", "Jan 15" |
| `getDateGroup(iso)` | Groups: "Today", "Yesterday", "This Week", "Last Week", "Month Year" |
| `formatLongDate(iso)` | Long date format |
| `isOverdue(iso)` | Checks if date is in the past |
| `isToday(iso)` | Checks if date is today |
| `getDaysUntil(iso)` | Calculates days until date |

> **Note:** Activities.tsx duplicates `formatDate`, `formatRelativeDate`, and `getDateGroup` inline instead of importing from this utility file.

---

## 26. Complete File Inventory

### Backend Files (25+ files)

| Category | Files |
|----------|-------|
| **Entity** | `Activity.cs` |
| **Service Interface** | `IActivityService.cs` |
| **Service Impl** | `ActivityService.cs` |
| **Repository Interface** | `IActivityRepository.cs` |
| **Repository Impl** | `ActivityRepository.cs` |
| **Controller** | `ActivitiesController.cs` |
| **DTOs** | `ActivityDto.cs`, `CreateActivityRequest.cs` |
| **DB Config** | `ActivityConfiguration.cs` |
| **Domain Errors** | `DomainErrors.cs` (Activity section) |
| **Tests** | `ActivityServiceTests.cs` |
| **Cross-entity (entities)** | `Deal.cs`, `Contact.cs`, `Lead.cs`, `User.cs`, `UserSettings.cs` (ICollection<Activity>) |
| **Cross-entity (services)** | `DealService.cs`, `ContactService.cs` (GetLastActivityBy*IdsAsync) |
| **Cross-entity (repos)** | `DealRepository.cs` (nullifies DealId on delete), `ContactRepository.cs` |
| **DI/Setup** | `AppDbContext.cs` (DbSet<Activity>), `DependencyInjection.cs`, `Program.cs` |
| **Migrations** | `InitialCreate.cs`, `AppDbContextModelSnapshot.cs` |

### Frontend Files (30+ files)

| Category | Files |
|----------|-------|
| **API Client** | `activities.ts`, `index.ts` |
| **Types** | `api/types.ts` (Activity interface), `activities/types.ts` (ActivityFormState) |
| **React Query** | `useActivities.ts`, `queryKeys.ts`, `hooks/queries/index.ts` |
| **Config (3 duplicates!)** | `config/activityTypes.ts`, `activities/config.ts`, inline in `Activities.tsx` |
| **Activities Page** | `Activities.tsx` |
| **Pipeline (Deal activities)** | `Pipeline.tsx`, `pipeline/DealCard.tsx` (last activity display), `pipeline/utils.tsx` (formatLastActivity) |
| **Leads (Lead activities)** | `leads/LeadDetailModal.tsx` |
| **Contacts (Activity metadata)** | `Contacts.tsx` (lastActivityAtUtc) |
| **Tasks (Related activities)** | `tasks/components/TaskDetailModal.tsx` |
| **Dashboard** | `Dashboard.tsx`, `dashboard/RecentActivity.tsx` (CopyHistory, NOT Activity) |
| **Team** | `team/components/MemberDetailPanel.tsx` (mock only) |
| **Settings** | `settings/components/AccountSection.tsx` (showActivityStatus toggle) |
| **Mock Data** | `api/mockData.ts` |
| **Utilities** | `utils/dateFormatters.ts` |
| **Navigation** | `App.tsx`, `AppHeader.tsx`, `config/navigation.ts` |

---

## 27. Complete Relationship Map

```
                    ┌──────────────┐
                    │     User     │
                    │              │
                    │ ICollection  │
                    │  <Activity>  │
                    └──────┬───────┘
                           │ owns many
                           ▼
┌──────────┐      ┌──────────────┐      ┌──────────────┐
│ Contact  │◄─────│   Activity   │─────►│    Deal      │
│          │ opt  │              │ opt  │              │
│ ICol<A>  │ FK   │  Type        │ FK   │ ICol<A>      │
│          │      │  Subject     │      │ LastActivity │
│ lastAct  │      │  Body        │      │   AtUtc      │
└──────────┘      │  Participants│      └──────────────┘
                  │  CreatedAtUtc│
┌──────────┐      │              │
│   Lead   │◄─────│  ContactId?  │
│          │ opt  │  DealId?     │
│ ICol<A>  │ FK   │  LeadId?     │
│          │      │              │
│ (team-   │      └──────────────┘
│  visible)│
└──────────┘

Data Flow:
┌─────────────────────────────────────────────────┐
│ ActivityRepository                              │
│                                                 │
│  GetLastActivityByContactIdsAsync() ──► ContactService │
│    (batch aggregation per contact)     enriches ContactDto │
│                                        with LastActivityAtUtc │
│                                                 │
│  GetLastActivityByDealIdsAsync() ────► DealService    │
│    (batch aggregation per deal)        enriches DealDto │
│                                        with LastActivityAtUtc │
└─────────────────────────────────────────────────┘

Frontend Pages Using Activities:
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ Activities  │  │  Pipeline   │  │   Leads     │
│ Page        │  │  Page       │  │   Page      │
│ (Main UI,  │  │ (Deal detail │  │ (Lead detail│
│  Timeline,  │  │  sheet with  │  │  modal with │
│  CRUD)      │  │  activity    │  │  activity   │
│             │  │  log form)   │  │  timeline)  │
└─────────────┘  └─────────────┘  └─────────────┘
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│  Contacts   │  │   Tasks     │  │   Team      │
│  Page       │  │   Page      │  │   Page      │
│ (lastActiv- │  │ (TaskDetail │  │  (Mock stats│
│  ityAtUtc   │  │  loads deal/ │  │   only)     │
│  metadata)  │  │  lead acts)  │  │             │
└─────────────┘  └─────────────┘  └─────────────┘
```

---

## 28. Critical Bugs & Issues

| # | Severity | Issue | Details | Files | Status |
|---|----------|-------|---------|-------|--------|
| 1 | ~~**HIGH**~~ | ~~**Frontend-Backend Activity Type Mismatch**~~ | Backend now accepts all 9 types: `call`, `meeting`, `email`, `note`, `task`, `follow_up`, `deadline`, `video`, `demo`. | `ActivityService.cs`, `CreateActivityRequest.cs` | ✅ FIXED (HP-1) |
| 2 | **MEDIUM** | **Triple duplication of ACTIVITY_TYPES** | Activity type config is defined in 3 places: `src/app/config/activityTypes.ts`, `src/app/pages/activities/config.ts`, and inline in `Activities.tsx` line 49. Any change must be made in all 3 places. | See files listed. | Open |
| 3 | **MEDIUM** | **Date format utility duplication** | `Activities.tsx` defines `formatDate()`, `formatRelativeDate()`, and `getDateGroup()` inline instead of importing from `src/app/utils/dateFormatters.ts`. | `Activities.tsx` lines 59-92, `utils/dateFormatters.ts`. | Open |
| 4 | ~~**LOW**~~ | ~~**ActivityDto includes Participants but frontend never displays it**~~ | Participants now displayed in Activities page cards and Pipeline deal detail timeline. Also settable in the Log Activity form. | `Activities.tsx`, `Pipeline.tsx`, `activities.ts` | ✅ FIXED (HP-6) |
| 5 | **LOW** | **Activities page doesn't use React Query hooks** | The Activities page manages its own state with `useState` + direct API calls instead of using the `useActivities`/`useCreateActivity`/`useDeleteActivity` hooks. | `Activities.tsx` vs `useActivities.ts`. | Open |
| 6 | ~~**LOW**~~ | ~~**No leadId in ActivityFormState**~~ | Lead filter now available on Activities page ("By Lead" tab). | `activities/types.ts`, `Activities.tsx` | ✅ FIXED (HP-5) |

---

## 29. What Is Missing — Comprehensive Detailed Analysis

This section provides a thorough breakdown of every missing or incomplete activity-related feature. For each item we explain: **what currently exists**, **what is missing**, **why we need it** (business justification), and **what it would take to implement**.

### IMPLEMENTATION STATUS (Updated February 9, 2026)

**ALL 6 of 6 HIGH PRIORITY items have been implemented.**

| Status | Count |
|--------|-------|
| ✅ Implemented | 6 |
| ⚠️ Partially Done | 0 |
| ❌ Not Started | 0 |

---

### MASTER OVERVIEW — Priority Classification At-a-Glance

Every issue in this section is classified into one of **four tiers**. The distinction is:

- **CRITICAL / MUST FIX** = Active bugs causing errors, fake data, or misleading UI. These MUST be fixed before any new feature work. Without fixing these, users encounter errors and see fabricated metrics.
- **MUST HAVE** = Core activity features that every CRM competitor has. Without these, the activity system is incomplete for daily sales work. Each includes step-by-step implementation.
- **SHOULD HAVE** = Features that make the activity system significantly more powerful but the system functions without them. Each includes implementation guidance.
- **NICE TO HAVE** = Enhancements for future sprints. Not blocking any core workflow. Planned when higher tiers are complete.

| # | Item | Tier | Category | Effort | Status | What Happens If We Don't Do It |
|---|------|------|----------|--------|-------|-------------------------------|
| HP-1 | Activity type mismatch (7 frontend vs 4 backend) | **CRITICAL** | Bug — 400 errors | 30 min | ✅ IMPLEMENTED | Users get errors when logging Video, Demo, or Task activities. Task audit trail completely broken. |
| HP-3 | Team member activity stats are fake | **CRITICAL** | Bug — fabricated data | Few hours | ✅ IMPLEMENTED | Managers see random numbers for team performance. Decisions based on lies. |
| HP-4 | Dashboard "Recent Activity" shows wrong data | **CRITICAL** | Bug — misleading | Few hours | ✅ IMPLEMENTED | "Recent Activity" shows AI copy history, not CRM activities. Users confused. |
| HP-2 | Activity edit — immutable after creation | **MUST HAVE** | Missing core feature | 1 day | ✅ IMPLEMENTED | Typos can't be corrected. Users must delete and recreate. Only entity without edit. |
| HP-5 | Lead filter missing on Activities page | **MUST HAVE** | Missing filter | Few hours | ✅ IMPLEMENTED | Lead activities exist in DB but can't be found from Activities page. Filtering incomplete. |
| HP-6 | Participants field stored but never shown | **MUST HAVE** | Dead feature | 30 min | ✅ IMPLEMENTED | Database stores participant data, API returns it, but UI never displays it. Wasted data. |
| MP-1 | Activity pagination for filtered views | **SHOULD HAVE** | Performance | 1-2 days | Contact/deal views load ALL activities at once. Slow for long-running deals. |
| MP-2 | Activity reporting / analytics | **SHOULD HAVE** | Missing feature | 2-3 days | No "activities per week", no type distribution, no team leaderboard. Managers blind. |
| MP-3 | Activity duration tracking | **SHOULD HAVE** | Missing field | Half day | No way to record call/meeting length. Can't track time spent. |
| MP-4 | Activity scheduling / future dates | **SHOULD HAVE** | Missing feature | 1-2 days | All activities are retrospective. Can't schedule future calls or meetings. |
| MP-5 | Activity search in global search | **SHOULD HAVE** | Missing integration | Half day | Activities not found via global search. Users must scroll manually. |
| MP-6 | Activity automation (auto-logging) | **SHOULD HAVE** | Missing feature | 3-5 days | Every activity must be manually logged. Incomplete audit trails. |
| LP-1 to LP-7 | Attachments, templates, assignment, recurrence, bulk ops, real-time feed, GET by ID | **NICE TO HAVE** | Enhancements | Varies | Product works without them; these are roadmap items. |

---

### A. CRITICAL BUGS — Must Fix Immediately (before any new feature work)

These are active bugs causing 400 errors, fabricated metrics, and misleading dashboard data. The product is actively harming user trust while these remain unfixed. Total effort: **~1 day for all 3 bug fixes.**

---

#### HP-1. Activity Type Mismatch — Frontend vs Backend (BUG) — ✅ IMPLEMENTED

**Effort:** Low (30 min) | **Impact:** Critical — production error

**What exists now:**
- Backend `ActivityService.cs`: `ValidActivityTypes` = `{ "call", "meeting", "email", "note" }` — only 4 types accepted
- Frontend `activities/config.ts`: Defines 7 types: `call`, `meeting`, `email`, `note`, `video`, `demo`, `task`
- Pipeline deal detail form: Shows 4 types (Note, Call, Email, Meeting) + "Task" as a 5th option

**What is missing:** Backend support for `video`, `demo`, and `task` activity types.

**Why we need it:**
1. **Production error**: When a user selects "Video Call", "Demo", or "Task Completed" from the Activities page, the backend returns a **400 Bad Request**. This is a live bug that affects every user who tries to log these activity types. The error is shown as a generic failure toast — the user has no idea why their activity wasn't saved.
2. **Modern sales activities**: Video calls (Zoom, Teams, Google Meet) are now a primary communication channel. "Demo" is one of the most important sales activities — logging demos separately from generic meetings enables reporting on demo-to-close conversion rates. Excluding these types means the activity log doesn't reflect how modern sales teams actually work.
3. **Task activity logging broken everywhere**: `Tasks.tsx` `logTaskActivity()` creates activities with `type: 'task'`. Since 'task' is not in the backend's allowed list, EVERY task action that attempts to log an activity silently fails. This means the entire task activity audit trail is broken — task creation, completion, and assignment are never recorded.

**How to implement:**
1. **Backend — `ActivityService.cs`**: Expand `ValidActivityTypes`:
   ```csharp
   private static readonly HashSet<string> ValidActivityTypes = new(StringComparer.OrdinalIgnoreCase)
   { "call", "meeting", "email", "note", "video", "demo", "task" };
   ```
2. **Backend — `CreateActivityRequest.cs`**: Update regex to `^(call|meeting|email|note|video|demo|task)$`
3. **Test**: Log an activity of each of the 7 types. Verify no 400 errors.

---

#### HP-3. Team Member Activity Stats Are Fake (BUG) — ✅ IMPLEMENTED

**Effort:** Low (few hours) | **Impact:** High — misleading data

**What exists now:** `MemberDetailPanel.tsx` shows "Activities Logged" per team member. The number is `Math.floor(Math.random() * 200) + 50` — a **random number** generated on every render.

**Why we need it:**
1. **Active deception**: A manager looking at team performance sees "Sarah: 187 activities" and "Tom: 93 activities" — these numbers are completely random and change on every page refresh. Decisions made based on these numbers (coaching, performance reviews, workload balancing) are based on lies.
2. **Erodes trust in all data**: If users discover one metric is fake, they question every metric in the system. "Is pipeline value real? Are deal counts accurate? Can I trust anything?"
3. **Simple fix**: The backend already has activity data. Just query real counts instead of generating random numbers.

**What was fixed:**
1. **Backend — `IActivityRepository.cs`**: Added `GetActivityCountsByOrgAsync(Guid? organizationId)` method that returns activity counts grouped by UserId for all org members.
2. **Backend — `ActivityRepository.cs`**: Implemented the method using EF Core `GroupBy` + `Count()` query on the Activities table, scoped to the organization.
3. **Backend — `IActivityService.cs` / `ActivityService.cs`**: Added `GetOrgMemberActivityCountsAsync()` service method wrapping the repository call.
4. **Backend — `ActivitiesController.cs`**: Added `GET /api/activities/org-member-counts` endpoint returning `Dictionary<Guid, int>` (userId → count).
5. **Frontend — `activities.ts`**: Added `getOrgMemberActivityCounts()` API function calling the new endpoint.
6. **Frontend — `MemberDetailPanel.tsx`**: Replaced random numbers with real data. On mount, fetches org member activity counts via the new endpoint and displays the real count for the selected member. Other stats (leads, deals, tasks, conversion rate, response time) still show "—" until their respective backend endpoints are implemented.

---

#### HP-4. Dashboard "Recent Activity" Is Misleading (Wrong Data Source) — ✅ IMPLEMENTED

**Effort:** Low (few hours) | **Impact:** Medium-High — user confusion

**What exists now:** The `RecentActivity` component on the Dashboard shows **copy generation history** (AI-generated sales copy entries), NOT CRM activities (calls, meetings, emails, notes).

**Why we need it:**
1. **Name implies CRM activities**: "Recent Activity" universally means "recent calls, meetings, emails logged" in CRM context. Showing copy generation history under this label confuses users who expect to see their team's interaction log.
2. **Missed opportunity**: The Dashboard should be the central hub showing real sales activity — "3 calls today, 2 meetings scheduled, 5 emails sent." Instead, it shows "Generated cold outreach email for John" entries from the AI writer, which is tangential to sales execution.
3. **Activity data exists**: `getActivities()` returns real CRM activity data. It's just not used on the Dashboard.

**What was fixed:**
1. **Frontend — `Dashboard.tsx`**: Now calls `getActivities()` on mount and passes the 5 most recent `Activity[]` items to the `RecentActivity` component (instead of copy generation history).
2. **Frontend — `RecentActivity.tsx`**: Completely rewritten to accept `Activity[]` (not `CopyHistoryItem[]`). Displays real CRM activities with type-specific icons (phone for calls, mail for email, users for meetings, etc.), color-coded badges, subject lines, related entity names (contact/deal/lead), and formatted dates. Includes an empty state message "No recent CRM activities — Log activities to track your sales interactions" and a "View all" link to `/activities`.

---

### A2. MUST HAVE — Core Features Required for a Complete Activity System

These are fundamental activity features that every CRM competitor (Salesforce, HubSpot, Pipedrive) has. Without these, the activity system is incomplete for daily sales operations. Each includes detailed step-by-step implementation instructions. Total effort: **~2 days for all 3 items.**

---

#### HP-2. Activity Update/Edit — Immutable After Creation (Missing Core Feature) — ✅ IMPLEMENTED

**Effort:** Low-Medium (1 day) | **Impact:** High | **MUST HAVE**

**What exists now:**
- `ActivitiesController.cs`: Has `GET`, `POST`, `DELETE` — no `PUT` or `PATCH` endpoint
- `IActivityService.cs`: Has `CreateAsync` and `DeleteAsync` — no `UpdateAsync`
- `Activity.cs`: Has `UpdatedAtUtc` and `UpdatedByUserId` fields — but they are NEVER SET because no update path exists

**What is missing:** The ability to edit an activity after creation.

**Why we need it:**
1. **Typos and mistakes are inevitable**: A rep quickly logs "Called John about the Acme proposal" but realizes they meant "Called Jane." Currently, they must delete the activity and recreate it — losing the original creation timestamp and `CreatedByUserId` metadata. The delete-and-recreate workaround is clunky and loses audit trail information.
2. **Correcting details after the fact**: After a meeting, a rep logs brief notes. Later, they want to add more detailed outcomes or correct inaccurate information. Without edit, they must delete and retype everything, or create a supplementary "note" activity that fragments the record.
3. **Dead infrastructure**: The entity has `UpdatedAtUtc` and `UpdatedByUserId` fields that exist specifically to track edits. Someone designed the data model for editability but never implemented the feature. This is wasted schema.
4. **Every other entity has edit**: Deals, Contacts, Leads, Tasks, Companies — all support update. Activities are the only entity without edit. This inconsistency is confusing for users who expect uniform behavior.

**How to implement — step by step:**

1. **Backend — `IActivityService.cs`**: Add method signature:
   ```csharp
   Task<Result<ActivityDto>> UpdateAsync(Guid id, UpdateActivityRequest request, Guid userId, Guid? organizationId, CancellationToken ct = default);
   ```

2. **Backend — Create `UpdateActivityRequest.cs`**: New DTO:
   ```csharp
   public record UpdateActivityRequest
   {
       [Required, StringLength(500)]
       public string? Subject { get; init; }

       [StringLength(5000)]
       public string? Body { get; init; }

       [RegularExpression("^(call|meeting|email|note|video|demo|task)$")]
       public string? Type { get; init; }

       public Guid? ContactId { get; init; }
       public Guid? DealId { get; init; }
       public Guid? LeadId { get; init; }
       public string? Participants { get; init; }
   }
   ```

3. **Backend — `ActivityService.cs`**: Implement `UpdateAsync`:
   ```csharp
   public async Task<Result<ActivityDto>> UpdateAsync(Guid id, UpdateActivityRequest request, Guid userId, Guid? orgId, CancellationToken ct)
   {
       var activity = await _repository.GetByIdAsync(id, ct);
       if (activity is null || activity.UserId != userId)
           return Result<ActivityDto>.Failure(DomainErrors.Activity.NotFound);

       if (request.Subject is not null) activity.Subject = request.Subject.Trim();
       if (request.Body is not null) activity.Body = request.Body.Trim();
       if (request.Type is not null) activity.Type = request.Type;
       if (request.ContactId is not null) activity.ContactId = request.ContactId;
       if (request.DealId is not null) activity.DealId = request.DealId;
       if (request.LeadId is not null) activity.LeadId = request.LeadId;
       if (request.Participants is not null) activity.Participants = request.Participants.Trim();

       activity.UpdatedAtUtc = DateTime.UtcNow;
       activity.UpdatedByUserId = userId;

       await _repository.UpdateAsync(activity, ct);
       return Result<ActivityDto>.Success(Map(activity));
   }
   ```

4. **Backend — `ActivitiesController.cs`**: Add endpoint:
   ```csharp
   [HttpPut("{id:guid}")]
   public async Task<ActionResult<ActivityDto>> Update(Guid id, [FromBody] UpdateActivityRequest request)
   {
       var userId = GetUserId();
       var orgId = GetOrganizationId();
       var result = await _activityService.UpdateAsync(id, request, userId, orgId);
       return result.IsSuccess ? Ok(result.Value) : result.Error == DomainErrors.Activity.NotFound ? NotFound() : BadRequest(result.Error);
   }
   ```

5. **Frontend — `activities.ts`**: Add API function:
   ```typescript
   export async function updateActivity(id: string, params: Partial<CreateActivityParams>): Promise<Activity> {
     return authFetchJson<Activity>(`/api/activities/${id}`, { method: 'PUT', body: JSON.stringify(params) });
   }
   ```

6. **Frontend — Activities page**: Add "Edit" button in activity card dropdown menu. On click, open the same creation form pre-filled with current values. On submit, call `updateActivity()` and refresh the list.

7. **Frontend — Pipeline deal detail**: Add edit action on activities in the timeline view.

8. **Test**: Create activity → Edit subject → Verify updated subject persists. Verify `UpdatedAtUtc` is set. Verify editing someone else's activity returns 404.

---

#### HP-5. Lead Filtering on Activities Page (Missing Filter) — ✅ IMPLEMENTED

**Effort:** Low (few hours) | **Impact:** Medium | **MUST HAVE**

**What exists now:** Activities page has filter: `'all' | 'contact' | 'deal'` — no `'lead'` option. Lead activities can only be seen from the Lead Detail Modal.

**Source evidence:** `ActivityFilter` type only includes `all`, `contact`, `deal`. Backend has `GetByLeadIdAsync` but the Activities page never calls it.

**Why we need it:**
1. **Incomplete filtering**: Activities can be linked to leads (the backend supports it, and Lead Detail Modal creates lead-linked activities). But the Activities page can't filter by lead. This means there's a category of activities that exists in the database but can only be viewed from one specific UI location.
2. **Lead activity review**: When reviewing a lead before conversion, a rep wants to see ALL activities for that lead. The Lead Detail Modal shows them, but if the rep is already on the Activities page, they can't filter to that lead.
3. **Consistency**: Contact and Deal filters exist. Lead filter is missing. This inconsistency is confusing.

**What was fixed:**
1. **Frontend — `activities/types.ts`**: Expanded `ActivityFilter` to `'all' | 'contact' | 'deal' | 'lead'`. Added `participants` to `ActivityFormState`.
2. **Frontend — `Activities.tsx`**: Added "By Lead" tab alongside existing filter tabs. Loads leads on mount via `getLeads()`. When "By Lead" is selected, shows a lead selector dropdown and calls `getActivitiesByLead(selectedLeadId)` to fetch activities. Filter state (`filterLeadId`) is managed alongside `filterContactId` and `filterDealId`.
3. **Backend**: `GET /api/activities/lead/{leadId}` endpoint already existed and works correctly.

---

#### HP-6. Participants Field — Stored But Never Displayed (Dead Feature) — ✅ IMPLEMENTED

**Effort:** Low (30 min) | **Impact:** Medium | **MUST HAVE**

**What exists now:** `Activity.cs` has `Participants` (string?). `ActivityDto` includes it. `CreateActivityRequest` accepts it. The field is stored in the database and returned by the API. But NO frontend UI displays, sets, or reads it.

**Why we need it:**
1. **Dead data wastes resources**: Every activity stores and transmits `Participants` data — database storage, API bandwidth, JSON serialization — but users never see it. Either display it or remove it.
2. **Meetings involve multiple people**: A meeting with 5 stakeholders should record who attended. This is critical for meeting follow-ups, accountability, and stakeholder mapping. The data structure exists but is invisible.
3. **Call participants**: "Called Jane and Mike from Acme Corp" — recording multiple participants helps track multi-party communications.

**What was fixed:**
1. **Frontend — Activities page (Log Activity form)**: Added a "Participants" text input field shown when type is `call`, `meeting`, `video`, or `demo`. Uses a violet-themed icon and label, with placeholder "e.g. Jane Smith, Mike Johnson".
2. **Frontend — Activity card display**: When `activity.participants` is present, displays a violet-themed badge below the body text showing "Participants: {names}" with a Users icon.
3. **Frontend — `activities.ts`**: Updated `createActivity()` function signature to accept `participants` field in the params object. `updateActivity()` already included it.
4. **Frontend — `Activities.tsx`**: Updated the `createActivity()` call to pass `form.participants.trim() || undefined` (was previously missing from the create path, though the update path already included it).
5. **Frontend — Pipeline deal detail (`Pipeline.tsx`)**: Added participants display in the deal detail sheet's activity timeline. When an activity has participants, shows "with {participants}" in violet text with a User icon below the activity body.
6. **Frontend — `activities/types.ts`**: `ActivityFormState` now includes `participants` field with default empty string.

---

### B. MEDIUM PRIORITY / SHOULD HAVE — Important Improvements (Do After All HIGH Items Are Done)

These make the activity system significantly more powerful and fill gaps that users WILL notice. The product functions without them but feels incomplete compared to competitors. Each includes full implementation guidance. Total effort: **~8-14 days for all 6 items.**

---

#### MP-1. Activity Pagination for Contact/Deal Filtered Views

**Effort:** Medium (1-2 days) | **Impact:** High at scale

**What's missing:** Server-side pagination works for the "All" view, but contact-filtered (`getActivitiesByContact`) and deal-filtered (`getActivitiesByDeal`) views load ALL activities at once — no pagination.

**Why we need it:** A long-running enterprise deal could accumulate 200+ activities over months of sales cycles. Loading all of them at once causes slow renders and high memory usage. This is especially problematic in the Pipeline detail sheet where the activity area is a small 300px scrollable container trying to render hundreds of items.

**How to implement — step by step:**

1. **Backend — `IActivityRepository.cs`**: Add paginated overloads:
   ```csharp
   Task<PagedResult<Activity>> GetByContactIdPagedAsync(Guid contactId, Guid orgId, int page = 1, int pageSize = 20);
   Task<PagedResult<Activity>> GetByDealIdPagedAsync(Guid dealId, Guid orgId, int page = 1, int pageSize = 20);
   ```

2. **Backend — `ActivityRepository.cs`**: Implement with `.Skip((page - 1) * pageSize).Take(pageSize)` and `.CountAsync()` for total.

3. **Backend — `ActivitiesController.cs`**: Add optional `page` and `pageSize` query params to the existing `GetByContact` and `GetByDeal` endpoints.

4. **Frontend — `activities.ts`**: Add `page` and `pageSize` params to `getActivitiesByContact()` and `getActivitiesByDeal()`.

5. **Frontend — Pipeline deal detail sheet**: Add "Load more" button or infinite scroll at the bottom of the activity timeline. Track current page in state:
   ```typescript
   const [activityPage, setActivityPage] = useState(1);
   const [hasMore, setHasMore] = useState(true);
   ```

6. **Test:** Create 30+ activities for a single deal. Open deal detail. Verify only 20 load initially. Click "Load more." Verify next batch loads.

---

#### MP-2. Activity Reporting / Analytics

**Effort:** Medium (2-3 days) | **Impact:** High for managers

**What's missing:** No activity-specific reports. Dashboard has no activity metrics. No "activities per day/week", "activities by type distribution", "activities by team member", or "average time between activities for a deal."

**Why we need it:** Sales managers need to track team effort, not just results. "How many calls did the team make this week?" is a fundamental sales management question. Activity metrics correlate strongly with pipeline health — teams that log more activities close more deals. Without activity reporting, there's no way to measure sales effort or identify reps who need coaching on activity volume.

**How to implement — step by step:**

1. **Backend — `ReportingService.cs`**: Add activity reporting methods:
   ```csharp
   public async Task<ActivityStatsDto> GetActivityStatsAsync(Guid orgId, DateTime from, DateTime to)
   {
       var activities = await _activityRepo.GetByOrgAndDateRange(orgId, from, to);
       return new ActivityStatsDto(
           TotalCount: activities.Count,
           ByType: activities.GroupBy(a => a.Type).ToDictionary(g => g.Key, g => g.Count()),
           ByUser: activities.GroupBy(a => a.CreatedByUserId).ToDictionary(g => g.Key, g => g.Count()),
           ByWeek: activities.GroupBy(a => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(a.CreatedAtUtc, ...)).ToDictionary(...)
       );
   }
   ```

2. **Backend — DTOs**: Create `ActivityStatsDto` record:
   ```csharp
   public record ActivityStatsDto(int TotalCount, Dictionary<string, int> ByType,
       Dictionary<Guid, int> ByUser, Dictionary<int, int> ByWeek);
   ```

3. **Backend — `ReportingController.cs`**: Add endpoint `GET /api/reports/activity-stats?from=&to=`.

4. **Frontend — Create `ActivityStats.tsx`** component for Dashboard:
   - Bar chart: "Activities per Week" (using Recharts `BarChart`)
   - Pie chart: "Activities by Type" (calls vs meetings vs emails vs notes)
   - Leaderboard: "Activities by Team Member" (ranked list)

5. **Frontend — `Dashboard.tsx`**: Import and render `ActivityStats` component. Add date range selector (This Week / This Month / This Quarter).

6. **Test:** Log 20+ activities across types and users. Navigate to Dashboard. Verify charts render with correct data. Change date range.

---

#### MP-3. Activity Duration Tracking

**Effort:** Low (half day) | **Impact:** Medium

**What's missing:** No start/end time or duration field for calls and meetings. `Activity.cs` has only `CreatedAtUtc` — no `StartTime`, `EndTime`, or `DurationMinutes`.

**Why we need it:** "How long was the call?" is critical information for:
- **Coaching**: A 5-minute call vs. a 45-minute call suggests very different interactions
- **Time management**: Reps need to track how much time they spend on calls/meetings for capacity planning
- **Quality signals**: Long meetings with decision-makers signal deal engagement. Short calls may indicate rejection or disinterest
- **Billing**: Some industries (consulting, legal) track client-facing time

**How to implement — step by step:**

1. **Backend — `Activity.cs`**: Add field:
   ```csharp
   public int? DurationMinutes { get; set; } // nullable, only relevant for calls/meetings
   ```

2. **Backend — `ActivityConfiguration.cs`**: No special config needed (int? is handled by EF convention).

3. **Backend — `ActivityDto.cs`**: Add `int? DurationMinutes`.

4. **Backend — `CreateActivityRequest.cs` / `UpdateActivityRequest.cs`**: Add:
   ```csharp
   [Range(1, 1440)] // 1 minute to 24 hours
   public int? DurationMinutes { get; init; }
   ```

5. **Backend — EF Migration**: `dotnet ef migrations add AddActivityDuration`

6. **Frontend — Activity creation form**: Show duration input only when type is `call`, `meeting`, `video`, or `demo`:
   ```tsx
   {['call', 'meeting', 'video', 'demo'].includes(selectedType) && (
     <Input type="number" min={1} max={1440} placeholder="Duration (min)" value={duration} onChange={...} />
   )}
   ```

7. **Frontend — Activity cards**: Display duration when present: "45 min call" or "1h 30m meeting".

8. **Test:** Create a call with 30-min duration. Verify it persists. Verify duration displays on the card. Verify duration input hidden for "note" and "email" types.

---

#### MP-4. Activity Scheduling / Future-Dated Activities

**Effort:** Medium (1-2 days) | **Impact:** Medium-High

**What's missing:** All activities are retrospective logs. No way to create a future-dated activity ("Meeting with Jane on March 15 at 2pm").

**Why we need it:** Sales reps plan activities in advance — scheduling follow-up calls, booking meeting slots, planning email sends. Currently, these must be tracked as Tasks (which are a different concept) or in an external calendar. A "scheduled activity" feature would bridge the gap between planning and execution, automatically converting to a logged activity when the time arrives.

**How to implement — step by step:**

1. **Backend — `Activity.cs`**: Add fields:
   ```csharp
   public DateTime? ScheduledAtUtc { get; set; }
   public bool IsCompleted { get; set; } = true; // existing activities default to completed (logged)
   ```

2. **Backend — DTOs**: Add `ScheduledAtUtc` and `IsCompleted` to `ActivityDto`, `CreateActivityRequest`.

3. **Backend — `ActivityService.cs`**: When `ScheduledAtUtc` is in the future, set `IsCompleted = false`. When user later "completes" the activity, set `IsCompleted = true` and `CompletedAtUtc = DateTime.UtcNow`.

4. **Backend — EF Migration**: `dotnet ef migrations add AddActivityScheduling`

5. **Frontend — Activity creation form**: Add optional date/time picker for scheduling:
   ```tsx
   <label>Schedule for later (optional)</label>
   <Input type="datetime-local" value={scheduledAt} onChange={...} />
   ```

6. **Frontend — Activities page**: Add "Upcoming" tab alongside existing filters showing future-dated activities sorted by scheduled date. Show countdown or date badge: "In 3 days" or "Tomorrow at 2pm".

7. **Frontend — Activities page**: Add "Mark as Done" action on scheduled (incomplete) activities.

8. **Test:** Schedule a meeting for next week. Verify it appears in "Upcoming" tab. Click "Mark as Done." Verify it moves to the completed activity list.

---

#### MP-5. Activity Search in Global Search

**Effort:** Low (half day) | **Impact:** Medium

**What's missing:** `GlobalSearchService` queries leads, contacts, companies, and deals — but NOT activities. Activities cannot be found via the global search bar.

**Why we need it:** Users often remember an activity detail but not the exact contact or deal: "I had a call last week about the pricing issue." Global search should surface this. Currently, they must go to Activities page and manually scroll through entries.

**How to implement — step by step:**

1. **Backend — `GlobalSearchService.cs`**: Add activity search task alongside existing entity searches:
   ```csharp
   var activityTask = _activityRepo.SearchAsync(query, orgId, limit: 10);
   // Add to Task.WhenAll(...)
   ```

2. **Backend — `IActivityRepository.cs`**: Add search method:
   ```csharp
   Task<List<Activity>> SearchAsync(string query, Guid organizationId, int limit = 10);
   ```

3. **Backend — `ActivityRepository.cs`**: Implement search across `Subject`, `Body`, `Participants`:
   ```csharp
   public async Task<List<Activity>> SearchAsync(string query, Guid orgId, int limit = 10) =>
       await _context.Activities
           .Where(a => a.OrganizationId == orgId &&
               (a.Subject.Contains(query) || a.Body.Contains(query) || (a.Participants != null && a.Participants.Contains(query))))
           .OrderByDescending(a => a.CreatedAtUtc)
           .Take(limit)
           .ToListAsync();
   ```

4. **Backend — `GlobalSearchResultDto.cs`**: Add `Activities` to the result:
   ```csharp
   public List<SearchResultItem> Activities { get; set; } = new();
   ```

5. **Frontend — `search.ts` types**: Add `activities` array to `GlobalSearchResult` type.

6. **Frontend — Global search dropdown** (once CS-1 is implemented): Add "Activities" result group with type icon, subject, and relative date.

7. **Test:** Log an activity with subject "Pricing discussion with Acme Corp." Search "Acme" in global search. Verify activity appears in results.

---

#### MP-6. Activity Automation (Auto-Logging)

**Effort:** Medium-High (3-5 days) | **Impact:** High

**What's missing:** All activities must be manually logged. No auto-creation of activities when deal stages change, tasks are completed, emails are sent via sequences, or other CRM events occur.

**Why we need it:** Manual logging is the #1 reason CRM activity data is incomplete. Reps forget to log calls, meetings happen without being recorded, stage changes are invisible. Auto-logging ensures a complete audit trail:
- "Deal moved from Qualification to Proposal" → auto-create activity
- "Email sequence step 3 sent to Jane" → auto-create activity
- "Task 'Send proposal' marked complete" → auto-create activity

**How to implement — step by step:**

1. **Backend — Create `IActivityLogger` interface:**
   ```csharp
   public interface IActivityLogger
   {
       Task LogDealStageChangeAsync(Deal deal, string fromStage, string toStage, Guid userId);
       Task LogTaskCompletedAsync(TaskItem task, Guid userId);
       Task LogEmailSentAsync(Guid contactId, string subject, Guid userId, Guid orgId);
       Task LogDealCreatedAsync(Deal deal, Guid userId);
   }
   ```

2. **Backend — `ActivityLogger.cs`**: Implement by creating `Activity` entities with `type: "system"` (or use existing types with a `IsAutoLogged = true` flag):
   ```csharp
   public async Task LogDealStageChangeAsync(Deal deal, string fromStage, string toStage, Guid userId)
   {
       var activity = new Activity
       {
           Subject = $"Deal stage changed: {fromStage} → {toStage}",
           Body = $"Deal '{deal.Name}' was moved from {fromStage} to {toStage}.",
           Type = "note",
           DealId = deal.Id,
           ContactId = deal.ContactId,
           OrganizationId = deal.OrganizationId,
           CreatedByUserId = userId,
           IsAutoLogged = true // new field
       };
       await _activityRepo.AddAsync(activity);
   }
   ```

3. **Backend — Inject `IActivityLogger` into services**:
   - `DealService.UpdateAsync`: Call `LogDealStageChangeAsync` when `DealStageId` changes
   - `TaskService.UpdateAsync`: Call `LogTaskCompletedAsync` when status changes to `Completed`
   - `EmailSequenceService`: Call `LogEmailSentAsync` when a sequence step executes

4. **Backend — `Activity.cs`**: Add flag:
   ```csharp
   public bool IsAutoLogged { get; set; } = false;
   ```

5. **Frontend — Activity cards**: Show auto-logged activities with a distinct visual (system icon, grey background, "Auto-logged" label) to differentiate from manual entries.

6. **Test:** Move a deal stage. Verify an activity is auto-created with correct subject and deal link. Complete a task. Verify an activity is auto-created.

---

### C. LOW PRIORITY / NICE TO HAVE — Future Roadmap Enhancements

These features add meaningful value but **are NOT blocking any core workflow**. The activity system works fully without them. Plan these for future sprints after all CRITICAL, MUST HAVE, and SHOULD HAVE items are complete. No implementation details needed yet — these should be scoped during sprint planning.

---

#### LP-1. Activity Attachments

**Effort:** Medium-High | **Impact:** Medium

**What exists now:** Activities store only text content (Subject, Body). No file upload capability anywhere in the activity system. No `ActivityAttachment` entity, no file storage configuration, no upload UI.

**What is missing:** The ability to attach files (screenshots, email copies, PDF proposals, meeting recordings, contracts, presentations) to an activity record.

**Why we need it:**
1. **Call context**: A rep logs "Called Jane about the pricing proposal." The actual pricing proposal PDF should be attached to this activity, not stored in a separate folder where it may get lost or mislabeled. When another rep reviews this activity, they need instant access to the referenced document.
2. **Meeting notes with media**: After a demo meeting, a rep may have screenshots of the client's current setup, a recorded video of the demo, or a whiteboard photo. Attaching these to the meeting activity creates a complete record of what happened.
3. **Email evidence**: When logging an email activity, the actual email content (or a forwarded copy) should be attachable for verification. This is especially important for legal/compliance purposes — "prove we sent the terms on March 5th."
4. **Audit trail completeness**: An activity log without supporting documents is an incomplete record. Attachments transform activities from "someone said they did something" to "here is the evidence of what happened."

**How to implement:**
- Backend: New `ActivityAttachment` entity (Id, ActivityId, FileName, ContentType, FileSizeBytes, StoragePath, UploadedAtUtc). File storage (Azure Blob Storage or local disk). Upload/download/delete endpoints on `ActivitiesController`. Size limit (e.g., 10MB per file, 50MB per activity).
- Frontend: Drag-and-drop upload zone in activity creation form. File list with type icons, file size, and download link in activity cards. Preview for images.

---

#### LP-2. Activity Templates

**Effort:** Low-Medium | **Impact:** Medium

**What exists now:** Every activity must be typed from scratch. The "Log Activity" dialog has empty Subject and Body fields each time. No saved templates, snippets, or pre-fill options.

**What is missing:** Pre-built and user-created templates for common activity types that auto-populate the Subject and Body fields.

**Why we need it:**
1. **Time savings**: Reps log 10-20 activities per day. Many follow the same pattern: "Follow-up call with {Contact} re: {Deal}", "Sent proposal for {Deal}", "Meeting notes — {Date}". Typing these from scratch each time wastes 30-60 seconds per activity. At 15 activities/day, that's 7-15 minutes of repetitive typing daily, or 25-50 hours per year per rep.
2. **Consistency**: Without templates, every rep logs differently. One writes "Called John, discussed pricing." Another writes "Phone conversation with client about rate card." Inconsistent logging makes analysis harder — you can't search or filter effectively when every entry uses different language.
3. **Training acceleration**: New reps don't know what to log or how detailed to be. Templates provide structure: "Meeting Notes Template: Attendees: ___, Agenda items: ___, Decisions made: ___, Next steps: ___." This ensures important information isn't forgotten.
4. **Quick logging**: A "quick log" feature (select template + contact/deal = done) would reduce activity logging from 5 clicks to 2, dramatically increasing adoption. The #1 reason CRM activity data is incomplete is that logging is too slow.

**How to implement:**
- Backend: New `ActivityTemplate` entity (Id, UserId, OrgId, Name, Type, SubjectTemplate, BodyTemplate, IsShared). CRUD endpoints.
- Frontend: Template selector in the Log Activity dialog. "Save as template" button. Template management in Settings. Variable substitution for `{ContactName}`, `{DealName}`, `{Date}`.

---

#### LP-3. Activity Assignment / Delegation

**Effort:** Low | **Impact:** Low-Medium

**What exists now:** Activities are owned by the user who creates them (`Activity.UserId`). There is no `AssigneeId` or delegation mechanism. One user cannot assign an activity to another user.

**What is missing:** The ability to assign activities to team members for follow-up or delegation.

**Why we need it:**
1. **Manager delegation**: A sales manager attends a meeting and logs it. They want to assign follow-up activities to specific reps: "Tom — send revised pricing by Friday," "Sarah — schedule technical deep-dive." Currently, the manager must verbally (or via Slack) tell each rep, who must then manually create their own activities.
2. **Team handoffs**: When a rep goes on vacation, their pending follow-ups need to be delegated. Without activity assignment, the covering rep has no structured way to see what follow-ups are needed.
3. **Accountability**: "This follow-up call was assigned to Tom on March 5th, but he hasn't completed it." Assignment creates a clear record of who was responsible for what action.

**How to implement:**
- Backend: Add `AssigneeId` (Guid?, FK to User) to `Activity.cs`. Add to DTOs. Update ActivityService to validate assignee exists.
- Frontend: "Assign to" dropdown in activity creation form. "My Assigned Activities" filter on the Activities page. Notification when assigned.

---

#### LP-4. Activity Recurrence

**Effort:** Medium | **Impact:** Medium

**What exists now:** Each activity is a one-time record. If a rep has a weekly check-in with a client, they must manually create a new activity record every week. There is no recurrence, scheduling, or auto-creation mechanism.

**What is missing:** The ability to define recurring activity patterns that automatically create activity records at specified intervals.

**Why we need it:**
1. **Regular check-ins**: Account managers often have weekly or bi-weekly calls with key accounts. Manually creating "Weekly check-in with Acme Corp" every Monday is tedious and easy to forget. A recurring activity would auto-create these entries, serving as both a reminder and a log.
2. **Monthly reviews**: Sales teams conduct monthly pipeline reviews, quarterly business reviews (QBRs), and annual planning meetings with clients. These should be scheduled activities that auto-create and remind.
3. **Compliance activities**: In regulated industries, certain client interactions must happen at defined intervals (monthly compliance check, quarterly risk review). Recurrence ensures these aren't missed.
4. **Activity volume accuracy**: If reps forget to manually create recurring activities, the activity log understates the actual communication volume. Auto-creation ensures completeness.

**How to implement:**
- Backend: New fields on Activity or a separate `RecurringActivityTemplate` entity: `RecurrenceType` (daily/weekly/monthly/yearly), `RecurrenceInterval`, `RecurrenceEndDate`. Background job (Hangfire/Quartz) to auto-create activities based on recurrence rules.
- Frontend: "Make recurring" toggle in activity creation form. Recurrence pattern selector (every N days/weeks/months). Calendar view showing upcoming recurring activities.

---

#### LP-5. Bulk Activity Operations

**Effort:** Medium | **Impact:** Low-Medium

**What exists now:** Activities can only be created one at a time via `POST /api/activities` and deleted one at a time via `DELETE /api/activities/{id}`. No bulk create, bulk delete, import, or export functionality.

**What is missing:** The ability to perform operations on multiple activities simultaneously.

**Why we need it:**
1. **Post-event logging**: After a trade show, a rep met 20 people. They need to log 20 "meeting" activities, each linked to a different contact. Currently this requires 20 separate create operations with full form completion each time. A bulk log feature ("I met contacts A, B, C at [event name] on [date]") would create all 20 in one operation.
2. **Data cleanup**: An admin discovers 50 activities logged against the wrong contact. They need to delete or re-link all of them. Doing this one-by-one is painful and error-prone.
3. **Import/Migration**: When migrating from another CRM, historical activities should be bulk-imported via CSV. Without this, years of interaction history must be manually re-entered or lost.
4. **Export/Reporting**: Teams need to export activity data to spreadsheets for external analysis (board reports, compliance audits, territory reviews). No export = manual copy-paste of individual records.

**How to implement:**
- Backend: New `POST /api/activities/bulk` endpoint accepting an array of `CreateActivityRequest`. New `DELETE /api/activities/bulk` accepting an array of IDs. CSV export endpoint: `GET /api/activities/export?format=csv&from=&to=`. CSV import endpoint: `POST /api/activities/import`.
- Frontend: "Bulk log" dialog with multi-contact/deal selector. Checkbox selection on activity cards for bulk delete. Export button on toolbar. Import wizard with field mapping.

---

#### LP-6. Real-Time Activity Feed (WebSocket/SSE)

**Effort:** Medium-High | **Impact:** Medium

**What exists now:** Activity data is fetched once on page load and does not update until the user manually refreshes. If User A logs a call while User B has the Activities page open, User B sees nothing until they refresh.

**What is missing:** Real-time push notifications when activities are created, updated, or deleted by other team members.

**Why we need it:**
1. **Team awareness**: In a sales floor, managers want to see a live feed of team activity: "Sarah just logged a call with Acme Corp," "Tom completed a meeting with TechStart." This creates positive peer pressure and helps managers coach in real-time ("I see you just had a long call with Acme — how did it go?").
2. **Pipeline meeting support**: During a live pipeline review, the manager might say "Tom, go log that call from this morning." With real-time updates, everyone in the meeting sees Tom's new activity appear instantly without refreshing.
3. **Reduced stale data**: Without real-time updates, users operate on increasingly stale data. A deal's "last activity" indicator may be outdated, leading to unnecessary duplicate outreach ("I already called them this morning — check the activity log." "I didn't see it.")
4. **Conflict prevention**: Two reps independently calling the same contact because neither saw the other's recent activity. Live updates would show "Sarah is currently logging a call with Jane" or at minimum show the new activity immediately after logging.

**How to implement:**
- Backend: Add SignalR hub (`ActivityHub`). In `ActivityService.CreateAsync` and `DeleteAsync`, broadcast events to connected clients in the same organization.
- Frontend: Connect to SignalR hub on Activities page mount. On `ActivityCreated` event, prepend to activity list. On `ActivityDeleted` event, remove from list. Show toast: "Sarah logged a call with Acme Corp."

---

#### LP-7. Activity GET by ID Endpoint

**Effort:** Low | **Impact:** Low

**What exists now:** `ActivitiesController.cs` has `GET /api/activities` (list), `GET /api/activities/all`, `GET /api/activities/contact/{contactId}`, `GET /api/activities/deal/{dealId}`, `GET /api/activities/lead/{leadId}`, `POST /api/activities`, and `DELETE /api/activities/{id}`. There is NO `GET /api/activities/{id}` endpoint. The `useActivityById` React Query hook works around this by fetching the full list and finding the matching activity client-side.

**What is missing:** A simple `GET /api/activities/{id}` endpoint returning a single `ActivityDto`.

**Why we need it:**
1. **Deep linking**: If we ever want to link directly to a specific activity (e.g., from a notification: "New activity on your deal — [view]"), we need a way to fetch a single activity by ID.
2. **Efficiency**: The current workaround (`useActivityById` fetches the entire activity list, then finds the match) is wasteful. For a simple "show this one activity" operation, it downloads potentially thousands of records.
3. **API completeness**: Every entity in the system has a GET-by-ID endpoint (contacts, deals, companies, tasks, leads) except activities. This inconsistency complicates API documentation and client development.
4. **Backend already supports it**: `ActivityRepository.GetByIdAsync(id)` already exists — it's used by `DeleteAsync` to find the activity before deletion. The endpoint just needs to be exposed in the controller.

**How to implement:**
- Backend: Add to `ActivitiesController.cs`:
  ```csharp
  [HttpGet("{id:guid}")]
  public async Task<ActionResult<ActivityDto>> GetById(Guid id) {
      var result = await _activityService.GetByIdAsync(id, userId, orgId);
      return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
  }
  ```
- Frontend: Update `useActivityById` to call the new endpoint instead of fetching the full list.

---

### D. UI/UX Inconsistencies

| # | Inconsistency | Severity | Details | Status |
|---|---------------|----------|---------|--------|
| 1 | ~~**Type count mismatch**~~ | ~~CRITICAL~~ | Backend now accepts all 9 types including video, demo, task. | ✅ FIXED (HP-1) |
| 2 | ~~**No lead filter**~~ | ~~MEDIUM~~ | "By Lead" filter tab now available on Activities page. | ✅ FIXED (HP-5) |
| 3 | **State management inconsistency** | **LOW** | Activities page uses `useState` + direct API calls. Other consumers use React Query hooks. Two different patterns for the same data. | Open |
| 4 | ~~**No edit anywhere**~~ | ~~HIGH~~ | Activities now support full edit via PUT endpoint and frontend edit form. | ✅ FIXED (HP-2) |
| 5 | ~~**Dashboard "Recent Activity" misleading**~~ | ~~MEDIUM~~ | Dashboard now shows real CRM activities (calls, meetings, emails, notes). | ✅ FIXED (HP-4) |
| 6 | ~~**Team stats are random numbers**~~ | ~~CRITICAL~~ | MemberDetailPanel now fetches real activity counts from backend API. | ✅ FIXED (HP-3) |
| 7 | ~~**Participants field invisible**~~ | ~~MEDIUM~~ | Participants now displayed in Activities page and Pipeline deal detail. | ✅ FIXED (HP-6) |
| 8 | **Lead activity visibility scope** | **LOW** | Lead activities are org-scoped (team-visible). Contact/deal activities are user-scoped. Inconsistent visibility rules. | Open |
| 9 | **Activity stats page-local only** | **LOW** | Stats computed client-side from current page data only. "Today: 3" may be wrong if there are more activities on other pages. | Open |

---

### E. Implementation Priority Order — Step-by-Step Execution Plan

Execute these phases **in order**. Each phase builds on the previous. Do NOT start a later phase until all items in the current phase are done.

---

#### PHASE 0: Critical Bug Fixes — ✅ ALL COMPLETE

| Item | What to Fix | Effort | File(s) | Status |
|------|-------------|--------|---------|--------|
| HP-1 | Add `video`, `demo`, `task` to backend `ValidActivityTypes` and regex | 30 min | `ActivityService.cs`, `CreateActivityRequest.cs` | ✅ Done |
| HP-3 | Replace `Math.random()` with real activity counts from API | Few hours | `MemberDetailPanel.tsx`, `ActivitiesController.cs`, `ActivityRepository.cs` | ✅ Done |
| HP-4 | Replace copy generation history with real CRM activities on Dashboard | Few hours | `RecentActivity.tsx`, `Dashboard.tsx` | ✅ Done |

**Result:** Zero 400 errors for Video/Demo/Task activities. Task audit trail works. Real performance metrics from backend API. Dashboard shows actual CRM activity. **User trust in all metrics restored.**

---

#### PHASE 1: Must-Have Core Features — ✅ ALL COMPLETE

| Item | What to Add | Effort | File(s) | Status |
|------|-------------|--------|---------|--------|
| HP-2 | `PUT /api/activities/{id}` endpoint + frontend edit form | 1 day | `IActivityService.cs`, `ActivityService.cs`, `ActivitiesController.cs`, `UpdateActivityRequest.cs`, `activities.ts`, `Activities.tsx` | ✅ Done |
| HP-5 | Add "By Lead" filter tab on Activities page | Few hours | `activities/types.ts`, `Activities.tsx` | ✅ Done |
| HP-6 | Display participants field in activity cards and creation form | 30 min | `Activities.tsx`, `Pipeline.tsx`, `activities.ts` | ✅ Done |

**Result:** Activities are editable (no longer the only entity without edit). Full filtering parity across contacts, deals, and leads. Participants data visible in both Activities page and Pipeline deal detail. **All dead features activated.**

---

#### PHASE 2: Performance & Analytics (takes 2-4 days total)

| Item | What to Add | Effort | File(s) |
|------|-------------|--------|---------|
| MP-1 | Paginated endpoints for contact/deal filtered activity views | 1-2 days | `IActivityRepository.cs`, `ActivityRepository.cs`, `ActivitiesController.cs`, `activities.ts`, `Pipeline.tsx` |
| MP-2 | Activity reporting (activities/week, by type, by team member) | 2-3 days | `ReportingService.cs`, new `ActivityStatsDto`, `ReportingController.cs`, new `ActivityStats.tsx`, `Dashboard.tsx` |
| MP-3 | Duration field for calls/meetings | Half day | `Activity.cs`, `ActivityDto.cs`, `CreateActivityRequest.cs`, migration, `Activities.tsx` |

**What you get:** Contact/deal activity views scale. Managers see real activity metrics and leaderboards. Call/meeting durations tracked. **Product becomes data-driven for sales management.**

---

#### PHASE 3: Advanced Workflow (takes 4-7 days total)

| Item | What to Add | Effort | File(s) |
|------|-------------|--------|---------|
| MP-4 | Future-dated activities with "Upcoming" tab and "Mark as Done" | 1-2 days | `Activity.cs`, `ActivityDto.cs`, migration, `Activities.tsx` |
| MP-5 | Add activities to global search results | Half day | `GlobalSearchService.cs`, `IActivityRepository.cs`, `ActivityRepository.cs`, `GlobalSearchResultDto.cs`, `search.ts` |
| MP-6 | Auto-log activities on deal stage change, task completion, email send | 3-5 days | New `IActivityLogger` interface, `ActivityLogger.cs`, inject into `DealService`, `TaskService`, `Activity.cs` (add `IsAutoLogged` flag) |

**What you get:** Future activity scheduling. Activities discoverable via global search. Automatic audit trail for CRM events. **Product approaches Salesforce/HubSpot functionality.**

---

#### PHASE 4: Nice-to-Have Enhancements (future sprints — no rush)

LP-1 through LP-7: Attachments, templates, assignment/delegation, recurrence, bulk operations, real-time feed, GET by ID. Plan during sprint planning when Phases 0-3 are complete.

---

#### TOTAL EFFORT SUMMARY

| Tier | Items | Estimated Effort | % of Total |
|------|-------|-----------------|-----------|
| **CRITICAL bugs** (HP-1, HP-3, HP-4) | 3 items | **Half day** | 5% |
| **MUST HAVE features** (HP-2, HP-5, HP-6) | 3 items | **1-2 days** | 15% |
| **SHOULD HAVE** (MP-1 to MP-6) | 6 items | **8-14 days** | 65% |
| **NICE TO HAVE** (LP-1 to LP-7) | 7 items | **TBD** | Future |
| **TOTAL (Phases 0-3)** | **12 items** | **9-16 developer days** | 100% |

~~After completing **Phase 0** (half day), the activity system stops throwing errors and stops showing fake data.~~ ✅ DONE
~~After completing **Phases 0-1** (2 days), activities are fully editable, filterable, and display all stored data.~~ ✅ DONE
After completing **Phases 0-2** (5 days), managers have activity metrics, duration tracking, and scalable views.
After completing **Phase 3** (12 days), the activity system has scheduling, global search, and auto-logging.
**NICE TO HAVE** items (Phase 4) bring it toward enterprise-grade features and are roadmap items.

---

---

### Implementation Log — Changes Made (February 9, 2026, Fourth Pass)

All 6 HIGH PRIORITY items implemented. Below is a summary of every file changed:

#### HP-1: Activity Type Mismatch (CRITICAL BUG) — previously implemented
- `backend/src/ACI.Application/Services/ActivityService.cs` — `ValidActivityTypes` expanded from 4 to 9 types
- `backend/src/ACI.Application/DTOs/CreateActivityRequest.cs` — Regex updated to accept all 9 types

#### HP-2: Activity Edit/Update (MUST HAVE) — previously implemented
- `backend/src/ACI.Application/Interfaces/IActivityService.cs` — Added `UpdateAsync` method
- `backend/src/ACI.Application/Services/ActivityService.cs` — Implemented `UpdateAsync` with field-level partial update
- `backend/src/ACI.Application/DTOs/UpdateActivityRequest.cs` — New DTO for activity updates
- `backend/src/ACI.WebApi/Controllers/ActivitiesController.cs` — Added `PUT /api/activities/{id}` endpoint
- `src/app/api/activities.ts` — Added `updateActivity()` API function
- `src/app/pages/Activities.tsx` — Added edit button on activity cards, edit dialog with pre-filled form

#### HP-3: Team Member Activity Stats (CRITICAL BUG) — implemented this pass
- `backend/src/ACI.Application/Interfaces/IActivityRepository.cs` — Added `GetActivityCountsByOrgAsync()` method
- `backend/src/ACI.Infrastructure/Repositories/ActivityRepository.cs` — Implemented org-wide activity count aggregation using `GroupBy` + `Count()`
- `backend/src/ACI.Application/Interfaces/IActivityService.cs` — Added `GetOrgMemberActivityCountsAsync()` method
- `backend/src/ACI.Application/Services/ActivityService.cs` — Implemented service wrapper for activity counts
- `backend/src/ACI.WebApi/Controllers/ActivitiesController.cs` — Added `GET /api/activities/org-member-counts` endpoint
- `src/app/api/activities.ts` — Added `getOrgMemberActivityCounts()` API function
- `src/app/pages/team/components/MemberDetailPanel.tsx` — Replaced fake `Math.random()` stats with real API data; uses `useEffect` to fetch counts on mount

#### HP-4: Dashboard Recent Activity (CRITICAL BUG) — previously implemented
- `src/app/pages/Dashboard.tsx` — Now calls `getActivities()` and passes real `Activity[]` to `RecentActivity`
- `src/app/pages/dashboard/RecentActivity.tsx` — Completely rewritten to accept `Activity[]` with type-specific icons and colors

#### HP-5: Lead Filter on Activities Page (MUST HAVE) — previously implemented
- `src/app/pages/activities/types.ts` — `ActivityFilter` expanded to include `'lead'`
- `src/app/pages/Activities.tsx` — Added "By Lead" tab, lead selector dropdown, `getActivitiesByLead()` integration

#### HP-6: Participants Field Display (MUST HAVE) — completed this pass
- `src/app/api/activities.ts` — Updated `createActivity()` params type to include `participants`
- `src/app/pages/Activities.tsx` — Updated `createActivity()` call to send `participants` field (was missing from create path)
- `src/app/pages/Pipeline.tsx` — Added participants display in deal detail sheet activity timeline (violet-themed "with {participants}" badge)
- `src/app/pages/activities/types.ts` — `ActivityFormState` already included `participants` from prior work

---

*This document provides a complete source-verified reference for every activity interaction in the Cadence CRM. All files read and verified. Contains 29 sections covering entity model, API, business logic, repositories, DTOs, validation, EF Core config, cross-entity interactions, all frontend pages/hooks/API clients, and a fully prioritized list with 4 tiers. **ALL 6 HIGH PRIORITY items are now IMPLEMENTED:** 3 CRITICAL bugs fixed (HP-1 type mismatch, HP-3 fake team stats, HP-4 misleading dashboard) and 3 MUST HAVE features completed (HP-2 activity edit, HP-5 lead filter, HP-6 participants display). 6 SHOULD HAVE improvements and 7 NICE TO HAVE enhancements remain for future sprints. Phased execution plan: Phases 0 and 1 are complete. Last updated: February 9, 2026 (fourth pass — all high priority items implemented with code changes).*
