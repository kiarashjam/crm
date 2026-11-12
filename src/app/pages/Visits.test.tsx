// Check-ins, read through the club's shared cache.
//
// The page read the visits and the members by hand when it opened: a check-in made at the
// door showed only after a reload, and a read that failed left a toast over "No check-ins
// yet", as if nobody had come. It now reads both through the cache the club pages share,
// and says when they could not be read, with a way to try again.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import type { Visit } from '@/app/api/visits';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const JEAN: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
const LUNCH: Visit = {
  id: 'v1', memberId: 'm1', memberName: 'Jean Dupont', memberTier: 'Gold', venue: 'Dining',
  visitedAtUtc: new Date().toISOString(), guestCount: 1, pointsAwarded: 50,
};

const club = vi.hoisted(() => ({ getVisits: vi.fn(), getMembers: vi.fn() }));
vi.mock('@/app/api/visits', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/visits')>()),
  getVisits: (...a: unknown[]) => club.getVisits(...a),
}));
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: (...a: unknown[]) => club.getMembers(...a),
}));

const Visits = (await import('./Visits')).default;

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  club.getVisits.mockReset().mockResolvedValue([LUNCH]);
  club.getMembers.mockReset().mockResolvedValue([JEAN]);
});

const show = () => render(withQueries(<MemoryRouter><Visits /></MemoryRouter>));

describe('the check-ins page', () => {
  it('shows the check-ins it read', async () => {
    show();

    expect((await screen.findAllByText('Jean Dupont')).length).toBeGreaterThan(0);
    expect(screen.queryByText('No check-ins yet')).toBeNull();
  });

  it('says the check-ins could not be read, with a retry, rather than that nobody came', async () => {
    club.getVisits.mockRejectedValue(new Error('The server is down for maintenance.'));
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Check-ins could not be loaded.')).toBeInTheDocument();
    expect(screen.queryByText('No check-ins yet')).toBeNull();

    club.getVisits.mockResolvedValue([LUNCH]);
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect((await screen.findAllByText('Jean Dupont')).length).toBeGreaterThan(0);
  });
});
