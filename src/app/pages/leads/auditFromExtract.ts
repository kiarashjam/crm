// Turns the raw rows produced by the lead-status-audit workflow into a verdict,
// using the SAME derivation the app uses.
//
// The split is the point. SQL extracts; this judges. Deciding correctness in
// T-SQL would have meant a second implementation of the pipeline→status rule,
// which is precisely the mistake that produced three consecutive rounds of bugs
// in this codebase.
//
// Input lines look like:
//   C|<status>|<outreachStatus>|<contactOutcome>|<meetingAttended>|<stillInterested>
//    |<contractStatus>|<contractSigned>|<depositPaid>|<meetingDate>|<contractSentDate>
//    |<signatureDate>|<paymentDate>|<isConverted>|<count>
//   L|<statusName>|<displayOrder>
//   TOTALS|<leads>|<withPipeline>|<converted>

import type { Lead } from '@/app/api/types';
import type { LeadPipeline } from './leadPipeline';
import { findStatusFixes, summariseFixes, groupFixesByTarget } from './bulkStatusReconcile';
import type { StatusOption } from './leadStatusSync';

/** SQL emits '' for NULL, and booleans as 'true'/'false'. */
const str = (v: string) => (v === '' ? undefined : v);
const bool = (v: string) => (v === '' ? undefined : v === 'true' || v === '1');

export interface ExtractRow {
  status: string;
  pipeline: LeadPipeline;
  isConverted: boolean;
  count: number;
}

export interface Extract {
  rows: ExtractRow[];
  statusOptions: StatusOption[];
  totals: { leads: number; withPipeline: number; converted: number };
}

export function parseExtract(text: string): Extract {
  const rows: ExtractRow[] = [];
  const statusOptions: StatusOption[] = [];
  let totals = { leads: 0, withPipeline: 0, converted: 0 };

  for (const raw of text.split('\n')) {
    const line = raw.trim();
    if (!line.includes('|')) continue;
    const f = line.split('|');

    if (f[0] === 'TOTALS' && f.length >= 4) {
      totals = {
        leads: Number(f[1]) || 0,
        withPipeline: Number(f[2]) || 0,
        converted: Number(f[3]) || 0,
      };
    } else if (f[0] === 'L' && f.length >= 3) {
      statusOptions.push({ id: f[1]!, name: f[1]!, displayOrder: Number(f[2]) || 0 });
    } else if (f[0] === 'C' && f.length >= 15) {
      rows.push({
        status: f[1] === '(null)' ? '' : f[1]!,
        pipeline: {
          outreachStatus: str(f[2]!) as LeadPipeline['outreachStatus'],
          contactOutcome: str(f[3]!) as LeadPipeline['contactOutcome'],
          meetingAttended: bool(f[4]!),
          stillInterested: bool(f[5]!),
          contractStatus: str(f[6]!) as LeadPipeline['contractStatus'],
          contractSigned: str(f[7]!) as LeadPipeline['contractSigned'],
          depositPaid: bool(f[8]!),
          meetingDate: str(f[9]!),
          contractSentDate: str(f[10]!),
          signatureDate: str(f[11]!),
          paymentDate: str(f[12]!),
        },
        isConverted: f[13] === '1',
        count: Number(f[14]) || 0,
      });
    }
  }
  return { rows, statusOptions, totals };
}

export interface AuditVerdict {
  totals: Extract['totals'];
  /** Leads whose status disagrees with their pipeline. */
  wrong: number;
  /** Of those, unambiguous forward moves. */
  safe: number;
  /** Of those, needing a human decision. */
  needsReview: number;
  /** Leads whose status already matches. */
  correct: number;
  byTarget: { to: string; count: number }[];
  /** One line per distinct problem shape, largest first. */
  detail: { count: number; from: string; to: string; because: string; flag?: string }[];
}

/**
 * Expand each grouped combination back into `count` synthetic leads and run the
 * real reconciler over them.
 *
 * Expanding is deliberate: `findStatusFixes` takes leads, and feeding it one
 * lead per group would report group counts rather than lead counts — an
 * undercount that would make the problem look smaller than it is.
 */
export function auditExtract(extract: Extract): AuditVerdict {
  const leads: Lead[] = [];
  extract.rows.forEach((r, gi) => {
    for (let i = 0; i < r.count; i += 1) {
      leads.push({
        id: `g${gi}-${i}`,
        name: `group ${gi}`,
        email: 'x@y.z',
        status: r.status,
        isConverted: r.isConverted,
        pipelineState: JSON.stringify(r.pipeline),
      });
    }
  });

  const fixes = findStatusFixes({
    leads,
    statusOptions: extract.statusOptions,
    statusesLoaded: extract.statusOptions.length > 0,
  });
  const s = summariseFixes(fixes);

  // Collapse back to distinct problem shapes for a readable report.
  const shapes = new Map<string, { count: number; from: string; to: string; because: string; flag?: string }>();
  for (const f of fixes) {
    const flag = !f.advances ? 'moves backwards' : f.parked ? 'deliberately set' : undefined;
    const key = `${f.from}→${f.to}|${f.because}|${flag ?? ''}`;
    const e = shapes.get(key) ?? { count: 0, from: f.from, to: f.to, because: f.because, flag };
    e.count += 1;
    shapes.set(key, e);
  }

  return {
    totals: extract.totals,
    wrong: s.total,
    safe: s.safe,
    needsReview: s.needsReview,
    correct: Math.max(extract.totals.leads - s.total, 0),
    byTarget: groupFixesByTarget(fixes),
    detail: [...shapes.values()].sort((a, b) => b.count - a.count),
  };
}
