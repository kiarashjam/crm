// Importing a spreadsheet of leads, contacts or companies.
//
// The server does all of it: it reads the file (the ";" Swiss Excel writes, quoted
// cells with line breaks, Windows-1252), suggests a field for each column, says what
// every row would do before anything is written, imports the file as one batch, and
// can undo that batch. This module only carries the file there and back. The file
// travels as its bytes, so the server can tell how they are encoded; the browser would
// read a Windows-1252 "Zürich" as "Z?rich".

import { authFetchJson } from './apiClient';

export type ImportKind = 'lead' | 'contact' | 'company';

/** A field a column can go into. Custom fields have keys starting "custom:". */
export interface ImportField {
  key: string;
  label: string;
  /** Needed to add a record. A person's name can come from "name" or from its parts. */
  required: boolean;
  custom: boolean;
  hint?: string | null;
}

export interface ImportColumn {
  index: number;
  header: string;
  /** The first few values in the column. */
  samples: string[];
  /** The field the header suggests, or null. */
  field: string | null;
}

export interface ImportAnalysis {
  kind: ImportKind;
  separator: 'semicolon' | 'comma' | 'tab';
  encoding: string;
  rowCount: number;
  columns: ImportColumn[];
  fields: ImportField[];
}

export type ImportAction = 'create' | 'update' | 'skip' | 'error';

export interface ImportRow {
  /** The row's number in the spreadsheet, the header being row 1. */
  row: number;
  action: ImportAction;
  label: string;
  message?: string | null;
  recordId?: string | null;
  /** For an update: "Phone: “079…” → “+41…”". */
  changes: string[];
  notes: string[];
}

export interface ImportResult {
  /** The batch to undo it by; null for a dry run. */
  batchId: string | null;
  dryRun: boolean;
  kind: ImportKind;
  totalRows: number;
  created: number;
  updated: number;
  skipped: number;
  failed: number;
  rows: ImportRow[];
}

export interface ImportBatch {
  id: string;
  kind: ImportKind;
  fileName: string;
  importedBy: string;
  startedAtUtc: string;
  finishedAtUtc: string | null;
  duplicates: 'skip' | 'update';
  ranAutomations: boolean;
  totalRows: number;
  created: number;
  updated: number;
  skipped: number;
  failed: number;
  undoneAtUtc: string | null;
  removed: number;
  /** Records it created that are still here. */
  remaining: number;
  canUndo: boolean;
}

export interface ImportKept {
  kind: ImportKind;
  id: string;
  label: string;
  reason: string;
}

export interface ImportUndo {
  batchId: string;
  dryRun: boolean;
  leads: number;
  contacts: number;
  companies: number;
  kept: ImportKept[];
}

/** A file read for sending: its name and its bytes in base64. */
export interface ImportFile {
  fileName: string;
  content: string;
}

export interface ImportOptions {
  /** The field for each column of the file, in order; null leaves the column out. */
  columns: (string | null)[];
  /** What to do with a row matching a record already here. */
  duplicates: 'skip' | 'update';
  /** Leads only. */
  runAutomations?: boolean;
  defaultStatus?: string;
  defaultSource?: string;
}

/** The file's bytes in base64, in pieces so a large file does not overflow the call stack. */
export async function readImportFile(file: File): Promise<ImportFile> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  let binary = '';
  const piece = 0x8000;
  for (let i = 0; i < bytes.length; i += piece) {
    binary += String.fromCharCode(...bytes.subarray(i, i + piece));
  }
  return { fileName: file.name, content: btoa(binary) };
}

export function analyzeImport(kind: ImportKind, file: ImportFile): Promise<ImportAnalysis> {
  return authFetchJson<ImportAnalysis>('/api/imports/analyze', {
    method: 'POST',
    body: JSON.stringify({ kind, ...file }),
  });
}

/** A dry run: what each row would do. Nothing is written. */
export function previewImport(kind: ImportKind, file: ImportFile, options: ImportOptions): Promise<ImportResult> {
  return authFetchJson<ImportResult>('/api/imports/preview', {
    method: 'POST',
    body: JSON.stringify({ kind, ...file, ...options }),
  });
}

export function runImport(kind: ImportKind, file: ImportFile, options: ImportOptions): Promise<ImportResult> {
  return authFetchJson<ImportResult>('/api/imports', {
    method: 'POST',
    body: JSON.stringify({ kind, ...file, ...options }),
  });
}

export async function listImports(kind: ImportKind): Promise<ImportBatch[]> {
  return (await authFetchJson<ImportBatch[]>(`/api/imports?kind=${kind}`)) ?? [];
}

/** What undoing would remove, and what it keeps and why. Nothing is removed. */
export function previewUndo(batchId: string): Promise<ImportUndo> {
  return authFetchJson<ImportUndo>(`/api/imports/${encodeURIComponent(batchId)}/undo`);
}

export function undoImport(batchId: string): Promise<ImportUndo> {
  return authFetchJson<ImportUndo>(`/api/imports/${encodeURIComponent(batchId)}/undo`, { method: 'POST' });
}
