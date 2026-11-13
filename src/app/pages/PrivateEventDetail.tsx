// One private event: its client and deal, its time and the spaces held for it, its menu and
// services, its money (the deposit and the final invoice on the client's house account), its
// contract, and its guest list checked in at the door. The server keeps every rule (a clash with a
// booking, money only by money.manage, nothing changed once invoiced); this page offers what the
// event's status and the person's permissions allow and says the server's refusals as they are.

import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  CalendarCheck, CalendarHeart, CheckCircle2, FileSignature, Loader2, Pencil, Plus, Receipt, Trash2, UserCheck, UserPlus, Undo2, Wallet, XCircle,
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import { Badge } from '@/app/components/ui/badge';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/app/components/ui/dialog';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import {
  addPrivateEventGuest, addPrivateEventLine, cancelPrivateEvent, checkInPrivateEventGuest, completePrivateEvent, confirmPrivateEvent,
  DEPOSIT_STATE_LABELS, draftPrivateEventContract, invoicePrivateEvent, PRIVATE_EVENT_STATUS_LABELS, removePrivateEventGuest,
  removePrivateEventLine, requestPrivateEventDeposit, updatePrivateEvent, updatePrivateEventLine, UNLINK, type PrivateEvent,
} from '@/app/api/corporate';
import { usePrivateEvent, useClubCatalog, useClubMembers, useClubResources, useAllDeals } from '@/app/hooks/queries';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import { useModules } from '@/app/hooks/useModules';
import { useMay } from '@/app/hooks/useMay';
import { useFormat } from '@/app/i18n';
import { isEditConflict, showEditConflict } from '@/app/lib/editConflict';
import { fromLocalInputValue, toLocalInputValue } from '@/app/lib/dateInputs';
import { CardShell, errorText, fieldClass, SectionTitle, useClubAmount } from '@/app/components/corporate/parts';

/** The catalogue kinds a private event's lines are taken from (as the server allows). */
const LINE_KINDS = new Set(['MenuItem', 'Product', 'Fee', 'Ticket']);

export default function PrivateEventDetail() {
  const { id = '' } = useParams<{ id: string }>();
  const query = usePrivateEvent(id);
  const e = query.data;
  useDocumentTitle(e?.title, 'Private events');

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          {query.isPending ? (
            <p className="flex items-center gap-2 text-slate-500" role="status"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
          ) : query.isError && !e ? (
            <LoadFailed what="This private event" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} />
          ) : !e ? (
            <p className="text-slate-600">This private event was not found.</p>
          ) : (
            <EventView event={e} onSaved={() => void query.refetch()} />
          )}
        </main>
      </PageTransition>
    </div>
  );
}

