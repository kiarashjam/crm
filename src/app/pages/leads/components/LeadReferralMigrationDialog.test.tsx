import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';

vi.mock('@/app/api', () => ({ getLeads: vi.fn(), getContacts: vi.fn(), setLeadReferral: vi.fn() }));
vi.mock('@/app/api/apiClient', () => ({ servesDemoData: () => false }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrg: () => ({ currentOrgId: 'org-1' }) }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), warning: vi.fn(), error: vi.fn() } }));

import { getContacts, getLeads, setLeadReferral } from '@/app/api';
import { loadLeadReferrals, saveLeadReferrals } from '../leadReferralStore';
import { LeadReferralMigrationDialog } from './LeadReferralMigrationDialog';

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.mocked(getLeads).mockResolvedValue([
    { id: 'l-ada', name: 'Ada Lovelace', email: 'ada@example.ch', status: 'New' },
    { id: 'l-bea', name: 'Bea', email: 'bea@example.ch', status: 'New', referredByContactId: 'c-grace' },
  ] as never);
  vi.mocked(getContacts).mockResolvedValue([
    { id: 'c-sam', name: 'Sam Lee', email: 'sam@example.ch' },
    { id: 'c-grace', name: 'Grace Hopper', email: 'grace@example.ch' },
  ] as never);
  vi.mocked(setLeadReferral).mockReset();
  vi.mocked(getLeads).mockClear();
});

describe('LeadReferralMigrationDialog', () => {
  it('offers only referrals the server does not have, saves them, and forgets them here', async () => {
    saveLeadReferrals({ 'l-ada': 'c-sam', 'l-bea': 'c-sam', 'l-other-org': 'c-sam' });
    vi.mocked(setLeadReferral).mockResolvedValue({ id: 'l-ada' } as never);
    const onMigrated = vi.fn();
    render(<LeadReferralMigrationDialog onMigrated={onMigrated} />);

    expect(await screen.findByText('referred by Sam Lee')).toBeTruthy();
    // Bea already has a referrer a teammate set: it is not offered, and not overwritten.
    expect(screen.queryByText('Bea')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Save 1' }));

    await waitFor(() => expect(onMigrated).toHaveBeenCalled());
    expect(setLeadReferral).toHaveBeenCalledTimes(1);
    expect(setLeadReferral).toHaveBeenCalledWith('l-ada', 'c-sam');
    // Saved and settled entries are forgotten. Another workspace's lead waits for that workspace.
    expect(loadLeadReferrals()).toEqual({ 'l-other-org': 'c-sam' });
  });

  it('asks nothing when this browser holds no referrals', async () => {
    render(<LeadReferralMigrationDialog />);
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(getLeads).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});
