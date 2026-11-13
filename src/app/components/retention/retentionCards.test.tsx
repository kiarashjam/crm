// A member's at-risk score and referrals on their page: the score with each sign and why, a
// recompute that says what it set off, concerns recorded with their kind, a referrer named or
// taken off and a referral bonus given, each offered only to whoever may.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ApiError } from '@/app/api/apiClient';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import {
  DEFAULT_RETENTION_SETTINGS, type Concern, type MemberReferrals, type MemberRisk, type ReferralBonus,
} from '@/app/api/retention';
import { withQueries } from '@/test/queryWrapper';

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
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const api = vi.hoisted(() => ({
  risk: vi.fn<() => Promise<MemberRisk | null>>(),
  recompute: vi.fn<() => Promise<MemberRisk>>(),
  concerns: vi.fn<() => Promise<Concern[]>>(),
  addConcern: vi.fn<(memberId: string, kind: string, note: string) => Promise<Concern>>(),
  referrals: vi.fn<() => Promise<MemberReferrals>>(),
  setReferrer: vi.fn<(memberId: string, referrerId: string | null) => Promise<MemberReferrals>>(),
  award: vi.fn<(memberId: string) => Promise<ReferralBonus>>(),
  bonusKind: 'points' as 'points' | 'credit',
}));
vi.mock('@/app/api/retention', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/retention')>()),
  getMemberRisk: () => api.risk(),
  recomputeMemberRisk: () => api.recompute(),
  getConcerns: () => api.concerns(),
  addConcern: (memberId: string, kind: string, note: string) => api.addConcern(memberId, kind, note),
  getMemberReferrals: () => api.referrals(),
  setReferrer: (memberId: string, referrerId: string | null) => api.setReferrer(memberId, referrerId),
  awardReferralBonus: (memberId: string) => api.award(memberId),
  getRetentionSettings: async () => ({ ...DEFAULT_RETENTION_SETTINGS, referralBonus: api.bonusKind }),
}));
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [],
}));

const { MemberRetentionCard } = await import('./MemberRetentionCard');
const { MemberReferralsCard } = await import('./MemberReferralsCard');

const RISK: MemberRisk = {
  memberId: 'm1', score: 55, level: 'watch', computedAtUtc: '2026-11-13T05:00:00Z', atRiskThreshold: 60, watchThreshold: 35, notes: [],
  factors: [
    { key: 'visits', label: 'Visits', weight: 30, strength: 1, points: 30, detail: '0 visits in the last 90 days, against about 13 in a usual 90 days before.' },
    { key: 'tenure', label: 'Tenure', weight: 5, strength: 0, points: 0, detail: 'A member for 3 years.' },
  ],
};

const REFERRALS: MemberReferrals = {
  memberId: 'm1',
  referredBy: { memberId: 'm-ana', name: 'Ana Keller', status: 'Active', tier: 'Gold', joinedAtUtc: '2023-01-01T00:00:00Z' },
  referred: [], proposed: [], seconded: [], bonusesEarned: [], bonusAvailable: true, standing: null,
};

beforeEach(() => {
  [api.risk, api.recompute, api.concerns, api.addConcern, api.referrals, api.setReferrer, api.award].forEach((fn) => fn.mockReset());
  api.bonusKind = 'points';
  toast.success.mockReset();
  toast.error.mockReset();
  as([PERMISSION.ClubMembers]);
  api.concerns.mockResolvedValue([]);
});

