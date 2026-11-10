// Sales and the POS, as cached queries keyed by workspace: whether a POS is connected, its
// settings and locations, its tickets, the latest sales, and a member's codes. The fourth part
// of each key is the kind of record api/dataChanged names, so a sale, an import or a match
// refreshes exactly these (and the money lists they post to).

import { useQuery } from '@tanstack/react-query';
import { getSales } from '@/app/api/sales';
import { getMemberCodes, getPosLocations, getPosSettings, getPosStatus, getPosTickets, type PosTicketFilter } from '@/app/api/pos';
import { workspacePrefix, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

const key = (s: WorkspaceScope, resource: string, ...rest: unknown[]) => [...workspacePrefix(s), resource, ...rest] as const;

export const posKeys = {
  status: (s: WorkspaceScope) => key(s, 'pos', 'status'),
  settings: (s: WorkspaceScope) => key(s, 'pos', 'settings'),
  locations: (s: WorkspaceScope) => key(s, 'pos', 'locations'),
  tickets: (s: WorkspaceScope, filter: PosTicketFilter) => key(s, 'pos', 'tickets', filter),
  sales: (s: WorkspaceScope, memberId: string | null) => key(s, 'sales', memberId ?? 'all'),
  memberCodes: (s: WorkspaceScope, memberId: string) => key(s, 'member-codes', memberId),
} as const;

/** Whether a POS is connected and importing, and how many tickets wait for staff. */
export function usePosStatus() {
  const ws = useWorkspaceScope();
  return useQuery({ queryKey: posKeys.status(ws), queryFn: ({ signal }) => timed(signal, (s) => getPosStatus({ signal: s })) });
}

/** The POS settings (no token). Off for someone who may not change them. */
export function usePosSettings(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: posKeys.settings(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPosSettings({ signal: s })),
    enabled,
  });
}

/** The connected account's business locations. Off until it is connected. */
export function usePosLocations(enabled: boolean) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: posKeys.locations(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPosLocations({ signal: s })),
    enabled,
  });
}

/** The POS's tickets: those waiting for staff (the default), one status, or all. */
export function usePosTickets(filter: PosTicketFilter = 'attention') {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: posKeys.tickets(ws, filter),
    queryFn: ({ signal }) => timed(signal, (s) => getPosTickets(filter, { signal: s })),
  });
}

/** The latest sales, the club's or one member's. */
export function useSales(memberId: string | null = null) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: posKeys.sales(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getSales(memberId ?? undefined, { signal: s })),
  });
}

/** The codes that name a member at the POS. Off while `memberId` is empty. */
export function useMemberCodes(memberId: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: posKeys.memberCodes(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getMemberCodes(memberId, { signal: s })),
    enabled: memberId.length > 0,
  });
}
