// Who the newsletter goes to, as chosen in its first step, and the request the server counts
// it from. The server decides who is left out and why; this only says it in a few words.

import type { ComponentType } from 'react';
import { Ban, CalendarCheck, Copy, MailWarning, MailX } from 'lucide-react';
import type { NewsletterAudience, NewsletterAudienceRequest } from '@/app/api/leadNewsletter';
import { LOST_STATUSES, statusIn } from '../leadStatusSync';
import { STATUS_BADGE_COLORS } from '../config';

export type CreatedRange = 'any' | '30' | '90' | '365' | 'custom';

export interface AudienceFilters {
  /** The statuses chosen; null: as it came (every status but the lost and not interested). */
  statuses: string[] | null;
  sources: string[];
  /** People whose leads it goes to; none (and not unassigned): anyone's. */
  owners: string[];
  unassigned: boolean;
  created: CreatedRange;
  /** yyyy-mm-dd, for a custom range. */
  createdFrom: string;
  createdTo: string;
  includeConverted: boolean;
}

export const DEFAULT_FILTERS: AudienceFilters = {
  statuses: null,
  sources: [],
  owners: [],
  unassigned: false,
  created: 'any',
  createdFrom: '',
  createdTo: '',
  includeConverted: false,
};

/** A status meaning the person is out: lost, or said they are not interested. */
export function isOutStatus(status: string): boolean {
  return statusIn(status, LOST_STATUSES) || /not\s+interested|lost|unqualified/i.test(status);
}

/** The statuses a newsletter goes to unless chosen otherwise: all but those who are out. */
export function defaultStatuses(all: readonly string[]): string[] {
  return all.filter((s) => !isOutStatus(s));
}

export function chosenStatuses(filters: AudienceFilters, all: readonly string[]): string[] {
  return filters.statuses ?? defaultStatuses(all);
}

/** The day `days` ago, yyyy-mm-dd in UTC, as the server reads a day. */
function daysAgo(days: number, today: Date): string {
  return new Date(today.getTime() - days * 86_400_000).toISOString().slice(0, 10);
}

/**
 * The request for these filters. Statuses are sent both ways: only the ones chosen, and never
 * the ones not, so a lead in an old status the list no longer has ("Lost", after the rename to
 * "Lost / Not Interested") is not invited by accident. Every status chosen: no status filter.
 */
export function toAudienceRequest(filters: AudienceFilters, all: readonly string[], today = new Date()): NewsletterAudienceRequest {
  const on = chosenStatuses(filters, all);
  const off = all.filter((s) => !on.includes(s));
  const request: NewsletterAudienceRequest = {};
  if (off.length > 0) {
    request.includeStatuses = on;
    request.excludeStatuses = off;
  }
  if (filters.sources.length > 0) request.sources = filters.sources;
  if (filters.owners.length > 0) request.ownerIds = filters.owners;
  if (filters.unassigned) request.unassigned = true;
  if (filters.created === 'custom') {
    if (filters.createdFrom) request.createdFrom = filters.createdFrom;
    if (filters.createdTo) request.createdTo = filters.createdTo;
  } else if (filters.created !== 'any') {
    request.createdFrom = daysAgo(Number(filters.created), today);
  }
  if (filters.includeConverted) request.includeConverted = true;
  return request;
}

/** How many of the filters beyond the statuses are in use, for the "More filters" button. */
export function moreFilterCount(filters: AudienceFilters): number {
  return (filters.sources.length > 0 ? 1 : 0)
    + (filters.owners.length > 0 || filters.unassigned ? 1 : 0)
    + (filters.created !== 'any' ? 1 : 0)
    + (filters.includeConverted ? 1 : 0);
}

export interface ReasonInfo {
  label: string;
  icon: ComponentType<{ className?: string }>;
}

/** Why someone is left out, in a few words. "already_booked" is ready for when the server says it. */
export const REASONS: Record<string, ReasonInfo> = {
  no_email: { label: 'No email', icon: MailX },
  do_not_contact: { label: 'Do not contact', icon: Ban },
  suppressed: { label: 'Unsubscribed or bounced', icon: MailWarning },
  duplicate: { label: 'Same email as another', icon: Copy },
  already_booked: { label: 'Already booked', icon: CalendarCheck },
};

export function reasonInfo(reason: string | null | undefined): ReasonInfo {
  return (reason && REASONS[reason]) || { label: reason ? reason.replace(/_/g, ' ') : 'Left out', icon: Ban };
}

/** The counts of those left out, by reason, as the server gave them; those with none are not listed. */
export function leftOutCounts(audience: NewsletterAudience): { reason: string; count: number }[] {
  return [
    { reason: 'no_email', count: audience.noEmail },
    { reason: 'do_not_contact', count: audience.doNotContact },
    { reason: 'suppressed', count: audience.suppressed },
    { reason: 'duplicate', count: audience.duplicates },
  ].filter((r) => r.count > 0);
}

export function leftOutTotal(audience: NewsletterAudience): number {
  return audience.noEmail + audience.doNotContact + audience.suppressed + audience.duplicates;
}

/** A status's colours, as the Leads page shows them; grey for one it does not know. */
export function statusColors(status: string): { bg: string; text: string; border: string; dot: string } {
  return STATUS_BADGE_COLORS[status] ?? { bg: 'bg-slate-50', text: 'text-slate-600', border: 'border-slate-200', dot: 'bg-slate-400' };
}

/** "1 lead", "23 leads". */
export function leads(count: number): string {
  return `${count.toLocaleString()} lead${count === 1 ? '' : 's'}`;
}
