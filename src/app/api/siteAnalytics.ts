// Site analytics: who visits www.cadencee.ai, where they come from, what they read and do.
// A platform page, not a workspace's: only CadenceE's platform admins may read it, and the
// server answers 403 to everyone else.
//
//   GET /api/platform/me                                   whether the signed-in person is a platform admin
//   GET /api/site-analytics/summary?from=&to=&audience=    the period's figures, against the period before
//   GET /api/site-analytics/live?audience=                 the last 5 minutes
//   GET /api/site-analytics/sessions?day=&limit=&audience= one day's visits, newest first, with their steps
//
// Days are "yyyy-MM-dd" in Europe/Zurich; moments are ISO UTC. The audience is whose events
// are counted (SiteAnalyticsAudience); every read sends it.
//
// Server only: in demo mode there is no site to report on, and nobody is a platform admin.

import { ApiError, authFetchJson, servesDemoData, type RequestOptions } from './apiClient';

/** The time zone the site's days are counted in. */
export const SITE_ANALYTICS_TIME_ZONE = 'Europe/Zurich';

/**
 * Whose events the figures count. The website and the app are one origin, so the tracker
 * sees both: 'visitors' is the website's (what was done signed out, plus every sign-up and
 * login, often recorded just after signing in); 'signed-in' is the app's signed-in use, the
 * owner's own included; 'everyone' is both.
 */
export type SiteAnalyticsAudience = 'visitors' | 'signed-in' | 'everyone';

export const SITE_ANALYTICS_AUDIENCES: readonly SiteAnalyticsAudience[] = ['visitors', 'signed-in', 'everyone'];

/** What the dashboard shows unless asked: the website's visitors. */
export const DEFAULT_SITE_ANALYTICS_AUDIENCE: SiteAnalyticsAudience = 'visitors';

export interface PlatformMe {
  isPlatformAdmin: boolean;
}

/** A period's headline figures. */
export interface SiteAnalyticsTotals {
  visitors: number;
  sessions: number;
  pageViews: number;
  /** 0..1: the share of visits that saw one page and did nothing else. */
  bounceRate: number;
  pagesPerSession: number;
  avgSessionSeconds: number;
  signups: number;
  /** Requests from bots, left out of every other figure. */
  botHits: number;
}

/** One day of the period; every day is present, zeros included. */
export interface SiteAnalyticsDay {
  /** yyyy-MM-dd, Europe/Zurich. */
  date: string;
  visitors: number;
  sessions: number;
  pageViews: number;
  signups: number;
}

/** A page, as top, entry or exit page. */
export interface SiteAnalyticsPageRow {
  path: string;
  views: number;
  visitors: number;
}

/**
 * Where visits came from: google, bing, duckduckgo, linkedin, facebook, instagram, x, ai,
 * email, direct, a utm source, or a referring host.
 */
export interface SiteAnalyticsSourceRow {
  source: string;
  sessions: number;
  visitors: number;
}

export interface SiteAnalyticsReferrerRow {
  host: string;
  sessions: number;
}

export interface SiteAnalyticsCampaignRow {
  campaign: string;
  source: string;
  medium: string;
  sessions: number;
}

/** A device, browser, operating system or language. */
export interface SiteAnalyticsNamedRow {
  name: string;
  sessions: number;
}

/** An event the site sent: cta-signup, signup_started, signup_completed, login, outbound… */
export interface SiteAnalyticsEventRow {
  name: string;
  count: number;
}

export interface SiteAnalyticsSummary {
  /** yyyy-MM-dd, the first day of the period. */
  from: string;
  /** yyyy-MM-dd, the last day of the period (included). */
  to: string;
  totals: SiteAnalyticsTotals;
  /** The equally long period just before. */
  previous: SiteAnalyticsTotals;
  daily: SiteAnalyticsDay[];
  topPages: SiteAnalyticsPageRow[];
  entryPages: SiteAnalyticsPageRow[];
  exitPages: SiteAnalyticsPageRow[];
  sources: SiteAnalyticsSourceRow[];
  referrers: SiteAnalyticsReferrerRow[];
  campaigns: SiteAnalyticsCampaignRow[];
  devices: SiteAnalyticsNamedRow[];
  browsers: SiteAnalyticsNamedRow[];
  os: SiteAnalyticsNamedRow[];
  languages: SiteAnalyticsNamedRow[];
  events: SiteAnalyticsEventRow[];
}

