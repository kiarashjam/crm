// The members list and its form read in the person's language: a Swiss club's front desk
// reads German or French, and a member's status and dues are said in it too.

import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import { withQueries } from '@/test/queryWrapper';
import { I18nProvider, loadCatalogue } from '@/app/i18n';

const membersApi = vi.hoisted(() => ({ getMembers: vi.fn(), createMember: vi.fn(), updateMember: vi.fn(), deleteMember: vi.fn() }));
vi.mock('@/app/api/members', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/members')>('@/app/api/members')),
  ...membersApi,
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, MEMBER_DUPLICATES: {} }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const Members = (await import('./Members')).default;

const jeanne: Member = {
  id: 'm1', firstName: 'Jeanne', lastName: 'Rochat', email: 'jeanne@example.ch', tier: 'Gold', status: 'Lapsed',
  joinedAtUtc: '2025-01-15T00:00:00Z', renewsAtUtc: '2027-01-15T00:00:00Z', duesAmount: 1200, duesFrequency: 'Annual',
  houseAccountBalance: 0,
} as Member;

beforeAll(async () => {
  await loadCatalogue('de');
  await loadCatalogue('fr');
});

beforeEach(() => {
  localStorage.clear();
  membersApi.getMembers.mockReset().mockResolvedValue([jeanne]);
});

const show = (language: 'de' | 'fr') => {
  localStorage.setItem('cadence.language', language);
  return render(withQueries(<I18nProvider><MemoryRouter><Members /></MemoryRouter></I18nProvider>));
};

describe('the members list in German and French', () => {
  it('reads in German, with the status and the dues in German', async () => {
    const user = userEvent.setup();
    show('de');
    expect(await screen.findByRole('heading', { name: 'Mitglieder' })).toBeInTheDocument();
    expect(await screen.findByText('Sistiert')).toBeInTheDocument();
    expect(screen.getByText(/\/Jahr/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Mitglied hinzufügen/ }));
    expect(await screen.findByLabelText('Vorname')).toBeInTheDocument();
    expect(screen.getByLabelText('Korrespondenzsprache')).toHaveValue('de');
  });

  it('reads in French, with Romandy’s words for a lapsed member', async () => {
    show('fr');
    expect(await screen.findByRole('heading', { name: 'Membres' })).toBeInTheDocument();
    expect(await screen.findByText('Suspendu')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Rechercher par nom, e-mail, téléphone…')).toBeInTheDocument();
  });
});
