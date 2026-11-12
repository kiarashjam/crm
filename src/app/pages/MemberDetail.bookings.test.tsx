// A member's bookings and event places on their page.
//
// The page found a member's bookings by matching their email text against every booking in
// the club, so one made without an email, or with it in another case, never showed, and a
// merge left it behind. It now asks the server for the member's own, by id, and shows the
// events they have a seat or a place in line for.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import type { Reservation } from '@/app/api/reservations';
import type { EventAttendee } from '@/app/api/eventAttendees';
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
vi.mock('@/app/api/communications', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/communications')>()), getCommunications: async () => [] }));

const club = vi.hoisted(() => ({ getReservations: vi.fn(), getMemberRegistrations: vi.fn() }));
vi.mock('@/app/api/reservations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/reservations')>()),
  getReservations: (...a: unknown[]) => club.getReservations(...a),
}));
vi.mock('@/app/api/eventAttendees', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/eventAttendees')>()),
  getMemberRegistrations: (...a: unknown[]) => club.getMemberRegistrations(...a),
}));

const MemberDetail = (await import('./MemberDetail')).default;

const tomorrow = new Date(Date.now() + 86_400_000).toISOString();
// Booked at the desk under Jean's record, with no email typed in.
const COURT: Reservation = {
  id: 'b-1', resourceId: 'r-court', resourceName: 'Tennis Court', memberId: 'm1', memberName: 'Jean Dupont',
  partySize: 2, startAtUtc: tomorrow, endAtUtc: new Date(Date.now() + 90_000_000).toISOString(), status: 'Confirmed',
};
const WINE_NIGHT: EventAttendee = {
  id: 'a-1', eventId: 'e-1', eventName: 'Wine night', eventStartAtUtc: new Date(Date.now() + 7 * 86_400_000).toISOString(),
  memberId: 'm1', memberName: 'Jean Dupont', ticketTier: 'Member', status: 'Waitlisted', waitlistPosition: 3, rsvpAtUtc: tomorrow,
};

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  club.getReservations.mockReset().mockResolvedValue([COURT]);
  club.getMemberRegistrations.mockReset().mockResolvedValue([WINE_NIGHT]);
});

const show = () => render(withQueries(
  <MemoryRouter initialEntries={['/members/m1']}>
    <Routes><Route path="/members/:id" element={<MemberDetail />} /></Routes>
  </MemoryRouter>,
));

describe("a member's page", () => {
  it('asks the server for their bookings by member id, and shows one made without an email', async () => {
    show();

    expect(await screen.findByText('Tennis Court')).toBeInTheDocument();
    expect(club.getReservations).toHaveBeenCalledWith(expect.objectContaining({ memberId: 'm1' }));
    // Never the whole club's bookings, to be matched here by email.
    expect(club.getReservations.mock.calls.every(([req]) => (req as { memberId?: string }).memberId === 'm1')).toBe(true);
  });

  it('shows the events they have a place in line for, linked to the event', async () => {
    show();

    const link = await screen.findByRole('link', { name: 'Wine night' });
    expect(link).toHaveAttribute('href', '/events/e-1');
    expect(screen.getByText('Waitlist #3')).toBeInTheDocument();
    expect(club.getMemberRegistrations).toHaveBeenCalledWith('m1', expect.anything());
  });
});
