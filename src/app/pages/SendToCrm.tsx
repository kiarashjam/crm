import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { CheckCircle, Paperclip, Search } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { getContacts, getDeals, getLeads, sendCopyToCrm } from '@/app/api';
import type { Contact, Deal, Lead } from '@/app/api/types';
import { messages } from '@/app/api/messages';

type ObjectType = 'lead' | 'contact' | 'deal';

type Incoming = {
  copy?: string;
  copyTypeLabel?: string;
  contactId?: string;
  contactName?: string;
  dealId?: string;
  leadId?: string;
};

type RecordChoice = { id: string; name: string; sub: string };

const TYPES: { id: ObjectType; label: string; sub: string }[] = [
  { id: 'lead', label: 'Lead', sub: 'Someone you are still qualifying' },
  { id: 'contact', label: 'Contact', sub: 'A person already in your book' },
  { id: 'deal', label: 'Deal', sub: 'An open opportunity' },
];

function readIncoming(state: unknown): Incoming {
  if (!state || typeof state !== 'object') return {};
  const source = state as Record<string, unknown>;
  const text = (key: string) => (typeof source[key] === 'string' ? (source[key] as string) : undefined);
  return {
    copy: text('copy'),
    copyTypeLabel: text('copyTypeLabel'),
    contactId: text('contactId'),
    contactName: text('contactName'),
    dealId: text('dealId'),
    leadId: text('leadId'),
  };
}

function presetFrom(incoming: Incoming): { type: ObjectType; id: string; name?: string } | null {
  if (incoming.leadId) return { type: 'lead', id: incoming.leadId };
  if (incoming.dealId) return { type: 'deal', id: incoming.dealId };
  if (incoming.contactId) return { type: 'contact', id: incoming.contactId, name: incoming.contactName };
  return null;
}

function matches(query: string, parts: Array<string | number | undefined | null>): boolean {
  return parts.some((part) => String(part ?? '').toLowerCase().includes(query));
}

function recordPath(type: ObjectType, id: string): string {
  if (type === 'lead') return `/leads/${id}`;
  if (type === 'deal') return `/deals/${id}`;
  return `/contacts/${id}`;
}

