// The duplicates on a list page: every record of its kind, loaded on its own so a
// paged list still sees them all, grouped, less any the person said are not the same.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { onDataChanged } from '@/app/api/dataChanged';
import type { DuplicateGroup } from '@/app/api/dedupe';
import type { DuplicateKind } from './kinds';

/** Wait for the page itself to settle before reading the whole list. */
const FIRST_LOOK_MS = 700;
/** A burst of saves is one reason to look again, not many. */
const SETTLE_MS = 900;

const storageKey = (kind: string) => `cadence.duplicates.notSame.${kind}`;

function readNotSame(kind: string): Set<string> {
  try {
    const raw = localStorage.getItem(storageKey(kind));
    const list = raw ? (JSON.parse(raw) as unknown) : [];
    return new Set(Array.isArray(list) ? list.filter((k): k is string => typeof k === 'string') : []);
  } catch {
    return new Set();
  }
}

function writeNotSame(kind: string, keys: Set<string>): void {
  try {
    localStorage.setItem(storageKey(kind), JSON.stringify([...keys].slice(-500)));
  } catch {
    // Without storage the choice lasts until the page is left, which is still useful.
  }
}

export interface DuplicateGroups<T> {
  /** Groups still to be dealt with, most records first. */
  groups: DuplicateGroup<T>[];
  /** Whether the list has been read at least once. */
  loaded: boolean;
  /** Groups the person said are not the same, and so are not shown. */
  notSameCount: number;
  /** Offer the groups marked "not the same" again. */
  clearNotSame: () => void;
  /** How many records would go: every group less the one kept from it. */
  extra: number;
  /** Look again now, after a merge. */
  refresh: () => Promise<void>;
  /** "These are not the same": never offered again. */
  markNotSame: (keys: string[]) => void;
}

export function useDuplicateGroups<T extends { id: string }>(
  spec: DuplicateKind<T>,
  { enabled = true, delayMs = FIRST_LOOK_MS }: { enabled?: boolean; delayMs?: number } = {},
): DuplicateGroups<T> {
  const [records, setRecords] = useState<T[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [notSame, setNotSame] = useState<Set<string>>(() => readNotSame(spec.kind));
  const alive = useRef(true);
  const loading = useRef<Promise<void> | null>(null);

  const refresh = useCallback(async () => {
    if (loading.current) return loading.current;
    const run = spec
      .load()
      .then((list) => { if (alive.current) setRecords(Array.isArray(list) ? list : []); })
      // Finding duplicates is a help, never a reason for the page to fail.
      .catch(() => undefined)
      .finally(() => {
        loading.current = null;
        if (alive.current) setLoaded(true);
      });
    loading.current = run;
    return run;
  }, [spec]);

  useEffect(() => {
    alive.current = true;
    if (!enabled) return () => { alive.current = false; };
    const first = window.setTimeout(() => { void refresh(); }, delayMs);
    let settle: number | undefined;
    const stop = onDataChanged(() => {
      window.clearTimeout(settle);
      settle = window.setTimeout(() => { void refresh(); }, SETTLE_MS);
    });
    return () => {
      alive.current = false;
      window.clearTimeout(first);
      window.clearTimeout(settle);
      stop();
    };
  }, [enabled, delayMs, refresh]);

  const all = useMemo(() => spec.group(records), [spec, records]);
  const groups = useMemo(() => all.filter((g) => !notSame.has(g.key)), [all, notSame]);
  const notSameCount = all.length - groups.length;

  const clearNotSame = useCallback(() => {
    const next = new Set(notSame);
    all.forEach((g) => next.delete(g.key));
    writeNotSame(spec.kind, next);
    setNotSame(next);
  }, [all, notSame, spec.kind]);

  const markNotSame = useCallback((keys: string[]) => {
    setNotSame((current) => {
      const next = new Set(current);
      keys.forEach((k) => next.add(k));
      writeNotSame(spec.kind, next);
      return next;
    });
  }, [spec.kind]);

  const extra = groups.reduce((n, g) => n + g.records.length - 1, 0);
  return { groups, loaded, notSameCount, clearNotSame, extra, refresh, markNotSame };
}
