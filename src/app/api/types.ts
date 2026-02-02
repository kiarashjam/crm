/** Shared types for the CRM API (frontend mock; replace with real API later). */

export type CopyTypeId = 'sales-email' | 'follow-up' | 'crm-note' | 'deal-message' | 'workflow-message';

export interface Contact {
  id: string;
  name: string;
  email: string;
  phone?: string;
  companyId?: string;
  lastActivityAtUtc?: string;
}

export interface Company {
  id: string;
  name: string;
}

export interface Lead {
  id: string;
  name: string;
  email: string;
  phone?: string;
  companyId?: string;
  source?: string;
  status: string;
}

export interface Deal {
  id: string;
  name: string;
  value: string;
  stage?: string;
  companyId?: string;
  contactId?: string;
  expectedCloseDateUtc?: string;
  isWon?: boolean;
  lastActivityAtUtc?: string;
}

export interface TaskItem {
  id: string;
  title: string;
  description?: string;
  dueDateUtc?: string;
  completed: boolean;
  leadId?: string;
  dealId?: string;
}

export interface Activity {
  id: string;
  type: string;
  subject?: string;
  body?: string;
  contactId?: string;
  dealId?: string;
  createdAt: string;
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
  recipientType: 'contact' | 'deal' | 'workflow' | 'email';
  recipientId: string;
  createdAt: string; // ISO
}

export interface GenerateCopyParams {
  copyTypeId: CopyTypeId;
  goal: string;
  context?: string;
  length: 'short' | 'medium' | 'long';
  companyName?: string;
  brandTone?: string;
}

export interface SendToCrmParams {
  objectType: 'contact' | 'deal' | 'workflow' | 'email';
  recordId: string;
  recordName: string;
  copy: string;
  copyTypeLabel: string;
}

export interface ConnectionStatus {
  connected: boolean;
  accountEmail?: string;
}

export interface UserSettings {
  companyName: string;
  brandTone: 'professional' | 'friendly' | 'persuasive';
}
