// The sales agent's panel, as a salesperson meets it.
//
// The lists come from the shared store (agentData.ts), mocked here at the API
// boundary so the store's real loading, freshness and scoping run underneath.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

vi.mock('@/app/api', () => ({
  getLeads: vi.fn(), getDeals: vi.fn(), getTasks: vi.fn(), getContacts: vi.fn(), getCompanies: vi.fn(),
}));
vi.mock('@/app/api/sequences', () => ({ getEnrollments: vi.fn(), getSequences: vi.fn() }));
vi.mock('@/app/api/automations', () => ({ getAutomations: vi.fn() }));
vi.mock('@/app/api/settings', () => ({
  getUserSettings: vi.fn(async () => ({})),
  saveUserSettings: vi.fn(async (value: unknown) => value),
}));

import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { announceDataChanged } from '@/app/api/dataChanged';
import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { NextActions } from './NextActions';
import { NextActionsSlot } from './NextActionsSlot';
import { resetAgentDataForTests, SETTLE_MS } from './agentData';

const leads = vi.mocked(getLeads as () => Promise<unknown>);
const deals = vi.mocked(getDeals as () => Promise<unknown>);
const others = [getTasks, getContacts, getCompanies, getAutomations, getEnrollments, getSequences]
  .map((fn) => vi.mocked(fn as () => Promise<unknown>));

const lead = (id: string, name: string, pipeline: object) => ({
  id, name, email: `${id}@example.com`, status: 'New', pipelineState: JSON.stringify(pipeline),
});
const lost = (id: string, name: string) => lead(id, name, { outreachStatus: 'contacted', contactOutcome: 'not_interested' });

function at(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <NextActions />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  resetAgentDataForTests();
  localStorage.clear();
  leads.mockReset();
  deals.mockReset();
  leads.mockResolvedValue([]);
  deals.mockResolvedValue([]);
  for (const fn of others) { fn.mockReset(); fn.mockResolvedValue([]); }
});

afterEach(() => {
  resetAgentDataForTests();
  vi.useRealTimers();
});

