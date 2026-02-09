# Task Reference Bug Audit Report

## Overview
This audit examines how tasks are referenced, loaded, and displayed across pages that are NOT the main task pages (Tasks.tsx). The audit focuses on data consistency, API usage, filtering, and display issues.

---

## Critical Issues

### 1. **DealDetail.tsx - Missing Task Display**
**File:** `src/app/pages/DealDetail.tsx`  
**Line:** Entire file  
**Severity:** Critical  
**Issue:** The DealDetail page does NOT display tasks linked to the deal, even though:
- ContactDetail.tsx shows tasks for contacts (lines 354-411)
- Tasks can be linked to deals (API supports `dealId` parameter)
- Pipeline page shows task counts on deal cards
- Users expect to see tasks on deal detail pages

**Expected Behavior:** DealDetail should load and display tasks using `getTasksByDeal(dealId)` similar to how ContactDetail uses `getTasksByContact(contactId)`.

**Impact:** Users cannot view or manage tasks directly from deal detail pages, creating inconsistent UX compared to contact pages.

---

### 2. **LeadDetailModal.tsx - Inefficient Task Loading**
**File:** `src/app/pages/leads/LeadDetailModal.tsx`  
**Line:** 129-141  
**Severity:** Critical  
**Issue:** The `loadTasks()` function loads ALL tasks using `getTasks()` and then filters client-side:
```typescript
const allTasks = await getTasks();
setTasks(allTasks.filter(t => t.leadId === lead.id));
```

**Problem:**
- Loads unnecessary data (all tasks instead of just tasks for this lead)
- Performance impact when there are many tasks
- API endpoint `/api/tasks/by-lead/${leadId}` exists but is not used

**Expected Behavior:** Should use `getTasksByLead(lead.id)` which calls `/api/tasks/by-lead/${leadId}`.

**Impact:** Poor performance, unnecessary network traffic, potential memory issues with large datasets.

---

### 3. **Contacts.tsx - Inefficient Task Loading**
**File:** `src/app/pages/Contacts.tsx`  
**Line:** 114-125  
**Severity:** Critical  
**Issue:** Loads ALL tasks using `getTasks()` on every page load, then filters client-side:
```typescript
const [allTasks, setAllTasks] = useState<TaskItem[]>([]);
// ...
getTasks(), // HP-12: Load all tasks for contact visibility
// ...
const getTasksForContact = (contactId: string) => allTasks.filter(t => t.contactId === contactId);
```

**Problem:**
- Loads all tasks even when only displaying a subset of contacts
- Re-fetches all tasks on every page change/search
- No server-side filtering despite API supporting `contactId` parameter

**Expected Behavior:** 
- Option 1: Load tasks per contact lazily when needed
- Option 2: Use server-side filtering if loading all contacts' tasks is required
- Option 3: Load tasks only for visible contacts (pagination-aware)

**Impact:** Performance degradation, unnecessary API calls, poor scalability.

---

## Medium Issues

### 4. **Pipeline.tsx - Task Count Includes Completed Tasks**
**File:** `src/app/pages/Pipeline.tsx`  
**Line:** 137-143  
**Severity:** Medium  
**Issue:** Task count computation includes ALL tasks (completed and incomplete):
```typescript
const taskCountsByDeal = useMemo(() => {
  const counts: Record<string, number> = {};
  for (const t of allTasks) {
    if (t.dealId) counts[t.dealId] = (counts[t.dealId] || 0) + 1;
  }
  return counts;
}, [allTasks]);
```

**Problem:** 
- Counts completed tasks, which may not be useful for users
- Inconsistent with common UX patterns where task counts typically exclude completed items
- No filtering by status

**Expected Behavior:** Filter out completed tasks:
```typescript
if (t.dealId && t.status !== 'completed') counts[t.dealId] = (counts[t.dealId] || 0) + 1;
```

**Impact:** Misleading task counts, users may think there are more active tasks than there actually are.

---

### 5. **Pipeline.tsx - Inefficient Task Loading**
**File:** `src/app/pages/Pipeline.tsx`  
**Line:** 284  
**Severity:** Medium  
**Issue:** Loads ALL tasks using `getTasks()` without any filters:
```typescript
Promise.all([getDealsPaged({ page: 1, pageSize: 500 }), getContacts(), getPipelines(), getTasks()])
```

**Problem:**
- Loads all tasks even though only need tasks linked to deals
- Could use `getTasks({ dealId: ... })` but that requires multiple calls
- No server-side filtering despite API supporting `dealId` parameter

**Expected Behavior:** 
- Option 1: Load tasks filtered by deal IDs (if API supports bulk filtering)
- Option 2: Load tasks lazily per deal when needed
- Option 3: Accept the current approach but document the trade-off

**Impact:** Performance impact with large task datasets, unnecessary data transfer.

---

### 6. **LeadDetailModal.tsx - Task Creation Missing Error Handling**
**File:** `src/app/pages/leads/LeadDetailModal.tsx`  
**Line:** 348-384  
**Severity:** Medium  
**Issue:** Task creation doesn't refresh the task list if the API call succeeds but returns null:
```typescript
const task = await createTask({
  title: newTask.title.trim(),
  dueDateUtc: newTask.dueDate || undefined,
  leadId: lead.id,
});
if (task) {
  setTasks(prev => [...prev, task]);
  // ...
}
```

**Problem:**
- If `createTask` returns null (e.g., API error but no exception), task list doesn't refresh
- User may think task was created but it wasn't
- Should reload tasks from server after creation to ensure consistency

**Expected Behavior:** After successful creation, reload tasks using `loadTasks()` to ensure consistency with server state.

**Impact:** Potential data inconsistency between UI and server state.

---

