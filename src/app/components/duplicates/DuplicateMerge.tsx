// Duplicates on a list page: a banner when some are found, and the merge dialog
// behind it. One line on each page —
//
//   <DuplicateMerge spec={CONTACT_DUPLICATES} onMerged={reload} />
//
// — and the page offers the same thing as every other: "2 duplicate contacts",
// with the people's initials, a button to review and merge them, and a way to
// put the banner away until something new turns up.

import { useMemo, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowRight, Copy, X } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { WriteOnly } from '@/app/components/WriteOnly';
import { initialsOf } from '@/app/components/RecordCard';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import type { DuplicateKind } from './kinds';
import { MergeDuplicatesDialog } from './MergeDuplicatesDialog';
import { useDuplicateGroups } from './useDuplicateGroups';

const EASE = [0.16, 1, 0.3, 1] as const;

const AVATARS = [
  'from-orange-500 to-pink-500',
  'from-violet-500 to-fuchsia-500',
  'from-sky-500 to-indigo-500',
  'from-emerald-500 to-teal-500',
];

const hiddenKey = (kind: string) => `cadence.duplicates.hidden.${kind}`;

/** The banner stays away for the groups it was put away with; a new one brings it back. */
function readHidden(kind: string): string {
  try {
    return sessionStorage.getItem(hiddenKey(kind)) ?? '';
  } catch {
    return '';
  }
}

interface Props<T extends { id: string }> {
  spec: DuplicateKind<T>;
  /** Reload the page's own list after a merge. */
  onMerged?: () => void;
  className?: string;
}

export function DuplicateMerge<T extends { id: string }>({ spec, onMerged, className }: Props<T>) {
  return (
    <WriteOnly>
      <DuplicateMergeInner spec={spec} onMerged={onMerged} className={className} />
    </WriteOnly>
  );
}

function DuplicateMergeInner<T extends { id: string }>({ spec, onMerged, className }: Props<T>) {
  const reduce = useMotionPreference();
  const { groups, extra, refresh, markNotSame } = useDuplicateGroups(spec);
  const [open, setOpen] = useState(false);
  const [hidden, setHidden] = useState(() => readHidden(spec.kind));
  const signature = useMemo(() => groups.map((g) => g.key).join(','), [groups]);
  const show = groups.length > 0 && signature !== hidden;

  const hide = () => {
    setHidden(signature);
    try {
      sessionStorage.setItem(hiddenKey(spec.kind), signature);
    } catch {
      // Hidden for now, at least.
    }
  };

  const faces = groups.flatMap((g) => g.records).slice(0, 4);
  const reasons = [...new Set(groups.flatMap((g) => g.reason.split(' · ')))].join(' · ');
  const noun = extra === 1 ? spec.noun.one : spec.noun.many;
  const groupNoun = groups.length === 1 ? spec.groupNoun.one : spec.groupNoun.many;

  return (
    <>
      <AnimatePresence initial={!reduce}>
        {show && (
          <motion.div
            key="duplicates-banner"
            role="region"
            aria-label={`Duplicate ${spec.noun.many}`}
            className={cn('overflow-hidden', className)}
            initial={reduce ? false : { opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={reduce ? { opacity: 0 } : { opacity: 0, height: 0 }}
            transition={{ duration: 0.4, ease: EASE }}
          >
            <div className="mb-4">
              <div className="relative flex flex-col gap-3 overflow-hidden rounded-2xl border border-amber-200/80 bg-gradient-to-r from-amber-50 via-orange-50 to-pink-50 p-3 shadow-sm sm:flex-row sm:items-center sm:gap-4 sm:p-3.5">
                {/* A light that sweeps across once in a while, to catch the eye without nagging. */}
                {!reduce && (
                  <motion.span
                    aria-hidden
                    className="pointer-events-none absolute inset-y-0 -left-1/3 w-1/3 bg-gradient-to-r from-transparent via-white/70 to-transparent"
                    animate={{ x: ['0%', '450%'] }}
                    transition={{ duration: 2.4, repeat: Infinity, repeatDelay: 4, ease: 'easeInOut' }}
                  />
                )}
                <div className="relative flex min-w-0 flex-1 items-center gap-3">
                  <span className="relative flex h-10 w-10 shrink-0 items-center justify-center">
                    {!reduce && (
                      <motion.span
                        aria-hidden
                        className="absolute inset-0 rounded-xl bg-amber-400/40"
                        animate={{ scale: [1, 1.35], opacity: [0.6, 0] }}
                        transition={{ duration: 1.6, repeat: Infinity, ease: 'easeOut' }}
                      />
                    )}
                    <span className="relative flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-amber-400 to-orange-500 text-white shadow-md shadow-orange-500/30">
                      <Copy className="h-5 w-5" aria-hidden />
                    </span>
                  </span>
                  <div className="min-w-0">
                    <p className="font-semibold text-slate-900">
                      {extra} duplicate {noun} found
                    </p>
                    <p className="truncate text-xs text-slate-600 sm:text-sm">
                      {groups.length} {groupNoun} {groups.length === 1 ? 'was' : 'were'} entered more than once
                      {reasons && <span className="text-slate-400"> · {reasons}</span>}
                    </p>
                  </div>
                  <div className="ml-auto hidden -space-x-2 md:flex" aria-hidden>
                    {faces.map((r, i) => (
                      <motion.span
                        key={r.id}
                        className={cn('flex h-8 w-8 items-center justify-center rounded-full bg-gradient-to-br text-[11px] font-bold text-white ring-2 ring-white', AVATARS[i % AVATARS.length])}
                        initial={reduce ? false : { opacity: 0, scale: 0.5, x: -6 }}
                        animate={{ opacity: 1, scale: 1, x: 0 }}
                        transition={{ type: 'spring', stiffness: 380, damping: 22, delay: reduce ? 0 : 0.15 + i * 0.06 }}
                      >
                        {initialsOf(spec.title(r))}
                      </motion.span>
                    ))}
                  </div>
                </div>
                <div className="relative flex items-center gap-2">
                  <Button
                    onClick={() => setOpen(true)}
                    className="flex-1 bg-gradient-to-r from-orange-500 to-pink-500 text-white shadow-md shadow-orange-500/25 hover:from-orange-600 hover:to-pink-600 sm:flex-none"
                    data-testid="review-duplicates"
                  >
                    Review & merge
                    <ArrowRight />
                  </Button>
                  <Button variant="ghost" size="icon" onClick={hide} aria-label="Hide for now" className="text-slate-400 hover:text-slate-600">
                    <X />
                  </Button>
                </div>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <MergeDuplicatesDialog
        spec={spec}
        groups={groups}
        open={open}
        onOpenChange={setOpen}
        onMerged={() => {
          void refresh();
          onMerged?.();
        }}
        onNotSame={(key) => markNotSame([key])}
      />
    </>
  );
}

export default DuplicateMerge;
