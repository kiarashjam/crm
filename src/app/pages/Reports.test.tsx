// The Reports page asks the server for its figures instead of downloading every
// deal, lead and activity and working them out here.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReportSummary, Forecast } from '@/app/api/reporting';

const summary = (p: Partial<ReportSummary> = {}): ReportSummary => ({
  period: {
    fromUtc: '2026-09-30T22:00:00.000Z', toUtc: '2026-10-31T23:00:00.000Z', scope: 'org', teamId: null, teamName: null,
    pipelineId: null, currency: null, timeZone: 'Europe/Zurich', bucket: 'day',
  },
  currencies: ['CHF', 'EUR'],
  headlineCurrency: 'CHF',
  open: { count: 3, byCurrency: [{ currency: 'CHF', count: 2, value: 300000, unreadable: 0 }, { currency: 'EUR', count: 1, value: 1000, unreadable: 0 }] },
  weighted: [{ currency: 'CHF', value: 150000, covered: 1, missing: 1 }],
  closingInPeriod: { count: 0, byCurrency: [] },
  created: { count: 0, byCurrency: [] },
  won: { count: 1, byCurrency: [{ currency: 'CHF', count: 1, value: 45000, unreadable: 0 }] },
  lost: { count: 1, byCurrency: [{ currency: 'CHF', count: 1, value: 5000, unreadable: 0 }] },
  winRate: 0.5,
  averageSalesCycleDays: 20,
  byStage: [{ pipelineId: 'p', pipelineName: 'Sales', stageKey: 's', stageName: 'Proposal', displayOrder: 1, currency: 'CHF', count: 2, value: 300000, weighted: 150000, unreadable: 0 }],
  byOwner: [{ ownerId: 'u', ownerName: 'Anna Keller', currency: 'CHF', openCount: 2, openValue: 300000, weighted: 150000, wonCount: 1, wonValue: 45000, lostCount: 1 }],
  leads: { active: 4, created: 2, createdConverted: 1, converted: 1, convertedToDeal: 1, scored: 1, averageScore: 40, byStatus: [{ status: 'New', count: 2, converted: 1 }], bySource: [{ key: 'website', count: 2 }], createdSeries: [] },
  activities: { total: 0, byType: [], series: [] },
  ...p,
});
const forecast: Forecast = { firstMonth: '2026-10', months: 6, scope: 'org', teamId: null, rows: [], overdue: [], noCloseDate: [] };

const getReportSummary = vi.fn(async (_filters: unknown) => summary());
const getForecast = vi.fn(async (_filters: unknown) => forecast);
const getDeals = vi.fn(async () => []);
const getActivities = vi.fn(async () => []);
const getLeads = vi.fn(async () => []);
const downloadCsv = vi.fn();

vi.mock('@/app/api/reporting', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/reporting')>()),
  getReportSummary: (f: unknown) => getReportSummary(f),
  getForecast: (f: unknown) => getForecast(f),
  getSalesTeams: vi.fn(async () => []),
}));
vi.mock('@/app/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api')>()),
  getDeals: () => getDeals(),
  getActivities: () => getActivities(),
  getLeads: () => getLeads(),
  getPipelines: vi.fn(async () => []),
}));
vi.mock('@/app/lib/exportCsv', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/lib/exportCsv')>()),
  downloadCsv: (...args: unknown[]) => downloadCsv(...args),
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('./reports/ConversionReportPanel', () => ({ ConversionReportPanel: () => null }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => null }));

import Reports from './Reports';

const renderPage = () => render(<MemoryRouter><Reports /></MemoryRouter>);

beforeEach(() => {
  localStorage.clear();
  getReportSummary.mockClear();
  getReportSummary.mockImplementation(async () => summary());
  getDeals.mockClear();
  getActivities.mockClear();
  getLeads.mockClear();
  downloadCsv.mockClear();
});

describe('Reports', () => {
  it('shows the server\'s figures in the open pipeline\'s currency, without downloading every record', async () => {
    renderPage();

    expect(await screen.findByText('Anna Keller')).toBeInTheDocument();
    expect(screen.getAllByText(/300,000/).length).toBeGreaterThan(0);
    expect(screen.getByText('1 won · 1 lost')).toBeInTheDocument();
    expect(screen.getByText(/EUR is listed separately/)).toBeInTheDocument();
    expect(getDeals).not.toHaveBeenCalled();
    expect(getActivities).not.toHaveBeenCalled();
    expect(getLeads).not.toHaveBeenCalled();
  });

  it('asks again for the viewer\'s own figures when "Mine" is picked', async () => {
    renderPage();
    await screen.findByText('Anna Keller');

    fireEvent.click(screen.getByRole('button', { name: 'Mine' }));

    await waitFor(() => expect(getReportSummary).toHaveBeenLastCalledWith(expect.objectContaining({ scope: 'me' })));
  });

  it('exports the owners table as CSV, every currency in it', async () => {
    renderPage();
    await screen.findByText('Anna Keller');

    fireEvent.click(screen.getByRole('button', { name: 'Download the owners table as CSV' }));

    expect(downloadCsv).toHaveBeenCalledTimes(1);
    const [name, rows] = downloadCsv.mock.calls[0]!;
    expect(name).toMatch(/^cadence-by-owner-.*\.csv$/);
    expect(rows).toHaveLength(1);
  });

  it('says the figures could not be loaded rather than showing zeros', async () => {
    getReportSummary.mockImplementation(async () => { throw new Error('You are in no sales team here.'); });
    renderPage();

    expect(await screen.findByText(/You are in no sales team here/)).toBeInTheDocument();
    expect(screen.queryByText('Open pipeline')).toBeNull();
  });
});
