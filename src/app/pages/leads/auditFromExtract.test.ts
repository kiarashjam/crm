// Not a unit test of the app: this is the harness that turns the production
// extract into a verdict, using the real derivation. Run with
//   AUDIT_FILE=/path/to/extract.txt npx vitest run auditFromExtract
// With no file it self-checks the parser against a synthetic extract.

import { describe, it, expect } from 'vitest';
import { readFileSync, existsSync } from 'node:fs';
import { parseExtract, auditExtract } from './auditFromExtract';

const SYNTHETIC = [
  'TOTALS|5|4|0',
  'L|New|0', 'L|Attempted Contact|1', 'L|Contacted|2', 'L|Connected|3',
  'L|Contract Pending|4', 'L|Awaiting Signature|5', 'L|Signed|6', 'L|Lost / Not Interested|7',
  // status New, but met and still interested -> should want Contract Pending
  'C|New|contacted|meeting_scheduled|true|true||||2026-06-01||||0|2',
  // already correct
  'C|Contacted|contacted|||||||||||0|1',
  // no pipeline at all -> not a problem
  'C|New|||||||||||0|2',
].join('\n');

describe('audit harness', () => {
  it('parses the extract format and judges with the real derivation', () => {
    const e = parseExtract(SYNTHETIC);
    expect(e.totals.leads).toBe(5);
    expect(e.statusOptions).toHaveLength(8);
    const v = auditExtract(e);
    // The two met-and-interested leads at New are the only problem.
    expect(v.wrong).toBe(2);
    expect(v.byTarget).toEqual([{ to: 'Contract Pending', count: 2 }]);
    expect(v.correct).toBe(3);
  });

  it('reports the production extract when one is supplied', () => {
    const file = process.env.AUDIT_FILE;
    if (!file || !existsSync(file)) {
      console.log('\n(no AUDIT_FILE supplied — skipping the live report)\n');
      return;
    }
    const v = auditExtract(parseExtract(readFileSync(file, 'utf8')));
    console.log('\n===== LEAD STATUS AUDIT =====');
    console.log(`leads: ${v.totals.leads}  with steps recorded: ${v.totals.withPipeline}  converted: ${v.totals.converted}`);
    console.log(`status matches steps: ${v.correct}`);
    console.log(`status WRONG: ${v.wrong}   (${v.safe} straightforward, ${v.needsReview} need a decision)`);
    if (v.byTarget.length) {
      console.log('\nwould become:');
      for (const t of v.byTarget) console.log(`  ${String(t.count).padStart(4)} → ${t.to}`);
    }
    if (v.detail.length) {
      console.log('\nby problem shape:');
      for (const d of v.detail) {
        console.log(`  ${String(d.count).padStart(4)}  ${d.from || '(blank)'} → ${d.to}   [${d.because}]${d.flag ? '  ⚠ ' + d.flag : ''}`);
      }
    }
    console.log('=============================\n');
    expect(v.totals.leads).toBeGreaterThanOrEqual(0);
  });
});
