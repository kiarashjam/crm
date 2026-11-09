// The rep's open work, from the server or, in demo mode, by the same rule in the browser.
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
const demo = { tasks: vi.fn(), leads: vi.fn(), deals: vi.fn() };
vi.mock('./tasks', async () => {
  const actual = await vi.importActual<typeof import('./tasks')>('./tasks');
  return { ...actual, getTasks: () => demo.tasks() };
});
vi.mock('./leads', async () => {
  const actual = await vi.importActual<typeof import('./leads')>('./leads');
  return { ...actual, getLeads: () => demo.leads() };
});
vi.mock('./deals', async () => {
  const actual = await vi.importActual<typeof import('./deals')>('./deals');
  return { ...actual, getDeals: () => demo.deals() };
});
vi.mock('@/app/lib/auth', async () => {
  const actual = await vi.importActual<typeof import('@/app/lib/auth')>('@/app/lib/auth');
  return { ...actual, getCurrentUser: () => ({ id: 'u-me', name: 'Me', email: 'me@example.ch' }) };
});

const { getWorkQueue } = await import('./workQueue');

beforeEach(() => {
  client.live = true;
  client.json.mockReset();
});

describe('the work queue on the live API', () => {
  it('asks for the scope and reads the records back as the app shows them', async () => {
    client.json.mockResolvedValue({
      scope: 'mine',
      tasks: [{ id: 't1', title: 'Call the Müllers', status: 'todo', priority: 'high', completed: false }],
      leads: [{ id: 'l1', name: 'Ada', email: 'ada@example.ch', status: 'New' }],
      deals: [{ id: 'd1', name: 'Membership', value: '4800' }],
      truncated: true,
    });

    const queue = await getWorkQueue('mine');

    expect(client.json).toHaveBeenCalledWith('/api/work-queue?scope=mine');
    expect(queue.scope).toBe('mine');
    expect(queue.tasks[0]).toMatchObject({ id: 't1', title: 'Call the Müllers', priority: 'high' });
    expect(queue.leads[0]).toMatchObject({ id: 'l1', name: 'Ada' });
    expect(queue.deals[0]).toMatchObject({ id: 'd1', name: 'Membership' });
    expect(queue.truncated).toBe(true);
  });
});

describe('the work queue in demo mode', () => {
  it('keeps open work given to the demo user or to nobody, and drops what is finished', async () => {
    client.live = false;
    demo.tasks.mockResolvedValue([
      { id: 't-mine', title: 'Mine', status: 'todo', completed: false, assigneeId: 'u-me' },
      { id: 't-free', title: 'Nobody\'s', status: 'todo', completed: false },
      { id: 't-other', title: 'Theirs', status: 'todo', completed: false, assigneeId: 'u-other' },
      { id: 't-done', title: 'Done', status: 'completed', completed: true },
    ]);
    demo.leads.mockResolvedValue([
      { id: 'l-mine', name: 'Mine', assignedToId: 'u-me' },
      { id: 'l-converted', name: 'Converted', isConverted: true },
      { id: 'l-other', name: 'Theirs', assignedToId: 'u-other' },
    ]);
    demo.deals.mockResolvedValue([
      { id: 'd-open', name: 'Open' },
      { id: 'd-won', name: 'Won', isWon: true },
      { id: 'd-lost', name: 'Lost, no reason', isWon: false },
      { id: 'd-explained', name: 'Lost, explained', isWon: false, closedReason: 'Price' },
    ]);

    const mine = await getWorkQueue('mine');
    const team = await getWorkQueue('team');

    expect(mine.tasks.map((t) => t.id)).toEqual(['t-mine', 't-free']);
    expect(mine.leads.map((l) => l.id)).toEqual(['l-mine']);
    expect(mine.deals.map((d) => d.id)).toEqual(['d-open', 'd-lost']);
    expect(team.tasks.map((t) => t.id)).toEqual(['t-mine', 't-free', 't-other']);
    expect(team.leads.map((l) => l.id)).toEqual(['l-mine', 'l-other']);
  });
});
