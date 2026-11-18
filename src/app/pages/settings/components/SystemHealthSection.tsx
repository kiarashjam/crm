// Settings > System health: what went wrong in the workspace, before a customer says so.
// A verdict at the top, then one card per area with its count and first few rows, each with a
// link to where it is fixed and, where an endpoint already does it, Try again. The server's
// background jobs close the page. It is read again every minute while it is open.

import { useState, type ComponentType, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import {
  CircleCheck, CreditCard, HeartPulse, Mail, MailWarning, OctagonAlert, Plug, RefreshCw, RotateCcw, Send, ServerCog,
  TriangleAlert, Webhook, Zap,
} from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { PERMISSION } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import { resendOnboarding } from '@/app/api/onboarding';
import { retryAutomationRun } from '@/app/api/automations';
import type { HealthRetry, HealthStatus, JobHealth, SystemHealth } from '@/app/api/systemHealth';
import { mayIn } from '@/app/hooks/useMay';
import { useSystemHealth } from '@/app/hooks/queries/systemHealth';
import { currentDateLocale } from '@/app/i18n/current';
import { SettingsCard, StatusPill, type CardTone } from './SettingsCard';

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

/** "just now", "5 min ago", "3 h ago", "2 d ago": how long ago, at a glance. */
function ago(iso: string | null | undefined, now = Date.now()): string {
  const t = iso ? Date.parse(iso) : NaN;
  if (Number.isNaN(t)) return '';
  const minutes = Math.floor((now - t) / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return `${Math.floor(hours / 24)} d ago`;
}

/** "in 5 min", for a message's next attempt. */
function inTime(iso: string | null | undefined): string {
  const t = iso ? Date.parse(iso) : NaN;
  if (Number.isNaN(t)) return '';
  const minutes = Math.max(0, Math.round((t - Date.now()) / 60_000));
  return minutes < 60 ? `in ${minutes} min` : `in ${Math.round(minutes / 60)} h`;
}

/** "every 5 min", "every day". */
function every(seconds: number): string {
  if (seconds < 60) return `${seconds} s`;
  if (seconds < 3600) return `${Math.round(seconds / 60)} min`;
  if (seconds === 86_400) return 'day';
  return `${Math.round(seconds / 3600)} h`;
}

/** A moment, as "3 h ago", with the full date and time on hover and for screen readers. */
function When({ iso, prefix }: { iso: string | null | undefined; prefix?: string }) {
  if (!iso) return null;
  const full = new Date(iso).toLocaleString(currentDateLocale(), { dateStyle: 'medium', timeStyle: 'short' });
  return (
    <time dateTime={iso} title={full} className="whitespace-nowrap text-xs text-slate-500">
      {prefix ? `${prefix} ` : ''}{ago(iso)}
    </time>
  );
}

const VERDICT: Record<HealthStatus, { icon: ComponentType<{ className?: string }>; box: string; tile: string; text: string }> = {
  ok: {
    icon: CircleCheck,
    box: 'bg-emerald-50/70 ring-emerald-200 dark:bg-emerald-500/10 dark:ring-emerald-500/25',
    tile: 'bg-emerald-500 text-white shadow-emerald-500/30',
    text: 'text-emerald-800 dark:text-emerald-200',
  },
  attention: {
    icon: TriangleAlert,
    box: 'bg-amber-50/70 ring-amber-200 dark:bg-amber-500/10 dark:ring-amber-500/25',
    tile: 'bg-amber-500 text-white shadow-amber-500/30',
    text: 'text-amber-900 dark:text-amber-200',
  },
  failing: {
    icon: OctagonAlert,
    box: 'bg-rose-50/70 ring-rose-200 dark:bg-rose-500/10 dark:ring-rose-500/25',
    tile: 'bg-rose-600 text-white shadow-rose-600/30',
    text: 'text-rose-900 dark:text-rose-200',
  },
};

function headline(health: SystemHealth): string {
  const n = health.overall.issues;
  if (health.overall.status === 'ok' || n === 0) return 'All good';
  return `${n} ${n === 1 ? 'thing needs' : 'things need'} attention`;
}

function Verdict({ health, refreshing, onRefresh }: { health: SystemHealth; refreshing: boolean; onRefresh: () => void }) {
  const look = VERDICT[health.overall.status] ?? VERDICT.attention;
  const Icon = look.icon;
  return (
    <section aria-label="Verdict" className={cn('rounded-2xl p-4 ring-1 sm:p-5', look.box)}>
      <div className="flex flex-wrap items-center gap-4">
        <span aria-hidden className={cn('flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl shadow-lg', look.tile)}>
          <Icon className="h-7 w-7" />
        </span>
        <div className="min-w-0 flex-1 basis-48" role="status" aria-live="polite">
          <h3 className={cn('text-2xl font-semibold tracking-tight', look.text)}>{headline(health)}</h3>
          <p className="text-sm text-slate-600">
            {health.overall.status === 'failing' ? 'Something is failing now. ' : ''}
            Checked <When iso={health.checkedAtUtc} />, again every minute.
          </p>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={onRefresh} disabled={refreshing}>
          <RefreshCw className={cn('h-3.5 w-3.5', refreshing && 'animate-spin')} aria-hidden />
          Check now
        </Button>
      </div>
    </section>
  );
}

/** One area: its icon, title, count pill, link, its rows or that nothing is wrong. */
function Area({ icon, tone, title, count, link, linkLabel, empty, summary, more, children }: {
  icon: ComponentType<{ className?: string }>;
  tone: CardTone;
  title: string;
  count: number;
  link?: string | null;
  linkLabel?: string;
  empty: string;
  summary?: ReactNode;
  more?: number;
  children?: ReactNode;
}) {
  return (
    <SettingsCard
      icon={icon}
      tone={tone}
      title={title}
      description={summary}
      aside={(
        <>
          {link && (
            <Link to={link} className="text-xs font-medium text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline">
              {linkLabel ?? 'Open'}
            </Link>
          )}
          <StatusPill tone={count > 0 ? 'warn' : 'good'}>
            <span className="sr-only">{title}: </span>{count}
          </StatusPill>
        </>
      )}
    >
      {count === 0 ? (
        <p className="flex items-center gap-2 text-sm text-slate-500">
          <CircleCheck className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" aria-hidden />
          {empty}
        </p>
      ) : (
        <>
          <ul aria-label={title} className="divide-y divide-slate-100 overflow-hidden rounded-xl border border-slate-200 dark:divide-white/10">
            {children}
          </ul>
          {more ? <p className="mt-2 text-xs text-slate-500">and {more} more</p> : null}
        </>
      )}
    </SettingsCard>
  );
}

/** One row: what it is, the second line, the error, and when; with the actions at its end. */
function Row({ title, href, detail, error, at, atPrefix, badge, actions }: {
  title: ReactNode;
  href?: string | null;
  detail?: ReactNode;
  error?: string | null;
  at?: string | null;
  atPrefix?: string;
  badge?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <li className="flex flex-wrap items-start gap-x-3 gap-y-1.5 px-3 py-2.5">
      <div className="min-w-0 flex-1 basis-56">
        <p className="flex min-w-0 items-center gap-2 text-sm font-medium text-slate-900">
          {badge}
          {href ? (
            <Link to={href} className="truncate underline-offset-2 hover:underline">{title}</Link>
          ) : (
            <span className="truncate">{title}</span>
          )}
        </p>
        {detail && <p className="truncate text-xs text-slate-500">{detail}</p>}
        {error && <p className="mt-0.5 line-clamp-2 break-words text-xs text-rose-700 dark:text-rose-300">{error}</p>}
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <When iso={at} prefix={atPrefix} />
        {actions}
      </div>
    </li>
  );
}

function Tag({ children, tone = 'slate' }: { children: ReactNode; tone?: 'slate' | 'rose' | 'amber' }) {
  return (
    <span className={cn('shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide', {
      slate: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-slate-300',
      rose: 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',
      amber: 'bg-amber-100 text-amber-800 dark:bg-amber-500/15 dark:text-amber-300',
    }[tone])}
    >
      {children}
    </span>
  );
}

const JOB_STATE = {
  ok: { label: 'Running', dot: 'bg-emerald-500' },
  failing: { label: 'Failing', dot: 'bg-amber-500' },
  silent: { label: 'Silent', dot: 'bg-rose-600' },
} as const;

function jobState(job: JobHealth): keyof typeof JOB_STATE {
  return job.silent ? 'silent' : job.failing ? 'failing' : 'ok';
}

function Jobs({ jobs }: { jobs: JobHealth[] }) {
  const trouble = jobs.filter((j) => j.silent || j.failing).length;
  return (
    <SettingsCard
      icon={ServerCog}
      tone="violet"
      title="Background jobs"
      description="The server's, shared by every workspace."
      aside={<StatusPill tone={trouble > 0 ? 'warn' : jobs.length > 0 ? 'good' : 'idle'}>{trouble > 0 ? `${trouble} not running well` : jobs.length > 0 ? 'All running' : 'None yet'}</StatusPill>}
    >
      {jobs.length === 0 ? (
        <p className="text-sm text-slate-500">No job has reported yet: they do soon after the server starts.</p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200">
          <table className="w-full min-w-[30rem] text-left text-sm">
            <caption className="sr-only">Background jobs</caption>
            <thead className="bg-slate-50 text-xs text-slate-500">
              <tr>
                <th scope="col" className="px-3 py-2 font-medium">Job</th>
                <th scope="col" className="px-3 py-2 font-medium">Every</th>
                <th scope="col" className="px-3 py-2 font-medium">Last seen</th>
                <th scope="col" className="px-3 py-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-white/10">
              {jobs.map((job) => {
                const state = JOB_STATE[jobState(job)];
                return (
                  <tr key={job.name} className="align-top">
                    <td className="px-3 py-2 font-medium text-slate-900">
                      <span className="flex items-center gap-2">
                        <span aria-hidden className={cn('h-2.5 w-2.5 shrink-0 rounded-full ring-2 ring-white dark:ring-slate-900', state.dot)} />
                        {job.name}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-slate-600">{every(job.intervalSeconds)}</td>
                    <td className="px-3 py-2"><When iso={job.lastSeenAtUtc} /></td>
                    <td className="px-3 py-2">
                      <span className="text-slate-700">{state.label}</span>
                      {job.failing && job.consecutiveFailures > 1 && <span className="text-xs text-slate-500"> · {job.consecutiveFailures} in a row</span>}
                      {job.lastError && <p className="mt-0.5 break-words text-xs text-rose-700 dark:text-rose-300">{job.lastError}</p>}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </SettingsCard>
  );
}

const KIND_LABEL = { email: 'Email', webhook: 'Webhook', handoff: 'Handoff' } as const;

export function SystemHealthSection() {
  const demo = servesDemoData();
  const query = useSystemHealth(!demo);
  const org = useOrgOptional();
  const [retrying, setRetrying] = useState<string | null>(null);

  const header = (
    <header className="flex flex-wrap items-center gap-4">
      <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-md shadow-orange-500/25">
        <HeartPulse className="h-5 w-5" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <h2 className="text-xl font-semibold text-slate-900">System health</h2>
        <p className="text-sm text-slate-500">What needs attention in this workspace, before a customer notices.</p>
      </div>
    </header>
  );

  if (demo) {
    return (
      <div className="space-y-6 p-4 sm:p-6">
        {header}
        <p className="rounded-xl bg-slate-50 p-3 text-sm text-slate-600">System health reports on the server, so there is nothing to show in demo mode.</p>
      </div>
    );
  }

  if (query.isPending) {
    return (
      <div className="space-y-6 p-4 sm:p-6">
        {header}
        <div className="py-10 text-center"><Spinner size={24} className="mx-auto" /></div>
      </div>
    );
  }

  if (query.isError || !query.data) {
    return (
      <div className="space-y-6 p-4 sm:p-6">
        {header}
        <LoadFailed what="System health" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} />
      </div>
    );
  }

  const h = query.data;
  const mayResend = mayIn(org, PERMISSION.EditRecords);

  const retry = async (what: HealthRetry) => {
    setRetrying(what.id);
    try {
      if (what.kind === 'handoff') await resendOnboarding(what.id);
      else await retryAutomationRun(what.id);
      toast.success(what.kind === 'handoff' ? 'Sent to the portal again.' : 'Tried again.');
      void query.refetch();
    } catch (err) {
      toast.error(reason(err, 'It could not be tried again.'));
    } finally {
      setRetrying(null);
    }
  };

  const retryButton = (what: HealthRetry | null | undefined, label: string) => {
    if (!what || (what.kind === 'handoff' && !mayResend)) return null;
    const busy = retrying === what.id;
    return (
      <Button type="button" size="sm" variant="outline" className="h-7 px-2 text-xs" onClick={() => void retry(what)} disabled={retrying !== null} aria-label={`Try again: ${label}`}>
        {busy ? <Spinner size={12} /> : <RotateCcw className="h-3 w-3" aria-hidden />}
        Try again
      </Button>
    );
  };

  const more = (count: number, shown: number) => Math.max(0, count - shown);

  return (
    <div className="space-y-5 p-4 sm:p-6">
      {header}
      <Verdict health={h} refreshing={query.isFetching} onRefresh={() => void query.refetch()} />

      <div className="grid gap-4 xl:grid-cols-2">
        <Area
          icon={Send}
          tone="pink"
          title="Welcome handoffs"
          count={h.handoffs.count}
          empty="Every handoff reached the portal."
          more={more(h.handoffs.count, h.handoffs.items.length)}
        >
          {h.handoffs.items.map((o) => (
            <Row
              key={o.id}
              title={o.person}
              href={o.link}
              detail={o.dealName}
              badge={o.state === 'late' ? <Tag tone="amber">Not taken</Tag> : <Tag tone="rose">Needs attention</Tag>}
              error={o.lastError}
              at={o.sinceUtc}
              actions={retryButton(o.retry, `the welcome for ${o.person}`)}
            />
          ))}
        </Area>

        <Area
          icon={Mail}
          tone="orange"
          title="Outbox"
          count={h.outbox.count}
          summary={h.outbox.count > 0 ? `${h.outbox.queued} waiting over 30 min · ${h.outbox.failed} failed this week` : undefined}
          empty="Nothing stuck, nothing failed this week."
          more={more(h.outbox.count, h.outbox.items.length)}
        >
          {h.outbox.items.map((m) => (
            <Row
              key={m.id}
              title={m.to || KIND_LABEL[m.kind]}
              href={m.link}
              detail={[m.subject, m.state === 'queued' ? `attempt ${m.attempts}${m.nextAttemptAtUtc ? `, next ${inTime(m.nextAttemptAtUtc)}` : ''}` : null].filter(Boolean).join(' · ')}
              badge={<><Tag>{KIND_LABEL[m.kind]}</Tag>{m.state === 'failed' ? <Tag tone="rose">Failed</Tag> : <Tag tone="amber">Waiting</Tag>}</>}
              error={m.lastError}
              at={m.createdAtUtc}
              atPrefix="queued"
              actions={retryButton(m.retry, `the ${KIND_LABEL[m.kind].toLowerCase()} to ${m.to ?? 'the portal'}`)}
            />
          ))}
        </Area>

        <Area
          icon={Zap}
          tone="violet"
          title="Automations"
          count={h.automations.count}
          empty="No run failed this week."
          more={more(h.automations.count, h.automations.items.length)}
        >
          {h.automations.items.map((r) => (
            <Row
              key={r.runId}
              title={r.ruleName}
              href={r.link}
              detail={r.recordName}
              error={r.lastError}
              at={r.failedAtUtc}
              actions={retryButton(r.retry, `the run of ${r.ruleName}`)}
            />
          ))}
        </Area>

        <Area
          icon={MailWarning}
          tone="orange"
          title="Email delivery"
          count={h.email.count}
          link={h.email.count > 0 || h.email.newSuppressions > 0 ? h.email.link : null}
          linkLabel="Do-not-send list"
          summary={h.email.count > 0 || h.email.newSuppressions > 0
            ? `${h.email.bounces} bounced · ${h.email.spamReports} spam ${h.email.spamReports === 1 ? 'report' : 'reports'} · ${h.email.newSuppressions} new on the do-not-send list`
            : undefined}
          empty="No bounce or spam report this week."
          more={more(h.email.count, h.email.items.length)}
        >
          {h.email.items.map((e) => (
            <Row
              key={e.id}
              title={e.to ?? 'Email'}
              href={e.link}
              detail={e.subject}
              badge={<Tag tone="rose">{e.kind === 'bounce' ? 'Bounced' : 'Spam report'}</Tag>}
              error={e.detail}
              at={e.atUtc}
            />
          ))}
        </Area>

        <Area
          icon={Webhook}
          tone="sky"
          title="Webhooks"
          count={h.webhooks.count}
          link={h.webhooks.count > 0 ? h.webhooks.link : null}
          linkLabel="Integrations"
          empty="Every event reached your address this week."
          more={more(h.webhooks.count, h.webhooks.items.length)}
        >
          {h.webhooks.items.map((d) => (
            <Row
              key={d.id}
              title={d.eventName || 'Event'}
              detail={`${d.attempts} ${d.attempts === 1 ? 'attempt' : 'attempts'}${d.lastStatusCode ? ` · answered ${d.lastStatusCode}` : ''}`}
              error={d.lastError}
              at={d.createdAtUtc}
            />
          ))}
        </Area>

        <Area
          icon={CreditCard}
          tone="emerald"
          title="Payments"
          count={h.payments.count}
          link={h.payments.count > 0 ? h.payments.link : null}
          linkLabel="Payments"
          summary={h.payments.count > 0 ? 'Stripe sends each event again for a few days.' : undefined}
          empty="Every Stripe event was processed this week."
          more={more(h.payments.count, h.payments.items.length)}
        >
          {h.payments.items.map((p) => (
            <Row key={p.id} title={p.type} detail={p.eventId} error={p.note} at={p.receivedAtUtc} />
          ))}
        </Area>

        <PortalArea health={h} />
      </div>

      <Jobs jobs={h.jobs} />
    </div>
  );
}

function PortalArea({ health }: { health: SystemHealth }) {
  const p = health.portal;
  const failed = p.configured && p.ok === false;
  return (
    <SettingsCard
      icon={Plug}
      tone="sky"
      title="Portal"
      description={!p.configured ? 'Not set up.' : p.ok == null ? 'Not pinged yet: it is, once a day.' : <>Last ping <When iso={p.atUtc} /></>}
      aside={(
        <>
          <Link to={p.link} className="text-xs font-medium text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline">Welcome &amp; portal</Link>
          <StatusPill tone={!p.configured || p.ok == null ? 'idle' : p.ok ? 'good' : 'warn'}>
            {!p.configured ? 'Off' : p.ok == null ? 'Waiting' : p.ok ? 'Connected' : 'Not answering'}
          </StatusPill>
        </>
      )}
    >
      {failed ? (
        <p className="break-words text-sm text-rose-700 dark:text-rose-300">
          {p.error ?? 'The portal did not take the ping.'}{p.statusCode ? ` (${p.statusCode})` : ''}
        </p>
      ) : undefined}
    </SettingsCard>
  );
}
