// The dashboard writes its amounts in the workspace's number format, as every other
// page does. It wrote them in the default one, de-CH, whatever the workspace said.
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { formatMoney } from '@/app/lib/money';

const workspace = vi.hoisted(() => ({ current: { id: 'o', name: 'Brooklyn Club', currency: 'USD', locale: 'en-US', timeZone: 'America/New_York' } }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: workspace.current }),
}));

const { PipelineChart } = await import('./PipelineChart');
const { TeamPerformance } = await import('./TeamPerformance');
const { DashboardHero } = await import('./DashboardHero');

// Intl writes a no-break space between the code and the number; the page is read with plain spaces.
const flat = (s: string) => s.replace(/\s+/g, ' ');
const american = (n: number, currency: string) => flat(formatMoney(n, currency, { locale: 'en-US' }));
const swiss = (n: number, currency: string) => flat(formatMoney(n, currency, { locale: 'de-CH' }));

describe("the dashboard's amounts", () => {
  it("are written in the workspace's number format", () => {
    // The two formats differ, or this test would prove nothing.
    expect(american(85500, 'CHF')).not.toBe(swiss(85500, 'CHF'));

    render(
      <MemoryRouter>
        <DashboardHero
          displayName="Ada"
          loaded
          failed={false}
          stats={{
            activeLeadsCount: 1, activeDealsCount: 2, pipelineValue: 85500, pipelineCurrency: 'CHF',
            pipelineByCurrency: [{ currency: 'CHF', value: 85500, dealCount: 1 }, { currency: 'EUR', value: 12500, dealCount: 1 }],
            unreadableValueCount: 0, dealsWonCount: 0, dealsLostCount: 0,
          }}
        />
        <PipelineChart loaded failed={false} stages={[{ stageId: 's', stageName: 'Proposal', currency: 'CHF', dealCount: 1, value: 85500 }]} />
        <TeamPerformance loaded failed={false} members={[{ assigneeUserId: 'u', assigneeName: 'Anna Keller', currency: 'CHF', dealCount: 1, value: 42000 }]} />
      </MemoryRouter>,
    );

    expect(screen.getByText(new RegExp(american(12500, 'EUR')))).toBeInTheDocument();
    expect(screen.getAllByText(american(85500, 'CHF')).length).toBeGreaterThan(0);
    expect(screen.getByText(american(42000, 'CHF'))).toBeInTheDocument();
    expect(screen.queryByText(swiss(42000, 'CHF'))).toBeNull();
  });
});
