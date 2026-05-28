import { useMemo } from 'react';
import {
  Phone, Mail, Calendar, FileText, RefreshCw, MessageSquare,
} from 'lucide-react';
import type { Activity } from '@/app/api/types';
import { cn } from '@/app/components/ui/utils';

interface TypeStyle {
  icon: typeof Phone;
  tone: string;       // tailwind classes for the icon "node"
  label: string;
}

const TYPE_STYLES: Record<string, TypeStyle> = {
  call:    { icon: Phone,         tone: 'bg-blue-100 text-blue-700 ring-blue-200',         label: 'Call' },
  email:   { icon: Mail,          tone: 'bg-purple-100 text-purple-700 ring-purple-200',   label: 'Email' },
  meeting: { icon: Calendar,      tone: 'bg-emerald-100 text-emerald-700 ring-emerald-200',label: 'Meeting' },
  note:    { icon: FileText,      tone: 'bg-amber-100 text-amber-700 ring-amber-200',      label: 'Note' },
  system:  { icon: RefreshCw,     tone: 'bg-slate-100 text-slate-500 ring-slate-200',      label: 'Update' },
};

const FALLBACK_STYLE: TypeStyle = {
  icon: MessageSquare,
  tone: 'bg-slate-100 text-slate-600 ring-slate-200',
  label: 'Activity',
};

function styleFor(type: string): TypeStyle {
  return TYPE_STYLES[type?.toLowerCase()] ?? FALLBACK_STYLE;
}

function startOfDayKey(d: Date): string {
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
}

function relativeDayLabel(d: Date): string {
  const today = new Date();
  const todayKey = startOfDayKey(today);
  const yesterday = new Date(today);
  yesterday.setDate(yesterday.getDate() - 1);
  const yesterdayKey = startOfDayKey(yesterday);
  const key = startOfDayKey(d);
  if (key === todayKey) return 'Today';
  if (key === yesterdayKey) return 'Yesterday';
  return d.toLocaleDateString(undefined, { weekday: 'long', month: 'short', day: 'numeric', year: today.getFullYear() === d.getFullYear() ? undefined : 'numeric' });
}

function timeLabel(d: Date): string {
  return d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

interface ActivityTimelineProps {
  activities: Activity[];
  emptyHint?: string;
}

/**
 * Activity timeline grouped by day with type-coloured icon nodes on a vertical
 * rail. Read-only on its own — write actions (quick-log, email send) live in
 * the surrounding page.
 */
export function ActivityTimeline({ activities, emptyHint = 'No activity yet.' }: ActivityTimelineProps) {
  const groups = useMemo(() => {
    const sorted = [...activities].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    const map = new Map<string, { day: Date; items: Activity[] }>();
    for (const a of sorted) {
      const d = new Date(a.createdAt);
      const key = startOfDayKey(d);
      const existing = map.get(key);
      if (existing) existing.items.push(a);
      else map.set(key, { day: d, items: [a] });
    }
    return Array.from(map.values());
  }, [activities]);

  if (groups.length === 0) {
    return (
      <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50/40 px-6 py-10 text-center text-sm text-slate-500">
        {emptyHint}
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {groups.map(({ day, items }) => (
        <section key={startOfDayKey(day)}>
          <div className="sticky top-0 z-10 -mx-1 mb-3 flex items-center gap-3 bg-gradient-to-b from-white via-white to-white/90 px-1 py-1 backdrop-blur">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">{relativeDayLabel(day)}</span>
            <span className="h-px flex-1 bg-slate-100" />
            <span className="text-xs text-slate-400">{items.length} item{items.length === 1 ? '' : 's'}</span>
          </div>
          <ol className="relative ml-3 border-l border-slate-200">
            {items.map((activity) => {
              const s = styleFor(activity.type);
              const Icon = s.icon;
              const isSystem = activity.type === 'system';
              const created = new Date(activity.createdAt);
              return (
                <li key={activity.id} className="relative pl-8 pb-5 last:pb-0">
                  <span
                    className={cn(
                      'absolute left-0 top-0 -translate-x-1/2 flex h-7 w-7 items-center justify-center rounded-full ring-4 ring-white shadow-sm',
                      s.tone,
                    )}
                    aria-hidden
                  >
                    <Icon className="h-3.5 w-3.5" />
                  </span>
                  <div
                    className={cn(
                      'rounded-xl border px-4 py-3 transition-colors',
                      isSystem
                        ? 'border-slate-100 bg-slate-50/60'
                        : 'border-slate-200 bg-white hover:border-indigo-200/70 hover:bg-indigo-50/30',
                    )}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className={cn(
                          'inline-flex items-center rounded-md px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider',
                          isSystem ? 'bg-slate-200/70 text-slate-600' : 'bg-slate-100 text-slate-600',
                        )}>
                          {s.label}
                        </span>
                        {activity.subject && (
                          <span className="truncate text-sm font-medium text-slate-800">{activity.subject}</span>
                        )}
                      </div>
                      <span className="shrink-0 text-xs text-slate-400">{timeLabel(created)}</span>
                    </div>
                    {activity.body && (
                      <p className={cn(
                        'mt-1.5 whitespace-pre-wrap text-sm',
                        isSystem ? 'italic text-slate-500' : 'text-slate-700',
                      )}>
                        {activity.body}
                      </p>
                    )}
                    {activity.createdByName && (
                      <p className="mt-2 text-[11px] text-slate-400">by {activity.createdByName}</p>
                    )}
                  </div>
                </li>
              );
            })}
          </ol>
        </section>
      ))}
    </div>
  );
}
