// What the workspace's leads are is set once, in Settings › Team, by whoever manages the
// workspace's settings. People is the default and what an unset workspace shows.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';

const state = vi.hoisted(() => ({
  org: null as null | {
    id: string; name: string; isOwner: boolean; role: number; permissions?: string[]; leadKinds?: string; salesEnabled?: boolean;
  },
  demo: false,
}));
const refreshOrgs = vi.hoisted(() => vi.fn(async () => {}));
const updateOrganizationSettings = vi.hoisted(() => vi.fn(async () => ({})));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => ({ currentOrg: state.org, refreshOrgs }) }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => state.demo,
}));
vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  updateOrganizationSettings,
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { LeadKindsCard } = await import('./LeadKindsCard');

beforeEach(() => {
  state.org = { id: 'org-1', name: 'Bonap', isOwner: true, role: 0 };
  state.demo = false;
  refreshOrgs.mockClear();
  updateOrganizationSettings.mockClear();
});

describe('the "Your leads are" setting', () => {
  it('shows people for a workspace that never set it', () => {
    render(<LeadKindsCard />);
    expect(screen.getByRole('radio', { name: /people/i })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('radio', { name: /establishments/i })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  it('saves the choice for the workspace and reads the workspace again', async () => {
    render(<LeadKindsCard />);
    fireEvent.click(screen.getByRole('radio', { name: /establishments/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(updateOrganizationSettings).toHaveBeenCalledWith('org-1', { leadKinds: 'establishment' }));
    expect(refreshOrgs).toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalled();
  });

  it('shows the saved choice', () => {
    state.org = { ...state.org!, leadKinds: 'both' };
    render(<LeadKindsCard />);
    expect(screen.getByRole('radio', { name: /both/i })).toHaveAttribute('aria-checked', 'true');
  });

  it('is read-only without the permission to manage settings', () => {
    state.org = { id: 'org-1', name: 'Pavillon 46', isOwner: false, role: 1, permissions: ['records.edit'] };
    render(<LeadKindsCard />);
    expect(screen.getByRole('radio', { name: /establishments/i })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
    expect(screen.getByText(/Manage settings permission/)).toBeInTheDocument();
  });

  it('is not offered in a demo workspace, which has no server to keep it', () => {
    state.demo = true;
    const { container } = render(<LeadKindsCard />);
    expect(container).toBeEmptyDOMElement();
  });

  it('is not offered while the workspace has the Sales module, where leads live, turned off', () => {
    state.org = { ...state.org!, salesEnabled: false };
    const { container } = render(<LeadKindsCard />);
    expect(container).toBeEmptyDOMElement();
  });
});
