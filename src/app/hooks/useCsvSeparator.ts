// The column separator of the workspace's CSV exports: a semicolon when it chose one in
// Settings (Excel set up for Switzerland splits on semicolons), else a comma, as exports
// always were. The server's exports read the same setting.

import { useOrgOptional } from '@/app/contexts/OrgContext';
import type { CsvSeparator } from '@/app/lib/exportCsv';

export function useCsvSeparator(): CsvSeparator {
  return useOrgOptional()?.currentOrg?.csvSemicolon ? ';' : ',';
}
