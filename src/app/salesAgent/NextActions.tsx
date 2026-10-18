import { useMemo, useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Check, ChevronDown, Compass, Loader2, PenLine, RotateCw } from 'lucide-react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { cn } from '@/app/components/ui/utils';
import { reloadAgentData, useAgentData, type AgentLists } from './agentData';
import { pageScope } from './pageScope';
import { dealPlan, type PlanLine } from './recordPlan';
import { collectWork, type WorkSuggestion } from './workspace';
import type { WriterBrief } from './writerBrief';

/**
 * The sales agent's panel: the next thing worth doing, from what is already saved.
 *
 * It sits under the header on every working page, so it has to earn its space.
 * It used to be a large card on every page, re-read eight lists on every
 * navigation, never noticed an edit made beneath it, and on a lead page repeated
 * the five-phase lifecycle tracker the page already shows — pushing the lead
 * itself below the fold. Now:
 *
 *   · the lists come from one shared store that refreshes itself after any save
 *     (agentData.ts), so the panel agrees with the page it sits on;
 *   · on the Dashboard — the agent's home — it opens with the full queue;
 *     elsewhere it shows the single top step, with the rest one click away, and
 *     remembers which way you left it;
 *   · on a record it shows that record's one next step, and nothing at all when
 *     there is nothing to do.
 *
 * "Draft message" only fills the sales writer. Nothing is ever sent from here.
 */

const MAX_SHOWN = 5;

function recipientFor(item: WorkSuggestion): { type: 'lead' | 'contact' | 'deal'; id: string; name: string } | null {
  const match = /^\/(leads|contacts|deals)\/([^/]+)$/.exec(item.href);
  if (!match?.[1] || !match[2]) return null;
  const type = match[1] === 'leads' ? 'lead' : match[1] === 'deals' ? 'deal' : 'contact';
  return { type, id: decodeURIComponent(match[2]), name: item.name };
}

function inList(item: WorkSuggestion, list: string): boolean {
  if (list === 'Leads') return item.href.startsWith('/leads/');
  if (list === 'Deals') return item.href.startsWith('/deals/');
  return item.list === list;
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

const shell = 'rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900';
const focusRing = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2 dark:focus-visible:ring-offset-slate-900';

function Wrapper({ children }: { children: ReactNode }) {
  return <div className="w-full px-[var(--page-padding)] pt-4">{children}</div>;
}

function DraftLink({ item, compact = false }: { item: WorkSuggestion; compact?: boolean }) {
  if (!item.brief) return null;
  return (
    <Link
      to="/dashboard"
      state={{ writerBrief: item.brief satisfies WriterBrief, writerRecipient: recipientFor(item) }}
      title="Opens the sales writer with a draft. Nothing is sent."
      className={cn(
        'inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700',
        'hover:border-[var(--brand-primary)] hover:text-[var(--brand-primary-dark)]',
        'dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100',
        compact ? 'h-9' : 'h-10',
        focusRing,
      )}
    >
      <PenLine className="h-4 w-4" aria-hidden="true" />
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
        'inline-flex h-9 shrink-0 items-center rounded-lg bg-[var(--brand-primary)] px-3 text-sm font-semibold text-white',
        'hover:bg-[var(--brand-primary-dark)]',
        focusRing,
      )}
    >
      Open<span className="sr-only"> {item.name}</span>
    </Link>
  );
}

function Refreshing({ on }: { on: boolean }) {
  if (!on) return null;
  return (
    <span className="inline-flex items-center gap-1 text-xs font-normal text-slate-400" role="status">
      <Loader2 className="h-3.5 w-3.5 motion-safe:animate-spin" aria-hidden="true" />
      <span className="sr-only">Updating next steps</span>
    </span>
  );
}

/** One suggestion: who, what, why — then the two things you can do about it. */
function Row({ item, index }: { item: WorkSuggestion; index: number }) {
  return (
    <li className="flex gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-950">
      <span
        className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[var(--brand-primary)] text-xs font-bold text-white"
        aria-hidden="true"
      >
        {index + 1}
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
        <div className="min-w-0 flex-1">
          <p className="text-sm text-slate-900 dark:text-white">
            <Link to={item.href} className={cn('rounded-sm font-semibold hover:text-[var(--brand-primary-dark)]', focusRing)}>
              {item.name}
            </Link>
            <span className="ml-2 rounded-full bg-slate-200/70 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">
              {item.list}
            </span>
          </p>
          <p className="mt-0.5 text-sm text-slate-800 dark:text-slate-200">{item.action}</p>
          <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">{item.because}</p>
        </div>
        <div className="flex shrink-0 flex-wrap gap-2">
          <OpenLink item={item} />
          <DraftLink item={item} compact />
        </div>
      </div>
    </li>
  );
}

// ── the record strip ─────────────────────────────────────────────────────────

const STEP_TONE: Record<PlanLine['state'], string> = {
  done: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300',
  now: 'border-[var(--brand-primary)] bg-white text-slate-900 ring-1 ring-[var(--brand-primary)] dark:bg-slate-900 dark:text-white',
  later: 'border-slate-200 bg-white text-slate-400 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-500',
  stopped: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900 dark:bg-rose-950 dark:text-rose-300',
};
const STEP_WORD: Record<PlanLine['state'], string> = { done: 'done', now: 'current stage', later: 'not reached', stopped: 'stopped' };

