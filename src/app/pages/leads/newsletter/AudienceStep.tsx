// Step one: who receives it. Statuses as chips (the lost and not interested left off), more
// filters a click away, and beside them the server's count, the people it goes to and those
// left out with why.

import { useId, useState, type ReactNode } from 'react';
import {
  CalendarDays, ChevronDown, Inbox, Loader2, RefreshCw, RotateCcw, SlidersHorizontal, Sparkles, Tag, TriangleAlert,
  UserMinus, UserRound, UserX, Users,
} from 'lucide-react';
import type { NewsletterAudience } from '@/app/api/leadNewsletter';
import { DateInput } from '@/app/components/DateInput';
import { Button } from '@/app/components/ui/button';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { cn } from '@/app/components/ui/utils';
import {
  chosenStatuses, defaultStatuses, isOutStatus, leads, leftOutCounts, leftOutTotal, moreFilterCount, reasonInfo, statusColors,
  type AudienceFilters, type CreatedRange,
} from './audience';
import { PersonRow, ProblemNote, Segmented, ToggleChip } from './parts';

export interface NewsletterMember {
  userId: string;
  name: string;
  email?: string;
}

const RANGES: { id: CreatedRange; label: string }[] = [
  { id: 'any', label: 'Any time' },
  { id: '30', label: '30 days' },
  { id: '90', label: '90 days' },
  { id: '365', label: '1 year' },
  { id: 'custom', label: 'Custom' },
];

function toggle(list: string[], item: string): string[] {
  return list.includes(item) ? list.filter((x) => x !== item) : [...list, item];
}

