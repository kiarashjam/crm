import { authFetchJson, authFetch, servesDemoData } from './apiClient';
import { getCurrentUser } from '@/app/lib/auth';

export interface Organization {
  id: string;
  name: string;
  ownerUserId: string;
  isOwner: boolean;
  /** 0 Owner, 1 Member, 2 Manager. */
  role: number;
  /** ISO code new deals are priced in. Older servers send none. Club pages keep their own (hospitality settings). */
  currency?: string;
  /** How amounts are written (the number format), e.g. "de-CH". Dates stay in the viewer's language. */
  locale?: string;
  /** IANA zone the workspace's days and months begin in, e.g. "Europe/Zurich". */
  timeZone?: string;
  /** Days without activity after which an open deal is stale, unless its pipeline says otherwise; 0 never. Older servers send none. */
  staleDealDays?: number;
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

/**
 * Set what the workspace works in (owner or manager). Fields left out keep their value.
 * Throws with the server's reason when a code, locale or zone is not one it knows.
 */
export async function updateOrganizationSettings(
  id: string,
  settings: { currency?: string; locale?: string; timeZone?: string; staleDealDays?: number },
): Promise<Organization | null> {
  const org = await authFetchJson<Organization>(`/api/organizations/${id}/settings`, {
    method: 'PUT',
    body: JSON.stringify(settings),
  });
  return org ?? null;
}

/** Who the workspace's email goes out from. */
export interface EmailSending {
  /** The workspace's own sender address, as saved; null when none is. */
  sendingAddress: string | null;
  /** Whether the mail service has verified its domain. Until it has, email goes out from `sharedAddress`. */
  verified: boolean;
  /** The configured address every workspace without a verified one sends from. */
  sharedAddress: string | null;
  /** Whether replies come back onto the record's timeline as well as reaching the sender. */
  repliesCaptured: boolean;
}

/** The workspace's sender, or null in demo mode, which sends nothing. */
export async function getEmailSending(orgId: string): Promise<EmailSending | null> {
  if (servesDemoData()) return null;
  return authFetchJson<EmailSending>(`/api/organizations/${orgId}/email`);
}

/** Sets the workspace's own sending address, or clears it with an empty string. Owner or manager only. */
export async function updateEmailSending(orgId: string, sendingAddress: string): Promise<EmailSending> {
  return authFetchJson<EmailSending>(`/api/organizations/${orgId}/email`, {
    method: 'PUT',
    body: JSON.stringify({ sendingAddress }),
  });
}

/** An address the workspace does not email because the mail service reported it. */
export interface EmailSuppression {
  id: string;
  email: string;
  reason: 'bounced' | 'spam_report' | 'invalid' | 'unsubscribed';
  /** What the mail service said, e.g. "550 5.1.1 The email account does not exist". */
  detail: string | null;
  createdAtUtc: string;
}

export async function getEmailSuppressions(orgId: string): Promise<EmailSuppression[]> {
  if (servesDemoData()) return [];
  return authFetchJson<EmailSuppression[]>(`/api/organizations/${orgId}/email/suppressions`);
}

/** Takes an address off the list, so the workspace may email it again. Owner or manager only. */
export async function liftEmailSuppression(orgId: string, suppressionId: string): Promise<void> {
  await authFetchJson<void>(`/api/organizations/${orgId}/email/suppressions/${suppressionId}`, { method: 'DELETE' });
}

export async function listMyPendingInvites(): Promise<InviteDto[]> {
  if (servesDemoData()) return [];
  const list = await authFetchJson<InviteDto[]>('/api/invites/pending');
  return Array.isArray(list) ? list : [];
}

/**
 * Accept with the token from the invitation email's link. There is no accept by id:
 * the token is what shows the person accepting received the email.
 */
export async function acceptInvite(token: string): Promise<InviteDto | null> {
  const invite = await authFetchJson<InviteDto>('/api/invites/accept', {
    method: 'POST',
    body: JSON.stringify({ token }),
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

/**
 * Re-send the invitation email for a pending invite (and refresh its expiry).
 * Throws when the server could not send — the message explains why, which is how
 * an unconfigured mail server becomes visible in the UI.
 */
export async function resendInvite(inviteId: string): Promise<InviteDto | null> {
  const invite = await authFetchJson<InviteDto>(`/api/invites/${inviteId}/resend`, {
    method: 'POST',
  });
  return invite ?? null;
}

export async function listPendingInvitesForOrg(organizationId: string): Promise<InviteDto[]> {
  if (servesDemoData()) return [];
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
  if (servesDemoData()) return [];
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
  if (servesDemoData()) {
    // Demo mode: the only "member" is the demo user themselves.
    const u = getCurrentUser();
    if (!u) return [];
    return [{ userId: u.id, name: u.name, email: u.email, role: 0 }];
  }
  const list = await authFetchJson<OrgMemberDto[]>(`/api/organizations/${organizationId}/members`);
  return Array.isArray(list) ? list : [];
}

/**
 * Add an existing registered user to the organization directly, skipping the
 * invitation + acceptance round trip. Owner/manager only. Throws with the
 * server's explanation when the person has no account yet or is already a member.
 */
export async function addOrgMember(organizationId: string, email: string, role: number): Promise<OrgMemberDto | null> {
  const member = await authFetchJson<OrgMemberDto>(`/api/organizations/${organizationId}/members`, {
    method: 'POST',
    body: JSON.stringify({ email: email.trim().toLowerCase(), role }),
  });
  return member ?? null;
}

export async function updateMemberRole(organizationId: string, memberUserId: string, role: number): Promise<boolean> {
  const res = await authFetch(`/api/organizations/${organizationId}/members/${memberUserId}/role`, {
    method: 'PUT',
    body: JSON.stringify({ role }),
  });
  return res.status === 204;
}

/** A member was removed: who took over their open work, and how much of it. */
export interface MemberRemovedDto {
  handedOverToUserId: string;
  handedOverToName: string;
  leads: number;
  deals: number;
  tasks: number;
}

/**
 * Remove a member (owner or manager). Their open leads, deals and tasks go to
 * `handOverTo`, or to the owner when it is left out. Throws with the server's
 * reason when that person cannot take them (read-only, or not a member).
 */
export async function removeMember(organizationId: string, memberUserId: string, handOverTo?: string): Promise<MemberRemovedDto> {
  const query = handOverTo ? `?handOverTo=${encodeURIComponent(handOverTo)}` : '';
  return authFetchJson<MemberRemovedDto>(`/api/organizations/${organizationId}/members/${memberUserId}${query}`, {
    method: 'DELETE',
  });
}

/** "12 leads, 3 deals and 7 tasks"; the kinds with none left out. Empty when nothing moved. */
export function handedOverSummary(removed: Pick<MemberRemovedDto, 'leads' | 'deals' | 'tasks'>): string {
  const parts = [
    [removed.leads, 'lead'],
    [removed.deals, 'deal'],
    [removed.tasks, 'task'],
  ]
    .filter(([count]) => (count as number) > 0)
    .map(([count, noun]) => `${count} ${noun}${count === 1 ? '' : 's'}`);
  if (parts.length <= 1) return parts.join('');
  return `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
}
