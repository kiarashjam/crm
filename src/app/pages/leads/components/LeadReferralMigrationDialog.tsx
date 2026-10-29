import { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { ArrowRightLeft, Loader2, UserPlus } from 'lucide-react';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { getContacts, getLeads, setLeadReferral } from '@/app/api';
import { servesDemoData } from '@/app/api/apiClient';
import { useOrg } from '@/app/contexts/OrgContext';
import { forgetLeadReferrals, loadLeadReferrals } from '../leadReferralStore';

// One-time move of browser-only referrals to the server.
//
// Before the server kept a lead's referrer, it lived only in this browser
// (crm.leadReferrals.v1), so nobody else on the team saw it. This offers to save
// those for everyone, but only for leads the server has no referrer for: it never
// replaces one a teammate already set. Each entry saved, or no longer valid, is
// forgotten here, so the question is not asked twice.
const SNOOZE_KEY = 'crm.leadReferrals.migrationSnoozed.v1';

type PendingItem = { leadId: string; leadName: string; contactId: string; contactName: string };

export function LeadReferralMigrationDialog({ onMigrated }: { onMigrated?: () => void }) {
  const { currentOrgId } = useOrg();
  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState<PendingItem[]>([]);
  const [busy, setBusy] = useState(false);
  const probedOrgRef = useRef<string | null>(null);

  useEffect(() => {
    // Demo mode saves referrals in its own store already, so there is nothing to move.
    if (servesDemoData() || !currentOrgId) return;
    if (probedOrgRef.current === currentOrgId) return;
    probedOrgRef.current = currentOrgId;
    try {
      if (sessionStorage.getItem(SNOOZE_KEY)) return;
    } catch { /* storage unavailable: ask anyway */ }

    const local = loadLeadReferrals();
    if (Object.keys(local).length === 0) return;

    let cancelled = false;
    (async () => {
      try {
        const [leads, contacts] = await Promise.all([getLeads(), getContacts()]);
        if (cancelled) return;
        const leadsById = new Map(leads.map((l) => [l.id, l]));
        const contactsById = new Map(contacts.map((c) => [c.id, c]));
        const items: PendingItem[] = [];
        const settled: string[] = [];
        for (const [leadId, contactId] of Object.entries(local)) {
          const lead = leadsById.get(leadId);
          // A lead of another workspace: leave it for when that workspace is open.
          if (!lead) continue;
          const contact = contactsById.get(contactId);
          // The server already has a referrer, or the contact is gone: nothing to save.
          if (lead.referredByContactId || !contact || contact.convertedFromLeadId === lead.id) {
            settled.push(leadId);
            continue;
          }
          items.push({ leadId, leadName: lead.name, contactId, contactName: contact.name });
        }
        if (settled.length > 0) forgetLeadReferrals(settled);
        if (cancelled || items.length === 0) return;
        setPending(items);
        setOpen(true);
      } catch {
        /* best effort: a failed probe asks again next time */
      }
    })();
    return () => { cancelled = true; };
  }, [currentOrgId]);

  const snooze = () => {
    try { sessionStorage.setItem(SNOOZE_KEY, '1'); } catch { /* ignore */ }
    setOpen(false);
  };

  const runMigration = async () => {
    setBusy(true);
    const results = await Promise.allSettled(pending.map((p) => setLeadReferral(p.leadId, p.contactId)));
    const saved = pending.filter((_, index) => {
      const result = results[index];
      return result?.status === 'fulfilled' && !!result.value;
    });
    forgetLeadReferrals(saved.map((p) => p.leadId));
    const failed = pending.length - saved.length;
    setBusy(false);
    setOpen(false);
    if (failed === 0) {
      toast.success(`Saved ${saved.length} referral${saved.length === 1 ? '' : 's'} for your team`);
    } else {
      try { sessionStorage.setItem(SNOOZE_KEY, '1'); } catch { /* ignore */ }
      if (saved.length > 0) toast.warning(`Saved ${saved.length}, but ${failed} could not be saved. I'll ask again later.`);
      else toast.error('The referrals could not be saved. Please try again later.');
    }
    onMigrated?.();
  };

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o && !busy) snooze(); }}>
      <DialogContent className="max-w-md">
        <DialogTitle className="flex items-center gap-2">
          <ArrowRightLeft className="h-5 w-5 text-violet-600" /> Share referrals with your team
        </DialogTitle>
        <DialogDescription className="mt-1">
          <strong>{pending.length}</strong> lead {pending.length === 1 ? 'referral is' : 'referrals are'} saved only in this
          browser, from before referrals were shared. Save {pending.length === 1 ? 'it' : 'them'} so everyone sees who
          referred each lead? A lead that already has a referrer is not changed.
        </DialogDescription>
        <ul className="mt-1 max-h-52 divide-y divide-slate-100 overflow-y-auto rounded-lg border border-slate-200 text-sm">
          {pending.slice(0, 50).map((p) => (
            <li key={p.leadId} className="flex items-center justify-between gap-3 px-3 py-2">
              <span className="truncate text-slate-700">{p.leadName}</span>
              <span className="shrink-0 font-medium text-slate-500">referred by {p.contactName}</span>
            </li>
          ))}
          {pending.length > 50 && (
            <li className="px-3 py-2 text-center text-xs text-slate-500">+ {pending.length - 50} more</li>
          )}
        </ul>
        <div className="mt-2 flex items-center justify-end gap-2">
          <Button variant="outline" onClick={snooze} disabled={busy}>Not now</Button>
          <Button onClick={runMigration} disabled={busy} className="gap-1.5">
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <UserPlus className="h-4 w-4" />}
            Save {pending.length}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