function EventView({ event: e, onSaved }: { event: PrivateEvent; onSaved: () => void }) {
  const f = useFormat();
  const may = useMay();
  const amount = useClubAmount();
  const salesOn = useModules().sales;
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [asking, setAsking] = useState(false);

  const open = e.status === 'Enquiry' || e.status === 'Confirmed';
  const invoiced = e.money.invoiced;
  const canArrange = may(PERMISSION.ClubMembers);
  const canMoney = may(PERMISSION.ManageMoney);

  /** Runs a change, then shows the event as the server now has it. */
  const act = async (what: string, action: () => Promise<PrivateEvent>, done: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(done);
      onSaved();
      return true;
    } catch (err) {
      if (isEditConflict(err)) showEditConflict(err.message, onSaved);
      else toast.error(errorText(err, `${what} did not go through.`));
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHero
        icon={CalendarHeart}
        tone="amber"
        back={{ to: '/private-events', label: 'Private events' }}
        title={e.title}
        subtitle={`${f.dateTime(e.startAtUtc)} to ${f.time(e.endAtUtc)} · ${e.guestCount} guests expected`}
        badges={<Badge variant="outline">{PRIVATE_EVENT_STATUS_LABELS[e.status] ?? e.status}</Badge>}
        actions={open && canArrange ? (
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="outline" className="gap-1.5" onClick={() => setEditing(true)} disabled={busy}>
              <Pencil className="h-4 w-4" aria-hidden="true" /> Edit
            </Button>
            {e.status === 'Enquiry' && (
              <Button type="button" variant="outline" className="gap-1.5" disabled={busy}
                onClick={() => void act('Confirming', () => confirmPrivateEvent(e.id), `${e.title} is confirmed; its spaces are held for certain.`)}>
                <CalendarCheck className="h-4 w-4" aria-hidden="true" /> Confirm
              </Button>
            )}
            <Button type="button" variant="outline" className="gap-1.5" disabled={busy}
              onClick={() => void act('Completing', () => completePrivateEvent(e.id), `${e.title} is completed.`)}>
              <CheckCircle2 className="h-4 w-4" aria-hidden="true" /> Completed
            </Button>
            {!invoiced && (
              <Button type="button" variant="outline" className="gap-1.5 text-rose-700" onClick={() => setCancelling(true)} disabled={busy}>
                <XCircle className="h-4 w-4" aria-hidden="true" /> Cancel the event
              </Button>
            )}
          </div>
        ) : undefined}
      />

      {e.status === 'Cancelled' && (
        <p className="mb-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-2 text-sm text-rose-800">
          Cancelled{e.cancelledAtUtc ? ` on ${f.date(e.cancelledAtUtc)}` : ''}{e.cancelReason ? `: ${e.cancelReason}` : ''}.
        </p>
      )}

      <div className="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="space-y-5">
          <CardShell id={`pe-details-${e.id}`} title="The event" icon={<CalendarHeart className="h-3.5 w-3.5" aria-hidden="true" />}>
            <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <dt className="text-xs text-slate-500">Client</dt>
                <dd className="font-medium">
                  {e.companyId && <Link to={`/companies/${e.companyId}`} className="hover:text-amber-800">{e.companyName ?? 'Company'}</Link>}
                  {e.companyId && e.contactId && ' · '}
                  {e.contactId && <Link to={`/contacts/${e.contactId}`} className="hover:text-amber-800">{e.contactName ?? 'Contact'}</Link>}
                  {!e.companyId && !e.contactId && '—'}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Deal</dt>
                <dd className="font-medium">{e.dealId ? <Link to={`/deals/${e.dealId}`} className="hover:text-amber-800">{e.dealName ?? 'The deal'}</Link> : 'None linked'}</dd>
              </div>
              <div className="sm:col-span-2">
                <dt className="text-xs text-slate-500">Spaces held</dt>
                <dd className="font-medium">
                  {e.spaces.length === 0 ? 'None' : e.spaces.map((s) => `${s.resourceName} (${s.status})`).join(', ')}
                </dd>
              </div>
              {e.notes && (
                <div className="sm:col-span-2">
                  <dt className="text-xs text-slate-500">Notes</dt>
                  <dd className="whitespace-pre-wrap">{e.notes}</dd>
                </div>
              )}
            </dl>
          </CardShell>

          <LinesCard event={e} editable={open && !invoiced && canArrange} onSaved={onSaved} />

          <GuestsCard event={e} onSaved={onSaved} />
        </div>

        <div className="space-y-5">
          <CardShell id={`pe-money-${e.id}`} title="Money" icon={<Wallet className="h-3.5 w-3.5" aria-hidden="true" />}>
            <dl className="space-y-1.5">
              <div className="flex justify-between gap-2"><dt className="text-slate-500">Billed to</dt><dd className="font-medium">{e.money.billToName ?? '—'}</dd></div>
              <div className="flex justify-between gap-2"><dt className="text-slate-500">Menu and services</dt><dd className="font-medium">{amount(e.money.linesTotal, e.currency)}</dd></div>
              <div className="flex justify-between gap-2">
                <dt className="text-slate-500">Deposit</dt>
                <dd className="text-right font-medium">
                  {e.money.depositAmount != null ? amount(e.money.depositAmount, e.currency) : '—'}
                  <span className="block text-xs font-normal text-slate-500">{DEPOSIT_STATE_LABELS[e.money.depositState] ?? e.money.depositState}</span>
                </dd>
              </div>
              {invoiced && (
                <div className="flex justify-between gap-2">
                  <dt className="text-slate-500">Final invoice</dt>
                  <dd className="text-right font-medium">
                    {amount(e.money.invoiceTotal, e.currency)}
                    <span className="block text-xs font-normal text-slate-500">{amount(e.money.invoicePaid, e.currency)} paid</span>
                  </dd>
                </div>
              )}
            </dl>
            {open && !invoiced && canMoney && (
              <div className="flex flex-wrap gap-2">
                {e.money.depositState === 'none' && (
                  <Button type="button" size="sm" variant="outline" className="gap-1.5" onClick={() => setAsking(true)} disabled={busy}>
                    <Wallet className="h-4 w-4" aria-hidden="true" /> Ask a deposit
                  </Button>
                )}
                <Button type="button" size="sm" variant="outline" className="gap-1.5" disabled={busy || e.money.linesTotal <= 0}
                  onClick={() => void act('The invoice', () => invoicePrivateEvent(e.id), `The final invoice for ${e.title} is on ${e.money.billToName ?? 'the client'}’s account.`)}>
                  <Receipt className="h-4 w-4" aria-hidden="true" /> Post the final invoice
                </Button>
              </div>
            )}
            {open && !invoiced && !canMoney && (
              <p className="text-xs text-slate-500">Asking the deposit and posting the invoice are done by whoever handles the club’s money.</p>
            )}
          </CardShell>

          {salesOn && (
            <CardShell id={`pe-contract-${e.id}`} title="Contract" icon={<FileSignature className="h-3.5 w-3.5" aria-hidden="true" />}>
              {e.contractId ? (
                <p>
                  A contract is {e.contractStatus ? e.contractStatus.replace(/_/g, ' ') : 'drafted'}.{' '}
                  {e.dealId && <Link to={`/deals/${e.dealId}`} className="font-medium text-amber-800 hover:underline">Open it on the deal</Link>}
                </p>
              ) : !e.dealId ? (
                <p className="text-slate-600">Link the event to its deal to draft its contract there.</p>
              ) : (
                <p className="text-slate-600">Drafted from the private event template, with its spaces, menu, total and deposit; countersigning it confirms the event.</p>
              )}
              {open && e.dealId && (!e.contractId || e.contractStatus === 'voided' || e.contractStatus === 'declined') && (
                <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.EditRecords]} all>
                  <Button type="button" size="sm" variant="outline" className="gap-1.5" disabled={busy}
                    onClick={() => void act('The contract', () => draftPrivateEventContract(e.id), 'The contract is drafted on the deal.')}>
                    <FileSignature className="h-4 w-4" aria-hidden="true" /> Draft the contract
                  </Button>
                </WriteOnly>
              )}
            </CardShell>
          )}
        </div>
      </div>

      <EditDialog open={editing} onOpenChange={setEditing} event={e} onSaved={onSaved} />
      <CancelDialog open={cancelling} onOpenChange={setCancelling} event={e} mayHandleMoney={canMoney}
        onCancel={(reason, deposit) => act('Cancelling', () => cancelPrivateEvent(e.id, reason, deposit), `${e.title} is cancelled; its spaces are free.`)} />
      <DepositDialog open={asking} onOpenChange={setAsking} event={e}
        onAsk={(value) => act('The deposit', () => requestPrivateEventDeposit(e.id, value), `The deposit is on ${e.money.billToName ?? 'the client'}’s account.`)} />
    </>
  );
}

