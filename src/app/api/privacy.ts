// A person's data rights and the workspace's privacy settings: finding someone across the
// workspace, downloading everything held about them, erasing them, the retention periods and
// their runs, and the facts the public privacy page prints.
//
// All of it is the server's: an export is built, an erasure done and a retention run made
// by the server, which also writes them to the audit trail. Demo data has none of it, so
// the page says so rather than pretend.

import { authFetch, authFetchJson, ensureOk, getApiBaseUrl, servesDemoData, type RequestOptions } from './apiClient';
import { downloadFile } from '@/app/lib/exportCsv';

export const DEMO_MESSAGE = 'Privacy requests are handled by the live app; demo data has nobody to export or erase.';

// ── finding a person ────────────────────────────────────────────────────────

export type PersonRecordKind = 'lead' | 'contact' | 'member' | 'application';

/** Where to start: an email address, or one of the person's records. */
export interface PersonLookup {
  email?: string;
  leadId?: string;
  contactId?: string;
  memberId?: string;
}

/** A record that looks like the person, with why it was found. */
export interface PersonRecord {
  kind: PersonRecordKind;
  id: string;
  name: string;
  email?: string | null;
  detail?: string | null;
  foundBy: string;
}

export interface PersonFound {
  addresses: string[];
  records: PersonRecord[];
}

/** The records and addresses staff confirmed are one person. */
export interface PersonSelection {
  leadIds: string[];
  contactIds: string[];
  memberIds: string[];
  applicationIds: string[];
  addresses: string[];
}

export function findPerson(lookup: PersonLookup, options: RequestOptions = {}): Promise<PersonFound> {
  return authFetchJson<PersonFound>('/api/privacy/find', { method: 'POST', body: JSON.stringify(lookup), signal: options.signal });
}

/** The selection for these records and addresses. */
export function selectionOf(records: PersonRecord[], addresses: string[]): PersonSelection {
  const ids = (kind: PersonRecordKind) => records.filter((r) => r.kind === kind).map((r) => r.id);
  return { leadIds: ids('lead'), contactIds: ids('contact'), memberIds: ids('member'), applicationIds: ids('application'), addresses };
}

// ── export ──────────────────────────────────────────────────────────────────

/** Downloads everything held about the selection as a zip; the server writes it to the audit trail. */
export async function exportPerson(selection: PersonSelection): Promise<void> {
  const res = await authFetch('/api/privacy/export', { method: 'POST', body: JSON.stringify(selection) });
  await ensureOk(res);
  const header = res.headers.get('content-disposition') ?? '';
  const name = /filename="?([^";]+)"?/i.exec(header)?.[1] ?? 'personal-data.zip';
  downloadFile(await res.blob(), name);
}

// ── erasure ─────────────────────────────────────────────────────────────────

export type ErasureAction = 'delete' | 'anonymise' | 'cancel' | 'keep';

export interface ErasureLine {
  area: string;
  action: ErasureAction;
  count: number;
  note?: string | null;
}

export interface ErasurePlan {
  records: number;
  lines: ErasureLine[];
  warnings: string[];
  addressesSuppressed: string[];
  addressesShared: string[];
}

export interface ErasureResult {
  number: number;
  pseudonym: string;
  erasedAtUtc: string;
  lines: ErasureLine[];
}

export interface PersonErasure {
  number: number;
  trigger: 'request' | 'retention';
  erasedAtUtc: string;
  erasedByName?: string | null;
  reference?: string | null;
  summary: Record<string, number>;
}

export function previewErasure(selection: PersonSelection, options: RequestOptions = {}): Promise<ErasurePlan> {
  return authFetchJson<ErasurePlan>('/api/privacy/erasure/preview', { method: 'POST', body: JSON.stringify(selection), signal: options.signal });
}

/** Erases the selection. `confirm` must be "ERASE"; `reference` is staff's own, never the person's name. */
export function erasePerson(selection: PersonSelection, confirm: string, reference?: string): Promise<ErasureResult> {
  return authFetchJson<ErasureResult>('/api/privacy/erasure', {
    method: 'POST',
    body: JSON.stringify({ ...selection, confirm, reference: reference?.trim() || undefined }),
  });
}

export function getErasures(options: RequestOptions = {}): Promise<PersonErasure[]> {
  return authFetchJson<PersonErasure[]>('/api/privacy/erasures', { signal: options.signal });
}

// ── settings ────────────────────────────────────────────────────────────────

