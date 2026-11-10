// Duplicate detection and merging, for every kind of record that can be entered twice.
//
// Contacts and companies are found by the server (GET /api/duplicates/contacts and
// /companies), with the rules below; the browser runs the same rules in demo mode and
// for the other kinds. Two records are the same when they share an identifier, or share
// a name and nothing known about them disagrees. A contact's email is unique in its
// workspace, so for contacts a different email rules nothing out: the same person
// entered twice has two addresses. Leads keep the strict rule, as their emails are not
// unique. "Not the same" is the workspace's decision, kept on the server.
//
// Merging goes to the server, which moves everything attached to the duplicates
// onto the record kept (see the merge endpoints). In demo mode it happens here,
// with the same rules: the kept record's values stay, its empty fields are filled
// from the others, and whatever pointed at a duplicate points at it instead.

import { authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import { relinkMockRecords } from './mockStore';
import { getContacts, updateContact, deleteContact, mapContact, type ApiContact } from './contacts';
import { getCompanies, updateCompany, deleteCompany, mapCompany, type CompanyRaw } from './companies';
import { getLeads, updateLead, deleteLead } from './leads';
import { getDeals, updateDeal, deleteDeal } from './deals';
import { getMembers, updateMember, deleteMember, type Member } from './members';
import type { Household } from './households';
import type { Contact, Company, Lead, Deal } from './types';
import { amountOf, currencyOf, formatMoney } from '@/app/lib/money';

export interface DuplicateGroup<T> {
  /** Stable for the same set of records, whatever order they load in. */
  key: string;
  /** Why they are considered the same, for the person deciding: "Same email". */
  reason: string;
  records: T[];
}

// ── Normalising ──────────────────────────────────────────────────────────────

const norm = (s?: string | null) => (s ?? '').trim().toLowerCase();

/** Lower case, single spaces, no accents: "  Zoë  Martin" and "zoe martin" are one name. */
export function normalizeName(s?: string | null): string {
  return norm(s).normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\s+/g, ' ');
}

/** The last nine digits, so "+41 79 123 45 67" and "079 123 45 67" match. Short numbers say nothing. */
export function normalizePhone(s?: string | null): string {
  const digits = (s ?? '').replace(/\D/g, '');
  return digits.length >= 7 ? digits.slice(-9) : '';
}

