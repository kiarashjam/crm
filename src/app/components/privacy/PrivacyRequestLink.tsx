// "Privacy" on a lead's, a contact's or a member's page: opens the privacy requests page with
// that record as the start, where everything held about the person can be downloaded or
// erased. Shown only in a workspace, to someone who may handle privacy requests, and not on
// demo data (which has nobody to export or erase).

import { Link } from 'react-router-dom';
import { ShieldCheck } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { servesDemoData } from '@/app/api/apiClient';
import { PERMISSION } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { mayIn } from '@/app/hooks/useMay';

export function PrivacyRequestLink({ kind, id, className }: { kind: 'lead' | 'contact' | 'member'; id: string; className?: string }) {
  const org = useOrgOptional();
  if (servesDemoData() || !org?.currentOrg || !mayIn(org, PERMISSION.ManagePrivacy)) return null;
  return (
    <Button asChild variant="outline" className={className}>
      <Link to={`/privacy-requests?${kind}=${encodeURIComponent(id)}`} aria-label="Privacy request: download or erase everything held about this person">
        <ShieldCheck className="h-4 w-4" aria-hidden="true" />
        <span className="hidden xl:inline">Privacy</span>
      </Link>
    </Button>
  );
}
