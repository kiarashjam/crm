import type { Activity, Contact, Deal, Lead } from '@/app/api/types';

export type ActivityFilter = 'all' | 'contact' | 'deal' | 'lead';

export interface ActivityFormState {
  type: string;
  subject: string;
  body: string;
  contactId: string;
  dealId: string;
  participants: string;
}

export const DEFAULT_ACTIVITY_FORM: ActivityFormState = {
  type: 'note',
  subject: '',
  body: '',
  contactId: '',
  dealId: '',
  participants: '',
};

// Re-export
export type { Activity, Contact, Deal, Lead };
