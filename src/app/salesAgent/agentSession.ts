/** In-memory workspace cache and load timings. Keys are user:organization. No record names are stored in the timings. */

const FRESH_MS = 45_000;
const KEEP_MS = 5 * 60_000;
const REFRESH_EVENT = 'cadence-agent-refresh';

interface Entry {
  at: number;
  value: unknown;
}

const store = new Map<string, Entry>();

export interface AgentLoadSpan {
  cache: 'hit' | 'refresh' | 'miss';
  ms: number;
  ok: boolean;
}

const spans: AgentLoadSpan[] = [];

export function readWork<T>(key: string): { value: T; fresh: boolean } | null {
  const entry = store.get(key);
  if (!entry) return null;
  const age = Date.now() - entry.at;
  if (age > KEEP_MS) {
    store.delete(key);
    return null;
  }
  return { value: entry.value as T, fresh: age <= FRESH_MS };
}

export function writeWork<T>(key: string, value: T): void {
  store.set(key, { at: Date.now(), value });
}

/** Drop one account's cache and ask the open agent to load again. */
export function invalidateWork(key: string): void {
  store.delete(key);
  if (typeof window === 'undefined') return;
  window.dispatchEvent(new CustomEvent(REFRESH_EVENT, { detail: key }));
}

export function onWorkInvalidated(listener: (key: string) => void): () => void {
  if (typeof window === 'undefined') return () => undefined;
  const handler = (event: Event) => {
    const key = (event as CustomEvent<string>).detail;
    if (typeof key === 'string') listener(key);
  };
  window.addEventListener(REFRESH_EVENT, handler);
  return () => window.removeEventListener(REFRESH_EVENT, handler);
}

export function recordLoad(span: AgentLoadSpan): void {
  spans.push(span);
  if (spans.length > 30) spans.shift();
  if (import.meta.env?.DEV) console.debug('cadence-agent', span);
}

export function agentLoadSpans(): AgentLoadSpan[] {
  return spans.slice();
}

export function clearWorkCache(): void {
  store.clear();
  spans.length = 0;
}

/** One retry for a read. A second failure is returned to the caller. Writes must not use this. */
export function retryRead<T>(load: () => Promise<T>): Promise<T> {
  return load().catch(() => load());
}

export function withTimeout<T>(promise: Promise<T>, ms: number): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('timeout')), ms);
    promise.then(
      (value) => {
        clearTimeout(timer);
        resolve(value);
      },
      (error: unknown) => {
        clearTimeout(timer);
        reject(error);
      },
    );
  });
}
