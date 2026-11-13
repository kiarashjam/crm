// Corporate memberships: every company's membership at the club, its seats and its renewal, with
// what the company owes on its house account. Each is worked on its company's page, where its
// nominees are seated, swapped and removed and its renewal and dues are kept.

import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Building2, CalendarClock, Loader2, Users } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Badge } from '@/app/components/ui/badge';
import { Label } from '@/app/components/ui/label';
import { RENEWAL_STATE_LABELS, NOMINEE_CHARGES_LABELS, type CorporateQuery } from '@/app/api/corporate';
import { useCorporateMemberships } from '@/app/hooks/queries';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import { useFormat } from '@/app/i18n';
import { fieldClass, useClubAmount } from '@/app/components/corporate/parts';

const STATUSES = [
  { value: '', label: 'Active and lapsed' },
  { value: 'Active', label: 'Active' },
  { value: 'Lapsed', label: 'Lapsed' },
  { value: 'Ended', label: 'Ended' },
] as const;

export default function CorporateMemberships() {
  useDocumentTitle('Corporate memberships');
  const f = useFormat();
  const amount = useClubAmount();
  const [status, setStatus] = useState('');
  const [renewing, setRenewing] = useState(false);
  const query = useMemo<CorporateQuery>(() => ({ status: status || undefined, renewingWithinDays: renewing ? 60 : undefined }), [status, renewing]);
  const list = useCorporateMemberships(query);
  const rows = useMemo(() => (list.data ?? []).filter((m) => status || m.status !== 'Ended'), [list.data, status]);
  const seats = rows.reduce((sum, m) => sum + m.seats, 0);
  const taken = rows.reduce((sum, m) => sum + m.seatsUsed, 0);
  const due = rows.filter((m) => m.renewalState === 'due' || m.renewalState === 'overdue').length;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Building2}
            tone="amber"
            title="Corporate memberships"
            subtitle="Memberships held by companies: seats their nominees fill, dues and nominees’ charges on the company’s own house account, and renewal on the company. Each is kept on its company’s page."
            stats={[
              { label: 'Memberships', value: rows.length, icon: Building2, tone: 'amber' },
              { label: 'Seats taken', value: taken, icon: Users, tone: 'emerald', hint: `of ${seats}` },
              { label: 'Renewal due', value: due, icon: CalendarClock, tone: 'rose' },
            ]}
          />

          <div className="mb-5 flex flex-wrap items-end gap-4">
            <div>
              <Label htmlFor="corporate-status" className="text-sm text-slate-600">Status</Label>
              <select id="corporate-status" className={fieldClass} value={status} onChange={(e) => setStatus(e.target.value)}>
                {STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" checked={renewing} onChange={(e) => setRenewing(e.target.checked)} className="h-4 w-4" />
              Renewing in the next 60 days
            </label>
            {list.isFetching && !list.isPending && <Loader2 className="h-4 w-4 animate-spin text-slate-400" aria-label="Refreshing" />}
          </div>

          {list.isError && !list.data ? (
            <LoadFailed what="Corporate memberships" error={list.error} onRetry={() => void list.refetch()} retrying={list.isFetching} />
          ) : list.isPending ? (
            <p className="flex items-center gap-2 text-slate-500" role="status"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
          ) : (
            <>
              {list.isError && <LoadFailed stale what="Corporate memberships" error={list.error} onRetry={() => void list.refetch()} retrying={list.isFetching} className="mb-3" />}
              {rows.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-slate-600 dark:border-slate-700 dark:bg-slate-800">
                  No corporate memberships here. A company’s is started on its page, under At the club.
                </div>
              ) : (
                <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-800">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 text-left text-xs uppercase tracking-wider text-slate-500 dark:bg-slate-900">
                      <tr>
                        <th scope="col" className="px-4 py-2">Company</th>
                        <th scope="col" className="px-4 py-2">Membership</th>
                        <th scope="col" className="px-4 py-2">Seats</th>
                        <th scope="col" className="px-4 py-2">Renewal</th>
                        <th scope="col" className="px-4 py-2 text-right">Owed</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows.map((m) => (
                        <tr key={m.id} className="border-t border-slate-100 dark:border-slate-700">
                          <td className="px-4 py-2">
                            <Link to={`/companies/${m.companyId}`} className="font-medium text-slate-900 hover:text-amber-800 dark:text-slate-100">{m.companyName}</Link>
                          </td>
                          <td className="px-4 py-2">
                            <span>{m.name}</span>
                            <span className="block text-xs text-slate-500">{NOMINEE_CHARGES_LABELS[m.nomineeCharges] ?? m.nomineeCharges}</span>
                          </td>
                          <td className="px-4 py-2">{m.seatsUsed} / {m.seats}</td>
                          <td className="px-4 py-2">
                            <Badge variant="outline" className={m.renewalState === 'overdue' || m.renewalState === 'not_renewing' ? 'border-rose-300 text-rose-700' : undefined}>
                              {RENEWAL_STATE_LABELS[m.renewalState] ?? m.renewalState}
                            </Badge>
                            <span className="ml-2 text-xs text-slate-500">{f.date(m.status === 'Ended' && m.endedAtUtc ? m.endedAtUtc : m.renewsAtUtc)}</span>
                          </td>
                          <td className="px-4 py-2 text-right">{m.account ? amount(m.account.owed, m.account.currency) : '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </main>
      </PageTransition>
    </div>
  );
}
