// One workspace on the launchpad.
//
// The card is a single <button>, not a div with role="button". That was the old
// shape and it meant re-implementing Enter/Space by hand, and it put the card in
// the tab order ahead of the menu nested inside it. A real button gets keyboard
// activation, :focus-visible and the right role for free.
//
// The `layoutId` on the plate is what makes the launch animation work: when the
// overlay mounts a plate with the same id, Framer measures both and interpolates
// the real geometry, so the tile you clicked is the thing that grows to fill the
// screen. Every visual layer that must survive that flight lives inside it.

import { memo } from 'react';
import { motion } from 'motion/react';
import { Crown, Eye, Users, ShieldCheck, CornerDownLeft } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { liftOnHover } from '@/app/components/motion/Stagger';
import type { Organization } from '@/app/api/organizations';
import { hueFor, monogram, roleLabel } from './launcher';

export const plateLayoutId = (orgId: string) => `workspace-plate-${orgId}`;

const ROLE_ICON: Record<string, typeof Crown> = {
  Owner: Crown,
  Manager: ShieldCheck,
  Viewer: Eye,
  Member: Users,
};

interface Props {
  org: Organization;
  index: number;
  isActive: boolean;
  isHighlighted: boolean;
  /** Undefined while the count is still loading, null when it could not be read. */
  memberCount?: number | null;
  /** True once this tile has been chosen — it stays put while the others leave. */
  isLaunching: boolean;
  /** True when some other tile was chosen. */
  isDimmed: boolean;
  reduceMotion: boolean;
  onSelect: () => void;
  onHover: () => void;
}

/**
 * One workspace on the launchpad: a single real <button>, so keyboard activation,
 * :focus-visible and the role come free.
 *
 * The white plate inside carries the `layoutId` the launch overlay shares, so the
 * tile you chose is the thing that grows to fill the screen. Memoised because the
 * parent re-renders on every keystroke in the search box.
 */
