// The club's members and money, as cached queries keyed by workspace.
//
// The club pages each read their lists once on mount and kept them in local state: a
// failed read left a list empty, which looks exactly like having nothing (no charges, no
// cards), and a payment taken on one page left another showing the old balance. These
// read the same endpoints through the app's cache: one copy per workspace shared by every
// club page, a request cancelled when nobody needs it, a failure a page shows with a way
// to try again, and a refresh when a save changes that kind of record (invalidation.ts,
// api/dataChanged).

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { getMembers } from '@/app/api/members';
import { getCharges, getPayments, getMemberStatement } from '@/app/api/charges';
import { getGiftCards, getRedemptions } from '@/app/api/giftCards';
import { getTabs } from '@/app/api/tabs';
import { getSubscriptions } from '@/app/api/subscriptions';
import { getDonations } from '@/app/api/donations';
import { getLoyaltyLedger } from '@/app/api/loyalty';
import { getVisits } from '@/app/api/visits';
import { queryKeys } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

export function useClubMembers() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.members(ws), queryFn: ({ signal }) => timed(signal, (s) => getMembers({ signal: s })) });
}

export function useClubCharges() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.charges(ws), queryFn: ({ signal }) => timed(signal, (s) => getCharges({ signal: s })) });
}

export function useClubPayments() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.payments(ws), queryFn: ({ signal }) => timed(signal, (s) => getPayments({ signal: s })) });
}

export function useGiftCards() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.giftCards(ws), queryFn: ({ signal }) => timed(signal, (s) => getGiftCards({ signal: s })) });
}

export function useGiftCardRedemptions() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.redemptions(ws), queryFn: ({ signal }) => timed(signal, (s) => getRedemptions({ signal: s })) });
}

export function useClubTabs() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.tabs(ws), queryFn: ({ signal }) => timed(signal, (s) => getTabs({ signal: s })) });
}

export function useClubSubscriptions() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.subscriptions(ws), queryFn: ({ signal }) => timed(signal, (s) => getSubscriptions({ signal: s })) });
}

export function useClubDonations() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.donations(ws), queryFn: ({ signal }) => timed(signal, (s) => getDonations({ signal: s })) });
}

export function useLoyaltyLedger() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.loyalty(ws), queryFn: ({ signal }) => timed(signal, (s) => getLoyaltyLedger({ signal: s })) });
}

export function useClubVisits() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: queryKeys.club.visits(ws), queryFn: ({ signal }) => timed(signal, (s) => getVisits({ signal: s })) });
}

/** A member's statement for [fromUtc, toUtc), from the server's ledger. Off until a member is picked. */
export function useMemberStatement(memberId: string | null, fromUtc: string, toUtc: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.club.statement(ws, memberId ?? '', fromUtc, toUtc),
    queryFn: ({ signal }) => timed(signal, (s) => getMemberStatement(memberId!, fromUtc, toUtc, { signal: s })),
    enabled: !!memberId,
  });
}

/** The list a page shows before its read arrives: one array, so what is worked out from it is not redone each render. */
export const NONE: never[] = [];

/** What a page that reads several lists shows: still loading, failed with nothing to show, or failed with a copy still on screen. */
export interface ClubReads {
  /** A first read is on its way. */
  loading: boolean;
  /** A read failed with nothing read before it: show the failure, not an empty list. */
  failed: boolean;
  /** A refresh failed while an earlier copy is on screen. */
  stale: boolean;
  error: unknown;
  /** A retry is on its way. */
  retrying: boolean;
  /** Read every list again; what a page awaits after a save of its own. */
  reload: () => Promise<void>;
}

/** The state of a page's reads, taken together. */
export function useClubReads(...queries: UseQueryResult<unknown>[]): ClubReads {
  const failedQuery = queries.find((q) => q.isError && q.data === undefined);
  const staleQuery = queries.find((q) => q.isError && q.data !== undefined);
  return {
    // A read with nothing yet that is on its way; a disabled one (no member picked) is not loading.
    loading: queries.some((q) => q.isPending && q.fetchStatus !== 'idle'),
    failed: failedQuery !== undefined,
    stale: failedQuery === undefined && staleQuery !== undefined,
    error: (failedQuery ?? staleQuery)?.error,
    retrying: queries.some((q) => q.isFetching),
    // A read that is off (a statement before a member is picked) stays off: refetch would run it anyway.
    reload: async () => {
      await Promise.all(queries.filter((q) => q.isEnabled).map((q) => q.refetch()));
    },
  };
}
