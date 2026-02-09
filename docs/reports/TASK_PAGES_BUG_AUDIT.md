# Task Pages Bug Audit Report

**Date:** February 9, 2026  
**Scope:** Frontend task pages and components  
**Files Analyzed:**
- `src/app/pages/Tasks.tsx`
- `src/app/pages/TaskDetail.tsx`
- `src/app/pages/tasks/components/TaskDetailModal.tsx`
- `src/app/pages/tasks/components/KanbanTaskCard.tsx`
- `src/app/pages/tasks/components/ListTaskCard.tsx`

---

## Critical Issues

### 1. **~~Stale Date Reference in Tasks.tsx~~ — RESOLVED**
**File:** `src/app/pages/Tasks.tsx`  
**Line:** 172  
**Severity:** ~~Critical~~ Fixed  
**Issue (was):** `const now = useMemo(() => new Date(), []);` created a date once and never updated.

**Resolution:** Line 172 now uses `useMemo(() => new Date(), [tasks])` — the date recomputes whenever tasks data changes, keeping date comparisons fresh.

---

### 2. **~~Missing Error Handling in handleDetailModalUpdate~~ — RESOLVED**
**File:** `src/app/pages/Tasks.tsx`  
**Line:** 715-741  
**Severity:** ~~Critical~~ Fixed  
**Issue (was):** `handleDetailModalUpdate` function had no try/catch block.

**Resolution:** The function (line 715+) now wraps the `updateTask` call in a `try/catch` block with `toast.error` on failure.

---

### 3. **~~Type Safety Gap - `as any` Assertion~~ — RESOLVED**
**File:** `src/app/pages/Tasks.tsx`  
**Line:** 663  
**Severity:** ~~Critical~~ Fixed  
**Issue (was):** `status: value as any` bypassed TypeScript type checking.

**Resolution:** Line 663 now uses `status: value as TaskStatusType` — proper type assertion instead of `any`.

---

## Medium Issues

### 4. **Missing Dependency in useEffect - TaskDetail.tsx**
**File:** `src/app/pages/TaskDetail.tsx`  
**Line:** 118  
**Severity:** Medium  
**Issue:** `loadTask` callback is missing from dependency array. While `loadTask` uses `id` which is in the dependency array, React best practices suggest including the callback itself.

**Impact:** Potential stale closure issues, though unlikely in this case.

**Fix:** Add `loadTask` to dependencies or use `useCallback` properly:
```typescript
useEffect(() => { loadTask(); }, [loadTask]);
```

---

### 5. **Inconsistent Error Handling in TaskDetail.tsx**
**File:** `src/app/pages/TaskDetail.tsx`  
**Line:** 141-149, 151-159, 161-177  
**Severity:** Medium  
**Issue:** `handleStatusChange`, `handlePriorityChange`, and `handleSaveEdit` catch errors but don't provide user feedback beyond toast. No loading states shown during API calls.

**Impact:** Users may not know if an operation is in progress or if it failed silently.

**Fix:** Add loading states and better error messages.

---

### 6. **~~Date Formatting Issue in TaskDetail.tsx~~ — RESOLVED**
**File:** `src/app/pages/TaskDetail.tsx`  
**Line:** 116, 169  
**Severity:** ~~Medium~~ Fixed  
**Issue (was):** `editDueDate` used `slice(0, 10)` which only got the date portion, but the input type is `datetime-local`. This caused time information to be lost.

**Resolution:** Changed to `slice(0, 16)` to preserve time for datetime-local input.

**Former Fix Suggestion:** Use `slice(0, 16)` for datetime-local inputs:
```typescript
setEditDueDate(t.dueDateUtc ? t.dueDateUtc.slice(0, 16) : '');
```

---

### 7. **Missing Error Handling in TaskDetailModal.tsx**
**File:** `src/app/pages/tasks/components/TaskDetailModal.tsx`  
**Line:** 120-142  
**Severity:** Medium  
**Issue:** `loadRelatedActivities` catches errors but only logs to console. No user feedback.

**Impact:** Users won't know if activity loading failed.

**Fix:** Add toast notification or error state.

---

### 8. **Potential Memory Leak - Missing Cleanup in TaskDetailModal.tsx**
**File:** `src/app/pages/tasks/components/TaskDetailModal.tsx`  
**Line:** 100-118  
**Severity:** Medium  
**Issue:** `useEffect` calls `loadRelatedActivities` but if the component unmounts or task changes before the async operation completes, it could update state on unmounted component.

**Impact:** Memory leak warnings, potential state updates on unmounted component.

**Fix:** Add cleanup:
```typescript
useEffect(() => {
  if (task) {
    // ... form setup
    let cancelled = false;
    loadRelatedActivities(task).then(() => {
      if (!cancelled) {
        // update state
      }
    });
    return () => { cancelled = true; };
  }
}, [task]);
```

---

### 9. **Missing Key Props in TaskDetailModal.tsx**
**File:** `src/app/pages/tasks/components/TaskDetailModal.tsx`  
**Line:** 689  
**Severity:** Medium  
**Issue:** Activities are mapped with `key={activity.id}` which is correct, but if `activity.id` is not unique or missing, React will warn.

