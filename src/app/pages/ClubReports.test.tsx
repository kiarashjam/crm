// Club Reports and Today at the Club read the club's figures from the server.
//
// They downloaded every member, charge, visit and loyalty entry and summed them here:
// revenue in dollars whatever the club kept its books in, and a page of zeros ("Everything
// is on track") when one of those reads failed. The server sums them now (GET
// /api/club-reports and /today); a page that cannot read them says so and offers to try
// again, and one that cannot refresh them keeps the last copy and says it is that.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { ClubReport, ClubReportRange, ClubToday } from '@/app/api/clubReports';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { regionalOf } from '@/app/lib/money';
import { testQueryClient, withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
// Headline figures count up; with motion reduced they show their final value at once.
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

const WORKSPACE = { currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' };
/**
 * As the pages write an amount for this workspace, whatever this machine's Intl makes of
 * it, with its no-break spaces as the matcher reads the page's text.
 */
const chf = (amount: number) => formatClubMoney(amount, 'CHF', regionalOf(WORKSPACE).locale).replace(/\s+/g, ' ');

/** Signed in to a club that keeps its books in francs, holding these permissions. */
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, ...WORKSPACE },
  };
};

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const api = vi.hoisted(() => ({
  report: vi.fn<(range: unknown) => Promise<unknown>>(),
  today: vi.fn<() => Promise<unknown>>(),
  download: vi.fn<(range: unknown) => Promise<void>>(),
}));
vi.mock('@/app/api/clubReports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/clubReports')>()),
  getClubReport: (range: unknown) => api.report(range),
  getClubToday: () => api.today(),
  downloadClubReportCsv: (range: unknown) => api.download(range),
}));

const REPORT: ClubReport = {
  currency: 'CHF',
  timeZone: 'Europe/Zurich',
  fromUtc: '2025-12-31T23:00:00.000Z',
  toUtc: '2026-12-31T23:00:00.000Z',
  asOfUtc: '2026-11-10T12:00:00.000Z',
  grain: 'month',
  periods: [{ start: '2026-01-01', startUtc: '2025-12-31T23:00:00.000Z', endUtc: '2026-01-31T23:00:00.000Z' }],
  cash: [
    { currency: 'CHF', in: 1500, out: 250.5, net: 1249.5, byPeriod: [{ in: 1500, out: 250.5, net: 1249.5 }] },
    { currency: 'EUR', in: 80, out: 0, net: 80, byPeriod: [{ in: 80, out: 0, net: 80 }] },
  ],
  accrual: [{ currency: 'CHF', in: 2000, out: 100, net: 1900, byPeriod: [{ in: 2000, out: 100, net: 1900 }] }],
  accrualByKind: [{ currency: 'CHF', kind: 'Dining', charged: 2000, voided: 100, net: 1900 }],
  receivables: [{ currency: 'CHF', current: 100, days1To30: 50, days31To60: 0, days61To90: 0, over90: 25, total: 175, openCharges: 3 }],
  giftCards: [{ currency: 'CHF', outstanding: 300, cards: 2 }],
  points: { points: 1200, members: 4, pointsPerUnit: 100, value: 12, currency: 'CHF' },
  mrr: [{ currency: 'CHF', monthly: 899, yearly: 10788, subscriptions: 3 }],
  events: { events: 2, limitedEvents: 1, capacity: 10, seatsTaken: 7, fillRate: 0.7, ended: 1, checkedIn: 3, noShows: 1, notMarked: 0, noShowRate: 0.25 },
  topSpenders: [{ memberId: 'm1', memberName: 'Jean Dupont', currency: 'CHF', amount: 640 }],
  members: { active: 12, lapsed: 2, activeByTier: [{ name: 'Gold', count: 5 }], joined: 1, joinedByPeriod: [1] },
  visits: { checkIns: 9, byVenue: [{ name: 'Bar', count: 9 }] },
};

