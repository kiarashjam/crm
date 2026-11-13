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

import { useEffect, useState } from 'react';
import { getLeadById } from '@/app/api';
import type { Lead } from '@/app/api/types';
import type { LeadForm } from './types';

type Option = { id: string; name: string };

/** The edit dialog's fields for a lead, as the dialog opens on it. */
export function leadEditForm(lead: Lead, sourceOptions: Option[], statusOptions: Option[]): LeadForm {
  const sourceOpt = sourceOptions.find((s) => s.id === lead.leadSourceId || s.name === lead.source);
  const statusOpt = statusOptions.find((s) => s.id === lead.leadStatusId || s.name === lead.status);
  return {
    kind: lead.kind === 'establishment' ? 'establishment' : 'person',
    name: lead.name,
    email: lead.email,
    phone: lead.phone ?? '',
    jobTitle: lead.jobTitle ?? '',
    establishmentType: lead.establishmentType ?? '',
    addressLine: lead.addressLine ?? '',
    postalCode: lead.postalCode ?? '',
    city: lead.city ?? '',
    country: lead.country ?? '',
    website: lead.website ?? '',
    seats: lead.seats != null ? String(lead.seats) : '',
    contactPersonName: lead.contactPersonName ?? '',
    contactPersonRole: lead.contactPersonRole ?? '',
    contactPersonEmail: lead.contactPersonEmail ?? '',
    contactPersonPhone: lead.contactPersonPhone ?? '',
    referredByContactId: lead.referredByContactId ?? '',
    companyId: lead.companyId ?? '',
    source: lead.source ?? (sourceOpt?.name ?? 'Manual'),
    status: lead.status,
    leadSourceId: lead.leadSourceId ?? (sourceOpt?.id ?? ''),
    leadStatusId: lead.leadStatusId ?? (statusOpt?.id ?? ''),
    leadScore: lead.leadScore?.toString() ?? '',
    description: lead.description ?? '',
    lifecycleStage: lead.lifecycleStage ?? '',
  };
}

/**
 * Reads the lead again when its edit dialog opens, and hands the fresh copy to `take`
 * if the form is still `untouched` when it arrives.
 *
 * @param leadId The lead the open dialog edits; undefined while it is closed.
 */
export function useFreshLeadOnOpen(
  leadId: string | undefined,
  untouched: () => boolean,
  take: (fresh: Lead) => void,
): void {
  const [fresh, setFresh] = useState<Lead | null>(null);
  useEffect(() => {
    if (!leadId) return;
    let live = true;
    getLeadById(leadId)
      .then((lead) => { if (live && lead) setFresh(lead); })
      .catch(() => { /* the page's copy stands; a stale save is refused and offers a reload */ });
    return () => { live = false; };
  }, [leadId]);
  useEffect(() => {
    if (!fresh) return;
    setFresh(null);
    if (fresh.id === leadId && untouched()) take(fresh);
  }, [fresh, leadId, untouched, take]);
}
