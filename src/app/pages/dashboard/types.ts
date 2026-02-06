import type { CopyTypeId, Lead, Contact, Deal } from '@/app/api/types';
import type { SupportedLanguage } from '@/app/api/copyGenerator';

export interface DashboardStats {
  activeLeadsCount: number;
  activeDealsCount: number;
  pipelineValue: number;
  dealsWonCount: number;
  dealsLostCount: number;
}

export interface CopyStats {
  sentThisWeek: number;
  totalSent: number;
  templateCount: number;
}

export interface PipelineStage {
  stageId: string;
  stageName: string;
  dealCount: number;
  value: number;
}

export interface SelectedRecipient {
  type: 'lead' | 'contact' | 'deal';
  id: string;
  name: string;
  email?: string;
  company?: string;
  dealStage?: string;
  dealValue?: string;
}

export interface SalesWriterState {
  selectedType: CopyTypeId | '';
  goal: string;
  context: string;
  length: 'short' | 'medium' | 'long';
  language: SupportedLanguage;
  isGenerating: boolean;
  showRecipientPicker: boolean;
  recipientType: 'lead' | 'contact' | 'deal' | null;
  selectedRecipient: SelectedRecipient | null;
  recipientSearch: string;
}

// Re-export for convenience
export type { CopyTypeId, Lead, Contact, Deal, SupportedLanguage };
