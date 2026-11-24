// Who a campaign or an event's invitations are for: one audience of club members, worked out
// by the server the same way for the counts, the preview and the send.
//
// Every filter given must hold; one left out chooses everyone:
//   tiers, statuses              as campaigns have always had
//   tags (any of, or all of), notTags, fields   the Members list's own conditions on tags and
//                                custom fields, so a club's "Interests" field works here too:
//                                "interests:any:Wine|Art"
//   viewId                       a saved Members view to start from (its tier, status, tags, fields)
//   attendedEventIds             came to any of these events (checked in at the door)
//   minEvents + eventMonths      came to at least N events in the last M months
//   minVisits + visitDays        visited the club at least N times in the last D days
//   notVisitedDays               has not visited in the last D days
//
// Mirrors MemberAudienceFilter / MemberAudienceDto in backend/src/ACI.Application/Services/MemberAudience.cs.

import type { FlexFilters } from './flexFilters';

export interface MemberAudienceFilter {
  tiers?: string[] | null;
  statuses?: string[] | null;
  /** Tag ids: members with any of them, or all of them with `allTags`. */
  tags?: string[] | null;
  allTags?: boolean | null;
  /** Tag ids: members with none of them. */
  notTags?: string[] | null;
  /** Conditions on the members' custom fields, "key:op[:value]", all of which must hold. */
  fields?: string[] | null;
  /** A saved Members view to start from. */
  viewId?: string | null;
  /** The view as a saved campaign kept it (filled in by the server). */
  view?: AudienceView | null;
  attendedEventIds?: string[] | null;
  minEvents?: number | null;
  eventMonths?: number | null;
  minVisits?: number | null;
  visitDays?: number | null;
  notVisitedDays?: number | null;
}

/** A saved Members view as an audience applies it. */
export interface AudienceView {
  id: string;
  name: string;
  tier?: string | null;
  status?: string | null;
  tags: string[];
  allTags: boolean;
  notTags: string[];
  fields: string[];
}

/** Why a member the filters chose does not get the email. The server may add reasons: show any other as it is. */
export type AudienceReason =
  | 'status' | 'erased' | 'no_email' | 'no_consent' | 'suppressed' | 'do_not_contact'
  | 'already_registered' | 'already_invited' | 'duplicate';

export interface AudiencePerson {
  memberId: string;
  name: string;
  email?: string | null;
  tier: string;
  status: string;
  reason?: AudienceReason | (string & {}) | null;
  /** The language they are written to in: en, de or fr. */
  language: string;
}

/** Who an audience reaches now, counted by the server. Chosen less every left-out count is willReceive. */
export interface MemberAudience {
  purpose: 'marketing' | 'service' | (string & {});
  /** Members the filters chose, their statuses aside. */
  chosen: number;
  /** Of those, the ones whose status is chosen too. */
  matching: number;
  withAddress: number;
  agreed: number;
  /** Emails that would go: one per member, one per address. */
  willReceive: number;
  suppressed: number;
  doNotContact: number;
  statusExcluded: number;
  erased: number;
  noEmail: number;
  noConsent: number;
  alreadyRegistered: number;
  alreadyInvited: number;
  duplicates: number;
  /** The first of those who get it. */
  recipients: AudiencePerson[];
  /** The first of those left out, with why. */
  leftOut: AudiencePerson[];
  view?: AudienceView | null;
}

/** Each reason a member is left out, in the order the server checks them, with how it reads. */
export const LEFT_OUT_REASONS: { reason: AudienceReason; count: (a: MemberAudience) => number; label: string; one: string }[] = [
  { reason: 'status', count: (a) => a.statusExcluded, label: 'not in the statuses chosen', one: 'Not in the statuses chosen' },
  { reason: 'erased', count: (a) => a.erased, label: 'erased', one: 'Erased' },
  { reason: 'no_email', count: (a) => a.noEmail, label: 'no email address', one: 'No email address' },
  { reason: 'no_consent', count: (a) => a.noConsent, label: 'no consent to marketing by email', one: 'Has not agreed to marketing by email' },
  { reason: 'suppressed', count: (a) => a.suppressed, label: 'address unsubscribed, bounced or complained', one: 'Address unsubscribed, bounced or complained' },
  { reason: 'do_not_contact', count: (a) => a.doNotContact, label: 'marked do not contact', one: 'Marked do not contact' },
  { reason: 'already_registered', count: (a) => a.alreadyRegistered, label: 'already on the event’s list', one: 'Already on the event’s list' },
  { reason: 'already_invited', count: (a) => a.alreadyInvited, label: 'invited already', one: 'Invited already' },
  { reason: 'duplicate', count: (a) => a.duplicates, label: 'share an address with another member', one: 'Another member at this address gets it' },
];

