// Staff's side of the member portal: turning it on (settings.manage), inviting members and
// ending their sessions (club.members), and who has used it. The members' own side is
// src/app/api/portal.ts, which never sends the staff token.

import { authFetchJson, type RequestOptions } from './apiClient';

export interface MemberPortalSettings {
  enabled: boolean;
  /** The portal's sign-in page, for the club's website; null before it was first turned on. */
  portalUrl: string | null;
  memberCancelHours: number;
  /** The workspace also hands new members to its own portal (Welcome & portal). */
  handsOverToOwnPortal: boolean;
  updatedAtUtc?: string | null;
  version: string;
}

export interface MemberPortalUse {
  memberId: string;
  name: string;
  email?: string | null;
  status: string;
  invitedAtUtc?: string | null;
  invitationsSent: number;
  firstSignInAtUtc?: string | null;
  lastSeenAtUtc?: string | null;
  activeSessions: number;
  canInvite: boolean;
  fromHandoff: boolean;
}

export interface InviteAllResult {
  invited: number;
  alreadyInvited: number;
  noEmail: number;
  refused: number;
  remaining: number;
}

export const getMemberPortalSettings = (req?: RequestOptions) =>
  authFetchJson<MemberPortalSettings>('/api/member-portal/settings', { signal: req?.signal });

export const saveMemberPortalSettings = (changes: { enabled?: boolean; memberCancelHours?: number }, version?: string) =>
  authFetchJson<MemberPortalSettings>('/api/member-portal/settings', {
    method: 'PUT',
    body: JSON.stringify({ ...changes, version }),
  });

export const getMemberPortalUsage = (req?: RequestOptions) =>
  authFetchJson<MemberPortalUse[]>('/api/member-portal/members', { signal: req?.signal });

export const getMemberPortalUse = (memberId: string, req?: RequestOptions) =>
  authFetchJson<MemberPortalUse>(`/api/members/${memberId}/portal`, { signal: req?.signal });

export const inviteToMemberPortal = (memberId: string) =>
  authFetchJson<MemberPortalUse>(`/api/members/${memberId}/portal/invite`, { method: 'POST' });

export const inviteAllToMemberPortal = () =>
  authFetchJson<InviteAllResult>('/api/member-portal/invite-all', { method: 'POST' });

export const endMemberPortalSessions = (memberId: string) =>
  authFetchJson<MemberPortalUse>(`/api/members/${memberId}/portal/revoke`, { method: 'POST' });
