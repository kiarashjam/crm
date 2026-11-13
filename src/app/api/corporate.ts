// Companies and private clients at the club: a house account on the ledger for a company or a
// contact, corporate memberships whose seats a company's nominees fill, and private events
// (a client's hire of the club's spaces, its menu, deposit, invoice, contract and guest list).
//
// All of it is kept on the server: the account's balance on the ledger, a seat taken against
// the membership's row, a private event's spaces held in the bookings diary. Demo data has
// none of it, so reads answer empty and writes say so rather than pretend.

import { authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import type { Charge } from './charges';
import { downloadFile } from '@/app/lib/exportCsv';

export const CORPORATE_DEMO_MESSAGE = 'Company accounts, corporate memberships and private events are kept by the live app; demo data has none.';

function live(): void {
  if (servesDemoData()) throw new Error(CORPORATE_DEMO_MESSAGE);
}

const json = (body: unknown) => JSON.stringify(body ?? {});

/** Sends the version a screen read, so a change made meanwhile is refused (409) rather than overwritten. */
const ifMatch = (version?: string) => (version ? { 'If-Match': `"${version}"` } : undefined);

/** The file name the server gave, or a plain one. */
function fileNameOf(res: Response, fallback: string): string {
  const header = res.headers.get('Content-Disposition') ?? '';
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header)?.[1];
  if (star) return decodeURIComponent(star);
  return /filename="?([^";]+)"?/i.exec(header)?.[1] ?? fallback;
}

// ── house accounts of companies and private clients ─────────────────────────

/** Whose account: a CRM company's, or a CRM contact's as a private client. */
export type ClientKind = 'company' | 'contact';

const segment = (kind: ClientKind) => (kind === 'company' ? 'companies' : 'contacts');

export interface ClientAccount {
  kind: ClientKind;
  id: string;
  name: string;
  /** Whether the account is open: charges can be posted to it. */
  open: boolean;
  currency: string;
  /** Payments less charges: below zero, what the client owes. */
  balance: number;
  owed: number;
  credit: number;
  openCharges: number;
  openedAtUtc?: string;
}

/** The client's account, open or not (a company with none yet reads as not open). */
export async function getClientAccount(kind: ClientKind, id: string, req?: RequestOptions): Promise<ClientAccount | null> {
  if (servesDemoData()) return null;
  return authFetchJson<ClientAccount>(`/api/club-accounts/${segment(kind)}/${id}`, { signal: req?.signal });
}

/** Opens the client's account (money.manage); opening one already open changes nothing. */
export function openClientAccount(kind: ClientKind, id: string): Promise<ClientAccount> {
  live();
  return authFetchJson<ClientAccount>(`/api/club-accounts/${segment(kind)}/${id}`, { method: 'POST' });
}

/** The charges on an account, newest first, whoever's account it is (a member's, a company's, a contact's). */
export async function getAccountCharges(accountId: string, req?: RequestOptions): Promise<Charge[]> {
  if (servesDemoData()) return [];
  const rows = await authFetchJson<Charge[]>(`/api/charges?memberId=${encodeURIComponent(accountId)}`, { signal: req?.signal });
  return Array.isArray(rows) ? rows : [];
}

/**
 * Downloads the client's statement for [fromUtc, toUtc) as the server's PDF: for a company, its
 * own charges and its nominees' (who ran up what), with the QR-bill when the club's bank is set.
 */
export async function downloadClientStatementPdf(kind: ClientKind, id: string, fromUtc: string, toUtc: string): Promise<void> {
  live();
  const res = await authFetch(
    `/api/club-accounts/${segment(kind)}/${id}/statement.pdf?from=${encodeURIComponent(fromUtc)}&to=${encodeURIComponent(toUtc)}`);
  await ensureOk(res);
  downloadFile(await res.blob(), fileNameOf(res, 'statement.pdf'));
}

// ── corporate memberships ────────────────────────────────────────────────────

export type CorporateStatus = 'Active' | 'Ended' | string;

/** Whose account a nominee's own charges go on: the company's, or the nominee's. */
export type NomineeCharges = 'company' | 'nominee';

export const NOMINEE_CHARGES_LABELS: Record<NomineeCharges, string> = {
  company: 'The company pays its nominees’ charges',
  nominee: 'Nominees pay their own charges',
};

