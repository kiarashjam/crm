# Task Service Layer Bug Audit Report

**Date:** February 9, 2026  
**Scope:** `backend/src/ACI.Application/Services/TaskService.cs` and related DTOs

---

## Summary

Found **5 issues** across the TaskService implementation:
- ~~**2 Critical** issues that can cause runtime exceptions~~ **RESOLVED** (both fixed)
- ~~**2 Medium** issues that can cause incorrect behavior~~ **RESOLVED** (both fixed)
- **1 Low** issue (design consideration)

---

## Critical Issues

### 1. **~~NullReferenceException in UpdatePriorityAsync~~ — RESOLVED** 
**File:** `TaskService.cs`  
**Line:** 712  
**Severity:** ~~Critical~~ Fixed

**Issue (was):**
The `UpdatePriorityAsync` method used `GetByIdAsync` instead of `GetByIdWithRelationsAsync`, which meant navigation properties were not loaded, causing `NullReferenceException` in `Map()`.

**Resolution:** Line 712 now uses `GetByIdWithRelationsAsync`, ensuring navigation properties are loaded before mapping.

**Current Code:**
```csharp
var existing = await _repository.GetByIdAsync(id, userId, organizationId, ct);
// ... later ...
return updated != null ? Map(updated) : DomainErrors.General.ServerError;
```

**Fix:**
Change line 707 to use `GetByIdWithRelationsAsync`:
```csharp
var existing = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
```

**Impact:** This will crash the application when bulk updating task priority.

---

### 2. **~~Missing Title Validation in UpdateAsync~~ — RESOLVED**
**File:** `TaskService.cs`  
**Line:** 241-245  
**Severity:** ~~Critical~~ Fixed

**Issue (was):**
The `UpdateAsync` method checked if `request.Title != null` but didn't validate that it's not empty or whitespace.

**Resolution:** Lines 241-245 now check `string.IsNullOrWhiteSpace(request.Title)` and return `DomainErrors.General.ValidationError` if empty/whitespace. Title is also trimmed before assignment.

**Current Code:**
```csharp
if (request.Title != null) existing.Title = request.Title;
```

**Fix:**
Add validation similar to `CreateAsync`:
```csharp
if (request.Title != null)
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        _logger.LogWarning("Task update failed - title cannot be empty");
        return DomainErrors.Task.TitleRequired;
    }
    existing.Title = request.Title;
}
```

**Impact:** Allows invalid data (empty titles) to be persisted, violating business rules.

---

## Medium Issues

### 3. **~~Missing Status Validation in BulkUpdateAsync~~ — RESOLVED**
**File:** `TaskService.cs`  
**Line:** 678  
**Severity:** ~~Medium~~ Fixed

**Issue (was):**
When `Action` was "status", the code checked `request.Status != null` but if it was null, it fell through to the default case and returned `DomainErrors.General.ValidationError`. However, this error was returned for each task individually, which meant the bulk operation would report all tasks as failed even though the real issue was a missing parameter.

**Resolution:** Added upfront validation for `Status` and `Priority` parameters before the loop.

**Current Code:**
```csharp
"status" when request.Status != null => 
    await UpdateStatusAsync(taskId, userId, organizationId, request.Status, ct),
```

**Fix:**
Add upfront validation before the loop:
```csharp
if (request.Action.ToLowerInvariant() == "status" && string.IsNullOrEmpty(request.Status))
{
    _logger.LogWarning("Bulk status update failed - Status is required");
    return DomainErrors.General.ValidationError;
}
if (request.Action.ToLowerInvariant() == "priority" && string.IsNullOrEmpty(request.Priority))
{
    _logger.LogWarning("Bulk priority update failed - Priority is required");
    return DomainErrors.General.ValidationError;
}
```

**Impact:** Poor error reporting - users won't understand why all tasks failed when the issue is a missing parameter.

---

### 4. **~~Potential NullReferenceException in GetTasksAsync~~ — RESOLVED**
**File:** `TaskService.cs`  
**Line:** 62-100  
**Severity:** ~~Medium~~ Fixed

