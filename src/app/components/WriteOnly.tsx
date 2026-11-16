import type { ReactNode } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import { mayIn } from '@/app/hooks/useMay';

/**
 * Renders its children only for members who may do what they offer: by default
 * create and edit records, or with `permission`, that (with a list, any one of it;
 * with `all`, every one of it).
 *
 * The server refuses what a member's permissions do not allow, so offering the
 * control at all is a dead end. Wrapping a create/edit/delete affordance in this
 * keeps the gate to one line at the call site and to one definition of who may.
 * In a personal workspace everything is the person's own, so everything shows.
 *
 * A delete takes `all`: the server asks for records.delete on top of what changing
 * that kind of record takes (records.edit, club.members, money.manage, …), so a
 * member allowed to delete but not to work that area is refused there.
 *
 * Uses the optional org hook so it is safe outside an OrgProvider (e.g. tests).
 */
export function WriteOnly({
  children,
  permission = PERMISSION.EditRecords,
  all = false,
}: {
  children: ReactNode;
  permission?: Permission | readonly Permission[];
  /** Every permission in the list is needed, not just one. */
  all?: boolean;
}) {
  const org = useOrgOptional();
  if (!mayIn(org, permission, all)) return null;
  return <>{children}</>;
}
