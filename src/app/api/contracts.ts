// Contracts: draft, edit, send for signature, countersign.
//
// Two audiences, two shapes. `ContractDto` is what the CRM user sees — full body,
// audit trail, allowed actions. `PublicContract` is what a stranger holding a
// signing link sees, and it deliberately carries no ids, no lead and no audit
// trail; the server projects it that way and this mirrors it so nothing here can
// accidentally expect more.
//
// Unlike most of this API layer these calls have NO demo-mode fallback. Everything
// else degrades to a localStorage mock so the product is explorable without a
// backend, but a contract that pretends to have been sent — with a signing link
// that goes nowhere — is worse than a feature that says it needs a server.

import { authFetch, authFetchJson, getApiBaseUrl, isUsingRealApi } from './apiClient';

export type ContractStatus =
  | 'draft' | 'sent' | 'signed_by_client' | 'countersigned' | 'declined' | 'voided';

export interface ContractEvent {
  id: string;
  type: string;
  detail?: string;
  actorLabel?: string;
  atUtc: string;
}

export interface Contract {
  id: string;
  leadId?: string;
  dealId?: string;
  status: ContractStatus;
  title: string;
  body: string;
  counterpartyName: string;
  counterpartyEmail: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  sentAtUtc?: string;
  firstViewedAtUtc?: string;
  clientSignatureName?: string;
  clientSignedAtUtc?: string;
  counterSignatureName?: string;
  counterSignedAtUtc?: string;
  executedCopySentAtUtc?: string;
  closedReason?: string;
  /** What the SERVER will permit. The authority; the local table only greys buttons. */
  allowedActions: string[];
  /** Placeholders still unfilled. Sending is refused while this is non-empty. */
  unresolvedFields: string[];
  /** Only ever present on the response to a send. */
  signingUrl?: string;
  events: ContractEvent[];
}

/** What the counterparty sees. Narrower on purpose. */
export interface PublicContract {
  status: ContractStatus;
  title: string;
  body: string;
  counterpartyName: string;
  organizationName: string;
  sentAtUtc?: string;
  clientSignatureName?: string;
  clientSignedAtUtc?: string;
  counterSignatureName?: string;
  counterSignedAtUtc?: string;
  canSign: boolean;
  /** Why not, when it cannot be signed. */
  blocked?: string;
}

export interface SendResult {
  contract: Contract;
  /**
   * Whether the email actually left.
   *
   * False is a normal outcome, not an error — SMTP is not configured in every
   * environment. The contract IS sent and the link IS live; nobody has been told.
   * The UI must show the link rather than report a delivery that did not happen.
   */
  emailSent: boolean;
  signingUrl: string;
}

/** Thrown so callers can show the server's own message rather than a generic one. */
export class ContractError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ContractError';
  }
}

function requireApi(): void {
  if (!isUsingRealApi()) {
    throw new ContractError(
      'Contracts need a connected backend. In demo mode there is nowhere to send a signing link.',
    );
  }
}

export async function listContractsForLead(leadId: string): Promise<Contract[]> {
  if (!isUsingRealApi()) return [];
  const list = await authFetchJson<Contract[]>(`/api/contracts/for-lead/${leadId}`);
  return Array.isArray(list) ? list : [];
}

export async function getContract(id: string): Promise<Contract | null> {
  requireApi();
  return await authFetchJson<Contract>(`/api/contracts/${id}`);
}

export interface DraftArgs {
  leadId?: string;
  title?: string;
  templateOverride?: string;
  /** Values for placeholders the CRM cannot know — fee, term, jurisdiction. */
  values?: Record<string, string>;
}

export async function createContractDraft(args: DraftArgs): Promise<Contract | null> {
  requireApi();
  return await authFetchJson<Contract>('/api/contracts/draft', {
    method: 'POST',
    body: JSON.stringify(args),
  });
}

export async function updateContract(
  id: string,
  patch: { title?: string; body?: string; counterpartyName?: string; counterpartyEmail?: string },
): Promise<Contract | null> {
  requireApi();
  return await authFetchJson<Contract>(`/api/contracts/${id}`, {
    method: 'PUT',
    body: JSON.stringify(patch),
  });
}

export async function sendContract(id: string, resend = false): Promise<SendResult | null> {
  requireApi();
  return await authFetchJson<SendResult>(
    `/api/contracts/${id}/send?resend=${resend ? 'true' : 'false'}`,
    { method: 'POST' },
  );
}

