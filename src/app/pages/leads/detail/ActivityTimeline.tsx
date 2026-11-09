import { useMemo } from 'react';
import { motion } from 'motion/react';
import {
  Phone, Mail, Calendar, FileText, RefreshCw, MessageSquare, Sparkles,
} from 'lucide-react';
import type { Activity } from '@/app/api/types';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { useRegional } from '@/app/hooks/useMoney';
import { daysBetween, dayKeyIn, formatDay, todayKeyIn } from '@/app/lib/zonedTime';
import { activityFactsLine, activityTime } from '@/app/lib/activityFacts';

interface TypeStyle {
  icon: typeof Phone;
  /** The icon node on the rail. */
  tone: string;
  /** The small type chip. */
  chip: string;
  /** The entry's left accent and hover tint, in the same colour. */
  card: string;
  label: string;
}

const TYPE_STYLES: Record<string, TypeStyle> = {
  call:    { icon: Phone,     tone: 'bg-gradient-to-br from-sky-400 to-blue-600 text-white shadow-sky-500/30',         chip: 'bg-sky-50 text-sky-700',         card: 'border-l-sky-400 hover:bg-sky-50/40 hover:border-sky-200',         label: 'Call' },
  email:   { icon: Mail,      tone: 'bg-gradient-to-br from-purple-500 to-fuchsia-600 text-white shadow-fuchsia-500/30', chip: 'bg-fuchsia-50 text-fuchsia-700', card: 'border-l-fuchsia-400 hover:bg-fuchsia-50/40 hover:border-fuchsia-200', label: 'Email' },
  meeting: { icon: Calendar,  tone: 'bg-gradient-to-br from-emerald-400 to-teal-600 text-white shadow-emerald-500/30',   chip: 'bg-emerald-50 text-emerald-700', card: 'border-l-emerald-400 hover:bg-emerald-50/40 hover:border-emerald-200', label: 'Meeting' },
  note:    { icon: FileText,  tone: 'bg-gradient-to-br from-amber-400 to-orange-500 text-white shadow-amber-500/30',     chip: 'bg-amber-50 text-amber-700',     card: 'border-l-amber-400 hover:bg-amber-50/40 hover:border-amber-200',     label: 'Note' },
  system:  { icon: RefreshCw, tone: 'bg-gradient-to-br from-slate-300 to-slate-400 text-white shadow-slate-400/20',      chip: 'bg-slate-200/70 text-slate-600', card: 'border-l-slate-300',                                                   label: 'Update' },
};

const FALLBACK_STYLE: TypeStyle = {
  icon: MessageSquare,
  tone: 'bg-gradient-to-br from-indigo-400 to-violet-500 text-white shadow-indigo-500/30',
  chip: 'bg-indigo-50 text-indigo-700',
  card: 'border-l-indigo-400 hover:bg-indigo-50/40 hover:border-indigo-200',
  label: 'Activity',
};

/** Icons that bob above an empty timeline, so "nothing yet" still looks alive. */
const EMPTY_ICONS = [
  { icon: Phone, className: 'from-sky-400 to-blue-600 shadow-sky-500/30', delay: 0 },
  { icon: Mail, className: 'from-purple-500 to-fuchsia-600 shadow-fuchsia-500/30', delay: 0.25 },
  { icon: Calendar, className: 'from-emerald-400 to-teal-600 shadow-emerald-500/30', delay: 0.5 },
  { icon: FileText, className: 'from-amber-400 to-orange-500 shadow-amber-500/30', delay: 0.75 },
];

function styleFor(type: string): TypeStyle {
  return TYPE_STYLES[type?.toLowerCase()] ?? FALLBACK_STYLE;
}

/**
 * A day's heading: Today, Yesterday, or the day itself. Days are the workspace's, in its
 * time zone, as the reports count them: by this browser's clock, a call made at the club
 * just after midnight was filed under the day before for anyone west of it.
 */
export function relativeDayLabel(dayKey: string, timeZone: string, now: Date = new Date()): string {
  const today = todayKeyIn(timeZone, now);
  const ago = daysBetween(dayKey, today);
  if (ago === 0) return 'Today';
  if (ago === 1) return 'Yesterday';
  const sameYear = dayKey.slice(0, 4) === today.slice(0, 4);
  return formatDay(dayKey, { weekday: 'long', month: 'short', day: 'numeric', year: sameYear ? undefined : 'numeric' });
}

/** The time of day, on the same clock as the day it is filed under. */
function timeLabel(d: Date, timeZone: string): string {
  return d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit', timeZone });
}

interface ActivityTimelineProps {
  activities: Activity[];
  emptyHint?: string;
  /** The smaller line under the hint. */
  emptyDetail?: string;
}

/**
 * Activity timeline grouped by day with type-coloured icon nodes on a vertical
 * rail. Read-only on its own — write actions (quick-log, email send) live in
 * the surrounding page.
 */
