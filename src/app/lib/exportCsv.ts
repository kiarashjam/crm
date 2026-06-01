// Client-side CSV export. No backend needed — builds a CSV string from rows and
// a column spec, then triggers a download.

export interface CsvColumn<T> {
  header: string;
  value: (row: T) => string | number | null | undefined;
}

/** UTF-8 byte-order mark (U+FEFF) so Excel opens the file with the right encoding. */
const BOM = String.fromCharCode(0xfeff);

function escapeCell(v: string | number | null | undefined): string {
  if (v == null) return '';
  const s = String(v);
  // Quote when the value contains a comma, quote, or newline; double inner quotes.
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

export function toCsv<T>(rows: T[], columns: CsvColumn<T>[]): string {
  const head = columns.map((c) => escapeCell(c.header)).join(',');
  const body = rows.map((r) => columns.map((c) => escapeCell(c.value(r))).join(',')).join('\n');
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
