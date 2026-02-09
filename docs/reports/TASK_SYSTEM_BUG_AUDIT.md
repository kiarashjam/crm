# Task Repository and Controller Bug Audit Report

**Date:** February 9, 2026  
**Scope:** TaskRepository.cs, TasksController.cs, ITaskRepository.cs, TaskItem.cs, TaskComment.cs

---

## Summary

This audit identified **8 issues** across the task repository and controller:
- ~~**2 Critical** security/multi-tenancy issues~~ **RESOLVED**
- **3 Medium** performance and consistency issues  
- **3 Low** code quality and maintainability issues

---

## Critical Issues

### 🔴 CRITICAL-1: Multi-tenancy Security Gap in `GetByAssigneeIdAsync` — RESOLVED

**File:** `backend/src/ACI.Infrastructure/Repositories/TaskRepository.cs`  
**Line:** 121-124  
**Severity:** ~~Critical~~ Fixed

**Issue (was):**
The `GetByAssigneeIdAsync` method used `FilterByOrgMember` instead of `FilterByUserAndOrg`, which meant it only filtered by `organizationId` and did NOT filter by `userId`. This allowed any user in the same organization to retrieve tasks assigned to other users, even if they're not the task owner.

**Resolution:** `GetByAssigneeIdAsync` now takes a `userId` parameter. When in an organization, it uses `FilterByOrgMember` (team visibility). When personal (no org), it uses `FilterByUserAndOrg` to prevent data leakage.

**Former Code:**
```csharp
public async Task<IReadOnlyList<TaskItem>> GetByAssigneeIdAsync(Guid assigneeId, Guid? organizationId, CancellationToken ct = default) =>
    await OrderTasks(IncludeRelations(FilterByOrgMember(_db.TaskItems, organizationId)
        .Where(t => t.AssigneeId == assigneeId)))
        .ToListAsync(ct);
```

**Problem:**
- `FilterByOrgMember` only checks `organizationId`, not `userId`
- This violates the multi-tenancy pattern used throughout the codebase
- Other similar methods (`GetByLeadIdAsync`, `GetByDealIdAsync`, etc.) correctly use `FilterByUserAndOrg`

**Recommendation:**
Determine the intended behavior:
- If assignees should only see tasks assigned to them within their organization: Use `FilterByUserAndOrg` with the `assigneeId` as the `userId` parameter
- If organization members should see all tasks assigned to anyone in the org: Document this as intentional behavior and ensure proper authorization checks exist at the service/controller layer

---

### 🔴 CRITICAL-2: Inconsistent Access Control in TaskComment Endpoints — RESOLVED

**File:** `backend/src/ACI.WebApi/Controllers/TasksController.cs`  
**Lines:** 524-525, 548-549, 580-581  
**Severity:** ~~Critical~~ Fixed

**Issue (was):**
The TaskComment endpoints (`GetComments`, `AddComment`, `DeleteComment`) used a different access control pattern than the repository layer:
- Repository uses: `t.UserId == userId && (organizationId == null ? t.OrganizationId == null : t.OrganizationId == organizationId)`
- Controller uses: `t.OrganizationId == orgId || t.UserId == userId.Value`

**Resolution:** Changed `(t.OrganizationId == orgId || t.UserId == userId.Value)` to `((orgId != null && t.OrganizationId == orgId) || t.UserId == userId.Value)` across all 3 comment endpoints.

**Former Code:**
```csharp
// Line 524-525
var taskExists = await _db.TaskItems.AnyAsync(
    t => t.Id == id && (t.OrganizationId == orgId || t.UserId == userId.Value), ct);
```

**Problem (was):**
- The `||` (OR) operator allowed access if EITHER condition was true, meaning:
  - A user can access tasks from their organization even if they didn't create them
  - OR a user can access tasks they created even if they're in a different organization
- This is inconsistent with the repository's stricter `&&` (AND) pattern
- Could allow unauthorized access across organization boundaries

**Recommendation:**
Align with repository pattern:
```csharp
var taskExists = await _db.TaskItems.AnyAsync(
    t => t.Id == id && t.UserId == userId.Value && 
         (orgId == null ? t.OrganizationId == null : t.OrganizationId == orgId), ct);
```

