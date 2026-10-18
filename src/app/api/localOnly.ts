/**
 * Which features fell back to this browser's storage because the backend has
 * no endpoint for them yet.
 *
 * `apiWithFallback` records every such fallback here, keyed by the request's
 * resource path ("members", "gift-cards", "deals/line-items"). Pages use it to
 * tell people that what they are entering is saved on this device only. The
 * registry is filled from real 404s rather than a hand-kept list, so a feature
 * stops being reported the moment the backend starts serving it.
 */

const localOnly = new Set<string>();
const listeners = new Set<() => void>();

/** A path segment that is a record id rather than part of the route. */
function isIdSegment(segment: string): boolean {
  return /\d/.test(segment);
}

/** "/api/deals/line-items/li-123?dealId=x" and "/api/deals/<id>/line-items" -> "deals/line-items".
 *  Ids are dropped wherever they sit, so every record of a resource maps to one key. */
export function resourceOf(path: string): string | null {
  let pathname = path.split(/[?#]/)[0] ?? '';
  if (pathname.includes('://')) {
    try {
      pathname = new URL(pathname).pathname;
    } catch {
      return null;
    }
  }
  const segments = pathname.split('/').filter(Boolean);
  if (segments[0] === 'api') segments.shift();
  const route: string[] = [];
  for (const segment of segments) {
    if (!isIdSegment(segment)) route.push(segment);
  }
  return route.length ? route.join('/') : null;
}

export function markLocalOnly(path: string | undefined): void {
  if (!path) return;
  const resource = resourceOf(path);
  if (!resource || localOnly.has(resource)) return;
  localOnly.add(resource);
  listeners.forEach((listener) => listener());
}

/** True when any of `features` (or a sub-route of one) has fallen back. */
export function isLocalOnly(features: readonly string[]): boolean {
  for (const resource of localOnly) {
    if (features.some((f) => resource === f || resource.startsWith(`${f}/`))) return true;
  }
  return false;
}

export function subscribeLocalOnly(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Test hook: forget everything recorded so far. */
export function resetLocalOnly(): void {
  localOnly.clear();
  listeners.forEach((listener) => listener());
}
