// The bank's camt files (camt.054, or the statement camt.053): read here, sent to the server,
// which records each credit whose QR-bill reference names a member as their payment, once,
// whatever file it comes in again. A transfer with no reference, or one nobody's, waits on
// the unmatched list for staff to say whose it is, or that it is not a member's payment.

import { useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, FileUp, Loader2, Search, UserCheck, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { LoadFailed } from '@/app/components/LoadFailed';
import EmptyState from '@/app/components/EmptyState';
import { ApiError } from '@/app/api/apiClient';
import {
  allocateBankTransaction, ignoreBankTransaction, importBankFile,
  type BankImport, type BankTransaction, type BankTransactionStatus, type PaymentStatus,
} from '@/app/api/payments';
import type { Member } from '@/app/api/members';
import { useBankImports, useBankTransactions, useClubMembers } from '@/app/hooks/queries';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { BANK_STATUS_BADGE, BANK_STATUS_LABEL, importSummary } from './paymentLabels';

/** The server takes a file of up to 20 MB. */
const MAX_FILE_BYTES = 20 * 1024 * 1024;

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

function formatDay(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
  } catch {
    return iso;
  }
}

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

export function BankFilesPanel({ canTake, status }: { canTake: boolean; status?: PaymentStatus }) {
  const { locale } = useRegional();
  const [show, setShow] = useState<BankTransactionStatus | null>('unmatched');
  const importsQuery = useBankImports(canTake);
  const transactionsQuery = useBankTransactions(show, canTake);
  const [importing, setImporting] = useState(false);
  const [last, setLast] = useState<BankImport | null>(null);
  const [allocating, setAllocating] = useState<BankTransaction | null>(null);
  const [ignoring, setIgnoring] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  if (!canTake) {
    return (
      <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">
        Only someone who takes payments imports the bank&apos;s files and allocates their transfers.
      </p>
    );
  }

  const read = async (file: File) => {
    if (file.size > MAX_FILE_BYTES) {
      toast.error('The file is larger than 20 MB. Ask the bank for a shorter period.');
      return;
    }
    setImporting(true);
    try {
      const result = await importBankFile(file.name, await file.text());
      setLast(result);
      toast.success(`${file.name}: ${importSummary(result)}`);
    } catch (err) {
      // Not a camt file, or one the server could not read: it says which.
      toast.error(message(err, 'Could not import the file'));
    } finally {
      setImporting(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  const ignore = async (t: BankTransaction) => {
    const reason = window.prompt('Why is this transfer not a member\'s payment? The reason stays on the record.');
    if (reason === null) return;
    if (!reason.trim()) {
      toast.error('Say why it is not a member payment.');
      return;
    }
    setIgnoring(t.id);
    try {
      await ignoreBankTransaction(t.id, reason.trim(), t.version);
      toast.success('Set aside as not a member payment');
    } catch (err) {
      toast.error(message(err, 'Could not set the transfer aside'));
      if (err instanceof ApiError && err.status === 409) void transactionsQuery.refetch();
    } finally {
      setIgnoring(null);
    }
  };

  const transactions = transactionsQuery.data ?? [];
  const imports = importsQuery.data ?? [];

  return (
    <div className="space-y-6">
      {status && !status.qrBills && (
        <p className="inline-flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Statements carry no QR-bill yet, so transfers come with no reference to match: each one waits here to be
            allocated. <Link to="/hospitality-settings?tab=payments" className="font-medium underline">Set the bank details in Club settings, Payments</Link>.
          </span>
        </p>
      )}

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="mb-1 font-semibold text-slate-900">Import a bank file</h3>
        <p className="mb-3 text-sm text-slate-500">
          The camt.054 credit notification (or the camt.053 statement) from e-banking, as XML. A payment whose QR-bill
          reference names a member is recorded on their account, once, however many files it comes in.
        </p>
        <input
          ref={fileInput}
          type="file"
          accept=".xml,.camt,.053,.054,text/xml,application/xml"
          aria-label="Bank file"
          className="sr-only"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) void read(file);
          }}
        />
        <Button type="button" onClick={() => fileInput.current?.click()} disabled={importing}>
          {importing ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <FileUp className="mr-1 h-4 w-4" />} Choose a camt file
        </Button>
        {last && (
          <p className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700" role="status">
            {last.fileName} ({last.format}): {importSummary(last)}
            {last.amountMatched > 0 && <> {formatClubMoney(last.amountMatched, status?.currency || 'CHF', locale)} recorded.</>}
          </p>
        )}
      </section>

      <section>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h3 className="font-semibold text-slate-900">Transfers</h3>
          <div className="flex flex-wrap gap-2" role="group" aria-label="Show transfers">
            {([['unmatched', 'Unmatched'], [null, 'All']] as const).map(([value, label]) => (
              <button
                key={label}
                type="button"
                onClick={() => setShow(value)}
                aria-pressed={show === value}
                className={`rounded-full px-3 py-1 text-sm font-medium ring-1 ${show === value ? 'bg-slate-900 text-white ring-slate-900' : 'bg-white text-slate-700 ring-slate-200 hover:ring-slate-300'}`}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
        {transactionsQuery.isError && !transactionsQuery.data ? (
          <LoadFailed what="The transfers" error={transactionsQuery.error} onRetry={() => void transactionsQuery.refetch()} retrying={transactionsQuery.isFetching} />
        ) : transactionsQuery.isPending ? (
          <div className="flex items-center justify-center py-10 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" /></div>
        ) : transactions.length === 0 ? (
          <EmptyState
            icon={UserCheck}
            title={show === 'unmatched' ? 'Nothing to allocate' : 'No transfers yet'}
            description={show === 'unmatched' ? 'Every transfer read so far is on a member\'s account or set aside.' : 'Transfers show here once a bank file is imported.'}
          />
        ) : (
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50">
                <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3">Booked</th>
                  <th className="px-4 py-3">From</th>
                  <th className="px-4 py-3">Reference or message</th>
                  <th className="px-4 py-3 text-right">Amount</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3"><span className="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((t) => (
                  <tr key={t.id} className="border-b border-slate-100 align-top">
                    <td className="whitespace-nowrap px-4 py-3 text-slate-500">{formatDay(t.bookedAtUtc)}</td>
                    <td className="px-4 py-3">
                      <span className="block text-slate-800">{t.debtorName ?? 'Unknown'}</span>
                      {t.debtorIban && <span className="block font-mono text-xs text-slate-500">{t.debtorIban}</span>}
                    </td>
                    <td className="px-4 py-3">
                      {t.reference && <span className="block font-mono text-xs text-slate-700">{t.referenceType} {t.reference}</span>}
                      {t.message && <span className="block text-xs text-slate-500">{t.message}</span>}
                      {!t.reference && !t.message && <span className="text-xs text-slate-400">None</span>}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right font-semibold">{formatClubMoney(t.amount, t.currency, locale)}</td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${BANK_STATUS_BADGE[t.status] ?? ''}`}>{BANK_STATUS_LABEL[t.status] ?? t.status}</span>
                      {t.memberName && t.memberId && (
                        <Link to={`/members/${t.memberId}`} className="mt-1 block text-xs text-slate-600 hover:underline">{t.memberName}</Link>
                      )}
                      {t.note && <span className="mt-1 block text-xs text-slate-500">{t.note}</span>}
                    </td>
                    <td className="px-4 py-3">
                      {t.status === 'unmatched' && (
                        <div className="flex justify-end gap-1">
                          <Button type="button" size="sm" variant="outline" onClick={() => setAllocating(t)} aria-label={`Allocate ${formatClubMoney(t.amount, t.currency, locale)} from ${t.debtorName ?? 'unknown'}`}>
                            <UserCheck className="mr-1 h-3.5 w-3.5" /> Allocate
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            onClick={() => void ignore(t)}
                            disabled={ignoring === t.id}
                            aria-label={`Not a member payment: ${formatClubMoney(t.amount, t.currency, locale)} from ${t.debtorName ?? 'unknown'}`}
                          >
                            {ignoring === t.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <XCircle className="h-3.5 w-3.5" />}
                          </Button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section>
        <h3 className="mb-3 font-semibold text-slate-900">Files imported</h3>
        {importsQuery.isError && !importsQuery.data ? (
          <LoadFailed what="The imports" error={importsQuery.error} onRetry={() => void importsQuery.refetch()} retrying={importsQuery.isFetching} />
        ) : imports.length === 0 ? (
          <p className="text-sm text-slate-500">{importsQuery.isPending ? 'Loading…' : 'No bank file imported yet.'}</p>
        ) : (
          <ul className="divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white shadow-sm">
            {imports.map((i) => (
              <li key={i.id} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-sm">
                <span className="min-w-0">
                  <span className="block truncate font-medium text-slate-800">{i.fileName} <span className="font-normal text-slate-500">({i.format})</span></span>
                  <span className="block text-xs text-slate-500">
                    {formatDateTime(i.importedAtUtc)}{i.importedByName ? ` by ${i.importedByName}` : ''} · {importSummary(i)}
                  </span>
                </span>
                {i.amountMatched > 0 && <span className="font-semibold text-emerald-700">{formatClubMoney(i.amountMatched, status?.currency || 'CHF', locale)}</span>}
              </li>
            ))}
          </ul>
        )}
      </section>

      {allocating && (
        <AllocateDialog transaction={allocating} onClose={() => setAllocating(null)} onConflict={() => void transactionsQuery.refetch()} />
      )}
    </div>
  );
}

function AllocateDialog({ transaction, onClose, onConflict }: { transaction: BankTransaction; onClose: () => void; onConflict: () => void }) {
  const { locale } = useRegional();
  const membersQuery = useClubMembers();
  const [search, setSearch] = useState(transaction.debtorName ?? '');
  const [picked, setPicked] = useState<Member | null>(null);
  const [saving, setSaving] = useState(false);
  const amount = formatClubMoney(transaction.amount, transaction.currency, locale);

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
      await allocateBankTransaction(transaction.id, picked.id, transaction.version);
      toast.success(`${amount} recorded as ${picked.firstName} ${picked.lastName}'s payment`);
      onClose();
    } catch (err) {
      toast.error(message(err, 'Could not allocate the transfer'));
      if (err instanceof ApiError && err.status === 409) {
        onConflict();
        onClose();
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Whose payment is {amount}?</DialogTitle>
          <DialogDescription>
            From {transaction.debtorName ?? 'an unknown sender'}{transaction.message ? `, "${transaction.message}"` : ''}. It is recorded as
            the member&apos;s payment by bank transfer, settling their oldest charges first; anything over stays as credit.
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
        <DialogFooter className="gap-2 sm:gap-2">
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="button" onClick={() => void save()} disabled={!picked || saving}>
            {saving ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : null}
            {picked ? `Record as ${picked.firstName} ${picked.lastName}'s payment` : 'Pick a member'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
