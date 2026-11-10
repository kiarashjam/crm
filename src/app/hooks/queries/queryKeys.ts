// Every cached read is keyed by the workspace it was read in, then by the kind of
// record it holds, then by what exactly was asked:
//
//   ['ws', orgId | 'personal', userId, 'leads', 'page', { page: 2, … }]
//
// So one organisation's rows are never served under another's, not even for a
// moment after a switch (the new workspace's keys simply hold nothing yet); a write
// refreshes exactly the kinds it changed, found by the fourth part of the key (see
// api/dataChanged and invalidation.ts); and a switch or a sign-out can drop a whole
// workspace's cache at once.

import type { WorkScope } from '@/app/lib/workScope';

/** Whose data a read is: the workspace (null for a personal one) and the signed-in user. */
export interface WorkspaceScope {
  orgId: string | null;
  userId: string | null;
}

const PREFIX = 'ws';

/** The first three parts of every key: whose data it is. */
export function workspacePrefix(scope: WorkspaceScope): readonly [string, string, string] {
  return [PREFIX, scope.orgId ?? 'personal', scope.userId ?? 'signed-out'];
}

/** Whether a key belongs to this workspace (`orgId` null: the personal one). */
export function isWorkspaceKey(key: readonly unknown[], orgId?: string | null): boolean {
  if (key[0] !== PREFIX) return false;
  return orgId === undefined || key[1] === (orgId ?? 'personal');
}

/** The kind of record a cached read holds: the fourth part of its key. */
export function resourceOfKey(key: readonly unknown[]): string | null {
  return key[0] === PREFIX && typeof key[3] === 'string' ? key[3] : null;
}

const key = (scope: WorkspaceScope, resource: string, ...rest: unknown[]) =>
  [...workspacePrefix(scope), resource, ...rest] as const;

/**
 * The keys, by kind of record. A kind is named as api/dataChanged names what a write
 * changes, so a list is refreshed when, and only when, a write touched its kind.
 */