describe('the queue', () => {
  it('shows the highest step and links to that record', async () => {
    leads.mockResolvedValue([lost('l1', 'Ada')]);
    at('/dashboard');

    expect(await screen.findByRole('link', { name: 'Ada' })).toHaveAttribute('href', '/leads/l1');
    expect(screen.getByRole('heading', { name: 'Do this next' })).toBeInTheDocument();
    expect(screen.getByText(/Record why they dropped out/)).toBeInTheDocument();
  });

  it('closes down to its icons, stays closed on other pages, and opens again from them', async () => {
    leads.mockResolvedValue(['A', 'B', 'C'].map((n) => lost(n, `Lead ${n}`)));
    const user = userEvent.setup();
    const { unmount } = at('/dashboard');
    await screen.findByRole('heading', { name: 'Do this next' });

    await user.click(screen.getByRole('button', { name: 'Close next steps' }));
    expect(screen.queryByRole('heading', { name: 'Do this next' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Open next steps, 3 waiting' })).toBeInTheDocument();

    // Another page: still closed until opened.
    unmount();
    at('/leads');
    const reopen = await screen.findByRole('button', { name: 'Open next steps, 3 waiting' });
    await user.click(reopen);
    expect(await screen.findByRole('heading', { name: 'Do this next' })).toBeInTheDocument();
  });

  it('the closed chat icon opens the panel with the chat open', async () => {
    leads.mockResolvedValue([lost('l1', 'Ada')]);
    localStorage.setItem('cadence.nextActions.panelOpen', 'false');
    const user = userEvent.setup();
    at('/dashboard');
    await user.click(await screen.findByRole('button', { name: 'Ask Cadence AI' }));
    expect(await screen.findByLabelText('Ask the agent')).toBeInTheDocument();
    expect(document.activeElement).toBe(screen.getByLabelText('Ask the agent'));
  });

  it('opens with the full queue on the dashboard', async () => {
    leads.mockResolvedValue(['A', 'B', 'C', 'D', 'E', 'F', 'G'].map((n) => lost(n, `Lead ${n}`)));
    at('/dashboard');

    const list = await screen.findByRole('list');
    expect(within(list).getAllByRole('listitem')).toHaveLength(5);
    expect(screen.getByText(/2 more after these/)).toBeInTheDocument();
  });

  it('elsewhere shows only the top step, with the rest one click away — and remembers', async () => {
    leads.mockResolvedValue(['A', 'B', 'C'].map((n) => lost(n, `Lead ${n}`)));
    const user = userEvent.setup();
    const { unmount } = at('/leads');

    const list = await screen.findByRole('list');
    expect(within(list).getAllByRole('listitem')).toHaveLength(1);
    const toggle = screen.getByRole('button', { name: /Show all 3/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');

    await user.click(toggle);
    expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(3);
    expect(screen.getByRole('button', { name: /Show less/ })).toHaveAttribute('aria-expanded', 'true');

    // Another page: it stays the way it was left.
    unmount();
    at('/contacts');
    await screen.findByRole('heading', { name: 'Do this next' });
    expect(screen.queryByRole('button', { name: /Show all/ })).toBeNull();
  });

  it('on a quiet page it is one line, and asking opens the chat with the cursor in it', async () => {
    const user = userEvent.setup();
    at('/contacts');
    expect(await screen.findByText('Nothing on this page is waiting.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Ask the agent')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Ask Cadence AI' }));
    expect(await screen.findByLabelText('Ask the agent')).toBeInTheDocument();
    expect(document.activeElement).toBe(screen.getByLabelText('Ask the agent'));
  });

  it('with steps waiting, the closed chat is a pill on the title row', async () => {
    leads.mockResolvedValue([lost('l1', 'Ada')]);
    const user = userEvent.setup();
    at('/dashboard');
    await screen.findByRole('link', { name: 'Ada' });
    expect(screen.queryByLabelText('Ask the agent')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Ask Cadence AI' }));
    expect(await screen.findByLabelText('Ask the agent')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ada' })).toBeInTheDocument();
  });

  it('says so when nothing is waiting', async () => {
    at('/dashboard');
    expect(await screen.findByText('You are all caught up.')).toBeInTheDocument();
  });

  it('offers a retry when the steps cannot be loaded', async () => {
    leads.mockRejectedValue(new Error('down'));
    deals.mockRejectedValue(new Error('down'));
    for (const fn of others) fn.mockRejectedValue(new Error('down'));
    at('/dashboard');

    const status = await screen.findByRole('status');
    expect(status).toHaveTextContent(/could not be loaded/);
    expect(screen.queryByRole('heading', { name: 'Do this next' })).toBeNull();

    leads.mockResolvedValue([lost('l1', 'Ada')]);
    deals.mockResolvedValue([]);
    for (const fn of others) fn.mockResolvedValue([]);
    await userEvent.setup().click(screen.getByRole('button', { name: /Try again/ }));
    expect(await screen.findByRole('link', { name: 'Ada' })).toBeInTheDocument();
  });
});

describe('where it shows', () => {
  it('stays off the till and the member-portal preview', async () => {
    leads.mockResolvedValue([lost('l1', 'Ada')]);
    const { container } = at('/pos');
    await act(async () => { await Promise.resolve(); });
    expect(container).toBeEmptyDOMElement();
  });

  it('on the lead routing page it is the leads queue, not an empty "plan"', async () => {
    leads.mockResolvedValue([lost('l1', 'Ada')]);
    at('/leads/routing');
    expect(await screen.findByRole('heading', { name: 'Do this next' })).toBeInTheDocument();
    expect(screen.queryByText(/no saved plan/i)).toBeNull();
  });
});

describe('on a record', () => {
  it("shows that lead's one next step — not a copy of the lifecycle the page already has", async () => {
    leads.mockResolvedValue([lead('l1', 'Ada', {})]);
    at('/leads/l1');

    expect(await screen.findByRole('heading', { name: /Next for this lead/ })).toBeInTheDocument();
    expect(screen.getByText('Make the first call and log whether they answered.')).toBeInTheDocument();
    // The old panel listed all five phases here, four of them as empty "Later" rows.
    expect(screen.queryByText('Later')).toBeNull();
    expect(screen.queryByText('Deposit')).toBeNull();
  });

  it('shows nothing on a record with nothing owed', async () => {
    leads.mockResolvedValue([{ ...lost('l1', 'Ada'), pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested', dropoutReason: 'too_soon' }) }]);
    const { container } = at('/leads/l1');
    // Wait for the first load to finish — the placeholder is not "nothing".
    await waitFor(() => expect(screen.queryByLabelText('Loading next steps')).toBeNull());
    expect(container).toBeEmptyDOMElement();
  });

  it('shows a deal its stage ladder', async () => {
    deals.mockResolvedValue([{ id: 'd1', name: 'Big deal', dealStageName: 'Proposal', expectedCloseDateUtc: '2099-01-01' }]);
    at('/deals/d1');

    const ladder = await screen.findByRole('list', { name: 'Deal stages' });
    expect(within(ladder).getByText('Qualification')).toBeInTheDocument();
    expect(within(ladder).getByText(/Proposal/).textContent).toMatch(/current stage/);
  });

  it('catches up with an edit made on the page beneath it', async () => {
    // The bug, end to end: log "no answer" on the lead, and the panel above kept
    // saying "make the first call" — telling you to do what you had just done.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    leads.mockResolvedValue([lead('l1', 'Ada', {})]);
    at('/leads/l1');
    await act(async () => { for (let i = 0; i < 5; i += 1) await Promise.resolve(); });
    expect(screen.getByText('Make the first call and log whether they answered.')).toBeInTheDocument();

    // The page saves the pipeline; the save announces itself.
    leads.mockResolvedValue([lead('l1', 'Ada', { outreachStatus: 'attempted_no_answer' })]);
    announceDataChanged();
    await act(async () => { vi.advanceTimersByTime(SETTLE_MS); });
    await act(async () => { for (let i = 0; i < 5; i += 1) await Promise.resolve(); });

    expect(screen.queryByText('Make the first call and log whether they answered.')).toBeNull();
    expect(screen.getByText(/Call again at a different time of day/)).toBeInTheDocument();
  });

  it('sits right after the page title when the page gives it a slot', async () => {
    leads.mockResolvedValue([lost('l1', 'Ada')]);
    render(
      <MemoryRouter initialEntries={['/leads']}>
        <NextActions />
        <main>
          <h1>Leads</h1>
          <NextActionsSlot />
          <p>The page</p>
        </main>
      </MemoryRouter>,
    );
    const heading = await screen.findByRole('heading', { name: 'Do this next' });
    const slot = heading.closest('[data-next-actions-slot]');
    expect(slot).not.toBeNull();
    expect(slot!.previousElementSibling).toHaveTextContent('Leads');
    expect(slot!.nextElementSibling).toHaveTextContent('The page');
  });

  it('leaves no gap when a record owes nothing', async () => {
    leads.mockResolvedValue([lost('l1', 'Ada')]);
    const { container } = render(
      <MemoryRouter initialEntries={['/contacts/c9']}>
        <NextActions />
        <NextActionsSlot />
      </MemoryRouter>,
    );
    await waitFor(() => expect(container.querySelector('[aria-label="Loading next steps"]')).toBeNull());
    const slot = container.querySelector('[data-next-actions-slot]')!;
    expect(slot).toBeEmptyDOMElement();
    expect(slot.className).toContain('empty:hidden');
  });
});

describe('every page places the panel after its title', () => {
  it('each page with the app header has a slot, or uses PageHero (which has one)', () => {
    // The panel stays under the header on a page without a slot, so a page that
    // forgets one still works — but it looks out of place. The till and the
    // member-portal preview never show the panel.
    const pages = resolve(__dirname, '../pages');
    const withHeader = readdirSync(pages)
      .filter((file) => file.endsWith('.tsx') && !file.includes('.test.'))
      .map((file) => ({ file, source: readFileSync(join(pages, file), 'utf8') }))
      .filter(({ source }) => source.includes('<AppHeader'));
    // The dashboard's title is its own DashboardHero, which is a PageHero.
    const missing = withHeader
      .filter(({ source }) => !source.includes('<NextActionsSlot') && !source.includes('<PageHero') && !source.includes('<DashboardHero'))
      .map(({ file }) => file);
    expect(missing).toEqual([]);
    // The lead page leaves the header's slot out only to place the panel in its own
    // main column; the till and the portal preview leave the panel out altogether.
    const optedOut = withHeader
      .filter(({ source }) => source.includes('noAgentSlot') && !source.includes('<NextActionsSlot'))
      .map(({ file }) => file);
    expect(optedOut.sort()).toEqual(['MemberPortal.tsx', 'PosTerminal.tsx']);
    expect(readFileSync(resolve(__dirname, '../components/PageHero.tsx'), 'utf8')).toContain('<NextActionsSlot');
    expect(readFileSync(resolve(pages, 'dashboard/DashboardHero.tsx'), 'utf8')).toContain('<PageHero');
  });
});
