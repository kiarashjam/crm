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

function WorkspaceCardImpl({
  org, index, isActive, isHighlighted, memberCount,
  isLaunching, isDimmed, reduceMotion, onSelect, onHover,
}: Props) {
  const hue = hueFor(org.id);
  const role = roleLabel(org);
  const RoleIcon = ROLE_ICON[role] ?? Users;
  // Two stops from one hue: enough variation to look designed, no palette to keep
  // in sync, and guaranteed distinct between workspaces.
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
      animate={
        isLaunching ? { opacity: 1, scale: 1 }
          : isDimmed ? { opacity: 0, scale: 0.92, filter: 'blur(6px)', y: 18 }
          : { opacity: 1, scale: 1, filter: 'blur(0px)', y: 0 }
      }
      transition={
        reduceMotion
          ? { duration: 0.12 }
          : { duration: 0.42, delay: isDimmed ? Math.min(index, 8) * 0.035 : 0, ease: [0.4, 0, 0.2, 1] }
      }
      whileHover={reduceMotion || isDimmed ? undefined : { y: -4 }}
      className={cn(
        'group relative isolate w-full overflow-hidden rounded-[26px] p-[1.5px] text-left',
        'transition-shadow duration-300 outline-none',
        'focus-visible:ring-2 focus-visible:ring-white/70 focus-visible:ring-offset-2 focus-visible:ring-offset-[#07080f]',
        isHighlighted ? 'shadow-[0_24px_70px_-24px_rgba(0,0,0,0.9)]' : 'shadow-[0_16px_50px_-30px_rgba(0,0,0,0.8)]',
      )}
      // The bright edge means "the cursor is here", and only one card can be that.
      // Giving the active workspace the same edge made two cards look selected at
      // once; it keeps a dimmed version of its own hue plus the badge and the dot,
      // which is identity without competing with the cursor.
      style={{
        background: isHighlighted
          ? `linear-gradient(140deg, ${from}, ${to})`
          : isActive
            ? `linear-gradient(140deg, hsl(${hue} 45% 34%), hsl(${(hue + 42) % 360} 42% 26%))`
            : 'linear-gradient(140deg, rgba(255,255,255,0.14), rgba(255,255,255,0.04))',
      }}
    >
      <motion.span
        layoutId={plateLayoutId(org.id)}
        // Plain CSS transitions only, so the layout animation owns transform.
        className="relative block overflow-hidden rounded-[24.5px] bg-[#0d0f18]"
      >
        {/* Ambient light in the workspace's own colour. */}
        <span
          aria-hidden
          className="pointer-events-none absolute -right-16 -top-20 h-56 w-56 rounded-full opacity-25 blur-3xl transition-opacity duration-500 group-hover:opacity-45"
          style={{ background: from }}
        />
        <span
          aria-hidden
          className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-white/25 to-transparent"
        />

        <span className="relative flex items-start gap-4 px-5 pt-5 sm:px-6">
          {/* Monogram */}
          <span
            className="relative flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl text-lg font-black tracking-tight text-white shadow-lg"
            style={{ background: `linear-gradient(140deg, ${from}, ${to})`, boxShadow: `0 12px 34px -12px ${from}` }}
          >
            {monogram(org.name)}
            {isActive && (
              <span className="absolute -bottom-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full bg-[#0d0f18]">
                <span className="h-2.5 w-2.5 rounded-full bg-emerald-400 shadow-[0_0_10px_2px_rgba(52,211,153,0.7)]" />
              </span>
            )}
          </span>

          <span className="min-w-0 flex-1">
            <span className="flex items-start justify-between gap-3">
              <span className="block truncate text-[1.0625rem] font-semibold text-white">{org.name}</span>
              {/* Digit shortcut — the launcher's whole promise is two keystrokes. */}
              {index < 9 && (
                <kbd className="hidden shrink-0 rounded-md border border-white/15 bg-white/5 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-white/50 sm:block">
                  {index + 1}
                </kbd>
              )}
            </span>

            <span className="mt-2 flex flex-wrap items-center gap-1.5">
              <span className="inline-flex items-center gap-1 rounded-full border border-white/10 bg-white/[0.06] px-2 py-0.5 text-[11px] font-medium text-white/70">
                <RoleIcon className="h-3 w-3" aria-hidden />
                {role}
              </span>
              {isActive && (
                <span className="inline-flex items-center gap-1 rounded-full border border-emerald-400/25 bg-emerald-400/10 px-2 py-0.5 text-[11px] font-semibold text-emerald-300">
                  Current
                </span>
              )}
              {/* Undefined = still loading, null = the request failed. Saying
                  "—" for a failure beats silently showing nothing, which reads
                  as "this workspace has no members". */}
              {memberCount !== undefined && (
                <span className="inline-flex items-center gap-1 rounded-full border border-white/10 bg-white/[0.06] px-2 py-0.5 text-[11px] font-medium text-white/70">
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
        <span className="relative mt-4 flex items-center justify-between gap-3 border-t border-white/[0.07] px-5 py-3 sm:px-6">
          <span className="truncate text-xs text-white/40">
            {isActive ? 'Continue where you left off' : 'Open this workspace'}
          </span>
          <span
            className={cn(
              'inline-flex shrink-0 items-center gap-1.5 text-xs font-semibold transition-all duration-300',
              isHighlighted ? 'translate-x-0 text-white opacity-100' : 'translate-x-1 text-white/40 opacity-0 group-hover:opacity-100',
            )}
          >
            Enter
            <CornerDownLeft className="h-3.5 w-3.5" aria-hidden />
          </span>
        </span>
      </motion.span>
    </motion.button>
  );
}

// Memoised because the parent re-renders on every keystroke in the search box,
// and a grid of cards each running a gradient and a blur is the one thing on this
// page that would make typing feel heavy.
export const WorkspaceCard = memo(WorkspaceCardImpl);
export default WorkspaceCard;
