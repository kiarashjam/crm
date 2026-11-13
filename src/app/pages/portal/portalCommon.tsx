// What the member portal's pages share: its errors in the member's own language, the frame
// of a page, a failed read with a way to try again, and the club's formats.

import { useEffect, useState, type ReactNode } from 'react';
import {
  PortalError, PORTAL_SESSION_CHANGED, readPortalSession, type PortalApi, type PortalMe, type PortalSession,
} from '@/app/api/portal';
import { dateLocaleFor, makeFormats, useT, type Formats, type I18nValue, type MessageKey } from '@/app/i18n';
import { LanguageSwitcher } from '@/app/i18n/LanguageSwitcher';
import { formatClubMoney } from '@/app/lib/clubMoney';

/** The server's refusals the portal says in the member's language; anything else is shown as the server wrote it. */
const ERROR_WORDS: Record<string, MessageKey> = {
  'Portal.Taken.Conflict': 'portal.error.taken',
  'Portal.Full.Conflict': 'portal.error.full',
  'Portal.AlreadyRegistered.Conflict': 'portal.error.alreadyRegistered',
  'Portal.Changed.Conflict': 'portal.error.changed',
  'Portal.Event.Conflict': 'portal.error.changed',
  'Portal.AlreadyCancelled.Conflict': 'portal.error.alreadyCancelled',
  'Portal.Record.NotFound': 'portal.error.notFound',
  'Portal.Resource.NotFound': 'portal.error.notBookable',
  'Portal.BookingTimes': 'portal.error.bookingTimes',
  'Portal.NotAllowed.TooLate': 'portal.error.tooLate',
  'Portal.NotAllowed.NotActive': 'portal.error.notActive',
  'Portal.NotAllowed.Started': 'portal.error.started',
  'Portal.NotAllowed.EventNotOpen': 'portal.error.notOpen',
  'Portal.PaymentsOff': 'portal.error.paymentsOff',
  'Portal.NothingToPay': 'portal.error.nothingToPay',
  'Portal.Phone': 'portal.error.phone',
  'Portal.TooManyLinks': 'portal.error.tooMany',
};

export function portalErrorText(error: unknown, t: I18nValue['t']): string {
  if (error instanceof PortalError) {
    if (error.status === 0) return t('portal.error.network');
    if (error.status === 429) return t('portal.error.tooMany');
    const key = error.code ? ERROR_WORDS[error.code] : undefined;
    if (key) return t(key);
    if (error.message) return error.message;
  }
  return t('portal.error.network');
}

/** The portal's cached reads, per club and member: one member's never stands in for another's. */
export function portalKeys(portalKey: string, memberId: string) {
  const base = ['member-portal', portalKey, memberId] as const;
  return {
    all: base,
    me: [...base, 'me'] as const,
    account: [...base, 'account'] as const,
    statement: (month: string) => [...base, 'statement', month] as const,
    card: [...base, 'card'] as const,
    events: [...base, 'events'] as const,
    resources: [...base, 'resources'] as const,
    bookings: [...base, 'bookings'] as const,
    consent: [...base, 'consent'] as const,
    points: [...base, 'points'] as const,
  };
}

export type PortalKeys = ReturnType<typeof portalKeys>;

/** What every section of the signed-in portal is given: the member's API, its cache keys, and who they are. */
export interface SectionProps {
  api: PortalApi;
  keys: PortalKeys;
  me: PortalMe;
}

/** The browser tab names the club's portal ("Pavillon 46 · Your club account"), not the app. */
export function usePortalTitle(clubName: string | null | undefined): void {
  const { t } = useT();
  const title = clubName ? `${clubName} · ${t('portal.title')}` : t('portal.title');
  useEffect(() => {
    const was = document.title;
    document.title = title;
    return () => {
      document.title = was;
    };
  }, [title]);
}

