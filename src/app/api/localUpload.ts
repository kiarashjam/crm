// Custom fields and attachments saved in this browser before they moved to the server.
//
// Until the backend served them, both lived in localStorage: first under one key
// shared by every workspace in the browser, then, if the app updated before the
// server did, under a key scoped to the user and workspace. Once the server answers,
// the app reads only the server, so anything entered before would quietly drop out
// of view. This finds that data and, when the person agrees, uploads it once.
//
// What can't go yet stays in the browser for a later run: values on records that
// belong to another workspace, and fields only an owner or manager may add.

import { ApiError, authFetch } from './apiClient';
import { scopedStorageKey } from './mockStore';
import {
  DEMO_FIELD_DEFS, createFieldDefinition, getFieldDefinitions, saveFieldValues,
  type CustomFieldDef, type CustomFieldValues, type ValuesRow,
} from './customFields';
import { uploadAttachment, type Attachment } from './attachments';

export const LOCAL_KEYS = {
  fields: 'crm.mock.customFieldDefs.v1',
  values: 'crm.mock.customFieldValues.v1',
  attachments: 'crm.mock.attachments.v1',
} as const;

/** Where each key's original contents are kept before an upload changes anything. */
export const UPLOAD_BACKUP_SUFFIX = ':upload-backup';

export interface LocalUploadPlan {
  fields: CustomFieldDef[];
  records: ValuesRow[];
  files: Attachment[];
  /** Listed in the browser but never stored there: too large to keep. */
  unrecoverable: Attachment[];
}

export interface LocalUploadResult {
  fieldsAdded: number;
  fieldsMatched: number;
  recordsUploaded: number;
  filesUploaded: number;
  /** Records and files on records in another workspace: switch to it to upload them. */
  elsewhere: number;
  /** Fields only an owner or manager can add; their values wait in the browser until then. */
  needsManager: string[];
  failed: number;
  unrecoverable: string[];
}

function readList<T>(key: string): T[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(key) ?? 'null');
    return Array.isArray(parsed) ? (parsed as T[]) : [];
  } catch {
    return [];
  }
}

/** The keys this browser may hold a list under: the old shared one, and the current scope's. */
function sourcesOf(key: string): string[] {
  const scoped = scopedStorageKey(key);
  return scoped === key ? [key] : [key, scoped];
}

function readAll<T extends { id: string }>(key: string): T[] {
  const seen = new Set<string>();
  const rows: T[] = [];
  for (const source of sourcesOf(key)) {
    for (const row of readList<T>(source)) {
      if (!row || seen.has(row.id)) continue;
      seen.add(row.id);
      rows.push(row);
    }
  }
  return rows;
}

const filled = (v: unknown) => v !== null && v !== undefined && v !== '';
const fieldRef = (entityType: string, key: string) => `${entityType}:${key}`;

/** What this browser holds that the server doesn't have yet. */
export function findLocalUploads(): LocalUploadPlan {
  const records = readAll<ValuesRow>(LOCAL_KEYS.values)
    .filter((r) => r.values && Object.values(r.values).some(filled));
  const used = new Set(records.flatMap((r) =>
    Object.entries(r.values).filter(([, v]) => filled(v)).map(([k]) => fieldRef(r.entityType, k))));

  // Demo fields were seeded into real browsers too. They only count once someone used them.
  const demoIds = new Set(DEMO_FIELD_DEFS.map((d) => d.id));
  const fields = readAll<CustomFieldDef>(LOCAL_KEYS.fields)
    .filter((d) => !demoIds.has(d.id) || used.has(fieldRef(d.entityType, d.key)));
  // Scoping drops demo rows from the store, so a value can outlive its demo field.
  for (const demo of DEMO_FIELD_DEFS) {
    const ref = fieldRef(demo.entityType, demo.key);
    if (used.has(ref) && !fields.some((f) => fieldRef(f.entityType, f.key) === ref)) fields.push({ ...demo });
  }

  const allFiles = readAll<Attachment>(LOCAL_KEYS.attachments);
  return {
    fields,
    records,
    files: allFiles.filter((a) => !!a.dataUrl),
    unrecoverable: allFiles.filter((a) => !a.dataUrl),
  };
}

/**
 * Whether there is anything worth asking about. A Member can't add fields, so a
 * field with no values in it isn't theirs to upload, and isn't worth a prompt.
 */
export function hasLocalUploads(plan: LocalUploadPlan, canDefineFields: boolean): boolean {
  return plan.records.length > 0 || plan.files.length > 0 || (canDefineFields && plan.fields.length > 0);
}

/** How many steps an upload takes, for a progress count. */
export function uploadSteps(plan: LocalUploadPlan): number {
  return plan.fields.length + plan.records.length + plan.files.length;
}

/** A browser data URL back into a file. FileReader always wrote base64. */
export function dataUrlToFile(dataUrl: string, fileName: string, fallbackType: string): File {
  const [header, payload = ''] = dataUrl.split(',', 2);
  const type = /^data:([^;,]+)/.exec(header ?? '')?.[1] ?? fallbackType;
  const isBase64 = (header ?? '').includes(';base64');
  const text = isBase64 ? atob(payload) : decodeURIComponent(payload);
  const bytes = new Uint8Array(text.length);
  for (let i = 0; i < text.length; i++) bytes[i] = text.charCodeAt(i);
  return new File([bytes], fileName, { type });
}

/**
 * Whether the server serves both features yet. Asked directly rather than through
 * the API modules: those fall back to this browser when an endpoint is missing, and
 * an upload that quietly landed back in the browser would then be cleared as done.
 */