### 7. **Pipeline.tsx - Task Creation Doesn't Refresh All Tasks**
**File:** `src/app/pages/Pipeline.tsx`  
**Line:** 568-584  
**Severity:** Medium  
**Issue:** After creating a task, only adds it to local state without refreshing from server:
```typescript
const task = await createTask({ title: addTaskTitle.trim(), dealId: addTaskDealId });
if (task) {
  setAllTasks((prev) => [...prev, task]);
  // ...
}
```

**Problem:**
- Doesn't reload tasks from server after creation
- If server adds additional fields or modifies the task, UI won't reflect those changes
- Potential inconsistency if task creation triggers side effects on server

**Expected Behavior:** After successful creation, reload tasks using `getTasks()` or at least refresh the specific deal's tasks.

**Impact:** Potential data inconsistency, missing server-side computed fields.

---

## Low Issues

### 8. **Contacts.tsx - Task Filtering Logic**
**File:** `src/app/pages/Contacts.tsx`  
**Line:** 368  
**Severity:** Low  
**Issue:** Task filtering function is simple but works correctly:
```typescript
const getTasksForContact = (contactId: string) => allTasks.filter(t => t.contactId === contactId);
```

**Note:** This is actually correct, but the inefficiency comes from loading all tasks (see Critical Issue #3).

**Impact:** None (function itself is correct).

---

### 9. **DealCard.tsx - Task Count Display**
**File:** `src/app/pages/pipeline/DealCard.tsx`  
**Line:** 215-231  
**Severity:** Low  
**Issue:** Task count display works correctly, but shows "0 tasks" when `taskCount` is undefined:
```typescript
{taskCount !== undefined || onAddTask) && (
  <div className="mt-2 flex items-center gap-2 text-xs text-slate-400 dark:text-slate-500">
    <ListTodo className="w-3 h-3" />
    <span>{taskCount ?? 0} task{taskCount !== 1 ? 's' : ''}</span>
```

**Note:** This is actually correct behavior (defaults to 0), but could be improved to hide the section when `taskCount` is undefined and `onAddTask` is not provided.

**Impact:** Minor UX issue - shows "0 tasks" even when task count isn't loaded yet.

---

### 10. **DroppableStageColumn.tsx - Task Counts Passed Correctly**
**File:** `src/app/pages/pipeline/DroppableStageColumn.tsx`  
**Line:** 184  
**Severity:** Low (No Issue)  
**Status:** ✅ **CORRECT**  
**Note:** Task counts are passed correctly to DealCard components:
```typescript
taskCount={taskCountsByDeal?.[deal.id]}
```

**Impact:** None - this is working as expected.

---

### 11. **ContactDetail.tsx - Correct API Usage**
**File:** `src/app/pages/ContactDetail.tsx`  
**Line:** 86-93  
**Severity:** Low (No Issue)  
**Status:** ✅ **CORRECT**  
**Note:** Uses the correct API function:
```typescript
getTasksByContact(id)
  .then(setContactTasks)
```

**Impact:** None - this is the correct pattern that should be followed elsewhere.

---

### 12. **Dashboard.tsx - No Task References**
**File:** `src/app/pages/Dashboard.tsx`  
**Severity:** Low (No Issue)  
**Status:** ✅ **NO TASKS REFERENCED**  
**Note:** Dashboard doesn't reference tasks, which is expected behavior.

**Impact:** None.

---

## API Function Analysis

### `src/app/api/tasks.ts` - Function Review

**✅ Correct Functions:**
- `getTasksByContact(contactId)` - Properly filters by contactId
- `getTasksByLead(leadId)` - Properly filters by leadId  
- `getTasksByDeal(dealId)` - Properly filters by dealId
- `getTasks(options)` - Supports filtering via options object

**⚠️ Usage Issues:**
- Many pages call `getTasks()` without filters instead of using specific functions
- Client-side filtering used instead of server-side filtering

---

## Summary

### Critical Issues: 3
1. DealDetail.tsx missing task display entirely
2. LeadDetailModal.tsx inefficient task loading
3. Contacts.tsx inefficient task loading

### Medium Issues: 4
4. Pipeline.tsx task count includes completed tasks
5. Pipeline.tsx inefficient task loading
6. LeadDetailModal.tsx task creation refresh issue
7. Pipeline.tsx task creation refresh issue

### Low Issues: 2
8. Contacts.tsx task filtering (works but inefficient due to #3)
9. DealCard.tsx task count display (minor UX)

### Correct Implementations: 3
- ContactDetail.tsx (correct API usage)
- DroppableStageColumn.tsx (correct prop passing)
- Dashboard.tsx (no tasks expected)

---

## Recommendations

1. **Immediate Actions:**
   - Add task display to DealDetail.tsx using `getTasksByDeal()`
   - Fix LeadDetailModal.tsx to use `getTasksByLead()` instead of filtering client-side
   - Optimize Contacts.tsx task loading (lazy load or server-side filter)

2. **Short-term Improvements:**
   - Filter out completed tasks from Pipeline.tsx task counts
   - Add task refresh after creation in LeadDetailModal and Pipeline
   - Consider pagination for task lists on detail pages

3. **Long-term Considerations:**
   - Implement server-side filtering for bulk task queries
   - Add caching strategy for frequently accessed task lists
   - Consider GraphQL or similar for more efficient data fetching

---

## Testing Checklist

- [ ] DealDetail page shows tasks linked to deal
- [ ] LeadDetailModal loads only tasks for the specific lead
- [ ] Contacts page loads tasks efficiently (not all tasks)
- [ ] Pipeline task counts exclude completed tasks
- [ ] Task creation refreshes task lists properly
- [ ] Task counts update correctly after task creation/deletion
- [ ] All pages use appropriate API endpoints (not client-side filtering)
