// Referral persistence for leads.
//
// The backend's UpdateLeadRequest has no `referredByContactId` field, so a
// lead's referral can't be saved through the normal updateLead() call. Both the
// leads list and the lead detail page therefore mirror the referral locally,
// keyed by lead id, and resolve the contact name from the loaded contacts. This
// module is the single source of truth for that storage so the two pages stay
// in sync (and share one storage key).

const LEAD_REFERRALS_STORAGE_KEY = 'crm.leadReferrals.v1';

/** Map of leadId -> referring contactId. */
export function loadLeadReferrals(): Record<string, string> {
  try {
    const raw = localStorage.getItem(LEAD_REFERRALS_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, string>;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

export function saveLeadReferrals(referrals: Record<string, string>): void {
  try {
    localStorage.setItem(LEAD_REFERRALS_STORAGE_KEY, JSON.stringify(referrals));
  } catch {
    // ignore storage failures
  }
}

/** Set or clear a single lead's referral, persisting the whole map. */
export function setLeadReferral(leadId: string, contactId: string | undefined): void {
  const referrals = loadLeadReferrals();
  if (contactId) referrals[leadId] = contactId;
  else delete referrals[leadId];
  saveLeadReferrals(referrals);
}
