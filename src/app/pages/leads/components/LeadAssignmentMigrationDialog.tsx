import { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { Loader2, ArrowRightLeft, Users } from 'lucide-react';
import { Dialog, DialogContent, DialogTitle, DialogDescription } from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { getLeads, assignLead } from '@/app/api';
import { getOrgMembers } from '@/app/api/organizations';
import { isUsingRealApi } from '@/app/api/apiClient';
import { useOrg } from '@/app/contexts/OrgContext';
import { loadLeadAssignments } from '../leadAssignmentStore';

// One-time migration of device-local lead owners to the shared backend.
//
// Before assignment was persisted server-side, a lead's owner lived only in
// this browser's localStorage (crm.leadAssignments.v1), so teammates couldn't
// see it. Now that PUT /api/leads/:id/assign exists, this dialog offers to push
// those local-only assignments to the database on confirmation — but only for
// leads the server doesn't already have an owner for (never overwriting an
// assignment made by someone else). It is self-clearing: once migrated, the
// server value matches local, so nothing is left "pending".
const MIGRATION_DONE_KEY = 'crm.leadAssignments.migrationDone.v1';
const MIGRATION_SNOOZE_KEY = 'crm.leadAssignments.migrationSnoozed.v1';

type PendingItem = { leadId: string; leadName: string; userId: string; userName: string };

export function LeadAssignmentMigrationDialog({ onMigrated }: { onMigrated?: () => void }) {
  const { currentOrgId } = useOrg();
  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState<PendingItem[]>([]);
  const [busy, setBusy] = useState(false);
  const probedOrgRef = useRef<string | null>(null);

  useEffect(() => {
    // Only relevant with a real backend and an active org. Demo mode already
    // uses the local store as its source of truth, so there is nothing to move.
    if (!isUsingRealApi() || !currentOrgId) return;
    if (probedOrgRef.current === currentOrgId) return;
    probedOrgRef.current = currentOrgId;

    try {
      if (localStorage.getItem(MIGRATION_DONE_KEY)) return;
      if (sessionStorage.getItem(MIGRATION_SNOOZE_KEY)) return;
    } catch { /* storage unavailable — just proceed */ }

    const local = loadLeadAssignments();
    if (Object.keys(local).length === 0) return;

    let cancelled = false;
    (async () => {
      try {
        const [leads, members] = await Promise.all([getLeads(), getOrgMembers(currentOrgId)]);
        if (cancelled) return;
        // Need the member list to validate targets and show names; without it we
        // can't present a trustworthy prompt, so skip this session.
        if (!members || members.length === 0) return;
        const membersById = new Map(members.map((m) => [m.userId, m]));
        const leadsById = new Map(leads.map((l) => [l.id, l]));

        const items: PendingItem[] = [];
        for (const [leadId, userId] of Object.entries(local)) {
          const lead = leadsById.get(leadId);
          if (!lead) continue;              // lead no longer exists in this org
          if (lead.assignedToId) continue;  // server already has an owner — don't clobber it
          const member = membersById.get(userId);
          if (!member) continue;            // assignee is no longer an org member
          items.push({ leadId, leadName: lead.name, userId, userName: member.name });
        }

        if (cancelled || items.length === 0) return;
        setPending(items);
        setOpen(true);
      } catch {
        /* best-effort: a probe failure just means we ask again next time */
      }
    })();
    return () => { cancelled = true; };
  }, [currentOrgId]);

  const snooze = () => {
    try { sessionStorage.setItem(MIGRATION_SNOOZE_KEY, '1'); } catch { /* ignore */ }
    setOpen(false);
  };

  const runMigration = async () => {
    setBusy(true);
    const results = await Promise.allSettled(
      pending.map((p) => assignLead(p.leadId, p.userId)),
    );
    const ok = results.filter((r) => r.status === 'fulfilled' && r.value).length;
    const failed = pending.length - ok;
    setBusy(false);
    setOpen(false);

    if (failed === 0) {
      // Fully migrated — never probe again on this device.
      try { localStorage.setItem(MIGRATION_DONE_KEY, '1'); } catch { /* ignore */ }
      toast.success(`Transferred ${ok} assignment${ok === 1 ? '' : 's'} to your team`);
    } else {
      // Partial failure — snooze for the session and retry next time.
      try { sessionStorage.setItem(MIGRATION_SNOOZE_KEY, '1'); } catch { /* ignore */ }
      if (ok > 0) toast.warning(`Transferred ${ok}, but ${failed} couldn't be saved — I'll ask again later`);
      else toast.error(`Couldn't transfer assignments — please try again later`);
    }
    onMigrated?.();
  };

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o && !busy) snooze(); }}>
      <DialogContent className="max-w-md">
        <DialogTitle className="flex items-center gap-2">
          <ArrowRightLeft className="h-5 w-5 text-indigo-600" /> Sync lead owners to your team
        </DialogTitle>
        <DialogDescription className="mt-1">
          You have <strong>{pending.length}</strong> lead {pending.length === 1 ? 'owner' : 'owners'} saved only on this
          device (from before owners were shared). Transfer {pending.length === 1 ? 'it' : 'them'} to the database so
          everyone on your team sees the same assignments? This won't change any lead that already has an owner.
        </DialogDescription>
        <ul className="mt-1 max-h-52 divide-y divide-slate-100 overflow-y-auto rounded-lg border border-slate-200 text-sm">
          {pending.slice(0, 50).map((p) => (
            <li key={p.leadId} className="flex items-center justify-between gap-3 px-3 py-2">
              <span className="truncate text-slate-700">{p.leadName}</span>
              <span className="shrink-0 font-medium text-slate-500">→ {p.userName}</span>
            </li>
          ))}
          {pending.length > 50 && (
            <li className="px-3 py-2 text-center text-xs text-slate-400">+ {pending.length - 50} more</li>
          )}
        </ul>
        <div className="mt-2 flex items-center justify-end gap-2">
          <Button variant="outline" onClick={snooze} disabled={busy}>Not now</Button>
          <Button onClick={runMigration} disabled={busy} className="gap-1.5">
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Users className="h-4 w-4" />}
            Transfer {pending.length}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
