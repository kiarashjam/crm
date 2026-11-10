// The records the sales agent reasons over: loaded once, shared, kept fresh.
//
// The panel sits in the header of every page, and every page renders its own
// header — so the panel remounted on every navigation and re-read eight full
// lists each time (leads, deals, tasks, enrollments, sequences, contacts,
// companies, automations). Opening Settings cost eight list loads. And because it
// only ever loaded on mount, it never noticed an edit made on the page beneath
// it: log "no answer" on a lead and the panel above kept saying "make the first
// call", telling you to do what you had just done.
//
// So the lists live in the app's query cache (hooks/queries), keyed by workspace:
//
//   · loaded once per workspace, reused across navigations, and shared with the
//     pages that read the same lists (Tasks, the board and Contacts read some);
//   · after a save, only the lists of the kinds it changed are re-read, quietly
//     behind what is shown (api/dataChanged). Every save used to re-read all eight,
//     so logging one call downloaded every lead, contact and deal again; a task
//     saved now re-reads tasks. Also re-read when the tab comes back into view or
//     the copy is older than FRESH_FOR_MS — never with a skeleton once there is data;
//   · a list nobody is looking at is only marked stale, and read when next shown;
//   · never shown under another workspace: its keys are that workspace's.

import { useCallback, useMemo } from 'react';
import { useQueries, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { servesDemoData, type RequestOptions } from '@/app/api/apiClient';
import { getNotSamePairs } from '@/app/api/dedupe';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { getAuthToken, getCurrentUser } from '@/app/lib/auth';
import { queryKeys, type WorkspaceScope } from '@/app/hooks/queries/queryKeys';
import { timed } from '@/app/hooks/queries/queryClient';
import type { SuggestableLead } from './nextAction';
import type {
  SuggestableAutomation,
  SuggestableCompany,
  SuggestableContact,
  SuggestableDeal,
  SuggestableEnrollment,
  SuggestableSequence,
  SuggestableTask,
} from './workspace';

export { SETTLE_MS } from '@/app/hooks/queries/invalidation';

export interface AgentLists {
  leads: SuggestableLead[];
  deals: SuggestableDeal[];
  tasks: SuggestableTask[];
  enrollments: SuggestableEnrollment[];
  sequences: SuggestableSequence[];
  contacts: SuggestableContact[];
  companies: SuggestableCompany[];
  automations: SuggestableAutomation[];
  /** Contact and company pairs the team said are not the same, so they are not offered for merging. */
  notSame?: string[];
}

export interface AgentSnapshot {
  /** Which workspace these lists belong to. */
  key: string | null;
  /** The lists, or null before the first successful load for this workspace. */
  lists: AgentLists | null;
  /** First load in flight — there is nothing to show yet. */
  loading: boolean;
  /** A reload is running behind lists that are already shown. */
  refreshing: boolean;
  /** The last load failed outright. Older lists, if any, are still shown. */
  failed: boolean;
  /**
   * Lists the last load could not read, as sentences. Each one either is empty because it
   * never loaded, or still shows an earlier copy. An answer built on them has to say so.
   */
  gaps: string[];
  /** Re-read every list now, behind whatever is shown: the panel's retry. */
  reload: () => void;
}

/** How long a loaded copy is trusted before a navigation re-reads it. */
export const FRESH_FOR_MS = 60_000;

type ListName = keyof Omit<AgentLists, 'notSame'>;

interface ListSource {
  name: ListName;
  label: string;
  key: (scope: WorkspaceScope) => readonly unknown[];
  read: (req: RequestOptions) => Promise<unknown>;
}

// The same reads, under the same keys, as the pages that show these lists.
const LISTS: readonly ListSource[] = [
  { name: 'leads', label: 'Leads', key: queryKeys.leads.all, read: (req) => getLeads(req) },
  { name: 'deals', label: 'Deals', key: queryKeys.deals.all, read: (req) => getDeals(req) },
  { name: 'tasks', label: 'Tasks', key: (s) => queryKeys.tasks.list(s), read: (req) => getTasks(undefined, req) },
  { name: 'enrollments', label: 'Sequence enrollments', key: queryKeys.sequences.enrollments, read: (req) => getEnrollments(undefined, req) },
  { name: 'sequences', label: 'Sequences', key: queryKeys.sequences.all, read: (req) => getSequences(req) },
  { name: 'contacts', label: 'Contacts', key: queryKeys.contacts.all, read: (req) => getContacts(false, req) },
  { name: 'companies', label: 'Companies', key: queryKeys.companies.all, read: (req) => getCompanies(req) },
  { name: 'automations', label: 'Automations', key: queryKeys.automations.all, read: (req) => getAutomations(req) },
];

/** The team's "not the same" decisions on contacts and companies (kept on the server; none in demo mode). */
async function getNotSame(req: RequestOptions): Promise<string[]> {
  if (servesDemoData()) return [];
  const [contacts, companies] = await Promise.all([getNotSamePairs('contacts', req), getNotSamePairs('companies', req)]);
  return [...contacts, ...companies];
}

function rows<T>(value: unknown): T[] {
  return Array.isArray(value) ? value.filter((item): item is T => !!item && typeof item === 'object') : [];
}

/** The identity the lists belong to: this organisation, for this signed-in session. */
export function workspaceKey(orgId: string | null | undefined): string {
  return `${orgId ?? 'no-org'}|${getAuthToken() ?? 'no-session'}`;
}

function snapshotOf(key: string, results: UseQueryResult<unknown>[], reload: () => void): AgentSnapshot {
  const lists = results.slice(0, LISTS.length);
  const decided = results[LISTS.length];
  const settled = lists.every((result) => result.status !== 'pending');
  const fetching = results.some((result) => result.isFetching);
  // A list failing keeps its last good copy: an outage on deals must not read as
  // "no deals need anything", which would be a quiet lie. The gap is said instead,
  // so the agent can say the list is old or missing instead of counting it.
  const gaps = LISTS.flatMap((list, index) => {
    const result = lists[index]!;
    if (result.status !== 'error') return [];
    return [result.data === undefined
      ? `${list.label} could not be loaded.`
      : `${list.label} could not be refreshed, so an earlier copy is used.`];
  });
  const nothing = lists.every((result) => result.data === undefined);
  const allFailed = lists.every((result) => result.status === 'error');
  if (!settled || nothing) {
    return { key, lists: null, loading: fetching || !settled, refreshing: false, failed: settled && allFailed, gaps: [], reload };
  }
  const read = {} as Record<ListName, unknown[]>;
  LISTS.forEach((list, index) => { read[list.name] = rows(lists[index]!.data); });
  return {
    key,
    lists: {
      ...(read as unknown as AgentLists),
      // The decisions only keep pairs out of the merge suggestions: without them,
      // nothing is hidden, and no list is reported missing for them.
      notSame: Array.isArray(decided?.data) ? (decided.data as string[]) : [],
    },
    loading: false,
    refreshing: fetching,
    failed: allFailed,
    gaps,
    reload,
  };
}

/**
 * The agent's lists for this organisation. Loads on first use, re-reads behind the
 * scenes after a save that changed them, and never returns another workspace's
 * lists. `enabled` false (the panel is hidden) reads nothing and watches nothing.
 */
export function useAgentData(orgId: string | null | undefined, enabled = true): AgentSnapshot {
  const userId = getCurrentUser()?.id ?? null;
  const scope = useMemo<WorkspaceScope>(() => ({ orgId: orgId ?? null, userId }), [orgId, userId]);
  const results = useQueries({
    queries: [
      ...LISTS.map((list) => ({
        queryKey: list.key(scope),
        queryFn: ({ signal }: { signal: AbortSignal }) => timed(signal, (s) => list.read({ signal: s })),
        enabled,
        staleTime: FRESH_FOR_MS,
        // The panel offers its own retry and says which lists are missing.
        retry: false,
      })),
      {
        queryKey: queryKeys.duplicates.notSame(scope),
        queryFn: ({ signal }: { signal: AbortSignal }) => timed(signal, (s) => getNotSame({ signal: s })),
        enabled,
        staleTime: FRESH_FOR_MS,
        retry: false,
      },
    ],
  });
  const client = useQueryClient();
  const reload = useCallback(() => {
    for (const list of LISTS) void client.refetchQueries({ queryKey: list.key(scope), exact: true });
    void client.refetchQueries({ queryKey: queryKeys.duplicates.notSame(scope), exact: true });
  }, [client, scope]);
  const key = workspaceKey(orgId);
  // Recomputed only when a list's state moves, not on every render of every page.
  const signature = results.map((r) => `${r.status}|${r.fetchStatus}|${r.dataUpdatedAt}|${r.errorUpdatedAt}`).join(',');
  // eslint-disable-next-line react-hooks/exhaustive-deps -- `signature` stands for `results`
  return useMemo(() => snapshotOf(key, results, reload), [key, signature, reload]);
}
