// The workspace launchpad — the threshold you cross to get into a workspace.
//
// Deliberately unlike every other page in the app. `/organizations` is the one
// route inside `ProtectedLayout` but OUTSIDE `RequireOrgLayout`: it is the only
// screen that runs with no organisation selected, so nothing else in the product
// is reachable from here and none of the app's chrome would work if it were shown.
// Making it a dark, keyboard-first launcher rather than another cream dashboard is
// telling the truth about where you are.
//
// Three things it does that the previous version did not:
//
//  · It goes back where you were headed. `RequireOrgLayout` redirects here with
//    `state.from` set to the page you actually asked for; that was thrown away and
//    everyone landed on the dashboard.
//  · It says when a member count could not be read. The old effect swallowed the
//    error, so a failed request and "no members" looked identical.
//  · It cannot double-launch. The tile was clickable, Enter-activatable and (now)
//    digit-selectable, and nothing stopped two of those from firing two
//    navigations at once.
//
// The list/filter/launch logic lives in ./organizations/launcher.ts under test —
// it is the half that can be wrong in ways a screenshot will not reveal.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { AnimatePresence, motion } from 'motion/react';
import {
  ArrowLeft, Building2, Check, Clock, Command, CornerDownLeft, Crown, Loader2, LogOut,
  Mail, Plus, Search, Sparkles, TriangleAlert, UserCheck, UserPlus, Users, X,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  acceptInviteById, acceptJoinRequest, getOrgMembers, isOrgAdmin,
  listPendingJoinRequestsForOrg, rejectJoinRequest,
  type InviteDto, type JoinRequestDto, type Organization,
} from '@/app/api/organizations';
import { isUsingRealApi } from '@/app/api/apiClient';
import { useOrg } from '@/app/contexts/OrgContext';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { getCurrentUser } from '@/app/lib/auth';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import DemoBanner from '@/app/components/DemoBanner';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import {
  IDLE, beginLaunch, digitTarget, filterWorkspaces, isBusy, isLaunching,
  moveHighlight, orderWorkspaces, returnPath, type LaunchState,
} from './organizations/launcher';
import { prefetchRoute } from './organizations/prefetchRoute';
import WorkspaceCard from './organizations/WorkspaceCard';
import LaunchSequence from './organizations/LaunchSequence';
import CreateWorkspaceDialog from './organizations/CreateWorkspaceDialog';

/** Human label for a route, for "Taking you to …". */
function destinationLabel(path: string): string {
  const seg = path.split('?')[0]!.split('/').filter(Boolean);
  if (seg.length === 0) return 'your workspace';
  const first = seg[0]!;
  if (seg.length > 1) return `that ${first.replace(/s$/, '')}`;
  return first.charAt(0).toUpperCase() + first.slice(1);
}

/* ------------------------------------------------------------------ backdrop */

function Backdrop({ still }: { still: boolean }) {
  // Three slow-drifting colour fields plus a hairline grid. Blur is doing the work,
  // so this is two composited layers rather than anything per-frame expensive.
  const blobs = [
    { c: 'rgba(249,115,22,0.30)', s: 'h-[38rem] w-[38rem] -top-40 -left-24', d: 0 },
    { c: 'rgba(56,189,248,0.20)', s: 'h-[32rem] w-[32rem] top-1/3 -right-32', d: 3 },
    { c: 'rgba(168,85,247,0.18)', s: 'h-[30rem] w-[30rem] -bottom-40 left-1/4', d: 6 },
  ];
  return (
    <div aria-hidden className="pointer-events-none fixed inset-0 overflow-hidden">
      {blobs.map((b, i) => (
        <motion.div
          key={i}
          className={cn('absolute rounded-full blur-[120px]', b.s)}
          style={{ background: b.c }}
          animate={still ? undefined : { x: [0, 40, -20, 0], y: [0, -30, 20, 0], scale: [1, 1.08, 0.96, 1] }}
          transition={still ? undefined : { duration: 26, delay: b.d, repeat: Infinity, ease: 'easeInOut' }}
        />
      ))}
      <div
        className="absolute inset-0 opacity-[0.16]"
        style={{
          backgroundImage:
            'linear-gradient(to right, rgba(255,255,255,0.09) 1px, transparent 1px),'
            + 'linear-gradient(to bottom, rgba(255,255,255,0.09) 1px, transparent 1px)',
          backgroundSize: '68px 68px',
          maskImage: 'radial-gradient(78% 60% at 50% 38%, black, transparent)',
          WebkitMaskImage: 'radial-gradient(78% 60% at 50% 38%, black, transparent)',
        }}
      />
      <div className="absolute inset-0 bg-gradient-to-b from-transparent via-transparent to-[#07080f]" />
    </div>
  );
}

