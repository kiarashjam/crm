// The reads the sales pages make, as cached queries keyed by workspace.
//
// Each asks the same endpoint the page asked before; what changes is how: one copy per
// workspace shared by every page and the sales agent, a request cancelled when nobody
// needs it any more, a failure the page can show and retry, and a refresh when a save
// changes that kind of record (invalidation.ts).

import { useQuery, useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query';
import { useCallback } from 'react';
// Through the API's front door, as the pages import it, so what a page's test puts in
// place of a read is what its queries call.
import {
  getActivitiesByContact, getActivitiesByDeal, getActivitiesByLead, getActivitiesByLeadIds, getActivitiesPaged,
  getCompanies, getCompaniesPaged, getCompanySummary, getContactStats, getContacts, getContactsPaged,
  getDealBoard, getDeals, getDealsPaged, getLeadSources, getLeadStatuses, getLeadStats, getLeads, getLeadsPaged,
  getOrgMembers, getPipelines, getTaskStats, getTasks,
  type DealBoardQuery, type GetActivitiesPagedOptions, type GetTasksOptions,
} from '@/app/api';
import type { WorkScope } from '@/app/lib/workScope';
import { queryKeys, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

/** What a list read is asked: its first argument. */
type Params<F extends (...args: never[]) => unknown> = NonNullable<Parameters<F>[0]>;

/**
 * Write a page's own change into a cached read, to show it at once. `update` gets the
 * cached value, or undefined before the first read.
 *
 * A read of the same list already on its way began before this change, so it cannot
 * hold it, and when it came back it put the list as it was: a second card moved on the
 * board while the read after the first move was out jumped back to its old column, as
 * if the server had refused it. That read is dropped (the copy it was refreshing stays,
 * changed), and the save's own announcement reads the list again. A first read is left
 * to finish: with nothing on screen yet, it is all there is to show.
 */
export function writeOwnChange<T>(client: QueryClient, key: QueryKey, update: (current: T | undefined) => T | undefined) {
  void client.cancelQueries({
    queryKey: key,
    exact: true,
    predicate: (query) => query.state.fetchStatus === 'fetching' && query.state.data !== undefined,
  });
  return client.setQueryData<T>(key, update);
}

/** {@link writeOwnChange}, bound to the app's cache. */
export function useCacheWriter() {
  const client = useQueryClient();
  return useCallback(
    <T,>(key: QueryKey, update: (current: T | undefined) => T | undefined) => writeOwnChange<T>(client, key, update),
    [client],
  );
}

/**
 * Keep the last page on screen while the next one is read (another page, search or
 * filter), but never across workspaces or accounts: after a switch, one workspace's rows
 * must not stand in for another's while they load.
 */
export function keepPreviousInWorkspace<T>(key: readonly unknown[]) {
  return (previous: T | undefined, previousQuery: { queryKey: readonly unknown[] } | undefined): T | undefined => {
    if (!previousQuery) return undefined;
    const [, org, user] = previousQuery.queryKey;
    return org === key[1] && user === key[2] ? previous : undefined;
  };
}

// ── leads ────────────────────────────────────────────────────────────────────

export function useLeadsPage(params: Params<typeof getLeadsPaged>, enabled = true) {
  const ws = useWorkspaceScope();
  const key = queryKeys.leads.page(ws, params);
  return useQuery<Awaited<ReturnType<typeof getLeadsPaged>>>({
    queryKey: key,
    queryFn: ({ signal }) => timed(signal, (s) => getLeadsPaged(params, { signal: s })),
    enabled,
    // A new page or search keeps the last one on screen until it arrives.
    placeholderData: keepPreviousInWorkspace<Awaited<ReturnType<typeof getLeadsPaged>>>(key),
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
  const key = dealBoardKey(ws, query);
  return useQuery<Awaited<ReturnType<typeof getDealBoard>>>({
    queryKey: key,
    queryFn: ({ signal }) => timed(signal, (s) => getDealBoard(query, { signal: s })),
    enabled,
    placeholderData: keepPreviousInWorkspace<Awaited<ReturnType<typeof getDealBoard>>>(key),
  });
}

// ── contacts and companies ───────────────────────────────────────────────────

export function useContactsPage(params: Params<typeof getContactsPaged>) {
  const ws = useWorkspaceScope();
  const key = queryKeys.contacts.page(ws, params);
  return useQuery<Awaited<ReturnType<typeof getContactsPaged>>>({
    queryKey: key,
    queryFn: ({ signal }) => timed(signal, (s) => getContactsPaged(params, { signal: s })),
    placeholderData: keepPreviousInWorkspace<Awaited<ReturnType<typeof getContactsPaged>>>(key),
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
  const key = queryKeys.companies.page(ws, params);
  return useQuery<Awaited<ReturnType<typeof getCompaniesPaged>>>({
    queryKey: key,
    queryFn: ({ signal }) => timed(signal, (s) => getCompaniesPaged(params, { signal: s })),
    placeholderData: keepPreviousInWorkspace<Awaited<ReturnType<typeof getCompaniesPaged>>>(key),
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
  const key = activitiesPageKey(ws, params);
  return useQuery<Awaited<ReturnType<typeof getActivitiesPaged>>>({
    queryKey: key,
    queryFn: ({ signal }) => timed(signal, (s) => getActivitiesPaged(params, { signal: s })),
    enabled,
    placeholderData: keepPreviousInWorkspace<Awaited<ReturnType<typeof getActivitiesPaged>>>(key),
  });
}

type RecordKind = 'lead' | 'deal' | 'contact';

function activitiesOf(kind: RecordKind, id: string, req: { signal: AbortSignal }) {
  if (kind === 'lead') return getActivitiesByLead(id, req);
  if (kind === 'deal') return getActivitiesByDeal(id, req);
  return getActivitiesByContact(id, req);
}

export function recordActivitiesKey(ws: WorkspaceScope, kind: RecordKind, id: string) {
  return queryKeys.activities.forRecord(ws, kind, id);
}

/** One lead's, deal's or contact's activities. Off while `id` is empty. */
export function useRecordActivities(kind: RecordKind, id: string | null | undefined) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: recordActivitiesKey(ws, kind, id ?? ''),
    queryFn: ({ signal }) => timed(signal, (s) => activitiesOf(kind, id!, { signal: s })),
    enabled: !!id,
  });
}

/** The activities of the leads on one page, read together. */
export function useActivitiesForLeads(leadIds: readonly string[]) {
  const ws = useWorkspaceScope();
  const key = queryKeys.activities.forLeads(ws, leadIds);
  return useQuery<Awaited<ReturnType<typeof getActivitiesByLeadIds>>>({
    queryKey: key,
    queryFn: ({ signal }) => timed(signal, (s) => getActivitiesByLeadIds([...leadIds], { signal: s })),
    enabled: leadIds.length > 0,
    placeholderData: keepPreviousInWorkspace<Awaited<ReturnType<typeof getActivitiesByLeadIds>>>(key),
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
