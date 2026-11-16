// A company or a contact as the club's client, on their page: their house account on the ledger
// (what they owe, the charges on it, a charge or a payment posted, the statement as the server's
// PDF), a company's corporate memberships with the seats its nominees fill, and the private
// events they hired. With the club turned off there is nothing to show.

import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import { CalendarHeart, FileDown, Loader2, Plus, Receipt, Wallet } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Badge } from '@/app/components/ui/badge';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/app/components/ui/dialog';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { CHARGE_KINDS, PAYMENT_METHODS, PAYMENT_METHOD_LABELS, createCharge, recordPayment, type Charge, type ChargeKind, type Payment } from '@/app/api/charges';
import {
  createCorporateMembership, downloadClientStatementPdf, NOMINEE_CHARGES_LABELS, openClientAccount, PRIVATE_EVENT_STATUS_LABELS,
  type ClientAccount, type ClientKind, type NomineeCharges,
} from '@/app/api/corporate';
import type { CatalogCadence } from '@/app/api/catalog';
import { CADENCE_LABEL } from '@/app/api/catalog';
import { useAccountCharges, useClientAccount, useCorporateMemberships, usePrivateEvents } from '@/app/hooks/queries';
import { useModules } from '@/app/hooks/useModules';
import { useMay } from '@/app/hooks/useMay';
import { useFormat } from '@/app/i18n';
import { CardShell, errorText, Failed, fieldClass, Loading, SectionTitle, useClubAmount } from './parts';
import { CorporateMembershipPanel } from './CorporateMembershipPanel';

const PERIODS = [
  { key: 'this-month', label: 'This month' },
  { key: 'last-month', label: 'Last month' },
  { key: 'twelve-months', label: 'The last 12 months' },
] as const;

type PeriodKey = (typeof PERIODS)[number]['key'];

/** The period a statement covers, as [from, to) in UTC from the reader's calendar. */
function periodOf(key: PeriodKey, now = new Date()): { fromUtc: string; toUtc: string } {
  const startOfMonth = (y: number, m: number) => new Date(y, m, 1);
  const y = now.getFullYear();
  const m = now.getMonth();
  if (key === 'last-month') return { fromUtc: startOfMonth(y, m - 1).toISOString(), toUtc: startOfMonth(y, m).toISOString() };
  if (key === 'twelve-months') return { fromUtc: startOfMonth(y, m - 11).toISOString(), toUtc: startOfMonth(y, m + 1).toISOString() };
  return { fromUtc: startOfMonth(y, m).toISOString(), toUtc: startOfMonth(y, m + 1).toISOString() };
}

const owedOn = (c: Charge) => Math.max(0, c.amount - c.paidAmount);

export function ClientClubCard(props: { kind: ClientKind; id: string; name: string; readOnly?: boolean }) {
  const clubOn = useModules().club;
  return clubOn ? <ClientClubBody {...props} /> : null;
}