**Issue (was):**
The `GetTasksAsync` method accepted `TaskFilterParams filters` as a non-nullable parameter, but the method accessed `filters.LeadId`, `filters.DealId`, etc. without null checks. If a null value was passed (which shouldn't happen but could due to a bug elsewhere), this would throw a `NullReferenceException`.

**Resolution:** Added `filters ??= new TaskFilterParams()` guard at method start.

**Current Code:**
```csharp
public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(
    Guid userId, 
    Guid? organizationId, 
    TaskFilterParams filters,  // Non-nullable but not validated
    CancellationToken ct = default)
{
    // Direct access to filters.LeadId, filters.DealId, etc.
```

**Fix:**
Add null check at the start:
```csharp
if (filters == null)
{
    _logger.LogWarning("GetTasksAsync called with null filters");
    filters = new TaskFilterParams();
}
```

**Impact:** Could cause unexpected crashes if called incorrectly from other code.

---

## Low Issues

### 5. **TryParseStatus/TryParsePriority Return True for Empty Strings**
**File:** `TaskService.cs`  
**Line:** 618-629, 639-650  
**Severity:** Low

**Issue:**
Both `TryParseStatus` and `TryParsePriority` return `true` for empty strings (line 628, 649), setting the result to default values (`Todo` and `None`). While this might be intentional for backward compatibility, it's inconsistent with typical parsing behavior where empty strings should return `false`.

**Current Code:**
```csharp
return !string.IsNullOrEmpty(status);  // Returns true for empty string
```

**Note:** This is likely intentional (defaulting to safe values), but worth documenting. If this is the desired behavior, consider adding a comment explaining why.

**Impact:** Low - works but may be confusing. Empty strings are treated as valid input and defaulted.

---

## Additional Observations

### Positive Findings:
1. ✅ **TaskDto Mapping:** The `Map()` method correctly includes all 20 parameters matching the `TaskDto` record definition.
2. ✅ **Clear Flags:** All clear flags (`ClearDueDate`, `ClearReminderDate`, `ClearAssignee`, `ClearLead`, `ClearDeal`, `ClearContact`) are properly handled in `UpdateAsync`.
3. ✅ **Status/Priority Enum Mapping:** The enum-to-string and string-to-enum conversions are correct and handle multiple input formats (e.g., "inprogress" and "in_progress").
4. ✅ **TaskStatsDto Computation:** The statistics calculation in `GetStatsAsync` correctly excludes completed and cancelled tasks from overdue/due today/high priority counts.
5. ✅ **Null Checks:** Most navigation property accesses use null-conditional operators (`?.`), preventing NullReferenceExceptions.

### Recommendations:
1. Consider adding input validation attributes to `UpdateTaskRequest.Title` similar to `CreateTaskRequest`.
2. Consider making `TaskFilterParams` nullable in `GetTasksAsync` signature or adding a default parameter value.
3. Add unit tests for edge cases (empty strings, null filters, bulk operations with missing parameters).

---

## Testing Recommendations

1. **Test UpdatePriorityAsync** with a task that has related entities (lead, deal, contact) to verify the fix.
2. **Test UpdateAsync** with empty string title to verify validation.
3. **Test BulkUpdateAsync** with missing Status parameter to verify error handling.
4. **Test GetTasksAsync** with null filters (if possible) to verify null handling.

---

## Priority Fix Order

1. ~~**Fix Issue #1** (UpdatePriorityAsync) - Prevents crashes~~ **RESOLVED**
2. ~~**Fix Issue #2** (Title validation) - Prevents invalid data~~ **RESOLVED**
3. ~~**Fix Issue #3** (BulkUpdate validation) - Improves error reporting~~ **RESOLVED**
4. ~~**Fix Issue #4** (Null check) - Defensive programming~~ **RESOLVED**
5. **Review Issue #5** (Empty string parsing) - Document or change behavior
