// Payments through Stripe across the club, the latest first: what each was for, whose,
// how it was paid, and where it stands. A failure stays listed with Stripe's reason, so a
// declined card on dues is seen by staff and not only by the member.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, CreditCard, Loader2 } from 'lucide-react';
import { LoadFailed } from '@/app/components/LoadFailed';
import EmptyState from '@/app/components/EmptyState';
import type { OnlinePaymentStatus, PaymentStatus } from '@/app/api/payments';
import { useOnlinePayments } from '@/app/hooks/queries';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { METHOD_LABEL, ONLINE_STATUS_BADGE, ONLINE_STATUS_LABEL, PURPOSE_LABEL } from './paymentLabels';
import { currentDateLocale } from '@/app/i18n/current';

const FILTERS: { value: OnlinePaymentStatus | ''; label: string }[] = [
  { value: '', label: 'All' },
  { value: 'failed', label: 'Failed' },
  { value: 'processing', label: 'Processing' },
  { value: 'open', label: 'Waiting for the member' },
  { value: 'succeeded', label: 'Paid' },
];

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(currentDateLocale(), { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

export function OnlinePaymentsPanel({ status }: { status?: PaymentStatus }) {
  const { locale } = useRegional();
  const [filter, setFilter] = useState<OnlinePaymentStatus | ''>('');
  const query = useOnlinePayments(filter ? { status: filter } : {});
  const payments = query.data ?? [];

  return (
    <div className="space-y-4">
      {status && !status.onlinePayments && (
        <p className="inline-flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Online payments are not set up: members cannot pay online.{' '}
            <Link to="/hospitality-settings?tab=payments" className="font-medium underline">Set up Stripe in Club settings, Payments</Link>.
          </span>
        </p>
      )}
      {status?.onlinePayments && !status.webhookConfigured && (
        <p className="inline-flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Stripe&apos;s word that a payment went through is not heard: the webhook signing secret is not set, so payments stay
            processing and never reach the ledger.{' '}
            <Link to="/hospitality-settings?tab=payments" className="font-medium underline">Set it in Club settings, Payments</Link>.
          </span>
        </p>
      )}
      {status?.onlinePayments && status.webhookConfigured && (
        <p className="inline-flex items-start gap-2 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Members can pay online ({status.methods.map((m) => METHOD_LABEL[m] ?? m).join(', ')})
            {status.mode === 'test' ? ', in Stripe test mode: no real money moves.' : '.'}
          </span>
        </p>
      )}

      <div className="flex flex-wrap gap-2" role="group" aria-label="Show">
        {FILTERS.map((f) => (
          <button
            key={f.value || 'all'}
            type="button"
            onClick={() => setFilter(f.value)}
            aria-pressed={filter === f.value}
            className={`rounded-full px-3 py-1 text-sm font-medium ring-1 ${filter === f.value ? 'bg-slate-900 text-white ring-slate-900' : 'bg-white text-slate-700 ring-slate-200 hover:ring-slate-300'}`}
          >
            {f.label}
          </button>
        ))}
      </div>

      {query.isError && !query.data ? (
        <LoadFailed what="Online payments" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} />
      ) : query.isPending ? (
        <div className="flex items-center justify-center py-12 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" /></div>
      ) : payments.length === 0 ? (
        <EmptyState
          icon={CreditCard}
          title={filter ? 'None in this state' : 'No online payments yet'}
          description={filter ? 'Nothing listed here is in this state.' : 'A payment shows here once a member opens a pay link, or a saved card is charged.'}
        />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50">
              <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                <th className="px-4 py-3">When</th>
                <th className="px-4 py-3">Member</th>
                <th className="px-4 py-3">What</th>
                <th className="px-4 py-3">Paid with</th>
                <th className="px-4 py-3 text-right">Amount</th>
                <th className="px-4 py-3">Status</th>
              </tr>
            </thead>
            <tbody>
              {payments.map((p) => (
                <tr key={p.id} className="border-b border-slate-100 align-top">
                  <td className="whitespace-nowrap px-4 py-3 text-slate-500">{formatDateTime(p.createdAtUtc)}</td>
                  <td className="px-4 py-3">
                    <Link to={`/members/${p.memberId}`} className="font-medium text-slate-800 hover:underline">{p.memberName}</Link>
                  </td>
                  <td className="px-4 py-3">
                    <span className="block text-slate-800">{PURPOSE_LABEL[p.purpose] ?? p.purpose}</span>
                    <span className="block text-xs text-slate-500">{p.description}</span>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{p.methodType ? METHOD_LABEL[p.methodType] ?? p.methodType : '—'}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-right font-semibold">
                    {p.purpose === 'setup' ? '—' : formatClubMoney(p.amount, p.currency, locale)}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${ONLINE_STATUS_BADGE[p.status] ?? ''}`}>
                      {ONLINE_STATUS_LABEL[p.status] ?? p.status}
                    </span>
                    {p.status === 'failed' && (p.failureMessage || p.failureCode) && (
                      <span className="mt-1 block text-xs text-rose-700">{p.failureMessage ?? p.failureCode}</span>
                    )}
                    {p.status === 'succeeded' && p.ledgerPaymentId && (
                      <span className="mt-1 block text-xs text-slate-500">On the member&apos;s account</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
