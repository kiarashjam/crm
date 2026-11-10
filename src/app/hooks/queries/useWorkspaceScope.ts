import { useMemo } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { getCurrentOrganizationId, getCurrentUser } from '@/app/lib/auth';
import type { WorkspaceScope } from './queryKeys';

/**
 * Whose data a page reads: the workspace open in this tab, and the signed-in user.
 * Every query key starts with it, so a switch of either reads afresh rather than
 * showing what was cached for the other.
 */
export function useWorkspaceScope(): WorkspaceScope {
  const org = useOrgOptional();
  // Outside the workspace provider (a page tested on its own), the tab's stored workspace.
  const orgId = org ? (org.currentOrgId ?? org.currentOrg?.id ?? null) : getCurrentOrganizationId();
  const userId = getCurrentUser()?.id ?? null;
  return useMemo(() => ({ orgId, userId }), [orgId, userId]);
}
