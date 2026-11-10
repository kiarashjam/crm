// The calendar puts a rep's tasks and meetings beside the club's events and
// reservations: their own by default, the team's one switch away. It showed club events
// and reservations only.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { CalendarEntry } from '@/app/api/calendar';

const api = { events: vi.fn(), reservations: vi.fn(), calendar: vi.fn() };
vi.mock('@/app/api/events', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/events')>('@/app/api/events');
  return { ...actual, getEvents: () => api.events() };
});
vi.mock('@/app/api/reservations', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/reservations')>('@/app/api/reservations');
  return { ...actual, getReservations: () => api.reservations() };
});
vi.mock('@/app/api/calendar', () => ({ getCalendar: (...a: unknown[]) => api.calendar(...a) }));
const org = { currentOrgId: 'org-1' as string | null };
vi.mock('@/app/contexts/OrgContext', async () => {
  const actual = await vi.importActual<typeof import('@/app/contexts/OrgContext')>('@/app/contexts/OrgContext');
  return { ...actual, useOrgOptional: () => (org.currentOrgId ? { currentOrgId: org.currentOrgId } : null) };
});
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const Calendar = (await import('./Calendar')).default;

const local = (day: number, hour: number, minute = 0) => new Date(2026, 10, day, hour, minute).toISOString();
const mineEntries = (): CalendarEntry[] => [
  {
    kind: 'meeting', id: 'a1', title: 'Tour of the club', startUtc: local(9, 9), endUtc: local(9, 9, 45), allDay: false,
    link: '/leads/l1', status: 'completed', detail: 'Jean Dupont', who: 'Me',
  },
  {
    kind: 'task', id: 't1', title: 'Call the Müllers', startUtc: local(9, 14, 30), allDay: false,
    link: '/tasks/t1', status: 'todo', detail: 'Jean Dupont', who: 'Me',
  },
  // A lead's meeting date: a day with no time.
  {
    kind: 'lead_meeting', id: 'l1', title: 'Meeting with Jean Dupont', startUtc: '2026-11-12T00:00:00Z', allDay: true, day: '2026-11-12',
    link: '/leads/l1', who: 'Me',
  },
];
const teamEntries = (): CalendarEntry[] => [
  ...mineEntries(),
  { kind: 'task', id: 't2', title: 'Luca\'s call', startUtc: local(12, 11), allDay: false, link: '/tasks/t2', status: 'todo', who: 'Luca' },
];

