export const JOB_TITLES = [
  'CEO',
  'CTO',
  'CFO',
  'COO',
  'VP of Sales',
  'VP of Marketing',
  'Director',
  'Manager',
  'Engineer',
  'Developer',
  'Designer',
  'Analyst',
  'Consultant',
  'Other',
] as const;

export type JobTitle = typeof JOB_TITLES[number];
