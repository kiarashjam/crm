// The member portal once signed in: the member's own club account, in the layout the staff
// preview (MemberPortal.tsx) showed, now read from the portal's own API with the member's
// session, in their language.

import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, CalendarHeart, Crown, LogOut, Receipt, Sparkles, UserRound, LayoutDashboard } from 'lucide-react';
import { toast } from 'sonner';
import { portalApi, savePortalSession, type PortalSession } from '@/app/api/portal';
import { LanguageSwitcher } from '@/app/i18n/LanguageSwitcher';
import { asLanguage, usePageLanguage, useT, type MessageKey } from '@/app/i18n';
import { timed } from '@/app/hooks/queries/queryClient';
import {
  LoadFailed, portalErrorText, portalKeys, secondaryButtonClass, useClubFormats, usePortalTitle, type SectionProps,
} from './portalCommon';
import { AccountSection, BookingsSection, EventsSection, PointsSection, PreferencesSection, statusWords } from './PortalSections';

const TABS = ['overview', 'account', 'events', 'bookings', 'points', 'preferences'] as const;
type Tab = (typeof TABS)[number];

const TAB_ICONS: Record<Tab, typeof Receipt> = {
  overview: LayoutDashboard,
  account: Receipt,
  events: CalendarHeart,
  bookings: CalendarClock,
  points: Sparkles,
  preferences: UserRound,
};

