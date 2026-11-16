import type { FormEvent } from 'react';
import { Activity as ActivityIcon, CheckCircle2, Mail, Pencil, Phone, Plus, Users } from 'lucide-react';
import type { Activity } from '@/app/api/types';
import { ActivityFactsFields } from '@/app/components/ActivityFactsFields';
import { MentionTextarea, useWorkspacePeople } from '@/app/components/MentionTextarea';
import { activityFactsLine, activityTime, factsProblem, EMPTY_FACTS, type ActivityFactsDraft } from '@/app/lib/activityFacts';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import { currentDateLocale } from '@/app/i18n/current';

/** Each kind of activity in its own colour and icon, the same as on the lead page. */
const KINDS: Record<string, { icon: typeof Phone; tone: string }> = {
  Call: { icon: Phone, tone: 'from-sky-400 to-blue-600' },
  Email: { icon: Mail, tone: 'from-purple-500 to-fuchsia-600' },
  Meeting: { icon: Users, tone: 'from-amber-400 to-orange-500' },
  Task: { icon: CheckCircle2, tone: 'from-emerald-400 to-teal-600' },
  Note: { icon: Pencil, tone: 'from-slate-400 to-slate-500' },
};

/** "call" and "Call" are the same kind of activity. */
export function activityKind(type: string | undefined): string {
  const t = (type ?? '').trim();
  return t ? t.charAt(0).toUpperCase() + t.slice(1).toLowerCase() : 'Note';
}

export function ActivityKindIcon({ type }: { type: string | undefined }) {
  const kind = KINDS[activityKind(type)];
  const Icon = kind?.icon ?? ActivityIcon;
  return (
    <span
      className={cn('flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br text-white shadow-sm', kind?.tone ?? 'from-slate-300 to-slate-400')}
      aria-hidden
    >
      <Icon className="h-4 w-4" />
    </span>
  );
}

/**
 * A newly logged activity in its place in a list that runs newest first by when things
 * happened: a call logged now for this morning goes below what happened since.
 */
export function withActivity(list: Activity[], activity: Activity): Activity[] {
  const at = Date.parse(activityTime(activity));
  const index = list.findIndex((other) => Date.parse(activityTime(other)) <= at);
  return index === -1 ? [...list, activity] : [...list.slice(0, index), activity, ...list.slice(index)];
}

/** One logged activity in a record's list, rising into place in turn. */
export function ActivityRow({ activity, index }: { activity: Activity; index: number }) {
  const kind = activityKind(activity.type);
  const facts = activityFactsLine(activity);
  const when = new Date(activityTime(activity));
  return (
    <StaggerItem
      as="li"
      index={index}
      className={cn('flex gap-3 rounded-xl border border-slate-100 bg-white p-3 dark:border-slate-600 dark:bg-slate-700/50', liftOnHover)}
    >
      <ActivityKindIcon type={activity.type} />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{activity.subject || kind}</p>
        <p className="mt-0.5 text-xs text-slate-500">
          {kind}{facts && <> &middot; {facts}</>} &middot;{' '}
          <time dateTime={when.toISOString()}>
            {when.toLocaleDateString(currentDateLocale(), { month: 'short', day: 'numeric', year: 'numeric' })}{' '}
            {when.toLocaleTimeString(currentDateLocale(), { hour: 'numeric', minute: '2-digit' })}
          </time>
          {activity.createdByName && <> &middot; by {activity.createdByName}</>}
        </p>
        {activity.body && <p className="mt-1 line-clamp-3 text-xs text-slate-600 dark:text-slate-400">{activity.body}</p>}
      </div>
    </StaggerItem>
  );
}

export interface ActivityDraft {
  type: string;
  subject: string;
  body: string;
  /** When it happened and how it went; absent means now, with nothing more said. */
  facts?: ActivityFactsDraft;
}

/** The "log an activity" box at the top of a record's activity list. */
export function ActivityLogForm({ draft, onChange, saving, onSubmit }: {
  draft: ActivityDraft;
  onChange: (next: ActivityDraft) => void;
  saving: boolean;
  onSubmit: (e: FormEvent) => void;
}) {
  const facts = draft.facts ?? EMPTY_FACTS;
  const problem = factsProblem(draft.type, facts);
  const people = useWorkspacePeople();
  return (
    <form
      onSubmit={(e) => {
        if (problem) {
          e.preventDefault();
          return;
        }
        onSubmit(e);
      }}
      className="mb-4 rounded-xl border border-slate-200 bg-slate-50/70 p-3 transition-[border-color,box-shadow] duration-300 focus-within:border-emerald-300 focus-within:shadow-md focus-within:shadow-emerald-500/10 dark:border-slate-600 dark:bg-slate-700/50"
    >
      <div className="mb-2 flex gap-2">
        <Select value={draft.type} onValueChange={(v) => onChange({ ...draft, type: v })}>
          <SelectTrigger aria-label="Activity type" className="h-9 w-28 bg-white text-sm dark:bg-slate-800"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="note">Note</SelectItem>
            <SelectItem value="call">Call</SelectItem>
            <SelectItem value="email">Email</SelectItem>
            <SelectItem value="meeting">Meeting</SelectItem>
            <SelectItem value="task">Task</SelectItem>
          </SelectContent>
        </Select>
        <Input
          placeholder="Subject..."
          value={draft.subject}
          onChange={(e) => onChange({ ...draft, subject: e.target.value })}
          className="h-9 flex-1 bg-white text-sm dark:bg-slate-800"
        />
      </div>
      <MentionTextarea
        aria-label="Details"
        placeholder="Details (optional)... @mention a teammate to tell them"
        value={draft.body}
        onChange={(body) => onChange({ ...draft, body })}
        people={people}
        className="w-full resize-none rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-emerald-400 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 dark:border-slate-600 dark:bg-slate-800"
        rows={2}
      />
      <ActivityFactsFields
        className="mt-2"
        type={draft.type}
        value={facts}
        onChange={(next) => onChange({ ...draft, facts: next })}
      />
      {problem && <p role="alert" className="mt-2 text-xs text-rose-600">{problem}</p>}
      <div className="mt-2 flex justify-end">
        <Button
          type="submit"
          size="sm"
          disabled={saving || !draft.subject.trim() || !!problem}
          className="gap-1 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 text-white shadow-sm hover:opacity-95"
        >
          <Plus className="h-3.5 w-3.5" />
          {saving ? 'Saving...' : 'Log Activity'}
        </Button>
      </div>
    </form>
  );
}