/** How one person's reason for being left out reads. */
export function reasonLabel(reason: string | null | undefined): string {
  return LEFT_OUT_REASONS.find((r) => r.reason === reason)?.one ?? (reason ?? '');
}

/** The reasons that left someone out, with their counts, those with none left out. */
export function leftOutCounts(audience: MemberAudience): { reason: AudienceReason; label: string; count: number }[] {
  return LEFT_OUT_REASONS.map((r) => ({ reason: r.reason, label: r.label, count: r.count(audience) ?? 0 })).filter((r) => r.count > 0);
}

/** Members, in words: "1 member", "12 members". */
export function members(n: number): string {
  return `${n.toLocaleString()} member${n === 1 ? '' : 's'}`;
}

/** The filter's conditions on tags and fields, as the Members list's filter bar reads them. */
export function flexOf(filter: MemberAudienceFilter): FlexFilters {
  return {
    ...(filter.tags?.length ? { tags: filter.tags } : {}),
    ...(filter.tags && filter.tags.length > 1 && filter.allTags ? { tagMatch: 'all' as const } : {}),
    ...(filter.notTags?.length ? { notTags: filter.notTags } : {}),
    ...(filter.fields?.length ? { fields: filter.fields } : {}),
  };
}

/** The filter with the Members list's tag and field conditions in place of the ones it had. */
export function withFlex(filter: MemberAudienceFilter, flex: FlexFilters): MemberAudienceFilter {
  return {
    ...filter,
    tags: flex.tags?.length ? flex.tags : null,
    allTags: flex.tagMatch === 'all' && (flex.tags?.length ?? 0) > 1 ? true : null,
    notTags: flex.notTags?.length ? flex.notTags : null,
    fields: flex.fields?.length ? flex.fields : null,
  };
}

/** The filter as the server takes it: empty lists and habits left blank are left out, the view's snapshot is not sent. */
export function cleanAudience(filter: MemberAudienceFilter): MemberAudienceFilter {
  const list = <T,>(values: T[] | null | undefined) => (values && values.length ? values : undefined);
  const positive = (n: number | null | undefined) => (typeof n === 'number' && Number.isFinite(n) && n > 0 ? Math.floor(n) : undefined);
  const minEvents = positive(filter.minEvents);
  const minVisits = positive(filter.minVisits);
  const clean: MemberAudienceFilter = {
    tiers: list(filter.tiers),
    statuses: list(filter.statuses),
    tags: list(filter.tags),
    allTags: filter.allTags && (filter.tags?.length ?? 0) > 1 ? true : undefined,
    notTags: list(filter.notTags),
    fields: list(filter.fields),
    viewId: filter.viewId || undefined,
    attendedEventIds: list(filter.attendedEventIds),
    minEvents,
    eventMonths: minEvents ? positive(filter.eventMonths) : undefined,
    minVisits,
    visitDays: minVisits ? positive(filter.visitDays) : undefined,
    notVisitedDays: positive(filter.notVisitedDays),
  };
  return Object.fromEntries(Object.entries(clean).filter(([, v]) => v !== undefined)) as MemberAudienceFilter;
}

/** Whether the filter narrows by anything beyond tiers and statuses. */
export function usesMoreThanTiersAndStatuses(filter: MemberAudienceFilter): boolean {
  const c = cleanAudience(filter);
  return !!(c.tags || c.notTags || c.fields || c.viewId || c.attendedEventIds || c.minEvents || c.minVisits || c.notVisitedDays);
}

/** The habits a filter asks for, in words, for a summary line. */
export function habitsInWords(filter: MemberAudienceFilter, eventName?: (id: string) => string | undefined): string[] {
  const c = cleanAudience(filter);
  const words: string[] = [];
  if (c.attendedEventIds?.length) {
    const names = c.attendedEventIds.map((id) => eventName?.(id)).filter(Boolean) as string[];
    words.push(names.length === c.attendedEventIds.length ? `came to ${names.join(' or ')}` : `came to any of ${c.attendedEventIds.length} events`);
  }
  if (c.minEvents && c.eventMonths) words.push(`came to at least ${c.minEvents} event${c.minEvents === 1 ? '' : 's'} in the last ${c.eventMonths} month${c.eventMonths === 1 ? '' : 's'}`);
  if (c.minVisits && c.visitDays) words.push(`visited at least ${c.minVisits} time${c.minVisits === 1 ? '' : 's'} in the last ${c.visitDays} days`);
  if (c.notVisitedDays) words.push(`has not visited in the last ${c.notVisitedDays} days`);
  return words;
}