function LinesCard({ event: e, editable, onSaved }: { event: PrivateEvent; editable: boolean; onSaved: () => void }) {
  const amount = useClubAmount();
  const catalog = useClubCatalog();
  const items = useMemo(() => (catalog.data ?? []).filter((i) => i.isActive && LINE_KINDS.has(i.kind) && i.currency === e.currency), [catalog.data, e.currency]);
  const [itemId, setItemId] = useState('');
  const [name, setName] = useState('');
  const [price, setPrice] = useState('');
  const [vat, setVat] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [busy, setBusy] = useState(false);
  const qty = Number(quantity.replace(',', '.'));
  const unit = Number(price.replace(',', '.'));
  const valid = qty > 0 && (itemId ? true : name.trim().length > 0 && Number.isFinite(unit) && unit >= 0 && price !== '');

  const run = async (what: string, action: () => Promise<PrivateEvent>) => {
    setBusy(true);
    try {
      await action();
      onSaved();
      return true;
    } catch (err) {
      toast.error(errorText(err, `${what} did not go through.`));
      return false;
    } finally {
      setBusy(false);
    }
  };

  const add = async () => {
    const vatRate = vat === '' ? undefined : Number(vat.replace(',', '.'));
    const ok = await run('The line', () => addPrivateEventLine(e.id, itemId
      ? { catalogItemId: itemId, quantity: qty }
      : { name: name.trim(), unitPrice: unit, vatRate, quantity: qty }));
    if (ok) {
      setItemId('');
      setName('');
      setPrice('');
      setVat('');
      setQuantity('1');
    }
  };

  return (
    <CardShell id={`pe-lines-${e.id}`} title="Menu and services" icon={<Receipt className="h-3.5 w-3.5" aria-hidden="true" />}>
      {e.lines.length === 0 ? (
        <p className="text-slate-600">Nothing on the menu yet.</p>
      ) : (
        <table className="w-full text-sm">
          <thead className="text-left text-xs text-slate-500">
            <tr>
              <th scope="col" className="py-1">Item</th>
              <th scope="col" className="py-1">Quantity</th>
              <th scope="col" className="py-1 text-right">Each</th>
              <th scope="col" className="py-1 text-right">Total</th>
              {editable && <th scope="col" className="py-1"><span className="sr-only">Remove</span></th>}
            </tr>
          </thead>
          <tbody>
            {e.lines.map((l) => (
              <tr key={l.id} className="border-t border-slate-100 dark:border-slate-800">
                <td className="py-1.5">{l.name}{l.vatRate != null && <span className="ml-1 text-xs text-slate-500">({l.vatRate}% VAT)</span>}</td>
                <td className="py-1.5">
                  {editable ? (
                    <Input aria-label={`Quantity of ${l.name}`} className="h-8 w-20" defaultValue={String(l.quantity)} inputMode="decimal"
                      onBlur={(ev) => {
                        const next = Number(ev.target.value.replace(',', '.'));
                        if (next > 0 && next !== l.quantity) void run('The quantity', () => updatePrivateEventLine(e.id, l.id, { quantity: next }));
                      }} />
                  ) : l.quantity}
                </td>
                <td className="py-1.5 text-right">{amount(l.unitPrice, e.currency)}</td>
                <td className="py-1.5 text-right font-medium">{amount(l.total, e.currency)}</td>
                {editable && (
                  <td className="py-1.5 text-right">
                    <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-rose-700" aria-label={`Remove ${l.name}`} disabled={busy}
                      onClick={() => void run('Removing the line', () => removePrivateEventLine(e.id, l.id))}>
                      <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                    </Button>
                  </td>
                )}
              </tr>
            ))}
            <tr className="border-t border-slate-200 font-semibold dark:border-slate-700">
              <td className="py-1.5" colSpan={3}>Total</td>
              <td className="py-1.5 text-right">{amount(e.money.linesTotal, e.currency)}</td>
              {editable && <td />}
            </tr>
          </tbody>
        </table>
      )}

      {editable && (
        <div className="space-y-2 border-t border-slate-100 pt-3 dark:border-slate-800">
          <SectionTitle>Add to the menu</SectionTitle>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
            <div>
              <Label htmlFor={`pe-item-${e.id}`} className="text-xs text-slate-600">From the catalogue</Label>
              <select id={`pe-item-${e.id}`} className={fieldClass} value={itemId} onChange={(ev) => setItemId(ev.target.value)}>
                <option value="">Something else (typed in)</option>
                {items.map((i) => <option key={i.id} value={i.id}>{i.name} · {amount(i.price, i.currency)}</option>)}
              </select>
            </div>
            <div>
              <Label htmlFor={`pe-qty-${e.id}`} className="text-xs text-slate-600">Quantity</Label>
              <Input id={`pe-qty-${e.id}`} inputMode="decimal" value={quantity} onChange={(ev) => setQuantity(ev.target.value)} />
            </div>
          </div>
          {!itemId && (
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
              <div>
                <Label htmlFor={`pe-line-name-${e.id}`} className="text-xs text-slate-600">Name</Label>
                <Input id={`pe-line-name-${e.id}`} value={name} onChange={(ev) => setName(ev.target.value)} maxLength={200} />
              </div>
              <div>
                <Label htmlFor={`pe-line-price-${e.id}`} className="text-xs text-slate-600">Price each</Label>
                <Input id={`pe-line-price-${e.id}`} inputMode="decimal" value={price} onChange={(ev) => setPrice(ev.target.value)} />
              </div>
              <div>
                <Label htmlFor={`pe-line-vat-${e.id}`} className="text-xs text-slate-600">VAT % (included)</Label>
                <Input id={`pe-line-vat-${e.id}`} inputMode="decimal" value={vat} onChange={(ev) => setVat(ev.target.value)} />
              </div>
            </div>
          )}
          <Button type="button" size="sm" className="gap-1.5" onClick={() => void add()} disabled={!valid || busy}>
            <Plus className="h-4 w-4" aria-hidden="true" /> Add
          </Button>
        </div>
      )}
    </CardShell>
  );
}

