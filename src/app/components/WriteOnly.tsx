import type { ReactNode } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { orgCan, PERMISSION, type Permission } from '@/app/api/organizations';

/**
 * Renders its children only for members who may do what they offer: by default
 * create and edit records, or with `permission`, that (with a list, any one of it).
 *
 * The server refuses what a member's permissions do not allow, so offering the
 * control at all is a dead end. Wrapping a create/edit/delete affordance in this
 * keeps the gate to one line at the call site and to one definition of who may.
 * In a personal workspace everything is the person's own, so everything shows.
 *
 * Uses the optional org hook so it is safe outside an OrgProvider (e.g. tests).
 */
export function WriteOnly({
  children,
  permission = PERMISSION.EditRecords,
}: {
  children: ReactNode;
  permission?: Permission | readonly Permission[];
}) {
  const org = useOrgOptional();
  if (org?.isReadOnly) return null;
  const wanted: readonly Permission[] = typeof permission === 'string' ? [permission] : permission;
  if (org && !wanted.some((p) => orgCan(org.currentOrg ?? null, p))) return null;
  return <>{children}</>;
}
