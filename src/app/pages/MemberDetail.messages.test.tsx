// A member's messages on their page.
//
// The Communications tab read them by hand when the page opened and kept that copy, so a
// campaign sent or a delivery reported meanwhile showed only after a reload, and a read
// that failed left a toast and "No messages to this member yet." It now reads them through
// the cache the communications pages share, and says when they could not be read.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import type { Communication } from '@/app/api/communications';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/pages/members/MemberConsentPanel', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
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
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [JEAN],
}));
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getCharges: async () => [],
  getPayments: async () => [],
}));
vi.mock('@/app/api/visits', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/visits')>()), getVisits: async () => [] }));
vi.mock('@/app/api/loyalty', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/loyalty')>()), getLoyaltyLedger: async () => [] }));
vi.mock('@/app/api/giftCards', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/giftCards')>()), getGiftCards: async () => [] }));
vi.mock('@/app/api/reservations', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/reservations')>()), getReservations: async () => [] }));
vi.mock('@/app/api/eventAttendees', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/eventAttendees')>()),
  getMemberRegistrations: async () => [],
}));

const comms = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock('@/app/api/communications', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/communications')>()),
  getCommunications: (...a: unknown[]) => comms.get(...a),
}));

const MemberDetail = (await import('./MemberDetail')).default;

const STATEMENT: Communication = {
  id: 'com-1', memberId: 'm1', memberName: 'Jean Dupont', email: 'jean@example.ch', channel: 'Email', category: 'Dues',
  purpose: 'service', subject: 'Your October statement', preview: 'Your statement is attached.', status: 'delivered',
  createdAtUtc: '2026-11-01T09:00:00Z', sentAtUtc: '2026-11-01T09:00:05Z',
};

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  comms.get.mockReset().mockResolvedValue([STATEMENT]);
});

const show = () => render(withQueries(
  <MemoryRouter initialEntries={['/members/m1']}>
    <Routes><Route path="/members/:id" element={<MemberDetail />} /></Routes>
  </MemoryRouter>,
));

describe("a member's messages", () => {
  it('are read for the member, through the cache, and shown with where each stands', async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('tab', { name: /Communications \(1\)/ }));

    expect(await screen.findByText('Your October statement')).toBeInTheDocument();
    expect(screen.getByText('Delivered')).toBeInTheDocument();
    expect(comms.get).toHaveBeenCalledWith({ memberId: 'm1' }, expect.objectContaining({ signal: expect.anything() }));
  });

  it('say they could not be read, with a retry, rather than that there are none', async () => {
    comms.get.mockRejectedValue(new Error('The server is down for maintenance.'));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('tab', { name: /Communications/ }));

    expect(await screen.findByText(/This member's messages could not be loaded/)).toBeInTheDocument();
    expect(screen.queryByText('No messages to this member yet.')).toBeNull();

    comms.get.mockResolvedValue([STATEMENT]);
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Your October statement')).toBeInTheDocument();
  });
});
