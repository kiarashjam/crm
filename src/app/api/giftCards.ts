// Gift cards.
//
// A purchaser issues a gift card to a recipient at a chosen face value. The
// recipient can redeem against any in-house charge until the balance hits
// zero. Each card has an immutable code.

import { apiWithFallback, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type GiftCardStatus = 'Active' | 'Redeemed' | 'Expired' | 'Voided';

export interface GiftCard {
  id: string;
  code: string;
  purchaserMemberId?: string;
  purchaserName: string;
  recipientName: string;
  recipientEmail?: string;
  faceValue: number;
  remainingValue: number;
  status: GiftCardStatus;
  issuedAtUtc: string;
  expiresAtUtc?: string;
  message?: string;
}

export interface GiftCardRedemption {
  id: string;
  giftCardId: string;
  giftCardCode: string;
  amount: number;
  redeemedByName: string;
  redeemedAtUtc: string;
  appliedTo?: string;
}

export const GIFT_CARD_STATUSES: GiftCardStatus[] = ['Active', 'Redeemed', 'Expired', 'Voided'];

function generateCode(): string {
  const chars = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
  let out = '';
  for (let i = 0; i < 12; i++) {
    out += chars[Math.floor(Math.random() * chars.length)];
    if (i === 3 || i === 7) out += '-';
  }
  return out;
}

const day = 86_400_000;

const cardSeed: GiftCard[] = [
  {
    id: 'gc-seed-1',
    code: 'GIFT-A4XQ-9KM2-PR7T',
    purchaserMemberId: 'mem-seed-1',
    purchaserName: 'Amelia Hartwell',
    recipientName: 'Lara Mendez',
    recipientEmail: 'lara@example.com',
    faceValue: 250,
    remainingValue: 175,
    status: 'Active',
    issuedAtUtc: new Date(Date.now() - 12 * day).toISOString(),
    expiresAtUtc: new Date(Date.now() + 365 * day).toISOString(),
    message: 'Happy birthday Lara — a dinner on me.',
  },
  {
    id: 'gc-seed-2',
    code: 'GIFT-B2WK-7HJ4-NM3D',
    purchaserMemberId: 'mem-seed-3',
    purchaserName: 'Priya Raman',
    recipientName: 'Aarav Raman',
    recipientEmail: 'aarav@example.com',
    faceValue: 100,
    remainingValue: 100,
    status: 'Active',
    issuedAtUtc: new Date(Date.now() - 3 * day).toISOString(),
    expiresAtUtc: new Date(Date.now() + 365 * day).toISOString(),
  },
  {
    id: 'gc-seed-3',
    code: 'GIFT-F9LQ-3RS5-TX8K',
    purchaserMemberId: 'mem-seed-2',
    purchaserName: 'Daniel Okafor',
    recipientName: 'Bea Okafor',
    faceValue: 500,
    remainingValue: 0,
    status: 'Redeemed',
    issuedAtUtc: new Date(Date.now() - 90 * day).toISOString(),
    expiresAtUtc: new Date(Date.now() + 275 * day).toISOString(),
  },
  {
    id: 'gc-seed-4',
    code: 'GIFT-Q1ZH-2VL8-WP4M',
    purchaserName: 'Walk-in purchaser',
    recipientName: 'Conor Yates',
    recipientEmail: 'conor.yates@example.com',
    faceValue: 75,
    remainingValue: 75,
    status: 'Active',
    issuedAtUtc: new Date(Date.now() - 1 * day).toISOString(),
  },
];

const redemptionSeed: GiftCardRedemption[] = [
  {
    id: 'gcr-seed-1',
    giftCardId: 'gc-seed-1',
    giftCardCode: 'GIFT-A4XQ-9KM2-PR7T',
    amount: 75,
    redeemedByName: 'Lara Mendez',
    redeemedAtUtc: new Date(Date.now() - 4 * day).toISOString(),
    appliedTo: 'Library Bar tab',
  },
  {
    id: 'gcr-seed-2',
    giftCardId: 'gc-seed-3',
    giftCardCode: 'GIFT-F9LQ-3RS5-TX8K',
    amount: 500,
    redeemedByName: 'Bea Okafor',
    redeemedAtUtc: new Date(Date.now() - 60 * day).toISOString(),
    appliedTo: 'Chef Series dinner × 2',
  },
];

const cardStore = createMockStore<GiftCard>({
  storageKey: 'crm.mock.giftCards.v1',
  seed: cardSeed,
  idOf: (g) => g.id,
});

const redemptionStore = createMockStore<GiftCardRedemption>({
  storageKey: 'crm.mock.giftCardRedemptions.v1',
  seed: redemptionSeed,
  idOf: (r) => r.id,
});

export async function getGiftCards(): Promise<GiftCard[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<GiftCard[]>('/api/gift-cards');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...cardStore.list()].sort(
        (a, b) => Date.parse(b.issuedAtUtc) - Date.parse(a.issuedAtUtc),
      );
    },
  );
}

