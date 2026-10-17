// "Something was saved" — one signal for every write in the app.
//
// Views that summarise records drawn from many lists (the sales agent's next
// steps, above all) cannot know which page changed what. Rather than threading a
// refresh callback through sixty pages, every successful write announces itself
// here, from the two places all writes pass through: `authFetch` for the real API
// and `createMockStore` for demo mode. Listeners decide for themselves whether
// and when to reload.
//
// The signal carries no payload on purpose. It means "your copy may be stale",
// not "this record now looks like this" — so a listener can never act on a
// half-understood change, only re-read the truth.

const EVENT = 'cadence:data-changed';

/** Say that a write just succeeded. Safe to call anywhere, including outside a browser. */
export function announceDataChanged(): void {
  if (typeof window === 'undefined') return;
  window.dispatchEvent(new Event(EVENT));
}

/** Listen for writes. Returns the unsubscribe function. */
export function onDataChanged(listener: () => void): () => void {
  if (typeof window === 'undefined') return () => {};
  window.addEventListener(EVENT, listener);
  return () => window.removeEventListener(EVENT, listener);
}

/**
 * Writes that change no CRM record: signing in and security, notification
 * read-marks, the AI writer's drafts, personal settings. Announcing them would
 * make every listener re-read for nothing — the Settings page, for one, POSTs
 * to /api/auth/2fa/setup every time it opens.
 */
const NOT_RECORD_WRITES = ['/api/auth/', '/api/notifications', '/api/copy', '/api/settings'];

/** Whether a successful request with this method and path changed records. */
export function changesRecords(method: string | undefined, path: string): boolean {
  const m = (method ?? 'GET').toUpperCase();
  if (m !== 'POST' && m !== 'PUT' && m !== 'PATCH' && m !== 'DELETE') return false;
  let pathname = path;
  try {
    if (/^https?:/i.test(path)) pathname = new URL(path).pathname;
  } catch {
    // Not a URL after all; use it as given.
  }
  const lower = pathname.toLowerCase();
  return !NOT_RECORD_WRITES.some((prefix) => lower.startsWith(prefix));
}