function ClientClubBody({ kind, id, name, readOnly = false }: { kind: ClientKind; id: string; name: string; readOnly?: boolean }) {
  const f = useFormat();
  const may = useMay();
  const account = useClientAccount(kind, id);
  const isOpen = account.data?.open === true;
  const memberships = useCorporateMemberships({ companyId: id }, kind === 'company');
  const events = usePrivateEvents(kind === 'company' ? { companyId: id } : { contactId: id });
  const [opening, setOpening] = useState(false);
  const [starting, setStarting] = useState(false);

  const open = async () => {
    setOpening(true);
    try {
      await openClientAccount(kind, id);
      toast.success(`${name} has a house account at the club now.`);
      await account.refetch();
    } catch (err) {
      toast.error(errorText(err, 'The account was not opened.'));
    } finally {
      setOpening(false);
    }
  };

  const upcoming = useMemo(() => [...(events.data ?? [])]
    .sort((a, b) => a.startAtUtc.localeCompare(b.startAtUtc))
    .slice(0, 6), [events.data]);

  return (
    <CardShell id={`club-client-${kind}-${id}`} title="At the club">
      <div>
        <SectionTitle>House account</SectionTitle>
        {account.isPending ? (
          <Loading what="the account" />
        ) : account.isError ? (
          <Failed what="The house account" error={account.error} onRetry={() => void account.refetch()} retrying={account.isFetching} />
        ) : !isOpen ? (
          <div className="space-y-2">
            <p className="text-slate-600 dark:text-slate-300">
              {kind === 'company'
                ? `${name} has no house account yet. Once opened, its own charges go on it, and its nominees’ when it pays them.`
                : `${name} has no house account yet. A private event’s deposit and invoice open one when they are asked.`}
            </p>
            {!readOnly && (
              <WriteOnly permission={PERMISSION.ManageMoney}>
                <Button type="button" size="sm" variant="outline" onClick={() => void open()} disabled={opening} className="gap-1.5">
                  {opening ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Wallet className="h-4 w-4" aria-hidden="true" />}
                  Open a house account
                </Button>
              </WriteOnly>
            )}
          </div>
        ) : (
          <AccountSummary kind={kind} id={id} name={name} account={account.data!} readOnly={readOnly} />
        )}
      </div>

      {kind === 'company' && (
        <div>
          <SectionTitle>Corporate membership</SectionTitle>
          {memberships.isPending ? (
            <Loading what="the corporate membership" />
          ) : memberships.isError ? (
            <Failed what="The corporate membership" error={memberships.error} onRetry={() => void memberships.refetch()} retrying={memberships.isFetching} />
          ) : (memberships.data ?? []).length === 0 ? (
            <div className="space-y-2">
              <p className="text-slate-600 dark:text-slate-300">{name} holds no corporate membership.</p>
              {!readOnly && (
                <WriteOnly permission={PERMISSION.ClubMembers}>
                  <Button type="button" size="sm" variant="outline" className="gap-1.5 border-dashed" onClick={() => setStarting(true)}>
                    <Plus className="h-4 w-4" aria-hidden="true" /> Start a corporate membership
                  </Button>
                </WriteOnly>
              )}
            </div>
          ) : (
            <div className="space-y-3">
              {memberships.data!.map((m) => (
                <CorporateMembershipPanel key={m.id} membership={m} readOnly={readOnly} onChanged={() => void memberships.refetch()} />
              ))}
            </div>
          )}
        </div>
      )}

      <div>
        <SectionTitle>Private events</SectionTitle>
        {events.isPending ? (
          <Loading what="the private events" />
        ) : events.isError ? (
          <Failed what="The private events" error={events.error} onRetry={() => void events.refetch()} retrying={events.isFetching} />
        ) : upcoming.length === 0 ? (
          <p className="text-slate-600 dark:text-slate-300">None yet.</p>
        ) : (
          <ul className="space-y-1">
            {upcoming.map((e) => (
              <li key={e.id} className="flex flex-wrap items-center justify-between gap-2">
                <Link to={`/private-events/${e.id}`} className="inline-flex items-center gap-1.5 font-medium text-slate-900 hover:text-amber-800 dark:text-slate-100">
                  <CalendarHeart className="h-3.5 w-3.5 shrink-0 text-amber-600" aria-hidden="true" /> {e.title}
                </Link>
                <span className="text-xs text-slate-500">{f.dateTime(e.startAtUtc, 'short')} · {PRIVATE_EVENT_STATUS_LABELS[e.status] ?? e.status}</span>
              </li>
            ))}
          </ul>
        )}
        {!readOnly && may(PERMISSION.ClubMembers) && (
          <Link to={`/private-events?new=1&${kind}Id=${id}`} className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-amber-800 hover:underline dark:text-amber-300">
            <Plus className="h-3.5 w-3.5" aria-hidden="true" /> Plan a private event
          </Link>
        )}
      </div>

      {kind === 'company' && (
        <StartCorporateDialog open={starting} onOpenChange={setStarting} companyId={id} companyName={name}
          mayHandleMoney={may(PERMISSION.ManageMoney)} onStarted={() => { void memberships.refetch(); void account.refetch(); }} />
      )}
    </CardShell>
  );
}