function GuestsCard({ event: e, onSaved }: { event: PrivateEvent; onSaved: () => void }) {
  const f = useFormat();
  const members = useClubMembers();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [memberId, setMemberId] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const atTheDoor = e.status !== 'Cancelled';

  const run = async (key: string, what: string, action: () => Promise<PrivateEvent>) => {
    setBusy(key);
    try {
      await action();
      onSaved();
      return true;
    } catch (err) {
      toast.error(errorText(err, `${what} did not go through.`));
      return false;
    } finally {
      setBusy(null);
    }
  };

  const add = async () => {
    const ok = await run('add', 'Adding the guest', () => addPrivateEventGuest(e.id, memberId
      ? { memberId, name: name.trim() || undefined }
      : { name: name.trim(), email: email.trim() || undefined }));
    if (ok) {
      setName('');
      setEmail('');
      setMemberId('');
    }
  };

  return (
    <CardShell id={`pe-guests-${e.id}`} title={`Guests (${e.guestsCheckedIn} of ${e.guests.length} checked in)`} icon={<UserCheck className="h-3.5 w-3.5" aria-hidden="true" />}>
      {e.guests.length === 0 ? (
        <p className="text-slate-600">Nobody on the list yet.</p>
      ) : (
        <ul className="divide-y divide-slate-100 dark:divide-slate-800">
          {e.guests.map((g) => (
            <li key={g.id} className="flex flex-wrap items-center justify-between gap-2 py-1.5">
              <span>
                <span className="font-medium">{g.name}</span>
                {g.memberId && <Link to={`/members/${g.memberId}`} className="ml-1.5 text-xs text-amber-800 hover:underline">member</Link>}
                {g.email && <span className="ml-1.5 text-xs text-slate-500">{g.email}</span>}
                {g.checkedInAtUtc && <span className="ml-1.5 text-xs text-emerald-700">in at {f.time(g.checkedInAtUtc)}{g.checkedInByName ? ` (${g.checkedInByName})` : ''}</span>}
              </span>
              <span className="flex gap-1">
                {atTheDoor && (
                  <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
                    {g.checkedInAtUtc ? (
                      <Button type="button" size="sm" variant="ghost" className="h-7 gap-1 px-2" disabled={busy === g.id} aria-label={`Undo ${g.name}’s check-in`}
                        onClick={() => void run(g.id, 'Undoing the check-in', () => checkInPrivateEventGuest(e.id, g.id, true))}>
                        <Undo2 className="h-3.5 w-3.5" aria-hidden="true" /> Undo
                      </Button>
                    ) : (
                      <Button type="button" size="sm" variant="outline" className="h-7 gap-1 px-2" disabled={busy === g.id} aria-label={`Check ${g.name} in`}
                        onClick={() => void run(g.id, 'The check-in', () => checkInPrivateEventGuest(e.id, g.id))}>
                        <UserCheck className="h-3.5 w-3.5" aria-hidden="true" /> Check in
                      </Button>
                    )}
                  </WriteOnly>
                )}
                <WriteOnly permission={PERMISSION.ClubMembers}>
                  <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-rose-700" disabled={busy === g.id} aria-label={`Take ${g.name} off the list`}
                    onClick={() => void run(g.id, 'Taking the guest off', () => removePrivateEventGuest(e.id, g.id))}>
                    <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                  </Button>
                </WriteOnly>
              </span>
            </li>
          ))}
        </ul>
      )}

      {atTheDoor && (
        <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
          <div className="space-y-2 border-t border-slate-100 pt-3 dark:border-slate-800">
            <SectionTitle>Add a guest</SectionTitle>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
              <div>
                <Label htmlFor={`pe-guest-member-${e.id}`} className="text-xs text-slate-600">A member</Label>
                <select id={`pe-guest-member-${e.id}`} className={fieldClass} value={memberId} onChange={(ev) => setMemberId(ev.target.value)}>
                  <option value="">Not a member</option>
                  {(members.data ?? []).filter((m) => m.status !== 'Cancelled').map((m) => <option key={m.id} value={m.id}>{m.firstName} {m.lastName}</option>)}
                </select>
              </div>
              <div>
                <Label htmlFor={`pe-guest-name-${e.id}`} className="text-xs text-slate-600">Name</Label>
                <Input id={`pe-guest-name-${e.id}`} value={name} onChange={(ev) => setName(ev.target.value)} maxLength={200} placeholder={memberId ? 'As the member' : ''} />
              </div>
              {!memberId && (
                <div>
                  <Label htmlFor={`pe-guest-email-${e.id}`} className="text-xs text-slate-600">Email (optional)</Label>
                  <Input id={`pe-guest-email-${e.id}`} type="email" value={email} onChange={(ev) => setEmail(ev.target.value)} />
                </div>
              )}
            </div>
            <Button type="button" size="sm" className="gap-1.5" onClick={() => void add()} disabled={busy === 'add' || (!memberId && !name.trim())}>
              <UserPlus className="h-4 w-4" aria-hidden="true" /> Add to the list
            </Button>
          </div>
        </WriteOnly>
      )}
    </CardShell>
  );
}

