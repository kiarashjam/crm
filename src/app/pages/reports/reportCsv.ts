// CSV exports of the Reports page's tables.
//
// Managers take these figures into board and finance meetings, and nothing on the
// page could leave it. Each export is the table as the server returned it, with
// raw numbers (not formatted text) so a spreadsheet can add them up, and one row
// per currency: a spreadsheet that sums the column gets one currency's total, not
// CHF plus EUR. Every cell goes through lib/exportCsv, which defuses text a
// spreadsheet would run as a formula.

import type { CsvColumn } from '@/app/lib/exportCsv';
import type {
  ForecastMonthRow, ForecastSnapshotRow, LeadStatusRow, OwnerRow, ReportSummary, StageRow, CountRow, SeriesPoint, MoneyRow,
} from '@/app/api/reporting';

export interface CsvTable<T> {
  name: string;
  rows: T[];
  columns: CsvColumn<T>[];
}

/** The figures at the top of the page, one row per figure and currency. */
export function overviewTable(s: ReportSummary): CsvTable<{ figure: string } & MoneyRow> {
  const money = (figure: string, rows: MoneyRow[]) => rows.map((r) => ({ figure, ...r }));
  return {
    name: 'overview',
    rows: [
      ...money('Open pipeline (now)', s.open.byCurrency),
      ...money('Expected to close in the period', s.closingInPeriod.byCurrency),
      ...money('Created in the period', s.created.byCurrency),
      ...money('Won in the period', s.won.byCurrency),
      ...money('Lost in the period', s.lost.byCurrency),
      ...s.weighted.map((w) => ({ figure: 'Weighted pipeline', currency: w.currency, count: w.covered, value: w.value, unreadable: 0 })),
    ],
    columns: [
      { header: 'Figure', value: (r) => r.figure },
      { header: 'Currency', value: (r) => r.currency },
      { header: 'Deals', value: (r) => r.count },
      { header: 'Value', value: (r) => r.value },
      { header: 'Deals with no readable value', value: (r) => r.unreadable },
    ],
  };
}

export function stageTable(rows: StageRow[]): CsvTable<StageRow> {
  return {
    name: 'pipeline-by-stage',
    rows,
    columns: [
      { header: 'Pipeline', value: (r) => r.pipelineName },
      { header: 'Stage', value: (r) => r.stageName },
      { header: 'Currency', value: (r) => r.currency },
      { header: 'Open deals', value: (r) => r.count },
      { header: 'Value', value: (r) => r.value },
      { header: 'Weighted', value: (r) => r.weighted },
      { header: 'Deals with no readable value', value: (r) => r.unreadable },
    ],
  };
}

export function ownerTable(rows: OwnerRow[]): CsvTable<OwnerRow> {
  return {
    name: 'by-owner',
    rows,
    columns: [
      { header: 'Owner', value: (r) => r.ownerName },
      { header: 'Currency', value: (r) => r.currency },
      { header: 'Open deals', value: (r) => r.openCount },
      { header: 'Open value', value: (r) => r.openValue },
      { header: 'Weighted', value: (r) => r.weighted },
      { header: 'Won in period', value: (r) => r.wonCount },
      { header: 'Won value', value: (r) => r.wonValue },
      { header: 'Lost in period', value: (r) => r.lostCount },
    ],
  };
}

export function forecastTable(rows: ForecastMonthRow[]): CsvTable<ForecastMonthRow> {
  return {
    name: 'forecast-by-close-month',
    rows,
    columns: [
      { header: 'Month', value: (r) => r.month },
      { header: 'Currency', value: (r) => r.currency },
      { header: 'Deals expected to close', value: (r) => r.count },
      { header: 'Value', value: (r) => r.value },
      { header: 'Weighted', value: (r) => r.weighted },
      { header: 'With a probability', value: (r) => r.covered },
      { header: 'Without a probability', value: (r) => r.missing },
      { header: 'Deals with no readable value', value: (r) => r.unreadable },
      { header: 'Won that month', value: (r) => r.wonCount },
      { header: 'Won value', value: (r) => r.wonValue },
    ],
  };
}

export function leadStatusTable(rows: LeadStatusRow[]): CsvTable<LeadStatusRow> {
  return {
    name: 'leads-by-status',
    rows,
    columns: [
      { header: 'Status now', value: (r) => r.status },
      { header: 'Leads created in period', value: (r) => r.count },
      { header: 'Of which converted', value: (r) => r.converted },
    ],
  };
}

export function countTable(name: string, header: string, rows: CountRow[]): CsvTable<CountRow> {
  return {
    name,
    rows,
    columns: [
      { header, value: (r) => r.key },
      { header: 'Count', value: (r) => r.count },
    ],
  };
}

export function seriesTable(name: string, header: string, rows: SeriesPoint[]): CsvTable<SeriesPoint> {
  return {
    name,
    rows,
    columns: [
      { header: 'Period starting', value: (r) => r.start },
      { header, value: (r) => r.count },
    ],
  };
}

export function savedForecastTable(rows: ForecastSnapshotRow[], savedBy: string): CsvTable<ForecastSnapshotRow> {
  return {
    name: 'saved-forecast',
    rows,
    columns: [
      { header: 'Month', value: (r) => r.month },
      { header: 'Owner', value: (r) => r.assigneeName },
      { header: 'Currency', value: (r) => r.currency },
      { header: 'Weighted', value: (r) => r.weightedValue },
      { header: 'Value', value: (r) => r.openValue },
      { header: 'With a probability', value: (r) => r.covered },
      { header: 'Without a probability', value: (r) => r.missing },
      { header: 'Past their close date', value: (r) => r.overdueCount },
      { header: 'Past their close date, value', value: (r) => r.overdueValue },
      { header: 'No close date', value: (r) => r.noCloseDateCount },
      { header: 'Deals with no readable value', value: (r) => r.unreadableCount },
      { header: 'Counted by close date', value: (r) => (r.byCloseDate ? 'yes' : 'no, every open deal') },
      { header: 'Saved at (UTC)', value: (r) => r.savedAtUtc },
      { header: 'Saved by', value: () => savedBy },
    ],
  };
}

/** "cadence-pipeline-by-stage-2026-10-01-to-2026-10-31.csv" */
export function csvFileName(table: string, firstDay: string, lastDay: string): string {
  return `cadence-${table}-${firstDay}-to-${lastDay}.csv`;
}
