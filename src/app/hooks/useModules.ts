// Which of the Sales and Club modules the workspace in use has on.
//
// A module that is off leaves the menu, its pages say it is turned off, and the server
// refuses its endpoints (Module.Off). Pages and panels that belong to both modules (a
// contact's page, the calendar, tasks) ask this before reading the other module's
// records, so a workspace without the club does not see "could not load" where its
// events would be.

import { useMemo } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { moduleOn, type WorkspaceModule } from '@/app/api/organizations';

export type ModulesOn = Readonly<Record<WorkspaceModule, boolean>>;

export function useModules(): ModulesOn {
  const current = useOrgOptional()?.currentOrg ?? null;
  const sales = moduleOn(current, 'sales');
  const club = moduleOn(current, 'club');
  return useMemo(() => ({ sales, club }), [sales, club]);
}
