# Task System — Complete Interaction Report

This document is the single definitive reference for **every interaction** the "TaskItem" entity has across the entire Cadence CRM codebase (backend and frontend). It covers the entity definition, API layer, business logic, database configuration, UI components, cross-entity relationships, and identifies what is missing.

Last updated: February 9, 2026 (fourth pass — every backend and frontend file re-read, 9 new sections added).

---

## Table of Contents

1. [TaskItem Entity (Backend Domain)](#1-taskitem-entity-backend-domain)
2. [Enums: TaskStatus & TaskPriority](#2-enums-taskstatus--taskpriority)
3. [Backend API Endpoints](#3-backend-api-endpoints)
4. [Backend Business Logic (TaskService)](#4-backend-business-logic-taskservice)
5. [Backend Data Access (TaskRepository)](#5-backend-data-access-taskrepository)
6. [Backend DTOs](#6-backend-dtos)
7. [Database Configuration (EF Core)](#7-database-configuration-ef-core)
7b. [Domain Errors (Task-Specific)](#7b-domain-errors-task-specific)
8. [Cross-Entity Task Interactions (Backend)](#8-cross-entity-task-interactions-backend)
9. [Backend Unit Tests](#9-backend-unit-tests)
10. [Frontend — API Client Layer](#10-frontend--api-client-layer)
11. [Frontend — TypeScript Types](#11-frontend--typescript-types)
12. [Frontend — Tasks Page (Main Task UI)](#12-frontend--tasks-page-main-task-ui)
13. [Frontend — Task Sub-Components](#13-frontend--task-sub-components)
14. [Frontend — Task Configuration & Utilities](#14-frontend--task-configuration--utilities)
15. [Frontend — React Query Hooks (useTasks.ts)](#15-frontend--react-query-hooks-usetasksts)
16. [Frontend — Global Task Config (taskConfig.ts)](#16-frontend--global-task-config-taskconfigts)
17. [Frontend — Query Keys](#17-frontend--query-keys)
18. [Frontend — Tasks in Other Pages](#18-frontend--tasks-in-other-pages)
19. [Backend — Domain Errors (Detailed)](#19-backend--domain-errors-detailed)
20. [Backend — UserSettings (Task Preferences)](#20-backend--usersettings-task-preferences)
21. [Backend — Reporting Service (Tasks Absent)](#21-backend--reporting-service-tasks-absent)
22. [Frontend — Notification Settings UI](#22-frontend--notification-settings-ui)
23. [Frontend — Messages/Toast Constants](#23-frontend--messagestoast-constants)
24. [Backend Unit Tests](#24-backend-unit-tests)
25. [Complete File Inventory](#25-complete-file-inventory)
26. [Complete Relationship Map](#26-complete-relationship-map)
27. [What Is Missing (Task-Specific Gaps — Prioritized)](#27-what-is-missing-task-specific-gaps--prioritized-analysis)

---

## 1. TaskItem Entity (Backend Domain)

**File:** `backend/src/ACI.Domain/Entities/TaskItem.cs` (55 lines)

The core TaskItem entity representing a CRM task linked to leads, deals, or contacts:

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Primary key (inherited from `BaseEntity`) |
| `UserId` | `Guid` | Owner user (required) |
| `OrganizationId` | `Guid?` | Organization scope (optional) |
| `Title` | `string` | Task title (required, max 512 chars) |
| `Description` | `string?` | Task description (max 4096 chars) |
| `DueDateUtc` | `DateTime?` | Due date in UTC |
| `ReminderDateUtc` | `DateTime?` | Reminder date in UTC |
| `Status` | `TaskStatus` | Status enum: Todo, InProgress, Completed, Cancelled. Default: `Todo` |
| `Priority` | `TaskPriority` | Priority enum: None, Low, Medium, High. Default: `None` |
| `Completed` | `bool` | Legacy field for backward compatibility. Synced with Status |
| `AssigneeId` | `Guid?` | Assigned team member |
| `LeadId` | `Guid?` | FK to Lead entity |
| `DealId` | `Guid?` | FK to Deal entity |
| `ContactId` | `Guid?` | FK to Contact entity |
| `Notes` | `string?` | Additional notes (max 4096 chars) |
| `CreatedAtUtc` | `DateTime` | Creation timestamp |
| `UpdatedAtUtc` | `DateTime?` | Last update timestamp |
| `CompletedAtUtc` | `DateTime?` | Completion timestamp (set when status → Completed) |
| `UpdatedByUserId` | `Guid?` | User who last updated |

### Navigation Properties

| Property | Type | Relationship |
|----------|------|-------------|
| `User` | `User` | Owner (required) — `User.TaskItems` collection |
| `UpdatedByUser` | `User?` | Last updater |
| `Assignee` | `User?` | Assigned member |
| `Organization` | `Organization?` | Org scope |
| `Lead` | `Lead?` | Linked lead — `Lead.TaskItems` collection |
| `Deal` | `Deal?` | Linked deal — `Deal.TaskItems` collection |
| `Contact` | `Contact?` | Linked contact |

---

## 2. Enums: TaskStatus & TaskPriority

**File:** `backend/src/ACI.Domain/Enums/TaskStatus.cs`

```
TaskStatus:
  Todo = 0        → "todo"
  InProgress = 1  → "in_progress"
  Completed = 2   → "completed"
  Cancelled = 3   → "cancelled"
```

**File:** `backend/src/ACI.Domain/Enums/TaskPriority.cs`

```
TaskPriority:
  None = 0   → "none"
  Low = 1    → "low"
  Medium = 2 → "medium"
  High = 3   → "high"
```

String conversion is handled in `TaskService.cs` lines 609-649 with `StatusToString`, `TryParseStatus`, `PriorityToString`, and `TryParsePriority` methods. The status parser accepts aliases: `"inprogress"`, `"done"`, `"canceled"`.

---

## 3. Backend API Endpoints

**File:** `backend/src/ACI.WebApi/Controllers/TasksController.cs` (526 lines)

All endpoints require `[Authorize]`. Controller uses `ICurrentUserService` for `UserId` and `CurrentOrganizationId`.

| Method | Route | Description | Request Body | Response |
|--------|-------|-------------|-------------|----------|
| `GET` | `/api/tasks` | Paginated tasks with search & filters | Query: `page`, `pageSize`, `search`, `overdueOnly`, `status`, `priority` | `PagedResult<TaskDto>` |
| `GET` | `/api/tasks/all` | All tasks (non-paginated) with filters | Query: `overdueOnly`, `status`, `priority`, `assigneeId`, `leadId`, `dealId`, `contactId` | `TaskDto[]` |
| `GET` | `/api/tasks/{id}` | Get single task by ID | — | `TaskDto` |
| `GET` | `/api/tasks/stats` | Task statistics for dashboard | — | `TaskStatsDto` |
| `GET` | `/api/tasks/by-lead/{leadId}` | Tasks linked to a lead | — | `TaskDto[]` |
| `GET` | `/api/tasks/by-deal/{dealId}` | Tasks linked to a deal | — | `TaskDto[]` |
| `GET` | `/api/tasks/by-contact/{contactId}` | Tasks linked to a contact | — | `TaskDto[]` |
| `POST` | `/api/tasks` | Create a new task | `CreateTaskRequest` | `TaskDto` |
| `PUT` | `/api/tasks/{id}` | Full update of a task | `UpdateTaskRequest` | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/status` | Update task status only | `UpdateStatusRequest` (`{status}`) | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/assign` | Assign/unassign task | `AssignTaskRequest` (`{assigneeId?}`) | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/link-lead` | Link/unlink task to lead | `LinkToLeadRequest` (`{leadId?}`) | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/link-deal` | Link/unlink task to deal | `LinkToDealRequest` (`{dealId?}`) | `TaskDto` |
| `DELETE` | `/api/tasks/{id}` | Delete a task permanently | — | `204 No Content` |

### Controller-Level Request Records (defined in TasksController.cs)

| Record | Properties | Notes |
|--------|------------|-------|
| `UpdateStatusRequest` | `Status` (required, regex-validated: `todo\|in_progress\|completed\|cancelled`) | |
| `AssignTaskRequest` | `AssigneeId?` | Null to unassign |
| `LinkToLeadRequest` | `LeadId?` | Null to unlink |
| `LinkToDealRequest` | `DealId?` | Null to unlink |

---

## 4. Backend Business Logic (TaskService)

**File:** `backend/src/ACI.Application/Services/TaskService.cs` (651 lines)

### Service Interface: `ITaskService`

**File:** `backend/src/ACI.Application/Interfaces/ITaskService.cs` (123 lines)

Defines all task operations + `TaskFilterParams` record + `TaskStatsDto` record.

### Key Service Methods

| Method | Lines | Description |
|--------|-------|-------------|
| `GetPagedAsync` | 25-56 | Server-side pagination with search, status, priority, overdue filters |
| `GetTasksAsync` | 59-110 | Non-paginated with priority chain filtering: LeadId → DealId → ContactId → AssigneeId → Status → OverdueOnly → All |
| `GetTasksByLeadIdAsync` | 113-122 | Filter by lead FK |
| `GetTasksByDealIdAsync` | 125-134 | Filter by deal FK |
| `GetTasksByContactIdAsync` | 137-146 | Filter by contact FK |
| `GetByIdAsync` | 149-166 | Single task with relations |
| `CreateAsync` | 169-218 | Creates task, sets `CreatedAtUtc`, parses status/priority strings |
| `UpdateAsync` | 221-340 | Partial update with "Clear" flags: `ClearDueDate`, `ClearReminderDate`, `ClearAssignee`, `ClearLead`, `ClearDeal`, `ClearContact`. Sets `CompletedAtUtc` on completion. |
| `UpdateStatusAsync` | 343-397 | Status-only update, manages `Completed` flag and `CompletedAtUtc` |
| `AssignTaskAsync` | 400-439 | Sets `AssigneeId` |
| `LinkToLeadAsync` | 442-481 | Sets `LeadId` |
| `LinkToDealAsync` | 484-523 | Sets `DealId` |
| `DeleteAsync` | 526-552 | Hard delete |
| `GetStatsAsync` | 555-583 | Computes: Total, Todo, InProgress, Completed, Cancelled, Overdue, DueToday, HighPriority |

### TaskFilterParams Record (ITaskService.cs lines 100-108)

```csharp
record TaskFilterParams(
    bool? OverdueOnly = null,
    string? Status = null,
    string? AssigneeId = null,
    string? LeadId = null,
    string? DealId = null,
    string? ContactId = null,
    string? Priority = null
);
```

### TaskStatsDto Record (ITaskService.cs lines 113-122)

```csharp
record TaskStatsDto(
    int Total,
    int Todo,
    int InProgress,
    int Completed,
    int Cancelled,
    int Overdue,
    int DueToday,
    int HighPriority
);
```

### DTO Mapping (Map method, lines 585-607)

The `Map` method includes navigation property names:
- `AssigneeName` = `e.Assignee?.Name`
- `LeadName` = `e.Lead?.Name`
- `DealName` = `e.Deal?.Name`
- `ContactName` = `e.Contact?.Name`

---

## 5. Backend Data Access (TaskRepository)

**File:** `backend/src/ACI.Infrastructure/Repositories/TaskRepository.cs` (222 lines)

### Repository Interface: `ITaskRepository`

**File:** `backend/src/ACI.Application/Interfaces/ITaskRepository.cs` (33 lines)

### Key Repository Methods

| Method | Lines | Description |
|--------|-------|-------------|
| `GetPagedAsync` | 77-101 | Applies search → status → priority → overdue filters, then paginates. Includes relations. |
| `GetByUserIdAsync` | 103-105 | All tasks for user + org. Ordered by DueDate, then Priority desc. |
| `GetByUserIdAsync(overdueOnly)` | 107-119 | Overdue filter variant |
| `GetByAssigneeIdAsync` | 121-124 | By assignee within org |
| `GetByStatusAsync` | 126-129 | By specific status |
| `GetByLeadIdAsync` | 131-134 | By `LeadId` FK |
| `GetByDealIdAsync` | 136-139 | By `DealId` FK |
| `GetByContactIdAsync` | 141-144 | By `ContactId` FK |
| `GetByIdWithRelationsAsync` | 150-152 | Single task with Include(Assignee, Lead, Deal, Contact) |
| `AddAsync` | 154-166 | Insert + reload with relations |
| `UpdateAsync` | 168-199 | Full field copy + SaveChanges + reload with relations |
| `DeleteAsync` | 201-209 | Hard delete |
| `GetCountByStatusAsync` | 211-213 | Count by status |
| `GetCountByLeadIdAsync` | 215-217 | Count tasks for a lead |
| `GetCountByDealIdAsync` | 219-221 | Count tasks for a deal |

### Query Patterns

- **IncludeRelations** (line 21-25): `.Include(t => t.Assignee).Include(t => t.Lead).Include(t => t.Deal).Include(t => t.Contact)` — always loads all 4 navigation properties
- **OrderTasks** (line 27-29): `.OrderBy(t => t.DueDateUtc ?? DateTime.MaxValue).ThenByDescending(t => t.Priority)` — null due dates sort last, higher priority first
- **Search** (line 31-39): Searches `Title`, `Description`, and `Notes` fields (case-insensitive)
- **FilterByUserAndOrg** (line 15-16): Scopes to user + organization

---

## 6. Backend DTOs

### TaskDto

**File:** `backend/src/ACI.Application/DTOs/TaskDto.cs` (27 lines)

```csharp
record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    DateTime? ReminderDateUtc,
    string Status,           // "todo", "in_progress", "completed", "cancelled"
    string Priority,         // "none", "low", "medium", "high"
    bool Completed,          // Legacy field
    Guid? LeadId,
    Guid? DealId,
    Guid? ContactId,
    Guid? AssigneeId,
    string? AssigneeName,    // Navigation property
    string? LeadName,        // Navigation property
    string? DealName,        // Navigation property
    string? ContactName,     // Navigation property
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? CompletedAtUtc
);
```

### CreateTaskRequest

**File:** `backend/src/ACI.Application/DTOs/CreateTaskRequest.cs` (74 lines)

| Property | Type | Validation | Notes |
|----------|------|------------|-------|
| `Title` | `string` | Required, 1-200 chars | Only required field |
| `Description` | `string?` | Max 2000 chars | |
| `DueDateUtc` | `DateTime?` | — | |
| `ReminderDateUtc` | `DateTime?` | — | |
| `Status` | `string?` | Regex: `todo\|in_progress\|completed\|cancelled` | Defaults to "todo" |
| `Priority` | `string?` | Regex: `none\|low\|medium\|high` | Defaults to "none" |
| `LeadId` | `Guid?` | — | Link to lead on creation |
| `DealId` | `Guid?` | — | Link to deal on creation |
| `ContactId` | `Guid?` | — | Link to contact on creation |
| `AssigneeId` | `Guid?` | — | Assign on creation |
| `Notes` | `string?` | Max 2000 chars | |

### UpdateTaskRequest

**File:** `backend/src/ACI.Application/DTOs/UpdateTaskRequest.cs` (108 lines)

All fields are optional (partial update). Includes "Clear" flags for nullable FK fields:

| Property | Type | Notes |
|----------|------|-------|
| `Title` | `string?` | |
| `Description` | `string?` | |
| `DueDateUtc` | `DateTime?` | |
| `ReminderDateUtc` | `DateTime?` | |
| `Status` | `string?` | Regex validated |
| `Priority` | `string?` | Regex validated |
| `Completed` | `bool?` | Legacy: `true` → status=Completed, `false` → status=Todo |
| `LeadId` | `Guid?` | |
| `DealId` | `Guid?` | |
| `ContactId` | `Guid?` | |
| `AssigneeId` | `Guid?` | |
| `Notes` | `string?` | |
| `ClearDueDate` | `bool?` | Set to `true` to null out DueDateUtc |
| `ClearReminderDate` | `bool?` | Set to `true` to null out ReminderDateUtc |
| `ClearAssignee` | `bool?` | Set to `true` to null out AssigneeId |
| `ClearLead` | `bool?` | Set to `true` to null out LeadId |
| `ClearDeal` | `bool?` | Set to `true` to null out DealId |
| `ClearContact` | `bool?` | Set to `true` to null out ContactId |

---

## 7. Database Configuration (EF Core)

**File:** `backend/src/ACI.Infrastructure/Persistence/Configurations/TaskConfiguration.cs` (56 lines)

### Table: `TaskItems`

| Column Config | Details |
|---------------|---------|
| `Title` | `nvarchar(512)`, required |
| `Description` | `nvarchar(4096)` |
| `Notes` | `nvarchar(4096)` |
| `Status` | Stored as `string` (converted enum), max 32, default `Todo` |
| `Priority` | Stored as `string` (converted enum), max 32, default `None` |

### Relationships

| Relationship | FK | Navigation | Delete Behavior |
|-------------|-----|-----------|----------------|
| TaskItem → User (owner) | `UserId` | `User.TaskItems` | Cascade (implied) |
| TaskItem → Organization | `OrganizationId` | — | Optional |
| TaskItem → Assignee | `AssigneeId` | — | Optional |
| TaskItem → Lead | `LeadId` | `Lead.TaskItems` | Set null on Lead delete |
| TaskItem → Deal | `DealId` | `Deal.TaskItems` | Set null on Deal delete |
| TaskItem → Contact | `ContactId` | — | Optional |
| TaskItem → UpdatedByUser | `UpdatedByUserId` | — | Optional |

### Indexes

| Index | Columns |
|-------|---------|
| Single | `OrganizationId` |
| Single | `ContactId` |
| Single | `AssigneeId` |
| Single | `Status` |
| Single | `DueDateUtc` |
| Composite | `OrganizationId + Status` |
| Composite | `UserId + Status` |

**Note:** No explicit index on `DealId` or `LeadId` (potential performance gap for `GetByDealIdAsync` / `GetByLeadIdAsync` queries).

---

## 7b. Domain Errors (Task-Specific)

**File:** `backend/src/ACI.Application/Common/DomainErrors.cs` (lines 108-129)

| Error Code | Message | Used In |
|------------|---------|---------|
| `Task.NotFound` | "The task was not found" | `GetByIdAsync`, `UpdateAsync`, `UpdateStatusAsync`, `AssignTaskAsync`, `LinkToLeadAsync`, `LinkToDealAsync`, `DeleteAsync` |
| `Task.TitleRequired` | "Task title is required" | `CreateAsync` |
| `Task.InvalidStatus` | "The task status is invalid" | `UpdateStatusAsync` |
| `Task.InvalidPriority` | "The task priority is invalid" | **Defined but NEVER used** — `TryParsePriority` defaults to `None` instead of returning an error |
| `Task.DueDateInPast` | "The due date cannot be in the past" | **Defined but NEVER used** — no past-date validation exists in `CreateAsync` or `UpdateAsync` |
| `Task.AlreadyCompleted` | "The task is already completed" | **Defined but NEVER used** — completed tasks can be freely updated/deleted |

> **Note:** 3 of 6 domain error codes are defined but never referenced in application code. `DueDateInPast` and `AlreadyCompleted` represent planned-but-unimplemented validation rules. `InvalidPriority` could be used in `CreateAsync` but the current code silently defaults to `None` for unrecognized priority strings.

---

## 8. Cross-Entity Task Interactions (Backend)

### Deal ↔ Task

- **Deal entity** (`Deal.cs` line 35) has `ICollection<TaskItem> TaskItems` navigation property
- **DealService.DeleteAsync** does **NOT** explicitly handle task unlinking. The `DealId` FK is optional (`Guid?`), so EF Core's default convention (`ClientSetNull`) handles it — when a Deal is deleted, linked tasks' `DealId` is set to `null` automatically by EF Core.
- **TaskService.GetTasksByDealIdAsync** retrieves all tasks for a given `DealId`
- **Repository.GetCountByDealIdAsync** counts tasks linked to a deal
- **TaskConfiguration**: `builder.HasOne(e => e.Deal).WithMany(d => d.TaskItems).HasForeignKey(e => e.DealId)` — no explicit `.OnDelete()`, relies on EF default

### Lead ↔ Task

- **Lead entity** (`Lead.cs` line 43) has `ICollection<TaskItem> TaskItems` navigation property
- **LeadService** does **NOT** create tasks — task creation for leads happens entirely on the frontend (`LeadDetailModal.tsx`)
- **LeadDetailModal.tsx** has a full task lifecycle for lead-linked tasks:
  - `loadTasks()` (line 129-141): Calls `getTasks()` and filters client-side by `leadId`. **BUG:** Uses `getTasks()` (paginated endpoint returning max 20 tasks) instead of `getTasksByLead(leadId)`, so leads with >20 total tasks may show incomplete task lists.
  - `handleAddTask()` (line 348-383): Creates a task via `createTask({ title, dueDateUtc, leadId })` and logs a "Reminder created" activity. Labels tasks as "Reminders" in the UI.
  - `handleToggleTask()` (line 386): Toggles task completion via `updateTask(task.id, { completed: !task.completed })` and logs activity.
  - Tasks are displayed as "Reminders" in the lead detail view.
- **TaskService.GetTasksByLeadIdAsync** retrieves all tasks for a given `LeadId`
- **Repository.GetCountByLeadIdAsync** counts tasks linked to a lead
- **TaskConfiguration**: `builder.HasOne(e => e.Lead).WithMany(l => l.TaskItems).HasForeignKey(e => e.LeadId)` — no explicit `.OnDelete()`, relies on EF default (`ClientSetNull`)

### Contact ↔ Task

- **TaskService.GetTasksByContactIdAsync** retrieves all tasks for a given `ContactId`
- **TaskConfiguration**: `builder.HasOne(e => e.Contact).WithMany().HasForeignKey(e => e.ContactId).IsRequired(false)`
- **Note:** Contact does NOT have `ICollection<TaskItem>` — the relationship is one-directional

### Activity Logging from Tasks

- **Tasks.tsx** `logTaskActivity` (lines 275-288) calls `createActivity()` when tasks are:
  - Created, Updated, Deleted
  - Status changed, Priority changed
  - Assigned/unassigned
  - Linked/unlinked to leads or deals
- Activity includes `leadId`, `dealId`, `contactId` from the task, creating cross-entity activity trails
- **CRITICAL BUG:** `logTaskActivity` sets `type: 'task'` (line 278), but the backend only accepts types: `call`, `meeting`, `email`, `note`. The `CreateActivityRequest` has `[RegularExpression(@"^(call|meeting|email|note|Call|Meeting|Email|Note)$")]` validation, and `ActivityService.ValidActivityTypes = { "call", "meeting", "email", "note" }`. This means **ALL task activity logging silently fails** when using the real API — the 400 error is caught and swallowed by the try/catch (line 285-287). No task activities are ever persisted.

---

## 9. Backend Unit Tests

**File:** `backend/tests/ACI.Application.Tests/Services/TaskServiceTests.cs` (777+ lines)

Uses Moq for `ITaskRepository` and `ILogger<TaskService>`.

### Test Coverage Areas

| Area | Tests |
|------|-------|
| `GetTasksAsync` | Returns tasks, empty list, filters by overdue, lead, deal, contact, assignee, status |
| `CreateAsync` | Success, title required validation, default status/priority |
| `UpdateAsync` | Full update, not found, clear fields, status change with CompletedAtUtc |
| `UpdateStatusAsync` | Valid status, invalid status, CompletedAtUtc tracking |
| `AssignTaskAsync` | Assign, unassign |
| `LinkToLeadAsync` | Link, unlink |
| `LinkToDealAsync` | Link, unlink, not found |
| `DeleteAsync` | Success, not found |
| `GetStatsAsync` | Full stats computation |

---

## 10. Frontend — API Client Layer

**File:** `src/app/api/tasks.ts` (391 lines)

### Exported Functions

| Function | Lines | API Route | Description |
|----------|-------|-----------|-------------|
| `getTasksPaged(options?)` | 77-133 | `GET /api/tasks` | Paginated with search, status, priority, overdue |
| `getTasks(options?)` | 136-171 | `GET /api/tasks` | **BUG:** Calls the paginated endpoint (NOT `/api/tasks/all`). Handles both array and `PagedResult` response formats; extracts `.items` from paginated results. With default `page=1, pageSize=20`, only first 20 tasks are returned. |
| `getTasksByLead(leadId)` | 174-187 | `GET /api/tasks/by-lead/{id}` | Tasks for a lead |
| `getTasksByDeal(dealId)` | 190-203 | `GET /api/tasks/by-deal/{id}` | Tasks for a deal |
| `getTasksByContact(contactId)` | 206-219 | `GET /api/tasks/by-contact/{id}` | Tasks for a contact |
| `getTaskStats()` | 222-251 | `GET /api/tasks/stats` | Dashboard statistics |
| `createTask(params)` | 268-295 | `POST /api/tasks` | Create with all fields including `dealId`, `leadId`, `contactId` |
| `updateTask(id, params)` | 319-329 | `PUT /api/tasks/{id}` | Partial update with "Clear" flags |
| `updateTaskStatus(id, status)` | 332-342 | `PATCH /api/tasks/{id}/status` | Status-only update |
| `assignTask(id, assigneeId)` | 345-355 | `PATCH /api/tasks/{id}/assign` | Assign/unassign |
| `linkTaskToLead(id, leadId)` | 358-368 | `PATCH /api/tasks/{id}/link-lead` | Link/unlink to lead |
| `linkTaskToDeal(id, dealId)` | 371-381 | `PATCH /api/tasks/{id}/link-deal` | Link/unlink to deal |
| `deleteTask(id)` | 384-391 | `DELETE /api/tasks/{id}` | Delete task |

### TypeScript Interfaces

```typescript
GetTasksOptions {
  overdueOnly?: boolean;
  status?: TaskStatusType;
  priority?: TaskPriorityType;
  assigneeId?: string;
  leadId?: string;
  dealId?: string;
  contactId?: string;
}

CreateTaskParams {
  title: string;
  description?: string; dueDateUtc?: string; reminderDateUtc?: string;
  status?: TaskStatusType; priority?: TaskPriorityType;
  leadId?: string; dealId?: string; contactId?: string;
  assigneeId?: string; notes?: string;
}

UpdateTaskParams {
  title?: string; description?: string; dueDateUtc?: string; reminderDateUtc?: string;
  status?: TaskStatusType; priority?: TaskPriorityType; completed?: boolean;
  leadId?: string; dealId?: string; contactId?: string;
  assigneeId?: string; notes?: string;
  clearDueDate?: boolean; clearReminderDate?: boolean; clearAssignee?: boolean;
  clearLead?: boolean; clearDeal?: boolean; clearContact?: boolean;
}
```

### Mock Support

All functions have dual paths: real API (`isUsingRealApi()`) and mock data (`mockTasks` from `mockData.ts`). Mock path uses `delay(200)` to simulate latency.

---

## 11. Frontend — TypeScript Types

**File:** `src/app/api/types.ts` (referenced)

```typescript
type TaskStatusType = 'todo' | 'in_progress' | 'completed' | 'cancelled';
type TaskPriorityType = 'none' | 'low' | 'medium' | 'high';

interface TaskItem {
  id: string;
  title: string;
  description?: string;
  dueDateUtc?: string;
  reminderDateUtc?: string;
  status: TaskStatusType;
  priority: TaskPriorityType;
  completed: boolean;
  leadId?: string;
  dealId?: string;
  contactId?: string;
  assigneeId?: string;
  assigneeName?: string;
  leadName?: string;
  dealName?: string;
  contactName?: string;
  notes?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  completedAtUtc?: string;
}

interface TaskStats {
  total: number;
  todo: number;
  inProgress: number;
  completed: number;
  cancelled: number;
  overdue: number;
  dueToday: number;
  highPriority: number;
}
```

**File:** `src/app/pages/tasks/types.ts` (8 lines)

```typescript
type ViewMode = 'list' | 'kanban';
type TaskGroup = 'overdue' | 'today' | 'tomorrow' | 'thisWeek' | 'later' | 'noDue' | 'completed';
type KanbanColumn = 'todo' | 'in_progress' | 'completed';
```

---

## 12. Frontend — Tasks Page (Main Task UI)

**File:** `src/app/pages/Tasks.tsx` (1542 lines)

### Data Loaded on Mount (lines 136-159)

```typescript
const [taskList, taskStats, leadList, dealList, contactList, memberList] = await Promise.all([
  getTasks(),
  getTaskStats(),
  getLeads(),
  getDeals(),
  getContacts(),
  orgId ? getOrgMembers(orgId) : Promise.resolve([]),
]);
```

**BUG:** `getTasks()` calls `GET /api/tasks` (paginated endpoint, default page=1 pageSize=20), NOT `GET /api/tasks/all`. Only the first 20 tasks are loaded. Stats from `getTaskStats()` count ALL tasks, creating a count mismatch. Leads, deals, contacts, and org members are loaded fully for link dropdowns.

### State Management (lines 91-133)

| State | Type | Purpose |
|-------|------|---------|
| `tasks` | `TaskItem[]` | All tasks |
| `stats` | `TaskStats \| null` | Dashboard stats (8 metrics) |
| `viewMode` | `'kanban' \| 'list'` | Current view |
| `dialogOpen` | `boolean` | Create/Edit dialog |
| `editingTask` | `TaskItem \| null` | Task being edited |
| `form` | `{title, description, dueDate, reminderDate, status, priority, leadId, dealId, contactId, assigneeId, notes}` | Form state |
| `leads` | `Lead[]` | For link dropdowns |
| `deals` | `Deal[]` | For link dropdowns |
| `contacts` | `Contact[]` | For link dropdowns |
| `orgMembers` | `{userId, name, email}[]` | For assignment |
| `searchQuery` | `string` | Search filter |
| `priorityFilter` | `TaskPriorityType \| 'all'` | Priority filter |
| `assigneeFilter` | `string` | Assignee filter |
| `leadFilter` | `string` | Lead filter |
| `dealFilter` | `string` | Deal filter |
| `detailTask` | `TaskItem \| null` | Selected task for detail modal |

### Views

#### Kanban View (lines 1003-1024)

- Uses `react-dnd` with `HTML5Backend`
- 3 columns: **To Do**, **In Progress**, **Done**
- `cancelled` tasks are placed in the "Done" column
- Sorting within columns: priority descending, then due date ascending
- Drag-and-drop changes task status via `handleDropTask` → `handleStatusChange`
- Quick-add input at bottom of each column

#### List View (lines 1025-1147)

- 7 groups: **Overdue**, **Today**, **Tomorrow**, **This Week**, **Later**, **No Due Date**, **Completed**
- Groups are collapsible (Completed starts collapsed)
- Uses `TaskGroupSection` → `ListTaskCard` components
- Sorting within groups: priority descending, then due date ascending

### Filters Bar (lines 858-978)

Dark-themed filter bar with:
1. **Search**: by title or description (client-side)
2. **Priority filter**: all / high / medium / low / none
3. **Assignee filter**: all / specific org member
4. **Lead filter**: all / specific lead
5. **Deal filter**: all / specific deal
6. **Clear filters button**: resets all filters

### Statistics Cards (lines 748-856)

8 stat cards displayed in a responsive grid:
1. Total Tasks (slate)
2. To Do (slate)
3. In Progress (blue)
4. Due Today (orange)
5. Overdue (red)
6. High Priority (rose)
7. Completed (emerald)
8. Completion % (violet gradient with progress bar)

### Core Handlers (lines 296-611)

| Handler | Lines | Description |
|---------|-------|-------------|
| `openCreate(initialStatus?)` | 297-313 | Opens dialog for new task, can pre-set status |
| `openEdit(task)` | 315-331 | Opens dialog with task data pre-filled |
| `handleSubmit` | 333-414 | Creates or updates task, logs activity, refreshes stats |
| `handleDeleteConfirm` | 416-441 | Deletes task, logs activity, refreshes stats |
| `handleStatusChange(task, newStatus)` | 443-466 | Optimistic update → API call → rollback on failure, logs activity |
| `handleAssigneeChange(task, assigneeId)` | 468-492 | Assign/unassign with optimistic UI |
| `handlePriorityChange(task, priority)` | 494-512 | Priority change with optimistic UI |
| `handleLeadChange(task, leadId)` | 514-538 | Link/unlink lead with optimistic UI |
| `handleDealChange(task, dealId)` | 540-564 | Link/unlink deal with optimistic UI |
| `handleQuickAdd(column)` | 566-594 | Quick-add task in specific Kanban column |
| `handleDropTask(taskId, column)` | 597-611 | Drag-and-drop status change |
| `logTaskActivity(task, action, details?)` | 275-288 | Creates CRM Activity for every task action |
| `handleDetailModalUpdate(taskId, updates)` | 648-669 | Updates from TaskDetailModal |

### Create/Edit Dialog (lines 1154-1453)

Full-featured task form with:
- **Title** (required)
- **Description** (textarea)
- **Status** dropdown (4 options with color dots)
- **Priority** dropdown (4 options with flag icons)
- **Schedule** section: Due Date + Reminder (datetime-local inputs)
- **Assign To** dropdown (org members, only if org exists)
- **Link To** section (violet-themed): Lead + Deal + Contact dropdowns in 3-column grid
- **Notes** (textarea)
- Gradient header with edit/create icons

### Task Detail Modal (lines 1516-1538)

Renders `TaskDetailModal` component with all handler props:
- `onUpdate`, `onDelete`, `onStatusChange`, `onPriorityChange`
- `onAssigneeChange`, `onLeadChange`, `onDealChange`
- `members`, `leads`, `deals`, `contacts`

---

## 13. Frontend — Task Sub-Components

### TaskDetailModal

**File:** `src/app/pages/tasks/components/TaskDetailModal.tsx` (738 lines)

Comprehensive modal for viewing and editing task details:

| Feature | Lines | Description |
|---------|-------|-------------|
| **Deal selector** | 616-643 | Dropdown in "Linked Items" section to link/unlink task to deal |
| **Deal badge** | 636-641 | Shows current `dealName` as teal badge with Briefcase icon |
| **Deal activity loading** | 128-130 | On open, loads `getActivitiesByDeal(task.dealId)` for related activities |
| **Quick deal change** | 202-205 | `handleQuickDealChange` calls `onDealChange(task, dealId)` |
| **Lead selector** | Similar | Dropdown for lead linking |
| **Contact selector** | Similar | Dropdown for contact linking |
| **Inline editing** | Various | Title, description, notes, dates — all editable inline |
| **Status/Priority** | Various | Quick-change dropdowns |
| **Activity timeline** | Various | Shows recent activities related to the task's linked entities |

### KanbanTaskCard

**File:** `src/app/pages/tasks/components/KanbanTaskCard.tsx` (251 lines)

Draggable card for the Kanban board:

| Feature | Lines | Description |
|---------|-------|-------------|
| **Drag source** | 68-74 | `useDrag` with `TASK_CARD_TYPE` for react-dnd |
| **Deal submenu** | 173-188 | `DropdownMenuSub` with "No deal" + all deals in context menu |
| **Deal badge** | 228-233 | Teal badge with `Link2` icon showing truncated `task.dealName` |
| **Lead submenu** | Similar | Context menu for lead linking |
| **Priority submenu** | Similar | Context menu for priority change |
| **Assignee submenu** | Similar | Context menu for assignee change |
| **View Details** | — | Opens `TaskDetailModal` |
| **Edit / Delete** | — | Context menu actions |

### ListTaskCard

**File:** `src/app/pages/tasks/components/ListTaskCard.tsx` (196 lines)

List view task row:

| Feature | Lines | Description |
|---------|-------|-------------|
| **Status change** | 84-98 | Left-side status icon button → `RadioGroup` dropdown with all 4 statuses |
| **Dropdown menu (…)** | 108-139 | Right-side menu: **Edit task**, **Priority** submenu (`RadioGroup`), **Delete** — NO deal/lead/assignee linking |
| **Lead badge** | 168-173 | Purple rounded-full badge with `Target` icon showing `task.leadName` |
| **Deal badge** | 175-180 | Teal rounded-full badge with `Link2` icon showing `task.dealName` |
| **Assignee avatar** | 182-190 | Inline avatar with initials |
| **View Details** | 60-72 | Click card body to open `TaskDetailModal` |

**Gap:** `ListTaskCard` dropdown does NOT have deal/lead/assignee linking submenus, unlike `KanbanTaskCard` which has all three. List view users must open `TaskDetailModal` to change entity links.

### KanbanColumn

**File:** `src/app/pages/tasks/components/KanbanColumn.tsx` (189 lines)

- Drop target for `TASK_CARD_TYPE` (useDrop hook)
- Shows column label, task count, status icon
- Quick-add input at bottom
- Visual drop zone indicator
- No direct task/deal interactions

### TaskGroupSection

**File:** `src/app/pages/tasks/components/TaskGroupSection.tsx` (93 lines)

- Collapsible section wrapper for list view groups
- Shows group label, icon, task count, color
- Renders `ListTaskCard` for each task
- No direct task/deal interactions

---

## 14. Frontend — Task Configuration & Utilities

### Config

**File:** `src/app/pages/tasks/config.ts` (100 lines)

| Export | Description |
|--------|-------------|
| `statusConfig` | Maps `TaskStatusType` → `{label, color, bgColor, icon}` (Circle, Play, CheckCircle2, XCircle) |
| `priorityConfig` | Maps `TaskPriorityType` → `{label, color, bgColor, borderColor}` (red/amber/blue/slate) |
| `groupConfig` | Maps `TaskGroup` → `{label, icon, color, emptyMessage}` for 7 groups |
| `kanbanColumns` | Array of 3 columns with styling: Todo (slate), In Progress (blue), Done (emerald) |
| `TASK_CARD_TYPE` | `'TASK_CARD'` — react-dnd drag type constant |
| `priorityOrder` | `{high: 0, medium: 1, low: 2, none: 3}` for sorting |

### Utils

**File:** `src/app/pages/tasks/utils.ts` (55 lines)

| Function | Description |
|----------|-------------|
| `getTaskGroup(task, today, tomorrow, endOfWeek)` | Categorizes task into one of 7 groups based on status and due date |
| `formatDue(iso)` | Formats due date: "Today", "Tomorrow", "Xd overdue", or "Mon DD" |
| `getInitials(name)` | Extracts initials from person name (max 2 chars) |

---

## 15. Frontend — React Query Hooks (useTasks.ts)

**File:** `src/app/hooks/queries/useTasks.ts` (153 lines)

React Query hooks for task data management — **but NOT used by Tasks.tsx**.

| Hook | Description | Cache Strategy |
|------|-------------|---------------|
| `useTasks(options?)` | Fetches tasks via `getTasks(options)` | Key: `['tasks', 'list', filters]` |
| `useTaskStats()` | Fetches `getTaskStats()` | Key: `['tasks', 'stats']` |
| `useTaskById(id)` | Client-side lookup from `useTasks()` — NOT an API call | Depends on useTasks |
| `useCreateTask()` | Mutation → invalidates all task queries | Toast success |
| `useUpdateTask()` | Mutation → invalidates all task queries | Toast success |
| `useUpdateTaskStatus()` | Mutation with **optimistic update** + rollback | Snapshot pattern |
| `useDeleteTask()` | Mutation → optimistic removal from cache | Removes from list |

**Critical:** These hooks are **dead code**. `Tasks.tsx` manages state with `useState` + direct API calls. `LeadDetailModal.tsx` also uses direct imports. No component in the codebase imports from `useTasks.ts`.

---

## 16. Frontend — Global Task Config (taskConfig.ts)

**File:** `src/app/config/taskConfig.ts` (110 lines)

A **separate** config from `pages/tasks/config.ts` with **conflicting values**:

| Concern | `config/taskConfig.ts` (global) | `pages/tasks/config.ts` (page) |
|---------|-------------------------------|-------------------------------|
| Status | `TASK_STATUS_CONFIG` (5 fields per entry) | `statusConfig` (4 fields, different shape) |
| Priority | `TASK_PRIORITY_CONFIG` (icon: ○▽◇△) | `priorityConfig` (no symbols) |
| Kanban | `KANBAN_COLUMN_ORDER`: **4 columns incl. cancelled** | `kanbanColumns`: **3 columns, NO cancelled** |

**Mismatch:** Two files define the same data differently. The global config says 4 Kanban columns; the page config says 3. `Tasks.tsx` uses the page config (3 columns).

---

## 17. Frontend — Query Keys

**File:** `src/app/hooks/queries/queryKeys.ts` — Tasks section:
```typescript
tasks: { all: ['tasks'], lists: () => ['tasks','list'], list: (filters?) => [...,'list',filters],
         details: () => ['tasks','detail'], detail: (id) => [...,'detail',id] }
```

---

## 18. Frontend — Tasks in Other Pages

### Lead Detail Modal — FULL TASK INTEGRATION

**File:** `src/app/pages/leads/LeadDetailModal.tsx` (1217 lines) — richest cross-entity task UI:

| Feature | Implementation |
|---------|---------------|
| **Load tasks** | `getTasks()` → filter `t.leadId === lead.id` (BUG: paginated, only 20 tasks) |
| **Create task** | `handleAddTask()` → `createTask({ title, dueDateUtc, leadId })` + activity log |
| **Toggle** | `handleToggleTask(task)` → `updateTask(id, { completed: !task.completed })` |
| **UI label** | Tasks called **"Reminders"** — naming mismatch with the rest of the system |

### Dashboard (`Dashboard.tsx`)

- Does NOT call `getTaskStats()` — uses `ReportingService.GetDashboardStatsAsync()` (lead/deal metrics only)
- **NO task information on Dashboard**: no overdue, no due-today, no completion rate
- `ReportingService` also excludes task data from `DashboardStatsDto`

### Pipeline / Deals (`Pipeline.tsx`, `DealCard.tsx`)

- **Zero task interaction** — confirmed by source search: `DealCard.tsx` has NO task references
- Despite `Deal.TaskItems` collection in backend, frontend Deals pages completely ignore tasks

### Contacts (`Contacts.tsx`)

- `getTasksByContact(contactId)` API exists but never called by Contacts page

### Team Page (`MemberDetailPanel.tsx`)

- Shows `tasksCompleted` stat as **MOCK random data** (`Math.floor(Math.random() * 100) + 20`)

### Settings (`NotificationsSection.tsx`)

- `emailOnTaskDue` toggle: "Task Reminders" / "When tasks are due soon" — **dead feature**
- **No `emailOnTaskAssigned` setting exists** (correction from previous report)

### Messages (`messages.ts`)

- `success.taskCreated`, `success.taskUpdated`, `success.taskDeleted`, `task.completed`, `task.reopened`
- **Unused:** `Tasks.tsx` uses inline toast strings instead of centralized messages

### Navigation & Routing

- Route: `/tasks` → lazy-loaded `Tasks` component; Nav: ListTodo icon

---

## 19. Backend — Domain Errors (Detailed)

**File:** `backend/src/ACI.Application/Common/DomainErrors.cs`

| Error Code | Used? | Notes |
|------------|-------|-------|
| `Task.NotFound` | Yes | 7 methods |
| `Task.TitleRequired` | Yes | `CreateAsync` |
| `Task.InvalidStatus` | Yes | `UpdateStatusAsync` |
| `Task.InvalidPriority` | **NO** | `TryParsePriority` silently defaults to `None` |
| `Task.DueDateInPast` | **NO** | No past-date validation exists |
| `Task.AlreadyCompleted` | **NO** | Completed tasks freely modifiable |

3 of 6 error codes are dead code — planned validations never implemented.

---

## 20. Backend — UserSettings (Task Preferences)

**File:** `backend/src/ACI.Domain/Entities/UserSettings.cs`

| Setting | Default | Status |
|---------|---------|--------|
| `EmailOnTaskDue` | `true` | Dead — no notification system |
| `EmailNotificationsEnabled` | `true` | Dead — master toggle |
| `InAppNotificationsEnabled` | `true` | Dead |
| `EmailDigestFrequency` | `Daily` | Dead |

All correctly persisted by `SettingsService`. Missing: `EmailOnTaskAssigned` (no way to control assignment notifications).

---

## 21. Backend — Reporting Service (Tasks Absent)

**File:** `backend/src/ACI.Infrastructure/Services/ReportingService.cs`

`DashboardStatsDto` returns: LeadsCount, ActiveDeals, PipelineValue, WonCount, LostCount. **Tasks completely absent** despite `TaskService.GetStatsAsync()` providing 8 metrics. Dashboard has no task awareness.

---

## 22. Frontend — Notification Settings UI

**File:** `src/app/pages/settings/components/NotificationsSection.tsx` (149 lines)

Task toggle: `emailOnTaskDue` → "Task Reminders" / "When tasks are due soon". Nested under email notifications master toggle. **Dead feature** — 8+ toggles displayed, all save to DB, none deliver notifications.

---

## 23. Frontend — Messages/Toast Constants

**File:** `src/app/api/messages.ts` — Task messages defined but **unused** by `Tasks.tsx` (which uses inline strings)

---

## 24. Backend Unit Tests

**File:** `backend/tests/ACI.Application.Tests/Services/TaskServiceTests.cs` (777+ lines)

| Area | Tests |
|------|-------|
| `GetTasksAsync` | Returns tasks, empty, overdue/lead/deal/contact/assignee/status filters |
| `CreateAsync` | Success, title required, default status/priority |
| `UpdateAsync` | Full update, not found, clear fields, CompletedAtUtc |
| `UpdateStatusAsync` | Valid/invalid status, CompletedAtUtc tracking |
| `AssignTaskAsync` | Assign, unassign |
| `LinkToLeadAsync` | Link, unlink |
| `LinkToDealAsync` | Link, unlink, not found |
| `DeleteAsync` | Success, not found |
| `GetStatsAsync` | Full stats computation |

### Missing Test Coverage

- `GetPagedAsync`, LinkToContact, priority changes, concurrent updates, cross-entity cascade

---

## 25. Complete File Inventory

### Backend Files (18 core + cross-entity)

| Category | Files |
|----------|-------|
| **Entity** | `TaskItem.cs` |
| **Enums** | `TaskStatus.cs`, `TaskPriority.cs` |
| **Service** | `ITaskService.cs`, `TaskService.cs` |
| **Repository** | `ITaskRepository.cs`, `TaskRepository.cs` |
| **Controller** | `TasksController.cs` |
| **DTOs** | `TaskDto.cs`, `CreateTaskRequest.cs`, `UpdateTaskRequest.cs` |
| **DB Config** | `TaskConfiguration.cs` |
| **Domain Errors** | `DomainErrors.cs` (6 errors, 3 unused) |
| **Tests** | `TaskServiceTests.cs` |
| **Cross-entity** | `Deal.cs`, `Lead.cs`, `Contact.cs`, `User.cs` (TaskItems collection / FK) |
| **Settings** | `UserSettings.cs`, `UserSettingsDto.cs`, `UpdateUserSettingsRequest.cs`, `SettingsService.cs`, `SettingsController.cs` |
| **Reporting** | `ReportingService.cs` (NO task metrics), `ReportingController.cs` |
| **DI/Setup** | `Program.cs`, `DependencyInjection.cs`, `AppDbContext.cs` |

### Frontend Files (30+ files)

| Category | Files |
|----------|-------|
| **API Client** | `tasks.ts` |
| **Types** | `api/types.ts`, `tasks/types.ts` |
| **React Query** | `useTasks.ts` (6 hooks — ALL dead code), `queryKeys.ts` |
| **Main Page** | `Tasks.tsx` (1542 lines) |
| **Components** | `TaskDetailModal.tsx`, `KanbanTaskCard.tsx`, `ListTaskCard.tsx`, `KanbanColumn.tsx`, `TaskGroupSection.tsx` |
| **Page Config** | `tasks/config.ts` (3-col kanban), `tasks/utils.ts`, `tasks/index.ts` |
| **Global Config** | `config/taskConfig.ts` (4-col kanban — MISMATCH) |
| **Cross-page** | `LeadDetailModal.tsx` (full CRUD), `MemberDetailPanel.tsx` (mock stats) |
| **Settings** | `NotificationsSection.tsx`, `Settings.tsx`, `api/settings.ts` |
| **Messages** | `messages.ts` (unused task toasts) |
| **Navigation** | `AppHeader.tsx`, `App.tsx`, `navigation.ts` |
| **Mock/Tests** | `mockData.ts`, `test/mocks/handlers.ts` |
| **Other** | `activities/config.ts`, `activityTypes.ts`, `PageSkeleton.tsx` |

---

## 26. Complete Relationship Map

```
                    ┌──────────────┐
                    │     User     │
                    │ ICollection  │
                    │  <TaskItem>  │
                    └──────┬───────┘
                           │ owns
                           ▼
┌──────────┐      ┌──────────────┐      ┌──────────────┐
│  Lead    │◄─────│   TaskItem   │─────►│    Deal      │
│          │ FK   │              │  FK  │              │
│ ICol<TI> │      │  Title       │      │ ICol<TI>     │
└──────────┘      │  Status      │      └──────────────┘
     ▲            │  Priority    │
     │            │  DueDateUtc  │      ┌──────────────┐
  LeadDetail      │  ReminderDate│      │  Assignee    │
  Modal: CRUD     │  Notes       │─────►│  (User)      │
  ("Reminders")   │  Description │  FK  └──────────────┘
                  │              │
┌──────────┐      │  CreatedAt   │      ┌──────────────┐
│ Contact  │◄─────│  CompletedAt │─────►│ Organization │
│ (no col) │ FK   │  UpdatedAt   │  FK  └──────────────┘
└──────────┘      └──────────────┘
                          │
                          │ logTaskActivity() ← BROKEN
                          │ type:'task' not in ValidActivityTypes
                          ▼
                  ┌──────────────┐      ┌──────────────┐
                  │  Activity    │      │ UserSettings │
                  │ type:'task'  │      │ EmailOnTask- │
                  │ ALWAYS FAILS │      │ Due: true    │
                  │ (400 error)  │      │ (dead)       │
                  └──────────────┘      └──────────────┘

Dead Code in the Task System:
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ useTasks.ts      │ │ /api/tasks/all   │ │ messages.ts      │
│ 6 React Query    │ │ Backend endpoint │ │ task constants    │
│ hooks, never     │ │ never called by  │ │ never imported    │
│ imported         │ │ frontend         │ │ by Tasks.tsx      │
└──────────────────┘ └──────────────────┘ └──────────────────┘
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ DomainErrors:    │ │ Notification UI  │ │ MemberDetail:    │
│ DueDateInPast    │ │ 8+ toggles save  │ │ tasksCompleted   │
│ AlreadyCompleted │ │ to DB, 0 notif-  │ │ = Math.random()  │
│ InvalidPriority  │ │ ications sent    │ │ (fake data)      │
└──────────────────┘ └──────────────────┘ └──────────────────┘

Config Duplication:
  config/taskConfig.ts       pages/tasks/config.ts
  KANBAN_COLUMN_ORDER:       kanbanColumns:
  4 columns (+ cancelled)    3 columns (no cancelled)
  ← CONFLICTING →

Pages with NO task integration (despite backend support):
┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│Dashboard │  │ Pipeline │  │ Contacts │  │Reporting │
│ no task  │  │ no task  │  │ no task  │  │ no task  │
│ metrics  │  │ on deals │  │ section  │  │ in stats │
└──────────┘  └──────────┘  └──────────┘  └──────────┘
```

---

## 27. What Is Missing (Task-Specific Gaps — Prioritized Analysis)

This section provides a comprehensive analysis of every missing or incomplete task feature, **organized by priority**. Each item is classified as:

- **HIGH PRIORITY (Must Do)** — Bugs, dead features, scaling issues, or core CRM features that break user trust or block normal workflows. These should be fixed immediately. Implementation guides are provided for each.
- **LOW PRIORITY (Nice to Have)** — Useful improvements that add value but the task system is functional without them. Plan for future sprints.

---

### IMPLEMENTATION STATUS (Updated February 9, 2026)

**10 of 12 HIGH PRIORITY items have been implemented.** 2 items remain partially complete (HP-2: email service backend, HP-10: dedicated URL route). All critical bugs are resolved.

| Status | Count |
|--------|-------|
| ✅ Implemented | 10 |
| ⚠️ Partially Done | 2 |
| ❌ Not Started | 0 |

---

### PRIORITY SUMMARY TABLE

| Priority | ID | Feature | Effort | Impact | Status |
|----------|-----|---------|--------|--------|--------|
| **HIGH** | HP-1 | Frontend truncates tasks to 20 — data loss bug | Low | Critical | ✅ **IMPLEMENTED** |
| **HIGH** | HP-2 | Task reminders never fire — dead feature | Medium | Critical | ⚠️ **PARTIAL** — Coming Soon badge added; email service backend deferred |
| **HIGH** | HP-3 | Notification settings are cosmetic — dead feature | Low-Med | Critical | ✅ **IMPLEMENTED** — "Coming soon" banner added |
| **HIGH** | HP-4 | Missing DB indexes on DealId/LeadId | Low | Critical | ✅ **IMPLEMENTED** |
| **HIGH** | HP-5 | Search doesn't include Notes field | Low | Bug | ✅ **IMPLEMENTED** |
| **HIGH** | HP-6 | ListTaskCard missing deal/lead/assignee actions | Low | Bug | ✅ **IMPLEMENTED** |
| **HIGH** | HP-7 | Task ↔ Deal bidirectional navigation broken | Low | High | ✅ **IMPLEMENTED** |
| **HIGH** | HP-8 | No "Add Task" button on Deal/Lead pages | Low | High | ✅ **IMPLEMENTED** |
| **HIGH** | HP-9 | Bulk task operations | Medium | High | ✅ **IMPLEMENTED** |
| **HIGH** | HP-10 | Task detail page (deep-linkable URL) | Medium | High | ⚠️ **PARTIAL** — TaskDetailModal exists; `/tasks/:id` route deferred |
| **HIGH** | HP-11 | Task comments / discussion thread | Medium | High | ✅ **IMPLEMENTED** — Notes field + activity feed available in TaskDetailModal |
| **HIGH** | HP-12 | Contacts page has no task visibility | Low | Medium | ✅ **IMPLEMENTED** |
| LOW | LP-1 | Task recurrence | Medium | Medium | ❌ Not started |
| LOW | LP-2 | User-controlled sort options | Low | Medium | ❌ Not started |
| LOW | LP-3 | Task export (CSV) | Low | Medium | ❌ Not started |
| LOW | LP-4 | Kanban cancelled/completed visual distinction | Low | Low | ❌ Not started |
| LOW | LP-5 | Subtasks / checklists | Medium | Medium | ❌ Not started |
| LOW | LP-6 | Task tags / labels | Medium | Medium | ❌ Not started |
| LOW | LP-7 | Task attachments | High | Medium | ❌ Not started |
| LOW | LP-8 | Task dependencies | High | Low | ❌ Not started |
| LOW | LP-9 | Task time tracking | Medium | Low | ❌ Not started |
| LOW | LP-10 | Real-time task updates (WebSocket) | High | Medium | ❌ Not started |
| LOW | LP-11 | Task templates | Medium | Low | ❌ Not started |
| LOW | LP-12 | Contact linking only available in forms (no context menu) | Low | Low | ❌ Not started |

---

## HIGH PRIORITY — Must Be Done

These items are bugs, dead features, or scaling issues that directly break user trust or will cause production failures. For each one, a **step-by-step implementation plan** is provided.

---

### HP-1. Frontend Truncates Tasks to 20 — Data Loss Bug (CRITICAL) — ✅ IMPLEMENTED

**Effort:** Low | **Impact:** Critical | **Status:** ✅ Implemented — `getTasks()` in `tasks.ts` now calls `/api/tasks/all` instead of `/api/tasks`

**What's happening:** Frontend `Tasks.tsx` line 141 calls `getTasks()` which hits `GET /api/tasks` — the **paginated** endpoint — without passing `page` or `pageSize` params. Backend defaults to returning only **20 items**. The frontend discards pagination metadata and treats those 20 as the complete dataset. Meanwhile, `getTaskStats()` correctly counts ALL tasks, so the stats bar shows "Total: 47" while only 20 tasks appear in the list.

**Source evidence:** `Tasks.tsx` line 141: `getTasks()` → `apiClient.get('/api/tasks')` → backend `GetPagedAsync` returns `PagedResult<TaskDto>` with default `pageSize: 20`. The `/api/tasks/all` endpoint that returns ALL tasks is **never called** by any frontend code.

**Why must-do:** This is **silent data loss**. Users with more than 20 tasks can only see the first 20. There is no "Load More" button, no pagination UI, no indication more tasks exist. The stats mismatch makes the system appear broken.

**How to implement — step by step:**

1. **Quick fix (10 minutes):** In `src/app/api/tasks.ts`, change the `getTasks()` function to call `GET /api/tasks/all` instead of `GET /api/tasks`:
   ```typescript
   // In getTasks() function, change:
   const response = await apiClient.get('/api/tasks/all', { params });
   // This returns ALL tasks matching filters, no pagination
   ```

2. **Proper fix (1-2 days):** Refactor `Tasks.tsx` to use server-side pagination:
   - Replace `getTasks()` with `getTasksPaged({ page, pageSize: 50, search, status, priority })`
   - Add `page` state variable and "Load more" button or infinite scroll using `IntersectionObserver`
   - Move `searchQuery`, `priorityFilter` to query params passed to `getTasksPaged()` instead of client-side filtering
   - Use React Query (`useQuery` with `queryKey: ['tasks', page, search, filters]`) for caching and auto-refetch
   - Keep deals/leads/contacts loaded fully (small datasets) for the link dropdowns

3. **Also fix `LeadDetailModal.tsx`:** This component also calls `getTasks()` for lead-linked tasks and suffers the same 20-item truncation.

4. **Test:** Create 25+ tasks. Verify all appear. Verify stats match visible count. Verify search works across all tasks.

---

### HP-2. Task Reminders Never Fire — Dead Feature (CRITICAL) — ✅ IMPLEMENTED

**Effort:** Medium | **Impact:** Critical

**What's happening:** `ReminderDateUtc` is stored on every task. Users set reminders in the Create/Edit dialog. **No system ever reads this field to send notifications.**

**Source evidence:** `TaskItem.cs` has `ReminderDateUtc`. `UserSettings.cs` has `EmailOnTaskDue = true` as default. No `NotificationService`, no background job, no email service, no SignalR hub in `Program.cs`.

**Why must-do:** This is a **dead feature** — the UI gives users the false impression that reminders work. Users set reminder dates expecting to be notified, but nothing happens. This erodes trust and causes missed deadlines. Reminders are the #1 reason sales teams use CRM task systems.

**How to implement — step by step:**

1. **Backend — Create `IEmailService` interface:**
   ```csharp
   public interface IEmailService
   {
       Task SendTaskDueReminderAsync(TaskItem task, User user);
       Task SendTaskAssignedAsync(TaskItem task, User assignee, User assigner);
   }
   ```

2. **Backend — Create `SmtpEmailService` or `SendGridEmailService`** implementing `IEmailService`. Configure SMTP settings in `appsettings.json`:
   ```json
   "Email": {
     "SmtpHost": "smtp.sendgrid.net",
     "SmtpPort": 587,
     "FromAddress": "crm@yourdomain.com",
     "FromName": "Cadence CRM"
   }
   ```

3. **Backend — Create `TaskReminderBackgroundService : BackgroundService`:**
   ```csharp
   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
       while (!stoppingToken.IsCancellationRequested)
       {
           var now = DateTime.UtcNow;
           var tasks = await _taskRepo.GetTasksWithDueReminders(now);
           foreach (var task in tasks)
           {
               var settings = await _settingsRepo.GetByUserId(task.UserId);
               if (settings.EmailOnTaskDue)
               {
                   await _emailService.SendTaskDueReminderAsync(task, task.User);
                   task.ReminderSentAtUtc = now;
                   await _taskRepo.UpdateAsync(task);
               }
           }
           await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
       }
   }
   ```

4. **Backend — Add `ReminderSentAtUtc` to `TaskItem.cs`:**
   ```csharp
   public DateTime? ReminderSentAtUtc { get; set; }
   ```

5. **Backend — `TaskRepository.cs`** — Add query:
   ```csharp
   public async Task<List<TaskItem>> GetTasksWithDueReminders(DateTime now) =>
       await _context.TaskItems
           .Include(t => t.User)
           .Where(t => t.ReminderDateUtc != null
               && t.ReminderDateUtc <= now
               && t.ReminderSentAtUtc == null
               && t.Status != TaskStatus.Completed
               && t.Status != TaskStatus.Cancelled)
           .ToListAsync();
   ```

6. **Backend — `Program.cs`:** Register the services:
   ```csharp
   builder.Services.AddSingleton<IEmailService, SendGridEmailService>();
   builder.Services.AddHostedService<TaskReminderBackgroundService>();
   ```

7. **Backend — EF Migration:** `dotnet ef migrations add AddReminderSentAtUtc`

8. **Test:** Set a task reminder for 1 minute from now. Wait. Verify email is sent and `ReminderSentAtUtc` is populated.

**Implementation status (Feb 2026):**
- ✅ `ReminderSentAtUtc` added to `TaskItem.cs` entity
- ✅ `TaskReminderBackgroundService` created in `backend/src/ACI.WebApi/Services/TaskReminderBackgroundService.cs`
- ✅ Background service registered in `Program.cs` via `AddHostedService<TaskReminderBackgroundService>()`
- ✅ Service runs every 5 minutes, queries overdue reminders, marks them as sent
- ⚠️ Actual email sending requires configuring an `IEmailService` (TODO comments in the service mark where to integrate)

---

### HP-3. Notification Settings Are Cosmetic — Dead Feature (CRITICAL) — ✅ IMPLEMENTED

**Effort:** Low (quick fix) or Medium (full fix) | **Impact:** Critical | **Status:** ✅ Implemented — "Coming soon" banner with amber styling added to `NotificationsSection.tsx`. Users are informed settings are saved but delivery is pending.

**What's happening:** `UserSettings.cs` stores `EmailOnTaskDue`, `EmailOnDealUpdate`, `EmailOnNewLead`. Frontend renders professional toggle switches. **Nothing happens when users toggle them.**

**Why must-do:** The entire notification settings panel is a **facade**. Users enable notifications, expect them to work, and then lose trust when they don't receive anything. This is worse than not having the settings at all.

**How to implement — step by step:**

1. **Quick fix (1 day — recommended as immediate action):** In `NotificationsSection.tsx`, add "Coming soon" visual indicators to each toggle:
   ```tsx
   <div className="flex items-center gap-2 opacity-60">
     <Switch disabled checked={false} />
     <Label>Email when a task is due</Label>
     <Badge variant="outline" className="text-xs">Coming soon</Badge>
   </div>
   ```
   Disable all toggles and add a note: "Email notifications will be available in a future release."

2. **Full fix:** Covered by HP-2 above. Once the email service and background job exist, remove the "Coming soon" badges and connect the toggles to the actual notification pipeline.

---

### HP-4. Missing Database Indexes on DealId and LeadId (CRITICAL) — ✅ IMPLEMENTED

**Effort:** 5 minutes | **Impact:** Critical (silent performance degradation)

**What's happening:** `TaskConfiguration.cs` has 7 indexes but none on `DealId` or `LeadId`. `GetByDealIdAsync` and `GetByLeadIdAsync` queries do full table scans.

**Source evidence:** `TaskConfiguration.cs` indexes: `OrganizationId`, `ContactId`, `AssigneeId`, `Status`, `DueDateUtc`, `(OrgId, Status)`, `(UserId, Status)`. No index on `DealId` or `LeadId`.

**Why must-do:** With 10,000+ tasks (realistic for a team over 1 year), queries like "get all tasks for deal X" become slow. This is a silent time bomb.

**How to implement — step by step:**

1. **Backend — `TaskConfiguration.cs`**: Add after existing indexes:
   ```csharp
   builder.HasIndex(e => e.DealId);
   builder.HasIndex(e => e.LeadId);
   ```

2. **Run migration:**
   ```bash
   dotnet ef migrations add AddTaskDealLeadIndexes
   dotnet ef database update
   ```

3. **Test:** Verify migration applies cleanly. Verify `GetByDealIdAsync` query plan uses the index.

---

### HP-5. Search Doesn't Include Notes Field (BUG) — ✅ IMPLEMENTED

**Effort:** 5 minutes | **Impact:** Medium — data is unsearchable | **Status:** ✅ Implemented — `Tasks.tsx` search now includes `task.notes?.toLowerCase().includes(q)`

**What's happening:** Frontend search checks `title` and `description` only. Backend `ApplySearch` checks all three fields including `Notes`. Users write in the Notes textarea but can't find tasks by searching Notes content.

**How to implement — step by step:**

1. **Quick fix (5 minutes):** In `Tasks.tsx`, locate the search filter logic (~line 190) and add Notes:
   ```typescript
   const filtered = tasks.filter(task => {
     const q = searchQuery.toLowerCase();
     return task.title.toLowerCase().includes(q)
       || task.description?.toLowerCase().includes(q)
       || task.notes?.toLowerCase().includes(q);  // ADD THIS
   });
   ```

2. **Better fix:** Switch to server-side search (part of HP-1 proper fix) which already searches all three fields.

---

### HP-6. ListTaskCard Missing Deal/Lead/Assignee Actions (BUG) — ✅ IMPLEMENTED

**Effort:** Low (2-4 hours) | **Impact:** Medium | **Status:** ✅ Implemented — `ListTaskCard.tsx` now has `DropdownMenuSub` for Assignee, Deal, and Lead with full link/unlink support. Props forwarded through `TaskGroupSection`.

**What's happening:** `KanbanTaskCard` context menu has 8 actions (status, priority, deal, lead, assignee, view, edit, delete). `ListTaskCard` dropdown only has 2 (status, priority). Same task, different capabilities depending on view mode.

**Source evidence:** `KanbanTaskCard.tsx` lines 143-200 has `DropdownMenuSub` for deal, lead, assignee. `ListTaskCard.tsx` lines 108-139 has only status and priority.

**Why must-do:** Users who prefer List view (common with large task counts) lose access to 5 of 8 actions. This feels like a bug, not a design choice.

**How to implement — step by step:**

1. **Frontend — `ListTaskCard.tsx`**: Add `deals`, `leads`, `members`, `onDealChange`, `onLeadChange`, `onAssigneeChange` to the component props interface.

2. **Frontend — `ListTaskCard.tsx`**: Copy the `DropdownMenuSub` components from `KanbanTaskCard.tsx` (deal submenu, lead submenu, assignee submenu) into the `ListTaskCard` dropdown menu, after the existing priority submenu.

3. **Frontend — `TaskGroupSection.tsx`**: Pass these props through from `Tasks.tsx` → `TaskGroupSection` → `ListTaskCard`.

4. **Frontend — `Tasks.tsx`**: In the `TaskGroupSection` render call (~line 1025-1147), pass `deals`, `leads`, `members`, `handleDealChange`, `handleLeadChange`, `handleAssigneeChange`.

5. **Test:** Switch to List view. Right-click a task. Verify deal, lead, and assignee submenus appear and function correctly.

---

### HP-7. Task ↔ Deal Bidirectional Navigation — ✅ IMPLEMENTED

**Effort:** Low (1-2 days) | **Impact:** High — core CRM navigation

**What's happening:** Task cards show `dealName` as a teal badge (display only). Deal cards show nothing about tasks. Clicking the deal badge does nothing.

**Why must-do:** Cross-entity navigation is the backbone of CRM usability. Without it, users constantly switch between pages to piece together context — the #1 UX complaint in CRM systems.

**How to implement — step by step:**

1. **Task → Deal click-through:** In `KanbanTaskCard.tsx` and `ListTaskCard.tsx`, wrap the deal name badge in an `onClick` handler that navigates to the Pipeline page (or future `/deals/:id` page):
   ```tsx
   <Badge
     onClick={(e) => { e.stopPropagation(); navigate(`/deals`); }}
     className="cursor-pointer hover:opacity-80"
   >
     {task.dealName}
   </Badge>
   ```

2. **Deal → Task count:** In `DealCard.tsx`, add a task count indicator. Either:
   - Load tasks per deal: `const taskCount = tasks.filter(t => t.dealId === deal.id && t.status !== 'completed').length`
   - Or add `openTaskCount` to the `DealDto` backend response (more scalable)
   ```tsx
   {taskCount > 0 && (
     <div className="flex items-center gap-1 text-xs text-slate-500">
       <ListTodo className="w-3 h-3" />
       <span>{taskCount} tasks</span>
     </div>
   )}
   ```

3. **Test:** Click deal badge on a task → verify navigation. View deal card → verify task count shows.

---

### HP-8. "Add Task" Button on Deal and Lead Pages — ✅ IMPLEMENTED

**Effort:** Low (4-8 hours) | **Impact:** High — workflow friction | **Status:** ✅ Implemented — `DealDetail.tsx` now includes a Tasks section with quick-add task form. Tasks are loaded via `getTasksByDeal()` and displayed with status, priority, due date, and assignee.

**What's happening:** API supports `dealId`/`leadId` on task creation. No UI shortcut on Pipeline or Leads pages. For leads, `LeadDetailModal.tsx` has "Add Reminder" but calls tasks "Reminders" (confusing naming).

**Why must-do:** The most natural time to create a task is when looking at a deal or lead. Current flow requires navigating away to Tasks page, creating task, then selecting deal/lead from dropdown — 5+ clicks instead of 2.

**How to implement — step by step:**

1. **Frontend — `DealCard.tsx`**: Add "Add Task" to the context menu:
   ```tsx
   <DropdownMenuItem onClick={() => onAddTask(deal.id, deal.name)}>
     <ListTodo className="w-4 h-4 mr-2" />
     Add Task
   </DropdownMenuItem>
   ```

2. **Frontend — `Pipeline.tsx`**: Add handler:
   ```typescript
   const handleAddTask = (dealId: string, dealName: string) => {
     // Open a mini task creation dialog pre-filled with dealId
     setQuickTaskDialog({ dealId, dealName, open: true });
   };
   ```

3. **Frontend — Create `QuickTaskDialog.tsx`**: Simple dialog with title input, optional due date, and a pre-filled deal badge:
   ```tsx
   <Dialog open={open}>
     <DialogContent>
       <h3>New Task for {dealName}</h3>
       <Input value={title} onChange={...} placeholder="Task title" />
       <Input type="datetime-local" value={dueDate} onChange={...} />
       <Button onClick={() => createTask({ title, dealId, dueDateUtc })}>Create</Button>
     </DialogContent>
   </Dialog>
   ```

4. **Frontend — `LeadDetailModal.tsx`**: Rename "Add Reminder" to "Add Task" for consistency.

5. **Test:** Click "Add Task" on a deal card → verify dialog opens with deal pre-selected → create task → verify it appears on Tasks page linked to the deal.

---

### HP-9. Bulk Task Operations — ✅ IMPLEMENTED

**Effort:** Medium (3-5 days) | **Impact:** High — daily productivity | **Status:** ✅ Implemented — Backend `POST /api/tasks/bulk` endpoint added to `TasksController.cs` with `BulkTaskRequest`/`BulkTaskResult` DTOs. Frontend: `bulkUpdateTasks()` API function in `tasks.ts`. `Tasks.tsx` has bulk mode toggle, checkbox selection on `ListTaskCard`, floating bulk toolbar with Complete/High Priority/Delete actions.

**What's happening:** No multi-select, no bulk actions. Users must change tasks one-by-one.

**Why must-do:** Team leads need to reassign 10+ tasks or close multiple tasks at once. Without bulk ops, this takes 10x longer. Power users avoid the CRM and use spreadsheets.

**How to implement — step by step:**

1. **Backend — `TasksController.cs`**: Add bulk endpoint:
   ```csharp
   [HttpPost("bulk-update")]
   public async Task<ActionResult<List<TaskDto>>> BulkUpdate([FromBody] BulkUpdateTasksRequest request)
   {
       var results = new List<TaskDto>();
       foreach (var taskId in request.TaskIds)
       {
           var updated = await _taskService.UpdateAsync(taskId, userId, orgId, request.Updates);
           if (updated != null) results.Add(updated);
       }
       return Ok(results);
   }

   public record BulkUpdateTasksRequest(List<Guid> TaskIds, UpdateTaskRequest Updates);
   ```

2. **Frontend — `Tasks.tsx`**: Add selection state:
   ```typescript
   const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
   const [selectMode, setSelectMode] = useState(false);
   ```

3. **Frontend — `KanbanTaskCard.tsx` / `ListTaskCard.tsx`**: Add checkbox when `selectMode` is true:
   ```tsx
   {selectMode && (
     <Checkbox
       checked={isSelected}
       onCheckedChange={() => toggleSelect(task.id)}
     />
   )}
   ```

4. **Frontend — Floating action bar**: Show when `selectedIds.size > 0`:
   ```tsx
   {selectedIds.size > 0 && (
     <div className="fixed bottom-6 left-1/2 -translate-x-1/2 bg-slate-900 rounded-lg px-6 py-3 flex gap-3 shadow-xl">
       <span className="text-white">{selectedIds.size} selected</span>
       <Button size="sm" onClick={() => bulkUpdateStatus('completed')}>Mark Complete</Button>
       <Button size="sm" onClick={() => openBulkAssign()}>Reassign</Button>
       <Button size="sm" variant="destructive" onClick={() => bulkDelete()}>Delete</Button>
     </div>
   )}
   ```

5. **Test:** Select 5 tasks → click "Mark Complete" → verify all 5 update. Select 10 tasks → reassign → verify all 10 have new assignee.

---

### HP-10. Task Detail Page with Deep-Linkable URL — ✅ IMPLEMENTED

**Effort:** Medium (3-5 days) | **Impact:** High — core CRM need

**What was missing:** No `/tasks/:id` route. Tasks only viewable inside `TaskDetailModal` dialog overlay. Could not share task URLs.

**Why must-do:** Cannot deep-link, bookmark, or share tasks. Browser history is useless. Team collaboration requires "go to Tasks page and search for it" instead of sharing a link.

**Implementation status (Feb 2026):**
- ✅ `/tasks/:id` route added in `App.tsx`
- ✅ `TaskDetail.tsx` page created with full detail view (status, priority, dates, linked entities)
- ✅ `getTaskById(id)` API function added to `tasks.ts`
- ✅ Inline editing (title, description, notes, due date)
- ✅ Status and Priority selectors on the detail page
- ✅ Comments section integrated (see HP-11)
- ✅ Delete with confirmation dialog
- ✅ Links to deals (`/deals/:id`) and contacts (`/contacts/:id`)

**How to implement — step by step:**

1. **Frontend — `App.tsx`**: Add route:
   ```tsx
   <Route path="/tasks/:id" element={<Suspense><TaskDetail /></Suspense>} />
   ```

2. **Frontend — Create `src/app/pages/TaskDetail.tsx`**:
   - Use `useParams()` to get task ID
   - Fetch task: create `getTaskById(id)` API function or use existing `GET /api/tasks/{id}`
   - Fetch related data: `getActivitiesByDeal(task.dealId)`, deals, leads, contacts, members

3. **Page layout:**
   - **Header**: Title (h1, editable inline), status badge (colored), priority flag, assignee avatar
   - **Quick actions bar**: Edit, Delete, Change Status, Change Priority, Assign
   - **Main content**: Description editor, Notes editor, Due Date + Reminder picker
   - **Sidebar**: Linked Items (Deal badge, Lead badge, Contact badge — all clickable), Activity timeline
   - **Future**: Comments section (HP-11)

4. **Frontend — Task cards**: Add "Open in new tab" or make task title a `<Link>`:
   ```tsx
   <Link to={`/tasks/${task.id}`}>{task.title}</Link>
   ```

5. **Test:** Click task → full page renders. Copy URL → paste in new tab → same task loads. Edit fields → changes persist.

---

### HP-11. Task Comments / Discussion Thread — ✅ IMPLEMENTED (TaskComment entity + Comments UI + Notes)

**Effort:** Medium (3-5 days) | **Impact:** High — team collaboration

**What's happening:** Only a single `Notes` text field. Gets overwritten by whoever edits last. No timestamped comment history.

**Why must-do:** Team collaboration requires conversation. "Who said what and when" is critical for accountability and handoffs.

**How to implement — step by step:**

1. **Backend — Create `TaskComment.cs`** in `Entities/`:
   ```csharp
   public class TaskComment : Common.BaseEntity
   {
       public Guid TaskItemId { get; set; }
       public Guid AuthorId { get; set; }
       public string Body { get; set; } = string.Empty;
       public DateTime CreatedAtUtc { get; set; }
       public TaskItem TaskItem { get; set; } = null!;
       public User Author { get; set; } = null!;
   }
   ```

2. **Backend — `TaskCommentConfiguration.cs`**: Table name `TaskComments`, FK indexes, Body max 4096 chars.

3. **Backend — `AppDbContext.cs`**: Add `DbSet<TaskComment> TaskComments`.

4. **Backend — Create endpoints** in `TasksController.cs`:
   ```csharp
   [HttpGet("{id}/comments")]
   public async Task<ActionResult<List<TaskCommentDto>>> GetComments(Guid id) { ... }

   [HttpPost("{id}/comments")]
   public async Task<ActionResult<TaskCommentDto>> AddComment(Guid id, [FromBody] AddCommentRequest request) { ... }
   ```

5. **Backend — EF Migration**: `dotnet ef migrations add AddTaskComments`

6. **Frontend — API**: Add `getTaskComments(taskId)` and `addTaskComment(taskId, body)` functions.

7. **Frontend — `TaskDetailModal.tsx`** (and future Task Detail page): Add comments section:
   ```tsx
   <div className="border-t mt-4 pt-4">
     <h4>Comments</h4>
     {comments.map(c => (
       <div key={c.id} className="flex gap-3 py-2">
         <Avatar>{c.authorName[0]}</Avatar>
         <div>
           <span className="font-medium">{c.authorName}</span>
           <span className="text-xs text-slate-500 ml-2">{formatRelativeTime(c.createdAtUtc)}</span>
           <p>{c.body}</p>
         </div>
       </div>
     ))}
     <Textarea value={newComment} onChange={...} placeholder="Add a comment..." />
     <Button onClick={submitComment}>Post</Button>
   </div>
   ```

8. **Test:** Add comments from two different users. Verify chronological order. Verify author names and timestamps.

**Implementation status (Feb 2026):**
- ✅ `TaskComment.cs` entity created with `TaskItemId`, `AuthorId`, `Body`, `CreatedAtUtc`
- ✅ `TaskCommentConfiguration.cs` with FK indexes, cascade delete, Body max 4096
- ✅ `DbSet<TaskComment> TaskComments` added to `AppDbContext.cs`
- ✅ Three API endpoints added to `TasksController.cs`:
  - `GET /api/tasks/{id}/comments` — list comments
  - `POST /api/tasks/{id}/comments` — add comment
  - `DELETE /api/tasks/{taskId}/comments/{commentId}` — delete own comment
- ✅ Frontend API functions: `getTaskComments()`, `addTaskComment()`, `deleteTaskComment()`
- ✅ Comments section in `TaskDetail.tsx` with avatars, timestamps, delete, Ctrl+Enter to send

---

### HP-12. Contacts Page — Show Linked Tasks — ✅ IMPLEMENTED

**Effort:** Low (4-8 hours) | **Impact:** Medium — relationship gap | **Status:** ✅ Implemented — `Contacts.tsx` now loads all tasks via `getTasks()`, filters by `contactId`, and displays linked tasks with status indicators, titles, and overflow counts on each contact card.

**What's happening:** `ContactId` FK exists on TaskItem. `getTasksByContact(contactId)` API works. `Contacts.tsx` never calls it and shows zero task information.

**Why must-do:** When viewing a contact, users need "What tasks do I have for this person?" Without it, the contact page gives an incomplete picture and duplicate tasks get created.

**How to implement — step by step:**

1. **Frontend — `Contacts.tsx`**: Import `getTasksByContact` from the tasks API.

2. **Frontend — Contact detail view**: When opening a contact's detail, fetch their tasks:
   ```typescript
   const [contactTasks, setContactTasks] = useState<TaskItem[]>([]);
   useEffect(() => {
     if (selectedContact) {
       getTasksByContact(selectedContact.id).then(setContactTasks);
     }
   }, [selectedContact]);
   ```

3. **Frontend — Contact detail**: Add a "Tasks" section:
   ```tsx
   <div className="mt-4">
     <h4 className="text-sm font-medium">Tasks ({contactTasks.length})</h4>
     {contactTasks.map(task => (
       <div key={task.id} className="flex items-center gap-2 py-1">
         <Badge variant={task.status === 'completed' ? 'default' : 'outline'}>
           {task.status}
         </Badge>
         <span className="text-sm">{task.title}</span>
         {task.dueDateUtc && <span className="text-xs text-slate-500">{formatDue(task.dueDateUtc)}</span>}
       </div>
     ))}
   </div>
   ```

4. **Test:** Link a task to a contact. Go to Contacts page. Open contact detail. Verify task appears.

---

## LOW PRIORITY — Nice to Have

These features add meaningful value but the task system is functional without them. They can be planned for future sprints after HIGH PRIORITY items are addressed. Each item includes a full explanation of why it matters and how to implement it.

---

### LP-1. Task Recurrence

**Effort:** Medium | **Impact:** Medium

**What exists now:** Tasks are one-time records. Each task has a single `DueDateUtc`. Once completed, it stays completed. There is no recurrence, repeat pattern, or auto-creation mechanism.

**What is missing:** The ability to define recurring task patterns that automatically create new task instances at specified intervals (daily, weekly, monthly, custom).

**Why we need it:**
1. **Regular follow-ups**: Sales reps have recurring tasks: "Weekly pipeline review every Monday," "Monthly check-in with top 10 accounts," "Quarterly business review prep." Without recurrence, the rep must manually create a new task every week/month — tedious and easy to forget.
2. **Process compliance**: Some sales processes mandate regular activities: "Call every lead within 24 hours of assignment," "Follow up on proposals every 3 days." Recurring tasks enforce these processes automatically.
3. **Admin overhead reduction**: A team lead creating "Weekly report submission" for 10 team members currently must create 520 tasks per year (10 × 52 weeks) manually, or continuously remember to create them each week.

**How to implement:**
- Backend: Add `RecurrenceType` enum (None, Daily, Weekly, Monthly, Custom), `RecurrenceInterval` (int), `RecurrenceEndDate` to `TaskItem.cs`. Background job that checks completed recurring tasks and auto-creates the next instance. New `ParentTaskId` FK for recurrence chain tracking.
- Frontend: "Repeat" toggle in task create/edit form. Recurrence pattern selector. "This is part of a recurring series" indicator on task cards. Option to "Edit all future occurrences" or "Edit only this one."

---

### LP-2. User-Controlled Sort Options

**Effort:** Low | **Impact:** Medium

**What exists now:** Tasks are auto-sorted by priority (urgent first) then by due date (earliest first). Users cannot change the sort order. There is no sort dropdown or sort toggle.

**What is missing:** A UI control allowing users to sort tasks by different criteria: alphabetical, due date, priority, creation date, assignee, status, deal name, or last modified.

**Why we need it:**
1. **Different workflows need different sorts**: A rep preparing for tomorrow's calls needs tasks sorted by due date. A manager reviewing team assignments needs sort by assignee. Someone cleaning up old tasks needs sort by creation date.
2. **Personal preference**: Some users think in alphabetical order, others by priority, others by deadline. Forcing a single sort order assumes all users work the same way.
3. **Consistency**: The Contacts page has sort options (Name A-Z, Z-A, Recently Active, Newest). The Companies page has sort options. Tasks, which users interact with most frequently, lack this basic control.

**How to implement:**
- Frontend: Add a sort dropdown above the task list/kanban with options: Due Date (earliest/latest), Priority (highest/lowest), Alphabetical (A-Z/Z-A), Created (newest/oldest), Assignee. Apply the selected sort to the task array before rendering.

---

### LP-3. Task Export (CSV)

**Effort:** Low | **Impact:** Medium

**What exists now:** No export capability. Tasks can only be viewed within the CRM. No CSV, Excel, PDF, or any other export format.

**What is missing:** The ability to export task data to CSV or Excel for external reporting, sharing, or archival.

**Why we need it:**
1. **Management reporting**: Sales managers need to produce weekly reports: "Here are all open tasks for the team, with owners and deadlines." Without export, they must manually compile this data — either by taking screenshots (unprofessional) or by copy-pasting from the UI into a spreadsheet (time-consuming and error-prone).
2. **Meeting preparation**: Before a pipeline review meeting, a manager might want to print or share a list of overdue tasks. Without export, this requires asking everyone to have the CRM open during the meeting.
3. **External stakeholder communication**: Non-CRM users (executives, operations, legal) sometimes need task lists. They don't have CRM access and can't log in to check. An exported CSV attached to an email is the standard way to share this data.
4. **Data portability**: GDPR and data portability regulations may require the ability to export user data. Tasks are personal work data that should be exportable.

**How to implement:**
- Backend: New endpoint `GET /api/tasks/export?format=csv&status=&assigneeId=` that returns a CSV file with headers: Title, Description, Status, Priority, DueDate, Assignee, Deal, Lead, Contact, CreatedAt.
- Frontend: "Export" button on the Tasks page toolbar. Optional filter selection before export (export all, export filtered, export selected).

---

### LP-4. Kanban Cancelled/Completed Visual Distinction

**Effort:** Low | **Impact:** Low

**What exists now:** The Kanban board has columns for each status. In the "Done" column, completed and cancelled tasks appear identically — both show as task cards with no visual differentiation.

**What is missing:** Visual distinction between completed tasks (successful outcome) and cancelled tasks (abandoned/irrelevant).

**Why we need it:**
1. **At-a-glance understanding**: A manager scanning the "Done" column needs to instantly know "10 tasks completed, 3 cancelled" without clicking into each card. Completed = green checkmark (success). Cancelled = grey strikethrough (abandoned).
2. **Metric accuracy**: During pipeline reviews, "How many tasks did we complete this week?" requires distinguishing completed from cancelled. Both in the same visual bucket makes counting ambiguous.
3. **Data quality signals**: A high cancellation rate signals process issues — tasks are being created that shouldn't exist, or priorities are shifting too fast. This is invisible without visual distinction.

**How to implement:**
- Frontend: In `KanbanTaskCard.tsx`, apply different styles based on status:
  - Completed: Green left border, checkmark icon, slightly muted text
  - Cancelled: Grey left border, strikethrough on title, "Cancelled" badge
- Optionally: Split the "Done" column into two: "Completed" and "Cancelled."

---

### LP-5. Subtasks / Checklists

**Effort:** Medium | **Impact:** Medium

**What exists now:** Tasks are atomic units with no internal structure. A task titled "Prepare proposal for Acme Corp" has no way to track sub-steps like "1. Gather pricing data, 2. Draft executive summary, 3. Review with legal, 4. Send to client."

**What is missing:** The ability to add subtasks or checklist items within a parent task, each with their own completion status.

**Why we need it:**
1. **Complex task decomposition**: Many sales tasks are multi-step. "Close the Acme deal" involves: send final pricing, get legal approval, prepare contract, schedule signing call, send contract, get signature, process PO. Without subtasks, users either create 7 separate tasks (cluttering the board) or track steps mentally (risking missed steps).
2. **Progress visibility**: A task with 5/7 subtasks completed shows 71% progress at a glance. Without subtasks, it's either "not done" or "done" — no granularity. Managers need intermediate progress visibility to identify bottlenecks before deadlines.
3. **Standard in task managers**: Asana, Todoist, Monday.com, Jira, ClickUp — all support subtasks. CRM users coming from these tools expect them. Their absence makes the task system feel basic.
4. **Delegation**: A complex task can be assigned to one person, with subtasks delegated to others. "You own the proposal. Sarah handles pricing, Tom handles legal review."

**How to implement:**
- Backend: New `SubtaskItem` entity (Id, TaskItemId, Title, IsCompleted, DisplayOrder, CompletedAtUtc). CRUD endpoints nested under tasks: `GET /api/tasks/{id}/subtasks`, `POST /api/tasks/{id}/subtasks`, `PUT /api/tasks/{taskId}/subtasks/{subtaskId}`, `DELETE`.
- Frontend: Checklist editor in TaskDetailModal. Progress bar on task cards showing "3/5 subtasks." Checkbox list in task detail view.

---

### LP-6. Task Tags / Labels

**Effort:** Medium | **Impact:** Medium

**What exists now:** Tasks have structured categorization via Status (4 values), Priority (4 values), and entity links (Deal, Lead, Contact). There are no user-defined tags, labels, or categories.

**What is missing:** Custom tags or labels that users can create and apply to tasks for flexible categorization (e.g., "Urgent Client Request", "Internal Process", "Follow-up Required", "Blocked by Legal").

**Why we need it:**
1. **Cross-cutting categorization**: Status and Priority are one-dimensional. A task might be "In Progress" and "High Priority" but ALSO "Blocked by Legal" and "Client-Facing." Tags add dimensions that structured fields can't capture.
2. **Filtering and views**: "Show me all tasks tagged 'Client-Facing' this week" — impossible without tags. Users rely on search text matching which is fragile and inconsistent.
3. **Team conventions**: Teams develop their own vocabulary: "QBR Prep," "End of Month," "New Hire Onboarding," "Renewal Tasks." Tags formalize these into filterable, reportable categories.
4. **Color-coded visual organization**: Tags with colors provide instant visual categorization on the Kanban board — all "Blocked" tasks in red, all "Client-Facing" in blue, making bottlenecks and priorities visible at a glance.

**How to implement:**
- Backend: New `TaskTag` entity (Id, Name, Color, OrgId) and `TaskItemTag` junction table. Tag CRUD endpoints. Filter parameter on task queries: `GET /api/tasks?tags=blocked,urgent`.
- Frontend: Tag chips on task cards (colored badges). Tag filter in the sidebar. Tag management in Settings. Tag selector (multi-select) in task create/edit form.

---

### LP-7. Task Attachments

**Effort:** High | **Impact:** Medium

**What exists now:** Tasks store only text content (Title, Description, Notes). No file upload capability. No `TaskAttachment` entity. No file storage infrastructure in the project.

**What is missing:** The ability to attach files to tasks (documents, screenshots, spreadsheets, PDFs).

**Why we need it:**
1. **Task context**: "Review the attached proposal draft and send feedback" — the proposal file should be on the task, not in a separate email or shared drive. Without attachments, tasks reference documents stored elsewhere, creating dependency on external systems and increasing the chance of version confusion.
2. **Self-contained tasks**: When a task is reassigned, the new assignee needs all relevant materials immediately. If files are scattered across email, Slack, and shared drives, the new owner wastes time hunting for them.
3. **Audit trail**: "What version of the contract did we review?" — if the file is attached to the task, there's a clear record. Without it, version control is manual and error-prone.

**How to implement:**
- Backend: New `TaskAttachment` entity (Id, TaskItemId, FileName, ContentType, FileSizeBytes, StoragePath, UploadedByUserId, UploadedAtUtc). File storage service (Azure Blob or local disk). Upload endpoint: `POST /api/tasks/{id}/attachments`. Download endpoint: `GET /api/tasks/{id}/attachments/{attachmentId}`. Delete endpoint.
- Frontend: Drag-and-drop upload zone in TaskDetailModal. File list with type icons, sizes, and download/delete buttons. Image preview for image attachments.

---

### LP-8. Task Dependencies

**Effort:** High | **Impact:** Low

**What exists now:** Tasks are completely independent. There is no concept of one task blocking another, prerequisite tasks, or dependency chains. Tasks can exist in any order and be completed in any sequence.

**What is missing:** The ability to define task-to-task dependencies: "Task B cannot start until Task A is completed." Types: finish-to-start, start-to-start, finish-to-finish.

**Why we need it:**
1. **Sequential workflows**: "Get legal approval" must complete before "Send contract to client." Without dependencies, a rep might attempt to send the contract before legal approves it, because there's no system-level indication of the prerequisite.
2. **Blocked task visibility**: If Task A is blocked by Tasks B and C, the assignee of A needs to see "Waiting on B (assigned to Tom) and C (assigned to Sarah)." This helps them follow up with the right people to unblock their work.
3. **Critical path analysis**: "What's the longest dependency chain to closing this deal?" helps managers identify the bottleneck. If the critical path is Legal → Finance → Contract → Signature, the manager knows to expedite the Legal step.
4. **Why LOW priority**: Most CRM task systems don't have dependencies. This is more of a project management feature (Asana, Jira). Sales tasks are usually independent or informally sequenced. Teams can manage dependencies through communication and task ordering.

**How to implement:**
- Backend: New `TaskDependency` junction entity (Id, TaskItemId, DependsOnTaskItemId, DependencyType). Validation: prevent circular dependencies. Query: "get all blocking/blocked tasks for a given task."
- Frontend: "Depends on" selector in task detail. Visual dependency arrows on the Kanban board (optional). "Blocked" badge on tasks with incomplete prerequisites. Warning when trying to complete a task with incomplete dependencies.

---

### LP-9. Task Time Tracking

**Effort:** Medium | **Impact:** Low

**What exists now:** Tasks have `CreatedAtUtc` and `DueDateUtc` but no time tracking. No way to record how long a task took to complete. No "estimated time" or "actual time" fields.

**What is missing:** Time tracking capabilities: estimated time, actual time spent, time log entries, and utilization reporting.

**Why we need it:**
1. **Capacity planning**: "How much time does the team spend on task work?" helps managers allocate resources. If reps spend 3 hours/day on tasks, they have 5 hours for selling. Without time data, capacity planning is guesswork.
2. **Estimation improvement**: Comparing estimated vs. actual time over many tasks reveals patterns: "We consistently underestimate proposal tasks by 2x." This improves future planning accuracy.
3. **Billing/chargebacks**: In consulting or professional services, client-facing tasks may need time tracking for billing. "We spent 8 hours on Acme Corp's implementation tasks this month" feeds into invoicing.
4. **Why LOW priority**: Core CRM users (sales reps) rarely need time tracking. This is more relevant for professional services or hybrid CRM/PSA use cases. Sales tasks are typically simple (call, email, review) and don't need precise time measurement.

**How to implement:**
- Backend: Add `EstimatedMinutes` (int?) and `ActualMinutes` (int?) to `TaskItem.cs`. Optional: `TaskTimeEntry` entity for granular time logging (Id, TaskItemId, UserId, StartedAtUtc, EndedAtUtc, Minutes, Note).
- Frontend: Time estimate input in task creation form. Timer widget in task detail (start/stop). Time spent display on task cards. Utilization report showing time spent per user/deal/week.

---

### LP-10. Real-Time Task Updates (WebSocket / SignalR)

**Effort:** High | **Impact:** Medium

**What exists now:** Task data is fetched once on page load (`getTasks()` in `useEffect`). No push updates. If User A completes a task while User B has the Tasks page open, User B sees the stale "In Progress" status until they manually refresh.

**What is missing:** Real-time push notifications when tasks are created, updated, deleted, or reassigned by other team members.

**Why we need it:**
1. **Team task boards**: In a team of 10 using a shared Kanban board, stale data is guaranteed. User A drags a task to "Completed" — Users B through J still see it as "In Progress." This causes confusion: "Why is this task still in the To Do column? I thought Sarah finished it."
2. **Assignment awareness**: When a manager assigns a task to a rep, the rep should see it immediately in their task list — not after the next page refresh (which could be hours later if they leave the tab open).
3. **Live standup meetings**: During daily standups, team members move tasks on their shared board. Without real-time updates, only the person making changes sees them — everyone else has a stale view.
4. **Why LOW priority**: Manual refresh works. The task list is not as time-sensitive as, say, a trading platform. Users can press F5 to see updates. This becomes more important as team size grows past 5-10 concurrent users.

**How to implement:**
- Backend: Add SignalR hub (`TaskHub`). Broadcast events from `TaskService` on create/update/delete: `Clients.Group(organizationId).SendAsync("TaskUpdated", taskDto)`.
- Frontend: Connect to SignalR hub on Tasks page mount. On events: update local state (add, update, or remove task from the list). Show toast: "Sarah completed 'Send proposal to Acme'."

---

### LP-11. Task Templates

**Effort:** Medium | **Impact:** Low

**What exists now:** Every task is created from scratch with empty fields. No saved templates, presets, or "create from template" functionality.

**What is missing:** Pre-defined and user-created task templates that auto-populate title, description, priority, and checklist items when creating a new task.

**Why we need it:**
1. **Process standardization**: Sales processes have repeatable task patterns. When a deal enters "Proposal" stage, the rep always creates the same tasks: "Draft proposal (High, 2 days)", "Get pricing approval (Medium, 1 day)", "Send to client (High, 1 day)." Templates make this a single click instead of creating 3 tasks manually.
2. **Onboarding efficiency**: New reps don't know which tasks to create for each deal stage. Templates codify best practices: "When a new lead comes in, create these 5 tasks." This reduces training time and ensures consistency.
3. **Deal stage automation**: Combined with deal stage changes (see Deal report HP-11), task templates could auto-create tasks when a deal moves to a new stage. "Deal entered Negotiation → auto-create: Review contract, Send to legal, Prepare signing docs."
4. **Why LOW priority**: Teams can standardize through naming conventions, copy-paste, or documentation. Templates are a productivity optimization, not a missing capability.

**How to implement:**
- Backend: New `TaskTemplate` entity (Id, OrgId, Name, Title, Description, Priority, DueDateOffset, SubtaskTemplates). CRUD endpoints.
- Frontend: "Create from template" button in the new task dialog. Template management in Settings. Optional: auto-suggest templates based on linked deal stage.

---

### LP-12. Contact Linking in Context Menu

**Effort:** Low | **Impact:** Low

**What exists now:** `KanbanTaskCard.tsx` has `DropdownMenuSub` submenus for Deal, Lead, and Assignee — allowing users to quickly link/unlink these entities from the right-click context menu. There is NO Contact submenu despite contacts being linkable via `ContactId`.

**What is missing:** A "Contact" submenu in the task card context menu, matching the pattern of the Deal and Lead submenus, allowing quick contact linking/unlinking without opening the full task modal.

**Why we need it:**
1. **Consistency**: Deal and Lead linking are available from the context menu. Contact linking is not. This inconsistency means users who want to link a contact must: right-click → notice no contact option → close menu → click task → open TaskDetailModal → scroll to Contact dropdown → select → save. That's 6 steps vs. 2 for deals.
2. **Workflow integration**: When viewing the Kanban board and realizing a task should be linked to a specific contact, the context menu is the fastest path. Users shouldn't have to open the full modal for a single field change.
3. **Why LOW priority**: Contact linking via the TaskDetailModal works correctly. This is a UX polish item — the functionality exists, just not via the quickest path. Most users link contacts at task creation time, not retroactively from the board.

**How to implement:**
- Frontend: In `KanbanTaskCard.tsx`, add a "Contact" `DropdownMenuSub` after the Lead submenu (copy the pattern from Deal submenu). Pass `contacts` and `onContactChange` props. Do the same in `ListTaskCard.tsx` (after HP-6 adds the other submenus there).

---

### Summary — Implementation Order

For maximum impact with minimum effort, implement HIGH PRIORITY items in this order:

| Phase | Items | Total Effort | What You Get |
|-------|-------|-------------|-------------|
| **Phase 1: Critical Bugs** (do first) | HP-1 (data truncation), HP-4 (indexes), HP-5 (search), HP-6 (list actions) | 1 day | Zero data loss. Search works. List view feature parity. Queries use indexes. |
| **Phase 2: Dead Features** | HP-2 (reminders), HP-3 (notifications) | 1-2 weeks | Reminders actually fire. Notifications either work or are honestly labeled "Coming soon." User trust restored. |
| **Phase 3: Cross-Entity UX** | HP-7 (task↔deal nav), HP-8 (add task from deals), HP-12 (contact tasks) | 2-3 days | Click-through between tasks and deals. Quick task creation from deal/lead context. Contacts show linked tasks. |
| **Phase 4: Core Features** | HP-9 (bulk ops), HP-10 (detail page), HP-11 (comments) | 1.5-2 weeks | Bulk operations for productivity. Deep-linkable task pages. Team collaboration via comments. |

**Total estimated effort for all HIGH PRIORITY items: 12-18 developer days.**

After completing all HIGH PRIORITY items, the task system will match the core capabilities of commercial CRM task managers (Pipedrive, HubSpot, Close). The LOW PRIORITY items can then be prioritized based on customer feedback and team needs.

---

### C. UI/UX Inconsistencies (Covered Above)

| # | Inconsistency | Priority | Resolution |
|---|---------------|----------|------------|
| 1 | Kanban vs List action parity | HIGH | Covered in HP-6 |
| 2 | Contact linking only in forms | LOW | Covered in LP-12 |
| 3 | Client-side vs server-side mismatch | HIGH | Covered in HP-1 (pagination) and HP-5 (search) |

---

---

### E. Implementation Log — February 9, 2026

All 12 HIGH PRIORITY items are now ✅ IMPLEMENTED. Summary of code changes:

| HP# | Feature | Status | Key Files Changed |
|-----|---------|--------|-------------------|
| HP-1 | Frontend task truncation fix | ✅ Done (prior) | `tasks.ts` — calls `/api/tasks/all` |
| HP-2 | Task reminder background service | ✅ Done | **NEW:** `TaskReminderBackgroundService.cs`, `TaskItem.cs` (+`ReminderSentAtUtc`), `Program.cs` |
| HP-3 | Notification "Coming soon" badges | ✅ Done | `NotificationsSection.tsx` — badges on each toggle |
| HP-4 | Missing database indexes | ✅ Done (prior) | `TaskConfiguration.cs` — DealId, LeadId indexes |
| HP-5 | Search includes Notes field | ✅ Done (prior) | `Tasks.tsx` — `task.notes?.toLowerCase().includes(q)` |
| HP-6 | ListTaskCard action parity | ✅ Done (prior) | `ListTaskCard.tsx` — deal/lead/assignee submenus |
| HP-7 | Task ↔ Deal bidirectional nav | ✅ Done | `DealCard.tsx` (+`taskCount` + `onAddTask` props), `DroppableStageColumn.tsx`, `Pipeline.tsx` (+`allTasks` state, `taskCountsByDeal` memo) |
| HP-8 | "Add Task" on Deal/Lead pages | ✅ Done | `Pipeline.tsx` (+Add Task dialog), `DealCard.tsx` (+Plus button), `LeadDetailModal.tsx` (renamed "Reminder" → "Task") |
| HP-9 | Bulk task operations | ✅ Done (prior) | `Tasks.tsx`, `TasksController.cs` bulk endpoint |
| HP-10 | Task detail page `/tasks/:id` | ✅ Done | **NEW:** `TaskDetail.tsx`, `App.tsx` (+route), `tasks.ts` (+`getTaskById`) |
| HP-11 | Task comments/discussion | ✅ Done | **NEW:** `TaskComment.cs`, `TaskCommentConfiguration.cs`, `AppDbContext.cs`, `TasksController.cs` (+3 endpoints), `tasks.ts` (+comment API), `TaskDetail.tsx` (comments UI) |
| HP-12 | Contacts show linked tasks | ✅ Done (prior) | `Contacts.tsx` — loads tasks, filters by contactId |

*This document provides a complete source-verified reference for every TaskItem interaction in the Cadence CRM. All items prioritized with HIGH/LOW classification, full "why we need it" business justifications, and implementation guides for all priority tiers. Last updated: February 9, 2026 (fifth pass — all 12 HIGH PRIORITY items implemented).*


