export type CSVRow = Record<string, string>;

export type ColumnMapping = {
  name: string;
  email: string;
  phone: string;
  source: string;
  status: string;
  company: string;
};

export type ImportResult = {
  success: number;
  failed: number;
  errors: { row: number; name: string; error: string }[];
};

export type ImportStep = 'upload' | 'mapping' | 'preview' | 'importing' | 'complete';

export const REQUIRED_FIELDS = ['name', 'email'] as const;
export const OPTIONAL_FIELDS = ['phone', 'source', 'status', 'company'] as const;
export const ALL_FIELDS = [...REQUIRED_FIELDS, ...OPTIONAL_FIELDS] as const;
