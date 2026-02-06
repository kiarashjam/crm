import type { Lead, Company, LeadStatus, LeadSource, Contact, Deal, Pipeline, Activity as ActivityType, TaskItem } from '@/app/api/types';

// Step type for the multi-step wizard
export type WizardStep = 'contact' | 'company' | 'qualification' | 'notes';

// Lead form type
export type LeadForm = {
  name: string;
  email: string;
  phone: string;
  companyId: string;
  source: string;
  status: string;
  leadSourceId: string;
  leadStatusId: string;
  leadScore: string;
  description: string;
  lifecycleStage: string;
};

// Extended activity type with user info
export interface ActivityWithUser extends ActivityType {
  userName?: string;
  userEmail?: string;
}

// Lead Detail Modal Props
export interface LeadDetailModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  lead: Lead | null;
  companies: Company[];
  statusOptions: LeadStatus[];
  sourceOptions: LeadSource[];
  onEdit: (lead: Lead) => void;
  onConvert: (lead: Lead) => void;
  onDelete: (lead: Lead) => void;
  onUpdate: (lead: Lead) => void;
  orgMembers: { userId: string; name: string; email: string }[];
  currentUser?: { id: string; name: string; email: string } | null;
}

// Add Lead Dialog Props
export interface AddLeadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editingLead: Lead | null;
  form: LeadForm;
  setForm: React.Dispatch<React.SetStateAction<LeadForm>>;
  companies: Company[];
  sourceOptions: { id: string; name: string }[];
  statusOptions: { id: string; name: string }[];
  onSubmit: (e: React.FormEvent) => void;
  saving: boolean;
}

// Convert Lead Dialog Props
export interface ConvertLeadDialogProps {
  lead: Lead | null;
  onClose: () => void;
  convertForm: ConvertFormState;
  setConvertForm: React.Dispatch<React.SetStateAction<ConvertFormState>>;
  convertOptions: ConvertOptions;
  convertOptionsLoading: boolean;
  converting: boolean;
  onConvert: (e: React.FormEvent) => void;
}

export interface ConvertFormState {
  createContact: boolean;
  createDeal: boolean;
  dealName: string;
  dealValue: string;
  dealStage: string;
  pipelineId?: string;
  dealStageId?: string;
  createNewCompany: boolean;
  newCompanyName: string;
  existingCompanyId?: string;
  existingContactId?: string;
  existingDealId?: string;
}

export interface ConvertOptions {
  contacts: Contact[];
  deals: Deal[];
  pipelines: Pipeline[];
}

// Lead stats type
export interface LeadStats {
  total: number;
  newLeads: number;
  contacted: number;
  qualified: number;
  hotLeads: number;
  conversionRate: number;
}

// Re-export commonly used types
export type { Lead, Company, Contact, Deal, LeadStatus, LeadSource, Pipeline, TaskItem };
