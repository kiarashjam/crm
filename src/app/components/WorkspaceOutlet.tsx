import { Fragment } from 'react';
import { Outlet } from 'react-router-dom';
import { useOrgOptional } from '@/app/contexts/OrgContext';

/**
 * The page of the route, started afresh when the tab switches workspace. Nothing a page
 * holds (a list, a filter, the pipeline picked, a half-typed form) belongs to the
 * workspace switched to, and a page that kept it showed one workspace's records, or
 * saved into it, under the other's name.
 */
export function WorkspaceOutlet() {
  const workspace = useOrgOptional()?.currentOrgId ?? 'no-workspace';
  return (
    <Fragment key={workspace}>
      <Outlet />
    </Fragment>
  );
}
