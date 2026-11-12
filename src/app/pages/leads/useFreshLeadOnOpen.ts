// The lead an edit dialog is open on, read again as the dialog opens.
//
// The dialog's save carries the version of the lead it opened on, and the server
// refuses it (409) once the lead has been saved since. The copy a page holds goes
// stale under that page's own changes as much as under a colleague's: an assignment,
// a referrer, a logged call (the server moves "last contacted" and the score with it)
// each give the lead a new version the page never sees. Opened on that copy, the next
// edit was refused as changed by someone else. So the dialog takes the lead as it is
// now, as soon as it is read, while nothing has been typed: a fresher copy never
// throws away what someone entered, and a save from an older one is still refused and
// offers a reload. The deal dialog does the same (Pipeline.tsx).

import { useEffect, useRef, type Dispatch, type SetStateAction } from 'react';
import { getLeadById } from '@/app/api';
import type { Lead } from '@/app/api/types';
import type { LeadForm } from './types';

type Option = { id: string; name: string };

/** The edit dialog's fields for a lead, as the dialog opens on it. */
export function leadEditForm(lead: Lead, sourceOptions: Option[], statusOptions: Option[]): LeadForm {
  const sourceOpt = sourceOptions.find((s) => s.id === lead.leadSourceId || s.name === lead.source);
  const statusOpt = statusOptions.find((s) => s.id === lead.leadStatusId || s.name === lead.status);
  return {
    name: lead.name,
    email: lead.email,
    phone: lead.phone ?? '',
    referredByContactId: lead.referredByContactId ?? '',
    companyId: lead.companyId ?? '',
    source: lead.source ?? (sourceOpt?.name ?? 'Manual'),
    status: lead.status,
    leadSourceId: lead.leadSourceId ?? (sourceOpt?.id ?? ''),
    leadStatusId: lead.leadStatusId ?? (statusOpt?.id ?? ''),
    leadScore: lead.leadScore?.toString() ?? '',
    description: lead.description ?? '',
    lifecycleStage: lead.lifecycleStage ?? '',
    // The copy these fields came from travels with them: the save is checked against its
    // version, and sends the referrer only when it differs from that copy's.
    version: lead.version ?? '',
    referredByContactIdAsRead: lead.referredByContactId ?? '',
  };
}

/**
 * Reads the lead again when its edit dialog opens, and puts the fresh copy in the form
 * if the form is still as the dialog opened it when the copy arrives.
 *
 * That is decided inside the form's own update, against the form as it is when the
 * update applies. Checked in an effect against the form a render saw, and applied after,
 * a keystroke queued in between was overwritten by the fresh copy. Now a keystroke that
 * lands first keeps the form on the copy it was typed on, and one that lands after is
 * made on the fresh copy; either way the form carries the version of the copy its
 * fields came from, so the save is checked against that one.
 *
 * @param leadId The lead the open dialog edits; undefined while it is closed.
 * @param opened The form as the dialog opens on the page's copy of the lead.
 * @param setForm The dialog's form setter.
 * @param formFor The form for a copy of the lead.
 * @param onFresh Hears of the fresh copy, taken into the form or not: a page showing the
 *   lead can show it as it is now.
 */
export function useFreshLeadOnOpen(
  leadId: string | undefined,
  opened: LeadForm | null,
  setForm: Dispatch<SetStateAction<LeadForm>>,
  formFor: (lead: Lead) => LeadForm,
  onFresh?: (fresh: Lead) => void,
): void {
  const latest = useRef({ opened, setForm, formFor, onFresh });
  useEffect(() => {
    latest.current = { opened, setForm, formFor, onFresh };
  });
  useEffect(() => {
    if (!leadId) return;
    // The form as the dialog opened, read as it opens (this effect runs after the one
    // above, in the same commit): what "nothing typed yet" is measured against.
    const asOpened = latest.current.opened ? JSON.stringify(latest.current.opened) : null;
    let live = true;
    getLeadById(leadId)
      .then((fresh) => {
        if (!live || !fresh || fresh.id !== leadId) return;
        const { setForm: set, formFor: fill, onFresh: heard } = latest.current;
        if (asOpened !== null) {
          const taken = fill(fresh);
          set((current) => (JSON.stringify(current) === asOpened ? taken : current));
        }
        heard?.(fresh);
      })
      .catch(() => { /* the page's copy stands; a stale save is refused and offers a reload */ });
    return () => { live = false; };
  }, [leadId]);
}