/** "https://www.Acme.ch/about" → "acme.ch". */
export function normalizeDomain(s?: string | null): string {
  return norm(s).replace(/^[a-z]+:\/\//, '').replace(/^www\./, '').replace(/[/?#].*$/, '').replace(/:\d+$/, '');
}

/** Both known and different. A value one record lacks never rules a match out. */
const conflicts = (a: string, b: string) => a !== '' && b !== '' && a !== b;

// ── Grouping ─────────────────────────────────────────────────────────────────

export interface MatchRule<T> {
  /** Shown to the person: "Same email". */
  reason: string;
  /** What records must share. Empty says nothing about the record. */
  key: (record: T) => string;
  /** When sharing the key is not enough on its own, what else must hold for a pair. */
  same?: (a: T, b: T) => boolean;
}

/**
 * Records that any rule links, directly or through one another, form a group. A
 * record is in at most one group. With `maxShared`, a value more records share than
 * that links none of them, as on the server (DuplicateRules.MaxShared).
 */
export function groupDuplicates<T extends { id: string }>(records: T[], rules: MatchRule<T>[], maxShared = Infinity): DuplicateGroup<T>[] {
  const parent = new Map<string, string>();
  const find = (id: string): string => {
    let root = id;
    while (parent.get(root) !== root) root = parent.get(root)!;
    // Flatten the path so later lookups are quick.
    let at = id;
    while (parent.get(at) !== root) {
      const next = parent.get(at)!;
      parent.set(at, root);
      at = next;
    }
    return root;
  };
  const reasons = new Map<string, Set<string>>();
  const union = (a: string, b: string, reason: string) => {
    const ra = find(a);
    const rb = find(b);
    const merged = new Set([...(reasons.get(ra) ?? []), ...(reasons.get(rb) ?? []), reason]);
    if (ra !== rb) parent.set(rb, ra);
    reasons.set(ra, merged);
    reasons.delete(ra === rb ? '' : rb);
  };
  const unique = records.filter((r, i) => records.findIndex((o) => o.id === r.id) === i);
  for (const r of unique) parent.set(r.id, r.id);

  for (const rule of rules) {
    const buckets = new Map<string, T[]>();
    for (const r of unique) {
      const k = rule.key(r);
      if (!k) continue;
      const list = buckets.get(k);
      if (list) list.push(r);
      else buckets.set(k, [r]);
    }
    for (const list of buckets.values()) {
      if (list.length < 2 || list.length > maxShared) continue;
      list.forEach((a, i) => {
        for (const b of list.slice(i + 1)) {
          if (!rule.same || rule.same(a, b)) union(a.id, b.id, rule.reason);
        }
      });
    }
  }

  const members = new Map<string, T[]>();
  for (const r of unique) {
    const root = find(r.id);
    const list = members.get(root);
    if (list) list.push(r);
    else members.set(root, [r]);
  }
  const ruleOrder = rules.map((r) => r.reason);
  return [...members.entries()]
    .filter(([, list]) => list.length > 1)
    .map(([root, list]) => ({
      key: list.map((r) => r.id).sort().join('|'),
      reason: [...(reasons.get(root) ?? [])].sort((a, b) => ruleOrder.indexOf(a) - ruleOrder.indexOf(b)).join(' · '),
      records: list,
    }))
    .sort((a, b) => b.records.length - a.records.length || a.key.localeCompare(b.key));
}

type Person = { id: string; name?: string | null; email?: string | null; phone?: string | null; companyId?: string | null; companyName?: string | null };

const company = (p: Person) => p.companyId ?? normalizeName(p.companyName);

/** People are the same with the same email, or the same name and nothing that disagrees. */
function personRules<T extends Person>(): MatchRule<T>[] {
  return [
    { reason: 'Same email', key: (p) => norm(p.email) },
    {
      reason: 'Same name',
      key: (p) => normalizeName(p.name),
      same: (a, b) =>
        !conflicts(norm(a.email), norm(b.email))
        && !conflicts(normalizePhone(a.phone), normalizePhone(b.phone))
        && !conflicts(company(a), company(b)),
    },
  ];
}

/**
 * More contacts or companies than this sharing one phone, name or domain (an office
 * switchboard, a common name) says nothing about any two of them. The server's own
 * limit (DuplicateRules.MaxShared), so demo mode and the assistant offer what the
 * Duplicates page does.
 */
const MAX_SHARED = 50;

/** A name as the server matches it (MatchKeys.Name): one letter says nothing. */
const nameKey = (s?: string | null) => {
  const name = normalizeName(s);
  return name.length > 1 ? name : '';
};

/**
 * Contacts, as the server finds them (DuplicateRules): the same email, the same phone
 * however it is written, or the same name when neither phone nor company disagrees.
 */
function contactRules<T extends Person>(): MatchRule<T>[] {
  return [
    { reason: 'Same email', key: (p) => norm(p.email) },
    { reason: 'Same phone', key: (p) => normalizePhone(p.phone) },
    {
      reason: 'Same name',
      key: (p) => nameKey(p.name),
      same: (a, b) => !conflicts(normalizePhone(a.phone), normalizePhone(b.phone)) && !conflicts(company(a), company(b)),
    },
  ];
}

export function duplicateContactGroups<T extends Person>(contacts: T[]): DuplicateGroup<T>[] {
  return groupDuplicates(contacts, contactRules<T>(), MAX_SHARED);
}

export function duplicateLeadGroups<T extends Person>(leads: T[]): DuplicateGroup<T>[] {
  return groupDuplicates(leads, personRules<T>());
}

type CompanyLike = { id: string; name?: string | null; domain?: string | null; website?: string | null };
const domainOf = (c: CompanyLike) => normalizeDomain(c.domain || c.website);

export function duplicateCompanyGroups<T extends CompanyLike>(companies: T[]): DuplicateGroup<T>[] {
  return groupDuplicates(companies, [
    { reason: 'Same domain', key: domainOf },
    { reason: 'Same name', key: (c) => nameKey(c.name), same: (a, b) => !conflicts(domainOf(a), domainOf(b)) },
  ], MAX_SHARED);
}

type DealLike = { id: string; name?: string | null; companyId?: string | null; contactId?: string | null; isWon?: boolean | null };

/** Deals are the same with the same name, for the same company, and the same outcome. */
export function duplicateDealGroups<T extends DealLike>(deals: T[]): DuplicateGroup<T>[] {
  return groupDuplicates(deals, [
    {
      reason: 'Same name and company',
      key: (d) => normalizeName(d.name),
      same: (a, b) =>
        (a.companyId ?? '') === (b.companyId ?? '')
        && !conflicts(a.contactId ?? '', b.contactId ?? '')
        && (a.isWon ?? null) === (b.isWon ?? null),
    },
  ]);
}

type MemberLike = { id: string; firstName: string; lastName: string; email?: string | null; phone?: string | null };

export function duplicateMemberGroups<T extends MemberLike>(members: T[]): DuplicateGroup<T>[] {
  return groupDuplicates(members, [
    { reason: 'Same email', key: (m) => norm(m.email) },
    {
      reason: 'Same name',
      key: (m) => normalizeName(`${m.firstName} ${m.lastName}`),
      same: (a, b) => !conflicts(norm(a.email), norm(b.email)) && !conflicts(normalizePhone(a.phone), normalizePhone(b.phone)),
    },
  ]);
}

/** A household with the names of the people in it, as the merge preview shows it. */
export interface HouseholdForMerge extends Household {
  memberNames: string[];
}

export function duplicateHouseholdGroups<T extends { id: string; name?: string | null; primaryMemberId?: string | null }>(households: T[]): DuplicateGroup<T>[] {
  return groupDuplicates(households, [
    { reason: 'Same primary member', key: (h) => h.primaryMemberId ?? '' },
    { reason: 'Same name', key: (h) => normalizeName(h.name) },
  ]);
}

export async function findDuplicateContacts(): Promise<DuplicateGroup<Contact>[]> {
  if (!servesDemoData()) return (await scanContactDuplicates()).groups;
  return duplicateContactGroups(await getContacts());
}

export async function findDuplicateCompanies(): Promise<DuplicateGroup<Company>[]> {
  if (!servesDemoData()) return (await scanCompanyDuplicates()).groups;
  return duplicateCompanyGroups(await getCompanies());
}

// ── Found on the server, decided for the team ───────────────────────────────

/** A group someone said is not the same records, by its records' ids, so it can be offered again. */
export interface NotSameGroup {
  key: string;
  recordIds: string[];
}

/** A kind's duplicates, as the server found them, and the groups hidden by "not the same". */
export interface DuplicateScan<T> {
  groups: DuplicateGroup<T>[];
  notSame: NotSameGroup[];
}

interface ApiScan<R> {
  groups?: { key: string; reason: string; records?: R[] }[];
  notSame?: { key: string; recordIds?: string[] }[];
}

function scanOf<R, T>(raw: ApiScan<R> | undefined, map: (record: R) => T): DuplicateScan<T> {
  return {
    groups: (raw?.groups ?? []).map((g) => ({ key: g.key, reason: g.reason, records: (g.records ?? []).map(map) })),
    notSame: (raw?.notSame ?? []).map((g) => ({ key: g.key, recordIds: g.recordIds ?? g.key.split('|') })),
  };
}

/** The workspace's duplicate contacts, found by the server over every contact. */
export async function scanContactDuplicates(): Promise<DuplicateScan<Contact>> {
  return scanOf(await authFetchJson<ApiScan<ApiContact>>('/api/duplicates/contacts'), mapContact);
}

/** The workspace's duplicate companies, found by the server. */
export async function scanCompanyDuplicates(): Promise<DuplicateScan<Company>> {
  return scanOf(await authFetchJson<ApiScan<CompanyRaw>>('/api/duplicates/companies'), mapCompany);
}

/** Two record ids, written one way whichever comes first. */
export const pairKey = (a: string, b: string) => (a < b ? `${a}|${b}` : `${b}|${a}`);

/** Every pair among these records. */
export function pairsOf(ids: string[]): string[] {
  const pairs: string[] = [];
  ids.forEach((a, i) => ids.slice(i + 1).forEach((b) => pairs.push(pairKey(a, b))));
  return pairs;
}

/** The pairs of this kind the workspace said are not the same. */
export async function getNotSamePairs(kind: MergeKind, req?: RequestOptions): Promise<string[]> {
  const pairs = await authFetchJson<{ recordIdA: string; recordIdB: string }[]>(`/api/duplicates/${kind}/not-same`, req);
  return Array.isArray(pairs) ? pairs.map((p) => pairKey(p.recordIdA, p.recordIdB)) : [];
}

/** "These are not the same": never offered again, to anyone in the workspace. */
export async function markNotSame(kind: MergeKind, recordIds: string[]): Promise<void> {
  const res = await authFetch(`/api/duplicates/${kind}/not-same`, { method: 'POST', body: JSON.stringify({ recordIds }) });
  await ensureOk(res);
}

/** Offer these records as possible duplicates again. */
export async function clearNotSame(kind: MergeKind, recordIds: string[]): Promise<void> {
  const res = await authFetch(`/api/duplicates/${kind}/not-same/clear`, { method: 'POST', body: JSON.stringify({ recordIds }) });
  await ensureOk(res);
}

// ── What the merged record looks like ───────────────────────────────────────

/**
 * How a field of the kept record combines with the duplicates'. These mirror the
 * server's merge, so the preview is what the person gets.
 */
export type MergeRule =
  /** The kept record's value, whatever the others say. */
  | 'keep'
  /** The kept record's value; if it has none, the first duplicate's that does. */
  | 'fill'
  /** As fill, with zero counting as none: an amount of 0 was never entered. */
  | 'fillAmount'
  | 'earliest'
  | 'latest'
  | 'sum'
  /** True if any record says so: "do not contact", "private". */
  | 'any'
  /** Every distinct text, one after another. */
  | 'append'
  /** Every distinct item of a list. */
  | 'union';

export interface MergeField<T> {
  label: string;
  /** The properties this row stands for. They move together; the first decides. */
  keys: [keyof T & string, ...(keyof T & string)[]];
  rule: MergeRule;
  /** How the value reads. Defaults to the first property as text. */
  format?: (record: T) => string | undefined;
}

export interface MergedRow {
  label: string;
  rule: MergeRule;
  /** What each record has, in the order given: the kept record first. */
  values: (string | undefined)[];
  result: string | undefined;
  /** Which record the result came from, or -1 when it was worked out from several. */
  from: number;
  /** Whether merging changes the kept record's value. */
  changed: boolean;
}

const isBlank = (v: unknown, amount = false): boolean => {
  if (v === undefined || v === null) return true;
  if (typeof v === 'string') {
    const t = v.trim();
    return t === '' || (amount && Number.isFinite(Number(t)) && Number(t) === 0);
  }
  if (typeof v === 'number') return amount && v === 0;
  if (Array.isArray(v)) return v.length === 0;
  return false;
};

const time = (v: unknown) => (typeof v === 'string' && v ? Date.parse(v) : NaN);

function display<T>(field: MergeField<T>, record: T): string | undefined {
  if (field.format) return field.format(record);
  const v = record[field.keys[0]];
  if (isBlank(v)) return undefined;
  if (typeof v === 'boolean') return v ? 'Yes' : 'No';
  if (Array.isArray(v)) return v.join(', ');
  return String(v);
}

/** The kept record with the duplicates folded in, one after another, and a row per field. */
export function mergeRecords<T extends object>(fields: MergeField<T>[], keep: T, others: T[]): { merged: T; rows: MergedRow[] } {
  const merged = { ...keep } as T;
  const rows: MergedRow[] = [];
  const all = [keep, ...others];
  for (const field of fields) {
    const [first] = field.keys;
    const before = display(field, keep);
    let from = 0;
    for (const [i, other] of others.entries()) {
      const ours = merged[first] as unknown;
      const theirs = other[first] as unknown;
      switch (field.rule) {
        case 'keep':
          break;
        case 'fill':
        case 'fillAmount': {
          const amount = field.rule === 'fillAmount';
          if (isBlank(ours, amount) && !isBlank(theirs, amount)) {
            for (const k of field.keys) if (!isBlank(other[k])) merged[k] = other[k];
            from = i + 1;
          }
          break;
        }
        case 'earliest':
        case 'latest': {
          const a = time(ours);
          const b = time(theirs);
          const later = field.rule === 'latest';
          if (Number.isNaN(b)) break;
          if (Number.isNaN(a) || (later ? b > a : b < a)) {
            merged[first] = other[first];
            from = i + 1;
          }
          break;
        }
        case 'sum':
          if (typeof theirs === 'number' && theirs !== 0) {
            merged[first] = ((Number(ours) || 0) + theirs) as T[typeof first];
            from = -1;
          }
          break;
        case 'any':
          if (theirs === true && ours !== true) {
            merged[first] = true as T[typeof first];
            from = i + 1;
          }
          break;
        case 'union': {
          const ours_ = Array.isArray(ours) ? (ours as unknown[]) : [];
          const extra = Array.isArray(theirs) ? (theirs as unknown[]).filter((v) => !ours_.includes(v)) : [];
          if (extra.length) {
            merged[first] = [...ours_, ...extra] as T[typeof first];
            from = -1;
          }
          break;
        }
        case 'append': {
          const text = typeof theirs === 'string' ? theirs.trim() : '';
          const current = typeof ours === 'string' ? ours : '';
          if (text && !current.includes(text)) {
            merged[first] = (current.trim() ? `${current.trimEnd()}\n\n${text}` : text) as T[typeof first];
            from = current.trim() ? -1 : i + 1;
          }
          break;
        }
      }
    }
    const result = display(field, merged);
    rows.push({ label: field.label, rule: field.rule, values: all.map((r) => display(field, r)), result, from, changed: result !== before });
  }
  return { merged, rows };
}

/** The record to keep by default: the one with the most filled in, then the oldest. */
export function suggestKeep<T extends { id: string }>(fields: MergeField<T>[], records: T[], created?: (record: T) => string | undefined): string {
  const score = (r: T) => fields.reduce((n, f) => n + (isBlank(r[f.keys[0]]) ? 0 : 1), 0);
  const born = (r: T) => time(created?.(r)) || Number.MAX_SAFE_INTEGER;
  return [...records].sort((a, b) => score(b) - score(a) || born(a) - born(b))[0]?.id ?? '';
}

/** What the merge changes on the kept record, as a patch. */
function patchOf<T extends object>(fields: MergeField<T>[], keep: T, merged: T): Partial<T> {
  const patch: Partial<T> = {};
  for (const field of fields) {
    for (const k of field.keys) {
      if (merged[k] !== keep[k]) patch[k] = merged[k];
    }
  }
  return patch;
}

// ── The fields each kind of record merges ───────────────────────────────────

const date = (v?: string) => {
  if (!v) return undefined;
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? undefined : d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
};
const yes = (v?: boolean) => (v ? 'Yes' : undefined);

export const CONTACT_MERGE_FIELDS: MergeField<Contact>[] = [
  { label: 'Name', keys: ['name'], rule: 'keep' },
  { label: 'Email', keys: ['email'], rule: 'keep' },
  { label: 'Phone', keys: ['phone'], rule: 'fill' },
  { label: 'Job title', keys: ['jobTitle'], rule: 'fill' },
  { label: 'Company', keys: ['companyId', 'companyName'], rule: 'fill', format: (c) => c.companyName || (c.companyId ? 'Linked company' : undefined) },
  { label: 'Prefers', keys: ['preferredContactMethod'], rule: 'fill' },
  { label: 'Description', keys: ['description'], rule: 'fill' },
  { label: 'Do not contact', keys: ['doNotContact'], rule: 'any', format: (c) => yes(c.doNotContact) },
  { label: 'Added', keys: ['createdAtUtc'], rule: 'earliest', format: (c) => date(c.createdAtUtc) },
];

export const COMPANY_MERGE_FIELDS: MergeField<Company>[] = [
  { label: 'Name', keys: ['name'], rule: 'keep' },
  { label: 'Domain', keys: ['domain'], rule: 'fill' },
  { label: 'Website', keys: ['website'], rule: 'fill' },
  { label: 'Industry', keys: ['industry'], rule: 'fill' },
  { label: 'Size', keys: ['size'], rule: 'fill' },
  { label: 'Location', keys: ['location'], rule: 'fill' },
  { label: 'Description', keys: ['description'], rule: 'fill' },
];

export const LEAD_MERGE_FIELDS: MergeField<Lead>[] = [
  { label: 'Name', keys: ['name'], rule: 'keep' },
  { label: 'Email', keys: ['email'], rule: 'keep' },
  { label: 'Status', keys: ['status'], rule: 'keep' },
  { label: 'Phone', keys: ['phone'], rule: 'fill' },
  { label: 'Company', keys: ['companyId', 'companyName'], rule: 'fill', format: (l) => l.companyName || (l.companyId ? 'Linked company' : undefined) },
  { label: 'Job title', keys: ['jobTitle'], rule: 'fill' },
  { label: 'Source', keys: ['source', 'leadSourceId'], rule: 'fill' },
  { label: 'Owner', keys: ['assignedToId', 'assignedToName'], rule: 'fill', format: (l) => l.assignedToName || (l.assignedToId ? 'Assigned' : undefined) },
  { label: 'Referred by', keys: ['referredByContactId', 'referredByContactName'], rule: 'fill', format: (l) => l.referredByContactName || (l.referredByContactId ? 'A contact' : undefined) },
  { label: 'Country', keys: ['country'], rule: 'fill' },
  { label: 'Industry', keys: ['industry'], rule: 'fill' },
  { label: 'Company size', keys: ['companySize'], rule: 'fill' },
  { label: 'Score', keys: ['leadScore', 'scoreIsManual'], rule: 'fill', format: (l) => (l.leadScore == null ? undefined : String(l.leadScore)) },
  { label: 'Description', keys: ['description'], rule: 'fill' },
  { label: 'Emails opened', keys: ['emailOpenCount'], rule: 'sum', format: (l) => (l.emailOpenCount ? String(l.emailOpenCount) : undefined) },
  { label: 'Last contacted', keys: ['lastContactedAt'], rule: 'latest', format: (l) => date(l.lastContactedAt) },
  { label: 'First seen', keys: ['createdAtUtc'], rule: 'earliest', format: (l) => date(l.createdAtUtc) },
];

// The stored amount in the deal's own currency; the text was read again with Number(),
// which made "CHF 85,500" unreadable and showed it as typed.
const money = (d: Deal) => {
  const amount = amountOf(d);
  return amount === null || amount === 0 ? undefined : formatMoney(amount, currencyOf(d));
};
const outcome = (d: Deal) => (d.isWon === true ? 'Won' : d.isWon === false ? 'Lost' : 'Open');

export const DEAL_MERGE_FIELDS: MergeField<Deal>[] = [
  { label: 'Name', keys: ['name'], rule: 'keep' },
  { label: 'Stage', keys: ['dealStageId', 'dealStageName', 'stage', 'pipelineId', 'pipelineName'], rule: 'keep', format: (d) => d.dealStageName || d.stage },
  { label: 'Outcome', keys: ['isWon'], rule: 'keep', format: outcome },
  { label: 'Value', keys: ['amount', 'value', 'currency'], rule: 'fillAmount', format: money },
  { label: 'Company', keys: ['companyId', 'companyName'], rule: 'fill', format: (d) => d.companyName || (d.companyId ? 'Linked company' : undefined) },
  { label: 'Contact', keys: ['contactId', 'contactName'], rule: 'fill', format: (d) => d.contactName || (d.contactId ? 'Linked contact' : undefined) },
  { label: 'Owner', keys: ['assigneeId', 'assigneeName'], rule: 'fill', format: (d) => d.assigneeName || (d.assigneeId ? 'Assigned' : undefined) },
  { label: 'Expected close', keys: ['expectedCloseDateUtc'], rule: 'fill', format: (d) => date(d.expectedCloseDateUtc) },
  { label: 'Probability', keys: ['probability'], rule: 'fill', format: (d) => (d.probability == null ? undefined : `${d.probability}%`) },
  { label: 'Description', keys: ['description'], rule: 'fill' },
  { label: 'Private', keys: ['isPrivate'], rule: 'any', format: (d) => yes(d.isPrivate) },
  { label: 'Created', keys: ['createdAtUtc'], rule: 'earliest', format: (d) => date(d.createdAtUtc) },
];

export const MEMBER_MERGE_FIELDS: MergeField<Member>[] = [
  { label: 'Name', keys: ['firstName', 'lastName'], rule: 'keep', format: (m) => `${m.firstName} ${m.lastName}`.trim() || undefined },
  { label: 'Email', keys: ['email'], rule: 'keep' },
  { label: 'Tier', keys: ['tier'], rule: 'keep' },
  { label: 'Status', keys: ['status'], rule: 'keep' },
  { label: 'Dues', keys: ['duesAmount', 'duesFrequency'], rule: 'keep', format: (m) => `${m.duesAmount.toLocaleString()} / ${m.duesFrequency.toLowerCase()}` },
  { label: 'Phone', keys: ['phone'], rule: 'fill' },
  { label: 'House account', keys: ['houseAccountBalance'], rule: 'sum', format: (m) => m.houseAccountBalance.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) },
  { label: 'Member since', keys: ['joinedAtUtc'], rule: 'earliest', format: (m) => date(m.joinedAtUtc) },
  { label: 'Renews', keys: ['renewsAtUtc'], rule: 'latest', format: (m) => date(m.renewsAtUtc) },
  { label: 'Notes', keys: ['notes'], rule: 'append' },
];

