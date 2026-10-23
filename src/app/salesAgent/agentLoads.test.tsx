// The panel's lists are read once per workspace and reused across pages.
//
// The panel mounts on every page. Before the shared store, each navigation re-read
// eight lists. These pin that a fresh store reads them, and a second mount reads none.

import { describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { resetAgentDataForTests } from './agentData';
import { NextActions } from './NextActions';

vi.mock('@/app/api', () => ({
  getLeads: vi.fn(),
  getDeals: vi.fn(),
  getTasks: vi.fn(),
  getContacts: vi.fn(),
  getCompanies: vi.fn(),
}));
vi.mock('@/app/api/sequences', () => ({ getEnrollments: vi.fn(), getSequences: vi.fn(), enrollInSequence: vi.fn() }));
vi.mock('@/app/api/automations', () => ({ getAutomations: vi.fn() }));
vi.mock('@/app/api/settings', () => ({
  getUserSettings: vi.fn(async () => ({})),
  saveUserSettings: vi.fn(async (value: unknown) => value),
}));

import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';

const lists = [getLeads, getDeals, getTasks, getContacts, getCompanies, getAutomations, getEnrollments, getSequences]
  .map((fn) => vi.mocked(fn as () => Promise<unknown>));

const ada = {
  id: 'l1',
  name: 'Ada',
  email: 'a@example.com',
  status: 'New',
  pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }),
};

function reads(): number {
  return lists.reduce((sum, fn) => sum + fn.mock.calls.length, 0);
}

async function mountPanel(): Promise<void> {
  render(<MemoryRouter><NextActions /></MemoryRouter>);
  await screen.findByRole('link', { name: 'Ada' });
  cleanup();
}

describe('agent list loads', () => {
  it('reads each list once per workspace, then reuses it on the next page', async () => {
    resetAgentDataForTests();
    for (const fn of lists) fn.mockResolvedValue([]);
    vi.mocked(getLeads).mockResolvedValue([ada] as never);

    await mountPanel();
    expect(reads()).toBe(lists.length);

    for (const fn of lists) fn.mockClear();
    await mountPanel();
    expect(reads(), 'a second page inside the freshness window reads nothing').toBe(0);
  });
});
