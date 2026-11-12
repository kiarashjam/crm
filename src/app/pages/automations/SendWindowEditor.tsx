import { Input } from '@/app/components/ui/input';
import { cn } from '@/app/components/ui/utils';
import type { SendWindow } from '@/app/api/automations';

/** The days of the week as the server counts them: 1 Monday … 7 Sunday. */
export const WEEKDAYS: { day: number; short: string; long: string }[] = [
  { day: 1, short: 'Mon', long: 'Monday' },
  { day: 2, short: 'Tue', long: 'Tuesday' },
  { day: 3, short: 'Wed', long: 'Wednesday' },
  { day: 4, short: 'Thu', long: 'Thursday' },
  { day: 5, short: 'Fri', long: 'Friday' },
  { day: 6, short: 'Sat', long: 'Saturday' },
  { day: 7, short: 'Sun', long: 'Sunday' },
];

export const DEFAULT_WINDOW: SendWindow = { days: [1, 2, 3, 4, 5], start: '09:00', end: '17:00' };

/** The window in a few words: "Mon–Fri 09:00–17:00", "Mon, Wed 08:00–12:00". */
export function describeWindow(window: SendWindow): string {
  const days = [...new Set(window.days)].filter((d) => d >= 1 && d <= 7).sort((a, b) => a - b);
  const names = days.map((d) => WEEKDAYS[d - 1]?.short ?? String(d));
  const consecutive = days.length > 2 && days.every((d, i) => i === 0 || d === (days[i - 1] ?? 0) + 1);
  const dayText = days.length === 7 ? 'Every day' : consecutive ? `${names[0]}–${names[names.length - 1]}` : names.join(', ');
  return `${dayText} ${window.start}–${window.end}`;
}

/** What is wrong with a window as it would be saved; null when nothing is. */
export function windowProblem(window: SendWindow | null): string | null {
  if (!window) return null;
  if (window.days.length === 0) return 'Pick at least one day the emails may go.';
  if (!/^\d{2}:\d{2}$/.test(window.start) || !/^\d{2}:\d{2}$/.test(window.end)) return 'Give the hours as HH:mm, such as 09:00.';
  if (window.start >= window.end) return 'The send window has to close after it opens on the same day.';
  return null;
}

/**
 * The days and hours a rule's emails may go, in the workspace's time zone. An email step
 * reached outside them waits for the next opening; the rule's other steps are not held.
 */
export function SendWindowEditor({
  value, onChange, timeZone,
}: {
  value: SendWindow | null;
  onChange: (window: SendWindow | null) => void;
  /** The workspace's zone, as its settings name it. */
  timeZone?: string;
}) {
  const on = value != null;
  const window = value ?? DEFAULT_WINDOW;
  const toggleDay = (day: number) => {
    const days = window.days.includes(day) ? window.days.filter((d) => d !== day) : [...window.days, day].sort((a, b) => a - b);
    onChange({ ...window, days });
  };
  const problem = windowProblem(value);

  return (
    <div className="space-y-2">
      <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
        <input type="checkbox" checked={on} onChange={(e) => onChange(e.target.checked ? { ...DEFAULT_WINDOW } : null)} />
        Only send this rule&apos;s emails at certain times
      </label>
      {on && (
        <div className="space-y-2 rounded-lg border border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900">
          <div className="flex flex-wrap gap-1.5" role="group" aria-label="Days emails may go">
            {WEEKDAYS.map((d) => {
              const chosen = window.days.includes(d.day);
              return (
                <button
                  key={d.day}
                  type="button"
                  aria-pressed={chosen}
                  aria-label={d.long}
                  onClick={() => toggleDay(d.day)}
                  className={cn(
                    'h-8 w-11 rounded-md border text-xs font-semibold',
                    chosen ? 'border-amber-400 bg-amber-50 text-amber-900' : 'border-slate-200 bg-white text-slate-500 hover:border-slate-300',
                  )}
                >
                  {d.short}
                </button>
              );
            })}
          </div>
          <div className="flex flex-wrap items-center gap-2 text-sm text-slate-500">
            Between
            <Input aria-label="From" type="time" value={window.start} onChange={(e) => onChange({ ...window, start: e.target.value })} className="h-8 w-28 bg-white" />
            and
            <Input aria-label="Until" type="time" value={window.end} onChange={(e) => onChange({ ...window, end: e.target.value })} className="h-8 w-28 bg-white" />
          </div>
          <p className="text-xs text-slate-500">
            {timeZone ? `In the workspace's time zone, ${timeZone}.` : "In the workspace's time zone."}{' '}
            An email reached outside these times waits for the next opening.
          </p>
          {problem && <p className="text-xs text-red-600" role="alert">{problem}</p>}
        </div>
      )}
    </div>
  );
}
