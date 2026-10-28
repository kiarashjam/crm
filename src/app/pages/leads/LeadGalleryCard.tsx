import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'motion/react';
import {
  Building2, Calendar, Check, CheckCircle2, ChevronDown, Clock, Gauge, Mail,
  Heart, MessageSquarePlus, Phone, Sparkles, Tag, User, UserCircle, UserPlus, Users, Zap,
  Activity as ActivityIcon,
} from 'lucide-react';
import type { Activity, Lead } from '@/app/api/types';
import { isCouple, relationshipLabel } from '@/app/api/relationships';
import type { OrgMemberDto } from '@/app/api/organizations';
import { Reveal } from '@/app/components/motion/PageEnter';
import { Button } from '@/app/components/ui/button';
import { Checkbox } from '@/app/components/ui/checkbox';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { AssignPopover } from './components/AssignPopover';
import { DerivedStatusBadge } from './components/DerivedStatusBadge';
import { InlineSalesEditorPopover } from './components/InlineSalesEditorPopover';
import { QuickLogPopover } from './components/QuickLogPopover';
import { SalesTrackerBadges } from './components/SalesTrackerBadges';
import { ACTIVITY_TYPES } from './config';
import {
  PHASE_TITLES, currentPhase, isPipelineComplete, isPipelineEmpty, lostPhase,
  lostReason, parsePipeline, phaseCaptions, phaseCompletion, type LeadPipeline,
} from './leadPipeline';
import { leadInitials } from './leadText';
import { formatDate } from './utils';
import type { useLeadStatusSync } from './useLeadStatusSync';

const EASE = [0.16, 1, 0.3, 1] as const;

type ToneKey = 'sky' | 'violet' | 'amber' | 'pink' | 'teal' | 'emerald' | 'rose' | 'indigo';

interface Tone {
  /** The card's top band. */
  wash: string;
  /** The soft light that drifts in the band on hover. */
  blob: string;
  /** Border and glow while hovered or focused. */
  card: string;
  avatar: string;
  text: string;
  chip: string;
}

// Static class strings, because Tailwind only ships the classes it can read.
const TONES: Record<ToneKey, Tone> = {
  sky: {
    wash: 'from-sky-100 via-sky-50/70 to-white', blob: 'bg-sky-300/50',
    card: 'hover:border-sky-300 hover:shadow-sky-500/20 focus-visible:ring-sky-400',
    avatar: 'from-sky-400 to-blue-600 shadow-sky-500/30', text: 'text-sky-700', chip: 'bg-sky-50 text-sky-700 ring-sky-200/70',
  },
  violet: {
    wash: 'from-violet-100 via-violet-50/70 to-white', blob: 'bg-violet-300/50',
    card: 'hover:border-violet-300 hover:shadow-violet-500/20 focus-visible:ring-violet-400',
    avatar: 'from-violet-500 to-purple-600 shadow-violet-500/30', text: 'text-violet-700', chip: 'bg-violet-50 text-violet-700 ring-violet-200/70',
  },
  amber: {
    wash: 'from-amber-100 via-amber-50/70 to-white', blob: 'bg-amber-300/50',
    card: 'hover:border-amber-300 hover:shadow-amber-500/20 focus-visible:ring-amber-400',
    avatar: 'from-amber-400 to-orange-500 shadow-amber-500/30', text: 'text-amber-700', chip: 'bg-amber-50 text-amber-700 ring-amber-200/70',
  },
  pink: {
    wash: 'from-pink-100 via-pink-50/70 to-white', blob: 'bg-pink-300/50',
    card: 'hover:border-pink-300 hover:shadow-pink-500/20 focus-visible:ring-pink-400',
    avatar: 'from-pink-500 to-fuchsia-600 shadow-pink-500/30', text: 'text-pink-700', chip: 'bg-pink-50 text-pink-700 ring-pink-200/70',
  },
  teal: {
    wash: 'from-teal-100 via-teal-50/70 to-white', blob: 'bg-teal-300/50',
    card: 'hover:border-teal-300 hover:shadow-teal-500/20 focus-visible:ring-teal-400',
    avatar: 'from-teal-400 to-cyan-600 shadow-teal-500/30', text: 'text-teal-700', chip: 'bg-teal-50 text-teal-700 ring-teal-200/70',
  },
  emerald: {
    wash: 'from-emerald-100 via-emerald-50/70 to-white', blob: 'bg-emerald-300/50',
    card: 'hover:border-emerald-300 hover:shadow-emerald-500/20 focus-visible:ring-emerald-400',
    avatar: 'from-emerald-500 to-teal-500 shadow-emerald-500/30', text: 'text-emerald-700', chip: 'bg-emerald-50 text-emerald-700 ring-emerald-200/70',
  },
  rose: {
    wash: 'from-rose-100/80 via-rose-50/60 to-white', blob: 'bg-rose-300/40',
    card: 'hover:border-rose-300 hover:shadow-rose-500/15 focus-visible:ring-rose-400',
    avatar: 'from-rose-400 to-slate-400 shadow-rose-500/20', text: 'text-rose-700', chip: 'bg-rose-50 text-rose-700 ring-rose-200/70',
  },
  indigo: {
    wash: 'from-indigo-100 via-indigo-50/70 to-white', blob: 'bg-indigo-300/50',
    card: 'hover:border-indigo-300 hover:shadow-indigo-500/20 focus-visible:ring-indigo-400',
    avatar: 'from-indigo-500 to-purple-600 shadow-indigo-500/30', text: 'text-indigo-700', chip: 'bg-indigo-50 text-indigo-700 ring-indigo-200/70',
  },
};

