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
    forLeads: (s: WorkspaceScope, leadIds: readonly string[]) => key(s, 'activities', 'leads', leadIds),
  },
  pipelines: {
    all: (s: WorkspaceScope) => key(s, 'pipelines', 'all'),
  },
  team: {
    members: (s: WorkspaceScope) => key(s, 'team', 'members'),
  },
  reports: {
    dashboard: (s: WorkspaceScope) => key(s, 'reports', 'dashboard'),
    byStage: (s: WorkspaceScope) => key(s, 'reports', 'by-stage'),
    byAssignee: (s: WorkspaceScope) => key(s, 'reports', 'by-assignee'),
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
} as const;