export const HOUSEHOLD_MERGE_FIELDS: MergeField<HouseholdForMerge>[] = [
  { label: 'Name', keys: ['name'], rule: 'keep' },
  { label: 'Primary member', keys: ['primaryMemberId', 'primaryName'], rule: 'keep', format: (h) => h.primaryName },
  { label: 'People', keys: ['memberNames'], rule: 'union' },
  { label: 'Notes', keys: ['notes'], rule: 'append' },
  { label: 'Created', keys: ['createdAtUtc'], rule: 'keep', format: (h) => date(h.createdAtUtc) },
];

// ── Merging ─────────────────────────────────────────────────────────────────

export type MergeKind = 'contacts' | 'companies' | 'leads' | 'deals' | 'members' | 'households';

export interface MergeOutcome {
  /** How many duplicates were folded in. */
  merged: number;
  /** Why the rest were not, in the server's words. */
  error?: string;
}

const ROUTES: Record<Exclude<MergeKind, 'households'>, { path: string; field: string }> = {
  contacts: { path: '/api/contacts', field: 'otherContactId' },
  companies: { path: '/api/companies', field: 'otherCompanyId' },
  leads: { path: '/api/leads', field: 'otherLeadId' },
  deals: { path: '/api/deals', field: 'otherDealId' },
  members: { path: '/api/members', field: 'otherMemberId' },
};

