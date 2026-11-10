// Money from outside the club: the payment and bank settings, online payments through
// Stripe (pay links, a saved card, dues charged to it), the bank's camt files with their
// unmatched transfers, and the accounting journal.
//
// All of it is the server's: a payment is only ever taken on Stripe's own page or recorded
// from the bank's file, and recorded on the ledger by the server. Demo data has none of it,
// so the screens show what is not set up rather than pretend.

import { authFetch, authFetchJson, ensureOk, getApiBaseUrl, servesDemoData, type RequestOptions } from './apiClient';
import { downloadFile } from '@/app/lib/exportCsv';

export const DEMO_MESSAGE = 'Payments are taken and recorded by the live app; demo data has none.';

// ── settings ────────────────────────────────────────────────────────────────

/** The accounts the journal books to (Swiss SME chart by default). */
export interface AccountingAccounts {
  cash: string;
  bank: string;
  cardClearing: string;
  receivables: string;
  customerPrepayments: string;
  giftCardLiability: string;
  vatOwed: string;
  openingBalances: string;
  corrections: string;
  creditCorrections: string;
  giftCardSales: string;
  giftCardRedemptions: string;
  giftCardBreakage: string;
  revenueByKind: Record<string, string>;
  vatCodes: Record<string, string>;
}

/** The settings as the server answers them: no secret, only whether one is set. */
export interface PaymentSettings {
  stripeConfigured: boolean;
  stripeKeyHint?: string;
  stripeMode?: 'test' | 'live';
  stripeWebhookSecretSet: boolean;
  stripeOfferTwint: boolean;
  stripeOfferSepaDebit: boolean;
  stripeCheckedAtUtc?: string;
  webhookUrl: string;
  webhookEvents: string[];
  iban?: string;
  qrIban?: string;
  qrReferencePrefix?: string;
  creditorName?: string;
  creditorStreet?: string;
  creditorBuildingNumber?: string;
  creditorPostalCode?: string;
  creditorTown?: string;
  creditorCountry?: string;
  qrBillReady: boolean;
  qrBillProblem?: string;
  accounts: AccountingAccounts;
  currency: string;
  updatedAtUtc?: string;
  updatedByName?: string;
  version: string;
}

/**
 * What to save. A secret left out is kept and an empty one removed; every other field is
 * saved as sent.
 */
export interface PaymentSettingsInput {
  stripeSecretKey?: string;
  stripeWebhookSecret?: string;
  stripeOfferTwint?: boolean;
  stripeOfferSepaDebit?: boolean;
  iban?: string | null;
  qrIban?: string | null;
  qrReferencePrefix?: string | null;
  creditorName?: string | null;
  creditorStreet?: string | null;
  creditorBuildingNumber?: string | null;
  creditorPostalCode?: string | null;
  creditorTown?: string | null;
  creditorCountry?: string | null;
  accounts?: Partial<AccountingAccounts>;
  version?: string;
}

/** What the money screens show and hide by. */
export interface PaymentStatus {
  onlinePayments: boolean;
  mode?: 'test' | 'live';
  methods: string[];
  webhookConfigured: boolean;
  qrBills: boolean;
  qrReferenceType?: 'QRR' | 'SCOR';
  currency: string;
}

const NOTHING_SET_UP: PaymentStatus = { onlinePayments: false, methods: [], webhookConfigured: false, qrBills: false, currency: '' };

export async function getPaymentStatus(req?: RequestOptions): Promise<PaymentStatus> {
  if (servesDemoData()) return NOTHING_SET_UP;
  return authFetchJson<PaymentStatus>('/api/payment-settings/status', { signal: req?.signal });
}

export async function getPaymentSettings(req?: RequestOptions): Promise<PaymentSettings> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<PaymentSettings>('/api/payment-settings', { signal: req?.signal });
}

export async function savePaymentSettings(input: PaymentSettingsInput): Promise<PaymentSettings> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<PaymentSettings>('/api/payment-settings', { method: 'PUT', body: JSON.stringify(input) });
}

// ── online payments ─────────────────────────────────────────────────────────

export type OnlinePaymentStatus = 'open' | 'processing' | 'succeeded' | 'failed' | 'canceled' | 'expired';

export interface OnlinePayment {
  id: string;
  memberId: string;
  memberName: string;
  provider: string;
  /** checkout (a pay link), autopay (dues on a saved card), card (staff charged the card), setup (saving a card). */
  purpose: 'checkout' | 'autopay' | 'card' | 'setup';
  status: OnlinePaymentStatus;
  amount: number;
  currency: string;
  description: string;
  methodType?: string;
  paymentIntentId?: string;
  failureCode?: string;
  failureMessage?: string;
  amountReceived?: number;
  ledgerPaymentId?: string;
  createdByName?: string;
  createdAtUtc: string;
  completedAtUtc?: string;
}