export type MarketingConsentMode = 'off' | 'optional' | 'required';

export interface PrivacySettings {
  controllerName?: string | null;
  controllerAddress?: string | null;
  controllerEmail?: string | null;
  dpoContact?: string | null;
  marketingConsent: MarketingConsentMode;
  marketingConsentText: string;
  marketingConsentTextIsDefault: boolean;
  leadRetentionMonths?: number | null;
  memberRetentionYears?: number | null;
  mailRetentionMonths?: number | null;
  detailRetentionMonths?: number | null;
  version?: string | null;
  updatedAtUtc?: string | null;
  updatedByName?: string | null;
}

/** Every field, as the page shows them: an empty text or period clears it. */
export interface PrivacySettingsInput {
  controllerName: string | null;
  controllerAddress: string | null;
  controllerEmail: string | null;
  dpoContact: string | null;
  marketingConsent: MarketingConsentMode;
  marketingConsentText: string | null;
  leadRetentionMonths: number | null;
  memberRetentionYears: number | null;
  mailRetentionMonths: number | null;
  detailRetentionMonths: number | null;
}

export function getPrivacySettings(options: RequestOptions = {}): Promise<PrivacySettings> {
  return authFetchJson<PrivacySettings>('/api/privacy/settings', { signal: options.signal });
}

export function savePrivacySettings(input: PrivacySettingsInput, version?: string | null): Promise<PrivacySettings> {
  return authFetchJson<PrivacySettings>('/api/privacy/settings', {
    method: 'PUT',
    body: JSON.stringify(input),
    headers: version ? { 'If-Match': `"${version}"` } : undefined,
  });
}

// ── retention ───────────────────────────────────────────────────────────────

export interface RetentionRule {
  rule: 'leads' | 'members' | 'mail' | 'details';
  period?: number | null;
  unit: 'months' | 'years';
  count: number;
  what: string;
}

export interface RetentionPreview {
  anythingOn: boolean;
  rules: RetentionRule[];
  kept: string[];
}

export interface RetentionRun {
  id: string;
  trigger: 'schedule' | 'manual';
  scheduledFor?: string | null;
  status: 'running' | 'succeeded' | 'partly_failed' | 'failed';
  startedAtUtc: string;
  finishedAtUtc?: string | null;
  startedByName?: string | null;
  leadsDeleted: number;
  membersAnonymised: number;
  messagesPurged: number;
  detailsPurged: number;
  failures?: string | null;
}

export function getRetentionPreview(options: RequestOptions = {}): Promise<RetentionPreview> {
  return authFetchJson<RetentionPreview>('/api/privacy/retention/preview', { signal: options.signal });
}

export function runRetention(): Promise<RetentionRun> {
  return authFetchJson<RetentionRun>('/api/privacy/retention/run', { method: 'POST' });
}

export function getRetentionRuns(options: RequestOptions = {}): Promise<RetentionRun[]> {
  return authFetchJson<RetentionRun[]>('/api/privacy/retention/runs', { signal: options.signal });
}

// ── the public privacy page ─────────────────────────────────────────────────

export interface PrivacyParty {
  name?: string | null;
  address?: string | null;
  email?: string | null;
  dpoContact?: string | null;
}

export interface PrivacyProcessor {
  name: string;
  purpose: string;
  location: string;
  transfer?: string | null;
}

export interface PrivacyNotice {
  operator: PrivacyParty;
  controller: PrivacyParty;
  workspaceName?: string | null;
  hosting: string;
  processors: PrivacyProcessor[];
  retention?: { leadMonths?: number | null; memberYears?: number | null; mailMonths?: number | null; detailMonths?: number | null } | null;
  marketingConsentText?: string | null;
  sessionHours: number;
}

/**
 * What the privacy page prints, from the server. Anonymous: the page is public. Null when
 * there is no server to ask (the demo, or a build with no API): the page then says so.
 */
export async function getPrivacyNotice(formToken?: string | null, options: RequestOptions = {}): Promise<PrivacyNotice | null> {
  const base = getApiBaseUrl();
  if (servesDemoData() || !base) return null;
  const query = formToken ? `?form=${encodeURIComponent(formToken)}` : '';
  const res = await fetch(`${base}/api/public/privacy${query}`, { signal: options.signal });
  if (!res.ok) throw new Error(`The privacy details could not be loaded (${res.status}).`);
  return (await res.json()) as PrivacyNotice;
}
