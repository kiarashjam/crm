// The money screens send the version of the tab, card, subscription or donation they read.
//
// The server checks every change to them against the version it was made from (If-Match)
// and refuses one made from an older copy with a 409. The screens sent none, so a tab
// closed from a stale page, or a subscription cancelled over a colleague's pause, went
// through on whatever the record had become meanwhile.
import { beforeEach, describe, expect, it, vi } from 'vitest';

const client = { json: vi.fn() };
vi.mock('./apiClient', async () => {
  const actual = await vi.importActual<typeof import('./apiClient')>('./apiClient');
  return {
    ...actual,
    isUsingRealApi: () => true,
    servesDemoData: () => false,
    // On the live API: the call itself, never this browser's demo copy.
    apiWithFallback: <T,>(real: () => Promise<T>) => real(),
    authFetchJson: (...a: unknown[]) => client.json(...a),
  };
});

const tabs = await import('./tabs');
const giftCards = await import('./giftCards');
const subscriptions = await import('./subscriptions');
const donations = await import('./donations');

/** The If-Match header of the call the API made, if any. */
function ifMatchOf(call: number): string | undefined {
  const init = client.json.mock.calls[call]![1] as RequestInit | undefined;
  return (init?.headers as Record<string, string> | undefined)?.['If-Match'];
}

beforeEach(() => {
  client.json.mockReset().mockResolvedValue({ id: 'x' });
});

describe('a change to club money is sent with the version it was made from', () => {
  it('on a tab: adding, removing, closing and voiding', async () => {
    await tabs.addItemToTab({ tabId: 't1', name: 'Espresso', quantity: 1, unitPrice: 4.5, version: 'v1' });
    await tabs.removeItemFromTab('t1', 'i1', 'v2');
    await tabs.closeTab('t1', 'v3');
    await tabs.voidTab('t1', 'v4');

    expect([0, 1, 2, 3].map(ifMatchOf)).toEqual(['"v1"', '"v2"', '"v3"', '"v4"']);
    expect(client.json.mock.calls.map((c) => c[0])).toEqual([
      '/api/tabs/t1/items', '/api/tabs/t1/items/i1', '/api/tabs/t1/close', '/api/tabs/t1/void',
    ]);
  });

  it('on a gift card voided, a subscription paused, and a donation acknowledged', async () => {
    await giftCards.voidGiftCard('g1', 'Sold twice', 'v5');
    await subscriptions.updateSubscription('s1', { status: 'Paused' }, 'v6');
    await donations.acknowledgeDonation('d1', 'v7');

    expect([0, 1, 2].map(ifMatchOf)).toEqual(['"v5"', '"v6"', '"v7"']);
  });

  it('and with none when the screen read none, which the server takes as it always did', async () => {
    await tabs.closeTab('t1');

    expect(ifMatchOf(0)).toBeUndefined();
  });
});
