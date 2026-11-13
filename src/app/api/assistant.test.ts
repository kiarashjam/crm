// The assistant's API: asking it, its settings, and a charge it proposed sent once.
import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.resetModules();
  localStorage.clear();
});

async function live(response: Response) {
  vi.stubEnv('VITE_API_URL', 'https://api.example.test');
  const fetch = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetch);
  return { fetch, api: await import('./assistant') };
}

const usage = { used: 2, limit: 300, month: '2026-11-01', resetsOn: '2026-12-01', refused: 0, inputTokens: 900, outputTokens: 120, left: 298 };

describe('asking the assistant', () => {
  it('posts the question with the conversation so far and the page, and reads the proposals back', async () => {
    const answer = {
      reply: 'I propose a task.',
      proposals: [{ id: 'p1', kind: 'task', title: 'Create a task', details: ['Task: Call Ada'], payload: { title: 'Call Ada' }, link: '/leads/l1' }],
      lookedUp: ['get_lead'],
      usage,
      incomplete: false,
    };
    const { fetch, api } = await live(new Response(JSON.stringify(answer), { status: 200 }));

    const reply = await api.askAssistant({ message: 'Remind me to call Ada', history: [{ role: 'user', text: 'Hi' }], page: '/leads/l1', language: 'en' });

    const [url, init] = fetch.mock.calls[0]!;
    expect(url).toBe('https://api.example.test/api/agent/chat');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({ message: 'Remind me to call Ada', history: [{ role: 'user', text: 'Hi' }], page: '/leads/l1', language: 'en' });
    expect(reply.proposals).toHaveLength(1);
    expect(reply.proposals[0]).toMatchObject({ id: 'p1', kind: 'task', payload: { title: 'Call Ada' } });
    expect(reply.lookedUp).toEqual(['get_lead']);
  });

  it('keeps the status and code of a refusal, so the chat can answer from its rules instead', async () => {
    const problem = { title: 'Service Unavailable', status: 503, detail: 'No language model is set up.', errorCode: 'Assistant.NotConfigured' };
    const { api } = await live(new Response(JSON.stringify(problem), { status: 503 }));

    await expect(api.askAssistant({ message: 'Hi' })).rejects.toMatchObject({ status: 503, code: 'Assistant.NotConfigured' });
  });

  it('with demo data there is no model: nothing is sent and the settings say so', async () => {
    const fetch = vi.fn();
    vi.stubGlobal('fetch', fetch);
    const api = await import('./assistant');

    await expect(api.askAssistant({ message: 'Hi' })).rejects.toMatchObject({ status: 503, code: 'Assistant.NotConfigured' });
    await expect(api.getAssistantSettings()).resolves.toMatchObject({ configured: false });
    await expect(api.getMorningBrief()).resolves.toBeNull();
    expect(fetch).not.toHaveBeenCalled();
  });
});

describe("the assistant's settings", () => {
  it('saves a change with the version it was read at, so a change made meanwhile is refused', async () => {
    const saved = { configured: true, provider: 'OpenAI', model: 'gpt-4o-mini', monthlyLimit: 500, customLimit: 500, defaultLimit: 300, maxLimit: 3000, morningBriefs: true, usage, version: 'v2' };
    const { fetch, api } = await live(new Response(JSON.stringify(saved), { status: 200 }));

    await api.saveAssistantSettings({ monthlyLimit: 500, version: 'v1' });

    const [url, init] = fetch.mock.calls[0]!;
    expect(url).toBe('https://api.example.test/api/agent/settings');
    expect(init.method).toBe('PUT');
    expect(new Headers(init.headers).get('If-Match')).toBe('"v1"');
    expect(JSON.parse(init.body)).toEqual({ monthlyLimit: 500, version: 'v1' });
  });
});

describe('a charge the assistant proposed', () => {
  it('carries the proposal as its Idempotency-Key, so a second confirm cannot charge twice', async () => {
    vi.stubEnv('VITE_API_URL', 'https://api.example.test');
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'c1', memberId: 'm1', memberName: 'Ada', kind: 'Other', description: 'Wine', amount: 40, paidAmount: 0, status: 'Pending', postedAtUtc: '2026-11-13T08:00:00Z' }), { status: 200 }));
    vi.stubGlobal('fetch', fetch);
    const { createCharge } = await import('./charges');

    await createCharge({ memberId: 'm1', memberName: 'Ada', kind: 'Other', description: 'Wine', amount: 40 }, { idempotencyKey: 'proposal-1' });

    const [url, init] = fetch.mock.calls[0]!;
    expect(url).toBe('https://api.example.test/api/charges');
    expect(new Headers(init.headers).get('Idempotency-Key')).toBe('proposal-1');
  });
});