function AccountSummary({ kind, id, name, account, readOnly }: { kind: ClientKind; id: string; name: string; account: ClientAccount; readOnly: boolean }) {
  const f = useFormat();
  const amount = useClubAmount();
  const charges = useAccountCharges(id);
  const [period, setPeriod] = useState<PeriodKey>('this-month');
  const [downloading, setDownloading] = useState(false);
  const [dialog, setDialog] = useState<'charge' | 'payment' | null>(null);
  const recent = (charges.data ?? []).slice().sort((a, b) => b.postedAtUtc.localeCompare(a.postedAtUtc)).slice(0, 5);

  const statement = async () => {
    setDownloading(true);
    try {
      const { fromUtc, toUtc } = periodOf(period);
      await downloadClientStatementPdf(kind, id, fromUtc, toUtc);
    } catch (err) {
      toast.error(errorText(err, 'The statement could not be made.'));
    } finally {
      setDownloading(false);
    }
  };

  return (
    <div className="space-y-3">
      <dl className="grid grid-cols-3 gap-2 text-center">
        <div className="rounded-lg bg-white p-2 ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-700">
          <dt className="text-[11px] text-slate-500">Owed</dt>
          <dd className="font-semibold text-slate-900 dark:text-slate-100">{amount(account.owed, account.currency)}</dd>
        </div>
        <div className="rounded-lg bg-white p-2 ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-700">
          <dt className="text-[11px] text-slate-500">Credit</dt>
          <dd className="font-semibold text-slate-900 dark:text-slate-100">{amount(account.credit, account.currency)}</dd>
        </div>
        <div className="rounded-lg bg-white p-2 ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-700">
          <dt className="text-[11px] text-slate-500">Open charges</dt>
          <dd className="font-semibold text-slate-900 dark:text-slate-100">{account.openCharges}</dd>
        </div>
      </dl>

      {charges.isError ? (
        <Failed what="The charges" error={charges.error} onRetry={() => void charges.refetch()} retrying={charges.isFetching} />
      ) : recent.length > 0 && (
        <ul className="space-y-1 text-xs">
          {recent.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-2">
              <span className="min-w-0 truncate text-slate-700 dark:text-slate-300">
                {f.date(c.postedAtUtc, 'short')} · {c.description}
                {c.incurredByName ? <span className="text-slate-500"> ({c.incurredByName})</span> : null}
              </span>
              <span className="shrink-0 font-medium">
                {amount(c.amount, account.currency)}
                {c.status !== 'Paid' && c.status !== 'Voided' && owedOn(c) > 0 ? <Badge variant="outline" className="ml-1.5">{c.status}</Badge> : null}
              </span>
            </li>
          ))}
        </ul>
      )}

      <div className="flex flex-wrap items-end gap-2">
        <div>
          <Label htmlFor={`statement-period-${id}`} className="text-xs text-slate-600">Statement for</Label>
          <select id={`statement-period-${id}`} className={fieldClass} value={period} onChange={(e) => setPeriod(e.target.value as PeriodKey)}>
            {PERIODS.map((p) => <option key={p.key} value={p.key}>{p.label}</option>)}
          </select>
        </div>
        <Button type="button" size="sm" variant="outline" onClick={() => void statement()} disabled={downloading} className="gap-1.5">
          {downloading ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <FileDown className="h-4 w-4" aria-hidden="true" />} Statement (PDF)
        </Button>
        {!readOnly && (
          <WriteOnly permission={PERMISSION.ManageMoney}>
            <Button type="button" size="sm" variant="outline" className="gap-1.5" onClick={() => setDialog('charge')}>
              <Receipt className="h-4 w-4" aria-hidden="true" /> Post a charge
            </Button>
            <Button type="button" size="sm" variant="outline" className="gap-1.5" onClick={() => setDialog('payment')} disabled={account.openCharges === 0}>
              <Wallet className="h-4 w-4" aria-hidden="true" /> Record a payment
            </Button>
          </WriteOnly>
        )}
      </div>

      <ChargeDialog open={dialog === 'charge'} onClose={() => setDialog(null)} accountId={id} name={name} onDone={() => void charges.refetch()} />
      <PaymentDialog open={dialog === 'payment'} onClose={() => setDialog(null)} accountId={id} name={name}
        openCharges={(charges.data ?? []).filter((c) => c.status !== 'Voided' && owedOn(c) > 0)} currency={account.currency} onDone={() => void charges.refetch()} />
    </div>
  );
}

