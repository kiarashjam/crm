import { getCurrentOrganizationId, getCurrentUser } from '@/app/lib/auth';
import { servesDemoData } from './apiClient';
import { announceDataChanged } from './dataChanged';

/**
 * Tiny localStorage-backed store used by the mock API path (demo mode) so that
 * create/update/delete actually persist within a browser session. Without it,
 * every mutation silently returns null and the UI looks broken.
 *
 * The shape is intentionally minimal: each entity owns one key holding a JSON
 * array, seeded from a static `mockData` fallback the first time it's read.
 *
 * The same store also backs features the real backend doesn't serve yet (see
 * `apiWithFallback`). Against the real API it therefore behaves differently:
 * its key is scoped to the signed-in user and current organization, so one
 * organization's entries never appear in another's, and it starts empty rather
 * than showing demo rows that would read as real data.
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
  /** Reset to the seed data (used by "Reset demo data" UX); empty against the real API. */
  reset: () => void;
  /** Point rows whose `field` is `fromId` at `toId` instead. Returns how many changed. */
  relink: (field: string, fromId: string, toId: string) => number;
}

/** Every store made so far, so a merge can repoint links in all of them at once. */
const stores: MockStore<unknown>[] = [];

/**
 * After two records became one, whatever pointed at the one that is gone — a
 * task's `leadId`, a charge's `memberId` — points at the one kept. Only stores
 * whose module has been loaded are reached, so a caller that needs particular
 * ones imports them first.
 */
export function relinkMockRecords(field: string, fromId: string, toId: string): number {
  return stores.reduce((n, store) => n + store.relink(field, fromId, toId), 0);
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

/**
 * The key a store reads and writes. Demo data uses the bare key; a signed-in
 * user against the real API gets one scoped to them and their organization.
 */
export function scopedStorageKey(storageKey: string): string {
  if (servesDemoData()) return storageKey;
  const user = getCurrentUser()?.id ?? 'anonymous';
  const org = getCurrentOrganizationId() ?? 'personal';
  return `${storageKey}:user:${user}:org:${org}`;
}

/** Where a migrated bare key's original contents are kept. */
export const LEGACY_BACKUP_SUFFIX = ':legacy-backup';

/**
 * Before scoping, real-API data was written under the bare key and shared by
 * every organization in the browser. Move it once into the first scope that
 * reads it, so nothing anyone entered disappears, leaving out the demo seed
 * rows it was pre-filled with. The original stays under a backup key; the bare
 * key is removed only once both copies are safely written.
 */
function migrateLegacy<T>(storageKey: string, scopedKey: string, seedIds: Set<string>, idOf: (row: T) => string): T[] | null {
  const legacy = readArray<T>(storageKey);
  if (!legacy) return null;
  const kept = legacy.filter((row) => !seedIds.has(idOf(row)));
  writeArray(scopedKey, kept);
  if (readArray<T>(scopedKey) === null) return kept;
  try {
    localStorage.setItem(storageKey + LEGACY_BACKUP_SUFFIX, JSON.stringify(legacy));
    localStorage.removeItem(storageKey);
  } catch {
    // Keep the original in place rather than risk losing it.
  }
  return kept;
}

export function createMockStore<T>({ storageKey, seed, idOf }: MockStoreOptions<T>): MockStore<T> {
  const store = buildMockStore({ storageKey, seed, idOf });
  stores.push(store as MockStore<unknown>);
  return store;
}

function buildMockStore<T>({ storageKey, seed, idOf }: MockStoreOptions<T>): MockStore<T> {
  const seedIds = new Set(seed.map(idOf));

  const read = (key: string): T[] => {
    const stored = readArray<T>(key);
    if (stored) return stored;
    if (key !== storageKey) {
      // Real API: never seed. Recover anything entered before scoping existed.
      return migrateLegacy(storageKey, key, seedIds, idOf) ?? [];
    }
    writeArray(storageKey, seed);
    return [...seed];
  };

  return {
    list: () => read(scopedStorageKey(storageKey)),
    byId: (id: string) => read(scopedStorageKey(storageKey)).find((r) => idOf(r) === id),
    add: (row: T) => {
      const key = scopedStorageKey(storageKey);
      const list = read(key);
      list.unshift(row);
      writeArray(key, list);
      announceDataChanged();
      return row;
    },
    update: (id: string, patch: Partial<T>) => {
      const key = scopedStorageKey(storageKey);
      const list = read(key);
      const idx = list.findIndex((r) => idOf(r) === id);
      if (idx === -1) return null;
      const next = { ...list[idx], ...patch } as T;
      list[idx] = next;
      writeArray(key, list);
      announceDataChanged();
      return next;
    },
    remove: (id: string) => {
      const key = scopedStorageKey(storageKey);
      const list = read(key);
      const next = list.filter((r) => idOf(r) !== id);
      if (next.length === list.length) return false;
      writeArray(key, next);
      announceDataChanged();
      return true;
    },
    reset: () => {
      const key = scopedStorageKey(storageKey);
      writeArray(key, key === storageKey ? [...seed] : []);
      announceDataChanged();
    },
    relink: (field: string, fromId: string, toId: string) => {
      const key = scopedStorageKey(storageKey);
      // Read only what is already there: relinking must not seed a store nobody opened.
      const list = readArray<T>(key);
      if (!list) return 0;
      let changed = 0;
      const next = list.map((row) => {
        if ((row as Record<string, unknown>)[field] !== fromId) return row;
        changed++;
        return { ...row, [field]: toId } as T;
      });
      if (changed) {
        writeArray(key, next);
        announceDataChanged();
      }
      return changed;
    },
  };
}

/** Quick UUID-ish id for newly-created mock rows. */
export function mockId(prefix = 'demo'): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}
