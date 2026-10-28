import { useState } from 'react';
import { motion } from 'motion/react';
import { ArrowRight, CheckCircle2, Copy, Loader2, RotateCcw } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero, { heroSecondary, type PageStat } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import {
  COMPANY_DUPLICATES, CONTACT_DUPLICATES, DEAL_DUPLICATES, HOUSEHOLD_DUPLICATES, LEAD_DUPLICATES, MEMBER_DUPLICATES,
  MergeDuplicatesDialog, useDuplicateGroups, type DuplicateKind,
} from '@/app/components/duplicates';
import type { DuplicateGroups } from '@/app/components/duplicates/useDuplicateGroups';

// Static class strings, because Tailwind only ships the classes it can read.
const TILE: Record<string, string> = {
  emerald: 'from-emerald-500 to-teal-500 shadow-emerald-500/30',
  blue: 'from-blue-500 to-indigo-500 shadow-blue-500/30',
  orange: 'from-orange-500 to-pink-500 shadow-orange-500/30',
  violet: 'from-violet-500 to-fuchsia-500 shadow-violet-500/30',
  indigo: 'from-indigo-500 to-violet-500 shadow-indigo-500/30',
  teal: 'from-teal-500 to-cyan-500 shadow-teal-500/30',
};

/** Every kind of record that can be entered twice, in one place. */
export default function Duplicates() {
  // Read straight away: on this page the duplicates are the content.
  const contacts = useDuplicateGroups(CONTACT_DUPLICATES, { delayMs: 0 });
  const companies = useDuplicateGroups(COMPANY_DUPLICATES, { delayMs: 0 });
  const leads = useDuplicateGroups(LEAD_DUPLICATES, { delayMs: 0 });
  const deals = useDuplicateGroups(DEAL_DUPLICATES, { delayMs: 0 });
  const members = useDuplicateGroups(MEMBER_DUPLICATES, { delayMs: 0 });
  const households = useDuplicateGroups(HOUSEHOLD_DUPLICATES, { delayMs: 0 });

  const kinds = [
    { spec: CONTACT_DUPLICATES, found: contacts },
    { spec: LEAD_DUPLICATES, found: leads },
    { spec: COMPANY_DUPLICATES, found: companies },
    { spec: DEAL_DUPLICATES, found: deals },
    { spec: MEMBER_DUPLICATES, found: members },
    { spec: HOUSEHOLD_DUPLICATES, found: households },
  ] as const;

  const loaded = kinds.every((k) => k.found.loaded);
  const [scanning, setScanning] = useState(false);
  usePageLoading(!loaded);

  const scan = async () => {
    setScanning(true);
    await Promise.all(kinds.map((k) => k.found.refresh()));
    setScanning(false);
  };

  const totalGroups = kinds.reduce((n, k) => n + k.found.groups.length, 0);
  const totalExtra = kinds.reduce((n, k) => n + k.found.extra, 0);
  const stats: PageStat[] = [
    { label: 'Duplicates', value: totalExtra, icon: Copy, tone: 'rose', hint: totalGroups === 0 ? 'all clean' : `in ${totalGroups} ${totalGroups === 1 ? 'group' : 'groups'}` },
    ...kinds.map((k): PageStat => ({
      label: k.spec.noun.many[0]!.toUpperCase() + k.spec.noun.many.slice(1),
      value: k.found.extra,
      icon: k.spec.icon,
      tone: k.spec.tone,
      hint: k.found.groups.length === 0 ? 'clean' : `${k.found.groups.length} to review`,
    })),
  ];

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Copy}
            tone="rose"
            title="Duplicates"
            subtitle="Records entered more than once, ready to merge into one."
            actions={
              <Button onClick={scan} disabled={scanning || !loaded} variant="outline" className={cn(heroSecondary, 'disabled:opacity-60')}>
                {scanning ? <Loader2 className="h-4 w-4 animate-spin" /> : <RotateCcw className="h-4 w-4" />} Scan again
              </Button>
            }
            stats={loaded ? stats : undefined}
          />

          {loaded && totalGroups === 0 && (
            <motion.div
              className="mb-6 rounded-2xl border border-dashed border-emerald-200 bg-emerald-50/50 py-10 text-center"
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
            >
              <CheckCircle2 className="mx-auto h-10 w-10 text-emerald-400" />
              <p className="mt-3 text-sm font-medium text-slate-700">No duplicates anywhere</p>
              <p className="mt-1 text-sm text-slate-500">Every contact, lead, company, deal, member and household is in once.</p>
            </motion.div>
          )}

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <KindCard index={0} spec={CONTACT_DUPLICATES} found={contacts} />
            <KindCard index={1} spec={LEAD_DUPLICATES} found={leads} />
            <KindCard index={2} spec={COMPANY_DUPLICATES} found={companies} />
            <KindCard index={3} spec={DEAL_DUPLICATES} found={deals} />
            <KindCard index={4} spec={MEMBER_DUPLICATES} found={members} />
            <KindCard index={5} spec={HOUSEHOLD_DUPLICATES} found={households} />
          </div>
        </main>
      </PageTransition>
    </div>
  );
}

