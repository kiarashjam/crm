/** Shared types for the CRM API (frontend mock; replace with real API later). */

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
}

export interface Lead {
  id: string;
  name: string;
  email: string;
  phone?: string;
  companyId?: string;
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
  tags?: string[];
  createdAtUtc?: string;
}

export interface Deal {
  id: string;
  name: string;
  value: string;
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
}

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
  updatedAt?: string;
  contactName?: string;
  dealName?: string;
  leadName?: string;
}

export interface Pipeline {
  id: string;
  organizationId: string;
  name: string;
  displayOrder: number;
  dealStages?: DealStage[];
}

export interface DealStage {
  id: string;
  pipelineId: string;
  name: string;
  displayOrder: number;
  isWon: boolean;
  isLost: boolean;
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

export interface DashboardStats {
  activeLeadsCount: number;
  activeDealsCount: number;
  pipelineValue: number;
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

  // Defaults
  defaultPipelineId?: string;
  defaultLeadStatusId?: string;
  defaultLeadSourceId?: string;
  defaultFollowUpDays: number;
  defaultCurrency?: string;

  // Privacy
  showActivityStatus: boolean;
  allowAnalytics: boolean;

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

  // Defaults
  defaultPipelineId?: string;
  defaultLeadStatusId?: string;
  defaultLeadSourceId?: string;
  defaultFollowUpDays?: number;
  defaultCurrency?: string;

  // Privacy
  showActivityStatus?: boolean;
  allowAnalytics?: boolean;
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
