// The morning brief page: the day as the server works it out now, each line linking to its
// record, and the brief that went out this morning in its own words.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';
import type { MorningBriefView } from '@/app/api/assistant';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrgId: 'org-1', isReadOnly: false, currentOrg: { id: 'org-1', name: 'Pavillon' } }),
}));
const api = vi.hoisted(() => ({ getMorningBrief: vi.fn() }));
vi.mock('@/app/api/assistant', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/assistant')>()),
  getMorningBrief: (...a: unknown[]) => api.getMorningBrief(...a),
}));

const MorningBrief = (await import('./MorningBrief')).default;

const view = (thisMorning: MorningBriefView['thisMorning']): MorningBriefView => ({
  now: {
    day: '2026-11-13', timeZone: 'Europe/Zurich', workspaceName: 'Pavillon', headline: '2 tasks due, 1 booking today.', items: 3,
    sections: [
      { key: 'tasks', title: 'Your tasks due', count: 2, lines: [
        { text: 'Today at 17:00: Call Ada about the renewal', link: '/tasks/t1' },
        { text: 'Overdue since Wed 11 Nov: Send the proposal', link: '/tasks/t2' },
      ] },
      { key: 'arrivals', title: "Today's bookings", count: 1, lines: [{ text: '18:00 Court 1: Ada Muster, 2 people', link: '/members/m1' }] },
    ],
  },
  thisMorning,
});

function open() {
  return render(withQueries(<MemoryRouter><MorningBrief /></MemoryRouter>));
}

beforeEach(() => {
  api.getMorningBrief.mockReset();
});

describe('the morning brief page', () => {
  it('shows the day now, each line linking to its record', async () => {
    api.getMorningBrief.mockResolvedValue(view(null));
    open();

    expect(await screen.findByText('2 tasks due, 1 booking today.')).toBeInTheDocument();
    const tasks = screen.getByRole('region', { name: /Your tasks due/ });
    expect(within(tasks).getByText('Today at 17:00: Call Ada about the renewal')).toBeInTheDocument();
    expect(within(tasks).getAllByRole('link', { name: /Open/ })[0]).toHaveAttribute('href', '/tasks/t1');
    expect(screen.getByRole('region', { name: /Today's bookings/ })).toBeInTheDocument();
    expect(screen.getByText(/None went out today yet/)).toBeInTheDocument();
  });

  it("shows this morning's brief as it was sent, and says when the AI wrote its summary", async () => {
    api.getMorningBrief.mockResolvedValue(view({
      sentAtUtc: '2026-11-13T06:30:00Z', text: '2 tasks due.\n\nStart with Ada.\n\nYour tasks due (2)', writtenByModel: true, emailed: true,
    }));
    open();

    const sent = await screen.findByRole('complementary', { name: "This morning's brief" });
    expect(within(sent).getByText(/Start with Ada\./)).toBeInTheDocument();
    expect(within(sent).getByText(/summary written by the AI/)).toBeInTheDocument();
    expect(within(sent).getByText(/also by email/)).toBeInTheDocument();
  });

  it('says so when the brief could not be read', async () => {
    api.getMorningBrief.mockRejectedValue(new Error('Server unavailable'));
    open();

    expect(await screen.findByText(/Your morning brief/)).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /Try again/ })).toBeInTheDocument();
  });
});