export async function countersignContract(
  id: string,
  signatureName: string,
  agreed: boolean,
): Promise<Contract | null> {
  requireApi();
  return await authFetchJson<Contract>(`/api/contracts/${id}/countersign`, {
    method: 'POST',
    body: JSON.stringify({ signatureName, agreed }),
  });
}

export async function resendExecutedCopy(id: string): Promise<boolean> {
  requireApi();
  const ok = await authFetchJson<boolean>(`/api/contracts/${id}/resend-copy`, { method: 'POST' });
  return ok === true;
}

export async function voidContract(id: string, reason?: string): Promise<Contract | null> {
  requireApi();
  return await authFetchJson<Contract>(`/api/contracts/${id}/void`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

/* --------------------------------------------------------------- documents */

/**
 * The contract as a PDF.
 *
 * Fetched rather than linked, because the CRM endpoint needs the bearer token and
 * a bare `<a href>` cannot carry one. The blob URL is the caller's to revoke —
 * see `openContractPdf`, which is what components should use.
 */
export async function fetchContractPdf(id: string): Promise<Blob> {
  requireApi();
  const res = await authFetch(`/api/contracts/${id}/pdf`, { method: 'GET' });
  if (!res.ok) {
    throw new ContractError(
      res.status === 404
        ? 'That contract could not be found.'
        : 'The contract document could not be produced.',
    );
  }
  return await res.blob();
}

/**
 * Opens the contract PDF in a new tab, and cleans up after itself.
 *
 * Opened rather than downloaded: the common case is reading the thing, and a file
 * that lands silently in Downloads reads as nothing having happened. Returns false
 * when the browser blocked the tab, so the caller can say so instead of leaving the
 * user staring at an unchanged page.
 */
export async function openContractPdf(id: string): Promise<boolean> {
  const blob = await fetchContractPdf(id);
  const url = URL.createObjectURL(blob);
  const opened = window.open(url, '_blank', 'noopener');
  if (!opened) {
    URL.revokeObjectURL(url);
    return false;
  }
  // Long enough for the new tab to have loaded it. Revoking immediately races the
  // navigation and shows an empty viewer; never revoking leaks the blob for the
  // lifetime of the page.
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
  return true;
}

/* ------------------------------------------------------------ public paths */

/**
 * The counterparty's calls. Unauthenticated by design — they have no account, and
 * requiring one to sign a contract somebody sent you is a dead end.
 *
 * `fetch` directly rather than `authFetchJson`, which would attach a bearer token
 * the signer does not have and, worse, could attach a CRM user's token if one
 * happened to be in this browser.
 */
function publicUrl(path: string): string {
  // `getApiBaseUrl` already normalises and strips the trailing slash; re-reading
  // the env var here would be a second place to keep in step.
  return `${getApiBaseUrl() ?? ''}${path}`;
}

async function publicCall<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(publicUrl(path), {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
  });
  const text = await res.text();
  let parsed: unknown = null;
  try {
    parsed = text ? JSON.parse(text) : null;
  } catch {
    parsed = null;
  }
  if (!res.ok) {
    // The server's own wording — "this link has expired", "already signed" — is
    // more use to a signer than a status code.
    const message = (parsed as { message?: string; detail?: string } | null)?.message
      ?? (parsed as { detail?: string } | null)?.detail
      ?? 'This signing link could not be opened.';
    throw new ContractError(message);
  }
  return parsed as T;
}

export function getPublicContract(token: string): Promise<PublicContract> {
  return publicCall<PublicContract>(`/api/public/contracts/${encodeURIComponent(token)}`);
}

export function signPublicContract(
  token: string,
  signatureName: string,
  agreed: boolean,
): Promise<PublicContract> {
  return publicCall<PublicContract>(`/api/public/contracts/${encodeURIComponent(token)}/sign`, {
    method: 'POST',
    body: JSON.stringify({ signatureName, agreed }),
  });
}

export function declinePublicContract(token: string, reason?: string): Promise<PublicContract> {
  return publicCall<PublicContract>(`/api/public/contracts/${encodeURIComponent(token)}/decline`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

/**
 * The signer's own copy, as a URL they can open or save.
 *
 * A plain URL rather than a fetched blob: this endpoint takes no credential, the
 * token in the path is the whole authorisation, and letting the browser fetch it
 * directly means the PDF viewer's own print and save controls work normally.
 */
export function publicContractPdfUrl(token: string): string {
  return publicUrl(`/api/public/contracts/${encodeURIComponent(token)}/pdf`);
}
