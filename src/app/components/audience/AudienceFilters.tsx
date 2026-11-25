// Who a campaign or an event's invitations are for: the one audience of members both use, as
// filters staff set. The server works out who that is (MemberAudienceService); this only sets
// the filters and shows them.
//
// Tags and custom fields use the Members list's own filter bar, so a club's "Interests" field
// (wine, art, golf…) is chosen here exactly as it is filtered on the Members page.

import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Lightbulb } from 'lucide-react';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { cn } from '@/app/components/ui/utils';
import { FlexFilterBar } from '@/app/components/flex/FlexFilterBar';
import { appendFlexFilters, readFlexParams } from '@/app/api/flexFilters';
import { flexOf, withFlex, type MemberAudienceFilter } from '@/app/api/memberAudience';
import { MEMBER_STATUSES } from '@/app/api/members';
import type { ClubEvent } from '@/app/api/events';
import { useFieldDefinitions, useSavedViews } from '@/app/hooks/queries';
import { useT } from '@/app/i18n';
import { currentDateLocale } from '@/app/i18n/current';

const chip = (on: boolean) =>
  `text-xs px-3 py-1.5 rounded-full transition ${on ? 'bg-fuchsia-100 text-fuchsia-800 ring-2 ring-fuchsia-300' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`;

const toggle = (list: string[] | null | undefined, value: string) =>
  (list ?? []).includes(value) ? (list ?? []).filter((x) => x !== value) : [...(list ?? []), value];

/**
 * Where a club's interests come from, said where people look for them: when the workspace has
 * no member fields yet, how to add an "Interests" field to choose members by.
 */
export function InterestsHint({ className }: { className?: string }) {
  const { fields, isSuccess } = useFieldDefinitions('member');
  const { tx } = useT();
  if (!isSuccess || fields.length > 0) return null;
  return (
    <p role="note" className={cn('flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900', className)}>
      <Lightbulb className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden />
      <span>
        {tx('members.interestsHint', {
          link: (words) => <Link to="/settings?tab=customFields" className="font-medium underline underline-offset-2">{words}</Link>,
        })}
      </span>
    </p>
  );
}

function NumberInput({ id, label, value, onChange, min = 1, max }: {
  id: string; label: string; value: number | null | undefined; onChange: (n: number | null) => void; min?: number; max: number;
}) {
  return (
    <Input
      id={id}
      aria-label={label}
      type="number"
      inputMode="numeric"
      min={min}
      max={max}
      value={value ?? ''}
      onChange={(e) => {
        const n = Number.parseInt(e.target.value, 10);
        onChange(Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : null);
      }}
      className="h-8 w-20 px-2 text-sm"
    />
  );
}

export interface AudienceFiltersProps {
  value: MemberAudienceFilter;
  onChange: (next: MemberAudienceFilter) => void;
  /** The club's tiers, to choose from. */
  tiers: string[];
  /** The club's events, for "came to any of these"; those that have started are offered. */
  events?: ClubEvent[];
  /** Ids prefix, so two forms on one page do not share ids. */
  idPrefix?: string;
}