/** The lead page colours its five phases this way; the card's progress bar matches. */
const PHASE_TONE_KEYS: ToneKey[] = ['sky', 'violet', 'amber', 'pink', 'teal'];
const SEGMENT_FILL = [
  'from-sky-400 to-blue-500',
  'from-violet-400 to-purple-500',
  'from-amber-400 to-orange-500',
  'from-pink-400 to-fuchsia-500',
  'from-teal-400 to-cyan-500',
];
const SEGMENT_SOFT = ['bg-sky-100', 'bg-violet-100', 'bg-amber-100', 'bg-pink-100', 'bg-teal-100'];
const SEGMENT_PULSE = ['bg-sky-400', 'bg-violet-400', 'bg-amber-400', 'bg-pink-400', 'bg-teal-400'];

const SOURCE_ICONS: Record<string, string> = {
  website: '🌐',
  referral: '🤝',
  ads: '📢',
  events: '🎯',
  manual: '✏️',
  linkedin: '💼',
  cold_call: '📞',
  email_campaign: '📧',
};

const ACTIVITY_NODE: Record<string, string> = {
  call: 'from-sky-400 to-blue-600',
  email: 'from-purple-500 to-fuchsia-600',
  meeting: 'from-emerald-400 to-teal-600',
  note: 'from-amber-400 to-orange-500',
  system: 'from-slate-300 to-slate-400',
};

type SegmentState = 'done' | 'current' | 'lost' | 'idle';

interface Lifecycle {
  title: string;
  caption: string;
  segments: SegmentState[];
  done: number;
  tone: ToneKey;
}

function statusTone(status: string): ToneKey {
  const s = status.toLowerCase();
  if (s === 'new') return 'sky';
  if (s === 'contacted') return 'amber';
  if (s === 'qualified') return 'teal';
  if (s === 'lost') return 'rose';
  return 'indigo';
}

