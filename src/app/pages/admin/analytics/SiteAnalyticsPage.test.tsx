// Admin, Site analytics: shown to platform admins only; the headline figures with their
// change against the period before; the growth chart with zero days and its summary in
// words; Live, read again every 15 seconds; sources, pages, the funnel's rates, journeys
// opened step by step, the period's days in the requests, the audience (the website's
// visitors unless picked) in every read and in the address, empty states and a failed read.

import type { ReactElement } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import type {
  SiteAnalyticsLive, SiteAnalyticsSession, SiteAnalyticsSummary, SiteAnalyticsTotals,
} from '@/app/api/siteAnalytics';
import { SITE_ANALYTICS_LIVE_REFRESH_MS } from '@/app/hooks/queries/siteAnalytics';
import { addDays, todayInZurich } from './range';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
// happy-dom lays nothing out, so a responsive chart would measure 0×0 and draw nothing: give
// it a size, and the chart is drawn as in a browser.
vi.mock('recharts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('recharts')>();
  const { cloneElement } = await import('react');
  return {
    ...actual,
    ResponsiveContainer: ({ children }: { children: ReactElement<{ width?: number; height?: number }> }) => cloneElement(children, { width: 640, height: 260 }),
  };
});
// The page's own content is under test; the app's header has its own reads and tests.
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const { default: SiteAnalyticsPage } = await import('./SiteAnalyticsPage');

const BASE = 'http://api.test';
const today = todayInZurich();

const totals = (over: Partial<SiteAnalyticsTotals> = {}): SiteAnalyticsTotals => ({
  visitors: 0, sessions: 0, pageViews: 0, bounceRate: 0, pagesPerSession: 0, avgSessionSeconds: 0, signups: 0, botHits: 0, ...over,
});

/** Every day from `from` to `to`, with visitors on the days given and zeros on the rest. */
function days(from: string, to: string, visitors: Record<string, number> = {}) {
  const out = [];
  for (let d = from; d <= to; d = addDays(d, 1)) {
    const v = visitors[d] ?? 0;
    out.push({ date: d, visitors: v, sessions: v, pageViews: v * 2, signups: 0 });
  }
  return out;
}

function summaryFor(from: string, to: string, over: Partial<SiteAnalyticsSummary> = {}): SiteAnalyticsSummary {
  return {
    from,
    to,
    totals: totals({ visitors: 142, sessions: 170, pageViews: 300, bounceRate: 0.42, pagesPerSession: 1.8, avgSessionSeconds: 95, signups: 3, botHits: 12 }),
    previous: totals({ visitors: 103, sessions: 120, pageViews: 400, bounceRate: 0.5, pagesPerSession: 3.3, avgSessionSeconds: 95, signups: 0, botHits: 4 }),
    daily: days(from, to, { [addDays(to, -3)]: 12, [addDays(to, -1)]: 4, [to]: 2 }),
    topPages: [{ path: '/', views: 120, visitors: 90 }, { path: '/features/club-crm', views: 40, visitors: 31 }],
    entryPages: [{ path: '/blog/why-clubs-need-a-crm', views: 22, visitors: 20 }],
    exitPages: [{ path: '/pricing', views: 30, visitors: 28 }],
    sources: [{ source: 'google', sessions: 60, visitors: 52 }, { source: 'linkedin', sessions: 25, visitors: 21 }, { source: 'direct', sessions: 20, visitors: 19 }],
    referrers: [{ host: 'www.linkedin.com', sessions: 25 }],
    campaigns: [{ campaign: 'autumn-launch', source: 'linkedin', medium: 'social', sessions: 9 }],
    devices: [{ name: 'desktop', sessions: 110 }, { name: 'mobile', sessions: 60 }],
    browsers: [{ name: 'Chrome', sessions: 100 }],
    os: [{ name: 'macOS', sessions: 80 }],
    languages: [{ name: 'de-CH', sessions: 70 }],
    events: [{ name: 'cta-signup', count: 20 }, { name: 'signup_started', count: 8 }, { name: 'signup_completed', count: 3 }],
    ...over,
  };
}

