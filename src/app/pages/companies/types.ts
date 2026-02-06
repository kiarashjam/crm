import type { Company, Contact, Deal } from '@/app/api/types';

export interface CompanyFormState {
  name: string;
  industry: string;
  website: string;
  phone: string;
  address: string;
  description: string;
}

export const DEFAULT_COMPANY_FORM: CompanyFormState = {
  name: '',
  industry: '',
  website: '',
  phone: '',
  address: '',
  description: '',
};

export type SortField = 'name' | 'industry' | 'createdAt';
export type SortDirection = 'asc' | 'desc';

// Re-export
export type { Company, Contact, Deal };
