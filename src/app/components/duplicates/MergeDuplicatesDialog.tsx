// The merge, one group of duplicates at a time.
//
// The person picks the record to keep, sees every field side by side with what the
// merged record will hold, is told what moves across and what becomes of the
// others, and only then confirms. Nothing is merged without that last click, and
// a group can be skipped for later or marked as not the same people at all.

import { useEffect, useMemo, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowRight, Check, CheckCircle2, ExternalLink, GitMerge, Sparkles, X } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { initialsOf } from '@/app/components/RecordCard';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { mergeDuplicates, mergeRecords, suggestKeep, type DuplicateGroup, type MergeRule } from '@/app/api/dedupe';
import type { HeroTone } from '@/app/components/PageHero';
import type { DuplicateKind } from './kinds';

const EASE = [0.16, 1, 0.3, 1] as const;

// Static class strings, because Tailwind only ships the classes it can read.
const HEADER: Partial<Record<HeroTone, string>> = {
  emerald: 'from-emerald-500 via-teal-500 to-cyan-500',
  blue: 'from-blue-500 via-indigo-500 to-sky-500',
  orange: 'from-orange-500 via-pink-500 to-rose-500',
  violet: 'from-violet-500 via-fuchsia-500 to-pink-500',
  indigo: 'from-indigo-500 via-violet-500 to-purple-500',
  teal: 'from-teal-500 via-cyan-500 to-sky-500',
};
const AVATAR: Partial<Record<HeroTone, string>> = {
  emerald: 'from-emerald-500 to-teal-500',
  blue: 'from-blue-500 to-indigo-500',
  orange: 'from-orange-500 to-pink-500',
  violet: 'from-violet-500 to-fuchsia-500',
  indigo: 'from-indigo-500 to-violet-500',
  teal: 'from-teal-500 to-cyan-500',
};

const when = (v?: string) => {
  if (!v) return undefined;
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? undefined : d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
};

/** What a result taken from a duplicate says about how it was chosen. */
const HOW: Record<MergeRule, string> = {
  keep: 'kept',
  fill: 'filled',
  fillAmount: 'filled',
  earliest: 'earliest',
  latest: 'latest',
  sum: 'added up',
  any: 'from duplicate',
  append: 'combined',
  union: 'combined',
};

const list = (items: string[]) =>
  items.length <= 1 ? items.join('') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;

type Phase = 'review' | 'merging' | 'merged' | 'done';

