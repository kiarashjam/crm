import type { ReactNode } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';

/**
 * Renders its children only for members who may change data.
 *
 * A Viewer's writes are refused by the backend, so offering the control at all
 * is a dead end. Wrapping a create/edit/delete affordance in this keeps the gate
 * to one line at the call site and to one definition of "read-only".
 *
 * Uses the optional org hook so it is safe outside an OrgProvider (e.g. tests).
 */
export function WriteOnly({ children }: { children: ReactNode }) {
  const org = useOrgOptional();
  if (org?.isReadOnly) return null;
  return <>{children}</>;
}
