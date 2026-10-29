// The workspace launchpad — the threshold you cross to get into a workspace.
//
// `/organizations` is the one route inside `ProtectedLayout` but OUTSIDE
// `RequireOrgLayout`: it is the only screen that runs with no organisation
// selected, so nothing else in the product is reachable from here and none of the
// app's chrome would work if it were shown. It wears the app's light look and the
// same hero as every other page, under a top bar of its own, with a
// keyboard-first launcher beneath it.
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
import { AnimatePresence } from 'motion/react';
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
import { servesDemoData } from '@/app/api/apiClient';
import { useOrg } from '@/app/contexts/OrgContext';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { NextActions } from '@/app/salesAgent/NextActions';
import { getCurrentUser } from '@/app/lib/auth';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import DemoBanner from '@/app/components/DemoBanner';
import PageHero, { heroPrimary, type PageStat } from '@/app/components/PageHero';
import { StaggerItem } from '@/app/components/motion/Stagger';
import { CadenceLoader } from '@/app/components/motion/CadenceLoader';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import {
  IDLE, beginLaunch, digitTarget, filterWorkspaces, isBusy, isLaunching,
  moveHighlight, orderWorkspaces, returnPath, type LaunchState,
} from './organizations/launcher';
import { prefetchRoute } from './organizations/prefetchRoute';
import { WorkspaceCard } from './organizations/WorkspaceCard';
import LaunchSequence from './organizations/LaunchSequence';
import CreateWorkspaceDialog from './organizations/CreateWorkspaceDialog';

/** The page's colour, for the hero, its main action and the focus rings. */
const TONE = 'indigo' as const;

/** Human label for a route, for "Taking you to …". */
function destinationLabel(path: string): string {
  const seg = path.split('?')[0]!.split('/').filter(Boolean);
  if (seg.length === 0) return 'your workspace';
  const first = seg[0]!;
  if (seg.length > 1) return `that ${first.replace(/s$/, '')}`;
  return first.charAt(0).toUpperCase() + first.slice(1);
}

/* -------------------------------------------------------------- signal panel */

const PANEL = {
  emerald: { chip: 'from-emerald-400 to-teal-500 shadow-emerald-500/30', edge: 'from-emerald-400 via-teal-400 to-cyan-400' },
  violet: { chip: 'from-violet-500 to-fuchsia-500 shadow-violet-500/30', edge: 'from-violet-500 via-fuchsia-500 to-pink-400' },
  amber: { chip: 'from-amber-400 to-orange-500 shadow-amber-500/30', edge: 'from-amber-400 via-orange-400 to-rose-400' },
} as const;