export function AudienceStep({
  filters, onChange, statuses, sources, members, currentUserId, audience, counting, error, onRetry,
}: {
  filters: AudienceFilters;
  onChange: (next: AudienceFilters) => void;
  statuses: string[];
  sources: string[];
  members: NewsletterMember[];
  currentUserId?: string | null;
  audience: NewsletterAudience | null;
  counting: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  const ids = useId();
  const on = chosenStatuses(filters, statuses);
  const [more, setMore] = useState(() => moreFilterCount(filters) > 0);
  const [showLeftOut, setShowLeftOut] = useState(false);
  const set = (change: Partial<AudienceFilters>) => onChange({ ...filters, ...change });
  const asDefault = filters.statuses === null || sameSet(on, defaultStatuses(statuses));
  const extra = moreFilterCount(filters);
  const me = currentUserId ? members.find((m) => m.userId === currentUserId) : undefined;
  const others = members.filter((m) => m.userId !== currentUserId);

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,25rem)]">
      <div className="min-w-0 space-y-5">
        <FilterGroup
          icon={Tag}
          title="Status"
          aside={(
            <div className="ml-auto flex items-center gap-1">
              <Button
                type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs"
                onClick={() => set({ statuses: [...statuses] })}
                disabled={on.length === statuses.length}
              >
                <Users className="h-3.5 w-3.5" aria-hidden /> All
              </Button>
              <Button
                type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs"
                onClick={() => set({ statuses: null })}
                disabled={asDefault}
              >
                <RotateCcw className="h-3.5 w-3.5" aria-hidden /> Reset
              </Button>
            </div>
          )}
        >
          {statuses.map((status) => {
            const pressed = on.includes(status);
            return (
              <ToggleChip
                key={status}
                pressed={pressed}
                dot={statusColors(status).dot}
                icon={!pressed && isOutStatus(status) ? UserX : undefined}
                // Kept in the workspace's order, whatever order they are chosen in.
                onClick={() => set({ statuses: statuses.filter((s) => (s === status ? !pressed : on.includes(s))) })}
              >
                {status}
              </ToggleChip>
            );
          })}
        </FilterGroup>

        <div className="rounded-2xl border border-slate-200 dark:border-white/10">
          <button
            type="button"
            aria-expanded={more}
            aria-controls={`${ids}-more`}
            onClick={() => setMore((v) => !v)}
            className="flex w-full items-center gap-2 rounded-2xl px-4 py-3 text-left text-sm font-medium text-slate-700 transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:text-slate-200 dark:hover:bg-white/5"
          >
            <SlidersHorizontal className="h-4 w-4 text-slate-400" aria-hidden />
            More filters
            {extra > 0 && (
              <span className="rounded-full bg-orange-100 px-1.5 text-xs font-semibold text-orange-700 dark:bg-orange-500/20 dark:text-orange-200">{extra}</span>
            )}
            <ChevronDown className={cn('ml-auto h-4 w-4 text-slate-400 transition-transform motion-reduce:transition-none', more && 'rotate-180')} aria-hidden />
          </button>
          {more && (
            <div id={`${ids}-more`} className="space-y-5 border-t border-slate-200 px-4 py-4 dark:border-white/10">
              {sources.length > 0 && (
                <FilterGroup icon={Sparkles} title="Source">
                  {sources.map((source) => (
                    <ToggleChip key={source} pressed={filters.sources.includes(source)} onClick={() => set({ sources: toggle(filters.sources, source) })}>
                      {source}
                    </ToggleChip>
                  ))}
                </FilterGroup>
              )}

              <FilterGroup icon={UserRound} title="Owner">
                {me && (
                  <ToggleChip pressed={filters.owners.includes(me.userId)} onClick={() => set({ owners: toggle(filters.owners, me.userId) })} label={`My leads (${me.name})`}>
                    Me
                  </ToggleChip>
                )}
                {others.map((m) => (
                  <ToggleChip key={m.userId} pressed={filters.owners.includes(m.userId)} onClick={() => set({ owners: toggle(filters.owners, m.userId) })}>
                    {m.name || m.email}
                  </ToggleChip>
                ))}
                <ToggleChip pressed={filters.unassigned} icon={UserMinus} onClick={() => set({ unassigned: !filters.unassigned })}>
                  Unassigned
                </ToggleChip>
              </FilterGroup>

              <div>
                <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                  <CalendarDays className="h-3.5 w-3.5" aria-hidden /> Added
                </p>
                <Segmented
                  className="mt-2 flex-wrap"
                  label="Added"
                  value={filters.created}
                  options={RANGES}
                  onChange={(created) => set({ created })}
                />
                {filters.created === 'custom' && (
                  <div className="mt-3 grid gap-3 sm:grid-cols-2">
                    <div>
                      <Label htmlFor={`${ids}-from`} className="text-xs">From</Label>
                      <DateInput id={`${ids}-from`} value={filters.createdFrom} onChange={(createdFrom) => set({ createdFrom })} className="mt-1" />
                    </div>
                    <div>
                      <Label htmlFor={`${ids}-until`} className="text-xs">To</Label>
                      <DateInput id={`${ids}-until`} value={filters.createdTo} onChange={(createdTo) => set({ createdTo })} className="mt-1" />
                    </div>
                  </div>
                )}
              </div>

              <div className="flex items-center gap-3">
                <Switch
                  id={`${ids}-converted`}
                  checked={filters.includeConverted}
                  onCheckedChange={(includeConverted) => set({ includeConverted })}
                />
                <Label htmlFor={`${ids}-converted`} className="text-sm font-normal text-slate-700 dark:text-slate-200">Converted leads too</Label>
              </div>
            </div>
          )}
        </div>
      </div>

      <aside aria-label="Who receives it" className="min-w-0 space-y-4">
        <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-orange-500 via-orange-500 to-pink-500 p-5 text-white shadow-lg shadow-orange-500/20">
          <Users className="absolute -right-3 -top-3 h-24 w-24 text-white/10" aria-hidden />
          <p role="status" aria-live="polite" aria-atomic="true" className="relative">
            <span className="block text-5xl font-bold tabular-nums leading-none">
              {on.length === 0 ? 0 : audience ? audience.willReceive.toLocaleString() : '…'}
            </span>{' '}
            <span className="mt-1.5 block text-sm font-medium text-white/90">
              {audience && on.length > 0 ? (audience.willReceive === 1 ? 'lead will receive it' : 'leads will receive it') : 'will receive it'}
            </span>
          </p>
          {audience && on.length > 0 && (
            <p className="relative mt-3 flex flex-wrap gap-x-3 gap-y-1 text-xs text-white/85">
              <span>{audience.matching.toLocaleString()} match</span>
              {leftOutTotal(audience) > 0 && <span>{leftOutTotal(audience).toLocaleString()} left out</span>}
            </p>
          )}
          {counting && <Loader2 className="absolute right-4 top-4 h-4 w-4 animate-spin text-white/80" aria-label="Counting" />}
        </div>

        {error && (
          <ProblemNote
            action={(
              <Button type="button" size="sm" variant="ghost" onClick={onRetry} className="h-7 shrink-0 px-2 text-rose-700 dark:text-rose-200">
                <RefreshCw className="h-3.5 w-3.5" aria-hidden /> Retry
              </Button>
            )}
          >
            {error}
          </ProblemNote>
        )}

        {on.length === 0 ? (
          <EmptyNote icon={Tag}>Pick at least one status.</EmptyNote>
        ) : audience && audience.willReceive > audience.maxRecipients ? (
          <div role="alert" className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
            <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
            <span>One newsletter goes to at most {leads(audience.maxRecipients)}. Narrow the filters.</span>
          </div>
        ) : audience && audience.willReceive === 0 ? (
          <EmptyNote icon={Inbox}>Nobody to send to with these filters.</EmptyNote>
        ) : null}

        {audience && on.length > 0 && audience.recipients.length > 0 && (
          <section aria-labelledby={`${ids}-to`}>
            <h3 id={`${ids}-to`} className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Receives it</h3>
            <ul className="divide-y divide-slate-100 overflow-hidden rounded-xl border border-slate-200 bg-white dark:divide-white/5 dark:border-white/10 dark:bg-white/[0.02]">
              {audience.recipients.map((p) => <PersonRow key={p.leadId} person={p} />)}
            </ul>
            {audience.willReceive > audience.recipients.length && (
              <p className="mt-1.5 px-1 text-xs text-slate-500 dark:text-slate-400">
                + {(audience.willReceive - audience.recipients.length).toLocaleString()} more
              </p>
            )}
          </section>
        )}

        {audience && on.length > 0 && leftOutTotal(audience) > 0 && (
          <section className="rounded-xl border border-slate-200 dark:border-white/10">
            <button
              type="button"
              aria-expanded={showLeftOut}
              aria-controls={`${ids}-left-out`}
              onClick={() => setShowLeftOut((v) => !v)}
              className="flex w-full items-center gap-2 rounded-xl px-3 py-2.5 text-left text-sm font-medium text-slate-700 transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:text-slate-200 dark:hover:bg-white/5"
            >
              <UserX className="h-4 w-4 text-slate-400" aria-hidden />
              {leftOutTotal(audience).toLocaleString()} left out
              <ChevronDown className={cn('ml-auto h-4 w-4 text-slate-400 transition-transform motion-reduce:transition-none', showLeftOut && 'rotate-180')} aria-hidden />
            </button>
            {showLeftOut && (
              <div id={`${ids}-left-out`} className="border-t border-slate-200 dark:border-white/10">
                <ul aria-label="Why they are left out" className="flex flex-wrap gap-1.5 px-3 py-2.5">
                  {leftOutCounts(audience).map(({ reason, count }) => {
                    const info = reasonInfo(reason);
                    return (
                      <li key={reason} className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700 dark:bg-white/5 dark:text-slate-200">
                        <info.icon className="h-3.5 w-3.5 text-slate-500" aria-hidden />
                        {info.label}
                        <span className="tabular-nums text-slate-500 dark:text-slate-400">{count.toLocaleString()}</span>
                      </li>
                    );
                  })}
                </ul>
                {audience.leftOut.length > 0 && (
                  <ul aria-label="Left out" className="divide-y divide-slate-100 border-t border-slate-100 dark:divide-white/5 dark:border-white/5">
                    {audience.leftOut.map((p) => <PersonRow key={p.leadId} person={p} leftOut />)}
                  </ul>
                )}
                {leftOutTotal(audience) > audience.leftOut.length && (
                  <p className="px-3 pb-2.5 pt-1 text-xs text-slate-500 dark:text-slate-400">
                    + {(leftOutTotal(audience) - audience.leftOut.length).toLocaleString()} more
                  </p>
                )}
              </div>
            )}
          </section>
        )}
      </aside>
    </div>
  );
}

/** A few chips that belong together, under a small heading of their own. */
function FilterGroup({ icon: Icon, title, aside, children }: {
  icon: typeof Tag;
  title: string;
  aside?: ReactNode;
  children: ReactNode;
}) {
  const id = useId();
  return (
    <div role="group" aria-labelledby={id}>
      <div className="flex flex-wrap items-center gap-2">
        <h3 id={id} className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
          <Icon className="h-3.5 w-3.5" aria-hidden /> {title}
        </h3>
        {aside}
      </div>
      <div className="mt-2 flex flex-wrap gap-2">{children}</div>
    </div>
  );
}

function EmptyNote({ icon: Icon, children }: { icon: typeof Inbox; children: string }) {
  return (
    <p className="flex items-center gap-2.5 rounded-xl border border-dashed border-slate-300 p-4 text-sm text-slate-500 dark:border-white/15 dark:text-slate-400">
      <Icon className="h-4 w-4 shrink-0" aria-hidden />
      {children}
    </p>
  );
}

function sameSet(a: readonly string[], b: readonly string[]): boolean {
  return a.length === b.length && a.every((x) => b.includes(x));
}
