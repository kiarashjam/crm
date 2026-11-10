// Member statements — printable per-member monthly billing summaries.
//
// The statement is the server's: worked out from the member's ledger entries, so it shows
// what the books show — the balance brought forward, each charge, payment, credit, void
// and refund in the order it happened with what was owed after it, and the balance
// carried. It used to be added up here from the charges and payments lists, which left out
// opening balances, corrections and refunds.

import { useState } from 'react';
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
import type { StatementLine } from '@/app/api/charges';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { ClubReadState } from '@/app/components/ClubReadState';
import { NONE, useClubMembers, useClubReads, useMemberStatement } from '@/app/hooks/queries';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { StaggerItem } from '@/app/components/motion/Stagger';
import { useClubMoney } from '@/app/hooks/useClubMoney';

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

/** What an entry is, when its description alone does not say: a void, a refund, a credit. */
const ENTRY_LABEL: Record<string, string> = {
  Opening: 'Balance brought over',
  Adjustment: 'Correction',
  Void: 'Void',
  Refund: 'Refund',
};

// The server's balances are the house account's: negative while the member owes. A
// statement says what is owed, so it shows them the other way round.
const owed = (balance: number) => (balance === 0 ? 0 : -balance);

export default function Statements() {
  const { format: formatCurrency } = useClubMoney();
  const membersQuery = useClubMembers();
  const members = membersQuery.data ?? NONE;
  const [picked, setPicked] = useState<string>('');
  const [cursor, setCursor] = useState<Date>(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1);
  });

  // The first member until someone picks another.
  const member = members.find((m) => m.id === picked) ?? members[0] ?? null;
  const memberId = member?.id ?? '';
  const fromUtc = new Date(startOfMonth(cursor)).toISOString();
  const toUtc = new Date(endOfMonth(cursor)).toISOString();
  const statementQuery = useMemberStatement(member?.id ?? null, fromUtc, toUtc);
  const reads = useClubReads(membersQuery, statementQuery);
  usePageLoading(reads.loading);
  const statement = statementQuery.data;

  const openingBalance = owed(statement?.openingBalance ?? 0);
  const closingBalance = owed(statement?.closingBalance ?? 0);
  const periodCharges = statement?.totalDebits ?? 0;
  const periodCredits = statement?.totalCredits ?? 0;
  const lines: StatementLine[] = statement?.lines ?? NONE;

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
                <Button onClick={print} disabled={!statement} className={heroPrimary('slate')}>
                  <Printer className="h-4 w-4" /> Print
                </Button>
              }
            >
              <div className="flex flex-col md:flex-row md:items-center gap-3">
                <Select value={memberId} onValueChange={setPicked}>
                  <SelectTrigger aria-label="Member" className="h-9 rounded-xl border-slate-200/80 bg-white/80 text-slate-700 shadow-sm backdrop-blur hover:border-slate-300 hover:bg-white md:w-64">
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
                  <Button variant="outline" onClick={goPrev} className={heroSecondary} aria-label="Previous month">
                    <ChevronLeft className="w-4 h-4" />
                  </Button>
                  <Button variant="outline" onClick={goCurrent} className={heroSecondary}>
                    Current
                  </Button>
                  <Button variant="outline" onClick={goNext} className={heroSecondary} aria-label="Next month">
                    <ChevronRight className="w-4 h-4" />
                  </Button>
                  <span className="ml-3 text-sm font-medium text-slate-700">{monthLabel(cursor)}</span>
                </div>
              </div>
            </PageHero>
          </div>

          <ClubReadState reads={reads} what="The statement">
          {reads.loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : !member || !statement ? (
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
                <Box label="Payments and credits" value={formatCurrency(periodCredits)} tone="emerald" />
                <Box label="Closing balance" value={formatCurrency(closingBalance)} tone={closingBalance > 0 ? 'rose' : 'slate'} />
              </section>

              <section className="mb-8">
                <h3 className="font-semibold text-slate-900 mb-3">Activity</h3>
                {lines.length === 0 ? (
                  <p className="text-sm text-slate-500">Nothing charged or paid this period.</p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="text-left text-xs uppercase tracking-wide text-slate-500 border-b border-slate-200">
                          <th className="py-2 pr-3">Date</th>
                          <th className="py-2 pr-3">Description</th>
                          <th className="py-2 pr-3 text-right">Charges</th>
                          <th className="py-2 pr-3 text-right">Payments</th>
                          <th className="py-2 text-right">Owed</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr className="border-b border-slate-100">
                          <td className="py-2 pr-3 text-slate-500">{formatDate(statement.fromUtc)}</td>
                          <td className="py-2 pr-3 text-slate-500" colSpan={3}>Balance brought forward</td>
                          <td className="py-2 text-right font-medium">{formatCurrency(openingBalance)}</td>
                        </tr>
                        {lines.map((line, index) => (
                          <StaggerItem as="tr" key={`${line.id}-${index}`} index={index} className="border-b border-slate-100 transition-colors hover:bg-slate-50">
                            <td className="py-2 pr-3 text-slate-500 whitespace-nowrap">{formatDate(line.atUtc)}</td>
                            <td className="py-2 pr-3 text-slate-800">
                              <span className="block">{line.description}</span>
                              {(ENTRY_LABEL[line.entryType] || line.reason || line.reference) && (
                                <span className="block text-xs text-slate-500">
                                  {[ENTRY_LABEL[line.entryType], line.reason, line.reference].filter(Boolean).join(' · ')}
                                </span>
                              )}
                            </td>
                            <td className="py-2 pr-3 text-right font-medium">{line.debit ? formatCurrency(line.debit) : ''}</td>
                            <td className="py-2 pr-3 text-right font-medium text-emerald-700">{line.credit ? `-${formatCurrency(line.credit)}` : ''}</td>
                            <td className="py-2 text-right text-slate-700">{formatCurrency(owed(line.balance))}</td>
                          </StaggerItem>
                        ))}
                        <tr>
                          <td colSpan={2} className="py-2 pr-3 text-right text-xs uppercase tracking-wide text-slate-500">
                            Total
                          </td>
                          <td className="py-2 pr-3 text-right font-semibold">{formatCurrency(periodCharges)}</td>
                          <td className="py-2 pr-3 text-right font-semibold text-emerald-700">-{formatCurrency(periodCredits)}</td>
                          <td />
                        </tr>
                      </tbody>
                    </table>
                  </div>
                )}
              </section>

              <footer className="border-t border-slate-200 pt-6">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-sm text-slate-500">
                    Questions about this statement? Please contact the membership office.
                  </p>
                  <div className="text-right">
                    <p className="text-xs uppercase tracking-wide text-slate-500">Closing balance</p>
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
          </ClubReadState>
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
