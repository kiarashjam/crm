/** Shared types for the CRM API (frontend mock; replace with real API later). */

export type CopyTypeId = 'sales-email' | 'follow-up' | 'crm-note' | 'deal-message' | 'workflow-message';

export interface Contact {
  id: string;
  name: string;
  email: string;
  companyId?: string;
}

export interface Company {
  id: string;
  name: string;
}

export interface Deal {
  id: string;
  name: string;
  value: string;
  stage?: string;
  companyId?: string;
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
