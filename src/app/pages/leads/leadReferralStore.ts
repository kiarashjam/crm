// Referrals saved only in this browser, from before the server kept them.
//
// A lead's referrer used to live here, keyed by lead id, because the server had
// no field for it, so a teammate never saw it. The server now saves it on the
// lead (PUT /api/leads/:id/referral). Nothing writes here any more: this map is
// read once by LeadReferralMigrationDialog, which offers to save what it holds
// for the team and then forgets each entry it saved.

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

/** Forget these leads' browser-only referrals: they are on the server now, or no longer apply. */
export function forgetLeadReferrals(leadIds: string[]): void {
  const referrals = loadLeadReferrals();
  for (const id of leadIds) delete referrals[id];
  saveLeadReferrals(referrals);
}
