import type { Activity, Contact, Deal } from '@/app/api/types';

export type ActivityFilter = 'all' | 'contact' | 'deal';

export interface ActivityFormState {
  type: string;
  subject: string;
  body: string;
  contactId: string;
  dealId: string;
}

export const DEFAULT_ACTIVITY_FORM: ActivityFormState = {
  type: 'note',
  subject: '',
  body: '',
  contactId: '',
  dealId: '',
};

// Re-export
export type { Activity, Contact, Deal };