describe("a member's at-risk score", () => {
  it('shows the score, its level and each sign with the sentence that says why', async () => {
    api.risk.mockResolvedValue(RISK);

    render(withQueries(<MemberRetentionCard memberId="m1" />));

    expect(await screen.findByTestId('risk-score')).toHaveTextContent('55');
    expect(screen.getByText('Watch')).toBeInTheDocument();
    expect(screen.getByText('0 visits in the last 90 days, against about 13 in a usual 90 days before.')).toBeInTheDocument();
    expect(screen.getByText('+30')).toBeInTheDocument();
  });

  it('works the score out again and says what it set off', async () => {
    api.risk.mockResolvedValue(null);
    api.recompute.mockResolvedValue({ ...RISK, score: 63, level: 'at_risk', notes: ['Anna Keller is at risk: a task for Olivia Owner, an email queued.'] });
    const user = userEvent.setup();
    render(withQueries(<MemberRetentionCard memberId="m1" />));

    expect(await screen.findByText(/Not scored yet/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Work it out again/ }));

    expect(await screen.findByTestId('risk-score')).toHaveTextContent('63');
    expect(toast.success).toHaveBeenCalledWith('Anna Keller is at risk: a task for Olivia Owner, an email queued.');
  });

  it('records a concern with its kind', async () => {
    api.risk.mockResolvedValue(RISK);
    api.addConcern.mockResolvedValue({ id: 'c1', memberId: 'm1', kind: 'concern', note: 'Talked of moving away', recordedAtUtc: '2026-11-13T09:00:00Z' });
    const user = userEvent.setup();
    render(withQueries(<MemberRetentionCard memberId="m1" />));

    await screen.findByTestId('risk-score');
    await user.click(screen.getByRole('radio', { name: 'Concern' }));
    await user.type(screen.getByRole('textbox', { name: 'Complaint or concern' }), 'Talked of moving away');
    await user.click(screen.getByRole('button', { name: /Record/ }));

    await waitFor(() => expect(api.addConcern).toHaveBeenCalledWith('m1', 'concern', 'Talked of moving away'));
  });

  it('keeps a note within what the server takes', async () => {
    api.risk.mockResolvedValue(RISK);
    render(withQueries(<MemberRetentionCard memberId="m1" />));

    expect(await screen.findByRole('textbox', { name: 'Complaint or concern' })).toHaveAttribute('maxLength', '1000');
  });

  it('offers no recompute or recording to someone who may not manage members', async () => {
    as([PERMISSION.FrontDesk]);
    api.risk.mockResolvedValue(RISK);

    render(withQueries(<MemberRetentionCard memberId="m1" />));

    await screen.findByTestId('risk-score');
    expect(screen.queryByRole('button', { name: /Work it out again/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Complaint or concern' })).not.toBeInTheDocument();
  });

  it('says when the score could not be read, and reads it again', async () => {
    api.risk.mockRejectedValueOnce(new ApiError('The server is busy.', 503, '/api/members/m1/risk', true));
    api.risk.mockResolvedValue(RISK);
    const user = userEvent.setup();
    render(withQueries(<MemberRetentionCard memberId="m1" />));

    await user.click(await screen.findByRole('button', { name: /try again/i }));

    expect(await screen.findByTestId('risk-score')).toHaveTextContent('55');
  });
});

describe("a member's referrals", () => {
  it('shows who referred them and gives their referrer the bonus', async () => {
    api.referrals.mockResolvedValue(REFERRALS);
    api.award.mockResolvedValue({
      id: 'b1', referrerMemberId: 'm-ana', referrerName: 'Ana Keller', referredMemberId: 'm1', referredName: 'Ben Keller', kind: 'points',
      status: 'awarded', points: 500, amount: 0, currency: 'CHF', createdAtUtc: '2026-11-13T09:00:00Z',
    });
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><MemberReferralsCard memberId="m1" /></MemoryRouter>));

    expect(await screen.findByRole('link', { name: 'Ana Keller' })).toHaveAttribute('href', '/members/m-ana');
    await user.click(await screen.findByRole('button', { name: /Give Ana Keller the referral bonus/ }));

    await waitFor(() => expect(api.award).toHaveBeenCalledWith('m1'));
    expect(toast.success).toHaveBeenCalledWith('Ana Keller was given 500 points.');
  });

  it('shows the bonus someone else gave at the same moment, instead of offering it again', async () => {
    const given: ReferralBonus = {
      id: 'b1', referrerMemberId: 'm-ana', referrerName: 'Ana Keller', referredMemberId: 'm1', referredName: 'Ben Keller', kind: 'points',
      status: 'awarded', points: 500, amount: 0, currency: 'CHF', createdAtUtc: '2026-11-13T09:00:00Z', awardedAtUtc: '2026-11-13T09:00:00Z',
    };
    api.referrals.mockResolvedValueOnce(REFERRALS);
    api.referrals.mockResolvedValue({ ...REFERRALS, bonusAvailable: false, bonusForThem: given });
    api.award.mockRejectedValue(new ApiError('This referral bonus was already given.', 409, '/api/members/m1/referral-bonus', true));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><MemberReferralsCard memberId="m1" /></MemoryRouter>));

    await user.click(await screen.findByRole('button', { name: /Give Ana Keller the referral bonus/ }));

    expect(await screen.findByText(/Referral bonus for Ana Keller: 500 points, given/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Give Ana Keller the referral bonus/ })).not.toBeInTheDocument();
    expect(toast.error).toHaveBeenCalledWith('This referral bonus was already given.');
  });

  it('takes the referrer off once confirmed', async () => {
    api.referrals.mockResolvedValue(REFERRALS);
    api.setReferrer.mockResolvedValue({ ...REFERRALS, referredBy: undefined, bonusAvailable: false });
    Object.defineProperty(window, 'confirm', { value: vi.fn(() => true), configurable: true, writable: true });
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><MemberReferralsCard memberId="m1" /></MemoryRouter>));

    await screen.findByRole('link', { name: 'Ana Keller' });
    await user.click(screen.getByRole('button', { name: /Remove/ }));

    await waitFor(() => expect(api.setReferrer).toHaveBeenCalledWith('m1', null));
    expect(await screen.findByText('Nobody named')).toBeInTheDocument();
  });

  it('offers a bonus paid as a credit only to someone who may also charge accounts', async () => {
    api.bonusKind = 'credit';
    // The member's referrals come after the club's rules, so the card is drawn knowing the bonus is a credit.
    api.referrals.mockImplementation(() => new Promise((resolve) => { setTimeout(() => resolve(REFERRALS), 30); }));
    const { unmount } = render(withQueries(<MemoryRouter><MemberReferralsCard memberId="m1" /></MemoryRouter>));

    expect(await screen.findByRole('button', { name: /Change referrer/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /referral bonus/ })).not.toBeInTheDocument();
    unmount();

    as([PERMISSION.ClubMembers, PERMISSION.ManageMoney]);
    render(withQueries(<MemoryRouter><MemberReferralsCard memberId="m1" /></MemoryRouter>));
    expect(await screen.findByRole('button', { name: /Give Ana Keller the referral bonus/ })).toBeInTheDocument();
  });

  it('offers no changes to someone who may not manage members', async () => {
    as([PERMISSION.ExportRecords]);
    api.referrals.mockResolvedValue(REFERRALS);

    render(withQueries(<MemoryRouter><MemberReferralsCard memberId="m1" /></MemoryRouter>));

    const card = (await screen.findByRole('link', { name: 'Ana Keller' })).closest('div.rounded-2xl') as HTMLElement;
    expect(within(card).queryByRole('button')).not.toBeInTheDocument();
  });
});
