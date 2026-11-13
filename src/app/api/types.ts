import type { MoneyRow, WeightedRow } from './reporting';

/** Shared types for the CRM API (frontend mock; replace with real API later). */

import type { ContactRelationship } from './relationships';

// ============================================================================
// Pagination Types
// ============================================================================

/**
 * Generic paginated response from the API.
 */
export interface PagedResult<T> {
  /** Items for the current page */
  items: T[];
  /** Total number of items matching the query (across all pages) */
  totalCount: number;
  /** Current page number (1-based) */
  page: number;
  /** Number of items per page */
  pageSize: number;
  /** Total number of pages */
  totalPages: number;
  /** Whether there is a next page */
  hasNextPage: boolean;
  /** Whether there is a previous page */
  hasPreviousPage: boolean;
}

/**
 * Parameters for paginated requests.
 */
export interface PaginationParams {
  /** Page number (1-based). Defaults to 1. */
  page?: number;
  /** Number of items per page. Defaults to 20, max 100. */
  pageSize?: number;
  /** Optional search query. */
  search?: string;
}

// ============================================================================
// Core Types
// ============================================================================

export type CopyTypeId = 'sales-email' | 'follow-up' | 'crm-note' | 'deal-message' | 'workflow-message';

export interface Contact {
  id: string;
  name: string;
  email: string;
  phone?: string;
  jobTitle?: string;
  companyId?: string;
  lastActivityAtUtc?: string;
  convertedFromLeadId?: string;
  convertedAtUtc?: string;
  isArchived?: boolean;
  doNotContact?: boolean;
  preferredContactMethod?: string;
  companyName?: string;
  description?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  /**
   * The record as of this read. An edit dialog sends it back, and the server refuses the
   * save (409) when someone has saved the record since. See lib/editConflict.ts.
   */
  version?: string;
}

export interface Company {
  id: string;
  name: string;
  domain?: string;
  industry?: string;
  size?: string;
  description?: string;
  website?: string;
  location?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  /**
   * The record as of this read. An edit dialog sends it back, and the server refuses the
   * save (409) when someone has saved the record since. See lib/editConflict.ts.
   */
  version?: string;
}

export interface Lead {
  id: string;
  name: string;
  email: string;
  phone?: string;
  referredByContactId?: string;
  referredByContactName?: string;
  /** The lead's spouse or partner and family, spouse first. Filled on list reads. */
  relationships?: ContactRelationship[];
  companyId?: string;
  companyName?: string;
  source?: string;
  status: string;
  leadSourceId?: string;
  leadStatusId?: string;
  leadScore?: number;
  lastContactedAt?: string;
  description?: string;
  lifecycleStage?: string;
  isConverted?: boolean;
  convertedAtUtc?: string;
  assignedToId?: string;
  /** Display name of the assigned user, resolved server-side. */
  assignedToName?: string;
  /** Serialized JSON of the structured sales-lifecycle progression (see leadPipeline.ts). */
  pipelineState?: string;
  tags?: string[];
  createdAtUtc?: string;
  jobTitle?: string;
  country?: string;
  industry?: string;
  companySize?: string;
  firstResponseAtUtc?: string;
  firstResponseDueAtUtc?: string;
  slaBreached?: boolean;
  queueId?: string;
  territoryId?: string;
  emailOpenCount?: number;
  enrichedDomain?: string;
  scoreIsManual?: boolean;
  /**
   * The record as of this read. An edit dialog sends it back, and the server refuses the
   * save (409) when someone has saved the record since. See lib/editConflict.ts.
   */
  version?: string;
}

export interface Deal {
  id: string;
  name: string;
  /** The amount as text, kept for older servers and integrations. Read `amount`. */
  value: string;
  /**
   * The amount, to the cent, in `currency`. Null only for a deal saved before amounts
   * were numbers whose text held none ("to be agreed").
   */
  amount?: number | null;
  currency?: string;
  stage?: string;
  pipelineId?: string;
  dealStageId?: string;
  companyId?: string;
  contactId?: string;
  assigneeId?: string;
  expectedCloseDateUtc?: string;
  isWon?: boolean;
  lastActivityAtUtc?: string;
  // HP-1: Description & Probability
  description?: string;
  probability?: number;
  // HP-4: Enriched names & timestamps
  assigneeName?: string;
  companyName?: string;
  contactName?: string;
  pipelineName?: string;
  dealStageName?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  // HP-8: Close reason
  closedReason?: string;
  closedReasonCategory?: string;
  closedAtUtc?: string;
  /** Seen only by its creator, its owner and managers. */
  isPrivate?: boolean;
  /**
   * When the deal came into the stage it is in. Absent when no history says so (a deal
   * from before the history was kept), or where the server does not work it out.
   */
  stageEnteredAtUtc?: string;
  /** The last time anything happened to the deal: made, worked on, or moved. */
  idleSinceUtc?: string;
  /** Open, and nothing logged on it nor moved for `staleAfterDays`. */
  isStale?: boolean;
  /** Days without activity after which this deal goes stale; 0 never. */
  staleAfterDays?: number;
  /**
   * The record as of this read. An edit dialog sends it back, and the server refuses the
   * save (409) when someone has saved the record since. See lib/editConflict.ts.
   */
  version?: string;
}