export async function serverAcceptsUploads(): Promise<boolean> {
  try {
    const [fields, files] = await Promise.all([
      authFetch('/api/custom-fields'),
      authFetch('/api/attachments?entityType=lead&recordId=00000000-0000-0000-0000-000000000000'),
    ]);
    return fields.ok && files.ok;
  } catch {
    return false;
  }
}

/** The server explaining that a record isn't in this workspace. */
const isElsewhere = (err: unknown) => err instanceof ApiError && err.status === 404;

const sameLabel = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

function backUpOnce(): void {
  for (const key of Object.values(LOCAL_KEYS)) {
    try {
      if (localStorage.getItem(key + UPLOAD_BACKUP_SUFFIX) !== null) continue;
      const original = Object.fromEntries(sourcesOf(key).map((source) => [source, localStorage.getItem(source)]));
      localStorage.setItem(key + UPLOAD_BACKUP_SUFFIX, JSON.stringify(original));
    } catch {
      // Storage full or unavailable: the upload still runs, and leftovers are still kept.
    }
  }
}

/** Write what is left back under the shared key, where any workspace's next run finds it. */
function keep<T>(key: string, rows: T[]): void {
  try {
    for (const source of sourcesOf(key)) localStorage.removeItem(source);
    if (rows.length > 0) localStorage.setItem(key, JSON.stringify(rows));
  } catch {
    // ignore: nothing was removed if storage refused the writes above
  }
}

/**
 * Upload the plan into the current workspace.
 *
 * Fields go first: each local field is matched to a server field with the same
 * label and type, or added if the person may add fields. Values are then sent
 * under the server's keys, which differ from the browser's, and files last.
 */
export async function uploadLocalData(
  plan: LocalUploadPlan,
  options: { canDefineFields: boolean; onProgress?: (done: number) => void },
): Promise<LocalUploadResult> {
  if (!(await serverAcceptsUploads())) throw new Error("The server isn't ready to take these yet. Nothing was changed.");
  backUpOnce();
  const result: LocalUploadResult = {
    fieldsAdded: 0, fieldsMatched: 0, recordsUploaded: 0, filesUploaded: 0,
    elsewhere: 0, needsManager: [], failed: 0, unrecoverable: plan.unrecoverable.map((a) => a.fileName),
  };
  let done = 0;
  const step = () => options.onProgress?.(++done);

  // Fields.
  const serverFields = [...(await getFieldDefinitions())];
  const serverKey = new Map<string, string>();
  const unplaced: CustomFieldDef[] = [];
  const used = new Set(plan.records.flatMap((r) => Object.keys(r.values).map((k) => fieldRef(r.entityType, k))));
  for (const field of plan.fields) {
    const ref = fieldRef(field.entityType, field.key);
    const match = serverFields.find((s) => s.entityType === field.entityType && s.type === field.type && sameLabel(s.label, field.label));
    if (match) {
      serverKey.set(ref, match.key);
      result.fieldsMatched++;
    } else if (!options.canDefineFields) {
      unplaced.push(field);
      if (used.has(ref)) result.needsManager.push(field.label);
    } else {
      try {
        const created = await createFieldDefinition({
          entityType: field.entityType, label: field.label, type: field.type,
          options: field.options, required: field.required,
        });
        if (created) {
          serverKey.set(ref, created.key);
          serverFields.push(created);
          result.fieldsAdded++;
        } else {
          unplaced.push(field);
          result.failed++;
        }
      } catch {
        unplaced.push(field);
        result.failed++;
      }
    }
    step();
  }

  // Values, under the server's keys.
  const leftRecords: ValuesRow[] = [];
  for (const row of plan.records) {
    const send: CustomFieldValues = {};
    const wait: CustomFieldValues = {};
    for (const [key, value] of Object.entries(row.values)) {
      if (!filled(value)) continue;
      const mapped = serverKey.get(fieldRef(row.entityType, key));
      if (mapped) send[mapped] = value;
      else wait[key] = value;
    }
    if (Object.keys(send).length > 0) {
      try {
        await saveFieldValues(row.entityType, row.recordId, send);
        result.recordsUploaded++;
      } catch (err) {
        if (isElsewhere(err)) result.elsewhere++;
        else result.failed++;
        leftRecords.push(row);
        step();
        continue;
      }
    }
    if (Object.keys(wait).length > 0) leftRecords.push({ ...row, values: wait });
    step();
  }

  // Files.
  const leftFiles: Attachment[] = [];
  for (const file of plan.files) {
    try {
      const uploaded = await uploadAttachment(file.entityType, file.recordId,
        dataUrlToFile(file.dataUrl!, file.fileName, file.contentType));
      if (uploaded) result.filesUploaded++;
      else { result.failed++; leftFiles.push(file); }
    } catch (err) {
      if (isElsewhere(err)) result.elsewhere++;
      else result.failed++;
      leftFiles.push(file);
    }
    step();
  }

  // Keep a field locally only while something left behind still needs it.
  const stillUsed = new Set(leftRecords.flatMap((r) => Object.keys(r.values).map((k) => fieldRef(r.entityType, k))));
  const leftFields = plan.fields.filter((f) =>
    stillUsed.has(fieldRef(f.entityType, f.key)) || (unplaced.includes(f) && !options.canDefineFields));
  keep(LOCAL_KEYS.fields, leftFields);
  keep(LOCAL_KEYS.values, leftRecords);
  keep(LOCAL_KEYS.attachments, leftFiles);
  return result;
}
