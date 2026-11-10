// Removing a member hands their open work to someone: the request, and the sentence the Team page says.
import { beforeEach, describe, expect, it, vi } from 'vitest';

const client = { json: vi.fn() };
vi.mock('./apiClient', async () => {
  const actual = await vi.importActual<typeof import('./apiClient')>('./apiClient');
  return { ...actual, servesDemoData: () => false, authFetchJson: (...a: unknown[]) => client.json(...a) };
});

const { removeMember, handedOverSummary } = await import('./organizations');

// Braces: a hook that returns a function has it run as the test's clean-up.
beforeEach(() => {
  client.json.mockReset();
});

describe('removing a member', () => {
  it('names who takes over their work, and reads back how much moved', async () => {
    client.json.mockResolvedValue({ handedOverToUserId: 'u-anna', handedOverToName: 'Anna', leads: 2, deals: 1, tasks: 0 });

    const removed = await removeMember('org-1', 'u-marc', 'u-anna');

    expect(client.json).toHaveBeenCalledWith('/api/organizations/org-1/members/u-marc?handOverTo=u-anna', { method: 'DELETE' });
    expect(removed.handedOverToName).toBe('Anna');
  });

  it('leaves the choice to the server (the owner) when nobody is named', async () => {
    client.json.mockResolvedValue({ handedOverToUserId: 'u-owner', handedOverToName: 'Owner', leads: 0, deals: 0, tasks: 0 });

    await removeMember('org-1', 'u-marc');

    expect(client.json).toHaveBeenCalledWith('/api/organizations/org-1/members/u-marc', { method: 'DELETE' });
  });

  it('passes on the reason the server refused', async () => {
    client.json.mockRejectedValue(new Error('The person taking over their work can only read records here.'));

    await expect(removeMember('org-1', 'u-marc', 'u-viewer')).rejects.toThrow('can only read records');
  });
});

describe('what moved, in words', () => {
  it('names only the kinds of work that moved, as a person would list them', () => {
    expect(handedOverSummary({ leads: 12, deals: 3, tasks: 7 })).toBe('12 leads, 3 deals and 7 tasks');
    expect(handedOverSummary({ leads: 1, deals: 0, tasks: 1 })).toBe('1 lead and 1 task');
    expect(handedOverSummary({ leads: 0, deals: 2, tasks: 0 })).toBe('2 deals');
    expect(handedOverSummary({ leads: 0, deals: 0, tasks: 0 })).toBe('');
  });
});
