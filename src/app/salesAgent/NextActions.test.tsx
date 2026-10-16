import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { NextActions } from './NextActions';

vi.mock('@/app/api', () => ({
  getLeads: vi.fn(),
  getDeals: vi.fn(),
  getTasks: vi.fn(),
  getContacts: vi.fn(),
  getCompanies: vi.fn(),
}));

vi.mock('@/app/api/sequences', () => ({
  getEnrollments: vi.fn(),
  getSequences: vi.fn(),
}));

vi.mock('@/app/api/automations', () => ({
  getAutomations: vi.fn(),
}));

import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';

const leads = vi.mocked(getLeads);
const deals = vi.mocked(getDeals);
const tasks = vi.mocked(getTasks);
const contacts = vi.mocked(getContacts);
const companies = vi.mocked(getCompanies);
const automations = vi.mocked(getAutomations);
const enrollments = vi.mocked(getEnrollments);
const sequences = vi.mocked(getSequences);

function quietLists() {
  deals.mockResolvedValue([]);
  tasks.mockResolvedValue([]);
  contacts.mockResolvedValue([]);
  companies.mockResolvedValue([]);
  automations.mockResolvedValue([]);
  enrollments.mockResolvedValue([]);
  sequences.mockResolvedValue([]);
}

function renderBlock() {
  return render(
    <MemoryRouter>
      <NextActions />
    </MemoryRouter>,
  );
}

describe('NextActions', () => {
  it('shows the highest step and links to that record', async () => {
    leads.mockResolvedValue([
      { id: 'l1', name: 'Ada', email: 'a@example.com', status: 'New', pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }) },
    ]);
    quietLists();

    renderBlock();

    expect(await screen.findByRole('link', { name: 'Ada' })).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Do this next' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Ada' }).getAttribute('href')).toBe('/leads/l1');
    expect(screen.getByText(/Record why they dropped out/)).toBeTruthy();
  });

  it('stays hidden when every list fails', async () => {
    leads.mockRejectedValue(new Error('down'));
    deals.mockRejectedValue(new Error('down'));
    tasks.mockRejectedValue(new Error('down'));
    enrollments.mockRejectedValue(new Error('down'));
    sequences.mockRejectedValue(new Error('down'));
    contacts.mockRejectedValue(new Error('down'));
    companies.mockRejectedValue(new Error('down'));
    automations.mockRejectedValue(new Error('down'));

    renderBlock();

    await waitFor(() => {
      expect(leads).toHaveBeenCalled();
    });
    await waitFor(() => {
      expect(screen.getByRole('status').textContent).toMatch(/could not be loaded/);
    });
    expect(screen.queryByRole('heading', { name: 'Do this next' })).toBeNull();
  });
});