function ChargeDialog({ open, onClose, accountId, name, onDone }: { open: boolean; onClose: () => void; accountId: string; name: string; onDone: () => void }) {
  const [kind, setKind] = useState<ChargeKind>('Event');
  const [description, setDescription] = useState('');
  const [amountText, setAmountText] = useState('');
  const [saving, setSaving] = useState(false);
  const value = Number(amountText.replace(',', '.'));
  const valid = description.trim().length > 0 && Number.isFinite(value) && value > 0;

  const save = async () => {
    setSaving(true);
    try {
      await createCharge({ memberId: accountId, memberName: name, kind, description: description.trim(), amount: value });
      toast.success(`The charge is on ${name}’s account.`);
      setDescription('');
      setAmountText('');
      onDone();
      onClose();
    } catch (err) {
      toast.error(errorText(err, 'The charge was not posted.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader><DialogTitle>Post a charge to {name}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <Label htmlFor={`charge-kind-${accountId}`}>Kind</Label>
            <select id={`charge-kind-${accountId}`} className={fieldClass} value={kind} onChange={(e) => setKind(e.target.value as ChargeKind)}>
              {CHARGE_KINDS.filter((k) => k !== 'Dues').map((k) => <option key={k} value={k}>{k}</option>)}
            </select>
          </div>
          <div>
            <Label htmlFor={`charge-description-${accountId}`}>Description</Label>
            <Input id={`charge-description-${accountId}`} value={description} onChange={(e) => setDescription(e.target.value)} maxLength={500} />
          </div>
          <div>
            <Label htmlFor={`charge-amount-${accountId}`}>Amount</Label>
            <Input id={`charge-amount-${accountId}`} inputMode="decimal" value={amountText} onChange={(e) => setAmountText(e.target.value)} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button onClick={() => void save()} disabled={!valid || saving}>{saving ? 'Posting…' : 'Post the charge'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function PaymentDialog({ open, onClose, accountId, name, openCharges, currency, onDone }: {
  open: boolean;
  onClose: () => void;
  accountId: string;
  name: string;
  openCharges: Charge[];
  currency: string;
  onDone: () => void;
}) {
  const amount = useClubAmount();
  const [chargeId, setChargeId] = useState('');
  const [amountText, setAmountText] = useState('');
  const [method, setMethod] = useState<Payment['method']>('Wire');
  const [saving, setSaving] = useState(false);
  const charge = openCharges.find((c) => c.id === chargeId);
  const value = Number(amountText.replace(',', '.'));
  const valid = !!charge && Number.isFinite(value) && value > 0;

  const choose = (id: string) => {
    setChargeId(id);
    const c = openCharges.find((x) => x.id === id);
    setAmountText(c ? String(owedOn(c)) : '');
  };

  const save = async () => {
    setSaving(true);
    try {
      await recordPayment({ memberId: accountId, memberName: name, amount: value, method, chargeId });
      toast.success(`The payment is on ${name}’s account.`);
      setChargeId('');
      setAmountText('');
      onDone();
      onClose();
    } catch (err) {
      toast.error(errorText(err, 'The payment was not recorded.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader><DialogTitle>Record a payment from {name}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <Label htmlFor={`payment-charge-${accountId}`}>For</Label>
            <select id={`payment-charge-${accountId}`} className={fieldClass} value={chargeId} onChange={(e) => choose(e.target.value)}>
              <option value="">Choose a charge…</option>
              {openCharges.map((c) => <option key={c.id} value={c.id}>{c.description} · {amount(owedOn(c), currency)} owed</option>)}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label htmlFor={`payment-amount-${accountId}`}>Amount</Label>
              <Input id={`payment-amount-${accountId}`} inputMode="decimal" value={amountText} onChange={(e) => setAmountText(e.target.value)} />
            </div>
            <div>
              <Label htmlFor={`payment-method-${accountId}`}>Method</Label>
              <select id={`payment-method-${accountId}`} className={fieldClass} value={method} onChange={(e) => setMethod(e.target.value as Payment['method'])}>
                {PAYMENT_METHODS.filter((m) => m !== 'CardOnFile' && m !== 'AppliedCredit').map((m) => <option key={m} value={m}>{PAYMENT_METHOD_LABELS[m]}</option>)}
              </select>
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button onClick={() => void save()} disabled={!valid || saving}>{saving ? 'Recording…' : 'Record the payment'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function StartCorporateDialog({ open, onOpenChange, companyId, companyName, mayHandleMoney, onStarted }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  companyId: string;
  companyName: string;
  mayHandleMoney: boolean;
  onStarted: () => void;
}) {
  const today = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  const [form, setForm] = useState({
    name: `${companyName} corporate`,
    seats: '4',
    rule: 'company' as NomineeCharges,
    starts: `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`,
    billingEmail: '',
    dues: '',
    cadence: 'Annual' as CatalogCadence,
  });
  const [saving, setSaving] = useState(false);
  const seats = Number(form.seats);
  const dues = Number(form.dues.replace(',', '.'));
  const valid = form.name.trim().length > 0 && Number.isInteger(seats) && seats > 0 && (!form.dues || dues > 0);

  const save = async () => {
    setSaving(true);
    try {
      const [y, mo, d] = form.starts.split('-').map(Number);
      const saved = await createCorporateMembership({
        companyId,
        name: form.name.trim(),
        seats,
        nomineeCharges: form.rule,
        startsAtUtc: new Date(y ?? today.getFullYear(), (mo ?? 1) - 1, d ?? 1).toISOString(),
        billingEmail: form.billingEmail.trim() || undefined,
        dues: mayHandleMoney && form.dues ? { amount: dues, cadence: form.cadence } : undefined,
      });
      toast.success(`${saved.name} has started, with ${saved.seats} seats.`);
      onOpenChange(false);
      onStarted();
    } catch (err) {
      toast.error(errorText(err, 'The corporate membership was not started.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader><DialogTitle>A corporate membership for {companyName}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <Label htmlFor={`corp-name-${companyId}`}>Name</Label>
            <Input id={`corp-name-${companyId}`} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} maxLength={200} />
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label htmlFor={`corp-seats-${companyId}`}>Seats</Label>
              <Input id={`corp-seats-${companyId}`} inputMode="numeric" value={form.seats} onChange={(e) => setForm({ ...form, seats: e.target.value })} />
            </div>
            <div>
              <Label htmlFor={`corp-starts-${companyId}`}>Starts</Label>
              <Input id={`corp-starts-${companyId}`} type="date" value={form.starts} onChange={(e) => setForm({ ...form, starts: e.target.value })} />
            </div>
          </div>
          <div>
            <Label htmlFor={`corp-rule-${companyId}`}>Nominees’ charges</Label>
            <select id={`corp-rule-${companyId}`} className={fieldClass} value={form.rule} onChange={(e) => setForm({ ...form, rule: e.target.value as NomineeCharges })}>
              {(Object.keys(NOMINEE_CHARGES_LABELS) as NomineeCharges[]).map((r) => <option key={r} value={r}>{NOMINEE_CHARGES_LABELS[r]}</option>)}
            </select>
          </div>
          <div>
            {/* Printed on the company's statements; the app does not email them (they are downloaded as a PDF). */}
            <Label htmlFor={`corp-email-${companyId}`}>Billing email (printed on its statements)</Label>
            <Input id={`corp-email-${companyId}`} type="email" value={form.billingEmail} onChange={(e) => setForm({ ...form, billingEmail: e.target.value })} />
          </div>
          {mayHandleMoney && (
            <div className="grid grid-cols-2 gap-2">
              <div>
                <Label htmlFor={`corp-dues-${companyId}`}>Dues (optional)</Label>
                <Input id={`corp-dues-${companyId}`} inputMode="decimal" value={form.dues} onChange={(e) => setForm({ ...form, dues: e.target.value })} />
              </div>
              <div>
                <Label htmlFor={`corp-cadence-${companyId}`}>Every</Label>
                <select id={`corp-cadence-${companyId}`} className={fieldClass} value={form.cadence} onChange={(e) => setForm({ ...form, cadence: e.target.value as CatalogCadence })}>
                  {(['Monthly', 'Quarterly', 'SemiAnnual', 'Annual'] as CatalogCadence[]).map((c) => <option key={c} value={c}>{CADENCE_LABEL[c]}</option>)}
                </select>
              </div>
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={() => void save()} disabled={!valid || saving}>{saving ? 'Starting…' : 'Start it'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
