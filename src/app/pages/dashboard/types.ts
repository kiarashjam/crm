import type { CopyTypeId, Lead, Contact, Deal } from '@/app/api/types';
import type { SupportedLanguage } from '@/app/api/copyGenerator';

// Re-exported from the API layer rather than restated. The local copy had drifted
// into a narrower shape that omitted currency, which is how the hero came to print
// a cross-currency sum with a dollar sign in front of it.
export type { DashboardStats, CurrencyTotal } from '@/app/api/types';

export interface CopyStats {
  sentThisWeek: number;
  totalSent: number;
  /** Null until the template list has actually been read. Never a placeholder. */
  templateCount: number | null;
}

export type { PipelineStageValue as PipelineStage } from '@/app/api/reporting';

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
