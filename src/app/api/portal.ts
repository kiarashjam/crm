// The member portal, as a member's browser talks to it: the club's sign-in page and its
// emailed links (anonymous), then the member's own account with the member token the link
// gives (Authorization: Bearer). A member token reaches only these endpoints, and the
// staff app's token is never sent here: this file keeps a session of its own.
//
// The session lives in this browser per club (localStorage, keyed by the portal's address),
// so one person in two clubs keeps two. A 401 means the session was ended (signed out
// elsewhere, by the club, or it ran out): it is forgotten and the sign-in page shows again.

import { ApiError, getApiBaseUrl } from './apiClient';

/**
 * A refusal or failure from the portal's API: its status (0 when the server could not be
 * reached) and the server's errorCode (Portal.LinkInvalid, Portal.NotAllowed.TooLate,
 * Module.Off …), for the page's own words. An ApiError, so the app's cache treats it as it
 * treats the staff app's: one more try after a dropped connection or a server error, none
 * after a refusal.
 */
export class PortalError extends ApiError {
  constructor(message: string, status: number, code?: string) {
    super(message, status, undefined, true, code);
    this.name = 'PortalError';
  }
}

export interface PortalSession {
  token: string;
  expiresAtUtc: string;
  memberId: string;
  firstName: string;
  clubName: string;
  language?: string | null;
}

const SESSION_PREFIX = 'cadence.portal.';

/** Said on window when a portal session begins or ends, so every part of the page follows. */
export const PORTAL_SESSION_CHANGED = 'cadence:portal-session';

