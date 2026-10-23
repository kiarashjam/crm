import { useMemo, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Link, useLocation } from 'react-router-dom';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowRight, Check, ChevronDown, Compass, Loader2, PenLine, RotateCw, Sparkles } from 'lucide-react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { cn } from '@/app/components/ui/utils';
import { getCurrentUser } from '@/app/lib/auth';
import { reloadAgentData, useAgentData, type AgentLists } from './agentData';
import { AgentDesk } from './AgentDesk';
import type { AgentWorld } from './brain/answer';
import { learnFrom } from './brain/patterns';
import { useNextActionsSlot } from './NextActionsSlot';
import { itemOnList, pageScope, type PageScope } from './pageScope';
import { dealPlan, type PlanLine } from './recordPlan';
import { collectWork, type WorkSuggestion } from './workspace';
import type { WriterBrief } from './writerBrief';

/**
 * The sales agent's panel: the next thing worth doing, from what is already saved.
 *
 * It sits right after each page's title (NextActionsSlot), so it has to earn its
 * space. The lists come from one shared store that refreshes itself after any
 * save (agentData.ts), so the panel agrees with the page it sits on. On the
 * Dashboard — the agent's home — it opens with the full queue; elsewhere it
 * shows the single top step, with the rest one click away, and remembers which
 * way you left it. On a record it shows that record's one next step, and
 * nothing at all when there is nothing to do.
 *
 * Motion is there to say what changed: the card arrives, steps slide in and a
 * finished one slides out, the count rolls when it moves. It all stops for
 * anyone who asked for less motion, in the OS or in Settings.
 *
 * "Draft message" only fills the sales writer. Nothing is ever sent from here.
 * The ask box answers from these same lists. A task, note, or other change runs
 * only after its button, and a read-only workspace saves nothing.
 */

const MAX_SHOWN = 5;
const EASE = [0.16, 1, 0.3, 1] as const;

function recipientFor(item: WorkSuggestion): { type: 'lead' | 'contact' | 'deal'; id: string; name: string } | null {
  const match = /^\/(leads|contacts|deals)\/([^/]+)$/.exec(item.href);
  if (!match?.[1] || !match[2]) return null;
  const type = match[1] === 'leads' ? 'lead' : match[1] === 'deals' ? 'deal' : 'contact';
  return { type, id: decodeURIComponent(match[2]), name: item.name };
}

function inList(item: WorkSuggestion, list: Extract<PageScope, { mode: 'list' }>['list']): boolean {
  return itemOnList(item, list);
}

function askWorld(lists: AgentLists, items: WorkSuggestion[], scope: Exclude<PageScope, { mode: 'hidden' }>): AgentWorld {
  return {
    items,
    leads: lists.leads,
    deals: lists.deals,
    contacts: lists.contacts,
    companies: lists.companies,
    tasks: lists.tasks,
    sequences: lists.sequences,
    plan: null,
    page: scope,
    patterns: learnFrom(lists.leads, lists.deals),
    invites: [],
    contractHint: null,
  };
}

function AskBox({ lists, items, scope }: { lists: AgentLists; items: WorkSuggestion[]; scope: Exclude<PageScope, { mode: 'hidden' }> }) {
  const org = useOrgOptional();
  const userId = getCurrentUser()?.id ?? 'session';
  return (
    <AgentDesk
      world={askWorld(lists, items, scope)}
      readOnly={org?.isReadOnly ?? false}
      accountKey={`${userId}:${org?.currentOrgId ?? 'no-org'}`}
    />
  );
}

/** What a record is called, for "Next for this …". */
function recordNoun(href: string): string {
  if (href.startsWith('/leads/')) return 'lead';
  if (href.startsWith('/deals/')) return 'deal';
  if (href.startsWith('/tasks/')) return 'task';
  if (href.startsWith('/contacts/')) return 'contact';
  if (href.startsWith('/companies/')) return 'company';
  return 'record';
}

/** How pressing a step is, from its rank: today-or-overdue, soon, or in its turn. */
type Urgency = 'now' | 'soon' | 'later';
function urgencyOf(item: WorkSuggestion): Urgency {
  if (item.priority >= 90) return 'now';
  if (item.priority >= 75) return 'soon';
  return 'later';
}
const URGENCY_BAR: Record<Urgency, string> = {
  now: 'bg-rose-500',
  soon: 'bg-amber-400',
  later: 'bg-slate-300 dark:bg-slate-600',
};
const URGENCY_WORD: Record<Urgency, string> = { now: 'Urgent', soon: 'Soon', later: 'In turn' };