function Panel({
  tone, icon: Icon, title, index, children,
}: {
  tone: keyof typeof PANEL;
  icon: typeof Mail;
  title: string;
  index: number;
  children?: React.ReactNode;
}) {
  const p = PANEL[tone];
  return (
    <StaggerItem
      as="section"
      index={index}
      className="relative overflow-hidden rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm sm:p-5"
    >
      <span aria-hidden className={cn('absolute inset-x-0 top-0 h-0.5 bg-gradient-to-r', p.edge)} />
      <div className="flex items-center gap-2.5">
        <span className={cn('relative flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br text-white shadow-md', p.chip)}>
          <span aria-hidden className="absolute inset-0 rounded-xl bg-gradient-to-b from-white/30 to-transparent" />
          <Icon className="relative h-4 w-4" aria-hidden />
        </span>
        <h2 className="text-sm font-semibold text-slate-900">{title}</h2>
      </div>
      {children && <div className="mt-3.5 space-y-2">{children}</div>}
    </StaggerItem>
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
  usePageLoading(!hasFetched || loading);

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
    if (servesDemoData() || !hasFetched) return;
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
      <div className="flex min-h-screen items-center justify-center bg-gradient-subtle">
        <div className="flex flex-col items-center">
          <CadenceLoader size="lg" />
          <p
            className="mt-5 bg-gradient-to-r from-slate-500 via-indigo-500 to-slate-500 bg-[length:200%_100%] bg-clip-text text-sm font-semibold text-transparent motion-safe:animate-[shimmer_3s_linear_infinite]"
            role="status"
          >
            Finding your workspaces…
          </p>
        </div>
      </div>
    );
  }

  const none = organizations.length === 0;
  const owned = organizations.filter((o) => o.isOwner).length;
  const joined = organizations.length - owned;
  const countsFailed = Object.values(memberCounts).some((v) => v === null);
  const hasSignals = pendingInvites.length > 0 || joinRequests.length > 0 || countsFailed || requestsFailed;

  // The counts the old page gave as a line of text, as the hero's figures.
  const stats: PageStat[] = [
    ...(none ? [] : [
      { label: 'Workspaces', value: organizations.length, icon: Building2, tone: 'indigo' },
      { label: 'Owned', value: owned, icon: Crown, tone: 'amber', progress: owned / organizations.length },
      { label: 'Joined', value: joined, icon: Users, tone: 'sky', progress: joined / organizations.length },
    ] satisfies PageStat[]),
    ...(pendingInvites.length > 0
      ? [{ label: 'Invitations waiting', value: pendingInvites.length, icon: Mail, tone: 'emerald' } satisfies PageStat]
      : []),
  ];

  return (
    <div className="relative min-h-screen bg-gradient-subtle text-slate-900">
      {/* Bespoke top bar rather than AppHeader. The header's navigation all leads
          to org-scoped pages, which from here would bounce straight back. */}
      <header
        className="sticky top-0 z-40 border-b border-slate-200/60 bg-white/80 backdrop-blur-md"
        role="banner"
      >
        <div className="flex h-16 items-center justify-between gap-3 px-[var(--page-padding)]">
          <div className="flex items-center gap-2.5">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-amber-500 via-orange-500 to-amber-600 shadow-lg shadow-amber-500/25 ring-1 ring-white/20">
              <Sparkles className="h-5 w-5 text-white drop-shadow-sm" aria-hidden />
            </span>
            <span className="text-lg font-semibold tracking-tight text-slate-800">Cadence</span>
          </div>
          <div className="flex min-w-0 items-center gap-2">
            {user && (
              <span className="hidden truncate text-xs text-slate-500 sm:block">{user.email}</span>
            )}
            {cameFromApp ? (
              <Link
                to={destination}
                className="inline-flex shrink-0 items-center gap-1.5 rounded-xl border border-slate-200/80 bg-white/70 px-3 py-1.5 text-xs font-medium text-slate-600 shadow-sm transition-colors hover:border-slate-300 hover:bg-white hover:text-slate-900 focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:outline-none"
              >
                <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
                Back to {destinationLabel(destination)}
              </Link>
            ) : (
              <Link
                to="/login"
                className="inline-flex shrink-0 items-center gap-1.5 rounded-xl border border-slate-200/80 bg-white/70 px-3 py-1.5 text-xs font-medium text-slate-600 shadow-sm transition-colors hover:border-slate-300 hover:bg-white hover:text-slate-900 focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:outline-none"
              >
                <LogOut className="h-3.5 w-3.5" aria-hidden />
                Sign in as someone else
              </Link>
            )}
          </div>
        </div>
      </header>

      <DemoBanner />

      <main
        id={MAIN_CONTENT_ID}
        tabIndex={-1}
        className="relative w-full px-[var(--page-padding)] pt-[var(--main-block-padding-y)] pb-16 sm:pb-28"
      >
        {/* No workspace, so no agent: its panel needs one to read from. */}
        <PageHero
          noAgentSlot
          icon={Building2}
          tone={TONE}
          title={none ? 'Your first workspace' : 'Where are you working today?'}
          subtitle={none
            ? 'A workspace holds one business — its team, its leads, its pipeline. Nothing crosses between them.'
            : 'Each workspace keeps its own team, leads, deals and reporting. Pick one to go in.'}
          badges={(
            <span className="hidden items-center rounded-full bg-indigo-100 px-2 py-0.5 text-[11px] font-semibold text-indigo-700 sm:inline-flex">
              {none ? 'Get started' : 'Choose a workspace'}
            </span>
          )}
          actions={(
            <Button onClick={() => setCreateOpen(true)} className={heroPrimary(TONE)}>
              <Plus className="h-4 w-4" aria-hidden />
              New workspace
            </Button>
          )}
          stats={stats}
        >
          {organizations.length > 1 && (
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-3">
              <div className="relative flex-1 sm:max-w-xl">
                <Search className="pointer-events-none absolute top-1/2 left-3.5 z-10 h-4 w-4 -translate-y-1/2 text-slate-500" aria-hidden />
                <input
                  ref={searchRef}
                  value={query}
                  onChange={(e) => { setQuery(e.target.value); setHighlight(0); }}
                  placeholder="Search workspaces…"
                  aria-label="Search workspaces"
                  autoComplete="off"
                  spellCheck={false}
                  className="h-10 w-full rounded-xl border border-slate-200 bg-white/80 pr-10 pl-10 text-sm text-slate-900 shadow-sm transition-[border-color,box-shadow] placeholder:text-slate-400 focus:border-indigo-300 focus:ring-4 focus:ring-indigo-200/50 focus:outline-none motion-reduce:transition-none"
                />
                {query && (
                  <button
                    type="button"
                    onClick={() => { setQuery(''); setHighlight(0); searchRef.current?.focus(); }}
                    aria-label="Clear search"
                    className="absolute top-1/2 right-2 -translate-y-1/2 rounded-lg p-1.5 text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-700 focus-visible:ring-2 focus-visible:ring-indigo-300 focus-visible:outline-none"
                  >
                    <X className="h-4 w-4" aria-hidden />
                  </button>
                )}
              </div>
              {query && (
                <p className="text-xs font-medium text-slate-500">
                  {visible.length} matching “{query}”
                </p>
              )}
            </div>
          )}
        </PageHero>

        {/* Signals */}
        {hasSignals && (
          <div className="mb-4 space-y-3">
            {pendingInvites.length > 0 && (
              <Panel
                tone="emerald"
                icon={Mail}
                index={0}
                title={`${pendingInvites.length} invitation${pendingInvites.length === 1 ? '' : 's'} waiting for you`}
              >
                {pendingInvites.map((invite) => (
                  <div
                    key={invite.id}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-100 bg-slate-50/70 px-3.5 py-2.5"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-slate-900">{invite.organizationName}</p>
                      <p className="mt-0.5 inline-flex items-center gap-1 text-[11px] text-slate-500">
                        <Clock className="h-3 w-3" aria-hidden />
                        Expires {new Date(invite.expiresAtUtc).toLocaleDateString()}
                      </p>
                    </div>
                    <Button
                      size="sm"
                      onClick={() => void handleAcceptInvite(invite)}
                      disabled={acceptingId === invite.id}
                      className="h-9 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 font-semibold text-white shadow-md shadow-emerald-500/20 hover:opacity-95"
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
                index={1}
                title={`${joinRequests.length} person${joinRequests.length === 1 ? '' : 's'} asking to join`}
              >
                {joinRequests.map((request) => (
                  <div
                    key={request.id}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-100 bg-slate-50/70 px-3.5 py-2.5"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-slate-900">{request.userName}</p>
                      <p className="truncate text-[11px] text-slate-500">{request.userEmail}</p>
                      <p className="mt-0.5 inline-flex items-center gap-1 text-[11px] font-medium text-violet-600">
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
                        className="h-9 rounded-xl border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 hover:text-slate-900"
                      >
                        {requestBusyId === request.id
                          ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
                          : <><X className="mr-1 h-3.5 w-3.5" aria-hidden /> Decline</>}
                      </Button>
                      <Button
                        size="sm"
                        onClick={() => void resolveRequest(request, true)}
                        disabled={requestBusyId === request.id}
                        className="h-9 rounded-xl bg-gradient-to-r from-violet-500 to-fuchsia-500 font-semibold text-white shadow-md shadow-violet-500/20 hover:opacity-95"
                      >
                        <Check className="mr-1 h-3.5 w-3.5" aria-hidden /> Approve
                      </Button>
                    </div>
                  </div>
                ))}
              </Panel>
            )}

            {(countsFailed || requestsFailed) && (
              <Panel tone="amber" icon={TriangleAlert} index={2} title="Some details could not be loaded">
                <p className="text-[13px] leading-relaxed text-slate-600">
                  {countsFailed && 'Member counts are shown as “—” where the request failed. '}
                  {requestsFailed && 'The list of pending join requests may be incomplete. '}
                  Choosing a workspace still works normally.
                </p>
              </Panel>
            )}
          </div>
        )}

        {/* The grid */}
        <section aria-label="Your workspaces">
          {none ? (
            <StaggerItem className="rounded-2xl border border-dashed border-slate-300 bg-white/70 px-6 py-14 text-center shadow-sm">
              <span className="relative mx-auto flex h-14 w-14 items-center justify-center overflow-hidden rounded-2xl bg-gradient-to-br from-orange-400 via-amber-500 to-pink-500 shadow-lg shadow-orange-500/25">
                <span aria-hidden className="absolute inset-0 bg-gradient-to-b from-white/30 to-transparent" />
                <Building2 className="relative h-7 w-7 text-white" aria-hidden />
              </span>
              <h2 className="mt-5 text-lg font-bold text-slate-900">Nothing here yet</h2>
              <p className="mx-auto mt-2 max-w-sm text-sm leading-relaxed text-slate-500">
                Create a workspace to start tracking leads, or accept an invitation
                to join one that already exists.
              </p>
              <Button onClick={() => setCreateOpen(true)} className={cn(heroPrimary(TONE), 'mt-6 h-11 px-6')}>
                <Plus className="h-4 w-4" aria-hidden />
                Create your first workspace
              </Button>
            </StaggerItem>
          ) : visible.length === 0 ? (
            <div className="rounded-2xl border border-slate-200/80 bg-white px-6 py-12 text-center shadow-sm">
              <p className="text-sm text-slate-500">
                No workspace matches <span className="font-semibold text-slate-900">“{query}”</span>.
              </p>
              <button
                type="button"
                onClick={() => { setQuery(''); setHighlight(0); searchRef.current?.focus(); }}
                className="mt-3 rounded text-sm font-semibold text-indigo-600 underline-offset-4 hover:text-indigo-700 hover:underline focus-visible:ring-2 focus-visible:ring-indigo-300 focus-visible:outline-none"
              >
                Clear the search
              </button>
            </div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2 sm:gap-4 xl:grid-cols-3 2xl:grid-cols-4">
              {visible.map((org, i) => (
                <StaggerItem key={org.id} index={i}>
                  <WorkspaceCard
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
                </StaggerItem>
              ))}
            </div>
          )}
        </section>

        {/* Settings shortcut, only where it would actually work. Owners and
            managers can change a workspace; members and viewers cannot, and the
            old menu offered it to everyone. */}
        {currentOrgId && isOrgAdmin(organizations.find((o) => o.id === currentOrgId)) && (
          <p className="mt-5 text-xs text-slate-500">
            Need to rename a workspace or manage its team?{' '}
            <Link
              to="/settings"
              className="rounded font-semibold text-indigo-600 underline-offset-4 hover:text-indigo-700 hover:underline focus-visible:ring-2 focus-visible:ring-indigo-300 focus-visible:outline-none"
            >
              Open workspace settings
            </Link>
          </p>
        )}
        {/* Outside a page's slot the panel brings its own page gutter, so it
            steps back out of this one to line up with the hero and the grid. */}
        {currentOrgId && (
          <div className="-mx-[var(--page-padding)] mt-4">
            <NextActions />
          </div>
        )}
      </main>

      {/* Keyboard legend. Fixed, because it is the page's contract with the user
          and scrolling it away would hide the only clue the shortcuts exist. */}
      {organizations.length > 0 && (
        // Desktop only. There is no keyboard to press on a phone, and a fixed
        // strip there is not a hint — it is something permanently covering the
        // bottom of the list.
        <div aria-hidden className="pointer-events-none fixed inset-x-0 bottom-0 z-10 hidden justify-center pb-5 sm:flex">
          <div className="flex flex-wrap items-center justify-center gap-x-4 gap-y-2 rounded-2xl border border-slate-200/80 bg-white/85 px-4 py-2.5 text-[11px] text-slate-500 shadow-lg shadow-slate-900/5 backdrop-blur">
            {organizations.length > 1 && (
              <span className="inline-flex items-center gap-1.5">
                <kbd className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-slate-600">↑↓</kbd>
                move
              </span>
            )}
            <span className="inline-flex items-center gap-1.5">
              <kbd className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-slate-600">
                <CornerDownLeft className="h-2.5 w-2.5" aria-hidden />
              </kbd>
              open
            </span>
            {organizations.length > 1 && query === '' && (
              <span className="inline-flex items-center gap-1.5">
                <kbd className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-slate-600">1–9</kbd>
                jump
              </span>
            )}
            {organizations.length > 1 && (
              <span className="inline-flex items-center gap-1.5">
                <Command className="h-3 w-3 text-indigo-500" aria-hidden />
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
