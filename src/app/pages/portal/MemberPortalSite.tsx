// A club's member portal, at /member-portal/{its address}: the sign-in page, the landing for
// an emailed link (/sign-in), and the member's own account once signed in.
//
// Public on purpose: a member has no staff account here. The portal keeps a session of its
// own (api/portal), which reaches only the portal's API, and a staff session in the same
// browser is neither used nor touched by it.

import { useParams } from 'react-router-dom';
import { PortalHome } from './PortalHome';
import { PortalLinkLanding, PortalSignIn } from './PortalSignIn';
import { usePortalSession } from './portalCommon';

export default function MemberPortalSite({ landing = false }: { landing?: boolean }) {
  const { key = '' } = useParams();
  const session = usePortalSession(key);
  if (landing) return <PortalLinkLanding portalKey={key} />;
  // A new session (another member signing in on this browser) starts the page afresh.
  return session ? <PortalHome key={session.token} portalKey={key} session={session} /> : <PortalSignIn portalKey={key} />;
}
