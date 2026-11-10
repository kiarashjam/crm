// The sales side of the calendar, from the server or, in demo mode, by the same rules in
// the browser.
import { beforeEach, describe, expect, it, vi } from 'vitest';

const client = { live: true, json: vi.fn() };
vi.mock('./apiClient', async () => {
  const actual = await vi.importActual<typeof import('./apiClient')>('./apiClient');
  return {
    ...actual,
    isUsingRealApi: () => client.live,
    servesDemoData: () => !client.live,
    authFetchJson: (...a: unknown[]) => client.json(...a),
  };
});
const demo = { tasks: vi.fn(), activities: vi.fn(), leads: vi.fn() };
vi.mock('./tasks', async () => {
  const actual = await vi.importActual<typeof import('./tasks')>('./tasks');
  return { ...actual, getTasks: () => demo.tasks() };
});
vi.mock('./activities', async () => {
  const actual = await vi.importActual<typeof import('./activities')>('./activities');
  return { ...actual, getActivities: () => demo.activities() };
});
vi.mock('./leads', async () => {
  const actual = await vi.importActual<typeof import('./leads')>('./leads');
  return { ...actual, getLeads: () => demo.leads() };
});
vi.mock('@/app/lib/auth', async () => {
  const actual = await vi.importActual<typeof import('@/app/lib/auth')>('@/app/lib/auth');
  return { ...actual, getCurrentUser: () => ({ id: 'u-me', name: 'Me', email: 'me@example.ch' }) };
});

const { getCalendar } = await import('./calendar');

// The November 2026 page: Sunday 1 November to Saturday 12 December, local days.
const FROM = new Date(2026, 10, 1);
const TO = new Date(2026, 11, 13);
const at = (day: number, hour: number, minute = 0) => new Date(2026, 10, day, hour, minute).toISOString();

beforeEach(() => {
  client.live = true;
  client.json.mockReset();
});

describe('the calendar on the live API', () => {
  it('asks for the span and whose work, and reads the entries back', async () => {
    client.json.mockResolvedValue({
      scope: 'team',
      entries: [
        {
          kind: 'task', id: 't1', title: 'Call the Müllers', startUtc: '2026-11-03T13:30:00Z', endUtc: null, allDay: false,
          day: null, link: '/tasks/t1', status: 'todo', detail: 'Jean Dupont', who: 'Anna',
        },
        {
          kind: 'lead_meeting', id: 'l1', title: 'Meeting with Jean Dupont', startUtc: '2026-11-12T00:00:00Z', endUtc: null,
          allDay: true, day: '2026-11-12', link: '/leads/l1', status: null, detail: null, who: 'Anna',
        },
        // A kind a newer server sends, which this page cannot show.
        { kind: 'holiday', id: 'h1', title: 'Saint Martin', startUtc: '2026-11-11T00:00:00Z', allDay: true, link: '/' },
      ],
      truncated: true,
    });

    const page = await getCalendar(FROM, TO, 'team');

    const url = new URL(String(client.json.mock.calls[0]![0]), 'http://cadence.test');
    expect(url.pathname).toBe('/api/calendar');
    expect(url.searchParams.get('from')).toBe(FROM.toISOString());
    expect(url.searchParams.get('to')).toBe(TO.toISOString());
    expect(url.searchParams.get('scope')).toBe('team');
    expect(page.scope).toBe('team');
    expect(page.truncated).toBe(true);
    expect(page.entries).toEqual([
      {
        kind: 'task', id: 't1', title: 'Call the Müllers', startUtc: '2026-11-03T13:30:00Z', endUtc: undefined, allDay: false,
        day: undefined, link: '/tasks/t1', status: 'todo', detail: 'Jean Dupont', who: 'Anna',
      },
      {
        kind: 'lead_meeting', id: 'l1', title: 'Meeting with Jean Dupont', startUtc: '2026-11-12T00:00:00Z', endUtc: undefined,
        allDay: true, day: '2026-11-12', link: '/leads/l1', status: undefined, detail: undefined, who: 'Anna',
      },
    ]);
  });
});