export interface SavedCard {
  type: string;
  brand?: string;
  last4?: string;
  expMonth?: number;
  expYear?: number;
  savedAtUtc?: string;
}

export interface MemberPaymentProfile {
  memberId: string;
  onlinePayments: boolean;
  card?: SavedCard;
  autoPayDues: boolean;
  autoPayAgreedAtUtc?: string;
  autoPayAgreedBy?: string;
  payments: OnlinePayment[];
  version: string;
}

export type PayLinkPurpose = 'account' | 'charges' | 'setup';

export interface PayLink {
  url: string;
  purpose: PayLinkPurpose;
  amount: number;
  currency: string;
  expiresAtUtc: string;
  emailStatus?: 'queued' | 'suppressed';
  emailReason?: string;
}

export interface PayLinkInput {
  purpose: PayLinkPurpose;
  chargeIds?: string[];
  autoPay?: boolean;
}

export async function getMemberPaymentProfile(memberId: string, req?: RequestOptions): Promise<MemberPaymentProfile> {
  if (servesDemoData()) return { memberId, onlinePayments: false, autoPayDues: false, payments: [], version: '' };
  return authFetchJson<MemberPaymentProfile>(`/api/members/${memberId}/payment-profile`, { signal: req?.signal });
}

export async function createPayLink(memberId: string, input: PayLinkInput): Promise<PayLink> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<PayLink>(`/api/members/${memberId}/pay-link`, { method: 'POST', body: JSON.stringify(input) });
}

export async function emailPayLink(memberId: string, input: PayLinkInput): Promise<PayLink> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<PayLink>(`/api/members/${memberId}/pay-link/email`, { method: 'POST', body: JSON.stringify(input) });
}

export async function removeSavedCard(memberId: string): Promise<MemberPaymentProfile> {
  return authFetchJson<MemberPaymentProfile>(`/api/members/${memberId}/card`, { method: 'DELETE' });
}

export async function setAutoPay(memberId: string, enabled: boolean, memberAgreed: boolean, version?: string): Promise<MemberPaymentProfile> {
  return authFetchJson<MemberPaymentProfile>(`/api/members/${memberId}/autopay`, {
    method: 'PUT',
    body: JSON.stringify({ enabled, memberAgreed, version: version || undefined }),
  });
}

export async function chargeSavedCard(memberId: string, chargeIds?: string[]): Promise<OnlinePayment> {
  return authFetchJson<OnlinePayment>(`/api/members/${memberId}/card/charge`, { method: 'POST', body: JSON.stringify({ chargeIds }) });
}

export async function getOnlinePayments(filter: { memberId?: string; status?: string } = {}, req?: RequestOptions): Promise<OnlinePayment[]> {
  if (servesDemoData()) return [];
  const query = new URLSearchParams();
  if (filter.memberId) query.set('memberId', filter.memberId);
  if (filter.status) query.set('status', filter.status);
  const res = await authFetchJson<OnlinePayment[]>(`/api/online-payments${query.size ? `?${query}` : ''}`, { signal: req?.signal });
  return Array.isArray(res) ? res : [];
}

// ── bank files ──────────────────────────────────────────────────────────────

export interface BankImport {
  id: string;
  fileName: string;
  format: string;
  messageId?: string;
  accountIban?: string;
  transactions: number;
  matched: number;
  unmatched: number;
  alreadyImported: number;
  skipped: number;
  amountMatched: number;
  importedAtUtc: string;
  importedByName?: string;
}

export type BankTransactionStatus = 'matched' | 'unmatched' | 'allocated' | 'ignored';

export interface BankTransaction {
  id: string;
  importId: string;
  bankReference: string;
  accountIban?: string;
  bookedAtUtc: string;
  valueDateUtc?: string;
  amount: number;
  currency: string;
  referenceType?: 'QRR' | 'SCOR';
  reference?: string;
  debtorName?: string;
  debtorIban?: string;
  message?: string;
  status: BankTransactionStatus;
  note?: string;
  memberId?: string;
  memberName?: string;
  ledgerPaymentId?: string;
  resolvedAtUtc?: string;
  resolvedByName?: string;
  version: string;
}

export async function getBankImports(req?: RequestOptions): Promise<BankImport[]> {
  if (servesDemoData()) return [];
  const res = await authFetchJson<BankImport[]>('/api/bank/imports', { signal: req?.signal });
  return Array.isArray(res) ? res : [];
}

