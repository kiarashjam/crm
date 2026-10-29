/** The page an invitation email links to, with the invitation's token in `?token=`. */
export const INVITE_ACCEPT_PATH = '/invites/accept';

/**
 * Where to go after signing in, when signing in interrupted opening an invitation link.
 *
 * RequireAuth sends a signed-out visitor to /login with the location they asked for
 * in `state.from`. Login used to ignore it, so the token in the link was lost and the
 * invitation could no longer be accepted. Only the accept page is resumed, and only as
 * an in-app path: anything else returns null and Login lands where it always has.
 */
export function invitationToResume(state: unknown): string | null {
  const from = (state as { from?: unknown } | null | undefined)?.from;
  if (!from || typeof from !== 'object') return null;
  const { pathname, search } = from as { pathname?: unknown; search?: unknown };
  if (pathname !== INVITE_ACCEPT_PATH) return null;
  const query = typeof search === 'string' && search.startsWith('?') ? search : '';
  return `${INVITE_ACCEPT_PATH}${query}`;
}
