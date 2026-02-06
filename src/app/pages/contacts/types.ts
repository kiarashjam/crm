import type { Contact, Company, Activity } from '@/app/api/types';

export interface ContactFormState {
  name: string;
  email: string;
  phone: string;
  companyId: string;
  jobTitle: string;
  description: string;
}

export const DEFAULT_CONTACT_FORM: ContactFormState = {
  name: '',
  email: '',
  phone: '',
  companyId: '',
  jobTitle: '',
  description: '',
};

export type SortField = 'name' | 'email' | 'company' | 'createdAt';
export type SortDirection = 'asc' | 'desc';

// Re-export
export type { Contact, Company, Activity };
