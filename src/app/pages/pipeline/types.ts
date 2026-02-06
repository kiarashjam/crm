import type { Deal, Pipeline as PipelineType, DealStage, Contact, Company } from '@/app/api/types';

export interface DealFormState {
  name: string;
  value: string;
  stageId: string;
  pipelineId: string;
  contactId: string;
  companyId: string;
  expectedCloseDate: string;
  probability: string;
  description: string;
}

export const DEFAULT_DEAL_FORM: DealFormState = {
  name: '',
  value: '',
  stageId: '',
  pipelineId: '',
  contactId: '',
  companyId: '',
  expectedCloseDate: '',
  probability: '',
  description: '',
};

export type ViewMode = 'board' | 'list';

// Re-export
export type { Deal, PipelineType as Pipeline, DealStage, Contact, Company };