const emptySummary = (from: string, to: string): SiteAnalyticsSummary => summaryFor(from, to, {
  totals: totals(), previous: totals(), daily: days(from, to), topPages: [], entryPages: [], exitPages: [], sources: [],
  referrers: [], campaigns: [], devices: [], browsers: [], os: [], languages: [], events: [],
});

const ago = (s: number) => new Date(Date.now() - s * 1000).toISOString();
const liveNow = (): SiteAnalyticsLive => ({
  visitorsNow: 3,
  sessionsNow: 3,
  pages: [{ path: '/pricing', visitors: 2 }, { path: '/features/club-crm', visitors: 1 }],
  recent: [
    { at: ago(3), type: 'page_view', path: '/pricing', source: 'google', device: 'desktop' },
    { at: ago(40), type: 'click', path: '/', name: 'cta-signup', source: 'linkedin', device: 'mobile' },
    { at: ago(180), type: 'event', path: '/signup', name: 'signup_completed', source: 'direct', device: 'desktop' },
  ],
});

const visit = (): SiteAnalyticsSession => ({
  id: 's1',
  startedAt: `${today}T08:00:00Z`,
  endedAt: `${today}T08:03:05Z`,
  durationSeconds: 185,
  source: 'linkedin',
  referrerHost: 'www.linkedin.com',
  device: 'mobile',
  browser: 'Safari',
  language: 'de-CH',
  signedIn: false,
  pageViews: 3,
  converted: true,
  steps: [
    { at: `${today}T08:00:00Z`, type: 'page_view', path: '/' },
    { at: `${today}T08:00:40Z`, type: 'page_view', path: '/features/club-crm' },
    { at: `${today}T08:01:30Z`, type: 'click', path: '/features/club-crm', name: 'cta-signup' },
    { at: `${today}T08:02:10Z`, type: 'page_view', path: '/login' },
  ],
});

let me: () => Response;
let summary: (from: string, to: string) => Response;
let live: () => Response;
let sessions: (day: string) => Response;
let summaryAsked: URLSearchParams[];
let liveReads: number;
let liveAsked: URLSearchParams[];
let sessionDays: string[];
let sessionsAsked: URLSearchParams[];

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-admin', name: 'Kia', email: 'kia@cadencee.ai' }));
  localStorage.setItem('aci_token', 'token');
  me = () => HttpResponse.json({ isPlatformAdmin: true });
  summary = (from, to) => HttpResponse.json(summaryFor(from, to));
  live = () => HttpResponse.json(liveNow());
  sessions = () => HttpResponse.json([visit()]);
  summaryAsked = [];
  liveReads = 0;
  liveAsked = [];
  sessionDays = [];
  sessionsAsked = [];
  server.use(
    http.get(`${BASE}/api/platform/me`, () => me()),
    http.get(`${BASE}/api/site-analytics/summary`, ({ request }) => {
      const params = new URL(request.url).searchParams;
      summaryAsked.push(params);
      const to = params.get('to') ?? today;
      return summary(params.get('from') ?? addDays(to, -29), to);
    }),
    http.get(`${BASE}/api/site-analytics/live`, ({ request }) => {
      liveReads++;
      liveAsked.push(new URL(request.url).searchParams);
      return live();
    }),
    http.get(`${BASE}/api/site-analytics/sessions`, ({ request }) => {
      const params = new URL(request.url).searchParams;
      sessionsAsked.push(params);
      const day = params.get('day') ?? '';
      sessionDays.push(day);
      return sessions(day);
    }),
  );
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllEnvs();
});

/** The address's query string, as the page left it. */
function Where() {
  return <span data-testid="location">{useLocation().search}</span>;
}

const show = (url = '/admin/analytics') => render(withQueries(
  <MemoryRouter initialEntries={[url]}>
    <Routes>
      <Route path="/admin/analytics" element={<SiteAnalyticsPage />} />
    </Routes>
    <Where />
  </MemoryRouter>,
));

const loaded = () => screen.findByRole('list', { name: 'Key figures' });

