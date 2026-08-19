// Logic for the workspace launcher: ordering, filtering, and the launch sequence.
//
// Kept out of the component because it is the part that can be wrong in ways a
// screenshot will not reveal — a comparator that is not a total order, a filter
// that misses accented names, a launch that fires twice and navigates mid-flight.

import type { Organization } from '@/app/api/organizations';

/**
 * Case- and accent-insensitive search key.
 *
 * Organisation names here are Swiss and French — "Pavillon", "Léman", "Zürich" —
 * so a plain `toLowerCase().includes()` would fail to match "leman" against
 * "Léman". Normalising to NFD and stripping combining marks fixes that.
 */
export function searchKey(s: string): string {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
}

/** Workspaces whose name matches the query. An empty query matches everything. */
export function filterWorkspaces(orgs: Organization[], query: string): Organization[] {
  const q = searchKey(query);
  if (q.length === 0) return orgs;
  return orgs.filter((o) => searchKey(o.name).includes(q));
}

/**
 * Display order: the active workspace first, then ones you own, then by name.
 *
 * Written as a chain of numeric comparisons rather than early `return -1`s. The
 * previous version returned -1 as soon as `a` was the active org without ever
 * comparing `b`, which is fine for one active id but is not a shape that stays
 * correct if a second "pinned" rule is ever added. Ties break on id so the order
 * is total and the list cannot reshuffle between renders.
 */
export function orderWorkspaces(orgs: Organization[], currentOrgId: string | null): Organization[] {
  const rank = (o: Organization) => (o.id === currentOrgId ? 0 : o.isOwner ? 1 : 2);
  return [...orgs].sort((a, b) =>
    rank(a) - rank(b)
    || a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
    || a.id.localeCompare(b.id));
}

/** Move a highlighted index by `delta`, wrapping at both ends. */
export function moveHighlight(current: number, delta: number, count: number): number {
  if (count <= 0) return -1;
  // `% count` alone goes negative for a left move from 0, which would index out
  // of the list and blank the selection.
  return ((current + delta) % count + count) % count;
}

/**
 * Which workspace a keypress selects, or null for "not a selection key".
 *
 * Digits 1-9 pick directly, which is the whole point of a launcher: two
 * keystrokes from load to inside the workspace.
 */
export function digitTarget<T>(key: string, items: T[]): T | null {
  if (!/^[1-9]$/.test(key)) return null;
  return items[Number(key) - 1] ?? null;
}

export type LaunchPhase = 'idle' | 'launching';

export interface LaunchState {
  phase: LaunchPhase;
  orgId: string | null;
}

export const IDLE: LaunchState = { phase: 'idle', orgId: null };

/**
 * Begin a launch, unless one is already in flight.
 *
 * The guard matters: the card is clickable AND Enter-activatable AND digit-
 * selectable, so without it a fast double-input starts two overlapping
 * animations and fires two navigations.
 */
export function beginLaunch(state: LaunchState, orgId: string): LaunchState {
  if (state.phase !== 'idle') return state;
  return { phase: 'launching', orgId };
}

/** True when this specific workspace is the one being launched. */
export function isLaunching(state: LaunchState, orgId: string): boolean {
  return state.phase === 'launching' && state.orgId === orgId;
}

/** True when any launch is in flight, so inputs should be ignored. */
export function isBusy(state: LaunchState): boolean {
  return state.phase !== 'idle';
}

/** Monogram for a workspace: up to two initials from the name. */
export function monogram(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0]!.slice(0, 2).toUpperCase();
  return (words[0]![0]! + words[words.length - 1]![0]!).toUpperCase();
}

/**
 * A stable hue per workspace, so each one is recognisable by colour without
 * anybody having to configure anything — and so it does not change between
 * sessions, which a random or index-based hue would.
 */
export function hueFor(id: string): number {
  let h = 0;
  for (let i = 0; i < id.length; i += 1) h = (h * 31 + id.charCodeAt(i)) % 360;
  return h;
}

/** Human name for a role, for the badge on a workspace tile. */
export function roleLabel(org: Pick<Organization, 'isOwner' | 'role'>): string {
  if (org.isOwner) return 'Owner';
  switch (org.role) {
    case 0: return 'Owner';
    case 2: return 'Manager';
    case 3: return 'Viewer';
    default: return 'Member';
  }
}

/**
 * Where to go after a workspace is chosen.
 *
 * `RequireOrgLayout` redirects here with `state.from` set to the page the user was
 * actually trying to reach, and the old page threw that away and always went to
 * the dashboard — so someone deep-linked to a lead had to navigate back by hand.
 *
 * The value is validated rather than trusted. It arrives via router state, which
 * is not attacker-controlled today, but a path is one `window.history.pushState`
 * away from being whatever a page wants, and `navigate()` on a protocol-relative
 * "//evil.example" would leave the app. Only a single-slash absolute in-app path
 * is accepted; anything else falls back.
 */
export function returnPath(raw: unknown, fallback = '/dashboard'): string {
  if (typeof raw !== 'string') return fallback;
  const p = raw.trim();
  if (!p.startsWith('/') || p.startsWith('//')) return fallback;
  // Landing back on the launcher would loop straight back to itself.
  if (p === '/organizations') return fallback;
  return p;
}
