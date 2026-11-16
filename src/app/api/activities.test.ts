// Activities against the live API and in demo mode: when they happened and how they went.
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

const api = await import('./activities');

beforeEach(() => {
  client.live = true;
  client.json.mockReset();
  localStorage.clear();
});

describe('activities on the live API', () => {
  it('reads back when it happened and how it went', async () => {
    client.json.mockResolvedValue([{
      id: 'a1', type: 'call', subject: 'Called', createdAtUtc: '2026-11-09T20:00:00Z',
      occurredAtUtc: '2026-11-09T08:00:00Z', outcome: 'no_answer', durationMinutes: 0, direction: 'outbound',
    }]);

    const [call] = await api.getActivitiesByLead('l1');

    expect(call).toMatchObject({ occurredAt: '2026-11-09T08:00:00Z', outcome: 'no_answer', durationMinutes: 0, direction: 'outbound' });
  });

  it('dates an activity from an older server by when it was logged', async () => {
    client.json.mockResolvedValue([{ id: 'a1', type: 'note', createdAtUtc: '2026-11-09T20:00:00Z' }]);

    const [note] = await api.getActivitiesByLead('l1');

    expect(note?.occurredAt).toBe('2026-11-09T20:00:00Z');
  });

  it('sends the facts with the activity', async () => {
    client.json.mockResolvedValue({ id: 'a1', type: 'call', createdAtUtc: '2026-11-09T20:00:00Z' });

    await api.createActivity({ type: 'call', leadId: 'l1', outcome: 'connected', direction: 'inbound', durationMinutes: 4, occurredAtUtc: '2026-11-09T09:00:00.000Z' });

    expect(JSON.parse((client.json.mock.calls[0]![1] as RequestInit).body as string)).toMatchObject({
      outcome: 'connected', direction: 'inbound', durationMinutes: 4, occurredAtUtc: '2026-11-09T09:00:00.000Z',
    });
  });
});

describe('activities in demo mode', () => {
  it('refuses a time in the future, as the server does', async () => {
    client.live = false;
    await expect(api.createActivity({ type: 'meeting', leadId: 'l1', occurredAtUtc: new Date(Date.now() + 86_400_000).toISOString() }))
      .rejects.toThrow(/task/);
  });

  it('keeps when it happened', async () => {
    client.live = false;
    const created = await api.createActivity({ type: 'call', leadId: 'l1', outcome: 'busy', occurredAtUtc: '2026-11-01T10:00:00.000Z' });

    expect(created).toMatchObject({ occurredAt: '2026-11-01T10:00:00.000Z', outcome: 'busy' });
  });
});

describe('activities of many leads at once', () => {
  it('passes on a read that was cancelled or timed out, rather than answering "no activities"', async () => {
    const controller = new AbortController();
    client.json.mockImplementation(async () => {
      controller.abort();
      throw new DOMException('The operation was aborted.', 'AbortError');
    });

    await expect(api.getActivitiesByLeadIds(['l1', 'l2'], { signal: controller.signal })).rejects.toThrow(/aborted/);
    // Not asked lead by lead either: the read was called off.
    expect(client.json).toHaveBeenCalledTimes(1);
  });

  it('passes on a refusal or a server error', async () => {
    const { ApiError } = await import('./apiClient');
    client.json.mockRejectedValue(new ApiError('Something broke', 500, '/api/activities/by-leads'));

    await expect(api.getActivitiesByLeadIds(['l1'])).rejects.toThrow('Something broke');
  });

  it('asks lead by lead a server without the batch route', async () => {
    const { ApiError } = await import('./apiClient');
    client.json.mockImplementation(async (path: string) => {
      if (path.startsWith('/api/activities/by-leads')) throw new ApiError('Not Found', 404, path);
      const leadId = path.split('/').pop();
      return [{ id: `a-${leadId}`, type: 'note', createdAtUtc: '2026-11-09T20:00:00Z', leadId }];
    });

    const list = await api.getActivitiesByLeadIds(['l1', 'l2']);

    expect(list.map((a) => a.id).sort()).toEqual(['a-l1', 'a-l2']);
  });
});
