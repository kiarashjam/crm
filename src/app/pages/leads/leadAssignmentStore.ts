// Owner (assignee) persistence for leads.
//
// The backend's UpdateLeadRequest has no `assignedToId` field, so a lead's
// owner can't be saved through the normal updateLead() call. Both the leads
// list and the lead detail page therefore mirror the assignment locally, keyed
// by lead id. This module is the single source of truth for that storage so the
// two pages stay in sync (and share one storage key).

const LEAD_ASSIGNMENTS_STORAGE_KEY = 'crm.leadAssignments.v1';

/** Map of leadId -> assigned userId. */
export function loadLeadAssignments(): Record<string, string> {
  try {
    const raw = localStorage.getItem(LEAD_ASSIGNMENTS_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, string>;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

export function saveLeadAssignments(assignments: Record<string, string>): void {
  try {
    localStorage.setItem(LEAD_ASSIGNMENTS_STORAGE_KEY, JSON.stringify(assignments));
  } catch {
    // ignore storage failures
  }
}

/** Set or clear a single lead's owner, persisting the whole map. */
export function setLeadAssignment(leadId: string, userId: string | undefined): void {
  const assignments = loadLeadAssignments();
  if (userId) assignments[leadId] = userId;
  else delete assignments[leadId];
  saveLeadAssignments(assignments);
}