/** Everything the card says about where the lead is, read from its pipeline. */
export function describeLifecycle(lead: Lead, pipeline: LeadPipeline): Lifecycle {
  const done = phaseCompletion(pipeline);
  const doneCount = done.filter(Boolean).length;
  const empty = isPipelineEmpty(pipeline);
  const complete = isPipelineComplete(pipeline);
  const lost = lostReason(pipeline);
  const lostAt = lostPhase(pipeline);
  const current = currentPhase(pipeline);
  const captions = phaseCaptions(pipeline);

  const segments = done.map((isDone, i): SegmentState => {
    if (lost && lostAt === i + 1) return 'lost';
    if (isDone) return 'done';
    if (!lost && !empty && !complete && current === i + 1) return 'current';
    return 'idle';
  });

  if (lead.isConverted) {
    const when = lead.convertedAtUtc ? formatDate(lead.convertedAtUtc) : null;
    return {
      title: 'Converted',
      caption: when ? `Became a contact · ${when}` : 'Became a contact',
      segments, done: doneCount, tone: 'emerald',
    };
  }
  if (complete) {
    return { title: 'Deal-ready', caption: 'Every phase is done. Time to make it a deal.', segments, done: doneCount, tone: 'emerald' };
  }
  if (lost) {
    const at = lostAt ?? current;
    return {
      title: `Stopped at ${PHASE_TITLES[at - 1] ?? 'a phase'}`,
      caption: captions[at - 1] ?? lost,
      segments, done: doneCount, tone: 'rose',
    };
  }
  if (empty) {
    return { title: 'Not started', caption: 'Log the first outreach to start the pipeline.', segments, done: 0, tone: statusTone(lead.status) };
  }
  return {
    title: `Phase ${current} · ${PHASE_TITLES[current - 1]}`,
    caption: captions[current - 1] ?? '',
    segments, done: doneCount, tone: PHASE_TONE_KEYS[current - 1] ?? 'indigo',
  };
}

/** "Now", "5m ago", "3h ago", "2d ago", then a date: short enough for a tile. */
function ago(iso: string): string {
  const text = formatDate(iso);
  return text === 'Just now' ? 'Now' : text;
}

function latestTouch(lead: Lead, interactions: Activity[]): string | null {
  // An update the system wrote (an owner change, a status sync) is not a touch.
  const logged = interactions.find((a) => (a.type || '').toLowerCase() !== 'system')?.createdAt;
  const candidates = [lead.lastContactedAt, logged].filter((d): d is string => !!d && !Number.isNaN(Date.parse(d)));
  if (candidates.length === 0) return null;
  return candidates.reduce((a, b) => (Date.parse(a) >= Date.parse(b) ? a : b));
}

function fullDate(iso?: string): string | undefined {
  if (!iso) return undefined;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return undefined;
  return d.toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
}

function ScoreRing({ score, reduce }: { score: number; reduce: boolean }) {
  const clamped = Math.max(0, Math.min(100, score));
  const stroke = clamped >= 70 ? 'stroke-orange-500' : clamped >= 40 ? 'stroke-sky-500' : 'stroke-slate-400';
  return (
    <svg viewBox="0 0 36 36" className="h-5 w-5 -rotate-90" aria-hidden>
      <circle cx="18" cy="18" r="15" className="fill-none stroke-slate-100" strokeWidth="4" />
      <motion.circle
        cx="18" cy="18" r="15"
        className={cn('fill-none', stroke)}
        strokeWidth="4"
        strokeLinecap="round"
        initial={reduce ? false : { pathLength: 0 }}
        animate={{ pathLength: clamped / 100 }}
        transition={{ duration: 0.9, ease: EASE, delay: 0.2 }}
      />
    </svg>
  );
}

function Fact({ icon, label, value, title, tone }: { icon: ReactNode; label: string; value: ReactNode; title?: string; tone: string }) {
  return (
    <div className="min-w-0 rounded-xl bg-white/80 px-2.5 py-2 ring-1 ring-slate-200/70" title={title}>
      <div className="truncate text-[10px] font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 flex min-w-0 items-center gap-1.5 text-sm font-semibold tabular-nums text-slate-800">
        <span className={cn('flex h-5 w-5 shrink-0 items-center justify-center rounded-md', tone)} aria-hidden>{icon}</span>
        <span className="truncate">{value}</span>
      </div>
    </div>
  );
}

export interface LeadGalleryCardProps {
  lead: Lead;
  /** Position in the page, for the entry cascade. */
  index: number;
  /** Company name to show, or null for none. */
  company: string | null;
  assignee: OrgMemberDto | null;
  members: OrgMemberDto[];
  /** This lead's interactions, newest first. */
  interactions: Activity[];
  readOnly: boolean;
  selected: boolean;
  /** Any card on the page is selected, so every checkbox stays visible. */
  selecting: boolean;
  statusesUnavailable: boolean;
  statusSync: ReturnType<typeof useLeadStatusSync>;
  onOpen: (lead: Lead) => void;
  onToggleSelected: (leadId: string) => void;
  onAssign: (lead: Lead, userId: string) => void;
  onQuickLog: (lead: Lead, payload: { type: string; body: string }) => Promise<void>;
  onSaved: (updated: Lead) => void;
}

