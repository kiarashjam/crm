// Private events: a company's dinner, a wedding reception, a launch. The club's spaces hired for a
// time by a client (a company or a contact), sold through a deal. The server holds the event's
// spaces in the bookings diary for its time and refuses one that clashes with a booking; this page
// lists the events and makes new ones, each worked on its own page.

import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { toast } from 'sonner';
import { CalendarCheck, CalendarHeart, Loader2, Plus, Users } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import { Badge } from '@/app/components/ui/badge';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/app/components/ui/dialog';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { createPrivateEvent, PRIVATE_EVENT_STATUS_LABELS, type PrivateEventQuery } from '@/app/api/corporate';
import { useAllCompanies, useAllContacts, useAllDeals, useClubResources, usePrivateEvents } from '@/app/hooks/queries';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import { useModules } from '@/app/hooks/useModules';
import { useFormat } from '@/app/i18n';
import { fromLocalInputValue, toLocalInputValue } from '@/app/lib/dateInputs';
import { errorText, fieldClass, useClubAmount } from '@/app/components/corporate/parts';

const WHEN = [
  { value: 'upcoming', label: 'Coming up' },
  { value: 'past', label: 'Past' },
  { value: 'all', label: 'All' },
] as const;

type When = (typeof WHEN)[number]['value'];

