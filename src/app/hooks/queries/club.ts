// The reads of the club's communications pages, as cached queries keyed by workspace:
// campaigns and their recipients, the communications log, and a member's consent.
//
// The fourth part of each key is the kind of record api/dataChanged names, so a save
// refreshes exactly these: sending a campaign writes member messages (communications),
// and a consent answer is saved under its member (members).

import { useQuery } from '@tanstack/react-query';
import {
  getCampaignAudience, getCampaignRecipients, getCampaigns, type CampaignPurpose, type SegmentFilter,
} from '@/app/api/campaigns';
import { getCommunications } from '@/app/api/communications';
import { getMemberConsent } from '@/app/api/consent';
import { getMembers } from '@/app/api/members';
import { workspacePrefix, type WorkspaceScope } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

const key = (s: WorkspaceScope, resource: string, ...rest: unknown[]) => [...workspacePrefix(s), resource, ...rest] as const;

export const clubKeys = {
  campaigns: (s: WorkspaceScope) => key(s, 'campaigns', 'all'),
  recipients: (s: WorkspaceScope, campaignId: string) => key(s, 'campaigns', 'recipients', campaignId),
  // Read again when members change (a tier, a status) as well as campaigns: who a segment reaches is theirs.
  audience: (s: WorkspaceScope, params: object) => key(s, 'campaigns', 'audience', params),
  communications: (s: WorkspaceScope, memberId: string | null) => key(s, 'communications', memberId ?? 'all'),
  memberConsent: (s: WorkspaceScope, memberId: string) => key(s, 'members', 'consent', memberId),
  members: (s: WorkspaceScope) => key(s, 'members', 'all'),
} as const;

/** The club's members, for choosing who a message is for. */
export function useClubMembers() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: clubKeys.members(ws),
    queryFn: ({ signal }) => timed(signal, () => getMembers()),
  });
}

export function useCampaigns() {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: clubKeys.campaigns(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getCampaigns({ signal: s })),
  });
}

/** One campaign's members and where each message stands. Off while `campaignId` is null. */
export function useCampaignRecipients(campaignId: string | null) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: clubKeys.recipients(ws, campaignId ?? ''),
    queryFn: ({ signal }) => timed(signal, (s) => getCampaignRecipients(campaignId!, { signal: s })),
    enabled: campaignId != null,
  });
}

/** Who a campaign for this segment and purpose would reach now. Off while `enabled` is false. */
export function useCampaignAudience(segment: SegmentFilter, purpose: CampaignPurpose, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: clubKeys.audience(ws, { segment, purpose }),
    queryFn: ({ signal }) => timed(signal, (s) => getCampaignAudience(segment, purpose, { signal: s })),
    enabled,
  });
}

/** The communications log, the club's or one member's. */
export function useCommunications(memberId: string | null = null) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: clubKeys.communications(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getCommunications(memberId ? { memberId } : {}, { signal: s })),
  });
}

export function useMemberConsent(memberId: string) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: clubKeys.memberConsent(ws, memberId),
    queryFn: ({ signal }) => timed(signal, (s) => getMemberConsent(memberId, { signal: s })),
    enabled: memberId.length > 0,
  });
}
