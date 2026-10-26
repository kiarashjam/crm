// What each list page needs to find and merge its duplicates: how to load every
// record, how to tell two apart, which fields the merge combines, and what moves
// to the record kept. One description per kind of record, so every page offers
// the same banner and the same dialog.

import type { ElementType } from 'react';
import { Building2, Crown, Home, Target, UserRound, Users } from 'lucide-react';
import type { HeroTone } from '@/app/components/PageHero';
import { servesDemoData } from '@/app/api/apiClient';
import {
  CONTACT_MERGE_FIELDS, COMPANY_MERGE_FIELDS, DEAL_MERGE_FIELDS, HOUSEHOLD_MERGE_FIELDS, LEAD_MERGE_FIELDS, MEMBER_MERGE_FIELDS,
  duplicateCompanyGroups, duplicateContactGroups, duplicateDealGroups, duplicateHouseholdGroups, duplicateLeadGroups, duplicateMemberGroups,
  type DuplicateGroup, type HouseholdForMerge, type MergeField, type MergeKind,
} from '@/app/api/dedupe';
import { getHouseholdMembers, getHouseholds } from '@/app/api/households';
import { getContacts } from '@/app/api/contacts';
import { getCompanies } from '@/app/api/companies';
import { getLeads } from '@/app/api/leads';
import { getDeals } from '@/app/api/deals';
import { getMembers, type Member } from '@/app/api/members';
import type { Company, Contact, Deal, Lead } from '@/app/api/types';

export interface DuplicateKind<T extends { id: string }> {
  kind: MergeKind;
  /** The record: "contact", "contacts". */
  noun: { one: string; many: string };
  /** What a group of duplicates is: one person, entered twice. */
  groupNoun: { one: string; many: string };
  tone: HeroTone;
  icon: ElementType;
  load: () => Promise<T[]>;
  group: (records: T[]) => DuplicateGroup<T>[];
  fields: MergeField<T>[];
  title: (record: T) => string;
  subtitle: (record: T) => string | undefined;
  /** When the record was made, to say which came first. */
  created: (record: T) => string | undefined;
  href?: (record: T) => string;
  /** Everything that moves to the record kept, read out before the person confirms. */
  moves: string[];
  /** What becomes of the others, given how many there are. */
  fate: (others: number) => string;
}

const removed = (others: number) =>
  others === 1 ? 'The other one is removed once everything has moved.' : 'The others are removed once everything has moved.';

/**
 * The company's name on each record that only has its id, so the preview says
 * "Acme" rather than "a linked company". Demo records and some lists carry the id alone.
 */
async function withCompanyNames<T extends { companyId?: string; companyName?: string }>(records: T[]): Promise<T[]> {
  if (!records.some((r) => r.companyId && !r.companyName)) return records;
  const names = new Map((await getCompanies().catch(() => [])).map((c) => [c.id, c.name]));
  return records.map((r) => (r.companyId && !r.companyName && names.has(r.companyId) ? { ...r, companyName: names.get(r.companyId) } : r));
}

export const CONTACT_DUPLICATES: DuplicateKind<Contact> = {
  kind: 'contacts',
  noun: { one: 'contact', many: 'contacts' },
  groupNoun: { one: 'person', many: 'people' },
  tone: 'emerald',
  icon: Users,
  load: async () => withCompanyNames(await getContacts()),
  group: duplicateContactGroups,
  fields: CONTACT_MERGE_FIELDS,
  title: (c) => c.name || c.email || 'Unnamed contact',
  subtitle: (c) => [c.email, c.companyName].filter(Boolean).join(' · ') || c.phone,
  created: (c) => c.createdAtUtc,
  href: (c) => `/contacts/${c.id}`,
  moves: ['deals', 'tasks', 'activities', 'emails', 'sequences', 'referred leads', 'files', 'custom fields'],
  fate: (others) => (servesDemoData()
    ? removed(others)
    : `The ${others === 1 ? 'other one is' : 'others are'} archived, not deleted, once everything has moved.`),
};

