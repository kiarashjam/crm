// Member statements — printable per-member monthly billing summaries.

import { useEffect, useMemo, useState } from 'react';
import {
  FileText,
  Printer,
  Loader2,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { getMembers, type Member } from '@/app/api/members';
import { getCharges, getPayments, type Charge, type Payment, PAYMENT_METHOD_LABELS } from '@/app/api/charges';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { buildStatement } from '@/app/lib/statement';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { StaggerItem } from '@/app/components/motion/Stagger';

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}

function monthLabel(d: Date): string {
  return d.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
}

function startOfMonth(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), 1).getTime();
}
function endOfMonth(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth() + 1, 1).getTime();
}

export default function Statements() {
  const [members, setMembers] = useState<Member[]>([]);
  const [charges, setCharges] = useState<Charge[]>([]);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [memberId, setMemberId] = useState<string>('');
  const [cursor, setCursor] = useState<Date>(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1);
  });

  const load = async () => {
    try {
      const [m, c, p] = await Promise.all([getMembers(), getCharges(), getPayments()]);
      setMembers(m);
      setCharges(c);
      setPayments(p);
      if (!memberId && m[0]) setMemberId(m[0].id);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const member = useMemo(
    () => members.find((m) => m.id === memberId) ?? null,
    [members, memberId],
  );

  const periodStart = startOfMonth(cursor);
  const periodEnd = endOfMonth(cursor);

  const memberCharges = useMemo(() => charges.filter((c) => c.memberId === memberId), [charges, memberId]);
  const memberPayments = useMemo(
    () => payments.filter((p) => p.memberId === memberId),
    [payments, memberId],
  );

  const statement = useMemo(
    () => buildStatement(memberCharges, memberPayments, periodStart, periodEnd),
    [memberCharges, memberPayments, periodStart, periodEnd],
  );
  const inPeriod = { charges: statement.charges, payments: statement.payments };
  const { openingBalance, periodCharges, periodPayments, closingBalance } = statement;

  const goPrev = () => setCursor(new Date(cursor.getFullYear(), cursor.getMonth() - 1, 1));
  const goNext = () => setCursor(new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1));
  const goCurrent = () => {
    const now = new Date();
    setCursor(new Date(now.getFullYear(), now.getMonth(), 1));
  };

  const print = () => window.print();

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <div className="print:hidden">
            <PageHero
              icon={FileText}
              tone="slate"
              title="Statements"
              subtitle="Monthly billing summary — charges, payments, and ending balance."
              actions={
                <Button onClick={print} disabled={!member} className={heroPrimary('slate')}>
                  <Printer className="h-4 w-4" /> Print
                </Button>
              }
            >
              <div className="flex flex-col md:flex-row md:items-center gap-3">
                <Select value={memberId} onValueChange={setMemberId}>
                  <SelectTrigger className="h-9 rounded-xl border-slate-200/80 bg-white/80 text-slate-700 shadow-sm backdrop-blur hover:border-slate-300 hover:bg-white md:w-64">
                    <SelectValue placeholder="Pick a member" />
                  </SelectTrigger>
                  <SelectContent>
                    {members.map((m) => (
                      <SelectItem key={m.id} value={m.id}>
                        {m.firstName} {m.lastName} — {m.tier}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <div className="flex items-center gap-1.5">
                  <Button variant="outline" onClick={goPrev} className={heroSecondary}>
                    <ChevronLeft className="w-4 h-4" />
                  </Button>
                  <Button variant="outline" onClick={goCurrent} className={heroSecondary}>
                    Current
                  </Button>
                  <Button variant="outline" onClick={goNext} className={heroSecondary}>
                    <ChevronRight className="w-4 h-4" />
                  </Button>
                  <span className="ml-3 text-sm font-medium text-slate-700">{monthLabel(cursor)}</span>
                </div>
              </div>
            </PageHero>
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : !member ? (
            <p className="text-slate-500">Select a member to view their statement.</p>
          ) : (
            <StaggerItem as="article" className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5 sm:p-8 print:shadow-none print:border-0">
              <header className="border-b border-slate-200 pb-6 mb-6 flex flex-wrap justify-between items-start gap-4">
                <div>
                  <h2 className="text-2xl font-bold text-slate-900">Account Statement</h2>
                  <p className="text-sm text-slate-500 mt-1">{monthLabel(cursor)}</p>
                </div>
                <div className="min-w-0 sm:text-right">
                  <p className="font-semibold text-slate-900">
                    {member.firstName} {member.lastName}
                  </p>
                  <p className="text-sm text-slate-500 break-all">{member.email}</p>
                  <p className="text-sm text-slate-500 mt-1">
                    {member.tier} · Member since {formatDate(member.joinedAtUtc)}
                  </p>
                </div>
              </header>

              <section className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
                <Box label="Opening balance" value={formatCurrency(openingBalance)} />
                <Box label="Charges this period" value={formatCurrency(periodCharges)} />
                <Box label="Payments this period" value={formatCurrency(periodPayments)} tone="emerald" />
                <Box label="Closing balance" value={formatCurrency(closingBalance)} tone={closingBalance > 0 ? 'rose' : 'slate'} />
              </section>

              <section className="mb-8">
                <h3 className="font-semibold text-slate-900 mb-3">Charges</h3>
                {inPeriod.charges.length === 0 ? (
                  <p className="text-sm text-slate-500">No charges this period.</p>
                ) : (
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-slate-200">
                        <th className="py-2">Date</th>
                        <th className="py-2">Description</th>
                        <th className="py-2">Kind</th>
                        <th className="py-2 text-right">Amount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {inPeriod.charges.map((c, index) => (
                        <StaggerItem as="tr" key={c.id} index={index} className="border-b border-slate-100 transition-colors hover:bg-slate-50">
                          <td className="py-2 text-slate-500">{formatDate(c.postedAtUtc)}</td>
                          <td className="py-2 text-slate-800">{c.description}</td>
                          <td className="py-2 text-slate-500">{c.kind}</td>
                          <td className="py-2 text-right font-medium">{formatCurrency(c.amount)}</td>
                        </StaggerItem>
                      ))}
                      <tr>
                        <td colSpan={3} className="py-2 text-right text-xs uppercase tracking-wide text-slate-400">
                          Total
                        </td>
                        <td className="py-2 text-right font-semibold">{formatCurrency(periodCharges)}</td>
                      </tr>
                    </tbody>
                  </table>
                )}
              </section>

              <section className="mb-8">
                <h3 className="font-semibold text-slate-900 mb-3">Payments</h3>
                {inPeriod.payments.length === 0 ? (
                  <p className="text-sm text-slate-500">No payments this period.</p>
                ) : (
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-slate-200">
                        <th className="py-2">Date</th>
                        <th className="py-2">Method</th>
                        <th className="py-2">Reference</th>
                        <th className="py-2 text-right">Amount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {inPeriod.payments.map((p, index) => (
                        <StaggerItem as="tr" key={p.id} index={index} className="border-b border-slate-100 transition-colors hover:bg-slate-50">
                          <td className="py-2 text-slate-500">{formatDate(p.receivedAtUtc)}</td>
                          <td className="py-2 text-slate-800">{PAYMENT_METHOD_LABELS[p.method]}</td>
                          <td className="py-2 text-slate-500 font-mono text-xs">{p.reference ?? '—'}</td>
                          <td className="py-2 text-right font-medium text-emerald-700">
                            -{formatCurrency(p.amount)}
                          </td>
                        </StaggerItem>
                      ))}
                      <tr>
                        <td colSpan={3} className="py-2 text-right text-xs uppercase tracking-wide text-slate-400">
                          Total
                        </td>
                        <td className="py-2 text-right font-semibold text-emerald-700">
                          -{formatCurrency(periodPayments)}
                        </td>
                      </tr>
                    </tbody>
                  </table>
                )}
              </section>

              <footer className="border-t border-slate-200 pt-6">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-sm text-slate-500">
                    Questions about this statement? Please contact the membership office.
                  </p>
                  <div className="text-right">
                    <p className="text-xs uppercase tracking-wide text-slate-400">Closing balance</p>
                    <p
                      className={`text-2xl font-bold ${
                        closingBalance > 0 ? 'text-rose-700' : 'text-slate-900'
                      }`}
                    >
                      {formatCurrency(closingBalance)}
                    </p>
                  </div>
                </div>
              </footer>
            </StaggerItem>
          )}
        </main>
      </PageTransition>
    </div>
  );
}

function Box({ label, value, tone }: { label: string; value: string; tone?: 'rose' | 'emerald' | 'slate' }) {
  const c = tone === 'rose' ? 'text-rose-700' : tone === 'emerald' ? 'text-emerald-700' : 'text-slate-900';
  return (
    <div className="bg-slate-50 rounded-xl p-4">
      <p className="text-xs uppercase tracking-wide text-slate-500">{label}</p>
      <p className={`text-xl font-bold mt-1 ${c}`}>{value}</p>
    </div>
  );
}
