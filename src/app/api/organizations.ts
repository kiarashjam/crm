import { authFetchJson, authFetch, servesDemoData, type RequestOptions } from './apiClient';
import { getCurrentUser } from '@/app/lib/auth';

export interface Organization {
  id: string;
  name: string;
  ownerUserId: string;
  isOwner: boolean;
  /** 0 Owner, 1 Member, 2 Manager, 3 Viewer. The coarse role; what the member may do is `permissions`. */
  role: number;
  /** The preset the caller's permissions start from, e.g. "sales_rep". Older servers send none. */
  permissionPreset?: string;
  /** What the caller may do here (see PERMISSION). Older servers send none, and the role decides. */
  permissions?: string[];
  /** ISO code new deals are priced in. Older servers send none. Club pages keep their own (hospitality settings). */
  currency?: string;
  /** How amounts are written (the number format), e.g. "de-CH". Dates stay in the viewer's language. */
  locale?: string;
  /** IANA zone the workspace's days and months begin in, e.g. "Europe/Zurich". */
  timeZone?: string;
  /** Days without activity after which an open deal is stale, unless its pipeline says otherwise; 0 never. Older servers send none. */
  staleDealDays?: number;
  /** Whether the Sales module is on (leads, deals, outreach, sales reports). Older servers send none: on. */
  salesEnabled?: boolean;
  /** Whether the Club module is on (members, house accounts, events, campaigns…). Older servers send none: on. */
  clubEnabled?: boolean;
}

/**
 * The two halves of Cadence a workspace can turn off (the server's WorkspaceModules).
 * Contacts, companies, tasks, activities, the calendar, the team and settings belong to
 * both and are always there.
 */
export type WorkspaceModule = 'sales' | 'club';

export const MODULE_LABELS: Readonly<Record<WorkspaceModule, string>> = { sales: 'Sales', club: 'Club' };

/**
 * Whether the workspace has this module on. On unless it says otherwise: a personal
 * workspace, a demo one and an older server have both.
 */
export function moduleOn(org: Pick<Organization, 'salesEnabled' | 'clubEnabled'> | null | undefined, module: WorkspaceModule): boolean {
  if (!org) return true;
  return (module === 'sales' ? org.salesEnabled : org.clubEnabled) !== false;
}

/** The server's answer when a request reaches a module the workspace has turned off. */
export const MODULE_OFF_CODE = 'Module.Off';

/**
 * Turn the workspace's Sales or Club module on or off (whoever manages its settings).
 * Fields left out keep their value. Throws with the server's reason when it refuses: one
 * module always stays on, and a module whose own work still runs (the club's automatic
 * billing, a scheduled campaign, leads in sequences) is not turned off until that stops.
 */
export async function updateWorkspaceModules(
  id: string,
  modules: { sales?: boolean; club?: boolean },
): Promise<{ sales: boolean; club: boolean }> {
  return authFetchJson<{ sales: boolean; club: boolean }>(`/api/organizations/${id}/modules`, {
    method: 'PUT',
    body: JSON.stringify(modules),
  });
}

/** Role ids as persisted by the backend (OrgMemberRole). */
export const ORG_ROLE = { Owner: 0, Member: 1, Manager: 2, Viewer: 3 } as const;

/**
 * What a member of an organization may do, beyond reading its records (the server's
 * MemberPermissions). Each member holds a set, started from a preset; the server
 * refuses what they do not hold, and the app hides it.
 */
export const PERMISSION = {
  EditRecords: 'records.edit',
  DeleteRecords: 'records.delete',
  ExportRecords: 'records.export',
  SeeAllDeals: 'deals.all',
  ManageSettings: 'settings.manage',
  ManageMembers: 'members.manage',
  /** Export everything held about a person, erase a person, and set how long personal data is kept. */
  ManagePrivacy: 'privacy.manage',
  ManageMoney: 'money.manage',
  VoidMoney: 'money.void',
  ClubMembers: 'club.members',
  FrontDesk: 'club.frontdesk',
} as const;

export type Permission = (typeof PERMISSION)[keyof typeof PERMISSION];

const EVERY_PERMISSION: readonly string[] = Object.values(PERMISSION);

/** The permissions that change something. A member with none of them can only read (and export). */
const WRITES: ReadonlySet<string> = new Set(
  EVERY_PERMISSION.filter((p) => p !== PERMISSION.ExportRecords && p !== PERMISSION.SeeAllDeals),
);

/** What a Member could do before permission sets, for a server that does not send permissions. */
const LEGACY_MEMBER: readonly string[] = [
  PERMISSION.EditRecords,
  PERMISSION.DeleteRecords,
  PERMISSION.ExportRecords,
  PERMISSION.ManageMoney,
  PERMISSION.ClubMembers,
  PERMISSION.FrontDesk,
];