export type RenewalState = 'current' | 'due' | 'overdue' | 'not_renewing' | 'ended';

export const RENEWAL_STATE_LABELS: Record<RenewalState, string> = {
  current: 'Current',
  due: 'Renewal due',
  overdue: 'Renewal overdue',
  not_renewing: 'Not renewing',
  ended: 'Ended',
};

export interface CorporateNominee {
  id: string;
  memberId: string;
  memberName: string;
  memberStatus?: string;
  memberEmail?: string;
  addedAtUtc: string;
  addedByName?: string;
  removedAtUtc?: string;
  removedReason?: string;
  replacedByMemberId?: string;
  /** The nominee's own dues still run beside the company's (the club may want to stop them). */
  ownDuesRunning: boolean;
}

export interface CorporateDues {
  subscriptionId: string;
  name: string;
  amount: number;
  currency: string;
  cadence: string;
  status: string;
  nextChargeAtUtc?: string;
}

export interface CorporateMembership {
  id: string;
  companyId: string;
  companyName: string;
  name: string;
  seats: number;
  seatsUsed: number;
  status: CorporateStatus;
  nomineeCharges: NomineeCharges;
  startsAtUtc: string;
  renewsAtUtc: string;
  termMonths: number;
  notRenewing: boolean;
  notRenewingReason?: string;
  lastRenewedAtUtc?: string;
  renewalState: RenewalState;
  daysToRenewal: number;
  dues?: CorporateDues;
  billingContactId?: string;
  billingContactName?: string;
  billingEmail?: string;
  language?: string;
  notes?: string;
  nominees: CorporateNominee[];
  formerNominees: CorporateNominee[];
  account?: ClientAccount;
  endedAtUtc?: string;
  endedReason?: string;
  createdAtUtc: string;
  version: string;
}

export interface CorporateDuesInput {
  catalogItemId?: string;
  amount?: number;
  cadence?: string;
  firstBillingAtUtc?: string;
}

export interface CorporateMembershipInput {
  companyId?: string;
  name?: string;
  seats?: number;
  nomineeCharges?: NomineeCharges;
  startsAtUtc?: string;
  renewsAtUtc?: string;
  termMonths?: number;
  billingContactId?: string | null;
  billingEmail?: string;
  language?: string;
  notes?: string;
  dues?: CorporateDuesInput;
}

export interface CorporateQuery {
  companyId?: string;
  status?: string;
  renewingWithinDays?: number;
}

/** The seat a member holds on a company's corporate membership. */
export interface MemberCorporateSeat {
  membershipId: string;
  name: string;
  companyId: string;
  companyName: string;
  nomineeCharges: NomineeCharges;
  renewsAtUtc: string;
  nomineeId: string;
}

export async function getCorporateMemberships(query: CorporateQuery = {}, req?: RequestOptions): Promise<CorporateMembership[]> {
  if (servesDemoData()) return [];
  const params = new URLSearchParams();
  if (query.companyId) params.set('companyId', query.companyId);
  if (query.status) params.set('status', query.status);
  if (query.renewingWithinDays) params.set('renewingWithinDays', String(query.renewingWithinDays));
  const qs = params.toString();
  const rows = await authFetchJson<CorporateMembership[]>(`/api/corporate-memberships${qs ? `?${qs}` : ''}`, { signal: req?.signal });
  return Array.isArray(rows) ? rows : [];
}

export async function getCorporateMembership(id: string, req?: RequestOptions): Promise<CorporateMembership | null> {
  if (servesDemoData()) return null;
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}`, { signal: req?.signal });
}

/** Makes the company's corporate membership and opens its account; dues sent with it take money.manage. */
export function createCorporateMembership(input: CorporateMembershipInput): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>('/api/corporate-memberships', { method: 'POST', body: json(input) });
}

export function updateCorporateMembership(id: string, input: CorporateMembershipInput, version: string): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}`, {
    method: 'PUT', body: json({ ...input, version }), headers: ifMatch(version),
  });
}

/** Starts or changes the company's dues on its own account (money.manage). */
export function setCorporateDues(id: string, dues: CorporateDuesInput): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}/dues`, { method: 'PUT', body: json(dues) });
}

export function addNominee(id: string, memberId: string): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}/nominees`, { method: 'POST', body: json({ memberId }) });
}