/** One at a time, in order, so a refusal part way through says exactly what was merged. */
async function mergeOnServer(kind: Exclude<MergeKind, 'households'>, primaryId: string, duplicateIds: string[]): Promise<MergeOutcome> {
  const { path, field } = ROUTES[kind];
  let merged = 0;
  for (const otherId of duplicateIds) {
    try {
      const res = await authFetch(`${path}/${primaryId}/merge`, { method: 'POST', body: JSON.stringify({ [field]: otherId }) });
      await ensureOk(res);
      merged++;
    } catch (err) {
      return { merged, error: err instanceof Error ? err.message : 'The merge failed.' };
    }
  }
  return { merged };
}

/** The same merge in this browser: fill the kept record, repoint links, delete the rest. */
async function mergeLocally<T extends { id: string }>(
  records: T[],
  fields: MergeField<T>[],
  primaryId: string,
  duplicateIds: string[],
  save: (id: string, patch: Partial<T>) => Promise<unknown>,
  remove: (id: string) => Promise<unknown>,
  links: string[],
): Promise<MergeOutcome> {
  const keep = records.find((r) => r.id === primaryId);
  const others = duplicateIds.map((id) => records.find((r) => r.id === id)).filter((r): r is T => !!r && r.id !== primaryId);
  if (!keep) return { merged: 0, error: 'The record to keep is no longer there.' };
  const { merged } = mergeRecords(fields, keep, others);
  const patch = patchOf(fields, keep, merged);
  if (Object.keys(patch).length) await save(primaryId, patch);
  for (const other of others) {
    for (const link of links) relinkMockRecords(link, other.id, primaryId);
    await remove(other.id);
  }
  return { merged: others.length };
}

