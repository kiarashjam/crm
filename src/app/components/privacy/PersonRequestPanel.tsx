// A person's request: look them up by an email address, or start from one of their records
// (the page passes ?lead=, ?contact= or ?member= on), confirm which records and addresses are
// theirs, then give them everything held as one download, or erase them. The server finds by
// the address and by the links between records (a lead's contact, a member's lead), never by
// a name alone, so everything found starts ticked; staff untick what is someone else's.

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Download, Loader2, Search, UserX } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { LoadFailed } from '@/app/components/LoadFailed';
import {
  exportPerson, selectionOf, type ErasureResult, type PersonLookup, type PersonRecord, type PersonRecordKind, type PersonSelection,
} from '@/app/api/privacy';
import { usePersonLookup } from '@/app/hooks/queries';
import { ErasureDialog } from './ErasureDialog';

const KIND: Record<PersonRecordKind, string> = { lead: 'Lead', contact: 'Contact', member: 'Member', application: 'Application' };
const ROUTE: Partial<Record<PersonRecordKind, string>> = { lead: '/leads', contact: '/contacts', member: '/members' };

const recordKey = (r: PersonRecord) => `${r.kind}:${r.id}`;
const addressKey = (a: string) => `@${a}`;

export function PersonRequestPanel({
  start, onErased,
}: {
  /** Where the page was opened from: one of the person's records, or an address. */
  start: PersonLookup | null;
  /** After an erasure: the records it started from are gone. */
  onErased?: (result: ErasureResult) => void;
}) {
  const [email, setEmail] = useState(start?.email ?? '');
  const [lookup, setLookup] = useState<PersonLookup | null>(start);
  const found = usePersonLookup(lookup);
  const [unticked, setUnticked] = useState<ReadonlySet<string>>(new Set());
  const [exporting, setExporting] = useState(false);
  const [erasing, setErasing] = useState<PersonSelection | null>(null);
  const [erased, setErased] = useState<ErasureResult | null>(null);

  // Another record opened from elsewhere starts its own lookup. Its start going away (the
  // page forgets the record an erasure removed) leaves the panel, and what it says, as it is.
  const startKey = JSON.stringify(start ?? null);
  const [seenStart, setSeenStart] = useState(startKey);
  if (startKey !== seenStart) {
    setSeenStart(startKey);
    if (start) {
      setLookup(start);
      setEmail(start.email ?? '');
      setErased(null);
    }
  }

  // Each answer starts with everything it found ticked.
  useEffect(() => setUnticked(new Set()), [found.data]);

  const data = found.data;
  const chosenRecords = useMemo(() => (data?.records ?? []).filter((r) => !unticked.has(recordKey(r))), [data, unticked]);
  const chosenAddresses = useMemo(() => (data?.addresses ?? []).filter((a) => !unticked.has(addressKey(a))), [data, unticked]);
  const nothingChosen = chosenRecords.length === 0 && chosenAddresses.length === 0;

  const toggle = (key: string) => setUnticked((current) => {
    const next = new Set(current);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    return next;
  });

  const find = (event: FormEvent) => {
    event.preventDefault();
    const address = email.trim();
    if (!address) return;
    setErased(null);
    // The same address asked again is read again: what is found is what gets acted on.
    if (lookup?.email === address && !lookup.leadId && !lookup.contactId && !lookup.memberId) void found.refetch();
    else setLookup({ email: address });
  };

  const download = async () => {
    setExporting(true);
    try {
      await exportPerson(selectionOf(chosenRecords, chosenAddresses));
      toast.success('Downloaded. The download is recorded in the audit log.');
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'Could not put the download together');
    } finally {
      setExporting(false);
    }
  };

  const done = (result: ErasureResult) => {
    // The records found are gone: nothing is left to show or act on.
    setLookup(null);
    setEmail('');
    setErased(result);
    onErased?.(result);
  };

  return (
    <div className="max-w-4xl space-y-5">
      <form onSubmit={find} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <Label htmlFor="privacy-email" className="mb-1 block font-semibold text-slate-900">Find a person</Label>
        <p className="mb-3 text-sm text-slate-600">
          By the email address they wrote from, or open their lead, contact or member page and choose <em>Privacy</em> there.
        </p>
        <div className="flex flex-col gap-2 sm:flex-row">
          <Input
            id="privacy-email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="name@example.com"
            autoComplete="off"
            className="sm:max-w-md"
          />
          <Button type="submit" disabled={!email.trim() || found.isFetching}>
            {found.isFetching ? <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" /> : <Search className="h-4 w-4" aria-hidden="true" />}
            Find
          </Button>
        </div>
      </form>

      {erased && (
        <p role="status" className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">
          Erased as &ldquo;{erased.pseudonym}&rdquo; (erasure {erased.number}). The log of erasures has the details, and names nobody.
        </p>
      )}

      {lookup && (found.isPending ? (
        <p className="flex items-center gap-2 text-sm text-slate-600" role="status">
          <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" /> Looking through the workspace…
        </p>
      ) : found.isError ? (
        <LoadFailed what="The lookup" error={found.error} onRetry={() => void found.refetch()} retrying={found.isFetching} />
      ) : data && data.records.length === 0 ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-600 shadow-sm">
          <p>No lead, contact, member or application has {lookup.email ? <strong>{lookup.email}</strong> : 'this address'}.</p>
          <p className="mt-1">
            Mail, consent and suppression may still hold the address: a download or an erasure for the address alone covers those.
          </p>
          {data.addresses.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              <Button type="button" variant="outline" onClick={() => void download()} disabled={exporting}>
                <Download className="h-4 w-4" aria-hidden="true" /> Download what the address has
              </Button>
              <Button type="button" variant="destructive" onClick={() => setErasing(selectionOf([], data.addresses))}>
                <UserX className="h-4 w-4" aria-hidden="true" /> Erase the address…
              </Button>
            </div>
          )}
        </div>
      ) : data ? (
        <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" aria-labelledby="privacy-found">
          <h3 id="privacy-found" className="font-semibold text-slate-900">
            {data.records.length} {data.records.length === 1 ? 'record' : 'records'} found
          </h3>
          <p className="mb-3 text-sm text-slate-600">Untick anything that belongs to someone else. What stays ticked is what is downloaded or erased.</p>
          <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
            {data.records.map((r) => {
              const route = ROUTE[r.kind];
              return (
                <li key={recordKey(r)} className="flex items-start gap-3 px-3 py-2">
                  <input
                    id={`pick-${recordKey(r)}`}
                    type="checkbox"
                    className="mt-1 h-4 w-4 rounded border-slate-300"
                    checked={!unticked.has(recordKey(r))}
                    onChange={() => toggle(recordKey(r))}
                  />
                  <label htmlFor={`pick-${recordKey(r)}`} className="min-w-0 flex-1 text-sm">
                    <span className="mr-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs font-semibold text-slate-700">{KIND[r.kind] ?? r.kind}</span>
                    <span className="font-medium text-slate-900">{r.name || '(no name)'}</span>
                    {r.email && <span className="text-slate-500"> · {r.email}</span>}
                    {r.detail && <span className="text-slate-500"> · {r.detail}</span>}
                    <span className="block text-xs text-slate-500">{r.foundBy}</span>
                  </label>
                  {route && (
                    <Link to={`${route}/${r.id}`} className="shrink-0 text-xs text-indigo-700 hover:underline">
                      Open<span className="sr-only"> {r.name}</span>
                    </Link>
                  )}
                </li>
              );
            })}
          </ul>
          {data.addresses.length > 0 && (
            <fieldset className="mt-4">
              <legend className="text-sm font-medium text-slate-900">Email addresses</legend>
              <p className="text-xs text-slate-500">Mail sent and received, consent and suppression are found by these.</p>
              <div className="mt-2 flex flex-wrap gap-x-5 gap-y-2">
                {data.addresses.map((a) => (
                  <label key={a} className="flex items-center gap-2 text-sm text-slate-700">
                    <input type="checkbox" className="h-4 w-4 rounded border-slate-300" checked={!unticked.has(addressKey(a))} onChange={() => toggle(addressKey(a))} />
                    {a}
                  </label>
                ))}
              </div>
            </fieldset>
          )}
          <div className="mt-5 flex flex-wrap gap-2">
            <Button type="button" onClick={() => void download()} disabled={nothingChosen || exporting}>
              {exporting ? <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" /> : <Download className="h-4 w-4" aria-hidden="true" />}
              Download everything held
            </Button>
            <Button type="button" variant="destructive" onClick={() => setErasing(selectionOf(chosenRecords, chosenAddresses))} disabled={nothingChosen}>
              <UserX className="h-4 w-4" aria-hidden="true" /> Erase this person…
            </Button>
          </div>
          <p className="mt-2 text-xs text-slate-500">
            The download is one zip: every record as JSON and a summary to read. Both actions are written to the audit log.
          </p>
        </section>
      ) : null)}

      <ErasureDialog selection={erasing} onClose={() => setErasing(null)} onErased={done} />
    </div>
  );
}
