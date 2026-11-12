// A workspace can turn its Sales or Club module off. The menu leaves the module out, its
// pages say it is turned off (the server refuses their requests), sign-in lands on what
// is on, and Settings turns a module on or off, showing the server's reason when it says no.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ModuleOutlet, SalesHome } from './ModuleOff';
import { AppSideNav } from './AppSideNav';
import { ModulesCard } from '@/app/pages/settings/components/ModulesCard';
import { PERMISSION, type Permission } from '@/app/api/organizations';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown>, refresh: vi.fn() }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

const api = vi.hoisted(() => ({ update: vi.fn() }));
vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  updateWorkspaceModules: (...args: unknown[]) => api.update(...args),
}));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

/** Signed in to a workspace with these modules on, holding these permissions (the owner by default). */
function workspace({ sales = true, club = true, permissions }: { sales?: boolean; club?: boolean; permissions?: Permission[] } = {}) {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    refreshOrgs: org.refresh,
    currentOrg: {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: !permissions, role: permissions ? 1 : 0,
      permissions, salesEnabled: sales, clubEnabled: club,
    },
  };
}

beforeEach(() => {
  org.refresh.mockReset().mockResolvedValue(undefined);
  api.update.mockReset();
});

const atPath = (path: string, routes: React.ReactNode) =>
  render(<MemoryRouter initialEntries={[path]}><Routes>{routes}</Routes></MemoryRouter>);