/** Gives a nominee's seat to another member in one step. */
export function swapNominee(id: string, nomineeId: string, memberId: string, reason?: string): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}/nominees/${nomineeId}/swap`, {
    method: 'POST', body: json({ memberId, reason }),
  });
}

export function removeNominee(id: string, nomineeId: string, reason?: string): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}/nominees/${nomineeId}/remove`, {
    method: 'POST', body: json({ reason }),
  });
}

/** Renews the membership for another term, on the company. */
export function renewCorporateMembership(id: string, version: string): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}/renew`, {
    method: 'POST', body: json({ version }), headers: ifMatch(version),
  });
}

export function markCorporateNotRenewing(id: string, reason: string, version: string): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}/not-renewing`, {
    method: 'POST', body: json({ reason, version }), headers: ifMatch(version),
  });
}

/** Ends the membership: its nominees leave their seats and its dues stop (money.manage while they run). */
export function endCorporateMembership(id: string, reason: string): Promise<CorporateMembership> {
  live();
  return authFetchJson<CorporateMembership>(`/api/corporate-memberships/${id}/end`, { method: 'POST', body: json({ reason }) });
}

/** The seat the member holds, or null when they hold none. */
export async function getMemberCorporateSeat(memberId: string, req?: RequestOptions): Promise<MemberCorporateSeat | null> {
  if (servesDemoData()) return null;
  const res = await authFetch(`/api/members/${memberId}/corporate`, { signal: req?.signal });
  if (res.status === 204) return null;
  await ensureOk(res);
  const text = await res.text();
  return text ? (JSON.parse(text) as MemberCorporateSeat) : null;
}

// ── private events ───────────────────────────────────────────────────────────

export type PrivateEventStatus = 'Enquiry' | 'Confirmed' | 'Completed' | 'Cancelled';

export const PRIVATE_EVENT_STATUS_LABELS: Record<PrivateEventStatus, string> = {
  Enquiry: 'Enquiry',
  Confirmed: 'Confirmed',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export type DepositState = 'none' | 'charged' | 'paid' | 'set_against_invoice' | 'released' | 'kept';

export const DEPOSIT_STATE_LABELS: Record<DepositState, string> = {
  none: 'No deposit asked',
  charged: 'Deposit asked, not paid yet',
  paid: 'Deposit paid',
  set_against_invoice: 'Deposit set against the invoice',
  released: 'Deposit given back as credit',
  kept: 'Deposit kept',
};

export interface PrivateEventSpace {
  reservationId: string;
  resourceId: string;
  resourceName: string;
  status: string;
}

export interface PrivateEventLine {
  id: string;
  name: string;
  quantity: number;
  unitPrice: number;
  total: number;
  vatRate?: number;
  catalogItemId?: string;
  chargeId?: string;
}

export interface PrivateEventGuest {
  id: string;
  name: string;
  email?: string;
  memberId?: string;
  notes?: string;
  checkedInAtUtc?: string;
  checkedInByName?: string;
}

export interface PrivateEventMoney {
  billToKind?: 'company' | 'contact' | 'member';
  billToAccountId?: string;
  billToName?: string;
  linesTotal: number;
  depositAmount?: number;
  depositState: DepositState;
  depositChargeId?: string;
  depositPaid: number;
  invoiced: boolean;
  invoiceChargeIds: string[];
  invoiceTotal: number;
  invoicePaid: number;
}

export interface PrivateEvent {
  id: string;
  title: string;
  companyId?: string;
  companyName?: string;
  contactId?: string;
  contactName?: string;
  dealId?: string;
  dealName?: string;
  startAtUtc: string;
  endAtUtc: string;
  guestCount: number;
  status: PrivateEventStatus;
  notes?: string;
  currency: string;
  spaces: PrivateEventSpace[];
  lines: PrivateEventLine[];
  guests: PrivateEventGuest[];
  guestsCheckedIn: number;
  money: PrivateEventMoney;
  contractId?: string;
  contractStatus?: string;
  confirmedAtUtc?: string;
  cancelledAtUtc?: string;
  cancelReason?: string;
  createdAtUtc: string;
  version: string;
}

export interface PrivateEventLineInput {
  catalogItemId?: string;
  name?: string;
  quantity?: number;
  unitPrice?: number;
  vatRate?: number;
}

export interface PrivateEventInput {
  title?: string;
  /** A client: a company, a contact, or both (the company's contact for the event). Empty string unlinks. */
  companyId?: string;
  contactId?: string;
  dealId?: string;
  startAtUtc?: string;
  endAtUtc?: string;
  guestCount?: number;
  /** The spaces to hold; left out, those held stay. */
  resourceIds?: string[];
  notes?: string;
  lines?: PrivateEventLineInput[];
}

export interface PrivateEventQuery {
  fromUtc?: string;
  toUtc?: string;
  status?: string;
  companyId?: string;
  contactId?: string;
}

/** The Guid the server reads as "unlink this". */
export const UNLINK = '00000000-0000-0000-0000-000000000000';

export async function getPrivateEvents(query: PrivateEventQuery = {}, req?: RequestOptions): Promise<PrivateEvent[]> {
  if (servesDemoData()) return [];
  const params = new URLSearchParams();
  if (query.fromUtc) params.set('from', query.fromUtc);
  if (query.toUtc) params.set('to', query.toUtc);
  if (query.status) params.set('status', query.status);
  if (query.companyId) params.set('companyId', query.companyId);
  if (query.contactId) params.set('contactId', query.contactId);
  const qs = params.toString();
  const rows = await authFetchJson<PrivateEvent[]>(`/api/private-events${qs ? `?${qs}` : ''}`, { signal: req?.signal });
  return Array.isArray(rows) ? rows : [];
}

export async function getPrivateEvent(id: string, req?: RequestOptions): Promise<PrivateEvent | null> {
  if (servesDemoData()) return null;
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}`, { signal: req?.signal });
}

/** Makes the event and holds its spaces; a booking in the way refuses it (409). */
export function createPrivateEvent(input: PrivateEventInput): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>('/api/private-events', { method: 'POST', body: json(input) });
}

export function updatePrivateEvent(id: string, input: PrivateEventInput, version: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}`, { method: 'PUT', body: json({ ...input, version }), headers: ifMatch(version) });
}