**Impact:** React warnings, potential rendering issues.

**Note:** This is likely fine if API guarantees unique IDs, but worth verifying.

---

### 10. **~~Incomplete Field Updates in TaskDetailModal.tsx~~ — RESOLVED**
**File:** `src/app/pages/tasks/components/TaskDetailModal.tsx`  
**Line:** 161-169  
**Severity:** ~~Medium~~ Fixed  
**Issue (was):** `handleSave` in TaskDetailModal didn't send `leadId`, `dealId`, `contactId`, or `assigneeId` to the API, even though these fields were in the form state.

**Resolution:** Added leadId, dealId, contactId, assigneeId to the onUpdate call in handleSave.

**Former Fix Suggestion:** Include all form fields in the update:
```typescript
const updated = await onUpdate(task.id, {
  title: form.title.trim(),
  description: form.description.trim() || undefined,
  dueDateUtc,
  reminderDateUtc,
  status: form.status,
  priority: form.priority,
  notes: form.notes.trim() || undefined,
  leadId: form.leadId || undefined,
  dealId: form.dealId || undefined,
  contactId: form.contactId || undefined,
  assigneeId: form.assigneeId || undefined,
});
```

---

## Low Issues

### 11. **Redundant useCallback Wrappers**
**File:** `src/app/pages/Tasks.tsx`  
**Line:** 681-691  
**Severity:** Low  
**Issue:** `handleInlineAssigneeChange`, `handleInlineDealChange`, `handleInlineLeadChange` are just thin wrappers that call the parent handlers. The useCallback is unnecessary.

**Impact:** Minor performance overhead, code complexity.

**Fix:** Pass handlers directly or remove wrappers.

---

### 12. **Missing Loading State in Drag-and-Drop**
**File:** `src/app/pages/Tasks.tsx`  
**Line:** 604-618  
**Severity:** Low  
**Issue:** `handleDropTask` is async but doesn't show loading state during the API call. The optimistic update happens in `handleStatusChange`, but there's no visual feedback during the drag operation.

**Impact:** Users may not know if the drop is processing.

**Fix:** Add loading indicator or disable drag during API call.

---

### 13. **Potential Race Condition in Status Changes**
**File:** `src/app/pages/Tasks.tsx`  
**Line:** 450-473  
**Severity:** Low  
**Issue:** `handleStatusChange` optimistically updates state, then makes API call. If user rapidly changes status multiple times, there could be race conditions where later updates overwrite earlier ones.

**Impact:** Status might not reflect the last user action.

**Fix:** Add request cancellation or debouncing.

---

### 14. **Missing Validation in Quick Add**
**File:** `src/app/pages/Tasks.tsx`  
**Line:** 573-601  
**Severity:** Low  
**Issue:** `handleQuickAdd` checks for empty title but doesn't validate length or other constraints.

**Impact:** Could create tasks with invalid data.

**Fix:** Add validation similar to main form.

---

### 15. **Inconsistent Date Handling**
**File:** `src/app/pages/TaskDetail.tsx`  
**Line:** 169  
**Severity:** Low  
**Issue:** `dueDateUtc: editDueDate || undefined` - if `editDueDate` is empty string, it becomes `undefined`, but the conversion to ISO string is missing.

**Impact:** Date might not be properly formatted for API.

**Fix:** Convert to ISO string:
```typescript
dueDateUtc: editDueDate ? new Date(editDueDate).toISOString() : undefined,
```

---

## Feature Parity Issues

### 16. **KanbanTaskCard vs ListTaskCard Feature Differences**
**Severity:** Medium

**KanbanTaskCard has:**
- Drag-and-drop support
- `onViewDetails` callback support

**ListTaskCard has:**
- Status change dropdown (clicking status icon)
- Bulk selection checkbox support
- `onAssigneeChange`, `onDealChange`, `onLeadChange` callbacks (HP-6 feature)

**Missing in KanbanTaskCard:**
- Status change via clicking status icon (only via dropdown menu)
- Bulk selection support

**Impact:** Inconsistent UX between views. Users might expect status changes to work the same way in both views.

**Recommendation:** Consider adding status icon click handler to KanbanTaskCard for consistency.

---

## Summary

**Total Issues Found:** 16
- **Critical:** 3
- **Medium:** 7
- **Low:** 6

**Priority Actions:**
1. ~~Fix stale date reference (Critical #1)~~ **RESOLVED**
2. ~~Add error handling to `handleDetailModalUpdate` (Critical #2)~~ **RESOLVED**
3. ~~Fix type safety issue with `as any` (Critical #3)~~ **RESOLVED**
4. ~~Fix date formatting in TaskDetail.tsx (Medium #6)~~ **RESOLVED**
5. ~~Complete field updates in TaskDetailModal (Medium #10)~~ **RESOLVED**
6. Add error handling and loading states throughout (Medium #5, #7)

**Recommendations:**
- Add comprehensive error boundaries
- Implement consistent loading state patterns
- Add request cancellation for rapid user actions
- Consider adding unit tests for date handling logic
- Standardize error handling patterns across all components