export default function PrivateEvents() {
  useDocumentTitle(null, 'Private events');
  const f = useFormat();
  const amount = useClubAmount();
  const [params, setParams] = useSearchParams();
  const [when, setWhen] = useState<When>('upcoming');
  const [status, setStatus] = useState('');
  // The start of today, fixed for the page's life, so the query (and its cache key) stays put.
  const [today] = useState(() => { const d = new Date(); d.setHours(0, 0, 0, 0); return d.toISOString(); });
  const query = useMemo<PrivateEventQuery>(() => ({
    fromUtc: when === 'upcoming' ? today : undefined,
    toUtc: when === 'past' ? today : undefined,
    status: status || undefined,
  }), [when, status, today]);
  const events = usePrivateEvents(query);
  const rows = events.data ?? [];
  const [creating, setCreating] = useState(false);

  // Asked for from a company's or a contact's page: the new event opens for them.
  useEffect(() => {
    if (params.get('new') === '1') setCreating(true);
  }, [params]);
  const closeCreate = (open: boolean) => {
    setCreating(open);
    if (!open && params.get('new')) {
      const next = new URLSearchParams(params);
      next.delete('new');
      setParams(next, { replace: true });
    }
  };

  const enquiries = rows.filter((e) => e.status === 'Enquiry').length;
  const confirmed = rows.filter((e) => e.status === 'Confirmed').length;
  const guests = rows.filter((e) => e.status !== 'Cancelled').reduce((sum, e) => sum + e.guestCount, 0);

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={CalendarHeart}
            tone="amber"
            title="Private events"
            subtitle="The club’s spaces hired by a company or a private client: held in the bookings diary for their time, with their menu, deposit, invoice, contract and guest list."
            stats={[
              { label: 'Enquiries', value: enquiries, icon: CalendarHeart, tone: 'amber' },
              { label: 'Confirmed', value: confirmed, icon: CalendarCheck, tone: 'emerald' },
              { label: 'Guests expected', value: guests, icon: Users, tone: 'violet' },
            ]}
            actions={(
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button type="button" className={heroPrimary('amber')} onClick={() => setCreating(true)}>
                  <Plus className="h-4 w-4" aria-hidden="true" /> New private event
                </Button>
              </WriteOnly>
            )}
          />

          <div className="mb-5 flex flex-wrap items-end gap-4">
            <div>
              <Label htmlFor="private-events-when" className="text-sm text-slate-600">When</Label>
              <select id="private-events-when" className={fieldClass} value={when} onChange={(e) => setWhen(e.target.value as When)}>
                {WHEN.map((w) => <option key={w.value} value={w.value}>{w.label}</option>)}
              </select>
            </div>
            <div>
              <Label htmlFor="private-events-status" className="text-sm text-slate-600">Status</Label>
              <select id="private-events-status" className={fieldClass} value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="">Any</option>
                {Object.entries(PRIVATE_EVENT_STATUS_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </div>
            {events.isFetching && !events.isPending && <Loader2 className="h-4 w-4 animate-spin text-slate-400" aria-label="Refreshing" />}
          </div>

          {events.isError && !events.data ? (
            <LoadFailed what="Private events" error={events.error} onRetry={() => void events.refetch()} retrying={events.isFetching} />
          ) : events.isPending ? (
            <p className="flex items-center gap-2 text-slate-500" role="status"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
          ) : rows.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-slate-600 dark:border-slate-700 dark:bg-slate-800">
              No private events here.
            </div>
          ) : (
            <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-800">
              <table className="w-full text-sm">
                <thead className="bg-slate-50 text-left text-xs uppercase tracking-wider text-slate-500 dark:bg-slate-900">
                  <tr>
                    <th scope="col" className="px-4 py-2">When</th>
                    <th scope="col" className="px-4 py-2">Event</th>
                    <th scope="col" className="px-4 py-2">Client</th>
                    <th scope="col" className="px-4 py-2">Spaces</th>
                    <th scope="col" className="px-4 py-2">Guests</th>
                    <th scope="col" className="px-4 py-2">Status</th>
                    <th scope="col" className="px-4 py-2 text-right">Menu and services</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((e) => (
                    <tr key={e.id} className="border-t border-slate-100 dark:border-slate-700">
                      <td className="whitespace-nowrap px-4 py-2">{f.dateTime(e.startAtUtc, 'short')}</td>
                      <td className="px-4 py-2">
                        <Link to={`/private-events/${e.id}`} className="font-medium text-slate-900 hover:text-amber-800 dark:text-slate-100">{e.title}</Link>
                      </td>
                      <td className="px-4 py-2">{[e.companyName, e.contactName].filter(Boolean).join(' · ') || '—'}</td>
                      <td className="px-4 py-2">{e.spaces.map((s) => s.resourceName).join(', ') || '—'}</td>
                      <td className="px-4 py-2">{e.guestCount}</td>
                      <td className="px-4 py-2"><Badge variant="outline">{PRIVATE_EVENT_STATUS_LABELS[e.status] ?? e.status}</Badge></td>
                      <td className="px-4 py-2 text-right">{amount(e.money.linesTotal, e.currency)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {events.isError && events.data && (
            <LoadFailed stale what="Private events" error={events.error} onRetry={() => void events.refetch()} retrying={events.isFetching} className="mt-3" />
          )}
        </main>
      </PageTransition>
      <NewPrivateEventDialog open={creating} onOpenChange={closeCreate}
        companyId={params.get('companyId') ?? ''} contactId={params.get('contactId') ?? ''} />
    </div>
  );
}

/** Tomorrow at 18:00 to 23:00, where the user is: a starting point for an evening's hire. */
function defaultTimes(): { start: string; end: string } {
  const start = new Date();
  start.setDate(start.getDate() + 1);
  start.setHours(18, 0, 0, 0);
  const end = new Date(start);
  end.setHours(23, 0, 0, 0);
  return { start: toLocalInputValue(start.toISOString()), end: toLocalInputValue(end.toISOString()) };
}

function NewPrivateEventDialog({ open, onOpenChange, companyId, contactId }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  companyId: string;
  contactId: string;
}) {
  const navigate = useNavigate();
  const salesOn = useModules().sales;
  const companies = useAllCompanies(open);
  const contacts = useAllContacts(open);
  const deals = useAllDeals(open && salesOn);
  const resources = useClubResources();
  const times = useMemo(defaultTimes, []);
  const blank = { title: '', companyId, contactId, dealId: '', start: times.start, end: times.end, guests: '', notes: '', spaces: [] as string[] };
  const [form, setForm] = useState(blank);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) setForm((current) => ({ ...current, companyId: companyId || current.companyId, contactId: contactId || current.contactId }));
  }, [open, companyId, contactId]);

  const startUtc = fromLocalInputValue(form.start);
  const endUtc = fromLocalInputValue(form.end);
  const timesValid = !!startUtc && !!endUtc && endUtc > startUtc;
  const valid = form.title.trim().length > 0 && (!!form.companyId || !!form.contactId) && timesValid;

  const toggleSpace = (id: string) =>
    setForm((current) => ({ ...current, spaces: current.spaces.includes(id) ? current.spaces.filter((s) => s !== id) : [...current.spaces, id] }));

  const save = async () => {
    setSaving(true);
    try {
      const made = await createPrivateEvent({
        title: form.title.trim(),
        companyId: form.companyId || undefined,
        contactId: form.contactId || undefined,
        dealId: form.dealId || undefined,
        startAtUtc: startUtc,
        endAtUtc: endUtc,
        guestCount: form.guests ? Number(form.guests) : undefined,
        resourceIds: form.spaces,
        notes: form.notes.trim() || undefined,
      });
      toast.success(made.spaces.length > 0 ? `${made.title} is pencilled in, its spaces held.` : `${made.title} is pencilled in.`);
      setForm(blank);
      onOpenChange(false);
      navigate(`/private-events/${made.id}`);
    } catch (err) {
      toast.error(errorText(err, 'The private event was not made.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader><DialogTitle>New private event</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <Label htmlFor="pe-title">Title</Label>
            <Input id="pe-title" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} maxLength={200} placeholder="Acme summer party" />
          </div>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            <div>
              <Label htmlFor="pe-company">Company</Label>
              <select id="pe-company" className={fieldClass} value={form.companyId} onChange={(e) => setForm({ ...form, companyId: e.target.value })}>
                <option value="">None</option>
                {(companies.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div>
              <Label htmlFor="pe-contact">Contact</Label>
              <select id="pe-contact" className={fieldClass} value={form.contactId} onChange={(e) => setForm({ ...form, contactId: e.target.value })}>
                <option value="">None</option>
                {(contacts.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
          </div>
          {salesOn && (
            <div>
              <Label htmlFor="pe-deal">Deal (its contract is drafted on it)</Label>
              <select id="pe-deal" className={fieldClass} value={form.dealId} onChange={(e) => setForm({ ...form, dealId: e.target.value })}>
                <option value="">None yet</option>
                {(deals.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </div>
          )}
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <div>
              <Label htmlFor="pe-start">Starts</Label>
              <Input id="pe-start" type="datetime-local" value={form.start} onChange={(e) => setForm({ ...form, start: e.target.value })} />
            </div>
            <div>
              <Label htmlFor="pe-end">Ends</Label>
              <Input id="pe-end" type="datetime-local" value={form.end} onChange={(e) => setForm({ ...form, end: e.target.value })} />
            </div>
            <div>
              <Label htmlFor="pe-guests">Guests</Label>
              <Input id="pe-guests" inputMode="numeric" value={form.guests} onChange={(e) => setForm({ ...form, guests: e.target.value.replace(/\D/g, '') })} />
            </div>
          </div>
          {!timesValid && form.start && form.end && <p className="text-xs text-rose-700">The end must be after the start.</p>}
          <fieldset>
            <legend className="text-sm font-medium">Spaces to hold</legend>
            {(resources.data ?? []).length === 0 ? (
              <p className="text-xs text-slate-500">The club has no bookable spaces set up yet.</p>
            ) : (
              <div className="mt-1 flex flex-wrap gap-3">
                {(resources.data ?? []).map((r) => (
                  <label key={r.id} className="flex items-center gap-1.5 text-sm">
                    <input type="checkbox" checked={form.spaces.includes(r.id)} onChange={() => toggleSpace(r.id)} className="h-4 w-4" />
                    {r.name}
                  </label>
                ))}
              </div>
            )}
          </fieldset>
          <div>
            <Label htmlFor="pe-notes">Notes</Label>
            <Textarea id="pe-notes" rows={2} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} maxLength={2000} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={() => void save()} disabled={!valid || saving}>{saving ? 'Making…' : 'Make the event'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
