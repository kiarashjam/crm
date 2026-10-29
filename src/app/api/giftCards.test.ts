// Gift cards against the server: one redemption per press of Redeem, the server's
// refusal shown as it said it, and an expired card no longer counted as owed.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { computeGiftCardStats, redeemGiftCard, type GiftCard } from './giftCards';

const BASE = 'http://api.test';

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
});
afterEach(() => {
  vi.unstubAllEnvs();
});

describe('redeeming a gift card on the server', () => {
  it('sends the key of this redemption, so a retry cannot spend the card twice', async () => {
    let key: string | null = null;
    let body: unknown = null;
    server.use(http.post(`${BASE}/api/gift-cards/g1/redeem`, async ({ request }) => {
      key = request.headers.get('Idempotency-Key');
      body = await request.json();
      return HttpResponse.json({ id: 'r1', giftCardId: 'g1', giftCardCode: 'GIFT-A', amount: 20, redeemedByName: 'Jean', redeemedAtUtc: '' });
    }));

    await redeemGiftCard({ giftCardId: 'g1', amount: 20, redeemedByName: 'Jean', idempotencyKey: 'k-1' });

    expect(key).toBe('k-1');
    expect(body).not.toHaveProperty('idempotencyKey');
  });

  it("shows the server's reason when it refuses, instead of a generic failure", async () => {
    server.use(http.post(`${BASE}/api/gift-cards/g1/redeem`, () =>
      HttpResponse.json({ status: 409, title: 'Conflict', detail: 'This gift card expired on 1 Oct 2026, so it can\'t be redeemed.' }, { status: 409 })));

    await expect(redeemGiftCard({ giftCardId: 'g1', amount: 20, redeemedByName: 'Jean' }))
      .rejects.toThrow('expired on 1 Oct 2026');
  });
});

describe('gift card figures', () => {
  const card = (over: Partial<GiftCard>): GiftCard => ({
    id: 'g', code: 'GIFT-A', purchaserName: 'Ana', recipientName: 'Bea', faceValue: 100, remainingValue: 100,
    status: 'Active', issuedAtUtc: new Date().toISOString(), ...over,
  });

  it('leaves a card past its expiry out of what the club still owes', () => {
    const stats = computeGiftCardStats([
      card({ id: 'live', expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString() }),
      card({ id: 'old', remainingValue: 60, expiresAtUtc: new Date(Date.now() - 86_400_000).toISOString() }),
    ], []);

    expect(stats.outstandingBalance).toBe(100);
    expect(stats.activeCount).toBe(1);
  });
});
