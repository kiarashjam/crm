// The POS's tickets: those waiting for staff (put on the house account with no member's code
// on them, or naming a customer no code matches; or not posted, with why), or every ticket.
// Someone who takes payments says whose a ticket is (and the POS customer on it is remembered
// for next time), tries it again once the cause is fixed, or sets it aside with a reason.
// Everyone else reads them. Each action is checked against the version the ticket was read
// at, so two people dealing with one ticket get one answer and a reload.

import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronDown, ChevronRight, Loader2, RotateCcw, Search, UserCheck, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { LoadFailed } from '@/app/components/LoadFailed';
import EmptyState from '@/app/components/EmptyState';
import { ApiError } from '@/app/api/apiClient';
import type { Member } from '@/app/api/members';
import {
  ignorePosTicket, matchPosTicket, POS_MEANS_LABEL, POS_STATUS_LABEL, retryPosTicket,
  type PosTicket, type PosTicketFilter,
} from '@/app/api/pos';
import { useClubMembers, usePosTickets } from '@/app/hooks/queries';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { currentDateLocale, currentNumberLocale } from '@/app/i18n/current';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

const STATUS_BADGE: Record<string, string> = {
  posted: 'bg-emerald-50 text-emerald-700',
  unmatched: 'bg-amber-50 text-amber-800',
  failed: 'bg-rose-50 text-rose-700',
  skipped: 'bg-slate-100 text-slate-600',
  ignored: 'bg-slate-100 text-slate-600',
};

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(currentDateLocale(), { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

export function PosTicketsPanel({ filter, canAct }: { filter: PosTicketFilter; canAct: boolean }) {
  const { locale } = useRegional();
  const ticketsQuery = usePosTickets(filter);
  const [open, setOpen] = useState<string | null>(null);
  const [matching, setMatching] = useState<PosTicket | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const tickets = ticketsQuery.data ?? [];
  const money = (n: number, currency: string) => formatClubMoney(n, currency || 'CHF', locale);

  const onConflict = (err: unknown) => {
    if (err instanceof ApiError && err.status === 409) void ticketsQuery.refetch();
  };

  const retry = async (t: PosTicket) => {
    setBusy(t.id);
    try {
      const after = await retryPosTicket(t.id, t.version);
      if (after.status === 'posted') toast.success(`Posted to ${after.memberName ?? 'the member'}'s account`);
      else toast.error(after.note ?? 'The ticket still could not be posted.');
    } catch (err) {
      toast.error(message(err, 'Could not try the ticket again'));
      onConflict(err);
    } finally {
      setBusy(null);
    }
  };

  const ignore = async (t: PosTicket) => {
    const reason = window.prompt('Why is this ticket not a member\'s? The reason stays on the record.');
    if (reason === null) return;
    if (!reason.trim()) {
      toast.error('Say why the ticket is not a member\'s.');
      return;
    }
    setBusy(t.id);
    try {
      await ignorePosTicket(t.id, reason.trim(), t.version);
      toast.success('Set aside as not a member\'s');
    } catch (err) {
      toast.error(message(err, 'Could not set the ticket aside'));
      onConflict(err);
    } finally {
      setBusy(null);
    }
  };

  if (ticketsQuery.isError && !ticketsQuery.data) {
    return <LoadFailed what="The POS tickets" error={ticketsQuery.error} onRetry={() => void ticketsQuery.refetch()} retrying={ticketsQuery.isFetching} />;
  }
  if (ticketsQuery.isPending) {
    return <div className="flex items-center justify-center py-10 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" /></div>;
  }
  if (tickets.length === 0) {
    return (
      <EmptyState
        icon={UserCheck}
        title={filter === 'attention' ? 'Nothing waits for staff' : 'No tickets yet'}
        description={filter === 'attention'
          ? 'Every ticket read from the POS is on a member\'s account, not a member\'s, or set aside.'
          : 'Tickets show here once the POS is connected and its import has run.'}
      />
    );
  }

  return (
    <>
      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="border-b border-slate-200 bg-slate-50">
            <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
              <th className="px-4 py-3"><span className="sr-only">Details</span></th>
              <th className="px-4 py-3">Closed</th>
              <th className="px-4 py-3">Ticket</th>
              <th className="px-4 py-3 text-right">Total</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3"><span className="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            {tickets.map((t) => {
              const expanded = open === t.id;
              const waiting = t.status !== 'posted';
              return [
                <tr key={t.id} className="border-b border-slate-100 align-top">
                  <td className="px-2 py-3">
                    <button
                      type="button"
                      aria-expanded={expanded}
                      aria-label={`${expanded ? 'Hide' : 'Show'} ticket ${t.receiptNumber ?? t.externalId}`}
                      onClick={() => setOpen(expanded ? null : t.id)}
                      className="rounded p-1 text-slate-500 hover:bg-slate-100"
                    >
                      {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                    </button>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-slate-500">{formatDateTime(t.closedAtUtc)}</td>
                  <td className="px-4 py-3">
                    <span className="block font-medium text-slate-800">Receipt {t.receiptNumber ?? t.externalId}</span>
                    <span className="block text-xs text-slate-500">
                      {[t.tableName && `Table ${t.tableName}`, t.staffName, t.tenders].filter(Boolean).join(' · ')}
                    </span>
                    {(t.customerName || t.customerReference) && (
                      <span className="block text-xs text-slate-600">
                        Customer: {[t.customerName, t.customerReference].filter(Boolean).join(', ')}
                      </span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    <span className="block font-semibold">{money(t.total, t.currency)}</span>
                    {t.onAccountAmount > 0 && <span className="block text-xs text-amber-700">{money(t.onAccountAmount, t.currency)} on the account</span>}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[t.status] ?? ''}`}>{POS_STATUS_LABEL[t.status] ?? t.status}</span>
                    {t.memberName && t.memberId && (
                      <Link to={`/members/${t.memberId}`} className="mt-1 block text-xs text-slate-600 hover:underline">{t.memberName}</Link>
                    )}
                    {t.note && <span className="mt-1 block text-xs text-slate-500">{t.note}</span>}
                    {t.status === 'posted' && t.pointsAwarded ? <span className="mt-1 block text-xs text-amber-700">+{t.pointsAwarded.toLocaleString(currentNumberLocale())} points</span> : null}
                  </td>
                  <td className="px-4 py-3">
                    {canAct && waiting && (
                      <div className="flex justify-end gap-1">
                        <Button type="button" size="sm" variant="outline" onClick={() => setMatching(t)} aria-label={`Match receipt ${t.receiptNumber ?? t.externalId} to a member`}>
                          <UserCheck className="mr-1 h-3.5 w-3.5" /> Match
                        </Button>
                        {(t.status === 'failed' || t.status === 'unmatched') && (
                          <Button type="button" size="sm" variant="ghost" disabled={busy === t.id} onClick={() => void retry(t)}
                            aria-label={`Try receipt ${t.receiptNumber ?? t.externalId} again`}>
                            {busy === t.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RotateCcw className="h-3.5 w-3.5" />}
                          </Button>
                        )}
                        {t.status !== 'ignored' && (
                          <Button type="button" size="sm" variant="ghost" disabled={busy === t.id} onClick={() => void ignore(t)}
                            aria-label={`Set receipt ${t.receiptNumber ?? t.externalId} aside`}>
                            <XCircle className="h-3.5 w-3.5" />
                          </Button>
                        )}
                      </div>
                    )}
                  </td>
                </tr>,
                expanded && (
                  <tr key={`${t.id}-lines`} className="border-b border-slate-100 bg-slate-50/60">
                    <td />
                    <td colSpan={5} className="px-4 py-3">
                      <ul className="space-y-0.5 text-xs text-slate-700">
                        {t.lines.map((l, i) => (
                          <li key={i} className="flex justify-between gap-4">
                            <span>
                              {l.quantity !== 1 ? `${l.quantity} × ` : ''}{l.name}
                              {l.vatRate !== undefined && <span className="text-slate-500"> · VAT {l.vatRate}%</span>}
                              {l.discount > 0 && <span className="text-rose-600"> · −{money(l.discount, t.currency)}{l.discountName ? ` (${l.discountName})` : ''}</span>}
                            </span>
                            <span>{money(l.total, t.currency)}</span>
                          </li>
                        ))}
                        {t.payments.map((p, i) => (
                          <li key={`p${i}`} className="flex justify-between gap-4 text-slate-500">
                            <span>{p.method}: {POS_MEANS_LABEL[p.means] ?? p.means}{p.tip > 0 ? `, tip ${money(p.tip, t.currency)}` : ''}</span>
                            <span>{money(p.amount, t.currency)}</span>
                          </li>
                        ))}
                      </ul>
                    </td>
                  </tr>
                ),
              ];
            })}
          </tbody>
        </table>
      </div>
      {matching && (
        <MatchDialog ticket={matching} onClose={() => setMatching(null)} onConflict={() => void ticketsQuery.refetch()} />
      )}
    </>
  );
}

function MatchDialog({ ticket, onClose, onConflict }: { ticket: PosTicket; onClose: () => void; onConflict: () => void }) {
  const { locale } = useRegional();
  const membersQuery = useClubMembers();
  const [search, setSearch] = useState(ticket.customerName ?? '');
  const [picked, setPicked] = useState<Member | null>(null);
  const [remember, setRemember] = useState(true);
  const [saving, setSaving] = useState(false);
  const amount = formatClubMoney(ticket.total, ticket.currency || 'CHF', locale);

  const matches = useMemo(() => {
    const members = membersQuery.data ?? [];
    const words = search.toLowerCase().split(/\s+/).filter(Boolean);
    const found = words.length === 0
      ? members
      : members.filter((m) => {
        const text = `${m.firstName} ${m.lastName} ${m.email ?? ''}`.toLowerCase();
        return words.some((w) => text.includes(w));
      });
    return found.slice(0, 50);
  }, [membersQuery.data, search]);

  const save = async () => {
    if (!picked) return;
    setSaving(true);
    try {
      const after = await matchPosTicket(ticket.id, picked.id, ticket.hasCustomer && remember, ticket.version);
      if (after.status === 'posted') toast.success(`${amount} posted to ${picked.firstName} ${picked.lastName}'s account`);
      else toast.error(after.note ?? 'Matched, but the ticket could not be posted.');
      onClose();
    } catch (err) {
      toast.error(message(err, 'Could not match the ticket'));
      if (err instanceof ApiError && err.status === 409) {
        onConflict();
        onClose();
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Whose is receipt {ticket.receiptNumber ?? ticket.externalId}?</DialogTitle>
          <DialogDescription>
            {amount}{ticket.onAccountAmount > 0 ? `, of which ${formatClubMoney(ticket.onAccountAmount, ticket.currency || 'CHF', locale)} on the house account` : ', paid at the POS'}.
            It is posted to the member picked, with the points it earns.
          </DialogDescription>
        </DialogHeader>
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <Input aria-label="Find a member" className="pl-9" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name or email" />
        </div>
        {membersQuery.isError && !membersQuery.data ? (
          <LoadFailed what="Members" error={membersQuery.error} onRetry={() => void membersQuery.refetch()} retrying={membersQuery.isFetching} />
        ) : (
          <ul className="max-h-64 space-y-1 overflow-y-auto" role="listbox" aria-label="Members">
            {matches.map((m) => (
              <li key={m.id}>
                <button
                  type="button"
                  role="option"
                  aria-selected={picked?.id === m.id}
                  onClick={() => setPicked(m)}
                  className={`w-full rounded-lg px-3 py-2 text-left text-sm ${picked?.id === m.id ? 'bg-indigo-50 ring-1 ring-indigo-300' : 'hover:bg-slate-50'}`}
                >
                  <span className="block font-medium text-slate-800">{m.firstName} {m.lastName}</span>
                  <span className="block text-xs text-slate-500">{m.email}</span>
                </button>
              </li>
            ))}
            {matches.length === 0 && <li className="px-3 py-2 text-sm text-slate-500">No member matches.</li>}
          </ul>
        )}
        {ticket.hasCustomer && (
          <label className="flex items-start gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} className="mt-0.5" />
            <span>Remember {ticket.customerName ? `the POS customer ${ticket.customerName}` : 'the POS customer on it'} as this member, so their next tickets find them</span>
          </label>
        )}
        <DialogFooter className="gap-2 sm:gap-2">
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="button" onClick={() => void save()} disabled={!picked || saving}>
            {saving ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : null}
            {picked ? `Post to ${picked.firstName} ${picked.lastName}` : 'Pick a member'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