// ── remembered open/closed state ─────────────────────────────────────────────

const OPEN_KEY = (where: 'home' | 'page') => `cadence.nextActions.open.${where}`;

function readOpen(where: 'home' | 'page'): boolean {
  try {
    const stored = localStorage.getItem(OPEN_KEY(where));
    if (stored === 'true') return true;
    if (stored === 'false') return false;
  } catch {
    // Storage blocked: fall through to the default.
  }
  return where === 'home';
}

function writeOpen(where: 'home' | 'page', open: boolean): void {
  try {
    localStorage.setItem(OPEN_KEY(where), String(open));
  } catch {
    // A preference that cannot be saved is still applied for this page.
  }
}

// ── shared pieces ────────────────────────────────────────────────────────────

const shell = 'relative overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm shadow-slate-200/60 dark:border-slate-700 dark:bg-slate-900 dark:shadow-none';
const focusRing = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2 dark:focus-visible:ring-offset-slate-900';

/** The thin moving gradient along the top of the card. */
function Accent() {
  return (
    <div
      aria-hidden="true"
      className="absolute inset-x-0 top-0 h-1 bg-[linear-gradient(90deg,#f97316,#f59e0b,#8b5cf6,#f97316)] bg-[length:200%_100%] motion-safe:animate-[shimmer_10s_linear_infinite]"
    />
  );
}