export type SiteAnalyticsLiveEventType = 'page_view' | 'click' | 'event';

export interface SiteAnalyticsLiveEvent {
  /** ISO UTC. */
  at: string;
  type: SiteAnalyticsLiveEventType;
  path: string;
  /** The click's or the event's name: cta-signup, signup_completed… */
  name?: string;
  source: string;
  device: string;
}

/** The last 5 minutes. */
export interface SiteAnalyticsLive {
  visitorsNow: number;
  sessionsNow: number;
  pages: { path: string; visitors: number }[];
  recent: SiteAnalyticsLiveEvent[];
}

export interface SiteAnalyticsStep {
  /** ISO UTC. */
  at: string;
  type: SiteAnalyticsLiveEventType;
  path: string;
  name?: string;
}

/** One visit, with the path it took. */
export interface SiteAnalyticsSession {
  id: string;
  /** ISO UTC. */
  startedAt: string;
  /** ISO UTC. */
  endedAt: string;
  durationSeconds: number;
  source: string;
  referrerHost?: string;
  campaign?: string;
  device: string;
  browser: string;
  language: string;
  signedIn: boolean;
  pageViews: number;
  steps: SiteAnalyticsStep[];
  converted: boolean;
}

/** The days a summary covers, both included; either left out for the server's default (the last 30 days). */
export interface SiteAnalyticsRange {
  from?: string;
  to?: string;
}

/**
 * Whether the signed-in person is a platform admin. Nobody is in demo mode. A refusal
 * (403), or a server that has no such route yet (404), is read as "no": the page and its
 * menu item stay hidden rather than showing an error to everyone else.
 */
export async function getPlatformMe(req?: RequestOptions): Promise<PlatformMe> {
  if (servesDemoData()) return { isPlatformAdmin: false };
  try {
    const me = await authFetchJson<Partial<PlatformMe> | null>('/api/platform/me', { signal: req?.signal });
    return { isPlatformAdmin: me?.isPlatformAdmin === true };
  } catch (err) {
    if (err instanceof ApiError && (err.status === 403 || err.status === 404)) return { isPlatformAdmin: false };
    throw err;
  }
}

function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [name, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(name, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

/** The audience's figures for the period, and for the period before. */
export async function getSiteAnalyticsSummary(
  range: SiteAnalyticsRange = {},
  audience: SiteAnalyticsAudience = DEFAULT_SITE_ANALYTICS_AUDIENCE,
  req?: RequestOptions,
): Promise<SiteAnalyticsSummary> {
  return authFetchJson<SiteAnalyticsSummary>(
    `/api/site-analytics/summary${query({ from: range.from, to: range.to, audience })}`,
    { signal: req?.signal },
  );
}

/** Who of the audience is on the site in the last 5 minutes, and what they did. */
export async function getSiteAnalyticsLive(
  audience: SiteAnalyticsAudience = DEFAULT_SITE_ANALYTICS_AUDIENCE,
  req?: RequestOptions,
): Promise<SiteAnalyticsLive> {
  return authFetchJson<SiteAnalyticsLive>(`/api/site-analytics/live${query({ audience })}`, { signal: req?.signal });
}

/** One day's visits of the audience, newest first, each with all its steps. */
export async function getSiteAnalyticsSessions(
  day: string,
  limit = 50,
  audience: SiteAnalyticsAudience = DEFAULT_SITE_ANALYTICS_AUDIENCE,
  req?: RequestOptions,
): Promise<SiteAnalyticsSession[]> {
  const rows = await authFetchJson<SiteAnalyticsSession[] | null>(
    `/api/site-analytics/sessions${query({ day, limit, audience })}`,
    { signal: req?.signal },
  );
  return Array.isArray(rows) ? rows : [];
}
