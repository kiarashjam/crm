export const INDUSTRIES = [
  'Technology',
  'Healthcare',
  'Finance',
  'Manufacturing',
  'Retail',
  'Education',
  'Real Estate',
  'Consulting',
  'Marketing',
  'Legal',
  'Transportation',
  'Energy',
  'Entertainment',
  'Hospitality',
  'Other',
] as const;

export type Industry = typeof INDUSTRIES[number];