export async function getRedemptions(): Promise<GiftCardRedemption[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<GiftCardRedemption[]>('/api/gift-cards/redemptions');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      return [...redemptionStore.list()].sort(
        (a, b) => Date.parse(b.redeemedAtUtc) - Date.parse(a.redeemedAtUtc),
      );
    },
  );
}

export interface GiftCardInput {
  purchaserMemberId?: string;
  purchaserName: string;
  recipientName: string;
  recipientEmail?: string;
  faceValue: number;
  expiresAtUtc?: string;
  message?: string;
}

export async function issueGiftCard(input: GiftCardInput): Promise<GiftCard | null> {
  return apiWithFallback(
    () =>
      authFetchJson<GiftCard>('/api/gift-cards', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    async () => {
      await delay(160);
      return cardStore.add({
        id: mockId('gc'),
        code: 'GIFT-' + generateCode(),
        purchaserMemberId: input.purchaserMemberId,
        purchaserName: input.purchaserName,
        recipientName: input.recipientName,
        recipientEmail: input.recipientEmail,
        faceValue: input.faceValue,
        remainingValue: input.faceValue,
        status: 'Active',
        issuedAtUtc: new Date().toISOString(),
        expiresAtUtc: input.expiresAtUtc,
        message: input.message,
      });
    },
  );
}

export interface RedeemInput {
  giftCardId: string;
  amount: number;
  redeemedByName: string;
  appliedTo?: string;
}

export async function redeemGiftCard(input: RedeemInput): Promise<GiftCardRedemption | null> {
  const card = cardStore.byId(input.giftCardId);
  if (!card) return null;
  if (card.status !== 'Active') return null;
  const amount = Math.min(input.amount, card.remainingValue);
  if (amount <= 0) return null;

  const redemption = await apiWithFallback(
    () =>
      authFetchJson<GiftCardRedemption>(`/api/gift-cards/${input.giftCardId}/redeem`, {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    async () => {
      await delay(160);
      return redemptionStore.add({
        id: mockId('gcr'),
        giftCardId: input.giftCardId,
        giftCardCode: card.code,
        amount,
        redeemedByName: input.redeemedByName,
        redeemedAtUtc: new Date().toISOString(),
        appliedTo: input.appliedTo,
      });
    },
  );
  if (redemption) {
    const newRemaining = card.remainingValue - amount;
    cardStore.update(card.id, {
      remainingValue: newRemaining,
      status: newRemaining <= 0 ? 'Redeemed' : 'Active',
    });
  }
  return redemption;
}

export async function voidGiftCard(id: string): Promise<GiftCard | null> {
  return apiWithFallback(
    () => authFetchJson<GiftCard>(`/api/gift-cards/${id}/void`, { method: 'POST' }),
    async () => {
      await delay(100);
      return cardStore.update(id, { status: 'Voided' });
    },
  );
}

export interface GiftCardStats {
  totalIssued: number;
  outstandingBalance: number;
  redeemedAllTime: number;
  activeCount: number;
}

export function computeGiftCardStats(
  cards: GiftCard[],
  redemptions: GiftCardRedemption[],
): GiftCardStats {
  let issued = 0;
  let outstanding = 0;
  let active = 0;
  for (const c of cards) {
    issued += c.faceValue;
    if (c.status === 'Active') {
      outstanding += c.remainingValue;
      active++;
    }
  }
  const redeemed = redemptions.reduce((s, r) => s + r.amount, 0);
  return {
    totalIssued: Math.round(issued * 100) / 100,
    outstandingBalance: Math.round(outstanding * 100) / 100,
    redeemedAllTime: Math.round(redeemed * 100) / 100,
    activeCount: active,
  };
}
