// What a prepared email becomes for one person, worked out here as the server's outbox
// does it (backend MergeFields), so the preview is what they will read.

const PLACEHOLDER = /\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*(?:\|([^{}]*))?\}\}/g;
const ANY_PLACEHOLDER = /\{\{\s*([^{}|]*?)\s*(?:\|[^{}]*)?\}\}/g;
const BLANK = /\[\[([^[\]]*)\]\]/g;

export interface Recipient {
  name?: string | null;
  email?: string | null;
  company?: string | null;
}

/** The values the outbox fills, for one recipient: the first and last name split as it splits them. */
export function mergeValues(recipient: Recipient, senderName?: string | null, workspace?: string | null): Record<string, string> {
  const full = (recipient.name ?? '').trim();
  const space = full.indexOf(' ');
  return {
    firstname: space < 0 ? full : full.slice(0, space),
    lastname: space < 0 ? '' : full.slice(space + 1).trim(),
    name: full,
    email: (recipient.email ?? '').trim(),
    company: (recipient.company ?? '').trim(),
    sendername: (senderName ?? '').trim(),
    workspace: (workspace ?? '').trim(),
  };
}

/** The text with each placeholder filled: its value, else its fallback, else nothing; and those that had neither. */
export function fillPlaceholders(text: string, values: Record<string, string>): { text: string; unfilled: string[] } {
  const unfilled: string[] = [];
  const filled = text.replace(PLACEHOLDER, (_match, key: string, fallback?: string) => {
    const value = values[key.toLowerCase()];
    if (value && value.trim()) return value.trim();
    if (fallback !== undefined) return fallback.trim();
    const written = `{{${key}}}`;
    if (!unfilled.some((u) => u.toLowerCase() === written.toLowerCase())) unfilled.push(written);
    return '';
  });
  return { text: filled, unfilled };
}

/** Placeholders the outbox does not fill, as written, once each. */
export function unknownPlaceholders(text: string, known: string[]): string[] {
  const knownLower = new Set(known.map((k) => k.toLowerCase()));
  const found: string[] = [];
  for (const m of text.matchAll(ANY_PLACEHOLDER)) {
    const key = (m[1] ?? '').trim().toLowerCase();
    if (!knownLower.has(key) && !found.some((f) => f.toLowerCase() === m[0].toLowerCase())) found.push(m[0]);
  }
  return found;
}

/** The blanks left to fill in before sending ("[[date]]" gives "date"), once each, in order. */
export function blanksIn(text: string): string[] {
  const found: string[] = [];
  for (const m of text.matchAll(BLANK)) {
    const label = (m[1] ?? '').trim() || '…';
    if (!found.includes(label)) found.push(label);
  }
  return found;
}

/** The text cut into plain runs and blanks, for a preview that marks what is still to fill in. */
export function splitBlanks(text: string): { text: string; blank: boolean }[] {
  const parts: { text: string; blank: boolean }[] = [];
  let at = 0;
  for (const m of text.matchAll(BLANK)) {
    if (m.index! > at) parts.push({ text: text.slice(at, m.index), blank: false });
    parts.push({ text: (m[1] ?? '').trim() || '…', blank: true });
    at = m.index! + m[0].length;
  }
  if (at < text.length) parts.push({ text: text.slice(at), blank: false });
  return parts;
}

/** How a placeholder reads in the editor's chips. */
export const PLACEHOLDER_LABELS: Record<string, string> = {
  firstName: 'First name',
  lastName: 'Last name',
  name: 'Full name',
  email: 'Email',
  company: 'Company',
  senderName: 'Your name',
  workspace: 'Workspace',
};
