// The renewals board: whose membership or yearly subscription ends in the window, in a column
// for where it stands. The server works each renewal's stage out from the records (the date
// moved on, the period billed and paid, someone in touch, the at-risk score) and keeps what
// staff set by hand; this page shows it and lets club.members move a renewal.
//
// Renewal dates sat on each member and nothing looked at them: nobody saw who was coming up,
// who had been spoken to, or who quietly did not come back.

import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { AlertTriangle, CalendarClock, CheckCircle2, Loader2, MessageSquare, RefreshCw, UserRound, XCircle } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { Switch } from '@/app/components/ui/switch';
import { Label } from '@/app/components/ui/label';
import { PERMISSION } from '@/app/api/organizations';
import {
  amountIn, setRenewalStage, RISK_LEVEL_LABELS, type RenewalCard, type RenewalStage, type RenewalStageChoice,
} from '@/app/api/retention';
import { useRenewalBoard } from '@/app/hooks/queries';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { useMay } from '@/app/hooks/useMay';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { isEditConflict, showEditConflict } from '@/app/lib/editConflict';
import { useFormat } from '@/app/i18n';
import { cn } from '@/app/components/ui/utils';

const COLUMN_STYLE: Record<RenewalStage, { bar: string; chip: string }> = {
  upcoming: { bar: 'bg-slate-400', chip: 'bg-slate-100 text-slate-700' },
  contacted: { bar: 'bg-sky-500', chip: 'bg-sky-50 text-sky-800' },
  at_risk: { bar: 'bg-rose-500', chip: 'bg-rose-50 text-rose-800' },
  renewed: { bar: 'bg-emerald-500', chip: 'bg-emerald-50 text-emerald-800' },
  lapsed: { bar: 'bg-amber-500', chip: 'bg-amber-50 text-amber-800' },
};

const CONTACT_KIND: Record<NonNullable<RenewalCard['lastContactKind']>, string> = {
  message: 'written to',
  activity: 'call or meeting',
  marked: 'marked contacted',
};

/** The moves a card offers, each to a stage other than the one it is in. */
const MOVES: { stage: RenewalStageChoice; label: string; icon: typeof CheckCircle2 }[] = [
  { stage: 'contacted', label: 'Contacted', icon: MessageSquare },
  { stage: 'at_risk', label: 'At risk', icon: AlertTriangle },
  { stage: 'renewed', label: 'Renewed', icon: CheckCircle2 },
  { stage: 'lapsed', label: 'Not renewing', icon: XCircle },
];

const WINDOWS = [30, 60, 90, 180, 365];

/**
 * A member who has left the club: the server refuses to mark their renewal renewed (their card
 * stays lapsed while they are), so the move is not offered; they are set active on their page.
 */
const LEFT_STATUSES = ['Lapsed', 'Cancelled', 'Resigned'];

