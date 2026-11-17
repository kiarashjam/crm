// The Leads page's way into the newsletter: "Invite to a meeting". Shown only to someone the
// server lets send it (records.edit, as POST /api/leads/newsletter/send requires), and only
// with a server to send it: in demo mode there is none.

import { useState } from 'react';
import { CalendarHeart } from 'lucide-react';
import { servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { Button } from '@/app/components/ui/button';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import type { NewsletterMember } from './AudienceStep';
import { LeadNewsletterDialog } from './LeadNewsletterDialog';

export function useCanSendNewsletter(): boolean {
  const org = useOrgOptional();
  return !servesDemoData() && !org?.isReadOnly && orgCan(org?.currentOrg ?? null, PERMISSION.EditRecords);
}

export function LeadNewsletterButton({ statuses, sources, members, className }: {
  statuses: string[];
  sources: string[];
  members: NewsletterMember[];
  className?: string;
}) {
  const canSend = useCanSendNewsletter();
  const [open, setOpen] = useState(false);
  // Mounted on the first open and kept: closed and opened again, the draft is still there.
  const [opened, setOpened] = useState(false);
  if (!canSend) return null;
  return (
    <>
      <Button
        type="button"
        variant="outline"
        className={className}
        aria-label="Invite to a meeting"
        aria-haspopup="dialog"
        onClick={() => { setOpened(true); setOpen(true); }}
      >
        <CalendarHeart className="h-4 w-4 text-orange-500" aria-hidden />
        <span className="hidden sm:inline">Invite to a meeting</span>
      </Button>
      {opened && (
        <LeadNewsletterDialog open={open} onOpenChange={setOpen} statuses={statuses} sources={sources} members={members} />
      )}
    </>
  );
}