/** Club records name their member by id; make sure their stores are loaded before relinking. */
async function loadClubStores(): Promise<void> {
  await Promise.all([
    import('./charges'), import('./visits'), import('./loyalty'), import('./donations'), import('./giftCards'),
    import('./subscriptions'), import('./reservations'), import('./households'), import('./applications'),
  ]);
}

/**
 * Households merge on the server in one save: the people move to the household kept
 * (each once; a duplicate's primary joins as "Other"), the notes are kept together,
 * and the duplicates go. It used to be a request per person from this browser, so a
 * merge cut off half way left people in two households.
 */
async function mergeHouseholdsInto(primaryId: string, duplicateIds: string[]): Promise<MergeOutcome> {
  const { mergeHouseholds } = await import('./households');
  try {
    await mergeHouseholds(primaryId, duplicateIds);
    return { merged: duplicateIds.length };
  } catch (err) {
    return { merged: 0, error: err instanceof Error ? err.message : 'The merge failed.' };
  }
}

/** Fold the duplicates into the record kept. */
export async function mergeDuplicates(kind: MergeKind, primaryId: string, duplicateIds: string[]): Promise<MergeOutcome> {
  const ids = duplicateIds.filter((id) => id && id !== primaryId);
  if (!ids.length) return { merged: 0 };
  if (kind === 'households') return mergeHouseholdsInto(primaryId, ids);
  if (!servesDemoData()) return mergeOnServer(kind, primaryId, ids);
  switch (kind) {
    case 'contacts':
      return mergeLocally(await getContacts(), CONTACT_MERGE_FIELDS, primaryId, ids, updateContact as (id: string, p: Partial<Contact>) => Promise<unknown>, deleteContact, ['contactId', 'referredByContactId', 'convertedToContactId']);
    case 'companies':
      return mergeLocally(await getCompanies(), COMPANY_MERGE_FIELDS, primaryId, ids, updateCompany as (id: string, p: Partial<Company>) => Promise<unknown>, deleteCompany, ['companyId', 'convertedToCompanyId']);
    case 'leads':
      return mergeLocally(await getLeads(), LEAD_MERGE_FIELDS, primaryId, ids, updateLead as (id: string, p: Partial<Lead>) => Promise<unknown>, deleteLead, ['leadId', 'convertedFromLeadId']);
    case 'deals':
      return mergeLocally(await getDeals(), DEAL_MERGE_FIELDS, primaryId, ids, updateDeal as (id: string, p: Partial<Deal>) => Promise<unknown>, deleteDeal, ['dealId', 'convertedToDealId']);
    case 'members': {
      await loadClubStores();
      const outcome = await mergeLocally(await getMembers(), MEMBER_MERGE_FIELDS, primaryId, ids, updateMember as (id: string, p: Partial<Member>) => Promise<unknown>, deleteMember, ['memberId']);
      const { demoMoveMemberLinks } = await import('./households');
      demoMoveMemberLinks(primaryId, ids);
      return outcome;
    }
  }
}

export async function mergeContacts(primaryId: string, duplicateIds: string[]): Promise<boolean> {
  const outcome = await mergeDuplicates('contacts', primaryId, duplicateIds);
  return !outcome.error;
}

export async function mergeCompanies(primaryId: string, duplicateIds: string[]): Promise<boolean> {
  const outcome = await mergeDuplicates('companies', primaryId, duplicateIds);
  return !outcome.error;
}
