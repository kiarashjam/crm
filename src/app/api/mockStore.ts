/**
 * Tiny localStorage-backed store used by the mock API path (demo mode) so that
 * create/update/delete actually persist within a browser session. Without it,
 * every mutation silently returns null and the UI looks broken.
 *
 * The shape is intentionally minimal: each entity owns one key holding a JSON
 * array, seeded from a static `mockData` fallback the first time it's read.
 */

interface MockStoreOptions<T> {
  /** localStorage key (namespaced so we don't collide with real app data). */
  storageKey: string;
  /** Initial data used the first time the store is accessed in this browser. */
  seed: T[];
  /** Extract the row's stable identifier. */
  idOf: (row: T) => string;
}

export interface MockStore<T> {
  list: () => T[];
  byId: (id: string) => T | undefined;
  add: (row: T) => T;
  update: (id: string, patch: Partial<T>) => T | null;
  remove: (id: string) => boolean;
  /** Reset to the seed data (used by "Reset demo data" UX). */
  reset: () => void;
}

function readArray<T>(key: string): T[] | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as T[]) : null;
  } catch {
    return null;
  }
}

function writeArray<T>(key: string, list: T[]): void {
  try {
    localStorage.setItem(key, JSON.stringify(list));
  } catch {
    // Storage may be unavailable (Safari private mode, quota exceeded) — fail silently;
    // the change just won't survive a reload.
  }
}

export function createMockStore<T>({ storageKey, seed, idOf }: MockStoreOptions<T>): MockStore<T> {
  const readOrSeed = (): T[] => {
    const stored = readArray<T>(storageKey);
    if (stored) return stored;
    writeArray(storageKey, seed);
    return [...seed];
  };

  return {
    list: () => readOrSeed(),
    byId: (id: string) => readOrSeed().find((r) => idOf(r) === id),
    add: (row: T) => {
      const list = readOrSeed();
      list.unshift(row);
      writeArray(storageKey, list);
      return row;
    },
    update: (id: string, patch: Partial<T>) => {
      const list = readOrSeed();
      const idx = list.findIndex((r) => idOf(r) === id);
      if (idx === -1) return null;
      const next = { ...list[idx], ...patch } as T;
      list[idx] = next;
      writeArray(storageKey, list);
      return next;
    },
    remove: (id: string) => {
      const list = readOrSeed();
      const next = list.filter((r) => idOf(r) !== id);
      if (next.length === list.length) return false;
      writeArray(storageKey, next);
      return true;
    },
    reset: () => writeArray(storageKey, [...seed]),
  };
}

/** Quick UUID-ish id for newly-created mock rows. */
export function mockId(prefix = 'demo'): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}
