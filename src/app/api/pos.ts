// The club's POS, connected: Lightspeed Restaurant K-Series. Its closed tickets come in by a
// scheduled import, are matched to members by the codes they carry, and are posted to their
// house accounts by the server: owing when put on the account, paid when paid at the POS.
// Tickets that need a member and name none wait here for staff, as do those that could not be
// posted. Members' codes (a member number, a card's code or QR code) are what the POS records.
//
// All of it is the server's; demo data has no POS.

import { authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';

export const POS_DEMO_MESSAGE = 'The POS is connected on the live app; demo data has none.';

export interface PosStatus {
  provider: string;
  /** Whether this server has Cadence's Lightspeed client: without it nobody can connect. */
  serverConfigured: boolean;
  connected: boolean;
  importing: boolean;
  locationName?: string;
  lastImportAtUtc?: string;
  lastImportError?: string;
  /** Tickets waiting for staff: unmatched or failed. */
  needsAttention: number;
}

/** What a POS payment method means. */
export type PosMeans = 'HouseAccount' | 'Card' | 'Cash' | 'Twint' | 'Skip';

export const POS_MEANS: PosMeans[] = ['HouseAccount', 'Card', 'Cash', 'Twint', 'Skip'];

export const POS_MEANS_LABEL: Record<PosMeans | 'Unknown', string> = {
  HouseAccount: 'House account',
  Card: 'Card',
  Cash: 'Cash',
  Twint: 'TWINT',
  Skip: 'Not a member sale',
  Unknown: 'Not known',
};

export interface PosSettings {
  provider: string;
  serverConfigured: boolean;
  connected: boolean;
  connectedAtUtc?: string;
  connectedByName?: string;
  tokenUnreadable: boolean;
  enabled: boolean;
  locationId?: string;
  locationName?: string;
  importFromUtc?: string;
  methodMap: Record<string, PosMeans>;
  defaultVenue: string;
  lastImportAtUtc?: string;
  lastImportError?: string;
  lastImportCount: number;
  /** Registered with Lightspeed for Cadence's client: where it sends the browser back. */
  redirectUri: string;
  updatedAtUtc?: string;
  updatedByName?: string;
  version: string;
}

export interface PosSettingsInput {
  enabled?: boolean;
  locationId?: string;
  locationName?: string;
  importFromUtc?: string;
  methodMap?: Record<string, PosMeans>;
  defaultVenue?: string;
  version?: string;
}

export interface PosLocation {
  id: string;
  name: string;
  currency?: string;
}

export interface PosImportResult {
  read: number;
  imported: number;
  posted: number;
  unmatched: number;
  failed: number;
  skipped: number;
  finishedAtUtc: string;
}

export type PosTicketStatus = 'posted' | 'unmatched' | 'failed' | 'skipped' | 'ignored';

export const POS_STATUS_LABEL: Record<PosTicketStatus, string> = {
  posted: 'Posted',
  unmatched: 'Needs a member',
  failed: 'Not posted',
  skipped: 'Not a member sale',
  ignored: 'Set aside',
};

export interface PosTicketLine {
  name: string;
  quantity: number;
  total: number;
  vatRate?: number;
  discount: number;
  discountName?: string;
}

export interface PosTicketPayment {
  method: string;
  means: PosMeans | 'Unknown';
  amount: number;
  tip: number;
}

export interface PosTicket {
  id: string;
  provider: string;
  externalId: string;
  receiptNumber?: string;
  closedAtUtc: string;
  currency: string;
  total: number;
  onAccountAmount: number;
  paidAmount: number;
  tipAmount: number;
  tenders?: string;
  staffName?: string;
  tableName?: string;
  customerName?: string;
  customerReference?: string;
  /** The POS put one of its customers on it: matching it can remember them for next time. */
  hasCustomer: boolean;
  status: PosTicketStatus;
  note?: string;
  attempts: number;
  memberId?: string;
  memberName?: string;
  matchedBy?: 'code' | 'customer' | 'email' | 'staff';
  saleId?: string;
  pointsAwarded?: number;
  importedAtUtc: string;
  postedAtUtc?: string;
  resolvedByName?: string;
  lines: PosTicketLine[];
  payments: PosTicketPayment[];
  version: string;
}

/** Which tickets to list: those waiting for staff, one status, or all. */
export type PosTicketFilter = 'attention' | PosTicketStatus | 'all';

export interface MemberCode {
  id: string;
  memberId: string;
  /** card: a member number or card code; pos-customer: the POS's own customer, remembered from a match. */
  kind: 'card' | 'pos-customer';
  code: string;
  label?: string;
  createdAtUtc: string;
  createdByName?: string;
  /** The code's QR code: a row of 1s (dark) and 0s per line of modules. */
  qrRows?: string[];
}

const NOT_CONNECTED: PosStatus = { provider: 'lightspeed-k', serverConfigured: false, connected: false, importing: false, needsAttention: 0 };

export async function getPosStatus(req?: RequestOptions): Promise<PosStatus> {
  if (servesDemoData()) return NOT_CONNECTED;
  return authFetchJson<PosStatus>('/api/pos/status', { signal: req?.signal });
}

export async function getPosSettings(req?: RequestOptions): Promise<PosSettings> {
  if (servesDemoData()) throw new Error(POS_DEMO_MESSAGE);
  const settings = await authFetchJson<PosSettings>('/api/pos/settings', { signal: req?.signal });
  return { ...settings, methodMap: settings.methodMap ?? {} };
}

export async function savePosSettings(input: PosSettingsInput): Promise<PosSettings> {
  if (servesDemoData()) throw new Error(POS_DEMO_MESSAGE);
  return authFetchJson<PosSettings>('/api/pos/settings', { method: 'PUT', body: JSON.stringify(input) });
}

/** Where to send the browser to let Cadence read the club's Lightspeed account. */
export async function connectPos(): Promise<string> {
  if (servesDemoData()) throw new Error(POS_DEMO_MESSAGE);
  const { url } = await authFetchJson<{ url: string }>('/api/pos/connect', { method: 'POST' });
  return url;
}

export async function disconnectPos(): Promise<PosSettings> {
  return authFetchJson<PosSettings>('/api/pos/disconnect', { method: 'POST' });
}

export async function getPosLocations(req?: RequestOptions): Promise<PosLocation[]> {
  if (servesDemoData()) return [];
  const res = await authFetchJson<PosLocation[]>('/api/pos/locations', { signal: req?.signal });
  return Array.isArray(res) ? res : [];
}

export async function importPosNow(): Promise<PosImportResult> {
  if (servesDemoData()) throw new Error(POS_DEMO_MESSAGE);
  return authFetchJson<PosImportResult>('/api/pos/import', { method: 'POST' });
}

export async function getPosTickets(filter: PosTicketFilter = 'attention', req?: RequestOptions): Promise<PosTicket[]> {
  if (servesDemoData()) return [];
  const res = await authFetchJson<PosTicket[]>(`/api/pos/tickets?status=${filter}`, { signal: req?.signal });
  return Array.isArray(res) ? res : [];
}

export async function matchPosTicket(id: string, memberId: string, remember: boolean, version?: string): Promise<PosTicket> {
  return authFetchJson<PosTicket>(`/api/pos/tickets/${id}/match`, {
    method: 'POST',
    body: JSON.stringify({ memberId, remember, version: version || undefined }),
  });
}

export async function retryPosTicket(id: string, version?: string): Promise<PosTicket> {
  return authFetchJson<PosTicket>(`/api/pos/tickets/${id}/retry`, { method: 'POST', body: JSON.stringify({ version: version || undefined }) });
}

export async function ignorePosTicket(id: string, reason: string, version?: string): Promise<PosTicket> {
  return authFetchJson<PosTicket>(`/api/pos/tickets/${id}/ignore`, {
    method: 'POST',
    body: JSON.stringify({ reason, version: version || undefined }),
  });
}

export async function getMemberCodes(memberId: string, req?: RequestOptions): Promise<MemberCode[]> {
  if (servesDemoData()) return [];
  const res = await authFetchJson<MemberCode[]>(`/api/members/${memberId}/codes`, { signal: req?.signal });
  return Array.isArray(res) ? res : [];
}

/** Gives the member a code: the one typed (a member number, a card's code) or, with none, a new one. */
export async function addMemberCode(memberId: string, code?: string, label?: string): Promise<MemberCode> {
  if (servesDemoData()) throw new Error(POS_DEMO_MESSAGE);
  return authFetchJson<MemberCode>(`/api/members/${memberId}/codes`, {
    method: 'POST',
    body: JSON.stringify({ code: code?.trim() || undefined, label: label?.trim() || undefined }),
  });
}

export async function removeMemberCode(memberId: string, codeId: string): Promise<void> {
  await ensureOk(await authFetch(`/api/members/${memberId}/codes/${codeId}`, { method: 'DELETE' }));
}
