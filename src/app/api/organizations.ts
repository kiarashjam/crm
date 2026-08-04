import { authFetchJson, authFetch, isUsingRealApi } from './apiClient';
import { getCurrentUser } from '@/app/lib/auth';

export interface Organization {
  id: string;
  name: string;
  ownerUserId: string;
  isOwner: boolean;
  /** 0 Owner, 1 Member, 2 Manager. */
  role: number;
}

/** Role ids as persisted by the backend (OrgMemberRole). */
export const ORG_ROLE = { Owner: 0, Member: 1, Manager: 2, Viewer: 3 } as const;

/** True when the current user is Owner or Manager of the organization (full admin access). */
export function isOrgAdmin(org: { isOwner?: boolean; role?: number } | null | undefined): boolean {
  if (!org) return false;
  return org.isOwner === true || org.role === 0 || org.role === 2;
}

/**
 * True when the user's role in this organization is view-only: they can see every
 * record but the backend rejects any create/edit/delete. The UI uses this to hide
 * or disable editing affordances so nothing is offered that would just fail.
 */
export function isOrgViewer(org: { isOwner?: boolean; role?: number } | null | undefined): boolean {
  if (!org) return false;
  // An owner is never read-only, even if a stale role value says otherwise.
  if (org.isOwner === true) return false;
  return org.role === ORG_ROLE.Viewer;
}

export interface OrgMemberDto {
  userId: string;
  name: string;
  email: string;
  role: number; // 0 Owner, 1 Member, 2 Manager
}

export interface InviteDto {
  id: string;
  organizationId: string;
  organizationName: string;
  email: string;
  expiresAtUtc: string;
  createdAtUtc: string;
}

export interface JoinRequestDto {
  id: string;
  organizationId: string;
  organizationName: string;
  userId: string;
  userName: string;
  userEmail: string;
  status: string;
  createdAtUtc: string;
}

export async function listMyOrganizations(): Promise<Organization[]> {
  const list = await authFetchJson<Organization[]>('/api/organizations');
  return Array.isArray(list) ? list : [];
}

export async function getOrganization(id: string): Promise<Organization | null> {
  const org = await authFetchJson<Organization>(`/api/organizations/${id}`);
  return org ?? null;
}

export async function createOrganization(name: string): Promise<Organization | null> {
  const org = await authFetchJson<Organization>('/api/organizations', {
    method: 'POST',
    body: JSON.stringify({ name: name.trim() || 'My Organization' }),
  });
  return org ?? null;
}

export async function listMyPendingInvites(): Promise<InviteDto[]> {
  if (!isUsingRealApi()) return [];
  const list = await authFetchJson<InviteDto[]>('/api/invites/pending');
  return Array.isArray(list) ? list : [];
}

export async function acceptInvite(token: string): Promise<InviteDto | null> {
  const invite = await authFetchJson<InviteDto>('/api/invites/accept', {
    method: 'POST',
    body: JSON.stringify({ token }),
  });
  return invite ?? null;
}

export async function acceptInviteById(inviteId: string): Promise<InviteDto | null> {
  const invite = await authFetchJson<InviteDto>(`/api/invites/${inviteId}/accept`, {
    method: 'POST',
  });
  return invite ?? null;
}

export async function createInvite(organizationId: string, email: string): Promise<InviteDto | null> {
  const invite = await authFetchJson<InviteDto>(`/api/invites/${organizationId}`, {
    method: 'POST',
    body: JSON.stringify({ email: email.trim().toLowerCase() }),
  });
  return invite ?? null;
}

export async function listPendingInvitesForOrg(organizationId: string): Promise<InviteDto[]> {
  if (!isUsingRealApi()) return [];
  const list = await authFetchJson<InviteDto[]>(`/api/invites/organization/${organizationId}`);
  return Array.isArray(list) ? list : [];
}

export async function createJoinRequest(organizationId: string): Promise<JoinRequestDto | null> {
  const jr = await authFetchJson<JoinRequestDto>(`/api/joinrequests/${organizationId}`, {
    method: 'POST',
  });
  return jr ?? null;
}

export async function listPendingJoinRequestsForOrg(organizationId: string): Promise<JoinRequestDto[]> {
  if (!isUsingRealApi()) return [];
  const list = await authFetchJson<JoinRequestDto[]>(`/api/joinrequests/organization/${organizationId}`);
  return Array.isArray(list) ? list : [];
}

export async function acceptJoinRequest(joinRequestId: string): Promise<JoinRequestDto | null> {
  const jr = await authFetchJson<JoinRequestDto>(`/api/joinrequests/${joinRequestId}/accept`, {
    method: 'POST',
  });
  return jr ?? null;
}

export async function rejectJoinRequest(joinRequestId: string): Promise<JoinRequestDto | null> {
  const jr = await authFetchJson<JoinRequestDto>(`/api/joinrequests/${joinRequestId}/reject`, {
    method: 'POST',
  });
  return jr ?? null;
}

export async function getOrgMembers(organizationId: string): Promise<OrgMemberDto[]> {
  if (!isUsingRealApi()) {
    // Demo mode: the only "member" is the demo user themselves.
    const u = getCurrentUser();
    if (!u) return [];
    return [{ userId: u.id, name: u.name, email: u.email, role: 0 }];
  }
  const list = await authFetchJson<OrgMemberDto[]>(`/api/organizations/${organizationId}/members`);
  return Array.isArray(list) ? list : [];
}

export async function updateMemberRole(organizationId: string, memberUserId: string, role: number): Promise<boolean> {
  const res = await authFetch(`/api/organizations/${organizationId}/members/${memberUserId}/role`, {
    method: 'PUT',
    body: JSON.stringify({ role }),
  });
  return res.status === 204;
}

export async function removeMember(organizationId: string, memberUserId: string): Promise<boolean> {
  const res = await authFetch(`/api/organizations/${organizationId}/members/${memberUserId}`, {
    method: 'DELETE',
  });
  return res.status === 204;
}
