import { useCallback } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { orgCan, PERMISSION, type Permission } from '@/app/api/organizations';

type OrgLike = { isReadOnly?: boolean; currentOrg?: Parameters<typeof orgCan>[0] } | null | undefined;

/**
 * Whether the signed-in member may do what a control offers: by default create and
 * edit records, or with `permission`, that (with a list, any one of it; with `all`,
 * every one of it). A read-only member may do nothing; outside a workspace (a personal
 * one, or a page tested on its own) everything is the person's own.
 *
 * The one definition of who may, shared by WriteOnly, empty states and the pages that
 * need the answer as a value (a disabled field, a column that is not drawn).
 */
export function mayIn(org: OrgLike, permission: Permission | readonly Permission[] = PERMISSION.EditRecords, all = false): boolean {
  if (org?.isReadOnly) return false;
  if (!org) return true;
  const wanted: readonly Permission[] = typeof permission === 'string' ? [permission] : permission;
  const may = (p: Permission) => orgCan(org.currentOrg ?? null, p);
  return all ? wanted.every(may) : wanted.some(may);
}

/** `mayIn` for the signed-in member, as a function a page asks with each permission. */
export function useMay(): (permission?: Permission | readonly Permission[], all?: boolean) => boolean {
  const org = useOrgOptional();
  return useCallback((permission?: Permission | readonly Permission[], all = false) => mayIn(org, permission, all), [org]);
}
