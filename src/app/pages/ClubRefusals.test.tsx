// What a club page says when the server refuses a delete or a change.
//
// With the money ledger the server refuses more than it did: a member who still owes the
// club, is owed by it, or has a tab or a subscription running is not deleted, and a
// subscription that has billed a charge is cancelled rather than deleted. The pages awaited
// those calls with no catch, so the refusal went nowhere and the button seemed to do
// nothing. Each now shows the server's sentence.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ApiError } from '@/app/api/apiClient';
import type { Member } from '@/app/api/members';
import type { Subscription } from '@/app/api/subscriptions';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, MEMBER_DUPLICATES: {} }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: async () => ({}),
}));

const JEAN: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: -20,
};
const members = vi.hoisted(() => ({ getMembers: vi.fn(), deleteMember: vi.fn() }));
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: () => members.getMembers(),
  deleteMember: (id: string) => members.deleteMember(id),
}));

// The member page reads the rest of the club alongside the member.
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getCharges: async () => [],
  getPayments: async () => [],
}));
vi.mock('@/app/api/visits', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/visits')>()), getVisits: async () => [] }));
vi.mock('@/app/api/loyalty', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/loyalty')>()), getLoyaltyLedger: async () => [] }));
vi.mock('@/app/api/giftCards', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/giftCards')>()), getGiftCards: async () => [] }));
vi.mock('@/app/api/reservations', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/reservations')>()), getReservations: async () => [] }));
vi.mock('@/app/api/eventAttendees', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/eventAttendees')>()), getMemberRegistrations: async () => [] }));
vi.mock('@/app/api/communications', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/communications')>()), getCommunications: async () => [] }));

const DUES: Subscription = {
  id: 's1', memberId: 'm1', memberName: 'Jean Dupont', name: 'Gold dues', amount: 299, cadence: 'Monthly', status: 'Active',
  nextChargeAtUtc: '2026-12-01T00:00:00Z', startedAtUtc: '2026-01-01T00:00:00Z', lastChargedAtUtc: '2026-11-01T00:00:00Z',
};
const subscriptions = vi.hoisted(() => ({ getSubscriptions: vi.fn(), updateSubscription: vi.fn(), deleteSubscription: vi.fn(), runDueBillingCycles: vi.fn() }));
vi.mock('@/app/api/subscriptions', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/subscriptions')>()),
  getSubscriptions: () => subscriptions.getSubscriptions(),
  updateSubscription: (...a: unknown[]) => subscriptions.updateSubscription(...a),
  deleteSubscription: (id: string) => subscriptions.deleteSubscription(id),
  runDueBillingCycles: () => subscriptions.runDueBillingCycles(),
}));

const Members = (await import('./Members')).default;
const MemberDetail = (await import('./MemberDetail')).default;
const Subscriptions = (await import('./Subscriptions')).default;

// Each delete asks first with the browser's confirm, which the test page lacks.
const confirm = vi.fn<(message?: string) => boolean>();
Object.defineProperty(window, 'confirm', { value: confirm, configurable: true, writable: true });

const OPEN_ACCOUNT = 'This member still has a balance, an open tab or a running subscription. Settle or close them first, or set the member Resigned or Lapsed instead.';
const BILLED = "This subscription has billed charges, so it can't be deleted. Cancel it instead; its charges stay on the account.";

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  toast.success.mockReset();
  toast.error.mockReset();
  confirm.mockReset().mockReturnValue(true);
  members.getMembers.mockReset().mockResolvedValue([JEAN]);
  members.deleteMember.mockReset().mockRejectedValue(new ApiError(OPEN_ACCOUNT, 409, '/api/members/m1', true, 'ClubMember.OpenAccount.Conflict'));
  subscriptions.getSubscriptions.mockReset().mockResolvedValue([DUES]);
  subscriptions.updateSubscription.mockReset();
  subscriptions.runDueBillingCycles.mockReset();
  subscriptions.deleteSubscription.mockReset().mockRejectedValue(new ApiError(BILLED, 409, '/api/subscriptions/s1', true, 'Subscription.HasCharges.Conflict'));
});

describe('removing a member who still has money with the club', () => {
  it('says why on the members list, rather than nothing happening', async () => {
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><Members /></MemoryRouter>));

    await user.click((await screen.findAllByRole('button', { name: 'Remove member' }))[0]!);

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith(OPEN_ACCOUNT));
    expect(members.deleteMember).toHaveBeenCalledWith('m1');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it("says why on the member's own page", async () => {
    const user = userEvent.setup();
    render(withQueries(
      <MemoryRouter initialEntries={['/members/m1']}>
        <Routes><Route path="/members/:id" element={<MemberDetail />} /></Routes>
      </MemoryRouter>,
    ));

    await user.click(await screen.findByRole('button', { name: 'Delete member' }));

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith(OPEN_ACCOUNT));
    expect(toast.success).not.toHaveBeenCalled();
  });
});

describe('a subscription the server will not change', () => {
  it('says that one which has billed is cancelled rather than deleted', async () => {
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><Subscriptions /></MemoryRouter>));

    await user.click((await screen.findAllByRole('button', { name: 'Delete' }))[0]!);

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith(BILLED));
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('says so when a pause is refused', async () => {
    const changed = 'Someone else changed this subscription at the same moment, so nothing was saved. Reload and try again.';
    subscriptions.updateSubscription.mockRejectedValue(new ApiError(changed, 409, '/api/subscriptions/s1', true, 'Subscription.Changed.Conflict'));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><Subscriptions /></MemoryRouter>));

    await user.click((await screen.findAllByRole('button', { name: /Pause/ }))[0]!);

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith(changed));
    expect(subscriptions.updateSubscription).toHaveBeenCalledWith('s1', { status: 'Paused' });
  });

  it('says so when a run of the due cycles stops at one billed already', async () => {
    const billed = 'This period was billed already, so nothing more was charged.';
    subscriptions.runDueBillingCycles.mockRejectedValue(new ApiError(billed, 409, '/api/subscriptions/s1/bill', true, 'Subscription.PeriodBilled.Conflict'));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><Subscriptions /></MemoryRouter>));

    await user.click(await screen.findByRole('button', { name: /Run due cycles/ }));

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith(billed));
    expect(toast.success).not.toHaveBeenCalled();
  });
});
