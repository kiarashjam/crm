import type { LeadStatus } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';

/**
 * Get all lead statuses for the current org (requires X-Organization-Id).
 *
 * Returns `[]` for anything that is not an array, because the caller cannot do
 * anything useful with a malformed list — but says so first. That silence used to
 * matter a great deal: the endpoint answered with the server's `Result` wrapper
 * rather than the array, `Array.isArray` was false, and every caller read a
 * perfectly successful 200 as "this workspace has no statuses". Auto-sync then
 * held every status write back, and leads sat on "New" no matter how much work was
 * logged against them, with nothing anywhere to suggest why.
 *
 * The server side is fixed and pinned by a test. This is the belt: if the shape is
 * ever wrong again, it is one line in the console away from being obvious instead
 * of a fortnight of "the status doesn't update".
 */
export async function getLeadStatuses(): Promise<LeadStatus[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<LeadStatus[]>('/api/leadstatuses');
    if (Array.isArray(list)) return list;
    console.error(
      '/api/leadstatuses did not return an array. The lead status list will be empty, '
      + 'so no pipeline step can move a lead’s status. Received:',
      list,
    );
    return [];
  }
  return [];
}