const TODAY: ClubToday = {
  date: '2026-11-10',
  timeZone: 'Europe/Zurich',
  currency: 'CHF',
  checkIns: 4,
  reservations: {
    count: 1,
    items: [{ id: 'r1', memberName: 'Anne Muster', resourceName: 'Terrace', partySize: 4, startAtUtc: '2026-11-10T18:00:00Z', status: 'Confirmed' }],
  },
  openTabs: {
    count: 1,
    totals: [{ currency: 'CHF', amount: 86.4 }],
    items: [{ id: 't1', memberName: 'Jean Dupont', venue: 'Bar', items: 3, total: 86.4, currency: 'CHF' }],
  },
  applications: { count: 0, items: [] },
  pastDue: {
    charges: 1,
    owed: [{ currency: 'CHF', amount: 320.5 }],
    chargeItems: [{ id: 'c1', memberName: 'Paul Keller', description: 'October dues', owed: 320.5, currency: 'CHF', dueAtUtc: '2026-10-31T23:00:00Z' }],
    subscriptions: 0,
    subscriptionItems: [],
  },
  eventsThisWeek: { count: 0, items: [] },
  anniversaries: [],
};

const HospitalityReports = (await import('./HospitalityReports')).default;
const HospitalityDashboard = (await import('./HospitalityDashboard')).default;

const show = (page: React.ReactNode) => render(withQueries(<MemoryRouter>{page}</MemoryRouter>));

beforeEach(() => {
  api.report.mockReset();
  api.today.mockReset();
  api.download.mockReset();
  as([PERMISSION.ExportRecords]);
});

