// Money from outside the club, as cached queries keyed by workspace: the payment settings
// and what they allow, a member's saved card and online payments, Stripe's payments, and
// the bank's files. The fourth part of each key is the kind of record api/dataChanged names,
// so a save refreshes exactly these (a bank file's payments refresh the ledger's lists too).

import { useQuery } from '@tanstack/react-query';
import {
  getBankImports, getBankTransactions, getMemberPaymentProfile, getOnlinePayments, getPaymentSettings, getPaymentStatus,
  type BankTransactionStatus,
} from '@/app/api/payments';
import { workspacePrefix, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

const key = (s: WorkspaceScope, resource: string, ...rest: unknown[]) => [...workspacePrefix(s), resource, ...rest] as const;

export const paymentKeys = {
  // The status is read again with the settings: saving them is what changes it.
  settings: (s: WorkspaceScope) => key(s, 'payment-settings', 'all'),
  status: (s: WorkspaceScope) => key(s, 'payment-settings', 'status'),
  profile: (s: WorkspaceScope, memberId: string) => key(s, 'payment-profile', memberId),
  online: (s: WorkspaceScope, filter: { memberId?: string; status?: string }) => key(s, 'online-payments', filter),
  bankImports: (s: WorkspaceScope) => key(s, 'bank', 'imports'),
  bankTransactions: (s: WorkspaceScope, status: BankTransactionStatus | null) => key(s, 'bank', 'transactions', status ?? 'all'),
} as const;

/** What the money screens offer: pay online (a Stripe key is set), the QR-bill (the bank details are). */
export function usePaymentStatus() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: paymentKeys.status(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPaymentStatus({ signal: s })),
  });
}

/** The settings (no secret). Off for someone who may not change them. */
export function usePaymentSettings(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: paymentKeys.settings(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getPaymentSettings({ signal: s })),
    enabled,
  });
}

/** A member's saved card, dues on file and online payments. Off while `memberId` is empty. */
export function useMemberPaymentProfile(memberId: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: paymentKeys.profile(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getMemberPaymentProfile(memberId, { signal: s })),
    enabled: memberId.length > 0,
  });
}

/** Payments through Stripe, the latest first: all, one member's, or those in one state. */
export function useOnlinePayments(filter: { memberId?: string; status?: string } = {}, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: paymentKeys.online(ws, filter),
    queryFn: ({ signal }) => timed(signal, (s) => getOnlinePayments(filter, { signal: s })),
    enabled,
  });
}

/** The bank files read so far, the latest first. Off for someone who may not see them. */
export function useBankImports(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: paymentKeys.bankImports(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getBankImports({ signal: s })),
    enabled,
  });
}

/** The bank's transfers in one state (unmatched: waiting for staff to say whose they are). */
export function useBankTransactions(status: BankTransactionStatus | null, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: paymentKeys.bankTransactions(ws, status),
    queryFn: ({ signal }) => timed(signal, (s) => getBankTransactions(status ?? undefined, { signal: s })),
    enabled,
  });
}
