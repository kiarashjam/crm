// Keeping members, as cached queries keyed by workspace: scores, concerns, the club's rules,
// the scoring runs, the renewals board and referrals. Each page shows a failed read with a
// way to try again, and a save refreshes what it changed (invalidation.ts, api/dataChanged).

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  getAtRiskMembers, getConcerns, getMemberReferrals, getMemberRisk, getReferralReport, getRenewalBoard, getRetentionSettings,
  getScoringRuns, type RenewalBoardQuery, type ReferralReportRange,
} from '@/app/api/retention';
import { queryKeys } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

export function useRetentionSettings(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.retention.settings(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getRetentionSettings({ signal: s })),
    enabled,
  });
}

export function useMemberRisk(memberId: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.retention.member(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getMemberRisk(memberId, { signal: s })),
    enabled: !!memberId,
  });
}

export function useMemberConcerns(memberId: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.retention.concerns(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getConcerns(memberId, { signal: s })),
    enabled: !!memberId,
  });
}

export function useAtRiskMembers(level: 'at_risk' | 'watch' = 'at_risk', enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.retention.atRisk(ws, level),
    queryFn: ({ signal }) => timed(signal, (s) => getAtRiskMembers(level, { signal: s })),
    enabled,
  });
}

export function useScoringRuns(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.retention.runs(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getScoringRuns({ signal: s })),
    enabled,
  });
}

export function useRenewalBoard(query: RenewalBoardQuery) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.renewals.board(ws, query),
    queryFn: ({ signal }) => timed(signal, (s) => getRenewalBoard(query, { signal: s })),
    // A new window or filter keeps the last board on screen until the new one comes.
    placeholderData: keepPreviousData,
  });
}

export function useMemberReferrals(memberId: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.referrals.member(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getMemberReferrals(memberId, { signal: s })),
    enabled: !!memberId,
  });
}

export function useReferralReport(range: ReferralReportRange | null) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.referrals.report(ws, range ?? {}),
    queryFn: ({ signal }) => timed(signal, (s) => getReferralReport(range!, { signal: s })),
    enabled: !!range,
    placeholderData: keepPreviousData,
  });
}
