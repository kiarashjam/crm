// Step three: what goes, to how many, a confirm, and then what the server did: the counts of
// the send, or that it had gone already (the same send pressed twice sends nothing more).

import {
  CalendarClock, CheckCircle2, Clock, History, KeyRound, Languages, Loader2, Mail, Send, ShieldCheck, UserX, Users,
} from 'lucide-react';
import type { NewsletterAudience, NewsletterLanguage, NewsletterSendResult } from '@/app/api/leadNewsletter';
import { StatusPill } from '@/app/pages/settings/components/SettingsCard';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import { leads, leftOutTotal } from './audience';
import { ProblemNote } from './parts';

export type SendPhase = 'ready' | 'confirm' | 'sending' | 'done';

/** Emails a minute the server hands the mail server (LeadNewsletterService.PerMinute). */
const PER_MINUTE = 60;

export function SendStep({
  audience, subject, language, phase, result, error, blocked, busy, onAsk, onConfirm, onCancel,
}: {
  audience: NewsletterAudience | null;
  /** The subject as one lead reads it. */
  subject: string;
  language: NewsletterLanguage;
  phase: SendPhase;
  result: NewsletterSendResult | null;
  error: string | null;
  /** Why it cannot go yet (still counting, a problem with the email), or null. */
  blocked: string | null;
  /** Whether that is only a wait: the count or the preview on its way. */
  busy: boolean;
  onAsk: () => void;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  if (phase === 'done' && result) return <SentPanel result={result} />;
  const count = audience?.willReceive ?? 0;
  const minutes = Math.ceil(count / PER_MINUTE);

  return (
    <div className="mx-auto max-w-xl space-y-5">
      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.03]">
        <div className="flex items-center gap-4">
          <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-md shadow-orange-500/25" aria-hidden>
            <Users className="h-6 w-6" />
          </span>
          <div className="min-w-0">
            <h3 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">Send to {leads(count)}</h3>
            <p className="truncate text-sm text-slate-500 dark:text-slate-400">{subject || '—'}</p>
          </div>
        </div>
        <ul className="mt-5 grid gap-2 text-sm sm:grid-cols-2">
          <Fact icon={Languages}>{language === 'fr' ? 'Français' : 'English'}</Fact>
          <Fact icon={KeyRound}>Own link and code each</Fact>
          <Fact icon={CalendarClock}>Weekday, then time</Fact>
          {audience && leftOutTotal(audience) > 0 && <Fact icon={UserX}>{`${leftOutTotal(audience).toLocaleString()} left out`}</Fact>}
          {minutes > 1 && <Fact icon={Clock}>{`Goes out over ~${minutes} min`}</Fact>}
          <Fact icon={ShieldCheck}>Unsubscribe link added</Fact>
        </ul>
      </div>

      {error && <ProblemNote>{error}</ProblemNote>}
      {blocked && phase !== 'sending' && (
        <p className="flex items-center justify-center gap-2 text-sm text-slate-500 dark:text-slate-400" role="status">
          {busy && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
          {blocked}
        </p>
      )}

      {phase === 'confirm' || phase === 'sending' ? (
        <div
          role="group"
          aria-labelledby="newsletter-confirm"
          className="rounded-2xl border-2 border-orange-300 bg-orange-50/70 p-5 text-center dark:border-orange-500/40 dark:bg-orange-500/10"
        >
          <p id="newsletter-confirm" className="text-lg font-semibold text-slate-900 dark:text-white">Send it now?</p>
          <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{count.toLocaleString()} emails. This can&apos;t be undone.</p>
          <div className="mt-4 flex flex-col-reverse justify-center gap-2 sm:flex-row">
            {/* The safe choice has the focus: a stray Enter cancels, it does not send. */}
            <Button type="button" variant="outline" autoFocus onClick={onCancel} disabled={phase === 'sending'}>Cancel</Button>
            <Button
              type="button"
              onClick={onConfirm}
              disabled={phase === 'sending' || Boolean(blocked)}
              className="bg-gradient-to-r from-orange-500 to-pink-500 text-white hover:opacity-95"
            >
              {phase === 'sending' ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Send className="h-4 w-4" aria-hidden />}
              {phase === 'sending' ? 'Sending…' : 'Yes, send'}
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex justify-center">
          <Button
            type="button"
            size="lg"
            onClick={onAsk}
            disabled={Boolean(blocked) || count === 0}
            className="h-12 rounded-xl bg-gradient-to-r from-orange-500 to-pink-500 px-8 text-base font-semibold text-white shadow-lg shadow-orange-500/25 hover:opacity-95"
          >
            <Send className="h-5 w-5" aria-hidden /> Send to {leads(count)}
          </Button>
        </div>
      )}
    </div>
  );
}

function Fact({ icon: Icon, children }: { icon: typeof Mail; children: string }) {
  return (
    <li className="flex items-center gap-2 rounded-lg bg-slate-50 px-3 py-2 text-slate-700 dark:bg-white/5 dark:text-slate-200">
      <Icon className="h-4 w-4 shrink-0 text-orange-500" aria-hidden />
      {children}
    </li>
  );
}

/** What the server did: queued, or had already sent this one, with its counts either way. */
function SentPanel({ result }: { result: NewsletterSendResult }) {
  const again = result.alreadySent;
  return (
    <div role="status" className="mx-auto flex max-w-md flex-col items-center py-6 text-center animate-in fade-in zoom-in-95 duration-300 motion-reduce:animate-none">
      <span
        aria-hidden
        className={cn(
          'flex h-20 w-20 items-center justify-center rounded-full shadow-lg',
          again
            ? 'bg-sky-100 text-sky-600 shadow-sky-500/20 dark:bg-sky-500/15 dark:text-sky-300'
            : 'bg-emerald-100 text-emerald-600 shadow-emerald-500/20 dark:bg-emerald-500/15 dark:text-emerald-300',
        )}
      >
        {again ? <History className="h-9 w-9" /> : <CheckCircle2 className="h-10 w-10" />}
      </span>
      <h3 className="mt-5 text-2xl font-bold tracking-tight text-slate-900 dark:text-white">{again ? 'Already sent' : 'On its way'}</h3>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
        {again ? 'This newsletter went out before. Nothing more was sent.' : 'Each lead gets their own link and code.'}
      </p>
      <dl className="mt-6 grid w-full grid-cols-2 gap-3">
        <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/[0.03]">
          <dt className="flex items-center justify-center gap-1.5 text-xs font-medium text-slate-500 dark:text-slate-400"><Send className="h-3.5 w-3.5" aria-hidden /> Invited</dt>
          <dd className="mt-1 text-3xl font-bold tabular-nums text-slate-900 dark:text-white">{result.recipients.toLocaleString()}</dd>
        </div>
        <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/[0.03]">
          <dt className="flex items-center justify-center gap-1.5 text-xs font-medium text-slate-500 dark:text-slate-400"><UserX className="h-3.5 w-3.5" aria-hidden /> Left out</dt>
          <dd className="mt-1 text-3xl font-bold tabular-nums text-slate-900 dark:text-white">{result.leftOut.toLocaleString()}</dd>
        </div>
      </dl>
      <StatusPill tone={result.status === 'sent' ? 'good' : 'info'} className="mt-4">
        {result.status === 'sent' ? 'All queued' : 'Queuing…'}
      </StatusPill>
    </div>
  );
}