export function ActivityTimeline({
  activities,
  emptyHint = 'No activity yet.',
  emptyDetail = 'Calls, emails, meetings, and notes land here as they happen.',
}: ActivityTimelineProps) {
  const reduce = useMotionPreference();
  const { timeZone } = useRegional();
  const groups = useMemo(() => {
    // By when each thing happened: a call logged tonight for this morning sits in the morning.
    const sorted = [...activities].sort((a, b) => Date.parse(activityTime(b)) - Date.parse(activityTime(a)));
    const map = new Map<string, { day: string; items: Activity[] }>();
    for (const a of sorted) {
      const day = dayKeyIn(activityTime(a), timeZone);
      const existing = map.get(day);
      if (existing) existing.items.push(a);
      else map.set(day, { day, items: [a] });
    }
    return Array.from(map.values());
  }, [activities, timeZone]);

  if (groups.length === 0) {
    return (
      <div className="relative overflow-hidden rounded-2xl border border-dashed border-fuchsia-200 bg-gradient-to-br from-white via-fuchsia-50/30 to-sky-50/40 px-6 py-8 text-center">
        <div aria-hidden className="mb-4 flex items-center justify-center gap-3">
          {EMPTY_ICONS.map(({ icon: Icon, className, delay }) => (
            <motion.span
              key={delay}
              className={cn('flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br text-white shadow-lg', className)}
              animate={reduce ? undefined : { y: [0, -7, 0], rotate: [-5, 5, -5] }}
              transition={{ duration: 2.4, repeat: Infinity, ease: 'easeInOut', delay }}
            >
              <Icon className="h-4 w-4" />
            </motion.span>
          ))}
        </div>
        <p className="flex items-center justify-center gap-1.5 text-sm font-medium text-slate-600">
          <Sparkles className="h-4 w-4 text-fuchsia-500" aria-hidden />
          {emptyHint}
        </p>
        <p className="mt-1 text-xs text-slate-500">{emptyDetail}</p>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {groups.map(({ day, items }) => (
        <section key={day}>
          <div className="sticky top-0 z-10 -mx-1 mb-3 flex items-center gap-3 bg-gradient-to-b from-white via-white to-white/90 px-1 py-1 backdrop-blur">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">{relativeDayLabel(day, timeZone)}</span>
            <span className="h-px flex-1 bg-slate-100" />
            <span className="text-xs text-slate-500">{items.length} item{items.length === 1 ? '' : 's'}</span>
          </div>
          <ol className="relative ml-3">
            {/* The rail: a soft line of colour the entries hang off. */}
            <span aria-hidden className="absolute bottom-2 left-0 top-2 w-0.5 -translate-x-1/2 rounded-full bg-gradient-to-b from-fuchsia-300 via-sky-300 to-emerald-200" />
            {items.map((activity, index) => {
              const s = styleFor(activity.type);
              const Icon = s.icon;
              const isSystem = activity.type === 'system';
              const created = new Date(activityTime(activity));
              const facts = activityFactsLine(activity);
              const creatorInitial = activity.createdByName?.trim().charAt(0).toUpperCase();
              return (
                <motion.li
                  key={activity.id}
                  className="group/entry relative pl-8 pb-5 last:pb-0"
                  initial={reduce ? false : { opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ duration: 0.35, ease: [0.16, 1, 0.3, 1], delay: reduce ? 0 : Math.min(index, 8) * 0.05 }}
                >
                  <span
                    className={cn(
                      'absolute left-0 top-0 -translate-x-1/2 flex h-7 w-7 items-center justify-center rounded-full shadow-md ring-4 ring-white transition-transform duration-200 group-hover/entry:scale-110',
                      s.tone,
                    )}
                    aria-hidden
                  >
                    <Icon className="h-3.5 w-3.5" />
                  </span>
                  <div
                    className={cn(
                      'rounded-xl border border-l-4 px-4 py-3 transition-all',
                      isSystem
                        ? 'border-slate-100 bg-slate-50/60'
                        : 'border-slate-200 bg-white hover:-translate-y-0.5 hover:shadow-md',
                      // Last, so the type's left edge is not merged away by the grey border.
                      s.card,
                    )}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className={cn(
                          'inline-flex items-center rounded-md px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider',
                          s.chip,
                        )}>
                          {s.label}
                        </span>
                        {activity.subject && (
                          <span className="truncate text-sm font-medium text-slate-800">{activity.subject}</span>
                        )}
                      </div>
                      <span className="shrink-0 text-xs text-slate-500">{timeLabel(created, timeZone)}</span>
                    </div>
                    {facts && <p className="mt-1 text-xs font-medium text-slate-500">{facts}</p>}
                    {activity.body && (
                      <p className={cn(
                        'mt-1.5 whitespace-pre-wrap text-sm',
                        isSystem ? 'italic text-slate-500' : 'text-slate-700',
                      )}>
                        {activity.body}
                      </p>
                    )}
                    {activity.createdByName && (
                      <div className="mt-2 flex items-center gap-1.5">
                        <span
                          className={cn(
                            'flex h-5 w-5 items-center justify-center rounded-full text-[10px] font-semibold text-white ring-2 ring-white shadow-sm',
                            isSystem ? 'bg-slate-400' : 'bg-gradient-to-br from-indigo-500 to-purple-500',
                          )}
                          aria-hidden
                        >
                          {creatorInitial ?? '?'}
                        </span>
                        <span className="text-[11px] text-slate-500">{activity.createdByName}</span>
                      </div>
                    )}
                  </div>
                </motion.li>
              );
            })}
          </ol>
        </section>
      ))}
    </div>
  );
}