describe('a module turned off', () => {
  it('shows its pages while it is on', () => {
    workspace();
    atPath('/members', (
      <Route element={<ModuleOutlet module="club" />}>
        <Route path="/members" element={<p>The members list</p>} />
      </Route>
    ));
    expect(screen.getByText('The members list')).toBeInTheDocument();
  });

  it('says so on a page reached by a link, offers Settings to whoever can turn it on, and asks nothing of the page', () => {
    workspace({ club: false });
    const page = vi.fn(() => <p>The members list</p>);
    atPath('/members', (
      <Route element={<ModuleOutlet module="club" />}>
        <Route path="/members" element={page()} />
      </Route>
    ));
    expect(screen.getByRole('heading', { name: 'The Club module is turned off' })).toBeInTheDocument();
    expect(screen.getByText(/Nothing in it was deleted/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open Settings' })).toHaveAttribute('href', '/settings?tab=organization');
    expect(screen.queryByText('The members list')).not.toBeInTheDocument();
  });

  it('tells someone who cannot turn it on who can', () => {
    workspace({ sales: false, permissions: [PERMISSION.ClubMembers, PERMISSION.FrontDesk] });
    atPath('/leads', (
      <Route element={<ModuleOutlet module="sales" />}>
        <Route path="/leads" element={<p>The leads</p>} />
      </Route>
    ));
    expect(screen.getByRole('heading', { name: 'The Sales module is turned off' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Open Settings' })).not.toBeInTheDocument();
    expect(screen.getByText(/Someone who manages the workspace’s settings/)).toBeInTheDocument();
  });

  it('lands sign-in on the club’s day when Sales is off, and on the dashboard when it is on', () => {
    workspace({ sales: false });
    const routes = (
      <>
        <Route path="/dashboard" element={<SalesHome><p>The sales dashboard</p></SalesHome>} />
        <Route path="/hospitality" element={<p>Today at the Club</p>} />
      </>
    );
    const { unmount } = atPath('/dashboard', routes);
    expect(screen.getByText('Today at the Club')).toBeInTheDocument();
    expect(screen.queryByText('The sales dashboard')).not.toBeInTheDocument();
    unmount();

    workspace();
    atPath('/dashboard', routes);
    expect(screen.getByText('The sales dashboard')).toBeInTheDocument();
  });
});

describe('the menu', () => {
  const openMenu = async () => {
    render(<MemoryRouter initialEntries={['/contacts']}><AppSideNav /></MemoryRouter>);
    await userEvent.click(screen.getByRole('button', { name: 'Open menu' }));
    return within(screen.getByRole('navigation', { name: 'Main navigation' }));
  };

  it('leaves the club out with the Club module off, and keeps the calendar with the work', async () => {
    workspace({ club: false });
    const nav = await openMenu();
    expect(nav.queryByText('Members')).not.toBeInTheDocument();
    expect(nav.queryByText('House accounts')).not.toBeInTheDocument();
    // The house-charge screen and the POS tickets are the club's too.
    expect(nav.queryByText('House charge')).not.toBeInTheDocument();
    expect(nav.queryByText('POS tickets')).not.toBeInTheDocument();
    expect(nav.queryByRole('button', { name: /^Club$/ })).not.toBeInTheDocument();
    expect(nav.getByText('Leads')).toBeInTheDocument();
    expect(nav.getAllByText('Calendar')).toHaveLength(1);
  });

  it('leaves sales out with the Sales module off, and calls what is left of the pipeline Work', async () => {
    workspace({ sales: false });
    const nav = await openMenu();
    expect(nav.queryByText('Leads')).not.toBeInTheDocument();
    expect(nav.queryByText('Deals')).not.toBeInTheDocument();
    expect(nav.queryByText('Reports')).not.toBeInTheDocument();
    expect(nav.queryByText('Dashboard')).not.toBeInTheDocument();
    expect(nav.getByRole('button', { name: /^Work$/ })).toBeInTheDocument();
    expect(nav.getByText('Members')).toBeInTheDocument();
    expect(nav.getByText('Contacts')).toBeInTheDocument();
  });

  it('reads in the person’s language, and says what is left of the pipeline in it', async () => {
    const { I18nProvider, loadCatalogue } = await import('@/app/i18n');
    await loadCatalogue('de');
    localStorage.setItem('cadence.language', 'de');
    try {
      workspace({ sales: false });
      render(<I18nProvider><MemoryRouter initialEntries={['/members']}><AppSideNav /></MemoryRouter></I18nProvider>);
      await userEvent.click(screen.getByRole('button', { name: 'Menü öffnen' }));
      const nav = within(screen.getByRole('navigation', { name: 'Hauptnavigation' }));
      expect(nav.getByRole('button', { name: /^Arbeit$/ })).toBeInTheDocument();
      expect(nav.getByText('Mitglieder')).toBeInTheDocument();
      expect(nav.getByText('Clubkonten')).toBeInTheDocument();
      expect(nav.queryByText('Leads')).not.toBeInTheDocument();
      // The page filter matches the words shown.
      await userEvent.type(screen.getByPlaceholderText('Seite suchen'), 'abonn');
      expect(nav.getByText('Abonnemente')).toBeInTheDocument();
      expect(nav.queryByText('Mitglieder')).not.toBeInTheDocument();
    } finally {
      localStorage.removeItem('cadence.language');
    }
  });

  it('offers privacy requests only to someone who may handle them', async () => {
    workspace({ permissions: [PERMISSION.EditRecords, PERMISSION.DeleteRecords, PERMISSION.ExportRecords] });
    const { unmount } = render(<MemoryRouter initialEntries={['/contacts']}><AppSideNav /></MemoryRouter>);
    await userEvent.click(screen.getByRole('button', { name: 'Open menu' }));
    expect(within(screen.getByRole('navigation', { name: 'Main navigation' })).queryByText('Privacy requests')).not.toBeInTheDocument();
    unmount();

    workspace({ permissions: [PERMISSION.ManagePrivacy] });
    const nav = await openMenu();
    expect(nav.getByText('Privacy requests')).toBeInTheDocument();
  });
});

describe('turning modules on and off in Settings', () => {
  it('turns the club off and reads the workspace again', async () => {
    workspace();
    api.update.mockResolvedValue({ sales: true, club: false });
    render(<ModulesCard />);
    await userEvent.click(screen.getByRole('button', { name: 'Turn Club off' }));
    await waitFor(() => expect(api.update).toHaveBeenCalledWith('org-1', { club: false }));
    expect(org.refresh).toHaveBeenCalled();
  });

  it('shows why the server would not turn it off', async () => {
    workspace();
    api.update.mockRejectedValue(new Error('The Club module still has work running on its own: automatic billing (Club settings, Billing). Stop that first, then turn the module off.'));
    render(<ModulesCard />);
    await userEvent.click(screen.getByRole('button', { name: 'Turn Club off' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('automatic billing');
  });

  it('keeps the last module on, and offers nothing to someone who cannot change settings', () => {
    workspace({ sales: false });
    const { unmount } = render(<ModulesCard />);
    expect(screen.getByRole('button', { name: 'Turn Club off' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Turn Sales on' })).toBeEnabled();
    unmount();

    workspace({ permissions: [PERMISSION.EditRecords] });
    render(<ModulesCard />);
    expect(screen.queryByRole('button', { name: /Turn/ })).not.toBeInTheDocument();
  });
});
