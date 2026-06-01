import { Download } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { downloadCsv, type CsvColumn } from '@/app/lib/exportCsv';

interface ExportCsvButtonProps<T> {
  rows: T[];
  columns: CsvColumn<T>[];
  filename: string;
  label?: string;
  className?: string;
}

/** Small toolbar button that exports the given rows to a CSV download. */
export default function ExportCsvButton<T>({ rows, columns, filename, label = 'Export', className }: ExportCsvButtonProps<T>) {
  const handle = () => {
    if (!rows.length) { toast.error('Nothing to export'); return; }
    downloadCsv(filename, rows, columns);
    toast.success(`Exported ${rows.length} row${rows.length === 1 ? '' : 's'}`);
  };
  return (
    <Button type="button" variant="outline" size="sm" onClick={handle} className={className}>
      <Download className="h-4 w-4" /> {label}
    </Button>
  );
}