interface Props<T extends { id: string }> {
  spec: DuplicateKind<T>;
  groups: DuplicateGroup<T>[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** After each merge, so the page can show the list as it now is. */
  onMerged: () => void;
  /** The person said a group is not the same record. */
  onNotSame: (key: string) => void;
}

export function MergeDuplicatesDialog<T extends { id: string }>({ spec, groups, open, onOpenChange, onMerged, onNotSame }: Props<T>) {
  const reduce = useMotionPreference();
  // The groups as they were when the dialog opened. Merging refreshes the page's
  // list underneath, and the person should not see the ground move.
  const [queue, setQueue] = useState<DuplicateGroup<T>[]>([]);
  const [index, setIndex] = useState(0);
  const [keepBy, setKeepBy] = useState<Record<string, string>>({});
  const [phase, setPhase] = useState<Phase>('review');
  const [error, setError] = useState<string | null>(null);
  const [tally, setTally] = useState({ merged: 0, groups: 0, skipped: 0 });
  const [lastKept, setLastKept] = useState<string>('');
  const [bulk, setBulk] = useState<{ at: number; of: number } | null>(null);
  const timer = useRef<number | undefined>(undefined);
  const content = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    setQueue(groups);
    setIndex(0);
    setKeepBy({});
    setPhase(groups.length ? 'review' : 'done');
    setError(null);
    setTally({ merged: 0, groups: 0, skipped: 0 });
    setBulk(null);
    // Only on opening: later changes to the groups are the merges themselves.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    if (!open) window.clearTimeout(timer.current);
  }, [open]);

  // Each group starts on its choice of record to keep; the last screen on its button.
  useEffect(() => {
    if (!open || phase === 'merging' || phase === 'merged') return;
    const frame = window.requestAnimationFrame(() => {
      const root = content.current;
      const target = root?.querySelector<HTMLElement>('[role="radio"][aria-checked="true"]')
        ?? root?.querySelector<HTMLElement>('[data-autofocus]');
      (target ?? root)?.focus();
    });
    return () => window.cancelAnimationFrame(frame);
  }, [open, phase, index, queue]);
  useEffect(() => () => window.clearTimeout(timer.current), []);

  const group = queue[index];
  const keepId = group ? keepBy[group.key] ?? suggestKeep(spec.fields, group.records, spec.created) : '';
  const keep = group?.records.find((r) => r.id === keepId) ?? group?.records[0];
  const others = useMemo(() => (group && keep ? group.records.filter((r) => r.id !== keep.id) : []), [group, keep]);
  const preview = useMemo(() => (keep ? mergeRecords(spec.fields, keep, others) : null), [spec.fields, keep, others]);
  const ordered = keep ? [keep, ...others] : [];
  const filled = preview?.rows.filter((r) => r.values.some((v) => v !== undefined)) ?? [];
  // Fields every record agrees on say nothing about the choice: one line for them,
  // and the table for what actually differs.
  const same = filled.filter((r) => r.values.every((v) => v === r.values[0]));
  const rows = filled.filter((r) => !same.includes(r));
  /** What a record has that the others do not, to tell near-identical records apart. */
  const onlyHere = (recordId: string) => {
    const i = ordered.findIndex((r) => r.id === recordId);
    if (i < 0) return [];
    return rows.filter((row) => row.values[i] !== undefined && row.values.every((v, j) => j === i || v !== row.values[i])).map((row) => row.label);
  };
  const remaining = queue.length - index;

  const advance = (after: Phase = 'review') => {
    window.clearTimeout(timer.current);
    if (index + 1 < queue.length) {
      setIndex((i) => i + 1);
      setPhase(after);
    } else {
      setPhase('done');
    }
    setError(null);
  };

  const mergeOne = async (g: DuplicateGroup<T>): Promise<boolean> => {
    const id = keepBy[g.key] ?? suggestKeep(spec.fields, g.records, spec.created);
    const kept = g.records.find((r) => r.id === id) ?? g.records[0];
    if (!kept) return false;
    const outcome = await mergeDuplicates(spec.kind, kept.id, g.records.filter((r) => r.id !== kept.id).map((r) => r.id));
    if (outcome.merged > 0) {
      setTally((t) => ({ ...t, merged: t.merged + outcome.merged, groups: t.groups + 1 }));
      setLastKept(spec.title(kept));
      onMerged();
    }
    if (outcome.error) {
      setError(outcome.merged > 0
        ? `${outcome.merged} merged, then: ${outcome.error}`
        : outcome.error);
      return false;
    }
    return true;
  };

  const confirm = async () => {
    if (!group) return;
    setPhase('merging');
    setError(null);
    const ok = await mergeOne(group).catch((err: unknown) => {
      setError(err instanceof Error ? err.message : 'The merge failed.');
      return false;
    });
    if (!ok) {
      setPhase('review');
      return;
    }
    setPhase('merged');
    timer.current = window.setTimeout(() => advance(), reduce ? 500 : 1300);
  };

  const confirmAll = async () => {
    setPhase('merging');
    setError(null);
    const todo = queue.slice(index);
    for (const [i, g] of todo.entries()) {
      setBulk({ at: i + 1, of: todo.length });
      setIndex(index + i);
      const ok = await mergeOne(g).catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'The merge failed.');
        return false;
      });
      if (!ok) {
        setBulk(null);
        setPhase('review');
        return;
      }
    }
    setBulk(null);
    setPhase('done');
  };

  const skip = () => {
    setTally((t) => ({ ...t, skipped: t.skipped + 1 }));
    advance();
  };

  const notSame = () => {
    if (group) onNotSame(group.key);
    advance();
  };

  // While one merge runs, or while its tick shows, nothing else may start.
  const busy = phase === 'merging' || phase === 'merged';
  const header = HEADER[spec.tone] ?? HEADER.orange;
  const avatar = AVATAR[spec.tone] ?? AVATAR.orange;
  const Icon = spec.icon;

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!busy) onOpenChange(next); }}>
      <DialogContent
        className="gap-0 overflow-hidden p-0 sm:max-w-3xl"
        data-testid="merge-duplicates-dialog"
        // Focus goes to the record being kept once it is drawn (below), not to the close
        // button; and never to Merge, where a stray Enter would merge unread.
        onOpenAutoFocus={(event) => event.preventDefault()}
      >
        {/* Header */}
        <div className="relative overflow-hidden">
          <div className={cn('absolute inset-0 bg-gradient-to-br', header)} />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_25%_15%,rgba(255,255,255,0.28),transparent_55%)]" />
          <motion.div
            aria-hidden
            className="absolute -top-12 -right-10 h-44 w-44 rounded-full bg-white/15 blur-2xl"
            animate={reduce ? undefined : { scale: [1, 1.15, 1], opacity: [0.6, 1, 0.6] }}
            transition={{ duration: 4, repeat: Infinity, ease: 'easeInOut' }}
          />
          <div className="relative flex items-center gap-4 px-6 py-5 pr-12">
            <motion.div
              className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl border border-white/30 bg-white/20 text-white shadow-lg backdrop-blur-sm"
              initial={reduce ? false : { rotate: -12, scale: 0.8 }}
              animate={{ rotate: 0, scale: 1 }}
              transition={{ type: 'spring', stiffness: 260, damping: 16 }}
            >
              <GitMerge className="h-6 w-6" />
            </motion.div>
            <div className="min-w-0 flex-1">
              <DialogTitle className="text-xl font-bold tracking-tight text-white">
                Merge duplicate {spec.noun.many}
              </DialogTitle>
              <DialogDescription className="mt-0.5 text-sm text-white/85">
                {phase === 'done'
                  ? 'All done.'
                  : group
                    ? `${queue.length > 1 ? `${index + 1} of ${queue.length} · ` : ''}${group.reason}`
                    : 'Nothing to merge.'}
              </DialogDescription>
            </div>
            {queue.length > 1 && phase !== 'done' && (
              <div className="hidden items-center gap-1.5 sm:flex" aria-hidden>
                {queue.map((g, i) => (
                  <motion.span
                    key={g.key}
                    className={cn('h-1.5 rounded-full bg-white', i === index ? 'opacity-100' : i < index ? 'opacity-70' : 'opacity-35')}
                    animate={{ width: i === index ? 20 : 6 }}
                    transition={{ duration: reduce ? 0 : 0.3, ease: EASE }}
                  />
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Body */}
        <div ref={content} tabIndex={-1} className="relative max-h-[min(64vh,640px)] overflow-y-auto outline-none">
          <AnimatePresence mode="wait" initial={false}>
            {phase === 'done' ? (
              <DoneScreen key="done" tally={tally} noun={spec.noun} reduce={reduce} onClose={() => onOpenChange(false)} />
            ) : group && keep ? (
              <motion.div
                key={group.key}
                className="space-y-5 p-6"
                initial={reduce ? false : { opacity: 0, x: 24 }}
                animate={{ opacity: 1, x: 0 }}
                exit={reduce ? { opacity: 0 } : { opacity: 0, x: -24 }}
                transition={{ duration: reduce ? 0.12 : 0.3, ease: EASE }}
              >
                {/* Which to keep */}
                <section>
                  <h3 className="mb-2.5 text-xs font-semibold tracking-wide text-slate-500 uppercase">Keep this one</h3>
                  <div role="radiogroup" aria-label={`The ${spec.noun.one} to keep`} className={cn('grid gap-3', group.records.length === 2 ? 'sm:grid-cols-2' : 'sm:grid-cols-3')}>
                    {group.records.map((record, i) => {
                      const selected = record.id === keep.id;
                      return (
                        <div key={record.id} className="group relative">
                        <motion.button
                          type="button"
                          role="radio"
                          aria-checked={selected}
                          disabled={busy}
                          onClick={() => setKeepBy((k) => ({ ...k, [group.key]: record.id }))}
                          className={cn(
                            'relative flex h-full w-full items-start gap-3 rounded-2xl border bg-white p-3.5 text-left transition-[border-color,box-shadow,background-color] duration-200 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50',
                            selected ? 'border-emerald-400 bg-emerald-50/60 shadow-md shadow-emerald-500/10' : 'border-slate-200 hover:border-slate-300 hover:shadow-sm',
                          )}
                          initial={reduce ? false : { opacity: 0, y: 10 }}
                          animate={{ opacity: 1, y: 0 }}
                          transition={{ duration: 0.35, ease: EASE, delay: reduce ? 0 : i * 0.06 }}
                          whileTap={reduce || busy ? undefined : { scale: 0.98 }}
                        >
                          <span className={cn('flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br text-sm font-bold text-white shadow-md', avatar)}>
                            {initialsOf(spec.title(record))}
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="block truncate font-semibold text-slate-900">{spec.title(record)}</span>
                            <span className="block truncate text-xs text-slate-500">{spec.subtitle(record) || '—'}</span>
                            {when(spec.created(record)) && (
                              <span className="mt-1 block text-[11px] text-slate-400">Added {when(spec.created(record))}</span>
                            )}
                            {onlyHere(record.id).length > 0 && (
                              <span className="mt-1.5 flex flex-wrap gap-1">
                                {onlyHere(record.id).slice(0, 3).map((label) => (
                                  <span key={label} className="rounded-full bg-sky-50 px-1.5 py-px text-[10px] font-medium text-sky-700 ring-1 ring-sky-100">
                                    {label}
                                  </span>
                                ))}
                              </span>
                            )}
                          </span>
                          <span
                            className={cn(
                              'flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-2 transition-colors duration-200',
                              selected ? 'border-emerald-500 bg-emerald-500 text-white' : 'border-slate-300 bg-white',
                            )}
                            aria-hidden
                          >
                            {selected && <Check className="h-3 w-3 animate-[control-pop_0.3s_cubic-bezier(0.34,1.56,0.64,1)]" strokeWidth={3} />}
                          </span>
                          {selected && (
                            <motion.span
                              layoutId={reduce ? undefined : `keep-badge-${group.key}`}
                              className="absolute -top-2 left-3 rounded-full bg-emerald-500 px-2 py-0.5 text-[10px] font-bold tracking-wide text-white uppercase shadow-sm"
                              transition={{ type: 'spring', stiffness: 400, damping: 30 }}
                            >
                              Keeping
                            </motion.span>
                          )}
                        </motion.button>
                        {spec.href && (
                          <Link
                            to={spec.href(record)}
                            target="_blank"
                            rel="noreferrer"
                            className="absolute right-3 bottom-2.5 rounded text-slate-300 opacity-0 transition-opacity group-hover:opacity-100 hover:text-slate-500 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
                            aria-label={`Open ${spec.title(record)} in a new tab`}
                          >
                            <ExternalLink className="h-3.5 w-3.5" />
                          </Link>
                        )}
                        </div>
                      );
                    })}
                  </div>
                </section>

                {/* Side by side */}
                <section>
                  <h3 className="mb-2.5 text-xs font-semibold tracking-wide text-slate-500 uppercase">What the merged {spec.noun.one} will hold</h3>
                  {same.length > 0 && (
                    <p className="mb-2.5 flex flex-wrap items-center gap-1.5 text-xs text-slate-500">
                      <Check className="h-3.5 w-3.5 text-emerald-500" aria-hidden />
                      <span>The same in {group.records.length === 2 ? 'both' : `all ${group.records.length}`}:</span>
                      {same.map((r) => (
                        <span key={r.label} className="rounded-full bg-slate-100 px-2 py-0.5 font-medium text-slate-600" title={r.result}>{r.label}</span>
                      ))}
                    </p>
                  )}
                  {rows.length === 0 ? (
                    <p className="rounded-2xl border border-dashed border-slate-200 px-4 py-5 text-center text-sm text-slate-500">
                      Nothing else differs — the merged {spec.noun.one} is the one you keep, with everything attached to the others.
                    </p>
                  ) : (
                  <div className="overflow-x-auto rounded-2xl border border-slate-200">
                    <table className="w-full min-w-[520px] text-sm">
                      <thead>
                        <tr className="bg-slate-50/80 text-left text-xs text-slate-500">
                          <th scope="col" className="w-32 px-3 py-2 font-medium">Field</th>
                          {ordered.map((r, i) => (
                            <th key={r.id} scope="col" className="px-3 py-2 font-medium">
                              <span className="flex items-center gap-1.5">
                                {i === 0 && <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" aria-hidden />}
                                <span className="truncate">{i === 0 ? 'Kept' : `Duplicate${others.length > 1 ? ` ${i}` : ''}`}</span>
                              </span>
                            </th>
                          ))}
                          <th scope="col" className="bg-emerald-50 px-3 py-2 font-semibold text-emerald-700">After merge</th>
                        </tr>
                      </thead>
                      <tbody>
                        {rows.map((row, r) => (
                          <motion.tr
                            key={row.label}
                            className="border-t border-slate-100"
                            initial={reduce ? false : { opacity: 0 }}
                            animate={{ opacity: 1 }}
                            transition={{ duration: 0.25, delay: reduce ? 0 : 0.1 + r * 0.025 }}
                          >
                            <th scope="row" className="px-3 py-2 text-left text-xs font-medium whitespace-nowrap text-slate-500">{row.label}</th>
                            {row.values.map((value, i) => {
                              const used = row.from === i || (row.from === -1 && value !== undefined);
                              return (
                                <td key={ordered[i]?.id ?? i} className={cn('max-w-[16rem] px-3 py-2 align-top', used ? 'text-slate-900' : 'text-slate-400')}>
                                  <span className="line-clamp-2 break-words">{value ?? <span className="text-slate-300">—</span>}</span>
                                </td>
                              );
                            })}
                            <td className="max-w-[18rem] bg-emerald-50/70 px-3 py-2 align-top font-medium text-slate-900">
                              <motion.span
                                key={`${keep.id}:${row.result ?? ''}`}
                                className="flex items-start gap-1.5"
                                initial={reduce ? false : { opacity: 0, y: -4 }}
                                animate={{ opacity: 1, y: 0 }}
                                transition={{ duration: 0.25, ease: EASE }}
                              >
                                <span className="line-clamp-2 break-words">{row.result ?? <span className="font-normal text-slate-300">—</span>}</span>
                                {row.changed && (
                                  <span className="mt-0.5 inline-flex shrink-0 items-center gap-0.5 rounded-full bg-emerald-100 px-1.5 text-[10px] font-semibold text-emerald-700" title="Taken from a duplicate">
                                    <Sparkles className="h-2.5 w-2.5" aria-hidden />
                                    {row.from === -1 && row.rule !== 'sum' ? 'combined' : HOW[row.rule]}
                                  </span>
                                )}
                              </motion.span>
                            </td>
                          </motion.tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  )}
                </section>

                {/* What moves */}
                <section className="flex items-start gap-3 rounded-2xl border border-sky-100 bg-gradient-to-r from-sky-50 to-indigo-50/60 p-3.5 text-sm text-slate-600">
                  <span className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-white text-sky-600 shadow-sm">
                    <Icon className="h-4 w-4" aria-hidden />
                  </span>
                  <p>
                    Everything on the {others.length > 1 ? `${others.length} duplicates` : 'duplicate'} — {list(spec.moves)} — moves to{' '}
                    <strong className="text-slate-900">{spec.title(keep)}</strong>. {spec.fate(others.length)}
                  </p>
                </section>
              </motion.div>
            ) : null}
          </AnimatePresence>

          {/* Merged: a moment to see it worked before the next group. */}
          <AnimatePresence>
            {phase === 'merged' && (
              <motion.div
                className="absolute inset-0 z-10 flex flex-col items-center justify-center bg-white/90 backdrop-blur-[2px]"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.2 }}
                role="status"
              >
                <SuccessMark reduce={reduce} />
                <p className="mt-4 text-base font-semibold text-slate-900">Merged into {lastKept}</p>
                {index + 1 < queue.length && <p className="mt-1 text-sm text-slate-500">Next group coming up…</p>}
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        {/* Footer */}
        {phase !== 'done' && group && (
          <div className="border-t border-slate-100 bg-slate-50/70 px-6 py-4">
            <AnimatePresence>
              {error && (
                <motion.p
                  role="alert"
                  className="mb-3 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
                  initial={reduce ? false : { opacity: 0, y: -6 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0 }}
                >
                  {error}
                </motion.p>
              )}
            </AnimatePresence>
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center">
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" onClick={notSame} disabled={busy} className="text-slate-500">
                  <X />
                  Not the same
                </Button>
                {remaining > 1 && (
                  <Button variant="outline" size="sm" onClick={skip} disabled={busy}>
                    Skip for now
                  </Button>
                )}
              </div>
              <div className="flex flex-1 flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                {remaining > 1 && (
                  <Button variant="outline" onClick={confirmAll} disabled={busy}>
                    {bulk ? `Merging ${bulk.at} of ${bulk.of}…` : `Merge all ${remaining} groups`}
                  </Button>
                )}
                <Button
                  onClick={confirm}
                  disabled={busy}
                  className="bg-gradient-to-r from-emerald-500 to-teal-500 text-white shadow-md shadow-emerald-500/25 hover:from-emerald-600 hover:to-teal-600"
                  data-testid="confirm-merge"
                >
                  {busy && !bulk ? <Spinner size={16} /> : <GitMerge />}
                  {busy && !bulk ? 'Merging…' : `Merge ${group.records.length} into 1`}
                  {!busy && <ArrowRight className="opacity-70" />}
                </Button>
              </div>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** A tick drawn in, with rings going out: the merge worked. */
function SuccessMark({ reduce }: { reduce: boolean }) {
  return (
    <span className="relative flex h-20 w-20 items-center justify-center">
      {!reduce && [0, 1].map((i) => (
        <motion.span
          key={i}
          aria-hidden
          className="absolute inset-0 rounded-full border-2 border-emerald-400"
          initial={{ scale: 0.6, opacity: 0.8 }}
          animate={{ scale: 1.8, opacity: 0 }}
          transition={{ duration: 1, delay: 0.15 + i * 0.25, ease: 'easeOut' }}
        />
      ))}
      <motion.span
        className="flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-emerald-400 to-teal-500 text-white shadow-lg shadow-emerald-500/30"
        initial={reduce ? false : { scale: 0.3, rotate: -30 }}
        animate={{ scale: 1, rotate: 0 }}
        transition={{ type: 'spring', stiffness: 320, damping: 15 }}
      >
        <svg viewBox="0 0 24 24" className="h-8 w-8" fill="none" stroke="currentColor" strokeWidth={3} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
          <motion.path
            d="M5 12.5l4.5 4.5L19 7.5"
            initial={reduce ? false : { pathLength: 0 }}
            animate={{ pathLength: 1 }}
            transition={{ duration: 0.4, delay: 0.15, ease: 'easeOut' }}
          />
        </svg>
      </motion.span>
    </span>
  );
}

function DoneScreen({ tally, noun, reduce, onClose }: {
  tally: { merged: number; groups: number; skipped: number };
  noun: { one: string; many: string };
  reduce: boolean;
  onClose: () => void;
}) {
  return (
    <motion.div
      className="flex flex-col items-center px-6 py-10 text-center"
      initial={reduce ? false : { opacity: 0, scale: 0.96 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.35, ease: EASE }}
      role="status"
    >
      {tally.merged > 0 ? <SuccessMark reduce={reduce} /> : <CheckCircle2 className="h-14 w-14 text-slate-300" aria-hidden />}
      <h3 className="mt-5 text-lg font-bold text-slate-900">
        {tally.merged > 0
          ? `${tally.merged} duplicate ${tally.merged === 1 ? noun.one : noun.many} merged`
          : 'Nothing was merged'}
      </h3>
      <p className="mt-1 max-w-sm text-sm text-slate-500">
        {tally.merged > 0
          ? `Folded into ${tally.groups} ${tally.groups === 1 ? noun.one : noun.many}, with everything attached to them.`
          : 'The records are as they were.'}
        {tally.skipped > 0 && ` ${tally.skipped} ${tally.skipped === 1 ? 'group is' : 'groups are'} left for later.`}
      </p>
      <Button className="mt-6" onClick={onClose} data-autofocus>Done</Button>
    </motion.div>
  );
}
