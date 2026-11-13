// Companies and private clients at the club, as cached queries keyed by workspace: a client's
// house account and its charges, corporate memberships and the seat a member holds, and private
// events. Each read is under the workspace's prefix like every other (queryKeys.ts), its kind
// the one api/dataChanged names, so a charge posted, a seat changed or a booking made refreshes
// exactly these; a page shows a failed read with a way to try again.

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  getAccountCharges, getClientAccount, getCorporateMembership, getCorporateMemberships, getMemberCorporateSeat, getPrivateEvent,
  getPrivateEvents, type ClientKind, type CorporateQuery, type PrivateEventQuery,
} from '@/app/api/corporate';
import { workspacePrefix, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

const key = (scope: WorkspaceScope, resource: string, ...rest: unknown[]) => [...workspacePrefix(scope), resource, ...rest] as const;

/** The keys of the club's clients, by kind as api/dataChanged names them. */
export const corporateKeys = {
  account: (s: WorkspaceScope, kind: ClientKind, id: string) => key(s, 'club-accounts', kind, id),
  accountCharges: (s: WorkspaceScope, accountId: string) => key(s, 'club-accounts', 'charges', accountId),
  memberships: (s: WorkspaceScope, query: CorporateQuery) => key(s, 'corporate', 'list', query),
  membership: (s: WorkspaceScope, id: string) => key(s, 'corporate', 'one', id),
  memberSeat: (s: WorkspaceScope, memberId: string) => key(s, 'corporate', 'member', memberId),
  privateEvents: (s: WorkspaceScope, query: PrivateEventQuery) => key(s, 'private-events', 'list', query),
  privateEvent: (s: WorkspaceScope, id: string) => key(s, 'private-events', 'one', id),
} as const;

/** A company's or contact's house account. Off while `id` is empty. */
export function useClientAccount(kind: ClientKind, id: string, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: corporateKeys.account(ws, kind, id),
    queryFn: ({ signal }) => timed(signal, (s) => getClientAccount(kind, id, { signal: s })),
    enabled: enabled && id.length > 0,
  });
}

/** The charges on an account (a company's or a contact's key). */
export function useAccountCharges(accountId: string, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: corporateKeys.accountCharges(ws, accountId),
    queryFn: ({ signal }) => timed(signal, (s) => getAccountCharges(accountId, { signal: s })),
    enabled: enabled && accountId.length > 0,
  });
}

export function useCorporateMemberships(query: CorporateQuery = {}, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: corporateKeys.memberships(ws, query),
    queryFn: ({ signal }) => timed(signal, (s) => getCorporateMemberships(query, { signal: s })),
    enabled,
    placeholderData: keepPreviousData,
  });
}

export function useCorporateMembership(id: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: corporateKeys.membership(ws, id),
    queryFn: ({ signal }) => timed(signal, (s) => getCorporateMembership(id, { signal: s })),
    enabled: id.length > 0,
  });
}

/** The corporate seat a member holds, or null. Off while `memberId` is empty. */
export function useMemberCorporateSeat(memberId: string, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: corporateKeys.memberSeat(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getMemberCorporateSeat(memberId, { signal: s })),
    enabled: enabled && memberId.length > 0,
  });
}

export function usePrivateEvents(query: PrivateEventQuery = {}, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: corporateKeys.privateEvents(ws, query),
    queryFn: ({ signal }) => timed(signal, (s) => getPrivateEvents(query, { signal: s })),
    enabled,
    placeholderData: keepPreviousData,
  });
}

/** One private event as the server keeps it now. Off while `id` is empty. */
export function usePrivateEvent(id: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: corporateKeys.privateEvent(ws, id),
    queryFn: ({ signal }) => timed(signal, (s) => getPrivateEvent(id, { signal: s })),
    enabled: id.length > 0,
  });
}