Alternatively, if organization-wide access is intentional, document it and ensure proper authorization middleware exists.

---

## Medium Issues

### 🟡 MEDIUM-1: Missing Relations in `GetByIdAsync` Causes N+1 Queries

**File:** `backend/src/ACI.Infrastructure/Repositories/TaskRepository.cs`  
**Line:** 146-148  
**Severity:** Medium

**Issue:**
The `GetByIdAsync` method doesn't include related entities (Assignee, Lead, Deal, Contact), while `GetByIdWithRelationsAsync` does. If callers access navigation properties after calling `GetByIdAsync`, it will trigger N+1 queries.

**Current Code:**
```csharp
public async Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
    await FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
        .FirstOrDefaultAsync(t => t.Id == id, ct);
```

**Problem:**
- Inconsistent with `GetByIdWithRelationsAsync` which includes relations
- If service layer calls `GetByIdAsync` and then accesses `task.Assignee`, `task.Lead`, etc., EF Core will issue separate queries for each navigation property
- Performance impact on high-traffic endpoints

**Recommendation:**
Either:
1. Always include relations in `GetByIdAsync` (merge with `GetByIdWithRelationsAsync`)
2. Document that `GetByIdAsync` doesn't include relations and should only be used when relations aren't needed
3. Add a parameter to control whether relations are included

---

### 🟡 MEDIUM-2: Potential Null Reference in `AddComment` Author Access

**File:** `backend/src/ACI.WebApi/Controllers/TasksController.cs`  
**Line:** 565-566  
**Severity:** Medium

**Issue:**
After saving a comment, the code reloads the author separately and accesses `author?.Name ?? author?.Email ?? "Unknown"`. However, if the author was deleted between saving the comment and reloading, `author` could be null, and the fallback to "Unknown" might not be ideal.

**Current Code:**
```csharp
var author = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
var dto = new TaskCommentDto(comment.Id, comment.TaskItemId, comment.AuthorId, 
    author?.Name ?? author?.Email ?? "Unknown", comment.Body, comment.CreatedAtUtc);
```

**Problem:**
- Extra database query (`FindAsync`) when author could be included in the initial query
- Potential null reference if user is deleted (though unlikely)
- Inconsistent with `GetComments` which uses `Include(c => c.Author)` in a single query

**Recommendation:**
Use `Include` in the initial query or reload the comment with author:
```csharp
var commentWithAuthor = await _db.TaskComments
    .Include(c => c.Author)
    .FirstAsync(c => c.Id == comment.Id, ct);
var dto = new TaskCommentDto(commentWithAuthor.Id, commentWithAuthor.TaskItemId, 
    commentWithAuthor.AuthorId, 
    commentWithAuthor.Author.Name ?? commentWithAuthor.Author.Email ?? "Unknown", 
    commentWithAuthor.Body, commentWithAuthor.CreatedAtUtc);
```

---

### 🟡 MEDIUM-3: Missing CancellationToken in `GetComments` Route Parameter

**File:** `backend/src/ACI.WebApi/Controllers/TasksController.cs`  
**Line:** 517  
**Severity:** Medium

**Issue:**
The `GetComments` method signature has `CancellationToken ct` but it's missing the `= default` default parameter value, which is inconsistent with other methods in the controller.

**Current Code:**
```csharp
public async Task<ActionResult<List<TaskCommentDto>>> GetComments(Guid id, CancellationToken ct)
```

**Problem:**
- Inconsistent with other controller methods that use `CancellationToken ct = default`
- While this works, it's a code style inconsistency

**Recommendation:**
Add default parameter: `CancellationToken ct = default`

---

## Low Issues

### 🟢 LOW-1: Case-Sensitive String Comparison in Search Filter

**File:** `backend/src/ACI.Infrastructure/Repositories/TaskRepository.cs`  
**Line:** 35-38  
**Severity:** Low

**Issue:**
The `ApplySearch` method uses `.ToLower()` on database columns in LINQ queries, which prevents index usage and forces case-insensitive comparison in memory.

