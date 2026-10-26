// How each person likes each list page shown: as cards, a list, a table or a board;
// roomy or compact; which columns. Chosen once on the page, then the page opens that
// way every time, in every browser they sign in from.
//
// A copy lives in this browser, keyed by the person, so the page is drawn the right
// way from the first frame. The server keeps the same choices with their settings,
// so another browser picks them up; whichever copy of a page's choice is newer wins.

import { useCallback, useMemo, useSyncExternalStore } from 'react';
import { getUserSettings, saveUserSettings } from '@/app/api/settings';
import { getCurrentUser } from '@/app/lib/auth';

export type ViewLayout = 'grid' | 'list' | 'table' | 'board';
export type ViewDensity = 'comfortable' | 'compact';

export const VIEW_LAYOUTS: ViewLayout[] = ['grid', 'list', 'table', 'board'];

export interface PageView {
  layout: ViewLayout;
  density: ViewDensity;
  /** Columns turned on or off by the person; any not named keep their default. */
  columns?: Record<string, boolean>;
  /** When it was chosen, to tell which of two copies is newer. */
  at?: number;
}

interface Saved {
  v: 1;
  pages: Record<string, PageView>;
}

/** What fits in the server's column, with room to spare. */
const MAX_SAVED = 3800;
const SAVE_AFTER_MS = 800;

const keyFor = (userId: string) => `cadence.views.v1:${userId}`;

function clean(raw: unknown): Record<string, PageView> {
  const pages: Record<string, PageView> = {};
  const source = (raw as Partial<Saved> | null)?.pages;
  if (!source || typeof source !== 'object') return pages;
  for (const [page, value] of Object.entries(source)) {
    const v = value as Partial<PageView> | null;
    if (!v || !VIEW_LAYOUTS.includes(v.layout as ViewLayout)) continue;
    const columns: Record<string, boolean> = {};
    if (v.columns && typeof v.columns === 'object') {
      for (const [id, on] of Object.entries(v.columns)) if (typeof on === 'boolean') columns[id] = on;
    }
    pages[page] = {
      layout: v.layout as ViewLayout,
      density: v.density === 'compact' ? 'compact' : 'comfortable',
      ...(Object.keys(columns).length ? { columns } : {}),
      ...(typeof v.at === 'number' ? { at: v.at } : {}),
    };
  }
  return pages;
}

function parse(json: string | null | undefined): Record<string, PageView> {
  if (!json) return {};
  try {
    return clean(JSON.parse(json));
  } catch {
    return {};
  }
}

/** The saved form, dropping the pages chosen longest ago if it would not fit. */
export function serializeViews(pages: Record<string, PageView>): string {
  const entries = Object.entries(pages).sort(([, a], [, b]) => (b.at ?? 0) - (a.at ?? 0));
  while (entries.length) {
    const json = JSON.stringify({ v: 1, pages: Object.fromEntries(entries) } satisfies Saved);
    if (json.length <= MAX_SAVED) return json;
    entries.pop();
  }
  return JSON.stringify({ v: 1, pages: {} } satisfies Saved);
}

/** Each page's newer choice of the two. */
export function mergeViews(local: Record<string, PageView>, remote: Record<string, PageView>): Record<string, PageView> {
  const merged = { ...local };
  for (const [page, view] of Object.entries(remote)) {
    const mine = merged[page];
    if (!mine || (view.at ?? 0) > (mine.at ?? 0)) merged[page] = view;
  }
  return merged;
}

// ── The store ───────────────────────────────────────────────────────────────

let user = '';
let pages: Record<string, PageView> = {};
const listeners = new Set<() => void>();
const fetched = new Set<string>();
let saveTimer: ReturnType<typeof setTimeout> | undefined;

const notify = () => listeners.forEach((l) => l());

function writeLocal() {
  try {
    localStorage.setItem(keyFor(user), JSON.stringify({ v: 1, pages } satisfies Saved));
  } catch {
    // Without storage the choice lasts until the page is reloaded; the server still has it.
  }
}

/** This person's choices as this browser has them. Cheap, so it can run while drawing. */
function ensureUser(): void {
  const id = getCurrentUser()?.id ?? 'anonymous';
  if (id === user) return;
  user = id;
  try {
    pages = parse(localStorage.getItem(keyFor(id)));
  } catch {
    pages = {};
  }
}