/* -------------------------------------------------------------- signal panel */

function Panel({
  tone, icon: Icon, title, children,
}: {
  tone: 'emerald' | 'violet' | 'amber';
  icon: typeof Mail;
  title: string;
  children?: React.ReactNode;
}) {
  const ring = {
    emerald: 'border-emerald-400/20 bg-emerald-400/[0.06]',
    violet: 'border-violet-400/20 bg-violet-400/[0.06]',
    amber: 'border-amber-400/20 bg-amber-400/[0.06]',
  }[tone];
  const chip = {
    emerald: 'bg-emerald-400/15 text-emerald-300',
    violet: 'bg-violet-400/15 text-violet-300',
    amber: 'bg-amber-400/15 text-amber-300',
  }[tone];
  return (
    <section className={cn('rounded-2xl border p-4 backdrop-blur-sm sm:p-5', ring)}>
      <div className="flex items-center gap-2.5">
        <span className={cn('flex h-8 w-8 shrink-0 items-center justify-center rounded-xl', chip)}>
          <Icon className="h-4 w-4" aria-hidden />
        </span>
        <h2 className="text-sm font-semibold text-white">{title}</h2>
      </div>
      {children && <div className="mt-3.5 space-y-2">{children}</div>}
    </section>
  );
}

/* ------------------------------------------------------------------ the page */