export const WorkspaceCard = memo(function WorkspaceCard({
  org, index, isActive, isHighlighted, memberCount,
  isLaunching, isDimmed, reduceMotion, onSelect, onHover,
}: Props) {
  const hue = hueFor(org.id);
  const role = roleLabel(org);
  const RoleIcon = ROLE_ICON[role] ?? Users;
  // Two stops from one hue: distinct per workspace, nothing to keep in sync.
  const from = `hsl(${hue} 82% 58%)`;
  const to = `hsl(${(hue + 42) % 360} 78% 48%)`;

  return (
    <motion.button
      type="button"
      onClick={onSelect}
      onMouseEnter={onHover}
      onFocus={onHover}
      data-testid={`workspace-${org.id}`}
      data-workspace-tile=""
      data-active={isActive || undefined}
      data-highlighted={isHighlighted || undefined}
      aria-current={isActive ? 'true' : undefined}
      aria-label={`Open ${org.name}${isActive ? ' (current workspace)' : ''}`}
      // The chosen tile must not be re-animated by the exit choreography — the
      // overlay takes over its plate, so anything applied here would fight it.
      // The entrance belongs to the StaggerItem around it, so none here.
      initial={false}
      animate={
        isLaunching ? { opacity: 1, scale: 1 }
          : isDimmed ? (reduceMotion ? { opacity: 0 } : { opacity: 0, scale: 0.94, filter: 'blur(6px)', y: 14 })
          : { opacity: 1, scale: 1, filter: 'blur(0px)', y: 0 }
      }
      transition={
        reduceMotion
          ? { duration: 0.12 }
          : { duration: 0.42, delay: isDimmed ? Math.min(index, 8) * 0.035 : 0, ease: [0.4, 0, 0.2, 1] }
      }
      className={cn(
        'group relative isolate block w-full rounded-2xl p-[1.5px] text-left outline-none',
        'focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-2 focus-visible:ring-offset-[#fdfbf5]',
        isHighlighted ? 'shadow-lg shadow-slate-900/10' : 'shadow-sm',
        !isDimmed && liftOnHover,
      )}
      // The coloured edge means "the cursor is here", and only one tile can be
      // that. The current workspace keeps a pale edge in its own hue, plus the
      // badge and the dot: identity without looking selected.
      style={{
        background: isHighlighted
          ? `linear-gradient(140deg, ${from}, ${to})`
          : isActive
            ? `linear-gradient(140deg, hsl(${hue} 80% 84%), hsl(${(hue + 42) % 360} 75% 80%))`
            : 'rgb(226 232 240 / 0.8)',
      }}
    >
      <motion.span
        layoutId={plateLayoutId(org.id)}
        // Plain CSS transitions only, so the layout animation owns transform.
        className="relative block overflow-hidden rounded-[14.5px] bg-white"
      >
        {/* The workspace's own colour, washing in from a corner. */}
        <span
          aria-hidden
          className="pointer-events-none absolute -top-16 -right-14 h-44 w-44 rounded-full opacity-[0.16] blur-3xl transition-opacity duration-500 group-hover:opacity-30 motion-reduce:transition-none"
          style={{ background: from }}
        />

        <span className="relative flex items-start gap-3.5 px-4 pt-4 sm:px-5">
          <span
            className="relative flex h-12 w-12 shrink-0 items-center justify-center rounded-xl text-base font-black tracking-tight text-white"
            style={{ background: `linear-gradient(140deg, ${from}, ${to})`, boxShadow: `0 10px 24px -10px ${from}` }}
          >
            <span aria-hidden className="absolute inset-0 rounded-xl bg-gradient-to-b from-white/30 to-transparent" />
            <span className="relative">{monogram(org.name)}</span>
            {isActive && (
              <span className="absolute -right-1 -bottom-1 flex h-5 w-5 items-center justify-center rounded-full bg-white shadow-sm">
                <span className="h-2.5 w-2.5 rounded-full bg-emerald-500 shadow-[0_0_8px_1px_rgba(16,185,129,0.55)]" />
              </span>
            )}
          </span>

          <span className="min-w-0 flex-1">
            <span className="flex items-start justify-between gap-3">
              <span className="block truncate text-[15px] font-semibold text-slate-900">{org.name}</span>
              {/* Digit shortcut — the launcher's whole promise is two keystrokes. */}
              {index < 9 && (
                <kbd className="hidden shrink-0 rounded-md border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-slate-500 sm:block">
                  {index + 1}
                </kbd>
              )}
            </span>

            <span className="mt-1.5 flex flex-wrap items-center gap-1.5">
              <span className="inline-flex items-center gap-1 rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                <RoleIcon className="h-3 w-3" aria-hidden />
                {role}
              </span>
              {isActive && (
                <span className="inline-flex items-center gap-1 rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                  Current
                </span>
              )}
              {/* Undefined = still loading, null = the request failed. Saying
                  "—" for a failure beats silently showing nothing, which reads
                  as "this workspace has no members". */}
              {memberCount !== undefined && (
                <span className="inline-flex items-center gap-1 rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                  <Users className="h-3 w-3" aria-hidden />
                  {memberCount === null
                    ? '—'
                    : `${memberCount} member${memberCount === 1 ? '' : 's'}`}
                </span>
              )}
            </span>
          </span>
        </span>

        {/* Enter affordance, revealed on the highlighted tile. */}
        <span className="relative mt-3.5 flex items-center justify-between gap-3 border-t border-slate-100 px-4 py-2.5 sm:px-5">
          <span className="truncate text-xs text-slate-500">
            {isActive ? 'Continue where you left off' : 'Open this workspace'}
          </span>
          <span
            className={cn(
              'inline-flex shrink-0 items-center gap-1.5 text-xs font-semibold text-slate-900 transition-all duration-300 motion-reduce:transition-none',
              isHighlighted ? 'translate-x-0 opacity-100' : 'translate-x-1 opacity-0 group-hover:opacity-100',
            )}
          >
            Enter
            <CornerDownLeft className="h-3.5 w-3.5" style={{ color: from }} aria-hidden />
          </span>
        </span>
      </motion.span>
    </motion.button>
  );
});

export default WorkspaceCard;
