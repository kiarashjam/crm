import { Fragment } from 'react';
import { Outlet } from 'react-router-dom';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { SsoRequiredGate } from '@/app/components/auth/SsoRequiredGate';

/**
 * The page of the route, started afresh when the tab switches workspace. Nothing a page
 * holds (a list, a filter, the pipeline picked, a half-typed form) belongs to the
 * workspace switched to, and a page that kept it showed one workspace's records, or
 * saved into it, under the other's name.
 */
export function WorkspaceOutlet() {
  const org = useOrgOptional();
  const workspace = org?.currentOrgId ?? 'no-workspace';
  // The workspace requires single sign-on and this session began another way: the server
  // refuses its every request (Sso.Required), so say why and offer the way in, rather
  // than a page of lists that all failed.
  if (org?.currentOrg?.ssoSignInNeeded) return <SsoRequiredGate org={org.currentOrg} />;
  return (
    <Fragment key={workspace}>
      <Outlet />
    </Fragment>
  );
}