describe('the calendar in demo mode', () => {
  beforeEach(() => {
    client.live = false;
    demo.tasks.mockResolvedValue([
      { id: 't-mine', title: 'Mine', status: 'todo', completed: false, assigneeId: 'u-me', assigneeName: 'Me', dueDateUtc: at(3, 14, 30), leadName: 'Jean Dupont' },
      { id: 't-free', title: 'Nobody\'s', status: 'todo', completed: false, dueDateUtc: at(4, 9) },
      { id: 't-other', title: 'Theirs', status: 'in_progress', completed: false, assigneeId: 'u-other', dueDateUtc: at(5, 9) },
      { id: 't-cancelled', title: 'Called off', status: 'cancelled', completed: false, dueDateUtc: at(5, 10) },
      { id: 't-later', title: 'Next year', status: 'todo', completed: false, dueDateUtc: new Date(2027, 0, 15, 9).toISOString() },
      { id: 't-undated', title: 'Someday', status: 'todo', completed: false },
    ]);
    demo.activities.mockResolvedValue([
      {
        id: 'a-mine', type: 'meeting', subject: 'Tour of the club', createdAt: at(9, 18), occurredAt: at(6, 15), durationMinutes: 45,
        outcome: 'completed', leadId: 'l-mine', leadName: 'Jean Dupont', createdByUserId: 'u-me', createdByName: 'Me',
      },
      { id: 'a-other', type: 'video', subject: '', createdAt: at(7, 10), dealId: 'd-1', createdByUserId: 'u-other' },
      { id: 'a-call', type: 'call', subject: 'Only a call', createdAt: at(7, 11), createdByUserId: 'u-me' },
      { id: 'a-old', type: 'meeting', subject: 'Last month', createdAt: new Date(2026, 9, 20, 10).toISOString(), createdByUserId: 'u-me' },
    ]);
    demo.leads.mockResolvedValue([
      { id: 'l-mine', name: 'Jean Dupont', assignedToId: 'u-me', assignedToName: 'Me', pipelineState: JSON.stringify({ meetingDate: '2026-11-12', meetingAttended: false }) },
      { id: 'l-other', name: 'Their lead', assignedToId: 'u-other', pipelineState: JSON.stringify({ meetingDate: '2026-11-13' }) },
      { id: 'l-later', name: 'Later', assignedToId: 'u-me', pipelineState: JSON.stringify({ meetingDate: '2027-02-01' }) },
      { id: 'l-none', name: 'No meeting', assignedToId: 'u-me' },
    ]);
  });

  it("shows my tasks, the meetings I logged and my leads' meetings in the span, by time", async () => {
    const mine = await getCalendar(FROM, TO, 'mine');

    expect(mine.entries.map((e) => e.id)).toEqual(['t-mine', 't-free', 'a-mine', 'l-mine']);
    expect(mine.entries[0]).toMatchObject({ kind: 'task', startUtc: at(3, 14, 30), allDay: false, link: '/tasks/t-mine', status: 'todo', detail: 'Jean Dupont', who: 'Me' });
    // When it happened, not when it was logged, and for as long as it lasted.
    expect(mine.entries[2]).toMatchObject({
      kind: 'meeting', title: 'Tour of the club', startUtc: at(6, 15), endUtc: at(6, 15, 45),
      link: '/leads/l-mine', status: 'completed', detail: 'Jean Dupont', who: 'Me',
    });
    expect(mine.entries[3]).toMatchObject({
      kind: 'lead_meeting', title: 'Meeting with Jean Dupont', allDay: true, day: '2026-11-12', link: '/leads/l-mine', status: 'missed', who: 'Me',
    });
    expect(mine.truncated).toBe(false);
  });

  it("shows everyone's for the team", async () => {
    const team = await getCalendar(FROM, TO, 'team');

    expect(team.scope).toBe('team');
    expect(team.entries.map((e) => e.id)).toEqual(['t-mine', 't-free', 't-other', 'a-mine', 'a-other', 'l-mine', 'l-other']);
    // Untitled, a meeting is named by its type, as the server names it.
    expect(team.entries.find((e) => e.id === 'a-other')).toMatchObject({ title: 'Video', link: '/deals/d-1' });
  });
});
