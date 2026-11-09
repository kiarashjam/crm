// The rep's own work for the assistant's queue, from the server (GET /api/work-queue).
//
// Kept outside any component, as the agent's lists are (agentData.ts): the panel sits in
// every page's header, so it remounts on each navigation, and re-reading the queue each
// time would be a request per page. It is read once per workspace, account and scope,
// re-read quietly after any save, when the tab comes back into view, or when older than
// FRESH_FOR_MS, and dropped the moment the workspace changes.

import { useEffect, useSyncExternalStore } from 'react';
import { getWorkQueue, type WorkQueue } from '@/app/api/workQueue';
import { onDataChanged } from '@/app/api/dataChanged';
import type { WorkScope } from '@/app/lib/workScope';
import { FRESH_FOR_MS, SETTLE_MS, workspaceKey } from './agentData';

export interface WorkQueueSnapshot {
  /** Which workspace, account and scope the queue is for. */
  key: string | null;
  queue: WorkQueue | null;
  /** First read in flight: nothing to show yet. */
  loading: boolean;
  /** The last read failed. An earlier queue, if any, is still shown. */
  failed: boolean;
}

const EMPTY: WorkQueueSnapshot = { key: null, queue: null, loading: true, failed: false };

let key: string | null = null;
let scopeOfKey: WorkScope = 'mine';
let queue: WorkQueue | null = null;
let loadedAt = 0;
let inflight: { key: string; promise: Promise<void> } | null = null;
let reloadAfter = false;
let failed = false;
let settleTimer: ReturnType<typeof setTimeout> | null = null;
let installed: (() => void) | null = null;
let snapshot: WorkQueueSnapshot = EMPTY;
const listeners = new Set<() => void>();

function emit(): void {
  snapshot = { key, queue, loading: queue === null && inflight !== null, failed };
  for (const listener of listeners) listener();
}

function load(forKey: string): Promise<void> {
  if (inflight && inflight.key === forKey) {
    reloadAfter = true;
    return inflight.promise;
  }
  const mine = { key: forKey } as { key: string; promise: Promise<void> };
  mine.promise = (async () => {
    try {
      const next = await getWorkQueue(scopeOfKey);
      // The workspace or the scope changed while this was on its way: not this one's.
      if (key !== forKey) return;
      queue = next;
      loadedAt = Date.now();
      failed = false;
    } catch {
      if (key === forKey) failed = true;
    }
  })().finally(() => {
    if (inflight !== mine) return;
    inflight = null;
    const again = reloadAfter && key === forKey;
    reloadAfter = false;
    emit();
    if (again) void load(forKey);
  });
  inflight = mine;
  emit();
  return mine.promise;
}

function install(): void {
  if (installed || typeof window === 'undefined') return;
  const offChange = onDataChanged(() => {
    if (!key) return;
    if (listeners.size === 0) {
      loadedAt = 0;
      return;
    }
    if (settleTimer) clearTimeout(settleTimer);
    const target = key;
    settleTimer = setTimeout(() => {
      settleTimer = null;
      if (key === target) void load(target);
    }, SETTLE_MS);
  });
  const onVisible = () => {
    if (document.visibilityState === 'visible' && key) ensureFresh(key, scopeOfKey);
  };
  document.addEventListener('visibilitychange', onVisible);
  installed = () => {
    offChange();
    document.removeEventListener('visibilitychange', onVisible);
  };
}

function ensureFresh(forKey: string, scope: WorkScope): void {
  install();
  if (forKey !== key) {
    key = forKey;
    scopeOfKey = scope;
    queue = null;
    loadedAt = 0;
    failed = false;
    reloadAfter = false;
    emit();
    void load(forKey);
    return;
  }
  if (inflight) return;
  if (queue === null || Date.now() - loadedAt > FRESH_FOR_MS) void load(forKey);
}

/** Read again now, behind whatever is shown: the panel's retry. */
export function reloadWorkQueue(): void {
  if (key) void load(key);
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

const noSubscription = () => () => {};

/**
 * The rep's open work for this workspace and scope. Off (no request) where the panel
 * does not show a queue: a record page, a hidden panel, a personal workspace.
 */
export function useWorkQueue(orgId: string | null | undefined, scope: WorkScope, enabled: boolean): WorkQueueSnapshot {
  const current = useSyncExternalStore(enabled ? subscribe : noSubscription, () => snapshot, () => snapshot);
  const wanted = `${workspaceKey(orgId)}|${scope}`;
  useEffect(() => {
    if (enabled) ensureFresh(wanted, scope);
  }, [wanted, scope, enabled]);
  return current.key === wanted ? current : EMPTY;
}

/** Test hook: forget everything, including installed listeners. */
export function resetWorkQueueForTests(): void {
  installed?.();
  installed = null;
  if (settleTimer) clearTimeout(settleTimer);
  settleTimer = null;
  key = null;
  queue = null;
  loadedAt = 0;
  inflight = null;
  reloadAfter = false;
  failed = false;
  snapshot = EMPTY;
  listeners.clear();
}