export default function SendToCrm() {
  const location = useLocation();
  const navigate = useNavigate();
  const incoming = readIncoming(location.state);
  const preset = presetFrom(incoming);

  const [draft, setDraft] = useState(incoming.copy ?? '');
  const [copyTypeLabel] = useState(incoming.copyTypeLabel?.trim() || 'Note');
  const [objectType, setObjectType] = useState<ObjectType>(preset?.type ?? 'lead');
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedRecord, setSelectedRecord] = useState<{ id: string; name: string } | null>(
    preset ? { id: preset.id, name: preset.name ?? 'Selected record' } : null,
  );
  const [isSent, setIsSent] = useState(false);
  const [sending, setSending] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const renamedPreset = useRef(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(false);
    Promise.allSettled([getContacts(), getDeals(), getLeads()]).then(([contactsResult, dealsResult, leadsResult]) => {
      if (cancelled) return;
      if (contactsResult.status === 'fulfilled') setContacts(Array.isArray(contactsResult.value) ? contactsResult.value : []);
      if (dealsResult.status === 'fulfilled') setDeals(Array.isArray(dealsResult.value) ? dealsResult.value : []);
      if (leadsResult.status === 'fulfilled') setLeads(Array.isArray(leadsResult.value) ? leadsResult.value : []);
      const failed = [contactsResult, dealsResult, leadsResult].some((result) => result.status === 'rejected');
      setLoadError(failed);
      setLoading(false);
    });
    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  useEffect(() => {
    if (loading || renamedPreset.current || !preset) return;
    const found =
      preset.type === 'lead'
        ? leads.find((lead) => lead.id === preset.id)
        : preset.type === 'deal'
          ? deals.find((deal) => deal.id === preset.id)
          : contacts.find((contact) => contact.id === preset.id);
    if (!found) return;
    renamedPreset.current = true;
    setSelectedRecord((current) => (current?.id === found.id ? { id: found.id, name: found.name } : current));
  }, [loading, leads, deals, contacts, preset]);

  const query = searchQuery.trim().toLowerCase();
  const records: RecordChoice[] =
    objectType === 'contact'
      ? contacts
          .filter((contact) => !query || matches(query, [contact.name, contact.email, contact.companyName]))
          .map((contact) => ({ id: contact.id, name: contact.name, sub: contact.email || contact.companyName || 'Contact' }))
      : objectType === 'deal'
        ? deals
            .filter((deal) => !query || matches(query, [deal.name, deal.value, deal.stage, deal.companyName]))
            .map((deal) => ({
              id: deal.id,
              name: deal.name,
              sub: [deal.value, deal.stage].filter(Boolean).join(' · ') || 'Deal',
            }))
        : leads
            .filter((lead) => !query || matches(query, [lead.name, lead.email, lead.status, lead.companyName]))
            .map((lead) => ({
              id: lead.id,
              name: lead.name,
              sub: [lead.email, lead.status].filter(Boolean).join(' · ') || 'Lead',
            }));

  const orderedRecords = selectedRecord
    ? [...records.filter((record) => record.id === selectedRecord.id), ...records.filter((record) => record.id !== selectedRecord.id)]
    : records;

  const handleSend = async () => {
    if (!selectedRecord || !draft.trim()) return;
    setSending(true);
    try {
      await sendCopyToCrm({
        objectType,
        recordId: selectedRecord.id,
        recordName: selectedRecord.name,
        copy: draft.trim(),
        copyTypeLabel,
      });
      toast.success(`Saved on ${selectedRecord.name}`);
      setIsSent(true);
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSending(false);
    }
  };

  const typeLabel = TYPES.find((type) => type.id === objectType)?.label ?? 'Record';
  const emptyListHref = objectType === 'lead' ? '/leads' : objectType === 'deal' ? '/deals' : '/contacts';

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          {!isSent ? (
            <div className="w-full max-w-6xl">
              <div className="mb-8 flex items-start gap-4">
                <div className="mt-1 flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-100 text-violet-700" aria-hidden>
                  <Paperclip className="h-5 w-5" />
                </div>
                <div>
                  <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Attach copy</h1>
                  <p className="mt-2 max-w-2xl text-slate-600">
                    Save this writing on a lead, contact, or deal. It stays in Cadence and shows up in History. It is not emailed, and it does not leave this workspace.
                  </p>
                </div>
              </div>

              <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.05fr)]">
                <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
                  <div className="mb-4 flex items-baseline justify-between gap-3">
                    <label htmlFor="attach-copy" className="text-sm font-medium text-slate-700">
                      Writing
                    </label>
                    {incoming.copyTypeLabel ? (
                      <p className="text-xs font-medium text-violet-700">{copyTypeLabel}</p>
                    ) : null}
                  </div>
                  <textarea
                    id="attach-copy"
                    value={draft}
                    onChange={(event) => setDraft(event.target.value)}
                    rows={12}
                    placeholder="Paste a draft, or write the note you want to keep on this record."
                    className="w-full resize-y rounded-xl border border-slate-300 bg-white px-4 py-3 text-slate-900 placeholder:text-slate-400 outline-none transition-colors focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20"
                  />
                  <p className="mt-3 text-sm text-slate-500">
                    Need a first draft?{' '}
                    <Link to="/dashboard" className="font-medium text-orange-700 underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 rounded">
                      Write with AI on the dashboard
                    </Link>
                    , then come back here. History can also open a past draft on this page.
                  </p>
                </section>

                <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
                  <fieldset>
                    <legend className="text-sm font-medium text-slate-700">Save it on</legend>
                    <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3">
                      {TYPES.map((type) => (
                        <button
                          key={type.id}
                          type="button"
                          aria-pressed={objectType === type.id}
                          onClick={() => {
                            setObjectType(type.id);
                            setSearchQuery('');
                            if (preset?.type === type.id) {
                              setSelectedRecord({ id: preset.id, name: preset.name ?? selectedRecord?.name ?? 'Selected record' });
                            } else {
                              setSelectedRecord(null);
                            }
                          }}
                          className={`min-h-11 rounded-xl border-2 px-3 py-3 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 ${
                            objectType === type.id
                              ? 'border-orange-500 bg-orange-50'
                              : 'border-slate-200 bg-white hover:border-orange-200'
                          }`}
                        >
                          <span className="block text-sm font-semibold text-slate-900">{type.label}</span>
                          <span className="mt-0.5 block text-xs text-slate-600">{type.sub}</span>
                        </button>
                      ))}
                    </div>
                  </fieldset>

                  <div className="mt-6">
                    <label htmlFor="attach-search" className="sr-only">
                      Search {typeLabel.toLowerCase()}s
                    </label>
                    <div className="relative">
                      <Search className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400" aria-hidden />
                      <input
                        id="attach-search"
                        type="search"
                        value={searchQuery}
                        onChange={(event) => setSearchQuery(event.target.value)}
                        placeholder={`Search ${typeLabel.toLowerCase()}s`}
                        className="h-11 w-full rounded-lg border border-slate-300 bg-white pl-10 pr-4 text-slate-900 placeholder:text-slate-400 outline-none transition-colors focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20"
                      />
                    </div>
                  </div>

                  {selectedRecord ? (
                    <p className="mt-4 text-sm text-slate-700">
                      Saving on <span className="font-semibold text-slate-900">{selectedRecord.name}</span>
                      <button
                        type="button"
                        onClick={() => setSelectedRecord(null)}
                        className="ml-3 font-medium text-orange-700 underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 rounded"
                      >
                        Clear
                      </button>
                    </p>
                  ) : null}

                  <div className="mt-4">
                    {loading ? (
                      <div className="flex justify-center py-10">
                        <LoadingSpinner size="md" />
                      </div>
                    ) : (
                      <div role="radiogroup" aria-label={`Choose a ${typeLabel.toLowerCase()}`} className="max-h-72 divide-y overflow-y-auto rounded-xl border border-slate-200">
                        {orderedRecords.map((record) => {
                          const selected = selectedRecord?.id === record.id;
                          return (
                            <label
                              key={record.id}
                              className={`flex min-h-11 cursor-pointer items-center px-4 py-3 transition-colors ${selected ? 'bg-orange-50' : 'hover:bg-slate-50'}`}
                            >
                              <input
                                type="radio"
                                name="attach-record"
                                value={record.id}
                                checked={selected}
                                onChange={() => setSelectedRecord({ id: record.id, name: record.name })}
                                className="h-4 w-4 text-orange-600 focus:ring-orange-500 focus:ring-offset-0"
                              />
                              <span className="ml-3 min-w-0">
                                <span className="block truncate font-medium text-slate-900">{record.name}</span>
                                <span className="block truncate text-sm text-slate-600">{record.sub}</span>
                              </span>
                            </label>
                          );
                        })}
                        {orderedRecords.length === 0 && (
                          <div className="px-4 py-8 text-center text-sm text-slate-500">
                            {query ? (
                              <>No {typeLabel.toLowerCase()}s match “{searchQuery.trim()}”.</>
                            ) : (
                              <>
                                No {typeLabel.toLowerCase()}s yet.{' '}
                                <Link to={emptyListHref} className="font-medium text-orange-700 underline-offset-2 hover:underline">
                                  Add one
                                </Link>
                              </>
                            )}
                          </div>
                        )}
                      </div>
                    )}
                    {loadError && !loading ? (
                      <p className="mt-3 text-sm text-rose-700">
                        Some records did not load.{' '}
                        <button
                          type="button"
                          onClick={() => setReloadKey((key) => key + 1)}
                          className="font-medium underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 rounded"
                        >
                          Try again
                        </button>
                      </p>
                    ) : null}
                  </div>

                  <button
                    type="button"
                    onClick={handleSend}
                    disabled={!selectedRecord || !draft.trim() || sending}
                    className="mt-6 h-12 w-full rounded-lg bg-orange-600 font-semibold text-white transition-colors hover:bg-orange-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-500"
                  >
                    {sending ? 'Saving…' : selectedRecord ? `Save on ${selectedRecord.name}` : 'Choose a record to save'}
                  </button>
                </section>
              </div>
            </div>
          ) : (
            <div className="mx-auto max-w-lg text-center">
              <div className="mx-auto mb-6 flex h-20 w-20 items-center justify-center rounded-full bg-emerald-100" aria-hidden>
                <CheckCircle className="h-12 w-12 text-emerald-600" />
              </div>
              <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">Saved in Cadence</h1>
              <p className="mx-auto mt-3 max-w-md text-slate-600">
                The writing is now on {selectedRecord?.name ?? 'that record'} and in History. Nothing was emailed.
              </p>
              {selectedRecord ? (
                <div className="mx-auto mt-6 rounded-xl border border-slate-200 bg-white p-6 text-left shadow-sm">
                  <p className="text-sm text-slate-600">Saved on</p>
                  <p className="mt-1 font-semibold text-slate-900">{selectedRecord.name}</p>
                  <p className="text-sm capitalize text-slate-600">{objectType}</p>
                </div>
              ) : null}
              <div className="mt-8 flex flex-wrap justify-center gap-3">
                {selectedRecord ? (
                  <button
                    type="button"
                    onClick={() => navigate(recordPath(objectType, selectedRecord.id))}
                    className="h-11 min-w-[140px] rounded-lg bg-orange-600 px-4 font-semibold text-white transition-colors hover:bg-orange-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                  >
                    Open {selectedRecord.name}
                  </button>
                ) : null}
                <button
                  type="button"
                  onClick={() => {
                    setIsSent(false);
                    setSelectedRecord(null);
                  }}
                  className="h-11 min-w-[140px] rounded-lg border-2 border-slate-200 bg-white px-4 font-semibold text-slate-900 transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  Save on someone else
                </button>
                <button
                  type="button"
                  onClick={() => navigate('/history')}
                  className="h-11 min-w-[140px] rounded-lg border-2 border-slate-200 bg-white px-4 font-semibold text-slate-900 transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  View history
                </button>
              </div>
            </div>
          )}
        </main>
      </PageTransition>
    </div>
  );
}
