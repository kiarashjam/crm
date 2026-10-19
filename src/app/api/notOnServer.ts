// Features the backend does not serve yet.
//
// These screens were built ahead of their API. Each call tries the server first
// and, on the 404, falls back to storage in this browser (`apiWithFallback`). The
// fallback works, but every visit still sent the request and printed a failed
// call in the console: three on every page for the sales agent's automations
// list alone.
//
// Known in advance, so `authFetch` answers reads of these itself with the same
// 404, without a request. Writes still go out: the server refuses a Viewer's
// with a 403 before routing, which the app would otherwise never see. `notOnServer.test.ts` reads the backend's controllers and
// fails when one of these gains a controller (take it off the list, and its
// screen starts using the server) or when a new call goes to a route nothing
// serves (add it here, or build the route).

export const NOT_ON_SERVER: readonly string[] = [];

const missing = new Set(NOT_ON_SERVER);

/** The first segment after `/api/`, e.g. "custom-fields" for `/api/custom-fields/values?…`. */
export function apiBase(path: string): string | null {
  let pathname = path;
  try {
    if (/^https?:/i.test(path)) pathname = new URL(path).pathname;
  } catch {
    // Not a URL after all; use it as given.
  }
  const match = /^\/?api\/([a-z0-9-]+)/i.exec(pathname);
  return match?.[1]?.toLowerCase() ?? null;
}

/** Whether nothing on the server answers this path. */
export function isNotOnServer(path: string): boolean {
  const base = apiBase(path);
  return base != null && missing.has(base);
}
