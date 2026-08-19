// What the launchpad has to get right.
//
// The animation is deliberately not under test — happy-dom has no compositor and
// Framer's output is unobservable here. What IS tested is everything the redesign
// could plausibly have broken or that the old page got wrong: which workspace a
// keystroke picks, where you land afterwards, that one launch cannot become two,
// and that a failed request is never rendered as a real number.
//
// LaunchSequence is stubbed so the test decides when the flight ends. Otherwise
// every assertion would wait on a real 1.3s animation, and the thing being pinned
// here is the page's logic, not Framer's timing.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import type { Organization } from '@/app/api/organizations';

/* ----------------------------------------------------------------- test doubles */

const org = (id: string, name: string, extra: Partial<Organization> = {}): Organization => ({
  id, name, ownerUserId: 'me', isOwner: true, role: 0, ...extra,
});

const ORGS = [
  org('o-lac', 'Lac Léman SA'),
  org('o-p46', 'Pavillon 46'),
  org('o-zur', 'Zürich Nord', { isOwner: false, role: 1 }),
];

const ctx = {
  organizations: ORGS as Organization[],
  pendingInvites: [] as never[],
  currentOrgId: null as string | null,
  currentOrg: null,
  isReadOnly: false,
  setCurrentOrg: vi.fn(),
  refreshOrgs: vi.fn(async () => {}),
  loading: false,
  hasFetched: true,
};

vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ctx,
  useOrgOptional: () => ctx,
}));

const getOrgMembers = vi.fn(async (_id: string) => [{ userId: 'a', name: 'A', email: 'a@b.c', role: 0 }]);
const listPendingJoinRequestsForOrg = vi.fn(async (_id: string) => [] as never[]);

vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  getOrgMembers: (id: string) => getOrgMembers(id),
  listPendingJoinRequestsForOrg: (id: string) => listPendingJoinRequestsForOrg(id),
  acceptInviteById: vi.fn(),
  acceptJoinRequest: vi.fn(),
  rejectJoinRequest: vi.fn(),
}));

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  isUsingRealApi: () => true,
}));

// Real prefetching would dynamically import every page module into the test run.
const prefetchRoute = vi.fn();
vi.mock('./organizations/prefetchRoute', () => ({
  prefetchRoute: (p: string) => prefetchRoute(p),
  routeChunkKey: () => null,
  PREFETCHABLE: [],
}));

// The stub exposes the one thing the page cares about: when the flight finishes.
vi.mock('./organizations/LaunchSequence', () => ({
  __esModule: true,
  default: ({ org: o, destinationLabel, onComplete }: {
    org: Organization; destinationLabel: string; onComplete: () => void;
  }) => (
    <div data-testid="launch-stub">
      <span>launching {o.name}</span>
      <span>to {destinationLabel}</span>
      <button type="button" onClick={onComplete}>finish-flight</button>
    </div>
  ),
}));

import Organizations from './Organizations';

/* ------------------------------------------------------------------- harness */

function Probe() {
  const loc = useLocation();
  return <div data-testid="landed">{loc.pathname + loc.search}</div>;
}

async function renderPage(state?: Record<string, unknown>) {
  const result = render(
    <MemoryRouter initialEntries={[{ pathname: '/organizations', state }]}>
      <Routes>
        <Route path="/organizations" element={<Organizations />} />
        <Route path="*" element={<Probe />} />
      </Routes>
    </MemoryRouter>,
  );
  // The counts/join-requests effect settles a tick after mount. Flushing it here
  // rather than letting it land mid-assertion keeps React's act() warning
  // meaningful instead of turning it into background noise in every test.
  await act(async () => { await Promise.resolve(); });
  return result;
}

const tile = (name: string) => screen.getByRole('button', { name: new RegExp(`Open ${name}`, 'i') });

beforeEach(() => {
  ctx.currentOrgId = null;
  ctx.organizations = ORGS;
  ctx.setCurrentOrg.mockClear();
  prefetchRoute.mockClear();
  getOrgMembers.mockClear();
  getOrgMembers.mockImplementation(async () => [{ userId: 'a', name: 'A', email: 'a@b.c', role: 0 }]);
  listPendingJoinRequestsForOrg.mockClear();
  listPendingJoinRequestsForOrg.mockImplementation(async () => []);
});

/* --------------------------------------------------------------------- tests */