/** The deal's stage ladder, in one row. Leads skip it: their page has the full lifecycle. */
function Ladder({ lines }: { lines: PlanLine[] }) {
  return (
    <ol className="mt-3 flex flex-wrap items-center gap-1.5" aria-label="Deal stages">
      {lines.map((line) => (
        <li
          key={`${line.state}-${line.title}`}
          className={cn('inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium', STEP_TONE[line.state])}
        >
          {line.state === 'done' && <Check className="h-3 w-3" aria-hidden="true" />}
          {line.title}
          <span className="sr-only">, {STEP_WORD[line.state]}</span>
        </li>
      ))}
    </ol>
  );
}

function RecordStrip({ href, step, lists, refreshing }: {
  href: string;
  step: WorkSuggestion;
  lists: AgentLists;
  refreshing: boolean;
}) {
  const isDeal = href.startsWith('/deals/');
  const dealId = isDeal ? decodeURIComponent(href.split('/').pop() ?? '') : '';
  const deal = isDeal ? lists.deals.find((item) => item.id === dealId) : undefined;
  const ladder = deal ? dealPlan(deal).lines : null;

  return (
    <section className={cn(shell, 'mb-4 px-4 py-3')} aria-labelledby="next-actions-heading">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:gap-4">
        <Compass className="hidden h-5 w-5 shrink-0 text-[var(--brand-primary)] sm:block" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          <h2 id="next-actions-heading" className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Next for this {recordNoun(href)}
            <Refreshing on={refreshing} />
          </h2>
          <p className="mt-0.5 text-sm font-medium text-slate-900 dark:text-white">{step.action}</p>
          <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">{step.because}</p>
        </div>
        <DraftLink item={step} />
      </div>
      {ladder && <Ladder lines={ladder} />}
    </section>
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

  // Recomputed only when the lists change — not on every render of every page.
  const allItems = useMemo(
    () => (data.lists ? collectWork(data.lists, new Date(), Number.MAX_SAFE_INTEGER) : []),
    [data.lists],
  );

  if (scope.mode === 'hidden') return null;

  if (!data.lists) {
    if (data.failed) {
      return (
        <Wrapper>
          <div className={cn(shell, 'mb-4 flex items-center justify-between gap-3 px-4 py-3')} role="status">
            <p className="text-sm text-slate-600 dark:text-slate-300">Next steps could not be loaded.</p>
            <button
              type="button"
              onClick={reloadAgentData}
              className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 px-3 text-sm font-semibold text-slate-700 hover:border-[var(--brand-primary)] dark:border-slate-600 dark:text-slate-100', focusRing)}
            >
              <RotateCw className="h-4 w-4" aria-hidden="true" />
              Try again
            </button>
          </div>
        </Wrapper>
      );
    }
    // A slim placeholder the size of the closed panel, so the page does not
    // jump when the steps arrive. Only ever shown before the very first load.
    return (
      <Wrapper>
        <div className={cn(shell, 'mb-4 flex h-14 items-center gap-3 px-4')} aria-busy="true" aria-label="Loading next steps">
          <div className="h-5 w-5 rounded-full bg-slate-100 dark:bg-slate-800" />
          <div className="h-4 w-48 rounded bg-slate-100 dark:bg-slate-800" />
        </div>
      </Wrapper>
    );
  }

  if (scope.mode === 'record') {
    const step = allItems.find((item) => item.href === scope.href);
    // Nothing owed on this record: render nothing at all — not even the spacing.
    // The page already shows its state, and a card (or a gap) on every finished
    // record is noise.
    if (!step) return null;
    return (
      <Wrapper>
        <RecordStrip href={scope.href} step={step} lists={data.lists} refreshing={data.refreshing} />
      </Wrapper>
    );
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

  return (
    <Wrapper>
      <section className={cn(shell, 'mb-4 p-4')} aria-labelledby="next-actions-heading">
        <div className="flex items-center gap-2">
          <Compass className="h-5 w-5 shrink-0 text-[var(--brand-primary)]" aria-hidden="true" />
          <h2 id="next-actions-heading" className="text-base font-semibold text-slate-900 dark:text-white">Do this next</h2>
          {scoped.length > 0 && (
            <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 dark:bg-slate-800 dark:text-slate-300">
              {scoped.length}
              <span className="sr-only"> {scoped.length === 1 ? 'step' : 'steps'} waiting</span>
            </span>
          )}
          <Refreshing on={data.refreshing} />
          {canToggle && (
            <button
              type="button"
              onClick={toggle}
              aria-expanded={open}
              aria-controls="next-actions-list"
              className={cn('ml-auto inline-flex h-8 items-center gap-1 rounded-lg px-2 text-sm font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800', focusRing)}
            >
              {open ? 'Show less' : `Show all ${Math.min(scoped.length, MAX_SHOWN)}`}
              <ChevronDown className={cn('h-4 w-4 transition-transform motion-reduce:transition-none', open && 'rotate-180')} aria-hidden="true" />
            </button>
          )}
        </div>

        {scoped.length === 0 ? (
          <p className="mt-2 flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
            <Check className="h-4 w-4 text-emerald-600" aria-hidden="true" />
            {scope.mode === 'list' ? 'Nothing on this page is waiting.' : 'You are all caught up.'}
          </p>
        ) : (
          <>
            <ol id="next-actions-list" className="mt-3 space-y-2">
              {shown.map((item, index) => <Row key={item.key} item={item} index={index} />)}
            </ol>
            {open && beyond > 0 && (
              <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
                {beyond} more after these. Finish one and the next moves up.
              </p>
            )}
          </>
        )}
      </section>
    </Wrapper>
  );
}