export async function getBankTransactions(status?: BankTransactionStatus, req?: RequestOptions): Promise<BankTransaction[]> {
  if (servesDemoData()) return [];
  const res = await authFetchJson<BankTransaction[]>(`/api/bank/transactions${status ? `?status=${status}` : ''}`, { signal: req?.signal });
  return Array.isArray(res) ? res : [];
}

/** Sends the bank's camt file as the browser read it. */
export async function importBankFile(fileName: string, content: string): Promise<BankImport> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<BankImport>('/api/bank/imports', { method: 'POST', body: JSON.stringify({ fileName, content }) });
}

export async function allocateBankTransaction(id: string, memberId: string, version?: string): Promise<BankTransaction> {
  return authFetchJson<BankTransaction>(`/api/bank/transactions/${id}/allocate`, {
    method: 'POST',
    body: JSON.stringify({ memberId, version: version || undefined }),
  });
}

export async function ignoreBankTransaction(id: string, reason: string, version?: string): Promise<BankTransaction> {
  return authFetchJson<BankTransaction>(`/api/bank/transactions/${id}/ignore`, {
    method: 'POST',
    body: JSON.stringify({ reason, version: version || undefined }),
  });
}

// ── the accounting journal ──────────────────────────────────────────────────

export type JournalLayout = 'gross' | 'net';

export interface JournalAccount {
  account: string;
  debits: number;
  credits: number;
  net: number;
}

export interface JournalRow {
  dateUtc: string;
  /** The day in the workspace's time zone (yyyy-MM-dd), as the CSV prints it. */
  date: string;
  document: string;
  debit: string;
  credit: string;
  amount: number;
  currency: string;
  vatCode?: string;
  vatRate?: number;
  vatAmount?: number;
  text: string;
  member?: string;
  type: string;
  entryId: string;
}

export interface Journal {
  fromUtc: string;
  toUtc: string;
  layout: JournalLayout;
  currency: string;
  rows: JournalRow[];
  accounts: JournalAccount[];
  mapping: AccountingAccounts;
}

function journalQuery(fromUtc: string, toUtc: string, layout: JournalLayout): string {
  return `from=${encodeURIComponent(fromUtc)}&to=${encodeURIComponent(toUtc)}&layout=${layout}`;
}

export async function getJournal(fromUtc: string, toUtc: string, layout: JournalLayout): Promise<Journal> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  return authFetchJson<Journal>(`/api/accounting/journal?${journalQuery(fromUtc, toUtc, layout)}`);
}

export async function downloadJournalCsv(fromUtc: string, toUtc: string, layout: JournalLayout): Promise<void> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  const res = await authFetch(`/api/accounting/journal?${journalQuery(fromUtc, toUtc, layout)}&format=csv`);
  await ensureOk(res);
  const header = res.headers.get('Content-Disposition') ?? '';
  const name = /filename="?([^";]+)"?/i.exec(header)?.[1] ?? 'journal.csv';
  downloadFile(await res.blob(), name);
}

// ── the public pay page ─────────────────────────────────────────────────────
// Opened by a member with no account: the token in the link is the whole permission.

export class PayLinkError extends Error {
  readonly status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = 'PayLinkError';
    this.status = status;
  }
}

export interface PublicPayPage {
  clubName: string;
  memberFirstName: string;
  purpose: PayLinkPurpose;
  description: string;
  amount: number;
  currency: string;
  methods: string[];
  available: boolean;
  reason?: string;
  autoPay: boolean;
}

async function publicCall<T>(path: string, method: 'GET' | 'POST'): Promise<T> {
  const base = getApiBaseUrl();
  if (!base) throw new PayLinkError('VITE_API_URL is not set', 0);
  let res: Response;
  try {
    res = await fetch(`${base}${path}`, { method });
  } catch {
    throw new PayLinkError('Could not reach the server', 0);
  }
  if (!res.ok) {
    let detail = '';
    try {
      const body = (await res.json()) as { detail?: string };
      detail = body?.detail ?? '';
    } catch {
      // No sentence in the answer; the status says enough.
    }
    throw new PayLinkError(detail || 'Not available', res.status);
  }
  return res.json() as Promise<T>;
}

/** What the link asks for. Opening it changes nothing. */
export const getPayPage = (token: string) => publicCall<PublicPayPage>(`/api/public/pay/${encodeURIComponent(token)}`, 'GET');

/** Starts Stripe's page for the link; the answer says where it is. */
export const startCheckout = (token: string) => publicCall<{ url: string }>(`/api/public/pay/${encodeURIComponent(token)}/checkout`, 'POST');