export function readPortalSession(key: string): PortalSession | null {
  try {
    const raw = localStorage.getItem(SESSION_PREFIX + key);
    if (!raw) return null;
    const session = JSON.parse(raw) as PortalSession;
    if (!session?.token || !session.expiresAtUtc || Date.parse(session.expiresAtUtc) <= Date.now()) {
      localStorage.removeItem(SESSION_PREFIX + key);
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

export function savePortalSession(key: string, session: PortalSession | null): void {
  try {
    if (session) localStorage.setItem(SESSION_PREFIX + key, JSON.stringify(session));
    else localStorage.removeItem(SESSION_PREFIX + key);
  } catch {
    // Private mode or a full quota: the session lasts as long as the page.
  }
  window.dispatchEvent(new Event(PORTAL_SESSION_CHANGED));
}

async function call<T>(path: string, init: RequestInit & { token?: string | null; portalKey?: string } = {}): Promise<T> {
  const base = getApiBaseUrl();
  if (!base) throw new PortalError('VITE_API_URL is not set', 0);
  const { token, portalKey, ...rest } = init;
  const headers = new Headers(rest.headers);
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (typeof rest.body === 'string' && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  let res: Response;
  try {
    res = await fetch(`${base}${path}`, { ...rest, headers });
  } catch {
    throw new PortalError('Could not reach the server', 0);
  }
  if (res.status === 401 && token && portalKey) savePortalSession(portalKey, null);
  const text = await res.text();
  if (!res.ok) {
    let detail = '';
    let code: string | undefined;
    try {
      const body = JSON.parse(text) as { detail?: string; title?: string; errorCode?: string };
      detail = body?.detail || body?.title || '';
      code = body?.errorCode;
    } catch {
      // No problem body: the status says enough.
    }
    throw new PortalError(detail || res.statusText || `HTTP ${res.status}`, res.status, code);
  }
  return (text ? JSON.parse(text) : undefined) as T;
}

// ── the club's sign-in page ──────────────────────────────────────────────────

export interface PortalClub {
  clubName: string;
  open: boolean;
  locale: string;
  language?: string | null;
}

export const getPortalClub = (key: string, signal?: AbortSignal) =>
  call<PortalClub>(`/api/public/portal/${encodeURIComponent(key)}`, { signal });

/** Asks for a sign-in link; the answer is the same whether or not the address is a member's. */
export const requestPortalLink = (key: string, email: string) =>
  call<void>(`/api/public/portal/${encodeURIComponent(key)}/sign-in-link`, { method: 'POST', body: JSON.stringify({ email }) });

/** Uses a sign-in link once, and keeps the session it opens in this browser. */
export async function redeemPortalLink(key: string, token: string): Promise<PortalSession> {
  const session = await call<PortalSession>(`/api/public/portal/${encodeURIComponent(key)}/sign-in`, {
    method: 'POST',
    body: JSON.stringify({ token }),
  });
  savePortalSession(key, session);
  return session;
}

// ── the member's own ─────────────────────────────────────────────────────────

export interface PortalHouseholdPerson {
  name: string;
  role: string;
  isPrimary: boolean;
  isYou: boolean;
  rollsUp: boolean;
  balance?: number | null;
}

export interface PortalMe {
  memberId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string | null;
  preferredLanguage?: string | null;
  tier: string;
  status: string;
  active: boolean;
  joinedAtUtc: string;
  renewsAtUtc: string;
  startsOnUtc?: string | null;
  startUndetermined: boolean;
  duesAmount: number;
  duesFrequency: string;
  clubName: string;
  currency: string;
  locale: string;
  timeZone: string;
  memberCancelHours: number;
  paysOnline: boolean;
  household?: { name: string; isPrimary: boolean; primaryName?: string | null; people: PortalHouseholdPerson[] } | null;
}

export interface PortalCharge {
  id: string;
  description: string;
  kind: string;
  amount: number;
  paidAmount: number;
  outstanding: number;
  currency: string;
  status: string;
  postedAtUtc: string;
  dueAtUtc?: string | null;
  incurredByName?: string | null;
}

export interface PortalAccount {
  currency: string;
  balance: number;
  owed: number;
  credit: number;
  openCharges: PortalCharge[];
  firstMonth: string;
  currentMonth: string;
  billsGoToPrimary: boolean;
  primaryName?: string | null;
}

export interface PortalStatementLine {
  atUtc: string;
  entryType: string;
  description: string;
  charge: number;
  payment: number;
  owed: number;
  incurredByName?: string | null;
}

export interface PortalStatement {
  month: string;
  currency: string;
  openingOwed: number;
  closingOwed: number;
  totalCharges: number;
  totalPayments: number;
  lines: PortalStatementLine[];
}

export interface PortalCard {
  paysOnline: boolean;
  card?: { type: string; brand?: string | null; last4?: string | null; expMonth?: number | null; expYear?: number | null } | null;
  autoPayDues: boolean;
}

export interface PortalEvent {
  id: string;
  name: string;
  description?: string | null;
  location?: string | null;
  startAtUtc: string;
  endAtUtc: string;
  coverEmoji: string;
  memberPrice: number;
  currency: string;
  seatsLeft?: number | null;
  myStatus: 'none' | 'Reserved' | 'Waitlisted' | 'CheckedIn' | 'Cancelled' | 'NoShow';
  myRegistrationId?: string | null;
  waitlistPosition?: number | null;
  canCancel: boolean;
  householdComing: string[];
}

export interface PortalResource {
  id: string;
  name: string;
  kind: string;
  capacity: number;
  description?: string | null;
  depositAmount: number;
  currency: string;
  confirmedAtOnce: boolean;
}

export interface PortalBooking {
  id: string;
  resourceId: string;
  resourceName: string;
  partySize: number;
  startAtUtc: string;
  endAtUtc: string;
  status: string;
  depositAmount: number;
  depositState: string;
  currency: string;
  notes?: string | null;
  mine: boolean;
  forName?: string | null;
  canCancel: boolean;
}

export interface PortalConsentState {
  channel: 'email' | 'sms' | 'post';
  purpose: 'marketing' | 'service';
  state: 'granted' | 'withdrawn' | 'unknown';
  recordedAtUtc?: string | null;
  source?: string | null;
}

export interface PortalPoints {
  balance: number;
  history: { atUtc: string; kind: string; reason: string; points: number; note?: string | null }[];
}

/** The member's own portal API, for one club's session. */
export function portalApi(key: string, token: string) {
  const member = <T,>(path: string, init: RequestInit = {}) => call<T>(`/api/portal${path}`, { ...init, token, portalKey: key });
  const post = <T,>(path: string, body?: unknown) =>
    member<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) });
  return {
    me: (signal?: AbortSignal) => member<PortalMe>('/me', { signal }),
    updateMe: (changes: { phone?: string | null; preferredLanguage?: string | null }) =>
      member<PortalMe>('/me', { method: 'PATCH', body: JSON.stringify(changes) }),
    account: (signal?: AbortSignal) => member<PortalAccount>('/account', { signal }),
    statement: (month: string, signal?: AbortSignal) => member<PortalStatement>(`/statements/${encodeURIComponent(month)}`, { signal }),
    statementPdf: (month: string, household = false) => pdf(key, token, `/api/portal/statements/${encodeURIComponent(month)}/${household ? 'household-pdf' : 'pdf'}`),
    card: (signal?: AbortSignal) => member<PortalCard>('/card', { signal }),
    setUpCard: (autoPay: boolean) => post<{ url: string }>('/card/setup', { autoPay }),
    removeCard: () => member<PortalCard>('/card', { method: 'DELETE' }),
    pay: (chargeIds?: string[]) => post<{ url: string }>('/pay', chargeIds && chargeIds.length > 0 ? { chargeIds } : {}),
    events: (signal?: AbortSignal) => member<PortalEvent[]>('/events', { signal }),
    register: (eventId: string) => post<PortalEvent>(`/events/${eventId}/register`),
    cancelRegistration: (registrationId: string) => post<PortalEvent>(`/registrations/${registrationId}/cancel`),
    resources: (signal?: AbortSignal) => member<PortalResource[]>('/resources', { signal }),
    bookings: (signal?: AbortSignal) => member<PortalBooking[]>('/reservations', { signal }),
    book: (booking: { resourceId: string; startAtUtc: string; endAtUtc: string; partySize: number; notes?: string }) =>
      post<PortalBooking>('/reservations', booking),
    cancelBooking: (reservationId: string) => post<PortalBooking>(`/reservations/${reservationId}/cancel`),
    consent: (signal?: AbortSignal) => member<{ current: PortalConsentState[] }>('/consent', { signal }),
    setConsent: (channel: string, purpose: string, granted: boolean) =>
      member<{ current: PortalConsentState[] }>('/consent', { method: 'PUT', body: JSON.stringify({ channel, purpose, granted }) }),
    points: (signal?: AbortSignal) => member<PortalPoints>('/points', { signal }),
    signOut: () => post<void>('/sign-out'),
    signOutEverywhere: () => post<{ ended: number }>('/sign-out-everywhere'),
  };
}

export type PortalApi = ReturnType<typeof portalApi>;

/** A statement PDF, fetched with the member token and handed to the browser to save. */
async function pdf(key: string, token: string, path: string): Promise<void> {
  const base = getApiBaseUrl();
  if (!base) throw new PortalError('VITE_API_URL is not set', 0);
  let res: Response;
  try {
    res = await fetch(`${base}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  } catch {
    throw new PortalError('Could not reach the server', 0);
  }
  if (res.status === 401) savePortalSession(key, null);
  if (!res.ok) {
    let detail = '';
    let code: string | undefined;
    try {
      const body = (await res.json()) as { detail?: string; errorCode?: string };
      detail = body?.detail ?? '';
      code = body?.errorCode;
    } catch {
      // No body.
    }
    throw new PortalError(detail || `HTTP ${res.status}`, res.status, code);
  }
  const blob = await res.blob();
  const disposition = res.headers.get('Content-Disposition') ?? '';
  const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] ?? 'statement.pdf';
  const url = URL.createObjectURL(blob);
  try {
    const a = document.createElement('a');
    a.href = url;
    a.download = decodeURIComponent(name);
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