export const queryKeys = {
  leads: {
    page: (s: WorkspaceScope, params: object) => key(s, 'leads', 'page', params),
    all: (s: WorkspaceScope) => key(s, 'leads', 'all'),
    stats: (s: WorkspaceScope) => key(s, 'leads', 'stats'),
  },
  leadLookups: {
    statuses: (s: WorkspaceScope) => key(s, 'lead-lookups', 'statuses'),
    sources: (s: WorkspaceScope) => key(s, 'lead-lookups', 'sources'),
  },
  deals: {
    all: (s: WorkspaceScope) => key(s, 'deals', 'all'),
    page: (s: WorkspaceScope, params: object) => key(s, 'deals', 'page', params),
    board: (s: WorkspaceScope, query: object) => key(s, 'deals', 'board', query),
  },
  contacts: {
    page: (s: WorkspaceScope, params: object) => key(s, 'contacts', 'page', params),
    all: (s: WorkspaceScope) => key(s, 'contacts', 'all'),
    stats: (s: WorkspaceScope) => key(s, 'contacts', 'stats'),
  },
  companies: {
    page: (s: WorkspaceScope, params: object) => key(s, 'companies', 'page', params),
    all: (s: WorkspaceScope) => key(s, 'companies', 'all'),
    summary: (s: WorkspaceScope) => key(s, 'companies', 'summary'),
  },
  tasks: {
    list: (s: WorkspaceScope, options: object = {}) => key(s, 'tasks', 'all', options),
    stats: (s: WorkspaceScope, scope: WorkScope | 'all') => key(s, 'tasks', 'stats', scope),
  },
  activities: {
    page: (s: WorkspaceScope, params: object) => key(s, 'activities', 'page', params),
    forRecord: (s: WorkspaceScope, kind: 'lead' | 'deal' | 'contact', id: string) => key(s, 'activities', kind, id),
  },
  pipelines: {
    all: (s: WorkspaceScope) => key(s, 'pipelines', 'all'),
  },
  team: {
    members: (s: WorkspaceScope) => key(s, 'team', 'members'),
    salesTeams: (s: WorkspaceScope) => key(s, 'team', 'sales-teams'),
  },
  reports: {
    dashboard: (s: WorkspaceScope) => key(s, 'reports', 'dashboard'),
    byStage: (s: WorkspaceScope) => key(s, 'reports', 'by-stage'),
    byAssignee: (s: WorkspaceScope) => key(s, 'reports', 'by-assignee'),
    summary: (s: WorkspaceScope, filters: object) => key(s, 'reports', 'summary', filters),
    forecast: (s: WorkspaceScope, params: object) => key(s, 'reports', 'forecast', params),
  },
  sequences: {
    all: (s: WorkspaceScope) => key(s, 'sequences', 'all'),
    enrollments: (s: WorkspaceScope) => key(s, 'sequences', 'enrollments'),
  },
  automations: {
    all: (s: WorkspaceScope) => key(s, 'automations', 'all'),
  },
  duplicates: {
    notSame: (s: WorkspaceScope) => key(s, 'duplicates', 'not-same'),
  },
  workQueue: (s: WorkspaceScope, scope: WorkScope) => key(s, 'work-queue', scope),
  templates: {
    all: (s: WorkspaceScope) => key(s, 'templates', 'all'),
  },
  copyHistory: {
    stats: (s: WorkspaceScope) => key(s, 'copy-history', 'stats'),
  },
  // The club: its members, and its money on the ledger. A charge, a payment, a tab's close
  // or a bill moves a member's balance, so writes to those refresh the members list too
  // (api/dataChanged).
  club: {
    members: (s: WorkspaceScope) => key(s, 'members', 'all'),
    charges: (s: WorkspaceScope) => key(s, 'charges', 'all'),
    payments: (s: WorkspaceScope) => key(s, 'payments', 'all'),
    giftCards: (s: WorkspaceScope) => key(s, 'gift-cards', 'all'),
    redemptions: (s: WorkspaceScope) => key(s, 'gift-cards', 'redemptions'),
    tabs: (s: WorkspaceScope) => key(s, 'tabs', 'all'),
    subscriptions: (s: WorkspaceScope) => key(s, 'subscriptions', 'all'),
    donations: (s: WorkspaceScope) => key(s, 'donations', 'all'),
    loyalty: (s: WorkspaceScope) => key(s, 'loyalty', 'ledger'),
    visits: (s: WorkspaceScope) => key(s, 'visits', 'all'),
    statement: (s: WorkspaceScope, memberId: string, fromUtc: string, toUtc: string) => key(s, 'statements', memberId, fromUtc, toUtc),
    // The price catalogue (tiers, dues, fees, products, tickets, menu items), the settings
    // the server applies, and the billing runs and what billing now would post.
    catalog: (s: WorkspaceScope, kind: string | null) => key(s, 'catalog', kind ?? 'all'),
    settings: (s: WorkspaceScope) => key(s, 'hospitality', 'settings'),
    billingRuns: (s: WorkspaceScope) => key(s, 'billing', 'runs'),
    billingPreview: (s: WorkspaceScope) => key(s, 'billing', 'preview'),
    // Events and who is coming, the bookable resources and their bookings. A change to an
    // event's list can post or take back a ticket, so those writes refresh the money too
    // (api/dataChanged).
    events: (s: WorkspaceScope) => key(s, 'events', 'all'),
    event: (s: WorkspaceScope, id: string) => key(s, 'events', 'one', id),
    attendees: (s: WorkspaceScope, eventId: string) => key(s, 'events', 'attendees', eventId),
    memberRegistrations: (s: WorkspaceScope, memberId: string) => key(s, 'events', 'member', memberId),
    resources: (s: WorkspaceScope) => key(s, 'resources', 'all'),
    reservations: (s: WorkspaceScope, memberId: string | null = null) => key(s, 'reservations', memberId ?? 'all'),
    // Applications and households. An approval makes a member, their dues, fee, household
    // link and welcome, so it refreshes those too; what a member's, a lead's or a contact's
    // page shows of the other side is read under 'members', which applications, households
    // and members all refresh (api/dataChanged).
    applications: (s: WorkspaceScope) => key(s, 'applications', 'all'),
    applicationForm: (s: WorkspaceScope) => key(s, 'applications', 'form'),
    households: (s: WorkspaceScope) => key(s, 'households', 'all'),
    memberHousehold: (s: WorkspaceScope, memberId: string) => key(s, 'households', 'member', memberId),
    memberLinks: (s: WorkspaceScope, memberId: string) => key(s, 'members', 'links', memberId),
    personMembership: (s: WorkspaceScope, kind: 'lead' | 'contact', id: string) => key(s, 'members', 'membership', kind, id),
    // The club's figures, summed on the server from the ledger, the events and the members:
    // every write that moves money, a member, a booking or a check-in refreshes them
    // (api/dataChanged).
    report: (s: WorkspaceScope, range: object) => key(s, 'club-reports', 'report', range),
    today: (s: WorkspaceScope) => key(s, 'club-reports', 'today'),
  },
} as const;
