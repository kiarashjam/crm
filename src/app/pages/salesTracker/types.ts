// Data model mirroring the P46 Sales Tracker Excel workbook.
//
// String-valued enums match the exact literals used in the source spreadsheet
// so the Excel COUNTIF formulas translate 1:1 to our computed selectors.

export type OutreachStatus =
  | 'Contacted'
  | 'Attempted – No Answer'
  | 'Not Interested'
  | '';

export type MeetingScheduled =
  | 'Yes'
  | 'No'
  | 'Meeting to be scheduled'
  | 'Not interested in meeting'
  | '';

export type YesNoPending = 'Yes' | 'No' | 'Pending' | '';

export type ContractSent =
  | 'Yes'
  | 'No'
  | 'Pending'
  | 'To be sent'
  | 'Rejected'
  | 'No longer interested'
  | 'Not interested'
  | '';

export type ContractSigned = 'Yes' | 'No' | 'Pending' | '';

export type YesNo = 'Yes' | 'No' | '';

export const OUTREACH_STATUS_OPTIONS: OutreachStatus[] = [
  '',
  'Contacted',
  'Attempted – No Answer',
  'Not Interested',
];

export const MEETING_SCHEDULED_OPTIONS: MeetingScheduled[] = [
  '',
  'Yes',
  'No',
  'Meeting to be scheduled',
  'Not interested in meeting',
];

export const YES_NO_PENDING_OPTIONS: YesNoPending[] = ['', 'Yes', 'No', 'Pending'];

export const CONTRACT_SENT_OPTIONS: ContractSent[] = [
  '',
  'Yes',
  'No',
  'Pending',
  'To be sent',
  'Rejected',
  'No longer interested',
  'Not interested',
];

export const CONTRACT_SIGNED_OPTIONS: ContractSigned[] = ['', 'Yes', 'No', 'Pending'];

export const YES_NO_OPTIONS: YesNo[] = ['', 'Yes', 'No'];
