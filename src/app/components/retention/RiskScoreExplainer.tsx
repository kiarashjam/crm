// "How is this score calculated?": the at-risk score explained where it is shown, on the
// renewals board and a member's risk card. The six signs in plain words with the club's current
// weights as shares of 100, where "watch" and "at risk" start, the windows each sign is read
// over, that each is measured against the member's own past, that the club sets the weights,
// and that the score needs a few months of club life before it means much.
//
// The weights are the club's retention settings (GET /api/retention/settings), which everyone
// who can see a score may read; only those who manage the settings change them. The rule itself
// is the server's (RiskScoring.cs): the windows below are its constants.

import { useId, useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronDown, HelpCircle, Loader2 } from 'lucide-react';
import type { RetentionSettings } from '@/app/api/retention';
import { LoadFailed } from '@/app/components/LoadFailed';
import { cn } from '@/app/components/ui/utils';
import { useRetentionSettings } from '@/app/hooks/queries';

/** Where the club changes the weights: Club settings, Retention & referrals. */
export const RETENTION_SETTINGS_PATH = '/hospitality-settings?tab=retention';

/** The six signs, in the order the server weighs them, each in plain words. */
const SIGNS: { key: keyof RetentionSettings; label: string; text: string }[] = [
  {
    key: 'visitWeight', label: 'Visits',
    text: 'How often they checked in over the last 90 days, against the 270 days before: coming less than they used to.',
  },
  {
    key: 'balanceWeight', label: 'Unpaid balance',
    text: 'What they owe past its due date and the payment reminders it took. Anything overdue counts a little; two months past due, or three reminders, is the whole of this sign.',
  },
  {
    key: 'spendWeight', label: 'Spending',
    text: 'What they spent at the club over the last 90 days (dues left out), against the 270 days before.',
  },
  {
    key: 'concernWeight', label: 'Complaints and concerns',
    text: 'Complaints and concerns recorded about them in the last year and not yet resolved; one resolved in the last 90 days still counts a little.',
  },
  {
    key: 'eventWeight', label: 'Events',
    text: 'Events they came to in the last 6 months, against the 6 months before, and events they had a seat at and missed.',
  },
  {
    key: 'tenureWeight', label: 'Length of membership',
    text: 'Members leave most in their first two years, above all at their first renewal: this sign is strongest for a new member and gone at two years.',
  },
];

/**
 * Each weight as its share of 100, rounded so the shares add up to exactly 100 (the largest
 * remainders get the spare points); all zero when every weight is.
 */
export function sharesOf100(weights: readonly number[]): number[] {
  const clean = weights.map((w) => (Number.isFinite(w) && w > 0 ? w : 0));
  const total = clean.reduce((sum, w) => sum + w, 0);
  if (total <= 0) return clean.map(() => 0);
  const exact = clean.map((w) => (w * 100) / total);
  const shares = exact.map((e) => Math.floor(e));
  let spare = 100 - shares.reduce((sum, s) => sum + s, 0);
  const byRemainder = exact.map((e, i) => ({ i, rest: e - Math.floor(e) })).sort((a, b) => b.rest - a.rest || a.i - b.i);
  for (const { i } of byRemainder) {
    if (spare <= 0) break;
    shares[i] = (shares[i] ?? 0) + 1;
    spare -= 1;
  }
  return shares;
}

/** The signs with their shares, the heaviest first (the server's order among equals). */
export function weightedSigns(settings: RetentionSettings) {
  const shares = sharesOf100(SIGNS.map((s) => settings[s.key] as number));
  return SIGNS.map((sign, i) => ({ ...sign, share: shares[i]! }))
    .map((sign, order) => ({ sign, order }))
    .sort((a, b) => b.sign.share - a.sign.share || a.order - b.order)
    .map(({ sign }) => sign);
}

export function RiskScoreExplainer({ className }: { className?: string }) {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  const settings = useRetentionSettings(open);

  return (
    <div className={cn('rounded-xl border border-slate-200 bg-white/80', className)}>
      <button
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((was) => !was)}
        className="flex w-full items-center justify-between gap-2 rounded-xl px-3 py-2 text-left text-sm font-medium text-slate-700 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500"
      >
        <span className="inline-flex items-center gap-2">
          <HelpCircle className="h-4 w-4 text-slate-500" aria-hidden="true" />
          How is this score calculated?
        </span>
        <ChevronDown className={cn('h-4 w-4 text-slate-400 transition-transform', open && 'rotate-180')} aria-hidden="true" />
      </button>
      {open && (
        <div id={panelId} className="border-t border-slate-100 px-4 pb-4 pt-3 text-sm text-slate-700">
          {settings.isError && !settings.data ? (
            <LoadFailed what="How the score is weighed" error={settings.error} onRetry={() => void settings.refetch()} retrying={settings.isFetching} />
          ) : !settings.data ? (
            <p className="flex items-center gap-2 text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
          ) : (
            <Explanation settings={settings.data} />
          )}
        </div>
      )}
    </div>
  );
}

function Explanation({ settings }: { settings: RetentionSettings }) {
  const signs = weightedSigns(settings);
  const allZero = signs.every((s) => s.share === 0);
  return (
    <div className="space-y-3">
      <p>
        Every morning each active member gets a score from 0 to 100: how strongly six signs in the club&apos;s own records say they
        may be drifting away, each counted by the share of 100 the club gives it. A member is <strong>worth watching</strong> from{' '}
        <strong>{settings.watchThreshold}</strong> and <strong>at risk</strong> from <strong>{settings.atRiskThreshold}</strong>.
      </p>
      {allZero ? (
        <p className="text-amber-800">The club has set every sign&apos;s weight to 0, so every score is 0.</p>
      ) : (
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-100" aria-label="The six signs and their weights">
          {signs.map((sign) => (
            <li key={sign.key} className="flex items-start gap-3 p-3">
              <span className="w-14 shrink-0 text-right font-semibold tabular-nums text-slate-900">{sign.share} of 100</span>
              <span>
                <span className="font-medium text-slate-900">{sign.label}.</span> {sign.text}
              </span>
            </li>
          ))}
        </ul>
      )}
      <p>
        Visits, spending and events are measured against the member&apos;s <strong>own past</strong>, not the club&apos;s average:
        someone who comes twice a month and still does is fine; someone who came every week and now comes twice a month is not.
        A member with less than a month of past to compare with shows no fall.
      </p>
      <p>
        The score needs a few months of club life (visits, spending, events) before it means much: until members have a past at the
        club, those signs have little to compare with, and most scores stay low.
      </p>
      <p className="text-slate-600">
        The club sets the weights and where watch and at risk start in{' '}
        <Link to={RETENTION_SETTINGS_PATH} className="font-medium text-orange-700 hover:underline">Club settings › Retention &amp; referrals</Link>
        {' '}(those who manage the settings can change them). Each member&apos;s page lists what each sign found for them.
      </p>
    </div>
  );
}