function EditDialog({ open, onOpenChange, event: e, onSaved }: { open: boolean; onOpenChange: (open: boolean) => void; event: PrivateEvent; onSaved: () => void }) {
  const salesOn = useModules().sales;
  const resources = useClubResources();
  const deals = useAllDeals(open && salesOn);
  const initial = useMemo(() => ({
    title: e.title, start: toLocalInputValue(e.startAtUtc), end: toLocalInputValue(e.endAtUtc), guests: String(e.guestCount),
    notes: e.notes ?? '', dealId: e.dealId ?? '', spaces: e.spaces.map((s) => s.resourceId),
  }), [e]);
  const [form, setForm] = useState(initial);
  const [saving, setSaving] = useState(false);
  const startUtc = fromLocalInputValue(form.start);
  const endUtc = fromLocalInputValue(form.end);
  const valid = form.title.trim().length > 0 && !!startUtc && !!endUtc && endUtc > startUtc;

  const save = async () => {
    setSaving(true);
    try {
      await updatePrivateEvent(e.id, {
        title: form.title.trim(),
        startAtUtc: startUtc,
        endAtUtc: endUtc,
        guestCount: form.guests ? Number(form.guests) : 0,
        notes: form.notes,
        dealId: form.dealId || (e.dealId ? UNLINK : undefined),
        resourceIds: form.spaces,
      }, e.version);
      toast.success(`${form.title.trim()} is saved.`);
      onOpenChange(false);
      onSaved();
    } catch (err) {
      if (isEditConflict(err)) showEditConflict(err.message, onSaved);
      else toast.error(errorText(err, 'The event was not saved.'));
    } finally {
      setSaving(false);
    }
  };

  const toggle = (id: string) => setForm((c) => ({ ...c, spaces: c.spaces.includes(id) ? c.spaces.filter((s) => s !== id) : [...c.spaces, id] }));

  return (
    <Dialog open={open} onOpenChange={(o) => { if (o) setForm(initial); onOpenChange(o); }}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader><DialogTitle>Edit {e.title}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <Label htmlFor="pe-edit-title">Title</Label>
            <Input id="pe-edit-title" value={form.title} onChange={(ev) => setForm({ ...form, title: ev.target.value })} maxLength={200} />
          </div>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <div>
              <Label htmlFor="pe-edit-start">Starts</Label>
              <Input id="pe-edit-start" type="datetime-local" value={form.start} onChange={(ev) => setForm({ ...form, start: ev.target.value })} />
            </div>
            <div>
              <Label htmlFor="pe-edit-end">Ends</Label>
              <Input id="pe-edit-end" type="datetime-local" value={form.end} onChange={(ev) => setForm({ ...form, end: ev.target.value })} />
            </div>
            <div>
              <Label htmlFor="pe-edit-guests">Guests</Label>
              <Input id="pe-edit-guests" inputMode="numeric" value={form.guests} onChange={(ev) => setForm({ ...form, guests: ev.target.value.replace(/\D/g, '') })} />
            </div>
          </div>
          {salesOn && (
            <div>
              <Label htmlFor="pe-edit-deal">Deal</Label>
              <select id="pe-edit-deal" className={fieldClass} value={form.dealId} onChange={(ev) => setForm({ ...form, dealId: ev.target.value })}>
                <option value="">None</option>
                {(deals.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                {e.dealId && !(deals.data ?? []).some((d) => d.id === e.dealId) && <option value={e.dealId}>{e.dealName ?? 'The linked deal'}</option>}
              </select>
            </div>
          )}
          <fieldset>
            <legend className="text-sm font-medium">Spaces to hold</legend>
            <div className="mt-1 flex flex-wrap gap-3">
              {(resources.data ?? []).map((r) => (
                <label key={r.id} className="flex items-center gap-1.5 text-sm">
                  <input type="checkbox" checked={form.spaces.includes(r.id)} onChange={() => toggle(r.id)} className="h-4 w-4" />
                  {r.name}
                </label>
              ))}
            </div>
          </fieldset>
          <div>
            <Label htmlFor="pe-edit-notes">Notes</Label>
            <Textarea id="pe-edit-notes" rows={2} value={form.notes} onChange={(ev) => setForm({ ...form, notes: ev.target.value })} maxLength={2000} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={() => void save()} disabled={!valid || saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function CancelDialog({ open, onOpenChange, event: e, mayHandleMoney, onCancel }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  event: PrivateEvent;
  mayHandleMoney: boolean;
  onCancel: (reason: string, deposit?: 'keep' | 'release') => Promise<boolean>;
}) {
  const [reason, setReason] = useState('');
  const [deposit, setDeposit] = useState<'keep' | 'release' | ''>('');
  const depositOpen = e.money.depositState === 'charged' || e.money.depositState === 'paid';
  const valid = reason.trim().length > 0 && (!depositOpen || deposit !== '');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader><DialogTitle>Cancel {e.title}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <Label htmlFor="pe-cancel-reason">Why</Label>
            <Input id="pe-cancel-reason" value={reason} onChange={(ev) => setReason(ev.target.value)} maxLength={500} />
          </div>
          {depositOpen && (
            <fieldset className="space-y-1.5">
              <legend className="text-sm font-medium">The deposit</legend>
              <label className="flex items-center gap-2 text-sm">
                <input type="radio" name="pe-deposit" checked={deposit === 'keep'} onChange={() => setDeposit('keep')} /> Keep it
              </label>
              <label className={`flex items-center gap-2 text-sm ${mayHandleMoney ? '' : 'opacity-50'}`}>
                <input type="radio" name="pe-deposit" checked={deposit === 'release'} onChange={() => setDeposit('release')} disabled={!mayHandleMoney} />
                Give it back as house credit{mayHandleMoney ? '' : ' (whoever handles the money)'}
              </label>
            </fieldset>
          )}
          <p className="text-xs text-slate-500">Its spaces are freed for their time.</p>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Back</Button>
          <Button variant="destructive" disabled={!valid}
            onClick={() => void onCancel(reason.trim(), depositOpen ? (deposit || undefined) : undefined).then((ok) => { if (ok) onOpenChange(false); })}>
            Cancel the event
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function DepositDialog({ open, onOpenChange, event: e, onAsk }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  event: PrivateEvent;
  onAsk: (amount: number) => Promise<boolean>;
}) {
  const amount = useClubAmount();
  const [value, setValue] = useState('');
  const number = Number(value.replace(',', '.'));
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader><DialogTitle>Ask a deposit for {e.title}</DialogTitle></DialogHeader>
        <div className="space-y-2">
          <Label htmlFor="pe-deposit-amount">Amount ({e.currency})</Label>
          <Input id="pe-deposit-amount" inputMode="decimal" value={value} onChange={(ev) => setValue(ev.target.value)} />
          <p className="text-xs text-slate-500">
            A charge on {e.money.billToName ?? 'the client'}’s house account; the final invoice sets what is paid on it against the menu
            {e.money.linesTotal > 0 ? ` (${amount(e.money.linesTotal, e.currency)} so far)` : ''}.
          </p>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!(number > 0)} onClick={() => void onAsk(number).then((ok) => { if (ok) { setValue(''); onOpenChange(false); } })}>Ask the deposit</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