const show = () => render(<MemoryRouter><Calendar /></MemoryRouter>);
const day = (name: RegExp) => screen.getByRole('button', { name });
const detail = () => screen.getByRole('heading', { name: /^Meetings \(/ }).closest('section')!.parentElement!;

beforeEach(() => {
  localStorage.clear();
  org.currentOrgId = 'org-1';
  // Monday 9 November 2026, mid-morning.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 10, 9, 10, 0));
  api.events.mockReset().mockResolvedValue([]);
  api.reservations.mockReset().mockResolvedValue([]);
  api.calendar.mockReset().mockImplementation(async (_from: Date, _to: Date, scope: string) => ({
    scope, entries: scope === 'team' ? teamEntries() : mineEntries(), truncated: false,
  }));
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllEnvs();
});

describe('a rep’s tasks and meetings on the calendar', () => {
  it('reads the six weeks on show and puts my tasks and meetings on their days, with their times', async () => {
    show();

    expect(await screen.findByText('Tour of the club')).toBeInTheDocument();
    // Sunday 1 November to Saturday 12 December: the grid, to the day after its last.
    expect(api.calendar).toHaveBeenCalledWith(new Date(2026, 10, 1), new Date(2026, 11, 13), 'mine');
    const today = within(detail());
    expect(today.getByRole('heading', { name: 'Meetings (1)' })).toBeInTheDocument();
    expect(today.getByRole('link', { name: /Tour of the club/ })).toHaveAttribute('href', '/leads/l1');
    expect(today.getByRole('heading', { name: 'Tasks (1)' })).toBeInTheDocument();
    expect(today.getByRole('link', { name: /Call the Müllers/ })).toHaveAttribute('href', '/tasks/t1');
    expect(today.getByRole('link', { name: /Call the Müllers/ })).toHaveTextContent(/Due 2:30\s?PM · Jean Dupont · Me/);
    expect(day(/November 9\b/)).toHaveAccessibleName(/2 on the calendar/);

    await userEvent.click(day(/November 12\b/));

    const thursday = within(detail());
    expect(thursday.getByRole('link', { name: /Meeting with Jean Dupont/ })).toHaveTextContent('Planned for the day');
    expect(thursday.getByRole('heading', { name: 'Tasks (0)' })).toBeInTheDocument();
  });

  it("shows the team's on the switch, and keeps the choice for the next visit", async () => {
    const { unmount } = show();
    await screen.findByText('Tour of the club');
    expect(screen.getByRole('button', { name: 'Mine' })).toHaveAttribute('aria-pressed', 'true');
    await userEvent.click(day(/November 12\b/));
    expect(within(detail()).queryByText('Luca\'s call')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Team' }));

    expect(await within(detail()).findByText('Luca\'s call')).toBeInTheDocument();
    expect(api.calendar).toHaveBeenLastCalledWith(new Date(2026, 10, 1), new Date(2026, 11, 13), 'team');
    unmount();

    api.calendar.mockClear();
    show();
    await screen.findByText('Tour of the club');
    expect(api.calendar).toHaveBeenCalledWith(expect.any(Date), expect.any(Date), 'team');
  });

  it('reads the next month’s grid on the way forward', async () => {
    show();
    await screen.findByText('Tour of the club');

    await userEvent.click(screen.getByRole('button', { name: 'Next month' }));

    // December 2026: Sunday 29 November to Saturday 9 January.
    await waitFor(() => expect(api.calendar).toHaveBeenLastCalledWith(new Date(2026, 10, 29), new Date(2027, 0, 10), 'mine'));
  });

  it('moves the day on show into the month gone to, instead of leaving it off the grid read', async () => {
    // The tasks and meetings read are the six weeks on the grid: 9 November, left
    // selected on the way to December, said "No tasks due" though it has one.
    show();
    await screen.findByText('Tour of the club');
    api.calendar.mockImplementation(async () => ({
      scope: 'mine', truncated: false,
      entries: [{ kind: 'task', id: 't-dec', title: 'December call', startUtc: new Date(2026, 11, 9, 10).toISOString(), allDay: false, link: '/tasks/t-dec' }],
    }));

    await userEvent.click(screen.getByRole('button', { name: 'Next month' }));

    const december = within(detail());
    expect(await december.findByText('December call')).toBeInTheDocument();
    expect(december.getByRole('heading', { level: 2 })).toHaveTextContent('December 9');
  });

  it('keeps a day still on the new grid where it is', async () => {
    show();
    await screen.findByText('Tour of the club');
    await userEvent.click(day(/November 30\b/));

    await userEvent.click(screen.getByRole('button', { name: 'Next month' }));

    // December's grid starts on Sunday 29 November.
    expect(within(detail()).getByRole('heading', { level: 2 })).toHaveTextContent('November 30');
  });

  it("keeps a lead's meeting date on its day wherever the viewer is", async () => {
    // West of Greenwich, the first moment of 12 November in UTC is still 11 November.
    vi.stubEnv('TZ', 'America/Los_Angeles');
    vi.setSystemTime(new Date(2026, 10, 9, 10, 0));
    show();
    await screen.findByText('Tour of the club');

    await userEvent.click(day(/November 11\b/));
    expect(within(detail()).queryByText('Meeting with Jean Dupont')).not.toBeInTheDocument();
    await userEvent.click(day(/November 12\b/));
    expect(within(detail()).getByText('Meeting with Jean Dupont')).toBeInTheDocument();
  });

  it('outside a workspace offers no switch and reads the rep’s own', async () => {
    org.currentOrgId = null;
    localStorage.setItem('cadence.workScope.calendar', 'team');
    show();
    await screen.findByText('Tour of the club');

    expect(screen.queryByRole('group', { name: 'Whose work' })).not.toBeInTheDocument();
    expect(api.calendar).toHaveBeenCalledWith(expect.any(Date), expect.any(Date), 'mine');
  });
});

describe('when part of the calendar cannot be loaded', () => {
  it('says the tasks and meetings are missing, and loads them again on request', async () => {
    api.calendar.mockRejectedValueOnce(new Error('offline'));
    show();

    expect(await screen.findByRole('alert')).toHaveTextContent('Tasks and meetings could not be loaded, so they are missing here.');
    expect(screen.queryByText('Tour of the club')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Tour of the club')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it("does not leave the last read's entries up when the next read fails", async () => {
    // Switched to the team and refused: my own meetings stayed up under "The team's
    // tasks and meetings", beside a message saying they could not be loaded.
    show();
    await screen.findByText('Tour of the club');
    api.calendar.mockRejectedValueOnce(new Error('offline'));

    await userEvent.click(screen.getByRole('button', { name: 'Team' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Tasks and meetings could not be loaded, so they are missing here.');
    expect(screen.queryByText('Tour of the club')).not.toBeInTheDocument();
  });

  it('does not let a late answer for the month just left replace the one on show', async () => {
    api.calendar.mockRejectedValueOnce(new Error('offline'));
    show();
    await screen.findByRole('alert');
    let answerNovember: (page: unknown) => void = () => {};
    api.calendar.mockImplementationOnce(() => new Promise((resolve) => { answerNovember = resolve; }));
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    api.calendar.mockImplementationOnce(async () => ({
      scope: 'mine', truncated: false,
      entries: [{ kind: 'task', id: 't-dec', title: 'December call', startUtc: new Date(2026, 11, 3, 10).toISOString(), allDay: false, link: '/tasks/t-dec' }],
    }));
    await userEvent.click(screen.getByRole('button', { name: 'Next month' }));
    await userEvent.click(day(/December 3\b/));
    expect(await within(detail()).findByText('December call')).toBeInTheDocument();

    await act(async () => answerNovember({ scope: 'mine', entries: mineEntries(), truncated: false }));

    expect(within(detail()).getByText('December call')).toBeInTheDocument();
  });

  it("says the club's events and reservations are missing, instead of an empty month", async () => {
    api.events.mockRejectedValueOnce(new Error('offline'));
    api.reservations.mockResolvedValue([{
      id: 'r1', resourceId: 'court-1', resourceName: 'Court 1', memberName: 'Ada Muster', partySize: 2,
      startAtUtc: local(9, 18), endAtUtc: local(9, 19), status: 'Confirmed',
    }]);
    show();

    expect(await screen.findByRole('alert')).toHaveTextContent('Events and reservations could not be loaded, so they are missing here.');

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await within(detail()).findByText('Court 1')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