/**
 * One lead in the leads gallery: who they are, how far along the five phases they
 * are and what the next phase is waiting on, when they were last touched, the
 * latest interactions, and the owner, track and log controls. Its colour follows
 * the phase the lead is in, the same colours the lead page uses.
 */
export function LeadGalleryCard({
  lead, index, company, assignee, members, interactions, readOnly, selected, selecting,
  statusesUnavailable, statusSync, onOpen, onToggleSelected, onAssign, onQuickLog, onSaved,
}: LeadGalleryCardProps) {
  const reduce = useMotionPreference();
  const pipeline = parsePipeline(lead.pipelineState);
  const life = describeLifecycle(lead, pipeline);
  const tone = TONES[life.tone];
  const initials = leadInitials(lead.name);
  const sourceKey = (lead.source || '').toLowerCase();
  const touched = latestTouch(lead, interactions);
  const recent = interactions.slice(0, 2);
  const hasScore = lead.leadScore !== undefined && lead.leadScore !== null;
  const hot = (hasScore && (lead.leadScore ?? 0) >= 70) || lead.lifecycleStage === 'Hot';
  const email = lead.email?.trim();
  // Spouse or partner (the couples package) gets its own chip; other relatives share one.
  const couple = lead.relationships?.find((r) => isCouple(r.type));
  const relatives = lead.relationships?.filter((r) => !isCouple(r.type)) ?? [];

  return (
    <Reveal
      index={index}
      onClick={(e) => {
        // A click anywhere on the card opens the lead, for the mouse. Controls inside
        // it (checkbox, links, owner, track, log and their popovers) do their own
        // thing. Keyboard and screen-reader users open it from the name, so the card
        // is not itself a button wrapped around other buttons.
        if ((e.target as HTMLElement).closest('button, a, input, textarea, label, [role="dialog"], [data-radix-popper-content-wrapper]')) return;
        onOpen(lead);
      }}
      className={cn(
        '@container group/card relative flex cursor-pointer flex-col overflow-hidden rounded-3xl border border-slate-200/80 bg-white shadow-sm',
        'transition-[translate,box-shadow,border-color] duration-300 ease-out hover:-translate-y-1 hover:shadow-xl motion-reduce:transition-none motion-reduce:hover:translate-y-0',
        'has-[[data-open-lead]:focus-visible]:-translate-y-1 has-[[data-open-lead]:focus-visible]:shadow-xl',
        selected && 'ring-2 ring-indigo-400 ring-offset-2',
        tone.card,
      )}
    >
      {/* The top band, in the colour of where the lead is. */}
      <div className={cn('relative overflow-hidden bg-gradient-to-br px-4 pb-3 pt-4', tone.wash)}>
        <span
          aria-hidden
          className={cn(
            'pointer-events-none absolute -right-10 -top-12 h-32 w-32 rounded-full blur-2xl transition-transform duration-700 ease-out',
            'group-hover/card:-translate-x-6 group-hover/card:scale-125 motion-reduce:transition-none',
            tone.blob,
          )}
        />
        {!reduce && (
          <span
            aria-hidden
            className="pointer-events-none absolute inset-y-0 -left-1/2 w-1/2 -skew-x-12 bg-gradient-to-r from-transparent via-white/70 to-transparent transition-transform duration-1000 ease-out group-hover/card:translate-x-[320%]"
          />
        )}

        {!readOnly && (
          <div
            className={cn(
              'absolute right-3 top-3 z-20 transition-opacity',
              selecting || selected ? 'opacity-100' : 'opacity-0 focus-within:opacity-100 group-hover/card:opacity-100',
            )}
            onClick={(e) => e.stopPropagation()}
          >
            <Checkbox
              checked={selected}
              onCheckedChange={() => onToggleSelected(lead.id)}
              aria-label={`Select ${lead.name}`}
              className="bg-white shadow"
            />
          </div>
        )}

        <div className="relative flex items-start gap-3">
          <div className="relative shrink-0">
            <div
              className={cn(
                'flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br text-base font-bold text-white shadow-lg ring-4 ring-white/80',
                'transition-transform duration-300 ease-out group-hover/card:-rotate-6 group-hover/card:scale-105 motion-reduce:transition-none',
                tone.avatar,
              )}
              aria-hidden
            >
              {initials || <User className="h-5 w-5" />}
            </div>
            {lead.isConverted && (
              <span className="absolute -bottom-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full border-2 border-white bg-emerald-500 text-white shadow" aria-hidden>
                <Check className="h-3 w-3" />
              </span>
            )}
            {!lead.isConverted && hot && (
              <motion.span
                className="absolute -right-1.5 -top-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-gradient-to-br from-amber-400 to-orange-500 text-white shadow-md"
                animate={reduce ? undefined : { rotate: [0, -14, 14, 0], scale: [1, 1.15, 1] }}
                transition={{ duration: 0.8, repeat: Infinity, repeatDelay: 2.6 }}
                aria-hidden
              >
                <Zap className="h-3 w-3" />
              </motion.span>
            )}
          </div>

          <div className="min-w-0 flex-1 pr-6">
            <h3 className="truncate text-base font-bold tracking-tight text-slate-900">
              <button
                type="button"
                data-open-lead
                onClick={() => onOpen(lead)}
                aria-label={`Open ${lead.name}`}
                className="max-w-full truncate rounded-sm text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-2"
              >
                {lead.name}
              </button>
            </h3>
            {(company || lead.jobTitle) && (
              <p className="mt-0.5 flex min-w-0 items-center gap-1.5 text-xs text-slate-500">
                <Building2 className="h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden />
                <span className="truncate">{[lead.jobTitle, company].filter(Boolean).join(' · ')}</span>
              </p>
            )}
            <div className="mt-2 flex flex-wrap items-center gap-1.5">
              <DerivedStatusBadge
                status={lead.status}
                pipeline={pipeline}
                className="px-2 py-0.5 text-[11px]"
                statusesUnavailable={statusesUnavailable}
              />
              {lead.source && (
                <span className="inline-flex items-center gap-1 rounded-full bg-white/80 px-2 py-0.5 text-[11px] font-semibold capitalize text-slate-700 ring-1 ring-slate-200/80">
                  <span aria-hidden>{SOURCE_ICONS[sourceKey] ?? '📌'}</span>
                  {lead.source.replace(/_/g, ' ')}
                </span>
              )}
              {lead.lifecycleStage && (
                <span className={cn(
                  'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider',
                  lead.lifecycleStage === 'Hot' ? 'bg-gradient-to-r from-red-500 to-rose-500 text-white'
                    : lead.lifecycleStage === 'SQL' ? 'bg-gradient-to-r from-emerald-500 to-teal-500 text-white'
                      : lead.lifecycleStage === 'MQL' ? 'bg-gradient-to-r from-blue-500 to-indigo-500 text-white'
                        : 'bg-slate-200 text-slate-600',
                )}>
                  {lead.lifecycleStage}
                </span>
              )}
              {lead.isConverted && (
                <span className="inline-flex items-center gap-1 rounded-full bg-gradient-to-r from-emerald-500 to-teal-500 px-2 py-0.5 text-[11px] font-bold text-white shadow-sm">
                  <CheckCircle2 className="h-3 w-3" aria-hidden />
                  Converted
                </span>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className="flex flex-1 flex-col gap-3 px-4 pb-3 pt-3">
        {/* Where the lead is in the five phases, and what the next step waits on. */}
        <div>
          <div className="flex items-center justify-between gap-2">
            <span className={cn('truncate text-xs font-bold', tone.text)}>{life.title}</span>
            <span className="shrink-0 text-[11px] font-semibold tabular-nums text-slate-500">{life.done}/5</span>
          </div>
          <div
            className="mt-1.5 grid grid-cols-5 gap-1"
            role="img"
            aria-label={`${life.done} of 5 phases done. ${life.title}.`}
          >
            {life.segments.map((state, i) => (
              <span
                key={PHASE_TITLES[i]}
                className={cn(
                  'relative h-2 overflow-hidden rounded-full',
                  state === 'idle' ? 'bg-slate-100' : state === 'lost' ? 'bg-rose-100' : SEGMENT_SOFT[i],
                )}
                title={PHASE_TITLES[i]}
              >
                {state === 'done' && (
                  <motion.span
                    className={cn('absolute inset-0 origin-left rounded-full bg-gradient-to-r', SEGMENT_FILL[i])}
                    initial={reduce ? false : { scaleX: 0 }}
                    animate={{ scaleX: 1 }}
                    transition={{ duration: 0.5, ease: EASE, delay: 0.15 + i * 0.08 }}
                  />
                )}
                {state === 'lost' && <span className="absolute inset-0 rounded-full bg-rose-400" />}
                {state === 'current' && (
                  <motion.span
                    data-current-phase
                    className={cn('absolute inset-y-0 left-0 w-1/2 rounded-full', SEGMENT_PULSE[i])}
                    animate={reduce ? undefined : { opacity: [0.35, 1, 0.35] }}
                    transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut' }}
                  />
                )}
              </span>
            ))}
          </div>
          <p className="mt-1.5 line-clamp-1 text-xs text-slate-500" title={life.caption}>{life.caption}</p>
        </div>

        {/* The numbers worth a glance. Two across on a narrow card, four on a wide one. */}
        <div className="grid grid-cols-2 gap-1.5 @sm:grid-cols-4">
          <Fact
            icon={hasScore ? <ScoreRing score={lead.leadScore ?? 0} reduce={reduce} /> : <Gauge className="h-3 w-3" />}
            tone={hasScore ? '' : 'bg-orange-100 text-orange-600'}
            label="Score"
            value={hasScore ? lead.leadScore : <span className="text-slate-300">—</span>}
          />
          <Fact
            icon={<Clock className="h-3 w-3" />}
            tone="bg-sky-100 text-sky-600"
            label="Last touch"
            title={fullDate(touched ?? undefined)}
            value={touched ? ago(touched) : <span className="text-rose-700">Never</span>}
          />
          <Fact
            icon={<ActivityIcon className="h-3 w-3" />}
            tone="bg-violet-100 text-violet-600"
            label="Activities"
            value={interactions.length}
          />
          <Fact
            icon={<Calendar className="h-3 w-3" />}
            tone="bg-emerald-100 text-emerald-600"
            label="Added"
            title={fullDate(lead.createdAtUtc)}
            value={lead.createdAtUtc && !Number.isNaN(Date.parse(lead.createdAtUtc)) ? ago(lead.createdAtUtc) : '—'}
          />
        </div>

        {/* How to reach them, who sent them, and who they come with. */}
        {(email || lead.phone || (lead.referredByContactId && lead.referredByContactName) || couple || relatives.length > 0) && (
          <div className="flex flex-wrap items-center gap-1.5">
            {email && (
              <a
                href={`mailto:${email}`}
                onClick={(e) => e.stopPropagation()}
                className="inline-flex min-w-0 max-w-full items-center gap-1.5 rounded-full bg-purple-50 px-2.5 py-1 text-xs font-medium text-purple-800 ring-1 ring-purple-200/70 transition-colors hover:bg-purple-100"
              >
                <Mail className="h-3.5 w-3.5 shrink-0" aria-hidden />
                <span className="truncate">{email}</span>
              </a>
            )}
            {lead.phone && (
              <a
                href={`tel:${lead.phone}`}
                onClick={(e) => e.stopPropagation()}
                className="inline-flex items-center gap-1.5 rounded-full bg-teal-50 px-2.5 py-1 text-xs font-medium text-teal-800 ring-1 ring-teal-200/70 transition-colors hover:bg-teal-100"
              >
                <Phone className="h-3.5 w-3.5 shrink-0" aria-hidden />
                {lead.phone}
              </a>
            )}
            {lead.referredByContactId && lead.referredByContactName && (
              <Link
                to={`/contacts/${lead.referredByContactId}`}
                onClick={(e) => e.stopPropagation()}
                className="inline-flex min-w-0 max-w-full items-center gap-1.5 rounded-full bg-fuchsia-50 px-2.5 py-1 text-xs font-medium text-fuchsia-800 ring-1 ring-fuchsia-200/70 transition-colors hover:bg-fuchsia-100"
              >
                <UserPlus className="h-3.5 w-3.5 shrink-0" aria-hidden />
                <span className="truncate">Referred by {lead.referredByContactName}</span>
              </Link>
            )}
            {couple && (
              <Link
                to={couple.relatedLeadId ? `/leads/${couple.relatedLeadId}` : `/contacts/${couple.relatedContactId}`}
                onClick={(e) => e.stopPropagation()}
                className="inline-flex min-w-0 max-w-full items-center gap-1.5 rounded-full bg-rose-50 px-2.5 py-1 text-xs font-medium text-rose-800 ring-1 ring-rose-200/70 transition-colors hover:bg-rose-100"
              >
                <Heart className="h-3.5 w-3.5 shrink-0 fill-rose-400 text-rose-500" aria-hidden />
                <span className="truncate">{relationshipLabel(couple.type)}: {couple.relatedContactName}</span>
              </Link>
            )}
            {relatives.length > 0 && (
              <span
                title={relatives.map((r) => `${relationshipLabel(r.type)}: ${r.relatedContactName}`).join('\n')}
                className="inline-flex min-w-0 max-w-full items-center gap-1.5 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700 ring-1 ring-slate-200/70"
              >
                <Users className="h-3.5 w-3.5 shrink-0" aria-hidden />
                <span className="truncate">
                  {relatives.length === 1 && relatives[0]
                    ? `${relationshipLabel(relatives[0].type)}: ${relatives[0].relatedContactName}`
                    : `${relatives.length} relatives`}
                </span>
              </span>
            )}
          </div>
        )}

        {/* The latest two interactions, so a glance says what happened last. */}
        {recent.length === 0 && (
          <div className="flex items-center gap-2 rounded-xl border border-dashed border-slate-200 bg-slate-50/60 px-3 py-2 text-xs text-slate-500">
            <motion.span
              className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-orange-400 to-pink-500 text-white shadow-sm"
              animate={reduce ? undefined : { rotate: [0, -10, 10, 0] }}
              transition={{ duration: 0.7, repeat: Infinity, repeatDelay: 3 }}
              aria-hidden
            >
              <MessageSquarePlus className="h-3.5 w-3.5" />
            </motion.span>
            {readOnly ? 'No interactions yet.' : 'No interactions yet. Log the first call, email, or note.'}
          </div>
        )}
        {recent.length > 0 && (
          <ul className="space-y-1" aria-label="Latest interactions">
            {recent.map((activity) => {
              const typeKey = (activity.type || '').toLowerCase();
              const config = ACTIVITY_TYPES.find((t) => t.id === typeKey);
              const Icon = config?.icon ?? MessageSquarePlus;
              const text = activity.subject || activity.body || config?.label || 'Activity';
              return (
                <li key={activity.id} className="flex items-center gap-2 rounded-lg bg-slate-50/80 px-2 py-1.5">
                  <span
                    className={cn(
                      'flex h-5 w-5 shrink-0 items-center justify-center rounded-md bg-gradient-to-br text-white',
                      ACTIVITY_NODE[typeKey] ?? 'from-indigo-400 to-violet-500',
                    )}
                    aria-hidden
                  >
                    <Icon className="h-3 w-3" />
                  </span>
                  <span className={cn('min-w-0 flex-1 truncate text-xs', typeKey === 'system' ? 'italic text-slate-500' : 'text-slate-700')}>
                    {text}
                  </span>
                  <span className="shrink-0 text-[10px] text-slate-400">{ago(activity.createdAt)}</span>
                </li>
              );
            })}
            {interactions.length > recent.length && (
              <li className="pl-1 text-[11px] text-slate-400">+{interactions.length - recent.length} more on the lead page</li>
            )}
          </ul>
        )}

        {lead.description && (
          <p className="line-clamp-2 rounded-xl border-l-4 border-orange-300 bg-orange-50/50 px-3 py-2 text-xs italic leading-relaxed text-slate-600">
            {lead.description}
          </p>
        )}

        {lead.tags && lead.tags.length > 0 && (
          <div className="flex flex-wrap items-center gap-1">
            {lead.tags.slice(0, 3).map((tag) => (
              <span key={tag} className="inline-flex items-center gap-1 rounded-full bg-gradient-to-r from-purple-50 to-pink-50 px-2 py-0.5 text-[11px] text-purple-700 ring-1 ring-purple-100">
                <Tag className="h-2.5 w-2.5" aria-hidden />
                {tag}
              </span>
            ))}
            {lead.tags.length > 3 && <span className="text-[11px] font-medium text-slate-400">+{lead.tags.length - 3}</span>}
          </div>
        )}

        <SalesTrackerBadges lead={lead} />
      </div>

      {/* Owner, track and log. Edit, convert and delete live on the lead page. */}
      <div
        className="mt-auto flex items-center justify-between gap-2 border-t border-slate-100 bg-slate-50/60 px-3 py-2.5"
        onClick={(e) => e.stopPropagation()}
      >
        {readOnly ? (
          <span
            className="inline-flex min-w-0 max-w-full items-center gap-1.5 rounded-full border border-slate-200 bg-white py-1 pl-1 pr-2.5 text-xs text-slate-600 shadow-sm"
            title={assignee ? `Assigned to ${assignee.name}` : 'Unassigned'}
          >
            <OwnerAvatar name={assignee?.name} />
            <span className="truncate font-medium">{assignee ? assignee.name : 'Unassigned'}</span>
          </span>
        ) : (
          <>
            <AssignPopover
              assigneeId={lead.assignedToId}
              members={members}
              onAssign={(userId) => onAssign(lead, userId)}
              trigger={
                <button
                  type="button"
                  aria-label={assignee ? `Assigned to ${assignee.name} — change owner` : 'Assign owner'}
                  className="inline-flex min-w-0 max-w-[50%] items-center gap-1.5 rounded-full border border-slate-200 bg-white py-1 pl-1 pr-2 text-xs text-slate-600 shadow-sm transition-colors hover:border-indigo-300 hover:bg-indigo-50/60"
                >
                  <OwnerAvatar name={assignee?.name} />
                  <span className="truncate font-medium">{assignee ? assignee.name : 'Unassigned'}</span>
                  <ChevronDown className="h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden />
                </button>
              }
            />
            <div className="flex shrink-0 items-center gap-1.5">
              <InlineSalesEditorPopover
                lead={lead}
                leadName={lead.name}
                statusSync={statusSync}
                onSaved={onSaved}
                trigger={
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-8 gap-1.5 rounded-full border-indigo-200/70 bg-white px-3 font-medium text-slate-700 shadow-sm hover:border-indigo-300 hover:bg-indigo-50 hover:text-indigo-700"
                    aria-label={`Lead pipeline for ${lead.name}`}
                  >
                    <Sparkles className="h-3.5 w-3.5" />
                    Track
                  </Button>
                }
              />
              <QuickLogPopover
                onSubmit={(payload) => onQuickLog(lead, payload)}
                trigger={
                  <Button
                    size="sm"
                    className="h-8 gap-1.5 rounded-full bg-gradient-to-r from-orange-500 to-pink-500 px-3 font-medium text-white shadow-sm shadow-pink-500/20 hover:opacity-95"
                    aria-label={`Log activity for ${lead.name}`}
                  >
                    <MessageSquarePlus className="h-3.5 w-3.5" />
                    Log
                  </Button>
                }
              />
            </div>
          </>
        )}
      </div>
    </Reveal>
  );
}

function OwnerAvatar({ name }: { name?: string }) {
  if (!name) {
    return (
      <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-400" aria-hidden>
        <UserCircle className="h-4 w-4" />
      </span>
    );
  }
  return (
    <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 text-[10px] font-bold text-white ring-2 ring-white" aria-hidden>
      {name.charAt(0).toUpperCase()}
    </span>
  );
}
