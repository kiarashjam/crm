// The signed-in portal's sections: the member's account and statements, events, bookings,
// points and their own details. Each reads its own part with the member's session, says when
// a read failed with a way to try again, and puts what a change answers straight into the
// cache, so the page shows what the club now has.

import { useState, type FormEvent, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { CreditCard, Download } from 'lucide-react';
import {
  savePortalSession, type PortalBooking, type PortalConsentState, type PortalEvent, type PortalResource,
} from '@/app/api/portal';
import { timed } from '@/app/hooks/queries/queryClient';
import { LanguageSwitcher } from '@/app/i18n/LanguageSwitcher';
import { useT, type I18nValue, type Language, type MessageKey } from '@/app/i18n';
import { dayKeyIn, instantIn } from '@/app/lib/zonedTime';
import {
  buttonClass, inputClass, LoadFailed, openPayPage, portalErrorText, secondaryButtonClass,
  useClubFormats, type ClubFormats, type SectionProps,
} from './portalCommon';

const MEMBER_STATUSES = ['Active', 'Pending', 'Lapsed', 'Cancelled'];
const BOOKING_STATUSES = ['Pending', 'Confirmed', 'CheckedIn', 'Completed', 'Cancelled', 'NoShow'];
const MARKETING_CHANNELS = ['email', 'sms', 'post'] as const;

/** A status in the member's words, or the server's own word for one the catalogue does not know. */
export function statusWords(t: I18nValue['t'], prefix: 'portal.status' | 'portal.bookingStatus', status: string): string {
  const known = prefix === 'portal.status' ? MEMBER_STATUSES : BOOKING_STATUSES;
  return known.includes(status) ? t(`${prefix}.${status}` as MessageKey) : status;
}

function Section({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-semibold text-slate-900">{title}</h2>
        {action}
      </div>
      {children}
    </section>
  );
}

function Loading() {
  const { t } = useT();
  return <p aria-busy="true" className="text-sm text-slate-500">{t('common.loading')}</p>;
}

function Note({ children, tone = 'slate' }: { children: ReactNode; tone?: 'slate' | 'amber' }) {
  return (
    <p className={`rounded-lg px-3 py-2 text-sm ${tone === 'amber' ? 'bg-amber-50 text-amber-900' : 'bg-slate-50 text-slate-700'}`}>
      {children}
    </p>
  );
}

/** "18:00 – 19:30" on the same day, the whole second date and time otherwise. */
function span(f: ClubFormats, startUtc: string, endUtc: string): string {
  const sameDay = dayKeyIn(startUtc, f.zone) === dayKeyIn(endUtc, f.zone);
  return `${f.dateTime(startUtc)} – ${sameDay ? f.time(endUtc) : f.dateTime(endUtc)}`;
}

/** The months with statements, newest first, from the first with an entry to this one (yyyy-MM). */
export function statementMonths(first: string, last: string): string[] {
  const read = (month: string) => {
    const m = /^(\d{4})-(\d{2})$/.exec(month);
    return m ? { year: Number(m[1]), month: Number(m[2]) } : null;
  };
  const from = read(first);
  const to = read(last);
  if (!to) return [];
  if (!from) return [last];
  const months: string[] = [];
  let { year, month } = to;
  while ((year > from.year || (year === from.year && month >= from.month)) && months.length < 120) {
    months.push(`${year}-${String(month).padStart(2, '0')}`);
    month -= 1;
    if (month === 0) {
      month = 12;
      year -= 1;
    }
  }
  return months;
}

// ── the account ──────────────────────────────────────────────────────────────

export function AccountSection({ api, keys, me }: SectionProps) {
  const { t } = useT();
  const f = useClubFormats(me.locale, me.currency, me.timeZone);
  const account = useQuery({ queryKey: keys.account, queryFn: ({ signal }) => timed(signal, (s) => api.account(s)) });
  const [ticked, setTicked] = useState<ReadonlySet<string>>(new Set());
  const [paying, setPaying] = useState(false);

  const pay = async (chargeIds?: string[]) => {
    setPaying(true);
    try {
      const { url } = await api.pay(chargeIds);
      openPayPage(url);
    } catch (err) {
      toast.error(portalErrorText(err, t));
      setPaying(false);
    }
  };

  if (account.isPending) return <Loading />;
  if (account.isError) return <LoadFailed onRetry={() => void account.refetch()} />;
  const a = account.data;
  const open = a.openCharges;
  const tick = (id: string, on: boolean) => setTicked((was) => {
    const next = new Set(was);
    if (on) next.add(id);
    else next.delete(id);
    return next;
  });
  const tickedIds = open.filter((c) => ticked.has(c.id)).map((c) => c.id);

  return (
    <div className="space-y-4">
      <Section title={t('portal.account.balance')}>
        <p className="text-2xl font-semibold text-slate-900">
          {a.owed > 0
            ? t('portal.account.owed', { amount: f.money(a.owed, a.currency) })
            : a.credit > 0
              ? t('portal.account.credit', { amount: f.money(a.credit, a.currency) })
              : t('portal.account.settled')}
        </p>
        {a.billsGoToPrimary && a.primaryName && (
          <div className="mt-3"><Note>{t('portal.account.rollsUp', { name: a.primaryName })}</Note></div>
        )}
        {a.owed > 0 && (me.paysOnline ? (
          <div className="mt-4 flex flex-wrap gap-2">
            <button type="button" className={buttonClass} disabled={paying} onClick={() => void pay()}>
              <CreditCard className="h-4 w-4" aria-hidden />
              {paying ? t('portal.opening') : t('portal.account.payAll', { amount: f.money(a.owed, a.currency) })}
            </button>
            {tickedIds.length > 0 && tickedIds.length < open.length && (
              <button type="button" className={secondaryButtonClass} disabled={paying} onClick={() => void pay(tickedIds)}>
                {t('portal.account.paySelected')}
              </button>
            )}
          </div>
        ) : (
          <div className="mt-3"><Note>{t('portal.account.paysOffline')}</Note></div>
        ))}
      </Section>

      <Section title={t('portal.account.openCharges')}>
        {open.length === 0 ? (
          <p className="text-sm text-slate-500">{t('portal.account.noOpen')}</p>
        ) : (
          <ul className="divide-y divide-slate-100">
            {open.map((c) => (
              <li key={c.id} className="flex items-start gap-3 py-2.5">
                {me.paysOnline && (
                  <input
                    type="checkbox"
                    className="mt-1 h-4 w-4 rounded border-slate-300"
                    checked={ticked.has(c.id)}
                    onChange={(e) => tick(c.id, e.target.checked)}
                    aria-label={t('portal.account.payThis', { charge: c.description })}
                  />
                )}
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-slate-900">{c.description}</p>
                  <p className="text-xs text-slate-500">
                    {f.date(c.postedAtUtc)}
                    {c.dueAtUtc ? ` · ${t('portal.account.due', { date: f.date(c.dueAtUtc) })}` : ''}
                    {c.incurredByName ? ` · ${t('portal.account.incurredBy', { name: c.incurredByName })}` : ''}
                  </p>
                </div>
                <span className="shrink-0 font-medium tabular-nums text-slate-900">{f.money(c.outstanding, c.currency)}</span>
              </li>
            ))}
          </ul>
        )}
      </Section>

      {me.paysOnline && <CardPanel api={api} keys={keys} me={me} />}

      <StatementsPanel api={api} keys={keys} me={me} first={a.firstMonth} last={a.currentMonth} />
    </div>
  );
}

function CardPanel({ api, keys }: SectionProps) {
  const { t } = useT();
  const client = useQueryClient();
  const card = useQuery({ queryKey: keys.card, queryFn: ({ signal }) => timed(signal, (s) => api.card(s)) });
  const [busy, setBusy] = useState(false);

  const setUp = async (autoPay: boolean) => {
    setBusy(true);
    try {
      const { url } = await api.setUpCard(autoPay);
      openPayPage(url);
    } catch (err) {
      toast.error(portalErrorText(err, t));
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!window.confirm(t('portal.card.removeConfirm'))) return;
    setBusy(true);
    try {
      client.setQueryData(keys.card, await api.removeCard());
      toast.success(t('portal.card.removed'));
    } catch (err) {
      toast.error(portalErrorText(err, t));
    } finally {
      setBusy(false);
    }
  };

  const saved = card.data?.card;
  return (
    <Section title={t('portal.card.title')}>
      {card.isPending ? <Loading /> : card.isError ? <LoadFailed onRetry={() => void card.refetch()} /> : saved ? (
        <div className="space-y-3">
          <p className="text-sm text-slate-800">
            {t('portal.card.saved', {
              brand: saved.brand || saved.type,
              last4: saved.last4 ?? '····',
              month: saved.expMonth ? String(saved.expMonth).padStart(2, '0') : '–',
              year: saved.expYear ?? '–',
            })}
          </p>
          {card.data.autoPayDues && <p className="text-sm text-slate-600">{t('portal.card.autoPay')}</p>}
          <button type="button" className={secondaryButtonClass} disabled={busy} onClick={() => void remove()}>
            {t('portal.card.remove')}
          </button>
        </div>
      ) : (
        <div className="space-y-3">
          <p className="text-sm text-slate-600">{t('portal.card.none')}</p>
          <div className="flex flex-wrap gap-2">
            <button type="button" className={secondaryButtonClass} disabled={busy} onClick={() => void setUp(false)}>
              {t('portal.card.save')}
            </button>
            <button type="button" className={secondaryButtonClass} disabled={busy} onClick={() => void setUp(true)}>
              {t('portal.card.saveAutoPay')}
            </button>
          </div>
        </div>
      )}
    </Section>
  );
}

function StatementsPanel({ api, keys, me, first, last }: SectionProps & { first: string; last: string }) {
  const { t } = useT();
  const f = useClubFormats(me.locale, me.currency, me.timeZone);
  const months = statementMonths(first, last);
  const [month, setMonth] = useState(months[0] ?? last);
  const statement = useQuery({
    queryKey: keys.statement(month),
    queryFn: ({ signal }) => timed(signal, (s) => api.statement(month, s)),
    enabled: Boolean(month),
  });
  const [downloading, setDownloading] = useState(false);

  const download = async (household: boolean) => {
    setDownloading(true);
    try {
      await api.statementPdf(month, household);
    } catch (err) {
      toast.error(portalErrorText(err, t));
    } finally {
      setDownloading(false);
    }
  };

  const monthName = (value: string) => {
    const [y = 1970, m = 1] = value.split('-').map(Number);
    return f.custom(new Date(Date.UTC(y, m - 1, 15)), { month: 'long', year: 'numeric', timeZone: 'UTC' });
  };

  return (
    <Section
      title={t('portal.account.statements')}
      action={months.length > 0 && (
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <span>{t('portal.account.month')}</span>
          <select value={month} onChange={(e) => setMonth(e.target.value)} className="h-9 rounded-lg border border-slate-200 bg-white px-2 text-sm text-slate-800">
            {months.map((m) => <option key={m} value={m}>{monthName(m)}</option>)}
          </select>
        </label>
      )}
    >
      {statement.isPending ? <Loading /> : statement.isError ? <LoadFailed onRetry={() => void statement.refetch()} /> : (
        <div className="space-y-3">
          <div className="flex flex-wrap justify-between gap-2 text-sm text-slate-600">
            <span>{t('portal.account.opening')}: <span className="tabular-nums">{f.money(statement.data.openingOwed, statement.data.currency)}</span></span>
            <span className="font-medium text-slate-900">
              {t('portal.account.closing')}: <span className="tabular-nums">{f.money(statement.data.closingOwed, statement.data.currency)}</span>
            </span>
          </div>
          {statement.data.lines.length === 0 ? (
            <p className="text-sm text-slate-500">{t('portal.account.noLines')}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 text-left text-xs uppercase tracking-wide text-slate-500">
                    <th scope="col" className="py-2 pr-3 font-medium">{t('portal.bookings.date')}</th>
                    <th scope="col" className="py-2 pr-3 font-medium" />
                    <th scope="col" className="py-2 pr-3 text-right font-medium">{t('portal.account.charge')}</th>
                    <th scope="col" className="py-2 text-right font-medium">{t('portal.account.payment')}</th>
                  </tr>
                </thead>
                <tbody>
                  {statement.data.lines.map((line, i) => (
                    <tr key={`${line.atUtc}-${i}`} className="border-b border-slate-100 last:border-0">
                      <td className="whitespace-nowrap py-2 pr-3 text-slate-600">{f.date(line.atUtc, 'short')}</td>
                      <td className="py-2 pr-3 text-slate-900">
                        {line.description}
                        {line.incurredByName && <span className="block text-xs text-slate-500">{t('portal.account.incurredBy', { name: line.incurredByName })}</span>}
                      </td>
                      <td className="py-2 pr-3 text-right tabular-nums">{line.charge ? f.money(line.charge, statement.data.currency) : ''}</td>
                      <td className="py-2 text-right tabular-nums">{line.payment ? f.money(line.payment, statement.data.currency) : ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="flex flex-wrap gap-2">
            <button type="button" className={secondaryButtonClass} disabled={downloading} onClick={() => void download(false)}>
              <Download className="h-4 w-4" aria-hidden /> {t('portal.account.download')}
            </button>
            {me.household?.isPrimary && (
              <button type="button" className={secondaryButtonClass} disabled={downloading} onClick={() => void download(true)}>
                <Download className="h-4 w-4" aria-hidden /> {t('portal.account.downloadHousehold')}
              </button>
            )}
          </div>
        </div>
      )}
    </Section>
  );
}

// ── events ───────────────────────────────────────────────────────────────────

export function EventsSection({ api, keys, me }: SectionProps) {
  const { t } = useT();
  const f = useClubFormats(me.locale, me.currency, me.timeZone);
  const client = useQueryClient();
  const events = useQuery({ queryKey: keys.events, queryFn: ({ signal }) => timed(signal, (s) => api.events(s)) });
  const [busy, setBusy] = useState<string | null>(null);

  const put = (next: PortalEvent) =>
    client.setQueryData<PortalEvent[]>(keys.events, (list) => list?.map((e) => (e.id === next.id ? next : e)));

  const act = async (ev: PortalEvent, run: () => Promise<PortalEvent>, done: MessageKey) => {
    setBusy(ev.id);
    try {
      put(await run());
      toast.success(t(done));
      // A ticket goes on their account, and comes off it when the place is cancelled.
      void client.invalidateQueries({ queryKey: keys.account });
    } catch (err) {
      toast.error(portalErrorText(err, t));
      void client.invalidateQueries({ queryKey: keys.events });
    } finally {
      setBusy(null);
    }
  };

  const signUp = (ev: PortalEvent) => act(ev, () => api.register(ev.id), 'portal.events.signedUp');
  const cancel = (ev: PortalEvent) => {
    if (!ev.myRegistrationId || !window.confirm(t('portal.events.cancelConfirm', { event: ev.name }))) return;
    const registrationId = ev.myRegistrationId;
    void act(ev, () => api.cancelRegistration(registrationId), 'portal.events.cancelledToast');
  };

  return (
    <div className="space-y-4">
      {!me.active && <Note tone="amber">{t('portal.notActive')}</Note>}
      <Section title={t('portal.events.title')}>
        <p className="mb-3 text-xs text-slate-500">{t('portal.events.cutoff', { hours: me.memberCancelHours })}</p>
        {events.isPending ? <Loading /> : events.isError ? <LoadFailed onRetry={() => void events.refetch()} /> : events.data.length === 0 ? (
          <p className="text-sm text-slate-500">{t('portal.events.none')}</p>
        ) : (
          <ul className="space-y-3">
            {events.data.map((ev) => {
              const placed = ev.myStatus === 'Reserved' || ev.myStatus === 'Waitlisted';
              const canSignUp = me.active && (ev.myStatus === 'none' || ev.myStatus === 'Cancelled');
              return (
                <li key={ev.id} className="rounded-xl border border-slate-200 p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-medium text-slate-900">
                        <span aria-hidden>{ev.coverEmoji} </span>{ev.name}
                      </p>
                      <p className="text-sm text-slate-600">{span(f, ev.startAtUtc, ev.endAtUtc)}{ev.location ? ` · ${ev.location}` : ''}</p>
                      {ev.description && <p className="mt-1 line-clamp-3 text-sm text-slate-600">{ev.description}</p>}
                      <p className="mt-1 text-xs text-slate-500">
                        {ev.memberPrice > 0 ? t('portal.events.price', { amount: f.money(ev.memberPrice, ev.currency) }) : t('portal.events.free')}
                        {ev.seatsLeft != null && ` · ${ev.seatsLeft > 0 ? t('portal.events.seatsLeft', { count: ev.seatsLeft }) : t('portal.events.full')}`}
                      </p>
                      {ev.householdComing.length > 0 && (
                        <p className="mt-1 text-xs text-slate-500">{t('portal.events.household', { names: ev.householdComing.join(', ') })}</p>
                      )}
                    </div>
                    <div className="flex shrink-0 flex-col items-end gap-2">
                      {ev.myStatus === 'Reserved' && <Badge tone="green">{t('portal.events.reserved')}</Badge>}
                      {ev.myStatus === 'Waitlisted' && (
                        <Badge tone="amber">{t('portal.events.waitlisted', { position: ev.waitlistPosition ?? '–' })}</Badge>
                      )}
                      {ev.myStatus === 'CheckedIn' && <Badge tone="green">{t('portal.events.checkedIn')}</Badge>}
                      {ev.myStatus === 'NoShow' && <Badge tone="slate">{t('portal.events.noShow')}</Badge>}
                      {ev.myStatus === 'Cancelled' && <Badge tone="slate">{t('portal.events.cancelled')}</Badge>}
                      {canSignUp && (
                        <button type="button" className={buttonClass} disabled={busy === ev.id} onClick={() => void signUp(ev)}>
                          {ev.seatsLeft === 0 ? t('portal.events.joinWaitlist') : t('portal.events.signUp')}
                        </button>
                      )}
                      {placed && ev.canCancel && (
                        <button type="button" className={secondaryButtonClass} disabled={busy === ev.id} onClick={() => cancel(ev)}>
                          {t('portal.events.cancel')}
                        </button>
                      )}
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </Section>
    </div>
  );
}

function Badge({ tone, children }: { tone: 'green' | 'amber' | 'slate'; children: ReactNode }) {
  const tones = {
    green: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
    amber: 'bg-amber-50 text-amber-900 ring-amber-200',
    slate: 'bg-slate-50 text-slate-700 ring-slate-200',
  };
  return <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${tones[tone]}`}>{children}</span>;
}

// ── bookings ─────────────────────────────────────────────────────────────────

export function BookingsSection({ api, keys, me }: SectionProps) {
  const { t } = useT();
  const f = useClubFormats(me.locale, me.currency, me.timeZone);
  const client = useQueryClient();
  const resources = useQuery({ queryKey: keys.resources, queryFn: ({ signal }) => timed(signal, (s) => api.resources(s)) });
  const bookings = useQuery({ queryKey: keys.bookings, queryFn: ({ signal }) => timed(signal, (s) => api.bookings(s)) });
  const [busy, setBusy] = useState<string | null>(null);

  const cancel = async (b: PortalBooking) => {
    if (!window.confirm(t('portal.bookings.cancelConfirm', { resource: b.resourceName }))) return;
    setBusy(b.id);
    try {
      const next = await api.cancelBooking(b.id);
      client.setQueryData<PortalBooking[]>(keys.bookings, (list) => list?.map((x) => (x.id === next.id ? next : x)));
      toast.success(t('portal.bookings.cancelledToast'));
      void client.invalidateQueries({ queryKey: keys.account });
    } catch (err) {
      toast.error(portalErrorText(err, t));
      void client.invalidateQueries({ queryKey: keys.bookings });
    } finally {
      setBusy(null);
    }
  };

  const now = Date.now();
  const list = [...(bookings.data ?? [])];
  const upcoming = list.filter((b) => Date.parse(b.endAtUtc) > now).sort((a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc));
  const earlier = list.filter((b) => Date.parse(b.endAtUtc) <= now).sort((a, b) => Date.parse(b.startAtUtc) - Date.parse(a.startAtUtc));

  return (
    <div className="space-y-4">
      {!me.active && <Note tone="amber">{t('portal.notActive')}</Note>}
      {me.active && (
        resources.isPending ? <Loading /> : resources.isError ? <LoadFailed onRetry={() => void resources.refetch()} /> : resources.data.length === 0 ? (
          <Note>{t('portal.bookings.nothingOffered')}</Note>
        ) : (
          <BookingForm api={api} keys={keys} me={me} resources={resources.data} f={f} />
        )
      )}
      <Section title={t('portal.bookings.title')}>
        {bookings.isPending ? <Loading /> : bookings.isError ? <LoadFailed onRetry={() => void bookings.refetch()} /> : list.length === 0 ? (
          <p className="text-sm text-slate-500">{t('portal.bookings.none')}</p>
        ) : (
          <ul className="divide-y divide-slate-100">
            {[...upcoming, ...earlier].map((b) => (
              <li key={b.id} className={`flex flex-wrap items-start justify-between gap-3 py-3 ${Date.parse(b.endAtUtc) <= now ? 'opacity-70' : ''}`}>
                <div className="min-w-0">
                  <p className="font-medium text-slate-900">{b.resourceName}</p>
                  <p className="text-sm text-slate-600">{span(f, b.startAtUtc, b.endAtUtc)} · {t('portal.bookings.party')}: {b.partySize}</p>
                  {!b.mine && b.forName && <p className="text-xs text-slate-500">{t('portal.bookings.for', { name: b.forName })}</p>}
                  {b.depositAmount > 0 && (
                    <p className="text-xs text-slate-500">{t('portal.bookings.deposit', { amount: f.money(b.depositAmount, b.currency) })}</p>
                  )}
                  {b.notes && <p className="mt-1 text-xs text-slate-500">{b.notes}</p>}
                </div>
                <div className="flex shrink-0 flex-col items-end gap-2">
                  <Badge tone={b.status === 'Confirmed' || b.status === 'CheckedIn' ? 'green' : b.status === 'Pending' ? 'amber' : 'slate'}>
                    {statusWords(t, 'portal.bookingStatus', b.status)}
                  </Badge>
                  {b.mine && b.canCancel && (
                    <button type="button" className={secondaryButtonClass} disabled={busy === b.id} onClick={() => void cancel(b)}>
                      {t('portal.bookings.cancel')}
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  );
}

function BookingForm({ api, keys, resources, f }: SectionProps & { resources: PortalResource[]; f: ClubFormats }) {
  const { t } = useT();
  const client = useQueryClient();
  const [resourceId, setResourceId] = useState(resources[0]?.id ?? '');
  const [day, setDay] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [party, setParty] = useState('1');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const resource = resources.find((r) => r.id === resourceId) ?? resources[0];
  const today = dayKeyIn(new Date(), f.zone);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    // The times are the club's: 18:00 is six in the evening where the club is, wherever the member is.
    const start = instantIn(day, from, f.zone);
    const end = instantIn(day, to, f.zone);
    const size = Number(party);
    if (!resource || !start || !end || end <= start || start.getTime() <= Date.now()) {
      setError(t('portal.error.bookingTimes'));
      return;
    }
    if (!Number.isInteger(size) || size < 1) {
      setError(t('portal.error.party'));
      return;
    }
    setBusy(true);
    try {
      const booked = await api.book({
        resourceId: resource.id,
        startAtUtc: start.toISOString(),
        endAtUtc: end.toISOString(),
        partySize: size,
        notes: notes.trim() || undefined,
      });
      client.setQueryData<PortalBooking[]>(keys.bookings, (list) => [booked, ...(list ?? []).filter((b) => b.id !== booked.id)]);
      void client.invalidateQueries({ queryKey: keys.account });
      toast.success(booked.status === 'Confirmed' ? t('portal.bookings.booked') : t('portal.bookings.requested'));
      setFrom('');
      setTo('');
      setNotes('');
    } catch (err) {
      setError(portalErrorText(err, t));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Section title={t('portal.bookings.new')}>
      <form onSubmit={(e) => void submit(e)} className="grid gap-3 sm:grid-cols-2" noValidate>
        <label className="text-sm font-medium text-slate-700 sm:col-span-2">
          {t('portal.bookings.what')}
          <select value={resource?.id ?? ''} onChange={(e) => setResourceId(e.target.value)} className={`${inputClass} mt-1 bg-white`}>
            {resources.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        </label>
        {resource && (
          <div className="text-xs text-slate-500 sm:col-span-2">
            {resource.description && <p>{resource.description}</p>}
            <p>
              {resource.confirmedAtOnce ? t('portal.bookings.instant') : t('portal.bookings.byRequest')}
              {resource.depositAmount > 0 && ` · ${t('portal.bookings.deposit', { amount: f.money(resource.depositAmount, resource.currency) })}`}
            </p>
          </div>
        )}
        <label className="text-sm font-medium text-slate-700">
          {t('portal.bookings.date')}
          <input type="date" required min={today} value={day} onChange={(e) => setDay(e.target.value)} className={`${inputClass} mt-1`} />
        </label>
        <label className="text-sm font-medium text-slate-700">
          {t('portal.bookings.party')}
          <input
            type="number"
            min={1}
            max={resource?.capacity || undefined}
            value={party}
            onChange={(e) => setParty(e.target.value)}
            className={`${inputClass} mt-1`}
          />
        </label>
        <label className="text-sm font-medium text-slate-700">
          {t('portal.bookings.from')}
          <input type="time" required value={from} onChange={(e) => setFrom(e.target.value)} className={`${inputClass} mt-1`} />
        </label>
        <label className="text-sm font-medium text-slate-700">
          {t('portal.bookings.to')}
          <input type="time" required value={to} onChange={(e) => setTo(e.target.value)} className={`${inputClass} mt-1`} />
        </label>
        <label className="text-sm font-medium text-slate-700 sm:col-span-2">
          {t('portal.bookings.notes')}
          <input type="text" maxLength={500} value={notes} onChange={(e) => setNotes(e.target.value)} className={`${inputClass} mt-1`} />
        </label>
        <p role="alert" className="text-sm text-red-600 sm:col-span-2">{error}</p>
        <div className="sm:col-span-2">
          <button type="submit" className={buttonClass} disabled={busy || !resource || !day || !from || !to}>
            {resource?.confirmedAtOnce === false ? t('portal.bookings.request') : t('portal.bookings.submit')}
          </button>
        </div>
      </form>
    </Section>
  );
}

// ── points ───────────────────────────────────────────────────────────────────

export function PointsSection({ api, keys, me }: SectionProps) {
  const { t } = useT();
  const f = useClubFormats(me.locale, me.currency, me.timeZone);
  const points = useQuery({ queryKey: keys.points, queryFn: ({ signal }) => timed(signal, (s) => api.points(s)) });
  if (points.isPending) return <Loading />;
  if (points.isError) return <LoadFailed onRetry={() => void points.refetch()} />;
  return (
    <Section title={t('portal.points.title')}>
      <p className="text-2xl font-semibold text-slate-900">{t('portal.points.balance', { count: points.data.balance })}</p>
      {points.data.history.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">{t('portal.points.none')}</p>
      ) : (
        <ul className="mt-3 divide-y divide-slate-100">
          {points.data.history.map((entry, i) => (
            <li key={`${entry.atUtc}-${i}`} className="flex items-start justify-between gap-3 py-2.5">
              <div className="min-w-0">
                <p className="text-sm text-slate-900">{entry.reason}</p>
                <p className="text-xs text-slate-500">{f.date(entry.atUtc)}{entry.note ? ` · ${entry.note}` : ''}</p>
              </div>
              <span className={`shrink-0 font-medium tabular-nums ${entry.points < 0 ? 'text-rose-700' : 'text-emerald-700'}`}>
                {entry.points > 0 ? '+' : ''}{f.number(entry.points)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Section>
  );
}

// ── their details ────────────────────────────────────────────────────────────

export function PreferencesSection({
  api, keys, me, portalKey, onLanguage,
}: SectionProps & { portalKey: string; onLanguage: (language: Language | null) => void }) {
  const { t } = useT();
  const client = useQueryClient();
  const f = useClubFormats(me.locale, me.currency, me.timeZone);
  const [phone, setPhone] = useState(me.phone ?? '');
  const [savingPhone, setSavingPhone] = useState(false);
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [leaving, setLeaving] = useState(false);

  const savePhone = async (e: FormEvent) => {
    e.preventDefault();
    setSavingPhone(true);
    setPhoneError(null);
    try {
      const saved = await api.updateMe({ phone: phone.trim() === '' ? null : phone.trim() });
      client.setQueryData(keys.me, saved);
      setPhone(saved.phone ?? '');
      toast.success(t('portal.saved'));
    } catch (err) {
      setPhoneError(portalErrorText(err, t));
    } finally {
      setSavingPhone(false);
    }
  };

  const signOutEverywhere = async () => {
    setLeaving(true);
    try {
      await api.signOutEverywhere();
      toast.success(t('portal.signedOutEverywhere'));
      client.removeQueries({ queryKey: keys.all });
      savePortalSession(portalKey, null);
    } catch (err) {
      toast.error(portalErrorText(err, t));
      setLeaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <Section title={t('portal.prefs.contact')}>
        <form onSubmit={(e) => void savePhone(e)} className="grid gap-3 sm:grid-cols-2" noValidate>
          <div className="text-sm">
            <p className="font-medium text-slate-700">{t('portal.prefs.email')}</p>
            <p className="mt-1 break-all text-slate-900">{me.email}</p>
            <p className="mt-1 text-xs text-slate-500">{t('portal.prefs.emailHelp')}</p>
          </div>
          <label className="text-sm font-medium text-slate-700">
            {t('portal.prefs.phone')}
            <input type="tel" autoComplete="tel" maxLength={40} value={phone} onChange={(e) => setPhone(e.target.value)} className={`${inputClass} mt-1`} />
            <span role="alert" className="mt-1 block text-sm font-normal text-red-600">{phoneError}</span>
          </label>
          <div className="sm:col-span-2">
            <button type="submit" className={buttonClass} disabled={savingPhone || phone.trim() === (me.phone ?? '')}>
              {t('portal.prefs.save')}
            </button>
          </div>
        </form>
      </Section>

      <Section title={t('portal.prefs.language')}>
        <LanguageSwitcher showLabel onChosen={onLanguage} className="flex items-center gap-2 text-sm text-slate-600" />
        <p className="mt-2 text-xs text-slate-500">{t('portal.prefs.languageHelp')}</p>
      </Section>

      <ConsentPanel api={api} keys={keys} me={me} />

      {me.household && (
        <Section title={t('portal.prefs.household')}>
          <p className="mb-2 text-sm text-slate-600">{me.household.name}</p>
          <ul className="divide-y divide-slate-100">
            {me.household.people.map((p, i) => (
              <li key={`${p.name}-${i}`} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                <span className="text-slate-900">
                  {p.name}
                  {p.isYou && <span className="text-slate-500"> ({t('portal.prefs.you')})</span>}
                </span>
                <span className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                  {p.isPrimary ? <Badge tone="slate">{t('portal.prefs.primary')}</Badge> : <span>{p.role}</span>}
                  {p.rollsUp && <span>{t('portal.prefs.rollsUp')}</span>}
                  {p.balance != null && p.balance < 0 && <span className="tabular-nums">{t('portal.prefs.owes', { amount: f.money(-p.balance) })}</span>}
                </span>
              </li>
            ))}
          </ul>
        </Section>
      )}

      <Section title={t('portal.signOutEverywhere')}>
        <p className="text-sm text-slate-600">{t('portal.signOutEverywhereHelp')}</p>
        <button type="button" className={`${secondaryButtonClass} mt-3`} disabled={leaving} onClick={() => void signOutEverywhere()}>
          {t('portal.signOutEverywhere')}
        </button>
      </Section>
    </div>
  );
}

function ConsentPanel({ api, keys }: SectionProps) {
  const { t } = useT();
  const client = useQueryClient();
  const consent = useQuery({ queryKey: keys.consent, queryFn: ({ signal }) => timed(signal, (s) => api.consent(s)) });
  const [busy, setBusy] = useState<string | null>(null);

  const stateOf = (channel: string): PortalConsentState['state'] =>
    consent.data?.current.find((c) => c.channel === channel && c.purpose === 'marketing')?.state ?? 'unknown';

  const set = async (channel: string, granted: boolean) => {
    setBusy(channel);
    try {
      client.setQueryData(keys.consent, await api.setConsent(channel, 'marketing', granted));
      toast.success(t('portal.saved'));
    } catch (err) {
      toast.error(portalErrorText(err, t));
    } finally {
      setBusy(null);
    }
  };

  return (
    <Section title={t('portal.prefs.consent')}>
      {consent.isPending ? <Loading /> : consent.isError ? <LoadFailed onRetry={() => void consent.refetch()} /> : (
        <div className="space-y-2">
          {MARKETING_CHANNELS.map((channel) => (
            <label key={channel} className="flex items-center gap-3 text-sm text-slate-800">
              <input
                type="checkbox"
                className="h-4 w-4 rounded border-slate-300"
                checked={stateOf(channel) === 'granted'}
                disabled={busy === channel}
                onChange={(e) => void set(channel, e.target.checked)}
              />
              {t(`portal.prefs.marketing.${channel}` as MessageKey)}
            </label>
          ))}
          <p className="pt-1 text-xs text-slate-500">{t('portal.prefs.serviceNote')}</p>
        </div>
      )}
    </Section>
  );
}
