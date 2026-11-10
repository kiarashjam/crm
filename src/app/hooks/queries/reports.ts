// The dashboard's figures, as cached queries keyed by workspace. Each one fails on its
// own: a broken pipeline chart does not blank the team figures beside it.

import { useQuery } from '@tanstack/react-query';
import {
  getCopyHistoryStats, getDashboardStats, getPipelineValueByAssignee, getPipelineValueByStage, getTemplates,
} from '@/app/api';
import { queryKeys } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

export function useDashboardStats() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.reports.dashboard(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getDashboardStats({ signal: s })),
  });
}

export function usePipelineByStage() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.reports.byStage(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPipelineValueByStage({ signal: s })),
  });
}

export function usePipelineByAssignee() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.reports.byAssignee(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPipelineValueByAssignee({ signal: s })),
  });
}

export function useCopyStats() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.copyHistory.stats(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getCopyHistoryStats({ signal: s })),
  });
}

export function useTemplates() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.templates.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getTemplates({ signal: s })),
  });
}
