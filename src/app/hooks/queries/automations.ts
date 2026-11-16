// The automations page's reads, as cached queries keyed by workspace.

import { useQuery } from '@tanstack/react-query';
import {
  getAutomationCatalog, getAutomationRun, getAutomationRuns, getAutomations, type RunQuery,
} from '@/app/api/automations';
import { queryKeys } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

export function useAutomations() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.automations.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getAutomations({ signal: s })),
  });
}

/** The triggers, what each can test, wait for and do: with the workspace's own tags and custom fields, refreshed when one changes. */
export function useAutomationCatalog() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.automations.catalog(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getAutomationCatalog({ signal: s })),
    staleTime: 60 * 60 * 1000,
  });
}

export function useAutomationRuns(query: RunQuery, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.automations.runs(ws, query),
    queryFn: ({ signal }) => timed(signal, (s) => getAutomationRuns(query, { signal: s })),
    enabled,
  });
}

export function useAutomationRun(runId: string | null) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.automations.run(ws, runId ?? ''),
    queryFn: ({ signal }) => timed(signal, (s) => getAutomationRun(runId!, { signal: s })),
    enabled: runId != null,
  });
}
