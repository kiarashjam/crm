// Saved views: a list's filters, order and columns under a name. A view is its maker's own,
// or shared with the whole workspace; each person can open a list with one view of their
// choice. Anyone can keep views for themselves; sharing one takes a role that changes
// records. Demo mode keeps them in this browser.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';

/** The lists a view can be of. */
export type SavedViewList = 'leads' | 'contacts' | 'companies' | 'deals' | 'members';

/** A list's filters and order, as its address's parameters: {"status": "archived", "tag": ["…"]}. */
export type SavedViewQuery = Record<string, string | string[]>;

export interface SavedView {
  id: string;
  list: SavedViewList;
  name: string;
  shared: boolean;
  /** Whether the person asking made it, and so may change it. */
  mine: boolean;
  /** Whether the person asking opens the list with it. */
  isDefault: boolean;
  query: SavedViewQuery;
  /** The columns shown, in order; null keeps the person's own. */
  columns: string[] | null;
  /** Who made it, for a shared view of someone else's. */
  ownerName?: string | null;
  version: string;
  updatedAtUtc: string;
}

export interface SavedViewInput {
  list: SavedViewList;
  name: string;
  shared?: boolean;
  query: SavedViewQuery;
  columns?: string[] | null;
}

const viewStore = createMockStore<SavedView>({ storageKey: 'crm.mock.savedViews.v1', seed: [], idOf: (v) => v.id });

/** The parameters of a page's address a view keeps: its filters and order, not its page. */
const NOT_KEPT = new Set(['page', 'pageSize', 'view']);

/** A view's query from a page's address. */
export function queryFromParams(params: URLSearchParams): SavedViewQuery {
  const query: SavedViewQuery = {};
  for (const key of new Set(params.keys())) {
    if (NOT_KEPT.has(key)) continue;
    const values = params.getAll(key).filter((v) => v !== '');
    const [only] = values;
    if (only === undefined) continue;
    query[key] = values.length === 1 && key !== 'tag' && key !== 'notTag' && key !== 'field' ? only : values;
  }
  return query;
}

/** A page's address for a view: its filters and order, on the first page, with the page size kept. */
export function paramsFromQuery(query: SavedViewQuery, current?: URLSearchParams): URLSearchParams {
  const params = new URLSearchParams();
  const pageSize = current?.get('pageSize');
  if (pageSize) params.set('pageSize', pageSize);
  for (const [key, value] of Object.entries(query ?? {})) {
    if (NOT_KEPT.has(key)) continue;
    for (const v of Array.isArray(value) ? value : [value]) if (typeof v === 'string' && v !== '') params.append(key, v);
  }
  return params;
}

/** The views of a list the person can open: their own, and those shared with the workspace. */
export async function getSavedViews(list: SavedViewList, req?: RequestOptions): Promise<SavedView[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<SavedView[]>(`/api/saved-views?list=${list}`, req);
      return Array.isArray(res) ? res : [];
    },
    async () => viewStore.list().filter((v) => v.list === list).sort((a, b) => a.name.localeCompare(b.name)),
  );
}

export async function createSavedView(input: SavedViewInput): Promise<SavedView> {
  return apiWithFallback(
    () => authFetchJson<SavedView>('/api/saved-views', { method: 'POST', body: JSON.stringify(input) }),
    async () => viewStore.add({
      id: mockId('view'), list: input.list, name: input.name.trim(), shared: !!input.shared, mine: true, isDefault: false,
      query: input.query, columns: input.columns ?? null, ownerName: null, version: mockId('v'), updatedAtUtc: new Date().toISOString(),
    }),
  );
}

/** Change a view of one's own: only what is sent changes. `version` is the one it was read at; an older one is refused. */
export async function updateSavedView(
  id: string,
  patch: { name?: string; shared?: boolean; query?: SavedViewQuery; columns?: string[] | null; version: string },
): Promise<SavedView | null> {
  return apiWithFallback(
    () => authFetchJson<SavedView>(`/api/saved-views/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      const { version: _version, ...rest } = patch;
      return viewStore.update(id, { ...rest, version: mockId('v'), updatedAtUtc: new Date().toISOString() });
    },
  );
}

export async function deleteSavedView(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/saved-views/${id}`, { method: 'DELETE' }); await ensureOk(res); return true; },
    async () => viewStore.remove(id),
  );
}

/** Open a list with this view from now on; null to open it as it comes. */
export async function setDefaultView(list: SavedViewList, viewId: string | null): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch('/api/saved-views/default', { method: 'PUT', body: JSON.stringify({ list, viewId }) });
      await ensureOk(res);
      return true;
    },
    async () => {
      for (const view of viewStore.list().filter((v) => v.list === list)) viewStore.update(view.id, { isDefault: view.id === viewId });
      return true;
    },
  );
}
