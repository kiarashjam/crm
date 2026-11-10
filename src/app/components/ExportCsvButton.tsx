import { Download } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { downloadCsv, type CsvColumn } from '@/app/lib/exportCsv';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { orgCan, PERMISSION } from '@/app/api/organizations';

interface ExportCsvButtonProps<T> {
  rows: T[];
  columns: CsvColumn<T>[];
  filename: string;
  label?: string;
  className?: string;
}

/** Small toolbar button that exports the given rows to a CSV download. */
export default function ExportCsvButton<T>({ rows, columns, filename, label = 'Export', className }: ExportCsvButtonProps<T>) {
  const org = useOrgOptional();
  const handle = () => {
    if (!rows.length) { toast.error('Nothing to export'); return; }
    downloadCsv(filename, rows, columns);
    toast.success(`Exported ${rows.length} row${rows.length === 1 ? '' : 's'}`);
  };
  // Exporting takes records.export; in a personal workspace everything is the person's own.
  if (org && !orgCan(org.currentOrg ?? null, PERMISSION.ExportRecords)) return null;
  return (
    <Button type="button" variant="outline" size="sm" onClick={handle} className={className}>
      <Download className="h-4 w-4" /> {label}
    </Button>
  );
}