function KindCard<T extends { id: string }>({ index, spec, found }: { index: number; spec: DuplicateKind<T>; found: DuplicateGroups<T> }) {
  const [open, setOpen] = useState(false);
  const { groups, extra, loaded, notSameCount } = found;
  const Icon = spec.icon;
  const clean = loaded && groups.length === 0;

  return (
    <StaggerItem index={index} className={cn('flex flex-col rounded-2xl border border-slate-200 bg-white p-4 shadow-sm', liftOnHover)}>
      <div className="flex items-center gap-3">
        <span className={cn('flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br text-white shadow-md', TILE[spec.tone] ?? TILE.orange)}>
          <Icon className="h-5 w-5" aria-hidden />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="font-semibold text-slate-900 capitalize">{spec.noun.many}</h2>
          <p className="text-xs text-slate-500">
            {!loaded
              ? 'Looking…'
              : clean
                ? 'No duplicates'
                : `${extra} duplicate ${extra === 1 ? spec.noun.one : spec.noun.many} · ${groups.length} ${groups.length === 1 ? spec.groupNoun.one : spec.groupNoun.many}`}
          </p>
        </div>
        {clean && <CheckCircle2 className="h-5 w-5 text-emerald-400" aria-label="Clean" />}
      </div>

      {groups.length > 0 && (
        <ul className="mt-3 space-y-1.5">
          {groups.slice(0, 3).map((g) => (
            <li key={g.key} className="flex items-center gap-2 rounded-lg bg-slate-50 px-2.5 py-1.5 text-sm">
              <span className="min-w-0 flex-1 truncate font-medium text-slate-700">{spec.title(g.records[0]!)}</span>
              <span className="shrink-0 rounded-full bg-amber-100 px-1.5 text-[11px] font-semibold text-amber-700">×{g.records.length}</span>
              <span className="hidden shrink-0 text-[11px] text-slate-500 sm:inline">{g.reason}</span>
            </li>
          ))}
          {groups.length > 3 && <li className="px-2.5 text-xs text-slate-500">and {groups.length - 3} more</li>}
        </ul>
      )}

      <div className="mt-auto flex items-center justify-between gap-2 pt-4">
        {notSameCount > 0 ? (
          <button type="button" onClick={found.clearNotSame} className="text-xs text-slate-500 underline-offset-2 hover:text-slate-600 hover:underline">
            {notSameCount} marked not the same · show again
          </button>
        ) : <span />}
        {groups.length > 0 && (
          <WriteOnly>
            <Button size="sm" onClick={() => setOpen(true)} className="bg-gradient-to-r from-orange-500 to-pink-500 text-white hover:from-orange-600 hover:to-pink-600">
              Review & merge
              <ArrowRight />
            </Button>
          </WriteOnly>
        )}
      </div>

      <MergeDuplicatesDialog
        spec={spec}
        groups={groups}
        open={open}
        onOpenChange={setOpen}
        onMerged={() => { void found.refresh(); }}
        onNotSame={(key) => found.markNotSame([key])}
      />
    </StaggerItem>
  );
}