describe('picking a workspace', () => {
  it('shows every workspace, in a stable order', async () => {
    await renderPage();
    const names = screen.getAllByRole('button', { name: /^Open / })
      .map((b) => b.getAttribute('aria-label'));
    expect(names).toEqual([
      'Open Lac Léman SA', 'Open Pavillon 46', 'Open Zürich Nord',
    ]);
  });

  it('puts the current workspace first and marks it', async () => {
    ctx.currentOrgId = 'o-zur';
    await renderPage();
    const first = screen.getAllByRole('button', { name: /^Open / })[0]!;
    expect(first).toHaveAttribute('aria-label', 'Open Zürich Nord (current workspace)');
    expect(first).toHaveAttribute('aria-current', 'true');
  });

  it('launches the workspace a digit names, and lands on the dashboard', async () => {
    const user = userEvent.setup();
    await renderPage();
    await user.keyboard('2');
    expect(screen.getByTestId('launch-stub')).toHaveTextContent('launching Pavillon 46');

    await user.click(screen.getByRole('button', { name: 'finish-flight' }));
    await waitFor(() => expect(screen.getByTestId('landed')).toHaveTextContent('/dashboard'));
    expect(ctx.setCurrentOrg).toHaveBeenCalledExactlyOnceWith('o-p46');
  });

  it('sends you back to the page you were actually trying to reach', async () => {
    // RequireOrgLayout redirects here with state.from. The old page discarded it
    // and dumped everyone on the dashboard.
    const user = userEvent.setup();
    await renderPage({ from: '/leads/abc-123' });
    await user.click(tile('Pavillon 46'));
    await user.click(screen.getByRole('button', { name: 'finish-flight' }));
    await waitFor(() => expect(screen.getByTestId('landed')).toHaveTextContent('/leads/abc-123'));
  });

  it('refuses a destination that would leave the app', async () => {
    const user = userEvent.setup();
    await renderPage({ from: '//evil.example/x' });
    await user.click(tile('Pavillon 46'));
    await user.click(screen.getByRole('button', { name: 'finish-flight' }));
    await waitFor(() => expect(screen.getByTestId('landed')).toHaveTextContent('/dashboard'));
  });

  it('cannot start a second launch while one is in flight', async () => {
    // The tile is clickable, Enter-activatable AND digit-selectable, so without a
    // guard a fast double input starts two flights and navigates twice.
    const user = userEvent.setup();
    await renderPage();
    await user.keyboard('2');
    await user.keyboard('3');
    await user.keyboard('{Enter}');

    const stubs = screen.getAllByTestId('launch-stub');
    expect(stubs).toHaveLength(1);
    expect(stubs[0]!).toHaveTextContent('launching Pavillon 46');

    await user.click(screen.getByRole('button', { name: 'finish-flight' }));
    await waitFor(() => expect(screen.getByTestId('landed')).toBeInTheDocument());
    expect(ctx.setCurrentOrg).toHaveBeenCalledTimes(1);
  });

  it('does not switch the workspace when you re-open the one you are in', async () => {
    ctx.currentOrgId = 'o-p46';
    const user = userEvent.setup();
    await renderPage();
    await user.click(tile('Pavillon 46'));
    await user.click(screen.getByRole('button', { name: 'finish-flight' }));
    await waitFor(() => expect(screen.getByTestId('landed')).toBeInTheDocument());
    expect(ctx.setCurrentOrg).not.toHaveBeenCalled();
  });

  it('Enter on another control does that control, not a launch', async () => {
    // "New workspace" is a real button. Enter used to open its dialog AND fire the
    // highlighted tile's launch, so the overlay covered the dialog and navigated.
    const user = userEvent.setup();
    await renderPage();
    screen.getByRole('button', { name: /New workspace/i }).focus();
    await user.keyboard('{Enter}');
    expect(screen.queryByTestId('launch-stub')).not.toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('Enter on a focused tile launches that tile, once', async () => {
    const user = userEvent.setup();
    await renderPage();
    tile('Zürich Nord').focus();
    await user.keyboard('{Enter}');
    const stubs = screen.getAllByTestId('launch-stub');
    expect(stubs).toHaveLength(1);
    expect(stubs[0]!).toHaveTextContent('launching Zürich Nord');
  });

  it('starts fetching the destination before the flight ends', async () => {
    const user = userEvent.setup();
    await renderPage({ from: '/reports' });
    await user.keyboard('1');
    expect(prefetchRoute).toHaveBeenCalledWith('/reports');
  });
});

describe('searching', () => {
  it('matches without accents, because the names have them', async () => {
    const user = userEvent.setup();
    await renderPage();
    await user.type(screen.getByLabelText('Search workspaces'), 'leman');
    expect(screen.getAllByRole('button', { name: /^Open / })).toHaveLength(1);
    expect(tile('Lac Léman SA')).toBeInTheDocument();
  });

  it('Enter opens the match, not whatever was first before filtering', async () => {
    const user = userEvent.setup();
    await renderPage();
    await user.type(screen.getByLabelText('Search workspaces'), 'zurich');
    await user.keyboard('{Enter}');
    expect(screen.getByTestId('launch-stub')).toHaveTextContent('launching Zürich Nord');
  });

  it('lets you type a digit into the search box instead of hijacking it', async () => {
    // "Pavillon 46" is exactly the case a bare digit shortcut would make
    // unsearchable, so digits only select while the box is empty.
    const user = userEvent.setup();
    await renderPage();
    await user.type(screen.getByLabelText('Search workspaces'), 'Pavillon 4');
    await user.keyboard('6');
    expect(screen.queryByTestId('launch-stub')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Open / })).toHaveLength(1);
    expect(tile('Pavillon 46')).toBeInTheDocument();
  });

  it('says so when nothing matches, and offers a way out', async () => {
    const user = userEvent.setup();
    await renderPage();
    await user.type(screen.getByLabelText('Search workspaces'), 'zzz');
    expect(screen.getByText(/No workspace matches/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Clear the search/i }));
    expect(screen.getAllByRole('button', { name: /^Open / })).toHaveLength(3);
  });

  it('Enter does nothing when the filter has emptied the list', async () => {
    const user = userEvent.setup();
    await renderPage();
    await user.type(screen.getByLabelText('Search workspaces'), 'zzz');
    await user.keyboard('{Enter}');
    expect(screen.queryByTestId('launch-stub')).not.toBeInTheDocument();
  });

  it('Escape clears the search', async () => {
    const user = userEvent.setup();
    await renderPage();
    const box = screen.getByLabelText('Search workspaces');
    await user.type(box, 'leman');
    await user.keyboard('{Escape}');
    expect(box).toHaveValue('');
    expect(screen.getAllByRole('button', { name: /^Open / })).toHaveLength(3);
  });
});