/** The card arriving: a short rise and fade. */
function Card({ className, labelledBy, children }: { className?: string; labelledBy: string; children: ReactNode }) {
  const reduce = useMotionPreference();
  return (
    <motion.section
      className={cn(shell, className)}
      aria-labelledby={labelledBy}
      initial={reduce ? false : { opacity: 0, y: 14, scale: 0.99 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={{ duration: 0.45, ease: EASE }}
    >
      <Accent />
      {children}
    </motion.section>
  );
}

function IconTile({ busy }: { busy: boolean }) {
  const reduce = useMotionPreference();
  return (
    <motion.span
      className="relative flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 text-white shadow-md shadow-orange-500/30"
      whileHover={reduce ? undefined : { rotate: -12, scale: 1.06 }}
      transition={{ type: 'spring', stiffness: 300, damping: 15 }}
      aria-hidden="true"
    >
      <Compass className="h-5 w-5" />
      {busy && (
        <span className="absolute -right-1 -top-1 flex h-3 w-3">
          <span className="absolute inline-flex h-full w-full rounded-full bg-amber-300 opacity-75 motion-safe:animate-ping" />
          <span className="relative inline-flex h-3 w-3 rounded-full bg-amber-400 ring-2 ring-white dark:ring-slate-900" />
        </span>
      )}
    </motion.span>
  );
}

/** A number that rolls when it changes. */
function RollingNumber({ value }: { value: number }) {
  const reduce = useMotionPreference();
  return (
    <span className="relative inline-flex overflow-hidden tabular-nums">
      <AnimatePresence mode="popLayout" initial={false}>
        <motion.span
          key={value}
          initial={reduce ? false : { y: '-100%', opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          exit={reduce ? { opacity: 0 } : { y: '100%', opacity: 0 }}
          transition={{ duration: 0.3, ease: EASE }}
        >
          {value}
        </motion.span>
      </AnimatePresence>
    </span>
  );
}

function DraftLink({ item, compact = false }: { item: WorkSuggestion; compact?: boolean }) {
  if (!item.brief) return null;
  return (
    <Link
      to="/dashboard"
      state={{ writerBrief: item.brief satisfies WriterBrief, writerRecipient: recipientFor(item) }}
      title="Opens the sales writer with a draft. Nothing is sent."
      className={cn(
        'group inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm font-semibold text-slate-700 transition-colors',
        'hover:border-[var(--brand-primary)] hover:text-[var(--brand-primary-dark)]',
        'dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100',
        compact ? 'h-9' : 'h-10',
        focusRing,
      )}
    >
      <PenLine className="h-4 w-4 transition-transform motion-safe:group-hover:-rotate-12" aria-hidden="true" />
      Draft message
      <span className="sr-only">for {item.name}. Nothing is sent.</span>
    </Link>
  );
}

function OpenLink({ item }: { item: WorkSuggestion }) {
  return (
    <Link
      to={item.href}
      className={cn(
        'group inline-flex h-9 shrink-0 items-center gap-1 rounded-lg bg-gradient-to-r from-orange-500 to-amber-500 px-3 text-sm font-semibold text-white shadow-sm shadow-orange-500/30 transition-shadow',
        'hover:shadow-md hover:shadow-orange-500/40',
        focusRing,
      )}
    >
      Open<span className="sr-only"> {item.name}</span>
      <ArrowRight className="h-4 w-4 transition-transform motion-safe:group-hover:translate-x-0.5" aria-hidden="true" />
    </Link>
  );
}

function Refreshing({ on }: { on: boolean }) {
  if (!on) return null;
  return (
    <span className="inline-flex items-center text-slate-400" role="status">
      <Loader2 className="h-3.5 w-3.5 motion-safe:animate-spin" aria-hidden="true" />
      <span className="sr-only">Updating next steps</span>
    </span>
  );
}

/** One suggestion: who, what, why — then the two things you can do about it. */
function Row({ item, index }: { item: WorkSuggestion; index: number }) {
  const reduce = useMotionPreference();
  const urgency = urgencyOf(item);
  const first = index === 0;
  return (
    <motion.li
      layout={reduce ? false : 'position'}
      initial={reduce ? false : { opacity: 0, height: 0 }}
      animate={{ opacity: 1, height: 'auto', transition: { duration: 0.35, ease: EASE, delay: reduce ? 0 : Math.min(index, 5) * 0.06 } }}
      exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, height: 0, x: 24, transition: { duration: 0.25, ease: EASE } }}
      className="overflow-hidden"
    >
      <div className="pb-2">
        <motion.div
          whileHover={reduce ? undefined : { y: -2 }}
          transition={{ type: 'spring', stiffness: 400, damping: 30 }}
          className={cn(
            'relative flex gap-3 overflow-hidden rounded-xl border py-3 pl-4 pr-3 transition-shadow hover:shadow-md',
            first
              ? 'border-orange-200 bg-gradient-to-r from-orange-50/80 via-white to-white dark:border-orange-900/60 dark:from-orange-950/30 dark:via-slate-900 dark:to-slate-900'
              : 'border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-950',
          )}
        >
          <span aria-hidden="true" className={cn('absolute inset-y-0 left-0 w-1', URGENCY_BAR[urgency])} />
          <span
            className={cn(
              'mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold',
              first
                ? 'bg-gradient-to-br from-orange-500 to-amber-500 text-white shadow-sm shadow-orange-500/40'
                : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
            )}
            aria-hidden="true"
          >
            {index + 1}
          </span>
          <div className="flex min-w-0 flex-1 flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
            <div className="min-w-0 flex-1">
              <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-slate-900 dark:text-white">
                <Link to={item.href} className={cn('rounded-sm font-semibold hover:text-[var(--brand-primary-dark)]', focusRing)}>
                  {item.name}
                </Link>
                <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                  {item.list}
                </span>
                {first && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-orange-100 px-2 py-0.5 text-[11px] font-semibold text-orange-700 dark:bg-orange-950 dark:text-orange-300">
                    <span className="relative flex h-1.5 w-1.5" aria-hidden="true">
                      <span className="absolute inline-flex h-full w-full rounded-full bg-orange-400 opacity-75 motion-safe:animate-ping" />
                      <span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-orange-500" />
                    </span>
                    Next up
                  </span>
                )}
                {urgency === 'now' && (
                  <span className="rounded-full bg-rose-50 px-2 py-0.5 text-[11px] font-semibold text-rose-700 dark:bg-rose-950 dark:text-rose-300">
                    {URGENCY_WORD.now}
                  </span>
                )}
              </p>
              <p className="mt-1 text-sm font-medium text-slate-800 dark:text-slate-100">{item.action}</p>
              <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">{item.because}</p>
            </div>
            <div className="flex shrink-0 flex-wrap gap-2">
              <OpenLink item={item} />
              <DraftLink item={item} compact />
            </div>
          </div>
        </motion.div>
      </div>
    </motion.li>
  );
}

// ── the record strip ─────────────────────────────────────────────────────────

const STEP_TONE: Record<PlanLine['state'], string> = {
  done: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300',
  now: 'border-orange-300 bg-orange-50 text-orange-800 ring-2 ring-orange-200 dark:border-orange-700 dark:bg-orange-950 dark:text-orange-200 dark:ring-orange-900',
  later: 'border-slate-200 bg-white text-slate-400 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-500',
  stopped: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900 dark:bg-rose-950 dark:text-rose-300',
};
const STEP_WORD: Record<PlanLine['state'], string> = { done: 'done', now: 'current stage', later: 'not reached', stopped: 'stopped' };

/** The deal's stage ladder, in one row. Leads skip it: their page has the full lifecycle. */
function Ladder({ lines }: { lines: PlanLine[] }) {
  const reduce = useMotionPreference();
  return (
    <ol className="mt-3 flex flex-wrap items-center gap-1.5" aria-label="Deal stages">
      {lines.map((line, index) => (
        <motion.li
          key={`${line.state}-${line.title}`}
          initial={reduce ? false : { opacity: 0, scale: 0.9 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.25, delay: reduce ? 0 : 0.15 + index * 0.05, ease: EASE }}
          className={cn('inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium', STEP_TONE[line.state])}
        >
          {line.state === 'done' && <Check className="h-3 w-3" aria-hidden="true" />}
          {line.state === 'now' && <span className="h-1.5 w-1.5 rounded-full bg-orange-500 motion-safe:animate-pulse" aria-hidden="true" />}
          {line.title}
          <span className="sr-only">, {STEP_WORD[line.state]}</span>
        </motion.li>
      ))}
    </ol>
  );
}

function RecordStrip({ href, step, items, lists, refreshing }: {
  href: string;
  step: WorkSuggestion;
  items: WorkSuggestion[];
  lists: AgentLists;
  refreshing: boolean;
}) {
  const reduce = useMotionPreference();
  const isDeal = href.startsWith('/deals/');
  const dealId = isDeal ? decodeURIComponent(href.split('/').pop() ?? '') : '';
  const deal = isDeal ? lists.deals.find((item) => item.id === dealId) : undefined;
  const ladder = deal ? dealPlan(deal).lines : null;

  return (
    <Card className="px-4 pb-4 pt-5" labelledBy="next-actions-heading">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:gap-4">
        <IconTile busy={refreshing} />
        <div className="min-w-0 flex-1">
          <h2 id="next-actions-heading" className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-orange-600 dark:text-orange-400">
            Next for this {recordNoun(href)}
            <Refreshing on={refreshing} />
          </h2>
          {/* Keyed on the step, so a new step fades in when the record moves on. */}
          <motion.div key={step.key + step.action} initial={reduce ? false : { opacity: 0, x: -6 }} animate={{ opacity: 1, x: 0 }} transition={{ duration: 0.3, ease: EASE }}>
            <p className="mt-0.5 text-sm font-semibold text-slate-900 dark:text-white">{step.action}</p>
            <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">{step.because}</p>
          </motion.div>
        </div>
        <DraftLink item={step} />
      </div>
      {ladder && <Ladder lines={ladder} />}
      <AskBox lists={lists} items={items} scope={{ mode: 'record', href }} />
    </Card>
  );
}

// ── the panel ────────────────────────────────────────────────────────────────

export function NextActions() {
  const orgId = useOrgOptional()?.currentOrgId ?? null;
  const { pathname } = useLocation();
  const scope = pageScope(pathname);
  const where: 'home' | 'page' = scope.mode === 'focus' && (pathname === '/dashboard' || pathname === '/') ? 'home' : 'page';
  const [open, setOpen] = useState(() => readOpen(where));
  const data = useAgentData(orgId, scope.mode !== 'hidden');
  const slot = useNextActionsSlot();
  const reduce = useMotionPreference();

  // Recomputed only when the lists change — not on every render of every page.
  const allItems = useMemo(
    () => (data.lists ? collectWork(data.lists, new Date(), Number.MAX_SAFE_INTEGER) : []),
    [data.lists],
  );

  /** In the page's slot when it has one (after its title), else under the header. */
  const place = (content: ReactNode) =>
    slot
      ? createPortal(content, slot)
      : <div className="w-full px-[var(--page-padding)] pb-4 pt-4">{content}</div>;

  if (scope.mode === 'hidden') return null;

  if (!data.lists) {
    if (data.failed) {
      return place(
        <div className={cn(shell, 'flex items-center justify-between gap-3 px-4 py-3')} role="status">
          <p className="text-sm text-slate-600 dark:text-slate-300">Next steps could not be loaded.</p>
          <button
            type="button"
            onClick={reloadAgentData}
            className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 px-3 text-sm font-semibold text-slate-700 hover:border-[var(--brand-primary)] dark:border-slate-600 dark:text-slate-100', focusRing)}
          >
            <RotateCw className="h-4 w-4" aria-hidden="true" />
            Try again
          </button>
        </div>,
      );
    }
    // A slim placeholder the size of the closed panel, so the page does not
    // jump when the steps arrive. Only ever shown before the very first load.
    return place(
      <div className={cn(shell, 'flex h-16 items-center gap-3 px-4')} aria-busy="true" aria-label="Loading next steps">
        <Accent />
        <div className="h-10 w-10 rounded-xl bg-slate-100 motion-safe:animate-pulse dark:bg-slate-800" />
        <div className="space-y-2">
          <div className="h-3.5 w-40 rounded bg-slate-100 motion-safe:animate-pulse dark:bg-slate-800" />
          <div className="h-3 w-24 rounded bg-slate-100 motion-safe:animate-pulse dark:bg-slate-800" />
        </div>
      </div>,
    );
  }

  if (scope.mode === 'record') {
    const step = allItems.find((item) => item.href === scope.href);
    // Nothing owed on this record: render nothing at all — not even the spacing.
    // The page already shows its state, and a card (or a gap) on every finished
    // record is noise.
    if (!step) return null;
    return place(<RecordStrip href={scope.href} step={step} items={allItems} lists={data.lists} refreshing={data.refreshing} />);
  }

  const scoped = scope.mode === 'list' ? allItems.filter((item) => inList(item, scope.list)) : allItems;
  const shown = scoped.slice(0, open ? MAX_SHOWN : 1);
  const beyond = scoped.length - Math.min(scoped.length, MAX_SHOWN);
  const canToggle = scoped.length > 1;
  const toggle = () => {
    const next = !open;
    setOpen(next);
    writeOpen(where, next);
  };

  return place(
    <Card className="px-4 pb-3 pt-5 sm:px-5" labelledBy="next-actions-heading">
      <div className="flex items-center gap-3">
        <IconTile busy={data.refreshing} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h2 id="next-actions-heading" className="text-base font-semibold text-slate-900 dark:text-white">Do this next</h2>
            <Refreshing on={data.refreshing} />
          </div>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {scoped.length === 0 ? (
              'Nothing waiting'
            ) : (
              <>
                <span className="font-semibold text-slate-700 dark:text-slate-200"><RollingNumber value={scoped.length} /></span>
                {scoped.length === 1 ? ' step waiting' : ' steps waiting, most urgent first'}
              </>
            )}
          </p>
        </div>
        {canToggle && (
          <button
            type="button"
            onClick={toggle}
            aria-expanded={open}
            aria-controls="next-actions-list"
            className={cn('inline-flex h-9 shrink-0 items-center gap-1 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 transition-colors hover:border-orange-300 hover:bg-orange-50 hover:text-orange-700 dark:border-slate-700 dark:text-slate-200 dark:hover:border-orange-800 dark:hover:bg-orange-950 dark:hover:text-orange-200', focusRing)}
          >
            {open ? 'Show less' : `Show all ${Math.min(scoped.length, MAX_SHOWN)}`}
            <motion.span animate={{ rotate: open ? 180 : 0 }} transition={{ duration: reduce ? 0 : 0.25, ease: EASE }} className="inline-flex">
              <ChevronDown className="h-4 w-4" aria-hidden="true" />
            </motion.span>
          </button>
        )}
      </div>

      {scoped.length === 0 ? (
        <motion.p
          initial={reduce ? false : { opacity: 0, y: 4 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3, delay: 0.1, ease: EASE }}
          className="mt-3 flex items-center gap-2 pb-1 text-sm text-slate-600 dark:text-slate-300"
        >
          <motion.span
            initial={reduce ? false : { scale: 0 }}
            animate={{ scale: 1 }}
            transition={{ type: 'spring', stiffness: 400, damping: 15, delay: 0.15 }}
            className="flex h-6 w-6 items-center justify-center rounded-full bg-emerald-100 text-emerald-600 dark:bg-emerald-950 dark:text-emerald-400"
            aria-hidden="true"
          >
            <Check className="h-4 w-4" />
          </motion.span>
          {scope.mode === 'list' ? 'Nothing on this page is waiting.' : 'You are all caught up.'}
        </motion.p>
      ) : (
        <>
          <ol id="next-actions-list" className="mt-4">
            <AnimatePresence initial={!reduce}>
              {shown.map((item, index) => <Row key={item.key} item={item} index={index} />)}
            </AnimatePresence>
          </ol>
          <AnimatePresence initial={false}>
            {open && beyond > 0 && (
              <motion.p
                key="beyond"
                initial={reduce ? false : { opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0, transition: { duration: reduce ? 0 : 0.15 } }}
                className="flex items-center gap-1.5 pb-1 text-xs text-slate-500 dark:text-slate-400"
              >
                <Sparkles className="h-3.5 w-3.5 text-amber-500" aria-hidden="true" />
                {beyond} more after these. Finish one and the next moves up.
              </motion.p>
            )}
          </AnimatePresence>
        </>
      )}
      <AskBox lists={data.lists} items={scoped} scope={scope} />
    </Card>,
  );
}
