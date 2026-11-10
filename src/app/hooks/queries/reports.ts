// The dashboard's figures, as cached queries keyed by workspace. Each one fails on its
// own: a broken pipeline chart does not blank the team figures beside it.

import { useQuery } from '@tanstack/react-query';
import {
  getCopyHistoryStats, getDashboardStats, getPipelineValueByAssignee, getPipelineValueByStage, getTemplates,
  getForecast, getReportSummary, getSalesTeams, type ReportFilters,
} from '@/app/api';
import { keepPreviousInWorkspace } from './records';
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

/** The Reports page's figures for these filters. Off while the filters are being typed (null). */
export function useReportSummary(filters: ReportFilters | null) {
  const ws = useWorkspaceScope();
  const key = queryKeys.reports.summary(ws, filters ?? {});
  return useQuery<Awaited<ReturnType<typeof getReportSummary>>>({
    queryKey: key,
    queryFn: ({ signal }) => timed(signal, (s) => getReportSummary(filters!, { signal: s })),
    enabled: !!filters,
    placeholderData: keepPreviousInWorkspace<Awaited<ReturnType<typeof getReportSummary>>>(key),
  });
}

type ForecastParams = Parameters<typeof getForecast>[0];

/** The forecast by close month for these filters. Off while the filters are being typed (null). */
export function useForecast(params: ForecastParams | null) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.reports.forecast(ws, params ?? {}),
    queryFn: ({ signal }) => timed(signal, (s) => getForecast(params!, { signal: s })),
    enabled: !!params,
  });
}

/** The workspace's sales teams, for the "My team" scope. */
export function useSalesTeams() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.team.salesTeams(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getSalesTeams({ signal: s })),
  });
}