export default function Renewals() {
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const may = useMay();
  const f = useFormat();
  const { locale } = useRegional();
  const [days, setDays] = useState(60);
  const [mine, setMine] = useState(false);
  const [monthly, setMonthly] = useState(false);
  const query = useMemo(() => ({ days, owner: mine ? 'me' : undefined, monthly: monthly || undefined }), [days, mine, monthly]);
  const board = useRenewalBoard(query);
  const canMove = may(PERMISSION.ClubMembers);

  const move = useMutation({
    mutationFn: ({ card, stage }: { card: RenewalCard; stage: RenewalStageChoice }) => setRenewalStage(card, { stage }),
    onSuccess: (saved, { stage }) => {
      void client.invalidateQueries({ queryKey: queryKeys.renewals.board(scope, query).slice(0, 4) });
      toast.success(stage === 'renewed' && saved.renewedUntilUtc
        ? `${saved.name} is renewed until ${f.date(saved.renewedUntilUtc)}.`
        : `${saved.name}: ${saved.stageReason || 'saved'}.`);
    },
    onError: (err) => {
      if (isEditConflict(err)) {
        showEditConflict(err.message, () => { void board.refetch(); });
        return;
      }
      toast.error(err instanceof Error && err.message ? err.message : 'The renewal could not be moved.');
    },
  });

  const data = board.data;
  const money = (amount: number, currency: string) => formatClubMoney(amount, currency, locale);
  const currency = data?.currency ?? 'CHF';
  const count = (stage: RenewalStage) => data?.columns.find((c) => c.stage === stage)?.count ?? 0;
  const open = data ? data.columns.filter((c) => c.stage !== 'renewed' && c.stage !== 'lapsed') : [];
  const atStake = open.reduce((sum, c) => sum + amountIn(c.totals, currency), 0);

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={RefreshCw}
            tone="amber"
            title="Renewals"
            subtitle="Whose membership or yearly dues end soon, and where each renewal stands: worked out from renewal dates, payments, contacts and at-risk scores, and moved by hand when you know better."
            stats={[
              { label: 'Coming up', value: count('upcoming') + count('contacted') + count('at_risk'), icon: CalendarClock, tone: 'amber', hint: `next ${days} days` },
              { label: 'At risk', value: count('at_risk'), icon: AlertTriangle, tone: 'rose' },
              { label: 'Renewed', value: count('renewed'), icon: CheckCircle2, tone: 'emerald' },
              { label: 'A year at stake', value: atStake, icon: RefreshCw, tone: 'violet', format: (n) => money(n, currency), hint: 'not renewed yet' },
            ]}
          />

          <div className="mb-5 flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-2">
              <Label htmlFor="renewals-window" className="text-sm text-slate-600">Ending in the next</Label>
              <Select value={String(days)} onValueChange={(v) => setDays(Number(v))}>
                <SelectTrigger id="renewals-window" className="h-9 w-32"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {WINDOWS.map((d) => <SelectItem key={d} value={String(d)}>{d} days</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center gap-2">
              <Switch id="renewals-mine" checked={mine} onCheckedChange={setMine} />
              <Label htmlFor="renewals-mine" className="text-sm text-slate-600">Only members I look after</Label>
            </div>
            <div className="flex items-center gap-2">
              <Switch id="renewals-monthly" checked={monthly} onCheckedChange={setMonthly} />
              <Label htmlFor="renewals-monthly" className="text-sm text-slate-600">Monthly dues too</Label>
            </div>
            {board.isFetching && !board.isLoading && <Loader2 className="h-4 w-4 animate-spin text-slate-400" aria-label="Refreshing" />}
          </div>

          {board.isError && (
            <LoadFailed what="The renewals board" error={board.error} onRetry={() => { void board.refetch(); }} retrying={board.isFetching} stale={!!data} />
          )}
          {board.isLoading ? (
            <div className="flex items-center justify-center py-20 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" aria-hidden /></div>
          ) : data ? (
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
              {data.columns.map((column) => (
                <section key={column.stage} aria-label={column.label} className="flex min-w-0 flex-col rounded-2xl border border-slate-200 bg-white/80 shadow-sm">
                  <header className="border-b border-slate-100 p-3">
                    <div className={cn('mb-2 h-1 w-10 rounded-full', COLUMN_STYLE[column.stage].bar)} aria-hidden />
                    <h2 className="flex items-center justify-between font-semibold text-slate-900">
                      <span>{column.label}</span>
                      <span className={cn('rounded-full px-2 py-0.5 text-xs font-semibold', COLUMN_STYLE[column.stage].chip)}>{column.count}</span>
                    </h2>
                    <p className="mt-0.5 text-xs text-slate-500">
                      {column.totals.length === 0 ? 'Nothing a year' : column.totals.map((t) => money(t.amount, t.currency)).join(' + ')}
                      {column.totals.length > 0 && ' a year'}
                    </p>
                  </header>
                  <ul className="flex flex-col gap-2 p-2">
                    {column.cards.length === 0 && <li className="px-2 py-6 text-center text-xs text-slate-400">None</li>}
                    {column.cards.map((card) => (
                      <li key={`${card.memberId}-${card.source}-${card.subscriptionId ?? ''}-${card.endsAtUtc}`}
                        className="rounded-xl border border-slate-200 bg-white p-3 text-sm shadow-xs">
                        <div className="flex items-start justify-between gap-2">
                          <Link to={`/members/${card.memberId}`} className="font-semibold text-slate-900 hover:underline">{card.name}</Link>
                          {card.riskScore !== undefined && (
                            <span
                              className={cn('shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold',
                                card.riskLevel === 'at_risk' ? 'bg-rose-50 text-rose-700' : card.riskLevel === 'watch' ? 'bg-amber-50 text-amber-700' : 'bg-slate-100 text-slate-600')}
                              title={`At-risk score: ${card.riskLevel ? RISK_LEVEL_LABELS[card.riskLevel] : ''}`}
                            >
                              {card.riskScore}
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-slate-500">
                          {card.tier}{card.source === 'subscription' && card.subscriptionName ? ` · ${card.subscriptionName}` : ' · membership'}
                        </p>
                        <dl className="mt-2 space-y-1 text-xs">
                          <div className="flex justify-between gap-2">
                            <dt className="text-slate-500">Ends</dt>
                            <dd className={cn('text-right font-medium', card.daysLeft < 0 ? 'text-rose-700' : 'text-slate-800')}>
                              {f.date(card.endsAtUtc)}{' '}
                              <span className="font-normal text-slate-500">
                                ({card.daysLeft < 0 ? `${-card.daysLeft} days ago` : card.daysLeft === 0 ? 'today' : `in ${card.daysLeft} days`})
                              </span>
                            </dd>
                          </div>
                          <div className="flex justify-between gap-2">
                            <dt className="text-slate-500">A year</dt>
                            <dd className="font-medium text-slate-800">{money(card.annualValue, card.currency)}</dd>
                          </div>
                          <div className="flex justify-between gap-2">
                            <dt className="text-slate-500">Last contact</dt>
                            <dd className="text-right text-slate-800">
                              {card.lastContactAtUtc ? `${f.date(card.lastContactAtUtc)} (${CONTACT_KIND[card.lastContactKind ?? 'message']})` : 'None'}
                            </dd>
                          </div>
                          <div className="flex justify-between gap-2">
                            <dt className="inline-flex items-center gap-1 text-slate-500"><UserRound className="h-3 w-3" aria-hidden /> Looked after by</dt>
                            <dd className="text-right text-slate-800">{card.ownerName ?? 'Nobody named'}</dd>
                          </div>
                        </dl>
                        <p className="mt-2 text-xs italic text-slate-600">{card.stageReason}</p>
                        {card.note && <p className="mt-1 text-xs text-slate-700">“{card.note}”</p>}
                        {canMove && (
                          <div className="mt-2 flex flex-wrap gap-1">
                            {MOVES.filter((m) => m.stage !== card.stage && !(m.stage === 'renewed' && LEFT_STATUSES.includes(card.memberStatus))).map((m) => (
                              <Button key={m.stage} type="button" size="sm" variant="outline" className="h-7 px-2 text-[11px]"
                                disabled={move.isPending}
                                onClick={() => {
                                  if (m.stage === 'renewed' && card.source === 'membership'
                                    && !window.confirm(`Mark ${card.name}'s membership renewed? Their renewal date moves on a year from ${f.date(card.endsAtUtc)}.`)) return;
                                  move.mutate({ card, stage: m.stage });
                                }}>
                                <m.icon className="h-3 w-3" aria-hidden /> {m.label}
                              </Button>
                            ))}
                            {card.stageSetByHand && (
                              <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-[11px]" disabled={move.isPending}
                                onClick={() => move.mutate({ card, stage: 'auto' })}>
                                Let the records decide
                              </Button>
                            )}
                          </div>
                        )}
                      </li>
                    ))}
                  </ul>
                </section>
              ))}
            </div>
          ) : null}
        </main>
      </PageTransition>
    </div>
  );
}
