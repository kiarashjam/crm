// The reads the sales pages make, as cached queries keyed by workspace.
//
// Each asks the same endpoint the page asked before; what changes is how: one copy per
// workspace shared by every page and the sales agent, a request cancelled when nobody
// needs it any more, a failure the page can show and retry, and a refresh when a save
// changes that kind of record (invalidation.ts).

import { keepPreviousData, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { useCallback } from 'react';
import {
  getActivitiesByContact, getActivitiesByDeal, getActivitiesByLead, getActivitiesByLeadIds, getActivitiesPaged,
  type GetActivitiesPagedOptions,
} from '@/app/api/activities';
import { getCompanies, getCompaniesPaged, getCompanySummary } from '@/app/api/companies';
import { getContactStats, getContacts, getContactsPaged } from '@/app/api/contacts';
import { getDealBoard, getDeals, getDealsPaged } from '@/app/api/deals';
import type { DealBoardQuery } from '@/app/api/types';
import { getLeadSources } from '@/app/api/leadSources';
import { getLeadStatuses } from '@/app/api/leadStatuses';
import { getLeadStats, getLeads, getLeadsPaged } from '@/app/api/leads';
import { getOrgMembers } from '@/app/api/organizations';
import { getPipelines } from '@/app/api/pipelines';
import { getTaskStats, getTasks, type GetTasksOptions } from '@/app/api/tasks';
import type { WorkScope } from '@/app/lib/workScope';
import { queryKeys, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

/** What a list read is asked: its first argument. */
type Params<F extends (...args: never[]) => unknown> = NonNullable<Parameters<F>[0]>;

/**
 * Write into a cached read: a page's own change shown at once, before (or instead of)
 * reading the list again. `update` gets the cached value, or undefined before the
 * first read.
 */
export function useCacheWriter() {
  const client = useQueryClient();
  return useCallback(
    <T,>(key: QueryKey, update: (current: T | undefined) => T | undefined) => client.setQueryData<T>(key, update),
    [client],
  );
}

// ── leads ────────────────────────────────────────────────────────────────────

export function useLeadsPage(params: Params<typeof getLeadsPaged>, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.leads.page(ws, params),
    queryFn: ({ signal }) => timed(signal, (s) => getLeadsPaged(params, { signal: s })),
    enabled,
    // A new page or search keeps the last one on screen until it arrives.
    placeholderData: keepPreviousData,
  });
}

export function useAllLeads(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.leads.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getLeads({ signal: s })),
    enabled,
  });
}

export function useLeadStats() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.leads.stats(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getLeadStats({ signal: s })),
  });
}

export function useLeadStatuses() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.leadLookups.statuses(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getLeadStatuses({ signal: s })),
    // Rarely changed, and refreshed when they are (api/dataChanged: leadstatuses).
    staleTime: 5 * 60 * 1000,
  });
}

export function useLeadSources() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.leadLookups.sources(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getLeadSources({ signal: s })),
    staleTime: 5 * 60 * 1000,
  });
}

// ── deals ────────────────────────────────────────────────────────────────────

export function useAllDeals(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.deals.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getDeals({ signal: s })),
    enabled,
  });
}

export function useDealsPage(params: Params<typeof getDealsPaged>, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.deals.page(ws, params),
    queryFn: ({ signal }) => timed(signal, (s) => getDealsPaged(params, { signal: s })),
    enabled,
  });
}

export function dealBoardKey(ws: WorkspaceScope, query: DealBoardQuery) {
  return queryKeys.deals.board(ws, query);
}

export function useDealBoard(query: DealBoardQuery, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: dealBoardKey(ws, query),
    queryFn: ({ signal }) => timed(signal, (s) => getDealBoard(query, { signal: s })),
    enabled,
    placeholderData: keepPreviousData,
  });
}

// ── contacts and companies ───────────────────────────────────────────────────

export function useContactsPage(params: Params<typeof getContactsPaged>) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.contacts.page(ws, params),
    queryFn: ({ signal }) => timed(signal, (s) => getContactsPaged(params, { signal: s })),
    placeholderData: keepPreviousData,
  });
}

export function useContactStats() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.contacts.stats(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getContactStats({ signal: s })),
  });
}

export function useAllContacts(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.contacts.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getContacts(false, { signal: s })),
    enabled,
  });
}

export function useCompaniesPage(params: Params<typeof getCompaniesPaged>) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.companies.page(ws, params),
    queryFn: ({ signal }) => timed(signal, (s) => getCompaniesPaged(params, { signal: s })),
    placeholderData: keepPreviousData,
  });
}

export function useCompanySummary() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.companies.summary(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getCompanySummary({ signal: s })),
  });
}

export function useAllCompanies(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.companies.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getCompanies({ signal: s })),
    enabled,
  });
}

// ── tasks and activities ─────────────────────────────────────────────────────

export function tasksKey(ws: WorkspaceScope, options: GetTasksOptions = {}) {
  return queryKeys.tasks.list(ws, options);
}

export function useTasksList(options: GetTasksOptions = {}, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: tasksKey(ws, options),
    queryFn: ({ signal }) => timed(signal, (s) => getTasks(options, { signal: s })),
    enabled,
  });
}

export function useTaskStats(scope?: WorkScope) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.tasks.stats(ws, scope ?? 'all'),
    queryFn: ({ signal }) => timed(signal, (s) => getTaskStats(scope, { signal: s })),
  });
}

export function activitiesPageKey(ws: WorkspaceScope, params: GetActivitiesPagedOptions) {
  return queryKeys.activities.page(ws, params);
}

export function useActivitiesPage(params: GetActivitiesPagedOptions, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: activitiesPageKey(ws, params),
    queryFn: ({ signal }) => timed(signal, (s) => getActivitiesPaged(params, { signal: s })),
    enabled,
    placeholderData: keepPreviousData,
  });
}

const BY_RECORD = { lead: getActivitiesByLead, deal: getActivitiesByDeal, contact: getActivitiesByContact } as const;

export function recordActivitiesKey(ws: WorkspaceScope, kind: keyof typeof BY_RECORD, id: string) {
  return queryKeys.activities.forRecord(ws, kind, id);
}

/** One lead's, deal's or contact's activities. Off while `id` is empty. */
export function useRecordActivities(kind: keyof typeof BY_RECORD, id: string | null | undefined) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: recordActivitiesKey(ws, kind, id ?? ''),
    queryFn: ({ signal }) => timed(signal, (s) => BY_RECORD[kind](id!, { signal: s })),
    enabled: !!id,
  });
}

/** The activities of the leads on one page, read together. */
export function useActivitiesForLeads(leadIds: readonly string[]) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.activities.forLeads(ws, leadIds),
    queryFn: ({ signal }) => timed(signal, (s) => getActivitiesByLeadIds([...leadIds], { signal: s })),
    enabled: leadIds.length > 0,
    placeholderData: keepPreviousData,
  });
}

// ── the workspace ────────────────────────────────────────────────────────────

export function usePipelines() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.pipelines.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPipelines({ signal: s })),
  });
}

/** The workspace's members. Empty, without a request, in a personal workspace. */
export function useOrgMembers() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.team.members(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getOrgMembers(ws.orgId!, { signal: s })),
    enabled: !!ws.orgId,
  });
}