export const COMPANY_DUPLICATES: DuplicateKind<Company> = {
  kind: 'companies',
  noun: { one: 'company', many: 'companies' },
  groupNoun: { one: 'company', many: 'companies' },
  tone: 'blue',
  icon: Building2,
  load: () => getCompanies(),
  group: duplicateCompanyGroups,
  fields: COMPANY_MERGE_FIELDS,
  title: (c) => c.name || c.domain || 'Unnamed company',
  subtitle: (c) => [c.domain || c.website, c.location].filter(Boolean).join(' · ') || c.industry,
  created: (c) => c.createdAtUtc,
  href: (c) => `/companies/${c.id}`,
  moves: ['contacts', 'deals', 'leads', 'files', 'custom fields'],
  fate: removed,
};

export const LEAD_DUPLICATES: DuplicateKind<Lead> = {
  kind: 'leads',
  noun: { one: 'lead', many: 'leads' },
  groupNoun: { one: 'person', many: 'people' },
  tone: 'orange',
  icon: UserRound,
  load: async () => withCompanyNames(await getLeads()),
  group: duplicateLeadGroups,
  fields: LEAD_MERGE_FIELDS,
  title: (l) => l.name || l.email || 'Unnamed lead',
  subtitle: (l) => [l.email, l.companyName, l.status].filter(Boolean).join(' · ') || l.phone,
  created: (l) => l.createdAtUtc,
  href: (l) => `/leads/${l.id}`,
  moves: ['tasks', 'activities', 'status history', 'emails', 'contracts', 'sequences', 'files', 'custom fields'],
  fate: removed,
};

export const DEAL_DUPLICATES: DuplicateKind<Deal> = {
  kind: 'deals',
  noun: { one: 'deal', many: 'deals' },
  groupNoun: { one: 'deal', many: 'deals' },
  tone: 'violet',
  icon: Target,
  load: async () => withCompanyNames(await getDeals()),
  group: duplicateDealGroups,
  fields: DEAL_MERGE_FIELDS,
  title: (d) => d.name || 'Unnamed deal',
  subtitle: (d) => [d.companyName, d.dealStageName || d.stage].filter(Boolean).join(' · ') || undefined,
  created: (d) => d.createdAtUtc,
  href: (d) => `/deals/${d.id}`,
  moves: ['tasks', 'activities', 'stage history', 'line items', 'emails', 'contracts', 'files', 'custom fields'],
  fate: removed,
};

export const MEMBER_DUPLICATES: DuplicateKind<Member> = {
  kind: 'members',
  noun: { one: 'member', many: 'members' },
  groupNoun: { one: 'member', many: 'members' },
  tone: 'indigo',
  icon: Crown,
  load: () => getMembers(),
  group: duplicateMemberGroups,
  fields: MEMBER_MERGE_FIELDS,
  title: (m) => `${m.firstName} ${m.lastName}`.trim() || m.email || 'Unnamed member',
  subtitle: (m) => [m.email, `${m.tier} · ${m.status}`].filter(Boolean).join(' · '),
  created: (m) => m.joinedAtUtc,
  href: (m) => `/members/${m.id}`,
  moves: ['visits', 'loyalty points', 'charges', 'payments', 'bookings', 'gift cards', 'donations', 'subscriptions', 'household links'],
  fate: removed,
};

/** Households with the names of the people in them, for the preview. */
async function householdsForMerge(): Promise<HouseholdForMerge[]> {
  const [households, links, members] = await Promise.all([getHouseholds(), getHouseholdMembers(), getMembers()]);
  const name = new Map(members.map((m) => [m.id, `${m.firstName} ${m.lastName}`.trim()]));
  return households.map((h) => ({
    ...h,
    primaryName: name.get(h.primaryMemberId),
    memberNames: links.filter((l) => l.householdId === h.id).map((l) => name.get(l.memberId) ?? 'A member'),
  }));
}

export const HOUSEHOLD_DUPLICATES: DuplicateKind<HouseholdForMerge> = {
  kind: 'households',
  noun: { one: 'household', many: 'households' },
  groupNoun: { one: 'household', many: 'households' },
  tone: 'teal',
  icon: Home,
  load: householdsForMerge,
  group: duplicateHouseholdGroups,
  fields: HOUSEHOLD_MERGE_FIELDS,
  title: (h) => h.name || 'Unnamed household',
  subtitle: (h) => [h.primaryName, h.memberNames.length ? `${h.memberNames.length} ${h.memberNames.length === 1 ? 'person' : 'people'}` : undefined].filter(Boolean).join(' · '),
  created: (h) => h.createdAtUtc,
  moves: ['people', 'their billing roll-up', 'notes'],
  fate: removed,
};
