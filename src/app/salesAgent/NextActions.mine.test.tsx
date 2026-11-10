// The assistant's queue in a workspace: the rep's own work by default, from the server,
// and the team's one switch away.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
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
vi.mock('@/app/api/workQueue', () => ({ getWorkQueue: vi.fn() }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrgId: 'org-1', isReadOnly: false }),
}));

import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { getWorkQueue } from '@/app/api/workQueue';
import { NextActions } from './NextActions';
import { withQueries } from '@/test/queryWrapper';

const leads = vi.mocked(getLeads as () => Promise<unknown>);
const workQueue = vi.mocked(getWorkQueue);
const others = [getDeals, getTasks, getContacts, getCompanies, getAutomations, getEnrollments, getSequences]
  .map((fn) => vi.mocked(fn as () => Promise<unknown>));

// Both lost with no reason recorded: "record why", the top of any queue.
const lost = (id: string, name: string) => ({
  id, name, email: `${id}@example.com`, status: 'New',
  pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }),
});
const TEAMS_LEAD = lost('l-team', "Luca's lead");
const MY_LEAD = lost('l-mine', 'My lead');

function at(path: string) {
  return render(withQueries(
    <MemoryRouter initialEntries={[path]}>
      <NextActions />
    </MemoryRouter>,
  ));
}

beforeEach(() => {
  localStorage.clear();
  leads.mockReset();
  leads.mockResolvedValue([TEAMS_LEAD, MY_LEAD]);
  for (const fn of others) { fn.mockReset(); fn.mockResolvedValue([]); }
  workQueue.mockReset();
  workQueue.mockResolvedValue({ scope: 'mine', tasks: [], leads: [MY_LEAD] as never, deals: [], truncated: false });
});


describe('the queue in a workspace', () => {
  it("shows the rep's own work from the server, not every rep's", async () => {
    at('/dashboard');

    expect(await screen.findByRole('link', { name: 'My lead' })).toHaveAttribute('href', '/leads/l-mine');
    expect(screen.queryByRole('link', { name: "Luca's lead" })).toBeNull();
    expect(workQueue).toHaveBeenCalledWith('mine', expect.objectContaining({ signal: expect.any(AbortSignal) }));
    expect(screen.getByRole('button', { name: 'Mine' })).toHaveAttribute('aria-pressed', 'true');
  });

  it("shows the team's work on the switch, and remembers it", async () => {
    const user = userEvent.setup();
    at('/dashboard');
    await screen.findByRole('link', { name: 'My lead' });

    await user.click(screen.getByRole('button', { name: 'Team' }));

    expect(await screen.findByRole('link', { name: "Luca's lead" })).toBeInTheDocument();
    expect(localStorage.getItem('cadence.workScope.assistant')).toBe('team');
  });

  it('says which of the rep\'s work was sent when the server held some back', async () => {
    // The leads the server sends are the ones worked most lately, not the most pressing.
    workQueue.mockResolvedValue({ scope: 'mine', tasks: [], leads: [MY_LEAD] as never, deals: [], truncated: true });
    at('/dashboard');

    expect(await screen.findByText(
      'More of your work is open than one read holds. Here: the tasks due and deals closing soonest, and the leads worked most lately.',
    )).toBeInTheDocument();
  });

  it('says when nothing of the rep is waiting, rather than that everyone is caught up', async () => {
    workQueue.mockResolvedValue({ scope: 'mine', tasks: [], leads: [], deals: [], truncated: false });
    at('/dashboard');

    expect(await screen.findByText('Nothing of yours is waiting.')).toBeInTheDocument();
  });

  it('says so when the rep\'s work cannot be read, and reads it again on request', async () => {
    workQueue.mockRejectedValueOnce(new Error('down'));
    const user = userEvent.setup();
    at('/dashboard');

    await user.click(await screen.findByRole('button', { name: /Try again/ }));

    expect(await screen.findByRole('link', { name: 'My lead' })).toBeInTheDocument();
  });

  it("shows a record's own step on its page, whoever's it is", async () => {
    at('/leads/l-team');

    await waitFor(() => expect(screen.getByText(/Record why they dropped out/)).toBeInTheDocument());
    expect(workQueue).not.toHaveBeenCalled();
  });
});