type OrgAccess = { isOwner?: boolean; role?: number; permissions?: string[] } | null | undefined;

/**
 * What the caller may do in this organization. With none (a personal workspace)
 * everything is theirs; the owner may do everything; otherwise what the server sent,
 * or, from a server too old to send it, what the role allowed.
 */
export function orgPermissions(org: OrgAccess): ReadonlySet<string> {
  if (!org || org.isOwner === true || org.role === ORG_ROLE.Owner) return new Set(EVERY_PERMISSION);
  if (Array.isArray(org.permissions)) return new Set(org.permissions);
  if (org.role === ORG_ROLE.Manager) return new Set(EVERY_PERMISSION);
  if (org.role === ORG_ROLE.Viewer) return new Set([PERMISSION.ExportRecords]);
  return new Set(LEGACY_MEMBER);
}

/** Whether the caller may do this in the organization (a personal workspace: always). */
export function orgCan(org: OrgAccess, permission: Permission): boolean {
  return orgPermissions(org).has(permission);
}

/**
 * True when the user's role in this organization is view-only: they can see every
 * record but the backend rejects any change. The UI uses this to hide or disable
 * editing affordances so nothing is offered that would just fail.
 */
export function isOrgViewer(org: OrgAccess): boolean {
  if (!org) return false;
  // An owner is never read-only, even if a stale role value says otherwise.
  if (org.isOwner === true) return false;
  return ![...orgPermissions(org)].some((p) => WRITES.has(p));
}

export interface OrgMemberDto {
  userId: string;
  name: string;
  email: string;
  role: number; // 0 Owner, 1 Member, 2 Manager, 3 Viewer
  /** The preset their permissions start from. Older servers send none. */
  preset?: string;
  /** What they may do. */
  permissions?: string[];
  /** Whether the owner changed their permissions from the preset's (or they kept an old role's). */
  customised?: boolean;
}

/**
 * How each preset is named, for a page that does not load the catalogue (the Team page
 * does, and names them from the server's).
 */
export const PRESET_LABELS: Readonly<Record<string, string>> = {
  owner: 'Owner',
  manager: 'Manager',
  sales_rep: 'Sales rep',
  membership_director: 'Membership director',
  finance: 'Finance',
  front_desk: 'Front desk',
  read_only_accountant: 'Read-only accountant',
};

/** A permission as the Team page lists it. */
export interface PermissionInfo {
  key: string;
  label: string;
  description: string;
}

/** A preset and what it allows. The owner's is listed but given to nobody. */
export interface PermissionPreset {
  key: string;
  label: string;
  description: string;
  permissions: string[];
  assignable: boolean;
}

export interface PermissionCatalogue {
  permissions: PermissionInfo[];
  presets: PermissionPreset[];
}

/** Every permission and preset a member can be given. */
export async function getPermissionSets(organizationId: string): Promise<PermissionCatalogue> {
  return authFetchJson<PermissionCatalogue>(`/api/organizations/${organizationId}/permission-sets`);
}

/**
 * Set what a member may do: a preset, and (the owner only) their own list of
 * permissions instead of the preset's. Throws with the server's reason when the
 * caller may not give that much.
 */
export async function setMemberPermissions(
  organizationId: string,
  memberUserId: string,
  preset: string,
  permissions?: string[],
): Promise<OrgMemberDto> {
  return authFetchJson<OrgMemberDto>(`/api/organizations/${organizationId}/members/${memberUserId}/permissions`, {
    method: 'PUT',
    body: JSON.stringify(permissions ? { preset, permissions } : { preset }),
  });
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

export async function getOrgMembers(organizationId: string, req?: RequestOptions): Promise<OrgMemberDto[]> {
  if (servesDemoData()) {
    // Demo mode: the only "member" is the demo user themselves.
    const u = getCurrentUser();
    if (!u) return [];
    return [{ userId: u.id, name: u.name, email: u.email, role: 0, preset: 'owner', permissions: [...EVERY_PERMISSION] }];
  }
  const list = await authFetchJson<OrgMemberDto[]>(`/api/organizations/${organizationId}/members`, req);
  return Array.isArray(list) ? list : [];
}

/**
 * Add an existing registered user to the organization directly, skipping the
 * invitation + acceptance round trip. Owner/manager only. Throws with the
 * server's explanation when the person has no account yet or is already a member.
 */
export async function addOrgMember(organizationId: string, email: string, role: number, preset?: string): Promise<OrgMemberDto | null> {
  const member = await authFetchJson<OrgMemberDto>(`/api/organizations/${organizationId}/members`, {
    method: 'POST',
    body: JSON.stringify(preset ? { email: email.trim().toLowerCase(), role, preset } : { email: email.trim().toLowerCase(), role }),
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