/** Once per person, fold in the choices the server has, made in another browser. */
function syncRemote(): void {
  ensureUser();
  const id = user;
  if (id === 'anonymous' || fetched.has(id)) return;
  fetched.add(id);
  void getUserSettings()
    .then((settings) => {
      if (user !== id) return;
      const remote = parse(settings.viewPreferencesJson);
      const merged = mergeViews(pages, remote);
      if (JSON.stringify(merged) === JSON.stringify(pages)) return;
      pages = merged;
      writeLocal();
      notify();
    })
    // Offline or signed out: this browser's copy is what there is.
    .catch(() => undefined);
}

function saveSoon() {
  if (user === 'anonymous') return;
  clearTimeout(saveTimer);
  const id = user;
  saveTimer = setTimeout(() => {
    if (user !== id) return;
    void saveUserSettings({ viewPreferencesJson: serializeViews(pages) }).catch(() => undefined);
  }, SAVE_AFTER_MS);
}

function setPage(page: string, view: PageView | undefined) {
  ensureUser();
  const next = { ...pages };
  if (view) next[page] = { ...view, at: Date.now() };
  else delete next[page];
  pages = next;
  writeLocal();
  notify();
  saveSoon();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  syncRemote();
  return () => listeners.delete(listener);
}

function snapshot() {
  ensureUser();
  return pages;
}

/** For tests: forget everything loaded. */
export function resetViewPreferencesForTests() {
  user = '';
  pages = {};
  fetched.clear();
  clearTimeout(saveTimer);
}

// ── The hook ────────────────────────────────────────────────────────────────

export interface ViewDefaults {
  /** The layouts this page can show, in the order the switcher offers them. */
  layouts: ViewLayout[];
  layout?: ViewLayout;
  density?: ViewDensity;
}

export interface ViewPreference {
  layout: ViewLayout;
  density: ViewDensity;
  layouts: ViewLayout[];
  /** Whether a column is shown, given whether it is by default. */
  shows: (columnId: string, byDefault?: boolean) => boolean;
  setLayout: (layout: ViewLayout) => void;
  setDensity: (density: ViewDensity) => void;
  setColumn: (columnId: string, shown: boolean) => void;
  /** Back to how the page comes. */
  reset: () => void;
  /** Whether anything differs from how the page comes. */
  customised: boolean;
}

export function useViewPreference(page: string, defaults: ViewDefaults): ViewPreference {
  const all = useSyncExternalStore(subscribe, snapshot, snapshot);
  const saved = all[page];
  const fallback = defaults.layout && defaults.layouts.includes(defaults.layout) ? defaults.layout : defaults.layouts[0] ?? 'grid';
  // A layout this page no longer offers falls back rather than showing nothing.
  const layout = saved && defaults.layouts.includes(saved.layout) ? saved.layout : fallback;
  const density = saved?.density ?? defaults.density ?? 'comfortable';
  const layoutsKey = defaults.layouts.join(',');

  const current = useCallback((): PageView => ({
    layout,
    density,
    ...(saved?.columns ? { columns: saved.columns } : {}),
  }), [layout, density, saved?.columns]);

  const setLayout = useCallback((next: ViewLayout) => setPage(page, { ...current(), layout: next }), [page, current]);
  const setDensity = useCallback((next: ViewDensity) => setPage(page, { ...current(), density: next }), [page, current]);
  const setColumn = useCallback((columnId: string, shown: boolean) => {
    const view = current();
    setPage(page, { ...view, columns: { ...(view.columns ?? {}), [columnId]: shown } });
  }, [page, current]);
  const reset = useCallback(() => setPage(page, undefined), [page]);
  const shows = useCallback((columnId: string, byDefault = true) => saved?.columns?.[columnId] ?? byDefault, [saved?.columns]);

  return useMemo(() => ({
    layout,
    density,
    layouts: layoutsKey.split(',') as ViewLayout[],
    shows,
    setLayout,
    setDensity,
    setColumn,
    reset,
    customised: !!saved,
  }), [layout, density, layoutsKey, shows, setLayout, setDensity, setColumn, reset, saved]);
}
