// The renewals board: the server's columns and cards as they are, a renewal moved by whoever
// manages members, a move made from an older copy said so with a way to reload, and a board
// that could not be read said so with a way to try again.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ApiError } from '@/app/api/apiClient';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { RenewalBoard, RenewalCard, RenewalStageChange } from '@/app/api/retention';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' },
  };
};

const api = vi.hoisted(() => ({
  board: vi.fn<(query: unknown) => Promise<RenewalBoard>>(),
  stage: vi.fn<(card: RenewalCard, change: RenewalStageChange) => Promise<RenewalCard>>(),
}));
vi.mock('@/app/api/retention', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/retention')>()),
  getRenewalBoard: (query: unknown) => api.board(query),
  setRenewalStage: (card: RenewalCard, change: RenewalStageChange) => api.stage(card, change),
}));

const { default: Renewals } = await import('./Renewals');

const ANNA: RenewalCard = {
  memberId: 'm-anna', name: 'Anna Keller', tier: 'Gold', memberStatus: 'Active', source: 'membership', endsAtUtc: '2026-12-03T10:00:00Z',
  daysLeft: 20, stage: 'upcoming', stageReason: 'Nobody has been in touch about it yet', stageSetByHand: false, ownerUserId: 'u-owner',
  ownerName: 'Olivia Owner', annualValue: 3588, periodAmount: 299, cadence: 'Monthly', currency: 'CHF', riskScore: 41, riskLevel: 'watch',
};
const BEN: RenewalCard = {
  ...ANNA, memberId: 'm-ben', name: 'Ben Keller', stage: 'contacted', stageReason: 'Someone wrote to them', lastContactAtUtc: '2026-11-10T09:00:00Z',
  lastContactKind: 'message', riskScore: undefined, riskLevel: undefined, version: 'v-ben',
};

function board(cards: RenewalCard[]): RenewalBoard {
  const stages = ['upcoming', 'contacted', 'at_risk', 'renewed', 'lapsed'] as const;
  const labels = { upcoming: 'Upcoming', contacted: 'Contacted', at_risk: 'At risk', renewed: 'Renewed', lapsed: 'Lapsed' };
  return {
    fromUtc: '2026-10-14T00:00:00Z', toUtc: '2027-01-12T00:00:00Z', days: 60, pastDays: 30, currency: 'CHF', atRiskThreshold: 60,
    columns: stages.map((stage) => {
      const inStage = cards.filter((c) => c.stage === stage);
      return {
        stage, label: labels[stage], count: inStage.length, cards: inStage,
        totals: inStage.length ? [{ currency: 'CHF', amount: inStage.reduce((s, c) => s + c.annualValue, 0) }] : [],
      };
    }),
  };
}

const show = () => render(withQueries(<MemoryRouter><Renewals /></MemoryRouter>));

beforeEach(() => {
  api.board.mockReset();
  api.stage.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  as([PERMISSION.ClubMembers]);
});

describe('the renewals board', () => {
  it("shows the server's columns, each card's end, owner, value, last contact and why it stands where it does", async () => {
    api.board.mockResolvedValue(board([ANNA, BEN]));

    show();

    const upcoming = await screen.findByRole('region', { name: 'Upcoming' });
    expect(within(upcoming).getByRole('link', { name: 'Anna Keller' })).toHaveAttribute('href', '/members/m-anna');
    expect(within(upcoming).getByText('Nobody has been in touch about it yet')).toBeInTheDocument();
    expect(within(upcoming).getByText('Olivia Owner')).toBeInTheDocument();
    expect(within(upcoming).getByText('41')).toBeInTheDocument();
    const contacted = screen.getByRole('region', { name: 'Contacted' });
    expect(within(contacted).getByText(/written to/)).toBeInTheDocument();
    expect(api.board).toHaveBeenCalledWith({ days: 60, owner: undefined, monthly: undefined });
  });

  it('moves a renewal on the card it was shown, and a move from an older copy offers the latest', async () => {
    api.board.mockResolvedValue(board([ANNA, BEN]));
    api.stage.mockResolvedValueOnce({ ...BEN, stage: 'at_risk', stageReason: 'Marked at risk by Olivia Owner', stageSetByHand: true });
    const user = userEvent.setup();
    show();

    const contacted = await screen.findByRole('region', { name: 'Contacted' });
    await user.click(within(contacted).getByRole('button', { name: /At risk/ }));

    await waitFor(() => expect(api.stage).toHaveBeenCalledWith(expect.objectContaining({ memberId: 'm-ben', version: 'v-ben' }), { stage: 'at_risk' }));
    expect(toast.success).toHaveBeenCalledWith('Ben Keller: Marked at risk by Olivia Owner.');

    api.stage.mockRejectedValueOnce(new ApiError('Someone else changed this renewal at the same moment.', 409, '/api/renewals/stage', true, 'Renewal.Changed.Conflict'));
    await user.click(within(contacted).getByRole('button', { name: /Not renewing/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Someone else changed this renewal at the same moment.',
      expect.objectContaining({ action: expect.objectContaining({ label: 'Reload' }) })));
  });

  it("does not offer to mark a lapsed member's membership renewed, which the server refuses", async () => {
    const lena: RenewalCard = { ...ANNA, memberId: 'm-lena', name: 'Lena Keller', memberStatus: 'Lapsed', stage: 'lapsed', stageReason: 'The member is lapsed' };
    api.board.mockResolvedValue(board([lena, ANNA]));

    show();

    const lapsed = await screen.findByRole('region', { name: 'Lapsed' });
    expect(within(lapsed).getByRole('button', { name: /Contacted/ })).toBeInTheDocument();
    expect(within(lapsed).queryByRole('button', { name: /Renewed/ })).not.toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Upcoming' })).getByRole('button', { name: /Renewed/ })).toBeInTheDocument();
  });

  it('offers no moves to someone who may not manage members', async () => {
    as([PERMISSION.FrontDesk]);
    api.board.mockResolvedValue(board([ANNA]));

    show();

    await screen.findByRole('link', { name: 'Anna Keller' });
    expect(screen.queryByRole('button', { name: /Contacted/ })).not.toBeInTheDocument();
  });

  it('says when the board could not be read, and reads it again', async () => {
    api.board.mockRejectedValueOnce(new ApiError('The server is busy.', 503, '/api/renewals', true));
    api.board.mockResolvedValue(board([ANNA]));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /try again/i }));

    expect(await screen.findByRole('link', { name: 'Anna Keller' })).toBeInTheDocument();
  });
});
