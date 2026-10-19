// Follow-up sequences against the live API: a refusal must read as a refusal.
import { beforeEach, describe, expect, it, vi } from 'vitest';

const client = { live: true, json: vi.fn(), fetch: vi.fn() };
vi.mock('./apiClient', async () => {
  const actual = await vi.importActual<typeof import('./apiClient')>('./apiClient');
  return {
    ...actual,
    isUsingRealApi: () => client.live,
    servesDemoData: () => !client.live,
    authFetchJson: (...a: unknown[]) => client.json(...a),
    authFetch: (...a: unknown[]) => client.fetch(...a),
  };
});

const { ApiError } = await import('./apiClient');
const seq = await import('./sequences');

const enroll = { sequenceId: 's1', targetType: 'lead' as const, targetId: 'l1', targetName: 'Jean' };

beforeEach(() => {
  client.live = true;
  client.json.mockReset();
  client.fetch.mockReset();
  localStorage.clear();
});

describe('sequences on the live API', () => {
  it('a refused enrolment rejects with the server reason, and nothing is enrolled locally', async () => {
    // This used to fall back to the browser's demo store on any failure: the toast
    // said "enrolled", a row appeared, and no email was ever sent.
    client.json.mockRejectedValue(new ApiError('This lead asked not to be contacted.', 400));
    await expect(seq.enrollInSequence(enroll)).rejects.toThrow('This lead asked not to be contacted.');

    client.live = false;
    const local = await seq.getEnrollments('s1');
    expect(local.find((e) => e.targetId === 'l1')).toBeUndefined();
  });

  it('a failed save rejects instead of saving to this browser', async () => {
    client.json.mockRejectedValue(new ApiError('Network down', 0));
    await expect(seq.createSequence({ name: 'Intro', steps: [] })).rejects.toThrow('Network down');
  });

  it('removing an enrolment that the server refuses rejects with its reason', async () => {
    client.fetch.mockResolvedValue(new Response(JSON.stringify({ detail: 'Enrollment not found.' }), { status: 404 }));
    await expect(seq.unenroll('e1')).rejects.toThrow('Enrollment not found.');
  });

  it('a missing sequence reads as null, not an error', async () => {
    client.json.mockRejectedValue(new ApiError('Not found', 404));
    await expect(seq.getSequence('gone')).resolves.toBeNull();
  });

  it('demo mode still uses the local store', async () => {
    client.live = false;
    const created = await seq.createSequence({ name: 'Local', steps: [] });
    expect(created?.name).toBe('Local');
    expect(client.json).not.toHaveBeenCalled();
  });
});