/** The session this browser holds for one club's portal, following sign-ins and sign-outs as they happen. */
export function usePortalSession(portalKey: string): PortalSession | null {
  const [session, setSession] = useState<PortalSession | null>(() => readPortalSession(portalKey));
  useEffect(() => {
    const follow = () => setSession(readPortalSession(portalKey));
    follow();
    window.addEventListener(PORTAL_SESSION_CHANGED, follow);
    window.addEventListener('storage', follow);
    return () => {
      window.removeEventListener(PORTAL_SESSION_CHANGED, follow);
      window.removeEventListener('storage', follow);
    };
  }, [portalKey]);
  return session;
}

/** A time zone this browser knows by name, or undefined (the browser's own is used then). */
export function knownZone(timeZone: string | null | undefined): string | undefined {
  if (!timeZone) return undefined;
  try {
    new Intl.DateTimeFormat('en-US', { timeZone });
    return timeZone;
  } catch {
    return undefined;
  }
}

type DateValue = Date | string | number | null | undefined;

export interface ClubFormats extends Formats {
  /** The club's time zone, which every date and time here is shown in. */
  zone: string;
  money: (amount: number, inCurrency?: string) => string;
}

/**
 * Dates in the member's language with the club's region and on the club's clock (an event at
 * 19:00 in Zurich reads 19:00 from anywhere), numbers and money in the club's format.
 */
export function useClubFormats(locale: string | undefined, currency: string | undefined, timeZone?: string | null): ClubFormats {
  const { language } = useT();
  const base: Formats = makeFormats(dateLocaleFor(language, locale ?? null), locale || language);
  const known = knownZone(timeZone);
  const zone = known ?? Intl.DateTimeFormat().resolvedOptions().timeZone;
  const onClock = known ? { timeZone: known } : {};
  return {
    ...base,
    zone,
    date: (value: DateValue, style: 'short' | 'medium' | 'long' | 'full' = 'medium') => base.custom(value, { dateStyle: style, ...onClock }),
    dateTime: (value: DateValue, style: 'short' | 'medium' = 'medium') => base.custom(value, { dateStyle: style, timeStyle: 'short', ...onClock }),
    time: (value: DateValue) => base.custom(value, { timeStyle: 'short', ...onClock }),
    custom: (value: DateValue, options: Intl.DateTimeFormatOptions) => base.custom(value, { ...onClock, ...options }),
    money: (amount: number, inCurrency?: string) => formatClubMoney(amount, inCurrency || currency || 'CHF', locale || undefined),
  };
}

/** A read that failed, and the way to try it again. */
export function LoadFailed({ onRetry, what }: { onRetry: () => void; what?: string }) {
  const { t } = useT();
  return (
    <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">
      <p>{what ?? t('portal.loadFailed')}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-2 rounded-lg bg-white px-3 py-1.5 text-sm font-medium text-rose-800 ring-1 ring-rose-200 hover:bg-rose-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-400"
      >
        {t('common.tryAgain')}
      </button>
    </div>
  );
}

/** The frame of the portal's pages that come before signing in: one card, the language picker above it. */
export function PortalCard({ children }: { children: ReactNode }) {
  return (
    <main className="min-h-screen bg-gradient-to-b from-slate-50 to-pink-50/40 px-4 py-16">
      <div className="mx-auto max-w-md">
        <div className="mb-3 flex justify-end">
          <LanguageSwitcher />
        </div>
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">{children}</div>
      </div>
    </main>
  );
}

export const buttonClass =
  'inline-flex h-10 items-center justify-center gap-2 rounded-xl bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2';

export const secondaryButtonClass =
  'inline-flex h-9 items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-800 hover:bg-slate-50 disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400';

export const inputClass =
  'h-10 w-full rounded-lg border border-slate-200 px-3 text-sm text-slate-900 focus:border-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-200';

/**
 * Opens the club's pay page (or Stripe's) for the member: the server answers with the address,
 * the app's own (/pay/…) or https only.
 */
export function openPayPage(url: string): void {
  const sameApp = url.startsWith(window.location.origin + '/');
  if (!sameApp && !/^https:\/\//i.test(url)) throw new PortalError('The payment page address is not a secure one.', 400);
  window.location.assign(url);
}
