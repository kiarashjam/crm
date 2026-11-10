// A month's saved forecasts are kept, not replaced, and set against what was won.

import { afterEach, describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import type { ForecastSnapshotSet, ForecastSnapshotRow } from '@/app/api/reporting';
import { formatMoney } from '@/app/lib/money';

const line = (p: Partial<ForecastSnapshotRow>): ForecastSnapshotRow => ({
  id: 'r', month: '2026-10', currency: 'CHF', assigneeName: 'Anna Keller', weightedValue: 5000, openValue: 10000,
  covered: 1, missing: 0, savedAtUtc: '2026-10-01T07:00:00Z', snapshotId: 's2', byCloseDate: true,
  overdueCount: 0, overdueValue: 0, noCloseDateCount: 0, unreadableCount: 0, ...p,
});
const twoSaves: ForecastSnapshotSet = {
  month: '2026-10',
  saves: [
    { snapshotId: 's2', savedAtUtc: '2026-10-30T09:00:00Z', savedByName: 'Beat', byCloseDate: true },
    { snapshotId: 's1', savedAtUtc: '2026-10-01T07:00:00Z', savedByName: 'Anna Keller', byCloseDate: false },
  ],
  snapshotId: 's2',
  rows: [line({ noCloseDateCount: 2 })],
  actual: [{ assigneeName: 'Anna Keller', currency: 'CHF', wonCount: 1, wonValue: 4000 }],
};

const getForecastSnapshots = vi.fn(async (..._args: unknown[]) => twoSaves);
const saveForecastSnapshot = vi.fn(async (..._args: unknown[]) => twoSaves);

vi.mock('@/app/api/reporting', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/reporting')>()),
  getForecastSnapshots: (...a: unknown[]) => getForecastSnapshots(...a),
  saveForecastSnapshot: (...a: unknown[]) => saveForecastSnapshot(...a),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
const workspace = vi.hoisted(() => ({ current: null as null | { id: string; name: string; currency?: string; locale?: string; timeZone?: string } }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => (workspace.current ? { currentOrg: workspace.current } : null),
}));

import { SavedForecastPanel, compareWithActual } from './SavedForecastPanel';

beforeEach(() => {
  workspace.current = null;
  getForecastSnapshots.mockClear();
  saveForecastSnapshot.mockClear();
});

describe('compareWithActual', () => {
  it('sets each owner\'s forecast beside what they won, an owner who won unforecast included', () => {
    const lines = compareWithActual({
      ...twoSaves,
      actual: [...twoSaves.actual, { assigneeName: 'Chloé', currency: 'CHF', wonCount: 1, wonValue: 900 }],
    });
    expect(lines).toEqual([
      { owner: 'Anna Keller', currency: 'CHF', weighted: 5000, expected: 10000, won: 4000, wonCount: 1 },
      { owner: 'Chloé', currency: 'CHF', weighted: 0, expected: 0, won: 900, wonCount: 1 },
    ]);
  });
});

describe('SavedForecastPanel', () => {
  describe("on the workspace's calendar", () => {
    // Half past midnight on 1 November in Zurich is still October in UTC and New York.
    // The panel opened on this browser's month, and offered to save October's forecast
    // for a club whose November had begun.
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['Date'] });
      vi.setSystemTime(new Date('2026-10-31T23:30:00Z'));
    });
    afterEach(() => vi.useRealTimers());

    it("opens on the month that has begun at the club", async () => {
      workspace.current = { id: 'o', name: 'Pavillon 46', timeZone: 'Europe/Zurich' };
      render(<SavedForecastPanel />);
      await screen.findByLabelText('Save shown');

      expect(getForecastSnapshots).toHaveBeenCalledWith(2026, 11, undefined);
    });

    it("opens on a workspace's own month wherever the browser is", async () => {
      workspace.current = { id: 'o', name: 'Brooklyn Club', timeZone: 'America/New_York' };
      render(<SavedForecastPanel />);
      await screen.findByLabelText('Save shown');

      expect(getForecastSnapshots).toHaveBeenCalledWith(2026, 10, undefined);
    });
  });

  it("writes the amounts in the workspace's format", async () => {
    workspace.current = { id: 'o', name: 'Pavillon 46', currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' };
    render(<SavedForecastPanel />);
    await screen.findByLabelText('Save shown');

    const swiss = formatMoney(10000, 'CHF', { locale: 'de-CH', cents: 'never' }).replace(/\s+/g, ' ');
    expect(screen.getByText(swiss)).toBeInTheDocument();
  });

  it('lists every save of the month and says saving again keeps the earlier one', async () => {
    render(<SavedForecastPanel />);

    expect(await screen.findByRole('option', { name: /by Anna Keller \(first\)/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /by Beat/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save again (keeps the earlier one)' })).toBeInTheDocument();
    expect(screen.getByText(/2 with no close date/)).toBeInTheDocument();
  });

  it('shows the month picked last, whichever answer comes back first', async () => {
    render(<SavedForecastPanel />);
    await screen.findByLabelText('Save shown');

    // One month back answers slowly, two months back at once.
    let answerLate: (s: ForecastSnapshotSet) => void = () => {};
    getForecastSnapshots.mockImplementationOnce(() => new Promise<ForecastSnapshotSet>((resolve) => { answerLate = resolve; }));
    getForecastSnapshots.mockImplementationOnce(async () => ({ month: '2026-08', saves: [], snapshotId: null, rows: [], actual: [] }));
    fireEvent.click(screen.getByRole('button', { name: 'Previous month' }));
    fireEvent.click(screen.getByRole('button', { name: 'Previous month' }));
    expect(await screen.findByText(/^Nothing saved for/)).toBeInTheDocument();

    await act(async () => { answerLate(twoSaves); });

    expect(screen.queryByLabelText('Save shown')).toBeNull();
    expect(screen.getByText(/^Nothing saved for/)).toBeInTheDocument();
  });

  it('keeps to the month being saved until the save comes back', async () => {
    render(<SavedForecastPanel />);
    await screen.findByLabelText('Save shown');
    let saved: (s: ForecastSnapshotSet) => void = () => {};
    saveForecastSnapshot.mockImplementationOnce(() => new Promise<ForecastSnapshotSet>((resolve) => { saved = resolve; }));

    fireEvent.click(screen.getByRole('button', { name: 'Save again (keeps the earlier one)' }));

    expect(screen.getByRole('button', { name: 'Next month' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Previous month' })).toBeDisabled();
    await act(async () => { saved(twoSaves); });
    expect(screen.getByRole('button', { name: 'Next month' })).toBeEnabled();
  });

  it('shows an earlier save when it is picked, and warns when it counted every open deal', async () => {
    render(<SavedForecastPanel />);
    const select = await screen.findByLabelText('Save shown');

    getForecastSnapshots.mockImplementationOnce(async () => ({ ...twoSaves, snapshotId: 's1', rows: [line({ snapshotId: 's1', byCloseDate: false })] }));
    fireEvent.change(select, { target: { value: 's1' } });

    await waitFor(() => expect(getForecastSnapshots).toHaveBeenLastCalledWith(expect.any(Number), expect.any(Number), 's1'));
    expect(await screen.findByText(/Saved before forecasts went by close date/)).toBeInTheDocument();
  });
});
