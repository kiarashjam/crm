// The duplicates on a list page: every record of its kind, grouped, less any the team said
// are not the same.
//
// Against the server, contacts and companies are found by the server itself (spec.scan),
// over every record; the other kinds are loaded whole and grouped here. Either way "not
// the same" is a decision kept on the server for the whole workspace, so a colleague, or
// the same person on another device, is not asked about that pair again. In demo mode the
// decisions stay in this browser, as everything else does.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { toast } from 'sonner';
import { servesDemoData } from '@/app/api/apiClient';
import { onDataChanged } from '@/app/api/dataChanged';
import {
  clearNotSame as clearNotSameOnServer, getNotSamePairs, markNotSame as markNotSameOnServer, pairsOf,
  type DuplicateGroup, type NotSameGroup,
} from '@/app/api/dedupe';
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

const idsOf = (key: string) => key.split('|').filter(Boolean);

export interface DuplicateGroups<T> {
  /** Groups still to be dealt with, most records first. */
  groups: DuplicateGroup<T>[];
  /** Whether the list has been read at least once. */
  loaded: boolean;
  /** Groups the team said are not the same, and so are not shown. */
  notSameCount: number;
  /** Offer the groups marked "not the same" again. */
  clearNotSame: () => void;
  /** How many records would go: every group less the one kept from it. */
  extra: number;
  /** Look again now, after a merge. */
  refresh: () => Promise<void>;
  /** "These are not the same": never offered again, to anyone in the workspace. */
  markNotSame: (keys: string[]) => void;
}

export function useDuplicateGroups<T extends { id: string }>(
  spec: DuplicateKind<T>,
  { enabled = true, delayMs = FIRST_LOOK_MS }: { enabled?: boolean; delayMs?: number } = {},
): DuplicateGroups<T> {
  const onServer = !servesDemoData();
  const [records, setRecords] = useState<T[]>([]);
  // What the server found, for the kinds it finds itself.
  const [scanned, setScanned] = useState<{ groups: DuplicateGroup<T>[]; notSame: NotSameGroup[] } | null>(null);
  // The team's decisions, for the kinds grouped here.
  const [decided, setDecided] = useState<Set<string> | null>(null);
  const [notSameHere, setNotSameHere] = useState<Set<string>>(() => readNotSame(spec.kind));
  // Hidden as soon as someone decides, before the server's answer comes back.
  const [justDecided, setJustDecided] = useState<Set<string>>(new Set());
  const [loaded, setLoaded] = useState(false);
  const alive = useRef(true);
  const loading = useRef<Promise<void> | null>(null);
  // Count the decisions made, and those whose request has settled. Only a read that began
  // once every decision so far was saved, with none made since, ends their hiding: an
  // earlier read, or one that began while a decision was still on its way, may still hold
  // the group, and would bring it back.
  const decisionsMade = useRef(0);
  const decisionsSettled = useRef(0);

  const read = useCallback(async () => {
    if (onServer && spec.scan) {
      const scan = await spec.scan();
      if (alive.current) setScanned(scan);
      return;
    }
    const [list, pairs] = await Promise.all([
      spec.load(),
      // Finding duplicates is a help: without the decisions, nothing is hidden.
      onServer ? getNotSamePairs(spec.kind).catch(() => null) : Promise.resolve(null),
    ]);
    if (!alive.current) return;
    setRecords(Array.isArray(list) ? list : []);
    setDecided(pairs ? new Set(pairs) : null);
  }, [spec, onServer]);

  const refresh = useCallback(async () => {
    if (loading.current) return loading.current;
    const made = decisionsMade.current;
    const allSaved = decisionsSettled.current === made;
    const run = read()
      .then(() => { if (alive.current && allSaved && made === decisionsMade.current) setJustDecided(new Set()); })
      // Finding duplicates is a help, never a reason for the page to fail.
      .catch(() => undefined)
      .finally(() => {
        loading.current = null;
        if (alive.current) setLoaded(true);
      });
    loading.current = run;
    return run;
  }, [read]);

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

  const { groups, hidden } = useMemo(() => {
    if (scanned) {
      return { groups: scanned.groups.filter((g) => !justDecided.has(g.key)), hidden: scanned.notSame };
    }
    const all = spec.group(records);
    // A group is hidden when every pair in it was said to be not the same; a new record
    // that looks like one of them brings the group back.
    const isHidden = decided
      ? (g: DuplicateGroup<T>) => justDecided.has(g.key) || pairsOf(g.records.map((r) => r.id)).every((p) => decided.has(p))
      : (g: DuplicateGroup<T>) => notSameHere.has(g.key);
    return {
      groups: all.filter((g) => !isHidden(g)),
      hidden: all.filter(isHidden).map((g) => ({ key: g.key, recordIds: g.records.map((r) => r.id) })),
    };
  }, [scanned, spec, records, decided, notSameHere, justDecided]);
  const notSameCount = hidden.length;

  // Once a decision is saved, or refused, read again: after any read already running,
  // which began before it was saved and so may still hold the group.
  const rereadAfterDecision = useCallback(() => {
    decisionsSettled.current++;
    void (loading.current ?? Promise.resolve()).then(() => refresh());
  }, [refresh]);

  const clearNotSame = useCallback(() => {
    if (!onServer) {
      const next = new Set(notSameHere);
      hidden.forEach((g) => next.delete(g.key));
      writeNotSame(spec.kind, next);
      setNotSameHere(next);
      return;
    }
    decisionsMade.current++;
    void Promise.all(hidden.map((g) => clearNotSameOnServer(spec.kind, g.recordIds)))
      .catch((err: unknown) => { toast.error(err instanceof Error && err.message ? err.message : 'They could not be offered again.'); })
      .finally(rereadAfterDecision);
  }, [onServer, notSameHere, hidden, spec.kind, rereadAfterDecision]);

  const markNotSame = useCallback((keys: string[]) => {
    if (!onServer) {
      setNotSameHere((current) => {
        const next = new Set(current);
        keys.forEach((k) => next.add(k));
        writeNotSame(spec.kind, next);
        return next;
      });
      return;
    }
    decisionsMade.current++;
    setJustDecided((current) => new Set([...current, ...keys]));
    void Promise.all(keys.map((key) => markNotSameOnServer(spec.kind, idsOf(key))))
      .catch((err: unknown) => {
        toast.error(err instanceof Error && err.message ? err.message : 'The decision was not saved.');
        setJustDecided((current) => new Set([...current].filter((k) => !keys.includes(k))));
      })
      .finally(rereadAfterDecision);
  }, [onServer, spec.kind, rereadAfterDecision]);

  const extra = groups.reduce((n, g) => n + g.records.length - 1, 0);
  return { groups, loaded, notSameCount, clearNotSame, extra, refresh, markNotSame };
}