export default function Organizations() {
  const navigate = useNavigate();
  const location = useLocation();
  const reduceMotion = useMotionPreference();
  const {
    organizations, pendingInvites, refreshOrgs, loading, hasFetched,
    currentOrgId, setCurrentOrg,
  } = useOrg();

  const [query, setQuery] = useState('');
  const [highlight, setHighlight] = useState(0);
  const [launch, setLaunch] = useState<LaunchState>(IDLE);
  const [createOpen, setCreateOpen] = useState(false);
  const [acceptingId, setAcceptingId] = useState<string | null>(null);
  const [requestBusyId, setRequestBusyId] = useState<string | null>(null);
  /** id → count, or null when the request failed. Absent means still loading. */
  const [memberCounts, setMemberCounts] = useState<Record<string, number | null>>({});
  const [joinRequests, setJoinRequests] = useState<JoinRequestDto[]>([]);
  const [requestsFailed, setRequestsFailed] = useState(false);
  const searchRef = useRef<HTMLInputElement>(null);

  const cameFromApp = !!location.state?.fromApp || !!currentOrgId;
  const destination = returnPath(location.state?.from);
  const user = getCurrentUser();

  /* --- member counts and join requests -------------------------------------
   * One pass, in parallel, with an unmount guard. The old version fired a
   * detached async function per organisation and looped the owned ones
   * sequentially, and every failure fell into an empty `catch {}` — so a broken
   * endpoint rendered as "0 members" with no hint anything had gone wrong. */
  useEffect(() => {
    if (!isUsingRealApi() || !hasFetched) return;
    let alive = true;
    const ids = organizations.map((o) => o.id);
    const owned = organizations.filter((o) => o.isOwner).map((o) => o.id);

    void (async () => {
      const counts = await Promise.all(ids.map(async (id) => {
        try {
          return [id, (await getOrgMembers(id)).length] as [string, number | null];
        } catch {
          return [id, null] as [string, number | null];
        }
      }));
      if (!alive) return;
      const next: Record<string, number | null> = {};
      for (const [id, n] of counts) next[id] = n;
      setMemberCounts(next);

      if (owned.length === 0) {
        setJoinRequests([]);
        setRequestsFailed(false);
        return;
      }
      const batches = await Promise.all(owned.map(async (id) => {
        try {
          return await listPendingJoinRequestsForOrg(id);
        } catch {
          return null;
        }
      }));
      if (!alive) return;
      const flat: JoinRequestDto[] = [];
      for (const b of batches) if (b) flat.push(...b);
      setJoinRequests(flat);
      // A partial read is worse than a failed one, because the number shown looks
      // authoritative. Say so rather than under-reporting silently.
      setRequestsFailed(batches.some((b) => b === null));
    })();

    return () => { alive = false; };
  }, [organizations, hasFetched]);

  /* --- the list ----------------------------------------------------------- */

  const ordered = useMemo(
    () => orderWorkspaces(organizations, currentOrgId),
    [organizations, currentOrgId],
  );
  const visible = useMemo(() => filterWorkspaces(ordered, query), [ordered, query]);

  // Clamped at render rather than reconciled in an effect: filtering can shrink
  // the list under the cursor at any time, and a highlight index that is briefly
  // out of range is the classic source of "Enter did nothing".
  const hi = visible.length === 0 ? -1 : Math.min(Math.max(highlight, 0), visible.length - 1);

  /* --- launching ---------------------------------------------------------- */

  const launchingOrg = launch.orgId
    ? organizations.find((o) => o.id === launch.orgId) ?? null
    : null;

  const startLaunch = useCallback((orgId: string) => {
    setLaunch((s) => beginLaunch(s, orgId));
    // Outside the updater, which must stay pure — React is free to call it twice.
    // Safe to repeat: a dynamic import of an already-loaded module is a no-op, so
    // this needs no guard of its own. The download and the animation now overlap,
    // which is the point — by the time the overlay leaves, the page is ready.
    prefetchRoute(destination);
  }, [destination]);

  const completeLaunch = useCallback(() => {
    const org = organizations.find((o) => o.id === launch.orgId);
    if (!org) {
      // The workspace vanished mid-flight (a refresh removed it). Back out
      // rather than setting a context to an id the server no longer knows.
      setLaunch(IDLE);
      toast.error('That workspace is no longer available.');
      return;
    }
    const switching = org.id !== currentOrgId;
    if (switching) setCurrentOrg(org.id);
    // `replace` on purpose: the launcher is a threshold, not a destination, and
    // leaving it in history means Back lands you right back outside.
    navigate(destination, { replace: true });
    if (switching) toast.success(`Now in ${org.name}`);
  }, [organizations, launch.orgId, currentOrgId, setCurrentOrg, navigate, destination]);

  /* --- keyboard ----------------------------------------------------------- */

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      // Let every browser and OS shortcut through untouched.
      if (e.metaKey || e.ctrlKey || e.altKey) return;
      if (createOpen || isBusy(launch)) return;

      // Anything focused that already has its own key semantics keeps them. Two
      // concrete cases this fixes: Enter on the "New workspace" button used to
      // open the dialog AND start a launch, and a digit typed into any other
      // field on the page — a dialog, the help panel — was read as a shortcut.
      const target = e.target as HTMLElement | null;
      if (target) {
        const ownTile = target.closest('[data-workspace-tile]');
        const interactive = target.closest(
          'button, a, input, textarea, select, [role="button"], [contenteditable="true"]',
        );
        if (interactive && !ownTile && target !== searchRef.current) return;
      }

      if (e.key === 'ArrowDown' || e.key === 'ArrowRight') {
        e.preventDefault();
        setHighlight((h) => moveHighlight(h, 1, visible.length));
        return;
      }
      if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') {
        e.preventDefault();
        setHighlight((h) => moveHighlight(h, -1, visible.length));
        return;
      }
      if (e.key === 'Enter') {
        const org = hi >= 0 ? visible[hi] : undefined;
        if (org) {
          e.preventDefault();
          startLaunch(org.id);
        }
        return;
      }
      if (e.key === 'Escape') {
        if (query) {
          e.preventDefault();
          setQuery('');
          setHighlight(0);
        }
        return;
      }
      // Digits only while the search box is empty. The box is focused on load, so
      // claiming digits unconditionally would make a name like "Pavillon 46"
      // unsearchable — and with nothing typed yet there is no search in progress
      // for the shortcut to interrupt.
      if (query === '') {
        const target = digitTarget(e.key, visible);
        if (target) {
          e.preventDefault();
          startLaunch(target.id);
        }
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [visible, hi, query, createOpen, launch, startLaunch]);

  // Focus the search box on arrival, so the first keystroke filters. Not on a
  // phone: autofocus there throws up the on-screen keyboard, which covers half the
  // list you came here to read. And not for a single workspace, where the box is
  // noise anyway.
  useEffect(() => {
    if (!hasFetched || loading || organizations.length <= 1) return;
    const wideEnough = typeof window === 'undefined'
      || window.matchMedia('(min-width: 640px)').matches;
    if (wideEnough) searchRef.current?.focus();
  }, [hasFetched, loading, organizations.length]);

  /* --- invites and requests ----------------------------------------------- */

  const handleAcceptInvite = async (invite: InviteDto) => {
    setAcceptingId(invite.id);
    try {
      const accepted = await acceptInviteById(invite.id);
      if (!accepted) {
        toast.error('Could not accept that invitation.');
        return;
      }
      await refreshOrgs();
      setCurrentOrg(accepted.organizationId);
      toast.success(`You have joined ${accepted.organizationName}`);
      navigate(destination, { replace: true });
    } catch {
      toast.error('Could not accept that invitation.');
    } finally {
      setAcceptingId(null);
    }
  };

  const resolveRequest = async (request: JoinRequestDto, accept: boolean) => {
    setRequestBusyId(request.id);
    try {
      const result = accept ? await acceptJoinRequest(request.id) : await rejectJoinRequest(request.id);
      if (!result) {
        toast.error(accept ? 'Could not approve that request.' : 'Could not decline that request.');
        return;
      }
      setJoinRequests((prev) => prev.filter((r) => r.id !== request.id));
      toast.success(accept
        ? `${request.userName} can now use ${request.organizationName}`
        : `Declined ${request.userName}`);
      if (accept) {
        // Keep the tile's member count honest without a full reload. A failure
        // here must not look like the approval failed — it succeeded.
        try {
          const members = await getOrgMembers(request.organizationId);
          setMemberCounts((prev) => ({ ...prev, [request.organizationId]: members.length }));
        } catch {
          setMemberCounts((prev) => ({ ...prev, [request.organizationId]: null }));
        }
      }
    } catch {
      toast.error(accept ? 'Could not approve that request.' : 'Could not decline that request.');
    } finally {
      setRequestBusyId(null);
    }
  };

  const handleCreated = async (org: Organization) => {
    setCurrentOrg(org.id);
    await refreshOrgs();
    toast.success(`${org.name} is ready — invite your team`);
    navigate('/settings', { replace: true });
  };

  /* --- render ------------------------------------------------------------- */

  if (!hasFetched || loading) {
    return (
      <div className="relative flex min-h-screen items-center justify-center bg-[#07080f]">
        <Backdrop still={reduceMotion} />
        <div className="relative flex flex-col items-center gap-4">
          <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-400 to-amber-500 shadow-lg shadow-orange-500/25">
            <Sparkles className="h-6 w-6 text-white" aria-hidden />
          </span>
          <p className="flex items-center gap-2 text-sm text-white/50" role="status">
            <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />
            Finding your workspaces…
          </p>
        </div>
      </div>
    );
  }

  const owned = organizations.filter((o) => o.isOwner).length;
  const countsFailed = Object.values(memberCounts).some((v) => v === null);

  return (
    <div className="relative min-h-screen overflow-hidden bg-[#07080f] text-white">
      <Backdrop still={reduceMotion} />

      {/* Bespoke top bar rather than AppHeader. The header's navigation all leads
          to org-scoped pages, which from here would bounce straight back — and its
          light styling belongs to the other half of the app. */}
      <header className="relative z-10 flex h-16 items-center justify-between gap-3 px-[var(--page-padding)]" role="banner">
        <div className="flex items-center gap-2.5">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-amber-500 via-orange-500 to-amber-600 shadow-lg shadow-orange-500/25 ring-1 ring-white/20">
            <Sparkles className="h-4 w-4 text-white" aria-hidden />
          </span>
          <span className="text-[15px] font-semibold tracking-tight text-white/90">Cadence</span>
        </div>
        <div className="flex items-center gap-2">
          {user && (
            <span className="hidden truncate text-xs text-white/35 sm:block">{user.email}</span>
          )}
          {cameFromApp ? (
            <Link
              to={destination}
              className="inline-flex items-center gap-1.5 rounded-xl border border-white/10 px-3 py-1.5 text-xs font-medium text-white/60 transition-colors hover:bg-white/5 hover:text-white focus-visible:ring-2 focus-visible:ring-white/60 focus-visible:outline-none"
            >
              <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
              Back to {destinationLabel(destination)}
            </Link>
          ) : (
            <Link
              to="/login"
              className="inline-flex items-center gap-1.5 rounded-xl border border-white/10 px-3 py-1.5 text-xs font-medium text-white/60 transition-colors hover:bg-white/5 hover:text-white focus-visible:ring-2 focus-visible:ring-white/60 focus-visible:outline-none"
            >
              <LogOut className="h-3.5 w-3.5" aria-hidden />
              Sign in as someone else
            </Link>
          )}
        </div>
      </header>

      <DemoBanner />

      <main
        id={MAIN_CONTENT_ID}
        tabIndex={-1}
        className="relative z-10 w-full px-[var(--page-padding)] pt-8 pb-14 sm:pt-14 sm:pb-28"
      >
        {/* Hero */}
        <motion.div
          initial={reduceMotion ? false : { opacity: 0, y: 14 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5, ease: [0.16, 1, 0.3, 1] }}
        >
          <p className="text-[11px] font-semibold tracking-[0.32em] text-white/35 uppercase">
            {organizations.length === 0 ? 'Get started' : 'Choose a workspace'}
          </p>
          <h1 className="mt-3 text-3xl font-bold tracking-tight text-balance sm:text-[2.6rem] sm:leading-[1.08]">
            {organizations.length === 0
              ? 'Your first workspace'
              : <>Where are you<span className="text-white/35"> working today?</span></>}
          </h1>
          <p className="mt-3 max-w-2xl text-[15px] leading-relaxed text-white/45">
            {organizations.length === 0
              ? 'A workspace holds one business — its team, its leads, its pipeline. Nothing crosses between them.'
              : 'Each workspace keeps its own team, leads, deals and reporting. Pick one to go in.'}
          </p>
        </motion.div>

        {/* Search + counts + create */}
        <motion.div
          className="mt-9 flex flex-col gap-3 sm:flex-row sm:items-center"
          initial={reduceMotion ? false : { opacity: 0, y: 14 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5, delay: 0.06, ease: [0.16, 1, 0.3, 1] }}
        >
          {organizations.length > 1 && (
            <div className="relative flex-1 sm:max-w-xl">
              <Search className="pointer-events-none absolute top-1/2 left-4 h-4 w-4 -translate-y-1/2 text-white/30" aria-hidden />
              <input
                ref={searchRef}
                value={query}
                onChange={(e) => { setQuery(e.target.value); setHighlight(0); }}
                placeholder="Search workspaces…"
                aria-label="Search workspaces"
                autoComplete="off"
                spellCheck={false}
                className="h-12 w-full rounded-2xl border border-white/10 bg-white/[0.04] pr-11 pl-11 text-[15px] text-white placeholder:text-white/25 transition-colors focus:border-white/25 focus:bg-white/[0.07] focus:outline-none"
              />
              {query && (
                <button
                  type="button"
                  onClick={() => { setQuery(''); setHighlight(0); searchRef.current?.focus(); }}
                  aria-label="Clear search"
                  className="absolute top-1/2 right-3 -translate-y-1/2 rounded-lg p-1.5 text-white/35 transition-colors hover:bg-white/10 hover:text-white"
                >
                  <X className="h-4 w-4" aria-hidden />
                </button>
              )}
            </div>
          )}
          <Button
            onClick={() => setCreateOpen(true)}
            className="h-12 shrink-0 rounded-2xl bg-white px-5 font-semibold text-[#07080f] hover:bg-white/90 sm:ml-auto"
          >
            <Plus className="mr-1.5 h-4 w-4" aria-hidden />
            New workspace
          </Button>
        </motion.div>

        {/* A single honest line of counts, in place of five decorative tiles that
            mostly restated the length of the list below them. */}
        {organizations.length > 0 && (
          <div className="mt-4 flex flex-wrap items-center gap-x-2.5 gap-y-1.5 text-xs text-white/40">
            <span className="font-medium text-white/60">
              {organizations.length} workspace{organizations.length === 1 ? '' : 's'}
            </span>
            <span aria-hidden>·</span>
            <span className="inline-flex items-center gap-1"><Crown className="h-3 w-3" aria-hidden />{owned} owned</span>
            <span aria-hidden>·</span>
            <span className="inline-flex items-center gap-1"><Users className="h-3 w-3" aria-hidden />{organizations.length - owned} joined</span>
            {query && (
              <>
                <span aria-hidden>·</span>
                <span className="text-white/60">{visible.length} matching “{query}”</span>
              </>
            )}
          </div>
        )}

        {/* Signals */}
        <div className="mt-8 space-y-3">
          {pendingInvites.length > 0 && (
            <Panel
              tone="emerald"
              icon={Mail}
              title={`${pendingInvites.length} invitation${pendingInvites.length === 1 ? '' : 's'} waiting for you`}
            >
              {pendingInvites.map((invite) => (
                <div
                  key={invite.id}
                  className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-white/[0.07] bg-black/20 px-3.5 py-2.5"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-white">{invite.organizationName}</p>
                    <p className="mt-0.5 inline-flex items-center gap-1 text-[11px] text-white/40">
                      <Clock className="h-3 w-3" aria-hidden />
                      Expires {new Date(invite.expiresAtUtc).toLocaleDateString()}
                    </p>
                  </div>
                  <Button
                    size="sm"
                    onClick={() => void handleAcceptInvite(invite)}
                    disabled={acceptingId === invite.id}
                    className="h-9 rounded-xl bg-emerald-500 font-semibold text-white hover:bg-emerald-400"
                  >
                    {acceptingId === invite.id
                      ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
                      : <><UserPlus className="mr-1.5 h-3.5 w-3.5" aria-hidden /> Accept</>}
                  </Button>
                </div>
              ))}
            </Panel>
          )}

          {joinRequests.length > 0 && (
            <Panel
              tone="violet"
              icon={UserCheck}
              title={`${joinRequests.length} person${joinRequests.length === 1 ? '' : 's'} asking to join`}
            >
              {joinRequests.map((request) => (
                <div
                  key={request.id}
                  className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-white/[0.07] bg-black/20 px-3.5 py-2.5"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-white">{request.userName}</p>
                    <p className="truncate text-[11px] text-white/40">{request.userEmail}</p>
                    <p className="mt-0.5 inline-flex items-center gap-1 text-[11px] text-violet-300/70">
                      <Building2 className="h-3 w-3" aria-hidden />
                      {request.organizationName}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => void resolveRequest(request, false)}
                      disabled={requestBusyId === request.id}
                      className="h-9 rounded-xl border border-white/10 text-white/60 hover:bg-white/5 hover:text-white"
                    >
                      {requestBusyId === request.id
                        ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
                        : <><X className="mr-1 h-3.5 w-3.5" aria-hidden /> Decline</>}
                    </Button>
                    <Button
                      size="sm"
                      onClick={() => void resolveRequest(request, true)}
                      disabled={requestBusyId === request.id}
                      className="h-9 rounded-xl bg-violet-500 font-semibold text-white hover:bg-violet-400"
                    >
                      <Check className="mr-1 h-3.5 w-3.5" aria-hidden /> Approve
                    </Button>
                  </div>
                </div>
              ))}
            </Panel>
          )}

          {(countsFailed || requestsFailed) && (
            <Panel tone="amber" icon={TriangleAlert} title="Some details could not be loaded">
              <p className="text-[13px] leading-relaxed text-white/50">
                {countsFailed && 'Member counts are shown as “—” where the request failed. '}
                {requestsFailed && 'The list of pending join requests may be incomplete. '}
                Choosing a workspace still works normally.
              </p>
            </Panel>
          )}
        </div>

        {/* The grid */}
        <section className="mt-8" aria-label="Your workspaces">
          {organizations.length === 0 ? (
            <div className="rounded-3xl border border-dashed border-white/12 bg-white/[0.02] px-6 py-16 text-center">
              <span className="mx-auto flex h-16 w-16 items-center justify-center rounded-3xl bg-gradient-to-br from-orange-400 to-amber-500 shadow-xl shadow-orange-500/20">
                <Building2 className="h-8 w-8 text-white" aria-hidden />
              </span>
              <h2 className="mt-6 text-xl font-bold text-white">Nothing here yet</h2>
              <p className="mx-auto mt-2 max-w-sm text-sm leading-relaxed text-white/45">
                Create a workspace to start tracking leads, or accept an invitation
                to join one that already exists.
              </p>
              <Button
                onClick={() => setCreateOpen(true)}
                className="mt-7 h-12 rounded-2xl bg-gradient-to-r from-orange-500 to-amber-500 px-7 font-semibold text-white hover:from-orange-400 hover:to-amber-400"
              >
                <Plus className="mr-2 h-4 w-4" aria-hidden />
                Create your first workspace
              </Button>
            </div>
          ) : visible.length === 0 ? (
            <div className="rounded-3xl border border-white/[0.07] bg-white/[0.02] px-6 py-14 text-center">
              <p className="text-sm text-white/55">
                No workspace matches <span className="font-semibold text-white">“{query}”</span>.
              </p>
              <button
                type="button"
                onClick={() => { setQuery(''); setHighlight(0); searchRef.current?.focus(); }}
                className="mt-3 text-sm font-semibold text-orange-300 underline-offset-4 hover:underline"
              >
                Clear the search
              </button>
            </div>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
              {visible.map((org, i) => (
                <WorkspaceCard
                  key={org.id}
                  org={org}
                  index={i}
                  isActive={org.id === currentOrgId}
                  isHighlighted={i === hi}
                  memberCount={memberCounts[org.id]}
                  isLaunching={isLaunching(launch, org.id)}
                  isDimmed={isBusy(launch) && !isLaunching(launch, org.id)}
                  reduceMotion={reduceMotion}
                  onSelect={() => startLaunch(org.id)}
                  onHover={() => { setHighlight(i); prefetchRoute(destination); }}
                />
              ))}
            </div>
          )}
        </section>

        {/* Settings shortcut, only where it would actually work. Owners and
            managers can change a workspace; members and viewers cannot, and the
            old menu offered it to everyone. */}
        {currentOrgId && isOrgAdmin(organizations.find((o) => o.id === currentOrgId)) && (
          <p className="mt-6 text-xs text-white/35">
            Need to rename a workspace or manage its team?{' '}
            <Link to="/settings" className="font-semibold text-white/70 underline-offset-4 hover:text-white hover:underline">
              Open workspace settings
            </Link>
          </p>
        )}
      </main>

      {/* Keyboard legend. Fixed, because it is the page's contract with the user
          and scrolling it away would hide the only clue the shortcuts exist. */}
      {organizations.length > 0 && (
        // Desktop only. There is no keyboard to press on a phone, and a fixed
        // strip there is not a hint — it is something permanently covering the
        // bottom of the list.
        <div className="pointer-events-none fixed inset-x-0 bottom-0 z-10 hidden justify-center bg-gradient-to-t from-[#07080f] via-[#07080f]/90 to-transparent pt-10 pb-5 sm:flex">
          <div className="flex flex-wrap items-center justify-center gap-x-4 gap-y-2 rounded-2xl border border-white/[0.07] bg-white/[0.03] px-4 py-2.5 text-[11px] text-white/40 backdrop-blur-sm">
            {organizations.length > 1 && (
              <span className="inline-flex items-center gap-1.5">
                <kbd className="rounded border border-white/15 bg-white/5 px-1.5 py-0.5 font-mono">↑↓</kbd>
                move
              </span>
            )}
            <span className="inline-flex items-center gap-1.5">
              <kbd className="rounded border border-white/15 bg-white/5 px-1.5 py-0.5 font-mono">
                <CornerDownLeft className="h-2.5 w-2.5" aria-hidden />
              </kbd>
              open
            </span>
            {organizations.length > 1 && query === '' && (
              <span className="inline-flex items-center gap-1.5">
                <kbd className="rounded border border-white/15 bg-white/5 px-1.5 py-0.5 font-mono">1–9</kbd>
                jump
              </span>
            )}
            {organizations.length > 1 && (
              <span className="inline-flex items-center gap-1.5">
                <Command className="h-3 w-3" aria-hidden />
                just start typing to search
              </span>
            )}
          </div>
        </div>
      )}

      <AnimatePresence>
        {launchingOrg && (
          <LaunchSequence
            key={launchingOrg.id}
            org={launchingOrg}
            destinationLabel={destinationLabel(destination)}
            reduceMotion={reduceMotion}
            onComplete={completeLaunch}
          />
        )}
      </AnimatePresence>

      <CreateWorkspaceDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={handleCreated}
      />
    </div>
  );
}