export function AudienceFilters({ value, onChange, tiers, events = [], idPrefix = 'audience' }: AudienceFiltersProps) {
  const set = (patch: Partial<MemberAudienceFilter>) => onChange({ ...value, ...patch });
  const views = useSavedViews('members');
  const params = useMemo(() => appendFlexFilters(new URLSearchParams(), flexOf(value)), [value]);
  // Events that have started, newest first: the ones a member could have come to.
  const past = useMemo(() => events
    .filter((e) => Date.parse(e.startAtUtc) <= Date.now() && e.status !== 'Cancelled')
    .sort((a, b) => Date.parse(b.startAtUtc) - Date.parse(a.startAtUtc))
    .slice(0, 30), [events]);
  const day = (iso: string) => {
    try {
      return new Date(iso).toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'short', year: 'numeric' });
    } catch {
      return iso;
    }
  };
  const id = (name: string) => `${idPrefix}-${name}`;

  return (
    <div className="space-y-4">
      <fieldset>
        <legend className="text-sm font-medium text-slate-800">Tiers</legend>
        <div className="mt-1 flex flex-wrap gap-1.5">
          {tiers.map((t) => {
            const on = (value.tiers ?? []).includes(t);
            return <button key={t} type="button" aria-pressed={on} onClick={() => set({ tiers: toggle(value.tiers, t) })} className={chip(on)}>{t}</button>;
          })}
        </div>
        <p className="mt-1 text-xs text-slate-500">None chosen: every tier.</p>
      </fieldset>

      <fieldset>
        <legend className="text-sm font-medium text-slate-800">Statuses</legend>
        <div className="mt-1 flex flex-wrap gap-1.5">
          {MEMBER_STATUSES.map((s) => {
            const on = (value.statuses ?? []).includes(s);
            return <button key={s} type="button" aria-pressed={on} onClick={() => set({ statuses: toggle(value.statuses, s) })} className={chip(on)}>{s}</button>;
          })}
        </div>
        <p className="mt-1 text-xs text-slate-500">Members in other statuses are counted as left out.</p>
      </fieldset>

      <fieldset>
        <legend className="text-sm font-medium text-slate-800">Interests and tags</legend>
        <FlexFilterBar
          entity="member"
          params={params}
          onChange={(next) => onChange(withFlex(value, readFlexParams(next)))}
          className="mt-1"
        />
        <InterestsHint className="mt-2" />
      </fieldset>

      <div>
        <Label htmlFor={id('view')} className="text-sm font-medium text-slate-800">Start from a saved Members view</Label>
        <select
          id={id('view')}
          className="mt-1 h-9 w-full rounded-md border border-slate-200 bg-white px-2 text-sm"
          value={value.viewId ?? ''}
          onChange={(e) => set({ viewId: e.target.value || null, view: null })}
        >
          <option value="">No view</option>
          {(views.data ?? []).map((v) => <option key={v.id} value={v.id}>{v.name}{v.shared && !v.mine ? ' (shared)' : ''}</option>)}
          {/* A view kept by a saved campaign that this person cannot open (any more) still shows what it is. */}
          {value.viewId && !(views.data ?? []).some((v) => v.id === value.viewId) && (
            <option value={value.viewId}>{value.view?.name ?? 'A view you cannot open'}</option>
          )}
        </select>
        {views.isError && <p className="mt-1 text-xs text-rose-700">The saved views did not load.</p>}
      </div>

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium text-slate-800">Habits at the club</legend>
        {past.length > 0 && (
          <div>
            <p className="text-xs text-slate-600">Came to any of these events (checked in at the door)</p>
            <ul className="mt-1 max-h-32 space-y-1 overflow-y-auto rounded-md border border-slate-200 p-2" aria-label="Events they came to">
              {past.map((e) => (
                <li key={e.id}>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={(value.attendedEventIds ?? []).includes(e.id)}
                      onChange={() => set({ attendedEventIds: toggle(value.attendedEventIds, e.id) })}
                    />
                    <span className="truncate">{e.name}</span>
                    <span className="shrink-0 text-xs text-slate-500">{day(e.startAtUtc)}</span>
                  </label>
                </li>
              ))}
            </ul>
          </div>
        )}
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <input
            type="checkbox"
            id={id('min-events')}
            checked={value.minEvents != null}
            onChange={(e) => set(e.target.checked ? { minEvents: 2, eventMonths: 6 } : { minEvents: null, eventMonths: null })}
          />
          <label htmlFor={id('min-events')}>Came to at least</label>
          <NumberInput id={id('events-n')} label="Events at least" value={value.minEvents} max={100} onChange={(n) => set({ minEvents: n, eventMonths: value.eventMonths ?? 6 })} />
          <span>events in the last</span>
          <NumberInput id={id('events-months')} label="In the last months" value={value.eventMonths} max={60} onChange={(n) => set({ eventMonths: n, minEvents: value.minEvents ?? 1 })} />
          <span>months</span>
        </div>
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <input
            type="checkbox"
            id={id('min-visits')}
            checked={value.minVisits != null}
            onChange={(e) => set(e.target.checked ? { minVisits: 4, visitDays: 30 } : { minVisits: null, visitDays: null })}
          />
          <label htmlFor={id('min-visits')}>Visited at least</label>
          <NumberInput id={id('visits-n')} label="Visits at least" value={value.minVisits} max={1000} onChange={(n) => set({ minVisits: n, visitDays: value.visitDays ?? 30 })} />
          <span>times in the last</span>
          <NumberInput id={id('visits-days')} label="Visits in the last days" value={value.visitDays} max={730} onChange={(n) => set({ visitDays: n, minVisits: value.minVisits ?? 1 })} />
          <span>days</span>
        </div>
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <input
            type="checkbox"
            id={id('not-visited')}
            checked={value.notVisitedDays != null}
            onChange={(e) => set({ notVisitedDays: e.target.checked ? 60 : null })}
          />
          <label htmlFor={id('not-visited')}>Has not visited in the last</label>
          <NumberInput id={id('away-days')} label="Not visited in the last days" value={value.notVisitedDays} max={730} onChange={(n) => set({ notVisitedDays: n })} />
          <span>days</span>
        </div>
      </fieldset>
    </div>
  );
}
