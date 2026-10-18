// Where the browser-side store keeps its rows, and for whom.
//
// Against the real API this store holds the features the backend doesn't serve
// yet. It used one fixed key per feature, so every organization opened in the
// same browser saw the same entries, and a fresh browser filled itself with demo
// rows that read as real people. Demo data must keep working exactly as before;
// real users must get their own empty store per organization, without losing
// anything they entered before the store was scoped.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createMockStore, LEGACY_BACKUP_SUFFIX, scopedStorageKey } from './mockStore';

type Row = { id: string; name: string };
const KEY = 'crm.mock.widgets.v1';
const seed: Row[] = [{ id: 'w-1', name: 'Sample widget' }, { id: 'w-2', name: 'Another sample' }];
const store = createMockStore<Row>({ storageKey: KEY, seed, idOf: (r) => r.id });

function signIn(userId: string, orgId: string | null) {
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: userId, name: userId, email: `${userId}@example.com` }));
  if (orgId) localStorage.setItem('aci_org_id', orgId);
  else localStorage.removeItem('aci_org_id');
}

beforeEach(() => localStorage.clear());
afterEach(() => vi.unstubAllEnvs());

describe('with demo data', () => {
  beforeEach(() => vi.stubEnv('VITE_API_URL', ''));

  it('keeps the bare key and seeds sample rows', () => {
    expect(scopedStorageKey(KEY)).toBe(KEY);
    expect(store.list().map((r) => r.id)).toEqual(['w-1', 'w-2']);
    expect(localStorage.getItem(KEY)).not.toBeNull();
  });

  it('resets to the seed', () => {
    store.add({ id: 'mine', name: 'Mine' });
    store.reset();
    expect(store.list().map((r) => r.id)).toEqual(['w-1', 'w-2']);
  });
});

describe('against the real API', () => {
  beforeEach(() => vi.stubEnv('VITE_API_URL', 'http://api.test'));

  it('starts empty instead of showing demo rows', () => {
    signIn('pat', 'org-a');
    expect(store.list()).toEqual([]);
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('keeps each organization separate in the same browser', () => {
    signIn('pat', 'org-a');
    store.add({ id: 'a-1', name: 'Entered in A' });
    signIn('pat', 'org-b');
    expect(store.list()).toEqual([]);
    store.add({ id: 'b-1', name: 'Entered in B' });
    signIn('pat', 'org-a');
    expect(store.list().map((r) => r.id)).toEqual(['a-1']);
  });

  it('keeps each user separate in the same organization', () => {
    signIn('pat', 'org-a');
    store.add({ id: 'p-1', name: "Pat's" });
    signIn('sam', 'org-a');
    expect(store.list()).toEqual([]);
  });

  it('keeps a personal workspace apart from organizations', () => {
    signIn('pat', null);
    store.add({ id: 'personal-1', name: 'Personal' });
    signIn('pat', 'org-a');
    expect(store.list()).toEqual([]);
  });

  it('updates and removes within the current scope only', () => {
    signIn('pat', 'org-a');
    store.add({ id: 'x', name: 'A version' });
    signIn('pat', 'org-b');
    store.add({ id: 'x', name: 'B version' });
    expect(store.update('x', { name: 'B edited' })?.name).toBe('B edited');
    expect(store.remove('x')).toBe(true);
    signIn('pat', 'org-a');
    expect(store.byId('x')?.name).toBe('A version');
  });

  it('resets to empty, never to the seed', () => {
    signIn('pat', 'org-a');
    store.add({ id: 'mine', name: 'Mine' });
    store.reset();
    expect(store.list()).toEqual([]);
  });

  describe('data saved before scoping existed', () => {
    const legacy: Row[] = [seed[0]!, { id: 'real-1', name: 'Amelia, entered by a person' }, seed[1]!];

    beforeEach(() => localStorage.setItem(KEY, JSON.stringify(legacy)));

    it('moves into the first scope that reads it, without the demo rows', () => {
      signIn('pat', 'org-a');
      expect(store.list().map((r) => r.id)).toEqual(['real-1']);
    });

    it('moves only once, so a second organization does not inherit it', () => {
      signIn('pat', 'org-a');
      store.list();
      signIn('pat', 'org-b');
      expect(store.list()).toEqual([]);
    });

    it('keeps the original under a backup key and clears the shared one', () => {
      signIn('pat', 'org-a');
      store.list();
      expect(localStorage.getItem(KEY)).toBeNull();
      expect(JSON.parse(localStorage.getItem(KEY + LEGACY_BACKUP_SUFFIX) ?? '[]')).toEqual(legacy);
    });
  });
});
