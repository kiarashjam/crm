// The workspace's own sending address is used only once the mail service has verified
// its domain. The card says which address email really goes out from, and does not
// claim the saved one is used before it is.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { EmailSending, Organization } from '@/app/api/organizations';

const state = vi.hoisted(() => ({
  org: null as Organization | null,
  demo: false,
  sender: null as EmailSending | null,
  get: vi.fn(),
  update: vi.fn(),
}));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrg: () => ({ currentOrg: state.org }) }));
vi.mock('@/app/api/apiClient', () => ({ servesDemoData: () => state.demo }));
vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  getEmailSending: (...args: unknown[]) => state.get(...args),
  updateEmailSending: (...args: unknown[]) => state.update(...args),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { EmailSendingCard } = await import('./EmailSendingCard');

const workspace = (over: Partial<Organization> = {}): Organization => ({
  id: 'org-1', name: 'Acme', ownerUserId: 'me', isOwner: true, role: 0, ...over,
});

beforeEach(() => {
  state.org = workspace();
  state.demo = false;
  state.sender = { sendingAddress: null, verified: false, sharedAddress: 'info@pavillon46.ch', repliesCaptured: false };
  state.get.mockReset();
  state.get.mockImplementation(async () => state.sender);
  state.update.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
});

describe('the workspace email sender', () => {
  it('says the shared address is used until the domain is verified, and saves what the owner types', async () => {
    state.update.mockResolvedValue({ sendingAddress: 'hello@acme.example', verified: false, sharedAddress: 'info@pavillon46.ch', repliesCaptured: false });
    const user = userEvent.setup();
    render(<EmailSendingCard />);

    expect(await screen.findByText('Acme <info@pavillon46.ch>')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    await user.type(screen.getByLabelText('The workspace’s own sending address'), 'hello@acme.example');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(state.update).toHaveBeenCalledWith('org-1', 'hello@acme.example'));
    expect(await screen.findByText(/Not used yet: acme\.example has not been verified/)).toBeTruthy();
    expect(screen.getByText('Acme <info@pavillon46.ch>')).toBeTruthy();
    expect(toast.success).toHaveBeenCalledWith('Saved. Email still goes out from info@pavillon46.ch until acme.example is verified.');
  });

  it('shows the workspace address once it is verified, and that replies come back', async () => {
    state.sender = { sendingAddress: 'hello@acme.example', verified: true, sharedAddress: 'info@pavillon46.ch', repliesCaptured: true };
    render(<EmailSendingCard />);

    expect(await screen.findByText('Acme <hello@acme.example>')).toBeTruthy();
    expect(screen.getByText(/acme\.example is verified with the mail service/)).toBeTruthy();
    expect(screen.getByText(/comes back onto the record’s timeline/)).toBeTruthy();
  });

  it('lets a member read it but not change it', async () => {
    state.org = workspace({ isOwner: false, role: 1 });
    render(<EmailSendingCard />);

    expect(await screen.findByText('Only the owner or a manager can change this.')).toBeTruthy();
    expect(screen.getByLabelText('The workspace’s own sending address')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull();
  });

  it('shows nothing in demo mode, which sends no email', () => {
    state.demo = true;
    const { container } = render(<EmailSendingCard />);

    expect(container.innerHTML).toBe('');
    expect(state.get).not.toHaveBeenCalled();
  });
});
