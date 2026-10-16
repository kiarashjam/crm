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

  const inPeriod = useMemo(() => {
    const inChg = memberCharges.filter((c) => {
      const t = Date.parse(c.postedAtUtc);
      return t >= periodStart && t < periodEnd;
    });
    const inPay = memberPayments.filter((p) => {
      const t = Date.parse(p.receivedAtUtc);
      return t >= periodStart && t < periodEnd;
    });
    return { charges: inChg, payments: inPay };
  }, [memberCharges, memberPayments, periodStart, periodEnd]);

  // Opening balance: net of everything before this period.
  const openingBalance = useMemo(() => {
    const priorCharges = memberCharges
      .filter((c) => Date.parse(c.postedAtUtc) < periodStart && c.status !== 'Voided')
      .reduce((s, c) => s + c.amount - c.paidAmount, 0);
    return priorCharges; // positive = owed
  }, [memberCharges, periodStart]);

  const periodCharges = inPeriod.charges.filter((c) => c.status !== 'Voided').reduce(
    (s, c) => s + c.amount,
    0,
  );
  const periodPayments = inPeriod.payments.reduce((s, p) => s + p.amount, 0);
  const closingBalance = openingBalance + periodCharges - periodPayments;

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
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-6 print:hidden">
            <div className="absolute inset-0">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-slate-400/20 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10 flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-slate-500 to-slate-700 flex items-center justify-center shadow-2xl shadow-slate-500/30">
                <FileText className="w-8 h-8 text-white" />
              </div>
              <div className="flex-1">
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Statements</h1>
                <p className="text-slate-400 mt-1">
                  Monthly billing summary — charges, payments, and ending balance.
                </p>
              </div>
              <Button
                onClick={print}
                disabled={!member}
                className="bg-white text-slate-900 hover:bg-slate-100"
              >
                <Printer className="w-4 h-4 mr-2" /> Print
              </Button>
            </div>
          </div>

          <div className="flex flex-col md:flex-row gap-3 mb-6 print:hidden">
            <Select value={memberId} onValueChange={setMemberId}>
              <SelectTrigger className="h-11 rounded-xl md:w-64">
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
              <Button variant="outline" onClick={goPrev}>
                <ChevronLeft className="w-4 h-4" />
              </Button>
              <Button variant="outline" onClick={goCurrent}>
                Current
              </Button>
              <Button variant="outline" onClick={goNext}>
                <ChevronRight className="w-4 h-4" />
              </Button>
              <span className="ml-3 text-sm font-medium text-slate-700">{monthLabel(cursor)}</span>
            </div>
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : !member ? (
            <p className="text-slate-500">Select a member to view their statement.</p>
          ) : (
            <article className="bg-white rounded-2xl border border-slate-200 shadow-sm p-8 print:shadow-none print:border-0">
              <header className="border-b border-slate-200 pb-6 mb-6 flex justify-between items-start">
                <div>
                  <h2 className="text-2xl font-bold text-slate-900">Account Statement</h2>
                  <p className="text-sm text-slate-500 mt-1">{monthLabel(cursor)}</p>
                </div>
                <div className="text-right">
                  <p className="font-semibold text-slate-900">
                    {member.firstName} {member.lastName}
                  </p>
                  <p className="text-sm text-slate-500">{member.email}</p>
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
                      {inPeriod.charges.map((c) => (
                        <tr key={c.id} className="border-b border-slate-100">
                          <td className="py-2 text-slate-500">{formatDate(c.postedAtUtc)}</td>
                          <td className="py-2 text-slate-800">{c.description}</td>
                          <td className="py-2 text-slate-500">{c.kind}</td>
                          <td className="py-2 text-right font-medium">{formatCurrency(c.amount)}</td>
                        </tr>
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
                      {inPeriod.payments.map((p) => (
                        <tr key={p.id} className="border-b border-slate-100">
                          <td className="py-2 text-slate-500">{formatDate(p.receivedAtUtc)}</td>
                          <td className="py-2 text-slate-800">{PAYMENT_METHOD_LABELS[p.method]}</td>
                          <td className="py-2 text-slate-500 font-mono text-xs">{p.reference ?? '—'}</td>
                          <td className="py-2 text-right font-medium text-emerald-700">
                            -{formatCurrency(p.amount)}
                          </td>
                        </tr>
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
                <div className="flex items-center justify-between">
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
            </article>
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