/** A deal coming into a stage, as its history shows it. Names are as the stages were called then. */
export interface DealStageChange {
  id: string;
  fromStageId?: string;
  fromStageName?: string;
  toStageId?: string;
  toStageName?: string;
  changedByUserId: string;
  changedByName?: string;
  changedAtUtc: string;
  /** When it moved on from that stage; absent while it is still there. */
  leftAtUtc?: string;
  /** The stage it was created in, rather than moved to. */
  isCreation: boolean;
}

/** One stage on the board, with the cards the filters leave in it, a page at a time. */
export interface DealBoardColumn {
  /** What to ask for more of it by: the stage id, a default stage's name, or "unset". */
  key: string;
  dealStageId?: string;
  name: string;
  displayOrder: number;
  isWon: boolean;
  isLost: boolean;
  /** The stage's default probability, for its deals with none of their own. */
  probability?: number;
  count: number;
  totals: MoneyRow[];
  weighted: WeightedRow[];
  stale: number;
  deals: Deal[];
  hasMore: boolean;
}

/** The pipeline's figures, all of its deals, whatever the filters. */
export interface DealBoardSummary {
  total: number;
  open: number;
  won: number;
  lost: number;
  openTotals: MoneyRow[];
  wonTotals: MoneyRow[];
  weighted: WeightedRow[];
  /** Won out of all closed, 0–1; absent before anything has closed. */
  winRate?: number;
  overdue: number;
  closingThisWeek: number;
  stale: number;
}

/** A pipeline as the board draws it. */
export interface DealBoard {
  /** Absent for a workspace without pipelines, which has the default stages. */
  pipelineId?: string;
  pipelineName?: string;
  /** 0 when deals are never flagged as stale. */
  staleAfterDays: number;
  columns: DealBoardColumn[];
  summary: DealBoardSummary;
  /** The deals the filters leave, across the columns. */
  count: number;
}

/** What the board shows: everything left out narrows nothing. */
export interface DealBoardQuery {
  pipelineId?: string;
  take?: number;
  search?: string;
  assigneeId?: string;
  unassigned?: boolean;
  mine?: boolean;
  minAmount?: number;
  maxAmount?: number;
  /** yyyy-MM-dd */
  closeFrom?: string;
  /** yyyy-MM-dd */
  closeTo?: string;
  staleOnly?: boolean;
  sort?: 'close' | 'value' | 'created' | 'name';
}

export type TaskStatusType = 'todo' | 'in_progress' | 'completed' | 'cancelled';
export type TaskPriorityType = 'none' | 'low' | 'medium' | 'high';

export interface TaskItem {
  id: string;
  title: string;
  description?: string;
  dueDateUtc?: string;
  reminderDateUtc?: string;
  status: TaskStatusType;
  priority: TaskPriorityType;
  completed: boolean;  // Legacy field
  leadId?: string;
  dealId?: string;
  contactId?: string;
  assigneeId?: string;
  assigneeName?: string;
  leadName?: string;
  dealName?: string;
  contactName?: string;
  notes?: string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  completedAtUtc?: string;
  /** Completing a repeating task opens the next one, this far after its due date. */
  recurrence?: TaskRecurrence;
}

export type TaskRecurrence = 'daily' | 'weekly' | 'monthly';

export interface TaskStats {
  total: number;
  todo: number;
  inProgress: number;
  completed: number;
  cancelled: number;
  overdue: number;
  dueToday: number;
  highPriority: number;
}

export interface Activity {
  id: string;
  type: string;
  subject?: string;
  body?: string;
  contactId?: string;
  dealId?: string;
  leadId?: string;
  participants?: string;
  createdAt: string;
  /** When it happened, which can be before it was logged (createdAt). See lib/activityFacts. */
  occurredAt?: string;
  /** How a call or meeting went, e.g. "no_answer". */
  outcome?: string;
  durationMinutes?: number;
  /** Who reached out, for a call or an email. */
  direction?: 'inbound' | 'outbound';
  updatedAt?: string;
  contactName?: string;
  dealName?: string;
  leadName?: string;
  createdByUserId?: string;
  createdByName?: string;
  createdByEmail?: string;
}

export interface Pipeline {
  id: string;
  organizationId: string;
  name: string;
  displayOrder: number;
  dealStages?: DealStage[];
  /** Its own stale-deal days (0 never); absent when it follows the workspace's. */
  staleDealDays?: number;
}

export interface DealStage {
  id: string;
  pipelineId: string;
  name: string;
  displayOrder: number;
  isWon: boolean;
  isLost: boolean;
  /** The chance, 0–100, a deal here is won, for its deals with no probability of their own. */
  probability?: number;
}

export interface LeadStatus {
  id: string;
  organizationId: string;
  name: string;
  displayOrder: number;
}

export interface LeadSource {
  id: string;
  organizationId: string;
  name: string;
  displayOrder: number;
}

