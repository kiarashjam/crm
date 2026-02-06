import { authFetchJson, authFetch } from './apiClient';

export interface Organization {
  id: string;
  name: string;
  ownerUserId: string;
  isOwner: boolean;
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