export function PortalHome({ portalKey, session }: { portalKey: string; session: PortalSession }) {
  const { t } = useT();
  const client = useQueryClient();
  const api = useMemo(() => portalApi(portalKey, session.token), [portalKey, session.token]);
  const keys = useMemo(() => portalKeys(portalKey, session.memberId), [portalKey, session.memberId]);
  const [search, setSearch] = useSearchParams();
  const tab = (TABS as readonly string[]).includes(search.get('tab') ?? '') ? (search.get('tab') as Tab) : 'overview';
  const me = useQuery({ queryKey: keys.me, queryFn: ({ signal }) => timed(signal, (s) => api.me(s)) });
  // The member's own language; without one, their browser's, then the club's.
  usePageLanguage(me.data?.preferredLanguage ?? session.language ?? null, 'reader');
  usePageLanguage(me.data?.locale ?? null, 'club');
  usePortalTitle(me.data?.clubName ?? session.clubName);
  const [leaving, setLeaving] = useState(false);

  const signOut = async () => {
    setLeaving(true);
    try {
      await api.signOut();
    } catch {
      // Ended here either way: the session is forgotten in this browser.
    }
    client.removeQueries({ queryKey: keys.all });
    savePortalSession(portalKey, null);
  };

  // The member picks a language: the portal shows it at once, and the club writes to them in it.
  const languageChosen = (language: string | null) => {
    const picked = asLanguage(language);
    if (!picked || picked === me.data?.preferredLanguage) return;
    api.updateMe({ preferredLanguage: picked })
      .then((saved) => client.setQueryData(keys.me, saved))
      .catch((err: unknown) => toast.error(portalErrorText(err, t)));
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-50 to-pink-50/30">
      <header className="border-b border-slate-200 bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-3 px-4 py-3">
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-semibold text-slate-900">{me.data?.clubName ?? session.clubName}</p>
            <p className="truncate text-xs text-slate-500">{t('portal.title')}</p>
          </div>
          <LanguageSwitcher onChosen={languageChosen} />
          <button type="button" onClick={() => void signOut()} disabled={leaving} className={secondaryButtonClass}>
            <LogOut className="h-4 w-4" aria-hidden /> {t('portal.signOut')}
          </button>
        </div>
        <nav aria-label={t('portal.nav.label')} className="mx-auto flex max-w-5xl gap-1 overflow-x-auto px-4 pb-2">
          {TABS.map((id) => {
            const Icon = TAB_ICONS[id];
            return (
              <button
                key={id}
                type="button"
                aria-current={tab === id ? 'page' : undefined}
                onClick={() => setSearch(id === 'overview' ? {} : { tab: id }, { replace: true })}
                className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium transition ${
                  tab === id ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'
                }`}
              >
                <Icon className="h-4 w-4" aria-hidden /> {t(`portal.nav.${id}` as MessageKey)}
              </button>
            );
          })}
        </nav>
      </header>
      <main id="main-content" className="mx-auto max-w-5xl px-4 py-6" tabIndex={-1}>
        {me.isPending ? (
          <p aria-busy="true" className="text-sm text-slate-500">{t('common.loading')}</p>
        ) : me.isError ? (
          <LoadFailed onRetry={() => void me.refetch()} />
        ) : tab === 'account' ? (
          <AccountSection api={api} keys={keys} me={me.data} />
        ) : tab === 'events' ? (
          <EventsSection api={api} keys={keys} me={me.data} />
        ) : tab === 'bookings' ? (
          <BookingsSection api={api} keys={keys} me={me.data} />
        ) : tab === 'points' ? (
          <PointsSection api={api} keys={keys} me={me.data} />
        ) : tab === 'preferences' ? (
          <PreferencesSection api={api} keys={keys} me={me.data} portalKey={portalKey} onLanguage={languageChosen} />
        ) : (
          <Overview api={api} keys={keys} me={me.data} onOpen={(id) => setSearch({ tab: id }, { replace: true })} />
        )}
      </main>
    </div>
  );
}

function Overview({ api, keys, me, onOpen }: SectionProps & { onOpen: (tab: Tab) => void }) {
  const { t } = useT();
  const f = useClubFormats(me.locale, me.currency, me.timeZone);
  const account = useQuery({ queryKey: keys.account, queryFn: ({ signal }) => timed(signal, (s) => api.account(s)) });
  const events = useQuery({ queryKey: keys.events, queryFn: ({ signal }) => timed(signal, (s) => api.events(s)) });
  const bookings = useQuery({ queryKey: keys.bookings, queryFn: ({ signal }) => timed(signal, (s) => api.bookings(s)) });
  const points = useQuery({ queryKey: keys.points, queryFn: ({ signal }) => timed(signal, (s) => api.points(s)) });

  const now = Date.now();
  const coming = [
    ...(events.data ?? [])
      .filter((e) => e.myStatus === 'Reserved' || e.myStatus === 'Waitlisted')
      .map((e) => ({ id: `e-${e.id}`, at: e.startAtUtc, label: `${e.coverEmoji} ${e.name}`, tab: 'events' as Tab })),
    ...(bookings.data ?? [])
      .filter((b) => b.mine && Date.parse(b.endAtUtc) > now && (b.status === 'Pending' || b.status === 'Confirmed'))
      .map((b) => ({ id: `b-${b.id}`, at: b.startAtUtc, label: b.resourceName, tab: 'bookings' as Tab })),
  ].sort((a, b) => Date.parse(a.at) - Date.parse(b.at)).slice(0, 6);

  return (
    <div className="space-y-4">
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-start gap-3">
          <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-pink-400 to-orange-400 text-white">
            <Crown className="h-5 w-5" aria-hidden />
          </span>
          <div className="min-w-0">
            <h1 className="text-xl font-semibold text-slate-900">{t('portal.hello', { name: me.firstName })}</h1>
            <p className="text-sm text-slate-600">
              {t('portal.membership', { tier: me.tier })} · {statusWords(t, 'portal.status', me.status)}
            </p>
            <p className="mt-1 text-sm text-slate-600">
              {me.startUndetermined
                ? t('portal.startUndetermined')
                : me.startsOnUtc && Date.parse(me.startsOnUtc) > now
                  ? t('portal.startsOn', { date: f.date(me.startsOnUtc, 'long') })
                  : t('portal.joined', { date: f.date(me.joinedAtUtc, 'long') })}
            </p>
          </div>
        </div>
        {!me.active && <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">{t('portal.notActive')}</p>}
      </section>

      <div className="grid gap-4 md:grid-cols-2">
        <button type="button" onClick={() => onOpen('account')} className="rounded-2xl border border-slate-200 bg-white p-5 text-left shadow-sm hover:border-pink-200">
          <p className="text-sm font-medium text-slate-500">{t('portal.account.balance')}</p>
          {account.isError ? (
            <p className="mt-1 text-sm text-rose-700">{t('portal.loadFailed')}</p>
          ) : account.data ? (
            <p className="mt-1 text-2xl font-semibold text-slate-900">
              {account.data.owed > 0
                ? t('portal.account.owed', { amount: f.money(account.data.owed, account.data.currency) })
                : account.data.credit > 0
                  ? t('portal.account.credit', { amount: f.money(account.data.credit, account.data.currency) })
                  : t('portal.account.settled')}
            </p>
          ) : (
            <p className="mt-1 text-sm text-slate-400">{t('common.loading')}</p>
          )}
        </button>
        <button type="button" onClick={() => onOpen('points')} className="rounded-2xl border border-slate-200 bg-white p-5 text-left shadow-sm hover:border-pink-200">
          <p className="text-sm font-medium text-slate-500">{t('portal.points.title')}</p>
          <p className="mt-1 text-2xl font-semibold text-slate-900">
            {points.data ? t('portal.points.balance', { count: points.data.balance }) : points.isError ? '—' : t('common.loading')}
          </p>
        </button>
      </div>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="mb-3 font-semibold text-slate-900">{t('portal.upcoming')}</h2>
        {events.isError || bookings.isError ? (
          <LoadFailed onRetry={() => { void events.refetch(); void bookings.refetch(); }} />
        ) : coming.length === 0 ? (
          <p className="text-sm text-slate-500">{t('portal.nothingUpcoming')}</p>
        ) : (
          <ul className="divide-y divide-slate-100">
            {coming.map((c) => (
              <li key={c.id}>
                <button type="button" onClick={() => onOpen(c.tab)} className="flex w-full items-center justify-between gap-3 py-2.5 text-left">
                  <span className="truncate font-medium text-slate-900">{c.label}</span>
                  <span className="shrink-0 text-sm text-slate-500">{f.dateTime(c.at)}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