/** A headline figure's card, found by the label it starts with. */
function kpiCard(name: string): HTMLElement {
  const card = within(screen.getByRole('list', { name: 'Key figures' })).getAllByRole('listitem')
    .find((li) => li.firstElementChild?.firstElementChild?.textContent === name);
  if (!card) throw new Error(`No key figure called ${name}`);
  return card;
}

describe('who may see it', () => {
  it('tells someone who is not a platform admin, politely, and reads none of the figures', async () => {
    me = () => HttpResponse.json({ isPlatformAdmin: false });
    show();

    expect(await screen.findByRole('heading', { name: 'Site analytics is not available' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to the dashboard' })).toHaveAttribute('href', '/dashboard');
    expect(summaryAsked).toHaveLength(0);
    expect(liveReads).toBe(0);
  });

  it('reads a refusal of /api/platform/me as "not an admin", not as an error', async () => {
    me = () => HttpResponse.json({ title: 'Forbidden' }, { status: 403 });
    show();

    expect(await screen.findByRole('heading', { name: 'Site analytics is not available' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows a platform admin the dashboard, with the privacy note and the bots left out', async () => {
    show();

    expect(await screen.findByRole('heading', { level: 1, name: /Site analytics · www\.cadencee\.ai/ })).toBeInTheDocument();
    await loaded();
    expect(screen.getByTestId('privacy-note')).toHaveTextContent(
      'Cookieless, privacy-friendly analytics. Visitors are counted per day with a hash that changes daily; IPs are never stored. Bots excluded (12 bot hits).',
    );
  });
});

describe('the headline figures', () => {
  it('show each value with its change against the period before: up, down, in points, and "new"', async () => {
    show();
    await loaded();
    const card = kpiCard;

    expect(within(card('Visitors')).getByTestId('kpi-visitors')).toHaveTextContent('142');
    expect(within(card('Visitors')).getByText('+38%')).toBeInTheDocument();
    expect(within(card('Visitors')).getByText('up 38% on the previous 30 days')).toBeInTheDocument();

    expect(within(card('Page views')).getByText('−25%')).toBeInTheDocument();
    expect(within(card('Page views')).getByText('down 25% on the previous 30 days')).toBeInTheDocument();
    expect(within(card('Page views')).getByText('−25%').closest('span[title]')).toHaveClass('text-rose-700');

    expect(within(card('Sign-ups')).getByText('New')).toBeInTheDocument();
    expect(within(card('Sign-ups')).getByText('new: none in the previous 30 days')).toBeInTheDocument();

    // Fewer people leaving after one page is good news, so it is green though it went down.
    expect(within(card('Bounce rate')).getByTestId('kpi-bounceRate')).toHaveTextContent('42%');
    expect(within(card('Bounce rate')).getByText('−8 pts').closest('span[title]')).toHaveClass('text-emerald-700');

    expect(within(card('Avg. visit')).getByTestId('kpi-avgSessionSeconds')).toHaveTextContent('1m 35s');
  });
});

describe('the growth chart', () => {
  it('draws a period of mostly empty days, and says what it shows in words', async () => {
    show();
    await loaded();

    expect(screen.getByTestId('growth-callout')).toHaveTextContent('+38%visitors vs the previous 30 days');
    expect(screen.getByTestId('best-day')).toHaveTextContent(/Best day: .+ · 12 visitors/);
    const summaryText = screen.getByTestId('growth-summary').textContent ?? '';
    expect(summaryText).toContain('142 visitors and 300 page views.');
    expect(summaryText).toContain('Visitors up 38% on the previous 30 days.');
    expect(summaryText).toMatch(/Best day: .+, with 12 visitors\./);
    expect(summaryText).toContain('27 days without a visitor.');
    const chart = screen.getByRole('img', { name: 'Daily visitors and page views' });
    expect(chart).toHaveAccessibleDescription(summaryText);

    // Drawn: the visitors' area, the page views' and the 7-day average's lines, on one
    // count axis that starts at 0.
    expect(chart.querySelector('.recharts-area-area')).not.toBeNull();
    expect(chart.querySelectorAll('.recharts-line-curve')).toHaveLength(2);
    const ticks = [...chart.querySelectorAll('.recharts-yAxis .recharts-cartesian-axis-tick-value')].map((t) => t.textContent);
    expect(ticks[0]).toBe('0');
  });

  it('shows the days as a table on request', async () => {
    const user = userEvent.setup();
    show();
    await loaded();

    await user.click(screen.getByRole('button', { name: 'Show table' }));
    const table = screen.getByRole('table', { name: 'Visitors and page views per day' });
    expect(within(table).getAllByRole('row')).toHaveLength(31);
  });

  it('says so when no day had a visitor yet', async () => {
    summary = (from, to) => HttpResponse.json(emptySummary(from, to));
    show();
    await loaded();

    expect(screen.getByTestId('growth-summary')).toHaveTextContent('No day had a visitor yet. 30 days without a visitor.');
    expect(screen.queryByTestId('best-day')).not.toBeInTheDocument();
    // A flat line on the baseline, under an axis that still reads 0 to 4: no zero-height
    // chart, no axis of zeros.
    const chart = screen.getByRole('img', { name: 'Daily visitors and page views' });
    expect(chart.querySelector('.recharts-area-curve')).not.toBeNull();
    const ticks = [...chart.querySelectorAll('.recharts-yAxis .recharts-cartesian-axis-tick-value')].map((t) => t.textContent);
    expect(ticks).toEqual(['0', '1', '2', '3', '4']);
  });
});

describe('Live now', () => {
  it('shows who is on the site, what they read and what they just did, in the header too', async () => {
    show();
    const card = await screen.findByRole('region', { name: 'Live now' });

    expect(await within(card).findByTestId('live-visitors')).toHaveTextContent('3');
    expect(screen.getByRole('link', { name: 'Live: 3 visitors in the last 5 minutes' })).toHaveAttribute('href', '#live');
    const reading = within(card).getByRole('list', { name: 'Pages being read now' });
    expect(within(reading).getByText('/pricing')).toBeInTheDocument();

    const feed = within(card).getByRole('list', { name: 'Recent activity' });
    const items = within(feed).getAllByRole('listitem');
    expect(items[0]).toHaveTextContent('Viewed /pricing');
    expect(items[0]).toHaveTextContent('just now');
    expect(items[1]).toHaveTextContent('Clicked cta-signup on /');
    expect(items[1]).toHaveTextContent('40 s ago');
    expect(items[1]).toHaveTextContent('LinkedIn');
    expect(items[2]).toHaveTextContent('Sign-up completed on /signup');
    expect(items[2]).toHaveTextContent('3 min ago');
  });

  it('is read again every 15 seconds', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    show();
    const card = await screen.findByRole('region', { name: 'Live now' });
    expect(await within(card).findByTestId('live-visitors')).toHaveTextContent('3');
    expect(liveReads).toBe(1);
    expect(SITE_ANALYTICS_LIVE_REFRESH_MS).toBe(15_000);

    live = () => HttpResponse.json({ ...liveNow(), visitorsNow: 5 });
    await act(async () => { await vi.advanceTimersByTimeAsync(15_000); });

    await waitFor(() => expect(within(card).getByTestId('live-visitors')).toHaveTextContent('5'));
    expect(liveReads).toBe(2);
  });
});

describe('the lists', () => {
  it('rank the sources with their visits and visitors, and the referrers and campaigns', async () => {
    show();
    await loaded();

    const sources = screen.getByRole('list', { name: 'Sources' });
    const rows = within(sources).getAllByRole('listitem');
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent(/Google\s*60 visits\s*52 visitors$/);
    expect(rows[1]).toHaveTextContent(/LinkedIn\s*25 visits\s*21 visitors$/);
    expect(rows[2]).toHaveTextContent(/Direct\s*20 visits\s*19 visitors$/);
    expect(within(screen.getByRole('list', { name: 'Referrers' })).getByText('www.linkedin.com')).toBeInTheDocument();
    expect(within(screen.getByRole('table', { name: 'Campaigns' })).getByText('autumn-launch')).toBeInTheDocument();
  });

  it('rank the top pages, and show the entry and exit pages on their tabs', async () => {
    const user = userEvent.setup();
    show();
    await loaded();

    const top = screen.getByRole('list', { name: 'Top pages' });
    expect(within(top).getAllByRole('listitem').map((r) => r.textContent)).toEqual([
      '/120 views90 visitors',
      '/features/club-crm40 views31 visitors',
    ]);

    await user.click(screen.getByRole('tab', { name: 'Entry pages' }));
    expect(within(await screen.findByRole('list', { name: 'Entry pages' })).getByText('/blog/why-clubs-need-a-crm')).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'Exit pages' }));
    expect(within(await screen.findByRole('list', { name: 'Exit pages' })).getByText('/pricing')).toBeInTheDocument();
  });
});

describe('the sign-up funnel', () => {
  it('gives each step its share of the step before, and the visitors who signed up', async () => {
    summary = (from, to) => HttpResponse.json(summaryFor(from, to, {
      totals: totals({ visitors: 200, sessions: 230, pageViews: 500, signups: 10 }),
      events: [{ name: 'cta-signup', count: 40 }, { name: 'signup_started', count: 20 }, { name: 'signup_completed', count: 10 }, { name: 'login', count: 4 }],
    }));
    show();
    await loaded();

    const funnel = screen.getByRole('list', { name: 'Sign-up funnel' });
    expect(within(funnel).getByTestId('funnel-visitors')).toHaveTextContent('Visitors200');
    expect(within(funnel).getByTestId('funnel-cta-signup-step')).toHaveTextContent('20%');
    expect(within(funnel).getByTestId('funnel-signup_started-step')).toHaveTextContent('50%');
    expect(within(funnel).getByTestId('funnel-signup_completed-step')).toHaveTextContent('50%');
    expect(within(funnel).getByTestId('funnel-signup_completed-visitors')).toHaveTextContent('5%');
    expect(screen.getByTestId('funnel-overall')).toHaveTextContent('5% of visitors signed up (10 of 200).');
    expect(within(screen.getByRole('list', { name: 'Events' })).getByText('Logged in')).toBeInTheDocument();
  });
});

describe('journeys', () => {
  it('lists the last day\'s visits and opens one to show its path, step by step', async () => {
    const user = userEvent.setup();
    show();
    await loaded();

    const list = await screen.findByRole('list', { name: /^Visits on / });
    expect(sessionDays).toEqual([today]);
    const row = within(list).getByRole('button', { expanded: false });
    expect(row).toHaveTextContent('LinkedIn');
    expect(row).toHaveTextContent('Converted');
    expect(row).toHaveTextContent('3m 05s');
    expect(row).toHaveTextContent('3 pages');
    expect(row).toHaveTextContent('/ → /features/club-crm → click cta-signup → /login');

    await user.click(row);
    expect(row).toHaveAttribute('aria-expanded', 'true');
    const path = within(list).getByRole('list', { name: /^Path of the visit at / });
    expect(within(path).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      '/+0:00',
      '/features/club-crm+0:40',
      'click cta-signup on /features/club-crm+1:30',
      '/login+2:10',
    ]);
  });

  it('reads the day before on request', async () => {
    const user = userEvent.setup();
    show();
    await screen.findByRole('list', { name: /^Visits on / });

    await user.click(screen.getByRole('button', { name: 'The day before' }));
    await waitFor(() => expect(sessionDays).toEqual([today, addDays(today, -1)]));
  });
});

describe('the period', () => {
  it('asks for the last 30 days, then for the period picked', async () => {
    const user = userEvent.setup();
    show();
    await loaded();
    const asked = () => summaryAsked.map((p) => `${p.get('from')}..${p.get('to')}`);
    expect(asked()).toEqual([`${addDays(today, -29)}..${today}`]);

    await user.click(screen.getByRole('button', { name: '7 days' }));
    await waitFor(() => expect(asked()).toContain(`${addDays(today, -6)}..${today}`));
    expect(screen.getByRole('button', { name: '7 days' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('period')).toHaveTextContent(/ · vs the previous 7 days$/);

    await user.click(screen.getByRole('button', { name: 'Today' }));
    await waitFor(() => expect(asked()).toContain(`${today}..${today}`));
    expect(screen.getByTestId('period')).toHaveTextContent(/ · vs yesterday$/);
    expect(await screen.findByText('Pick 7 days or more to see the trend.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '90 days' }));
    await waitFor(() => expect(asked()).toContain(`${addDays(today, -89)}..${today}`));
  });

  it('asks for two days picked', async () => {
    const user = userEvent.setup();
    show();
    await loaded();

    await user.click(screen.getByRole('button', { name: 'Custom' }));
    const from = screen.getByRole('textbox', { name: 'From' });
    const to = screen.getByRole('textbox', { name: 'To' });
    await user.clear(from);
    await user.type(from, '2026-10-01');
    await user.clear(to);
    await user.type(to, '2026-10-14');

    await waitFor(() => expect(summaryAsked.some((p) => p.get('from') === '2026-10-01' && p.get('to') === '2026-10-14')).toBe(true));
  });

  it('opens on the period in the address', async () => {
    show('/admin/analytics?range=7d');
    await loaded();
    expect(summaryAsked.map((p) => p.get('from'))).toEqual([addDays(today, -6)]);
  });
});

describe('the audience', () => {
  const audiences = (asked: URLSearchParams[]) => asked.map((p) => p.get('audience'));

  it("shows the website's visitors by default, in every read, and says so", async () => {
    show();
    await loaded();
    await screen.findByRole('list', { name: /^Visits on / });

    const group = screen.getByRole('group', { name: 'Audience' });
    expect(within(group).getAllByRole('button').map((b) => b.textContent)).toEqual(['Website visitors', 'Signed-in', 'Everyone']);
    expect(within(group).getByRole('button', { name: 'Website visitors' })).toHaveAttribute('aria-pressed', 'true');
    expect(within(group).getByRole('button', { name: 'Signed-in' })).toHaveAttribute('aria-pressed', 'false');
    expect(within(group).getByRole('button', { name: 'Website visitors' })).toHaveAccessibleDescription(/Your own use of the CRM is left out\./);

    expect(audiences(summaryAsked)).toEqual(['visitors']);
    expect(audiences(liveAsked)).toEqual(['visitors']);
    expect(audiences(sessionsAsked)).toEqual(['visitors']);
    expect(screen.getByTestId('kpi-visitors-hint')).toHaveTextContent('website visitors, once a day');
    expect(screen.getByTestId('kpi-sessions-hint')).toHaveTextContent('visits to the website');
    expect(screen.getByRole('region', { name: 'Live now' })).toHaveTextContent('Website visitors in the last 5 minutes');
    expect(screen.getByTestId('location')).toHaveTextContent(/^$/);
  });

  it('asks for the audience picked in the summary, Live and the journeys, and keeps it in the address', async () => {
    const user = userEvent.setup();
    show('/admin/analytics?range=7d');
    await loaded();
    await screen.findByRole('list', { name: /^Visits on / });

    await user.click(screen.getByRole('button', { name: 'Signed-in' }));
    await waitFor(() => expect(audiences(summaryAsked)).toEqual(['visitors', 'signed-in']));
    await waitFor(() => expect(audiences(liveAsked)).toContain('signed-in'));
    await waitFor(() => expect(audiences(sessionsAsked)).toContain('signed-in'));
    // The period stays as it was.
    expect(summaryAsked[1]?.get('from')).toBe(addDays(today, -6));
    expect(screen.getByRole('button', { name: 'Signed-in' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Website visitors' })).toHaveAttribute('aria-pressed', 'false');
    await waitFor(() => expect(screen.getByTestId('kpi-visitors-hint')).toHaveTextContent('signed-in people, once a day'));
    expect(screen.getByTestId('location')).toHaveTextContent('?range=7d&audience=signed-in');

    await user.click(screen.getByRole('button', { name: 'Everyone' }));
    await waitFor(() => expect(audiences(summaryAsked)).toEqual(['visitors', 'signed-in', 'everyone']));
    await waitFor(() => expect(audiences(liveAsked)).toContain('everyone'));
    await waitFor(() => expect(screen.getByTestId('kpi-visitors-hint')).toHaveTextContent('everyone, once a day'));
    expect(screen.getByTestId('location')).toHaveTextContent('?range=7d&audience=everyone');

    // Back to the website's visitors: the address needs no parameter for them.
    await user.click(screen.getByRole('button', { name: 'Website visitors' }));
    await waitFor(() => expect(screen.getByTestId('kpi-visitors-hint')).toHaveTextContent('website visitors, once a day'));
    expect(screen.getByTestId('location')).toHaveTextContent(/^\?range=7d$/);
  });

  it('opens on the audience in the address', async () => {
    show('/admin/analytics?audience=everyone');
    await loaded();
    expect(audiences(summaryAsked)).toEqual(['everyone']);
    expect(screen.getByRole('button', { name: 'Everyone' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('kpi-pageViews-hint')).toHaveTextContent('all pages opened');
  });

  it("reads an audience it does not know as the website's visitors", async () => {
    show('/admin/analytics?audience=robots');
    await loaded();
    expect(audiences(summaryAsked)).toEqual(['visitors']);
    expect(screen.getByRole('button', { name: 'Website visitors' })).toHaveAttribute('aria-pressed', 'true');
  });
});

describe('empty and failed', () => {
  it('says kindly, in every section, that nobody came yet', async () => {
    summary = (from, to) => HttpResponse.json(emptySummary(from, to));
    live = () => HttpResponse.json({ visitorsNow: 0, sessionsNow: 0, pages: [], recent: [] });
    sessions = () => HttpResponse.json([]);
    show();
    await loaded();

    expect(screen.getByRole('status')).toHaveTextContent('No visits yet in this period. Share your link to get the first visitors!');
    expect(screen.getByRole('button', { name: 'Copy link' })).toBeInTheDocument();
    for (const name of ['Visitors', 'Visits', 'Page views', 'Sign-ups']) {
      expect(within(kpiCard(name)).getByText('—')).toBeInTheDocument();
    }
    expect(screen.getByTestId('kpi-bounceRate')).toHaveTextContent('—');
    expect(screen.getByText('Nobody on the site right now. Visitors show up here within seconds.')).toBeInTheDocument();
    expect(screen.getByText('No activity in the last 5 minutes.')).toBeInTheDocument();
    expect(screen.getAllByText('No visits yet in this period. Share your link to get the first visitors!').length).toBeGreaterThanOrEqual(3);
    expect(screen.getByText(/^No other site has sent a visitor yet/)).toBeInTheDocument();
    expect(screen.getByText(/^No campaign visits yet/)).toBeInTheDocument();
    expect(screen.getByText('No visitors yet, so no funnel to draw. Share your link to get the first visitors!')).toBeInTheDocument();
    expect(screen.getByText(/^No events yet in this period/)).toBeInTheDocument();
    expect(await screen.findByText(/^No visits on .+\. Share your link to get the first visitors!$/)).toBeInTheDocument();
    expect(screen.getAllByText('No visits yet in this period.')).toHaveLength(4);
    expect(screen.getByTestId('privacy-note')).toHaveTextContent('Bots excluded (0 bot hits).');
  });

  it('says the figures could not be read, and reads them again on request', async () => {
    const user = userEvent.setup();
    summary = () => HttpResponse.json({ title: 'The database went away' }, { status: 500 });
    show();

    expect(await screen.findByText("The site's figures could not be loaded.")).toBeInTheDocument();
    expect(screen.getByText('The database went away')).toBeInTheDocument();
    // Live is read on its own, so it stays.
    expect(await screen.findByRole('region', { name: 'Live now' })).toBeInTheDocument();

    summary = (from, to) => HttpResponse.json(summaryFor(from, to));
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await loaded()).toBeInTheDocument();
  });

  it('says the visits of a day could not be read, with a retry', async () => {
    sessions = () => HttpResponse.json({ title: 'Timed out' }, { status: 503 });
    show();
    await loaded();

    expect(await screen.findByText('The visits could not be loaded.')).toBeInTheDocument();
    expect(screen.getByText('Timed out')).toBeInTheDocument();
  });
});