describe('member counts', () => {
  it('shows the count it read', async () => {
    await renderPage();
    await waitFor(() => expect(within(tile('Pavillon 46')).getByText('1 member')).toBeInTheDocument());
  });

  it('never renders a failed read as a number', async () => {
    // The old effect swallowed the error and left the count absent, so a broken
    // endpoint was indistinguishable from a workspace with no members.
    getOrgMembers.mockImplementation(async () => { throw new Error('502'); });
    await renderPage();
    await waitFor(() => expect(screen.getByText(/could not be loaded/i)).toBeInTheDocument());
    expect(within(tile('Pavillon 46')).getByText('—')).toBeInTheDocument();
    expect(screen.queryByText(/0 members/)).not.toBeInTheDocument();
  });

  it('warns when the join-request list may be incomplete', async () => {
    listPendingJoinRequestsForOrg.mockImplementation(async () => { throw new Error('502'); });
    await renderPage();
    await waitFor(() => expect(screen.getByText(/may be incomplete/i)).toBeInTheDocument());
  });

  it('says nothing about failures when everything loaded', async () => {
    await renderPage();
    await waitFor(() => expect(getOrgMembers).toHaveBeenCalled());
    expect(screen.queryByText(/could not be loaded/i)).not.toBeInTheDocument();
  });
});

describe('what it offers whom', () => {
  it('offers workspace settings to an owner', async () => {
    ctx.currentOrgId = 'o-p46';
    await renderPage();
    expect(screen.getByRole('link', { name: /workspace settings/i })).toBeInTheDocument();
  });

  it('hides workspace settings from a plain member', async () => {
    // The backend rejects the writes on that page, so offering it only produces
    // a failure. The old card menu showed it to everyone.
    ctx.currentOrgId = 'o-zur';
    await renderPage();
    expect(screen.queryByRole('link', { name: /workspace settings/i })).not.toBeInTheDocument();
  });

  it('offers to create a workspace when there are none', async () => {
    ctx.organizations = [];
    await renderPage();
    expect(screen.getByRole('button', { name: /Create your first workspace/i })).toBeInTheDocument();
    expect(screen.queryByLabelText('Search workspaces')).not.toBeInTheDocument();
  });

  it('hides the search box for a single workspace', async () => {
    ctx.organizations = [ORGS[0]!];
    await renderPage();
    expect(screen.queryByLabelText('Search workspaces')).not.toBeInTheDocument();
    expect(tile('Lac Léman SA')).toBeInTheDocument();
  });
});
