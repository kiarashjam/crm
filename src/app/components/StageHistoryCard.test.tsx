// A deal's page shows the stages it has been in, from the server's stage history:
// where it was created, each move with who made it, how long it stayed in each stage,
// and how long it has been in the one it is in.
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { DealStageChange } from '@/app/api/types';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const StageHistoryCard = (await import('./StageHistoryCard')).default;

const DAY = 24 * 60 * 60 * 1000;
const at = (daysAgo: number) => new Date(Date.now() - daysAgo * DAY).toISOString();

const created: DealStageChange = {
  id: 'c1', toStageId: 's1', toStageName: 'Enquiry', changedByUserId: 'u1', changedByName: 'Anna Keller',
  changedAtUtc: at(10), leftAtUtc: at(6), isCreation: true,
};
const moved: DealStageChange = {
  id: 'c2', fromStageId: 's1', fromStageName: 'Enquiry', toStageId: 's2', toStageName: 'Tour booked',
  changedByUserId: 'u2', changedByName: 'Beat Meier', changedAtUtc: at(6), isCreation: false,
};

describe('the stage history on a deal', () => {
  it('lists each stage newest first, with who moved the deal and how long it stayed', () => {
    render(<StageHistoryCard history={[moved, created]} loading={false} />);

    const items = screen.getAllByRole('listitem');
    expect(items[0]).toHaveTextContent('Enquiry');
    expect(items[0]).toHaveTextContent('Tour booked');
    expect(items[0]).toHaveTextContent('by Beat Meier');
    expect(items[0]).toHaveTextContent('In Tour booked for 6 days');
    expect(items[1]).toHaveTextContent('Created in Enquiry');
    expect(items[1]).toHaveTextContent('4 days in Enquiry');
    expect(screen.queryByText(/were not recorded/)).toBeNull();
  });

  it('says so when the history does not reach back to where the deal started', () => {
    render(<StageHistoryCard history={[moved]} loading={false} />);

    expect(screen.getByText('Earlier stage changes were not recorded.')).toBeInTheDocument();
  });

  it('says that nothing was recorded rather than showing an empty card', () => {
    render(<StageHistoryCard history={[]} loading={false} />);

    expect(screen.getByText(/No stage changes recorded yet/)).toBeInTheDocument();
  });
});