/** One currency's share of a total. Listed beside the others, never converted. */
export interface CurrencyTotal {
  currency: string;
  value: number;
  dealCount: number;
}

export interface DashboardStats {
  activeLeadsCount: number;
  activeDealsCount: number;
  /** Open pipeline in `pipelineCurrency` ONLY, never a cross-currency sum. */
  pipelineValue: number;
  pipelineCurrency: string;
  /** Every currency present in the open pipeline, largest first. */
  pipelineByCurrency: CurrencyTotal[];
  /** Open deals whose value column holds no readable number. */
  unreadableValueCount: number;
  dealsWonCount: number;
  dealsLostCount: number;
}

export interface Template {
  id: string;
  title: string;
  description: string;
  category: string;
  copyTypeId: CopyTypeId;
  goal: string;
  useCount: number;
}

export interface CopyHistoryItem {
  id: string;
  type: string;
  copy: string;
  recipientName: string;
  recipientType: 'contact' | 'deal' | 'lead' | 'workflow' | 'email';
  recipientId: string;
  createdAt: string; // ISO
}

export interface GenerateCopyParams {
  copyTypeId: CopyTypeId;
  goal: string;
  context?: string;
  length: 'short' | 'medium' | 'long';
  brandName?: string;
  brandTone?: string;
}

export interface SendToCrmParams {
  objectType: 'contact' | 'deal' | 'lead' | 'workflow' | 'email';
  recordId: string;
  recordName: string;
  copy: string;
  copyTypeLabel: string;
}

export type ThemeType = 'light' | 'dark' | 'system';
export type DataDensityType = 'comfortable' | 'compact' | 'spacious';
export type BrandToneType = 'professional' | 'friendly' | 'persuasive';

export interface UserSettings {
  // Profile
  brandName: string;
  jobTitle?: string;
  avatarUrl?: string;
  phone?: string;
  timezone: string;
  language: string;
  bio?: string;

  // Brand
  brandTone: BrandToneType;
  emailSignature?: string;
  defaultEmailSubjectPrefix?: string;

  // Appearance
  theme: ThemeType;
  dataDensity: DataDensityType;
  sidebarCollapsed: boolean;
  showWelcomeBanner: boolean;

  // Notifications
  emailNotificationsEnabled: boolean;
  emailOnNewLead: boolean;
  emailOnDealUpdate: boolean;
  emailOnTaskDue: boolean;
  emailOnTeamMention: boolean;
  inAppNotificationsEnabled: boolean;
  inAppSoundEnabled: boolean;
  browserNotificationsEnabled: boolean;
  /** A weekly email of the figures for each workspace this person owns or manages. Off by default. */
  weeklyDigestEnabled?: boolean;
  /** A morning brief of the day in each workspace, as a notification. On unless turned off. */
  morningBriefEnabled?: boolean;
  /** The morning brief by email as well (through the outbox). Off unless turned on. */
  morningBriefEmail?: boolean;

  // Defaults
  defaultPipelineId?: string;
  defaultLeadStatusId?: string;
  defaultLeadSourceId?: string;
  defaultFollowUpDays: number;
  defaultCurrency?: string;

  // Privacy
  showActivityStatus: boolean;
  allowAnalytics: boolean;

  /** Last agent turns for this signed-in user. */
  agentMemoryJson?: string;
  /** How this person likes each list page shown (see lib/viewPreferences). */
  viewPreferencesJson?: string;

  // Metadata
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpdateUserSettingsRequest {
  // Profile
  brandName?: string;
  jobTitle?: string;
  avatarUrl?: string;
  phone?: string;
  timezone?: string;
  language?: string;
  bio?: string;

  // Brand
  brandTone?: BrandToneType;
  emailSignature?: string;
  defaultEmailSubjectPrefix?: string;

  // Appearance
  theme?: ThemeType;
  dataDensity?: DataDensityType;
  sidebarCollapsed?: boolean;
  showWelcomeBanner?: boolean;

  // Notifications
  emailNotificationsEnabled?: boolean;
  emailOnNewLead?: boolean;
  emailOnDealUpdate?: boolean;
  emailOnTaskDue?: boolean;
  emailOnTeamMention?: boolean;
  inAppNotificationsEnabled?: boolean;
  inAppSoundEnabled?: boolean;
  browserNotificationsEnabled?: boolean;
  weeklyDigestEnabled?: boolean;
  morningBriefEnabled?: boolean;
  morningBriefEmail?: boolean;

  // Defaults
  defaultPipelineId?: string;
  defaultLeadStatusId?: string;
  defaultLeadSourceId?: string;
  defaultFollowUpDays?: number;
  defaultCurrency?: string;

  // Privacy
  showActivityStatus?: boolean;
  allowAnalytics?: boolean;
  agentMemoryJson?: string;
  viewPreferencesJson?: string;
}

export interface TimezoneOption {
  id: string;
  displayName: string;
  utcOffset: number;
}

export interface CurrencyOption {
  code: string;
  name: string;
  symbol: string;
}

export interface LanguageOption {
  code: string;
  name: string;
  nativeName: string;
}
