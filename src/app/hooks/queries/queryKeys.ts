/**
 * Centralized query keys for TanStack Query
 * Using factory pattern for type-safe, consistent keys
 */

export const queryKeys = {
  // Leads
  leads: {
    all: ['leads'] as const,
    lists: () => [...queryKeys.leads.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      [...queryKeys.leads.lists(), filters] as const,
    details: () => [...queryKeys.leads.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.leads.details(), id] as const,
  },

  // Companies
  companies: {
    all: ['companies'] as const,
    lists: () => [...queryKeys.companies.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      [...queryKeys.companies.lists(), filters] as const,
    details: () => [...queryKeys.companies.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.companies.details(), id] as const,
  },

  // Contacts
  contacts: {
    all: ['contacts'] as const,
    lists: () => [...queryKeys.contacts.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      [...queryKeys.contacts.lists(), filters] as const,
    details: () => [...queryKeys.contacts.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.contacts.details(), id] as const,
  },

  // Deals
  deals: {
    all: ['deals'] as const,
    lists: () => [...queryKeys.deals.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      [...queryKeys.deals.lists(), filters] as const,
    details: () => [...queryKeys.deals.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.deals.details(), id] as const,
  },

  // Tasks
  tasks: {
    all: ['tasks'] as const,
    lists: () => [...queryKeys.tasks.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      [...queryKeys.tasks.lists(), filters] as const,
    details: () => [...queryKeys.tasks.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.tasks.details(), id] as const,
  },

  // Activities
  activities: {
    all: ['activities'] as const,
    lists: () => [...queryKeys.activities.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      [...queryKeys.activities.lists(), filters] as const,
    details: () => [...queryKeys.activities.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.activities.details(), id] as const,
  },

  // Templates
  templates: {
    all: ['templates'] as const,
    lists: () => [...queryKeys.templates.all, 'list'] as const,
    list: (filters?: Record<string, unknown>) => 
      [...queryKeys.templates.lists(), filters] as const,
    details: () => [...queryKeys.templates.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.templates.details(), id] as const,
  },

  // Pipelines
  pipelines: {
    all: ['pipelines'] as const,
    lists: () => [...queryKeys.pipelines.all, 'list'] as const,
    details: () => [...queryKeys.pipelines.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.pipelines.details(), id] as const,
  },

  // Deal Stages
  dealStages: {
    all: ['dealStages'] as const,
    lists: () => [...queryKeys.dealStages.all, 'list'] as const,
    byPipeline: (pipelineId: string) => 
      [...queryKeys.dealStages.lists(), pipelineId] as const,
  },

  // Team / Organization Members
  team: {
    all: ['team'] as const,
    members: () => [...queryKeys.team.all, 'members'] as const,
    invites: () => [...queryKeys.team.all, 'invites'] as const,
  },

  // User Settings
  settings: {
    all: ['settings'] as const,
    user: () => [...queryKeys.settings.all, 'user'] as const,
  },

  // Copy History
  copyHistory: {
    all: ['copyHistory'] as const,
    lists: () => [...queryKeys.copyHistory.all, 'list'] as const,
  },

  // Reporting / Analytics
  reporting: {
    all: ['reporting'] as const,
    dashboard: () => [...queryKeys.reporting.all, 'dashboard'] as const,
    pipeline: () => [...queryKeys.reporting.all, 'pipeline'] as const,
  },

  // Organizations
  organizations: {
    all: ['organizations'] as const,
    lists: () => [...queryKeys.organizations.all, 'list'] as const,
    current: () => [...queryKeys.organizations.all, 'current'] as const,
  },
} as const;
