import { useMemo } from 'react';
import { Sparkles, Mail, Phone, CheckSquare, ArrowRightCircle, Clock } from 'lucide-react';
import type { Lead, Activity, TaskItem } from '@/app/api/types';
import { cn } from '@/app/components/ui/utils';
import {
  CONTACTED_OR_BEYOND, QUALIFIED_OR_BEYOND, LOST_STATUSES, statusIn,
} from '@/app/pages/leads/leadStatusSync';

export type NextActionKind = 'email' | 'call' | 'task' | 'convert' | 'wait';

interface AiNextActionCardProps {
  lead: Lead;
  activities: Activity[];
  tasks: TaskItem[];
  className?: string;
  /** Parent maps the recommended action to a behavior (open composer, switch tab…). */
  onAct?: (kind: NextActionKind) => void;
}

const DAY = 86_400_000;

/** A lightweight engagement score (0–100) from the signals we have on a lead. */
function scoreLead(lead: Lead, activities: Activity[], tasks: TaskItem[]): { score: number; factors: string[] } {
  let score = 20;
  const factors: string[] = [];
  if (lead.email) { score += 10; }
  if (lead.phone) { score += 8; }
  // Matched against the shared status groups, NOT by substring.
  // `'Unqualified'.includes('qualified')` is true, so the old test scored a
  // disqualified lead +25 and labelled it "Qualified status". It then broke the
  // other way when the vocabulary was renamed: nothing contains the word
  // "qualified" any more, so Connected / Contract Pending / Awaiting Signature /
  // Signed leads matched no branch at all and silently lost the credit they had
  // earned. Order matters — lost is checked first so a terminal status can never
  // be read as progress.
  if (statusIn(lead.status, LOST_STATUSES)) { score -= 20; factors.push('Marked lost'); }
  else if (statusIn(lead.status, QUALIFIED_OR_BEYOND)) { score += 25; factors.push('Qualified or beyond'); }
  else if (statusIn(lead.status, CONTACTED_OR_BEYOND)) { score += 12; factors.push('Has been contacted'); }
  else if (statusIn(lead.status, ['New'])) { score += 5; }

  const actBoost = Math.min(activities.length * 4, 20);
  if (actBoost > 0) { score += actBoost; factors.push(`${activities.length} logged ${activities.length === 1 ? 'activity' : 'activities'}`); }

  if (lead.lastContactedAt) {
    const since = Date.now() - Date.parse(lead.lastContactedAt);
    if (since < 7 * DAY) { score += 15; factors.push('Contacted recently'); }
    else if (since > 30 * DAY) { score -= 10; factors.push('No contact in 30+ days'); }
  } else {
    factors.push('Never contacted');
  }
  if (tasks.some((t) => t.status !== 'completed' && t.status !== 'cancelled')) { score += 5; }
  return { score: Math.max(0, Math.min(100, Math.round(score))), factors: factors.slice(0, 3) };
}

function nextAction(lead: Lead, activities: Activity[], tasks: TaskItem[]): { kind: NextActionKind; label: string; reason: string } {
  const openTasks = tasks.filter((t) => t.status !== 'completed' && t.status !== 'cancelled');
  const overdue = openTasks.find((t) => t.dueDateUtc && Date.parse(t.dueDateUtc) < Date.now());
  if (overdue) return { kind: 'task', label: 'Complete the overdue task', reason: `“${overdue.title}” is past due.` };

  // Same shared groups as the score, so the suggestion and the score can never
  // disagree about whether a lead is qualified.
  if (statusIn(lead.status, QUALIFIED_OR_BEYOND) && !lead.isConverted) {
    return { kind: 'convert', label: 'Convert to a deal', reason: 'This lead is qualified — capture the opportunity.' };
  }
  if (activities.length === 0) {
    return { kind: 'email', label: 'Send an intro email', reason: 'No outreach logged yet — make first contact.' };
  }
  const since = lead.lastContactedAt ? Date.now() - Date.parse(lead.lastContactedAt) : Infinity;
  if (since > 7 * DAY) {
    return lead.phone
      ? { kind: 'call', label: 'Give them a call', reason: 'It has been over a week since the last touch.' }
      : { kind: 'email', label: 'Send a follow-up', reason: 'It has been over a week since the last touch.' };
  }
  if (openTasks.length === 0) {
    return { kind: 'task', label: 'Set a follow-up task', reason: 'Keep momentum with a scheduled next step.' };
  }
  return { kind: 'wait', label: "You're on track", reason: 'Recent contact and an open task — no action needed yet.' };
}

const KIND_META: Record<NextActionKind, { icon: React.ElementType; cta: string }> = {
  email: { icon: Mail, cta: 'Compose email' },
  call: { icon: Phone, cta: 'Log a call' },
  task: { icon: CheckSquare, cta: 'Go to tasks' },
  convert: { icon: ArrowRightCircle, cta: 'Convert lead' },
  wait: { icon: Clock, cta: '' },
};

function scoreTone(score: number): string {
  if (score >= 70) return 'text-emerald-600';
  if (score >= 40) return 'text-amber-600';
  return 'text-slate-500';
}

/** "AI assist": a derived engagement score + the recommended next best action. */
export default function AiNextActionCard({ lead, activities, tasks, className, onAct }: AiNextActionCardProps) {
  const { score, factors } = useMemo(() => scoreLead(lead, activities, tasks), [lead, activities, tasks]);
  const action = useMemo(() => nextAction(lead, activities, tasks), [lead, activities, tasks]);
  const Icon = KIND_META[action.kind].icon;

  return (
    <div className={className ?? 'overflow-hidden rounded-2xl border border-indigo-200/70 bg-gradient-to-br from-indigo-50/80 to-white shadow-sm'}>
      <div className="flex items-center justify-between border-b border-indigo-100/70 px-4 py-2.5">
        <h3 className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-indigo-600">
          <Sparkles className="h-3.5 w-3.5" /> AI assist
        </h3>
        <span className={cn('text-xs font-bold', scoreTone(score))}>{score}/100</span>
      </div>
      <div className="p-4">
        <div className="flex items-start gap-3">
          <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-indigo-100 text-indigo-600">
            <Icon className="h-4 w-4" />
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-slate-900">{action.label}</p>
            <p className="mt-0.5 text-xs text-slate-500">{action.reason}</p>
          </div>
        </div>
        {factors.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-1">
            {factors.map((f) => (
              <span key={f} className="rounded-md bg-white/70 px-1.5 py-0.5 text-[11px] font-medium text-slate-500 ring-1 ring-slate-200/70">{f}</span>
            ))}
          </div>
        )}
        {action.kind !== 'wait' && onAct && (
          <button
            type="button"
            onClick={() => onAct(action.kind)}
            className="mt-3 inline-flex w-full items-center justify-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-indigo-700"
          >
            <Icon className="h-4 w-4" /> {KIND_META[action.kind].cta}
          </button>
        )}
      </div>
    </div>
  );
}