export function addPrivateEventLine(id: string, line: PrivateEventLineInput): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/lines`, { method: 'POST', body: json(line) });
}

export function updatePrivateEventLine(id: string, lineId: string, line: PrivateEventLineInput): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/lines/${lineId}`, { method: 'PUT', body: json(line) });
}

export function removePrivateEventLine(id: string, lineId: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/lines/${lineId}`, { method: 'DELETE' });
}

export function confirmPrivateEvent(id: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/confirm`, { method: 'POST' });
}

export function completePrivateEvent(id: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/complete`, { method: 'POST' });
}

/** Cancels the event and frees its spaces; a deposit asked is kept, or given back as credit (money.manage). */
export function cancelPrivateEvent(id: string, reason: string, deposit?: 'keep' | 'release'): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/cancel`, { method: 'POST', body: json({ reason, deposit }) });
}

/** Asks the deposit: a charge on the client's house account (money.manage). */
export function requestPrivateEventDeposit(id: string, amount: number, dueAtUtc?: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/deposit`, { method: 'POST', body: json({ amount, dueAtUtc }) });
}

/** Posts the final invoice: one charge per VAT rate, the deposit paid set against it (money.manage). */
export function invoicePrivateEvent(id: string, dueAtUtc?: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/invoice`, { method: 'POST', body: json({ dueAtUtc }) });
}

/** Drafts the event's contract on its deal, from the private event template. */
export function draftPrivateEventContract(id: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/contract`, { method: 'POST' });
}

export interface PrivateEventGuestInput {
  name?: string;
  email?: string;
  memberId?: string;
  notes?: string;
}

export function addPrivateEventGuest(id: string, guest: PrivateEventGuestInput): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/guests`, { method: 'POST', body: json(guest) });
}

export function removePrivateEventGuest(id: string, guestId: string): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/guests/${guestId}`, { method: 'DELETE' });
}

/** Checks a guest in at the door, or takes back a check-in made in error. */
export function checkInPrivateEventGuest(id: string, guestId: string, undo = false): Promise<PrivateEvent> {
  live();
  return authFetchJson<PrivateEvent>(`/api/private-events/${id}/guests/${guestId}/${undo ? 'undo-check-in' : 'check-in'}`, { method: 'POST' });
}