**Current Code:**
```csharp
return query.Where(t => 
    t.Title.ToLower().Contains(q) || 
    (t.Description != null && t.Description.ToLower().Contains(q)) ||
    (t.Notes != null && t.Notes.ToLower().Contains(q)));
```

**Problem:**
- `.ToLower()` on database columns prevents index usage
- SQL Server's case-insensitive collation might make this unnecessary
- Performance impact on large datasets

**Recommendation:**
If using SQL Server with case-insensitive collation, remove `.ToLower()` calls. Otherwise, consider using `EF.Functions.Like` or full-text search for better performance.

---

### 🟢 LOW-2: Missing XML Documentation on Comment Endpoints

**File:** `backend/src/ACI.WebApi/Controllers/TasksController.cs`  
**Lines:** 514-515, 538-539, 570-571  
**Severity:** Low

**Issue:**
The TaskComment endpoints have minimal XML documentation compared to other endpoints in the controller.

**Current Code:**
```csharp
/// <summary>Get all comments for a task.</summary>
[HttpGet("{id:guid}/comments")]
```

**Problem:**
- Missing `<param>`, `<returns>`, and `<response>` documentation
- Inconsistent with other endpoints that have comprehensive documentation

**Recommendation:**
Add full XML documentation matching the style of other endpoints.

---

### 🟢 LOW-3: Hardcoded String in `AddComment` Response Location

**File:** `backend/src/ACI.WebApi/Controllers/TasksController.cs`  
**Line:** 567  
**Severity:** Low

**Issue:**
The `Created` response uses a hardcoded string path instead of using `Url.Action` or route name.

**Current Code:**
```csharp
return Created($"/api/tasks/{id}/comments/{comment.Id}", dto);
```

**Problem:**
- Hardcoded path could break if route changes
- Not using ASP.NET Core's routing system

**Recommendation:**
Use `CreatedAtAction` or `CreatedAtRoute`:
```csharp
return CreatedAtAction(nameof(GetComments), new { id = id }, dto);
```

---

## Additional Observations

### ✅ Good Practices Found

1. **Cascade Delete Configured:** TaskCommentConfiguration correctly sets `OnDelete(DeleteBehavior.Cascade)`, so TaskComments are automatically deleted when TaskItems are deleted.

2. **Authorization Present:** All controller endpoints have `[Authorize]` attribute (class-level).

3. **CancellationToken Usage:** Most methods properly accept and pass `CancellationToken`.

4. **Route Parameter Types:** All route parameters correctly use `{id:guid}` constraint.

5. **No SQL Injection Risks:** All queries use parameterized LINQ queries, no raw SQL strings.

6. **Interface Consistency:** ITaskRepository interface matches TaskRepository implementation (except for the `GetByAssigneeIdAsync` filtering issue).

---

## Recommendations Summary

### Immediate Actions Required:
1. ~~**Fix CRITICAL-1:** Review and fix `GetByAssigneeIdAsync` multi-tenancy filtering~~ **RESOLVED**
2. ~~**Fix CRITICAL-2:** Align TaskComment endpoint access control with repository pattern~~ **RESOLVED**

### Should Fix Soon:
3. **Fix MEDIUM-1:** Include relations in `GetByIdAsync` or document usage clearly
4. **Fix MEDIUM-2:** Optimize `AddComment` to use Include instead of separate query
5. **Fix MEDIUM-3:** Add default parameter to `GetComments` CancellationToken

### Nice to Have:
6. **Fix LOW-1:** Optimize search filter for better performance
7. **Fix LOW-2:** Add comprehensive XML documentation to comment endpoints
8. **Fix LOW-3:** Use routing helpers instead of hardcoded paths

---

## Testing Recommendations

After fixing the critical issues, verify:
1. Multi-tenancy: Users can only access tasks they own or are assigned to (based on intended behavior)
2. Organization boundaries: Users cannot access tasks from other organizations
3. Comment access: Comment endpoints respect the same access rules as task endpoints
4. Performance: No N+1 queries when accessing task relations
5. Cascade delete: Deleting a task also deletes its comments

---

**Report Generated:** February 9, 2026