describe('Club Reports', () => {
  it("shows the server's figures in the workspace's currency, with other currencies apart", async () => {
    api.report.mockResolvedValue(REPORT);
    show(<HospitalityReports />);

    // Cash revenue counts what was received, less what was paid back: 1,500 in, 250.50 out.
    expect((await screen.findAllByText(chf(1249.5))).length).toBeGreaterThan(0);
    expect(screen.getAllByText(chf(1900)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(chf(175)).length).toBeGreaterThan(0);
    expect(screen.getByText('70%')).toBeInTheDocument();
    expect(screen.getByText('25%')).toBeInTheDocument();
    expect(screen.getByText('Jean Dupont')).toBeInTheDocument();
    // The euros are listed on their own, never added to the francs.
    expect(screen.getByText('Other currencies')).toBeInTheDocument();
    expect(screen.queryByText(/\$/)).toBeNull();
  });

  it('asks the server for the period picked, between the workspace midnights', async () => {
    api.report.mockResolvedValue(REPORT);
    show(<HospitalityReports />);
    await screen.findAllByText(chf(1249.5));

    const asked = api.report.mock.calls[0]![0] as ClubReportRange;
    // This year in Zurich starts at 23:00 UTC on the last day of the year before.
    expect(asked.fromUtc).toMatch(/-12-31T23:00:00\.000Z$/);
    expect(asked.grain).toBeUndefined();

    fireEvent.change(screen.getByLabelText('By'), { target: { value: 'week' } });
    await waitFor(() => expect(api.report).toHaveBeenCalledTimes(2));
    expect((api.report.mock.calls[1]![0] as ClubReportRange).grain).toBe('week');
  });

  it('says the figures could not be read and offers to try again, rather than showing zeros', async () => {
    api.report.mockRejectedValueOnce(new Error('The server is resting.'));
    show(<HospitalityReports />);

    expect(await screen.findByText("The club's figures could not be loaded.")).toBeInTheDocument();
    expect(screen.getByText('The server is resting.')).toBeInTheDocument();
    expect(screen.queryByText('Cash revenue')).toBeNull();
    expect(screen.queryByText(chf(0))).toBeNull();

    api.report.mockResolvedValue(REPORT);
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect((await screen.findAllByText(chf(1249.5))).length).toBeGreaterThan(0);
  });

  it('keeps the figures when a refresh fails, and says they are the last copy read', async () => {
    api.report.mockResolvedValueOnce(REPORT);
    const client = testQueryClient();
    render(withQueries(<MemoryRouter><HospitalityReports /></MemoryRouter>, client));
    await screen.findAllByText(chf(1249.5));

    api.report.mockRejectedValueOnce(new Error('Timed out.'));
    await act(() => client.refetchQueries());

    expect(await screen.findByText(/could not be refreshed, so this is the last copy read/)).toBeInTheDocument();
    expect(screen.getAllByText(chf(1249.5)).length).toBeGreaterThan(0);
  });

  it("says another period could not be read, rather than showing the last period's figures as its own", async () => {
    api.report.mockResolvedValueOnce(REPORT);
    show(<HospitalityReports />);
    await screen.findAllByText(chf(1249.5));

    api.report.mockRejectedValueOnce(new Error('Timed out.'));
    fireEvent.change(screen.getByRole('combobox', { name: 'Period' }), { target: { value: 'last-year' } });

    expect(await screen.findByText("The club's figures could not be loaded.")).toBeInTheDocument();
    expect(screen.queryByText(chf(1249.5))).toBeNull();
  });

  it('asks for a custom period between the workspace midnights, and nothing for one that ends before it starts', async () => {
    api.report.mockResolvedValue(REPORT);
    show(<HospitalityReports />);
    await screen.findAllByText(chf(1249.5));
    const lastAsked = () => api.report.mock.lastCall![0] as ClubReportRange;

    fireEvent.change(screen.getByRole('combobox', { name: 'Period' }), { target: { value: 'custom' } });
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-03-02' } });
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-03-03' } });
    // Both days included, from midnight in Zurich (an hour ahead of UTC in March).
    await waitFor(() => expect(lastAsked()).toMatchObject({ fromUtc: '2026-03-01T23:00:00.000Z', toUtc: '2026-03-03T23:00:00.000Z' }));
    const asked = api.report.mock.calls.length;

    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-03-01' } });
    expect(await screen.findByText('Pick an end on or after the start.')).toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(api.report.mock.calls.length).toBe(asked);
    expect(screen.getByRole('button', { name: /export csv/i })).toBeDisabled();
  });

  it('offers the spreadsheet only to members who may export records, and exports the period shown', async () => {
    api.report.mockResolvedValue(REPORT);
    api.download.mockResolvedValue();
    const { unmount } = show(<HospitalityReports />);
    await screen.findAllByText(chf(1249.5));

    fireEvent.click(screen.getByRole('button', { name: /export csv/i }));
    await waitFor(() => expect(api.download).toHaveBeenCalledTimes(1));
    expect(api.download.mock.calls[0]![0]).toEqual(api.report.mock.calls[0]![0]);
    unmount();

    as([PERMISSION.ManageMoney]);
    show(<HospitalityReports />);
    await screen.findAllByText(chf(1249.5));
    expect(screen.queryByRole('button', { name: /export csv/i })).toBeNull();
  });
});

describe('Today at the Club', () => {
  it("shows the server's day: check-ins, bookings, open tabs and what is past due, in the workspace's currency", async () => {
    api.today.mockResolvedValue(TODAY);
    show(<HospitalityDashboard />);

    expect(await screen.findByText('Anne Muster')).toBeInTheDocument();
    expect(screen.getByText('Terrace · party of 4')).toBeInTheDocument();
    expect(screen.getAllByText(chf(86.4)).length).toBeGreaterThan(0);
    expect(screen.getByText('Paul Keller')).toBeInTheDocument();
    expect(screen.getAllByText(chf(320.5)).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /Check-ins today, 4/ })).toBeInTheDocument();
    expect(screen.getByText('No applications awaiting review.')).toBeInTheDocument();
  });

  it('says the day could not be read and offers to try again, rather than an empty day', async () => {
    api.today.mockRejectedValueOnce(new Error('Failed to fetch'));
    show(<HospitalityDashboard />);

    expect(await screen.findByText('Today at the Club could not be loaded.')).toBeInTheDocument();
    // An empty day reads the same as a quiet one: none of it is shown when the read failed.
    expect(screen.queryByText('No reservations today.')).toBeNull();
    expect(screen.queryByText('Nothing is past due.')).toBeNull();

    api.today.mockResolvedValue(TODAY);
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Anne Muster')).toBeInTheDocument();
  });
});
