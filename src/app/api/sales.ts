// Sales: what a member bought at one go, rung up at the house-charge screen (lines from the
// price catalogue, how they paid, a reason for any discount) or imported from the POS.
//
// All of it is the server's. It posts a sale through the ledger in one go: a charge for each
// VAT rate on it, a payment for what was paid at the till, and the points the club's settings
// give. The page only builds the list of lines and shows what the server answered. Demo data
// has no ledger to post to, so the screen says so rather than pretend.

import { authFetchJson, servesDemoData, type RequestOptions } from './apiClient';

export const SALES_DEMO_MESSAGE = 'Sales are rung up on the live app; demo data has no ledger to post them to.';

/** How a sale was settled at the till. */
export type SaleTender = 'HouseAccount' | 'Card' | 'Cash' | 'Twint';

export const SALE_TENDERS: SaleTender[] = ['HouseAccount', 'Card', 'Cash', 'Twint'];

export const TENDER_LABEL: Record<SaleTender | 'Mixed', string> = {
  HouseAccount: 'House account',
  Card: 'Card',
  Cash: 'Cash',
  Twint: 'TWINT',
  Mixed: 'Partly on the account',
};

export interface SaleLine {
  id: string;
  catalogItemId?: string;
  name: string;
  sku?: string;
  quantity: number;
  unitPrice: number;
  discount: number;
  total: number;
  vatRate?: number;
  kind: string;
  earnsPoints: boolean;
}

export interface Sale {
  id: string;
  memberId: string;
  memberName: string;
  /** The account its charges went on, when it is the household primary's rather than the buyer's. */
  billedMemberId?: string;
  /** desk, or the POS it came from (lightspeed-k). */
  source: string;
  posTicketId?: string;
  venue: string;
  tender: SaleTender | 'Mixed';
  subtotal: number;
  discount: number;
  discountReason?: string;
  total: number;
  paidAmount: number;
  owedAmount: number;
  currency: string;
  pointsAwarded: number;
  chargeIds: string[];
  paymentIds: string[];
  reference?: string;
  staffName?: string;
  tableName?: string;
  notes?: string;
  soldAtUtc: string;
  createdAtUtc: string;
  createdByName?: string;
  lines: SaleLine[];
}

/** What the house-charge screen sends: catalogue items and quantities, how it was paid, any discount with its reason. */
export interface DeskSaleInput {
  memberId: string;
  venue: string;
  lines: { catalogItemId: string; quantity: number }[];
  tender: SaleTender;
  discountAmount?: number;
  discountPercent?: number;
  discountReason?: string;
  notes?: string;
}

/**
 * Posts a sale. `idempotencyKey` is made once per sale on the screen, so pressing again after
 * a dropped connection gets the first sale back instead of charging twice.
 */
export async function postSale(input: DeskSaleInput, idempotencyKey: string): Promise<Sale> {
  if (servesDemoData()) throw new Error(SALES_DEMO_MESSAGE);
  return authFetchJson<Sale>('/api/sales', {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify(input),
  });
}

/** The latest sales, the club's or one member's, newest first. */
export async function getSales(memberId?: string, req?: RequestOptions): Promise<Sale[]> {
  if (servesDemoData()) return [];
  const res = await authFetchJson<Sale[]>(`/api/sales${memberId ? `?memberId=${encodeURIComponent(memberId)}` : ''}`, { signal: req?.signal });
  return Array.isArray(res) ? res : [];
}

/** A key for one sale on the screen, unique enough that two tills never share one. */
export function newSaleKey(): string {
  const random = typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  return `desk:${random}`;
}
