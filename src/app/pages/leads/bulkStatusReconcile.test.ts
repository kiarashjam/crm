// Finding leads whose status disagrees with their pipeline, in bulk.
//
// The risky half of this feature is what it must NOT touch. A bulk action that
// quietly demotes a lead somebody marked ahead of the pipeline, or revives one
// they parked, is worse than leaving the mess alone — so most of these tests are
// about restraint.

import { describe, it, expect } from 'vitest';
import type { Lead } from '@/app/api/types';
import type { LeadPipeline } from './leadPipeline';
import {
  findStatusFixes, safeFixes, summariseFixes, groupFixesByTarget,
} from './bulkStatusReconcile';

const VOCAB = [
  'New', 'Attempted Contact', 'Contacted', 'Connected',
  'Contract Pending', 'Awaiting Signature', 'Signed', 'Lost / Not Interested',
].map((name, i) => ({ id: `s${i}`, name, displayOrder: i }));

const lead = (p: Partial<Lead> & { pipeline?: LeadPipeline }): Lead => {
  const { pipeline, ...rest } = p;
  return {
    id: 'l1', name: 'Lead', email: 'a@b.c', status: 'New',
    ...(pipeline ? { pipelineState: JSON.stringify(pipeline) } : {}),
    ...rest,
  };
};

const run = (leads: Lead[]) =>
  findStatusFixes({ leads, statusOptions: VOCAB, statusesLoaded: true });

const CONTACTED: LeadPipeline = { outreachStatus: 'contacted' };
const MET: LeadPipeline = { meetingAttended: true, stillInterested: true };
const SIGNED: LeadPipeline = { contractSigned: 'yes', signatureDate: '2026-06-01' };

describe('findStatusFixes — the reported problem', () => {
  it('finds a lead sitting at New with an advanced pipeline', () => {
    // The exact complaint: steps recorded, status never moved.
    const fixes = run([lead({ id: 'a', status: 'New', pipeline: MET })]);
    expect(fixes).toHaveLength(1);
    expect(fixes[0]!.from).toBe('New');
    expect(fixes[0]!.to).toBe('Contract Pending');
    expect(fixes[0]!.advances).toBe(true);
    expect(fixes[0]!.because).toBeTruthy();
  });

  it('finds one per stale lead and leaves correct ones alone', () => {
    const fixes = run([
      lead({ id: 'a', status: 'New', pipeline: CONTACTED }),      // stale
      lead({ id: 'b', status: 'Contacted', pipeline: CONTACTED }), // already right
      lead({ id: 'c', status: 'New', pipeline: SIGNED }),          // stale
    ]);
    expect(fixes.map((f) => f.lead.id)).toEqual(['a', 'c']);
  });

  it('suggests only statuses from the org\'s own list', () => {
    const allowed = new Set(VOCAB.map((v) => v.name));
    const fixes = run([
      lead({ id: 'a', status: 'New', pipeline: CONTACTED }),
      lead({ id: 'b', status: 'New', pipeline: MET }),
      lead({ id: 'c', status: 'New', pipeline: SIGNED }),
      lead({ id: 'd', status: 'New', pipeline: { contactOutcome: 'not_interested' } }),
    ]);
    for (const f of fixes) expect(allowed).toContain(f.to);
  });

  it('carries a REAL status id, so the write cannot leave a contradictory one', () => {
    const guid = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';
    const fixes = findStatusFixes({
      leads: [lead({ status: 'New', pipeline: SIGNED })],
      statusOptions: [...VOCAB.filter((v) => v.name !== 'Signed'), { id: guid, name: 'Signed', displayOrder: 6 }],
      statusesLoaded: true,
    });
    expect(fixes[0]!.to).toBe('Signed');
    expect(fixes[0]!.toId).toBe(guid);
  });

  it('omits the id when the option carries a placeholder rather than a real one', () => {
    // Deliberate: the fallback status list uses the NAME as the id, and sending
    // that to the server would set leadStatusId to something that is not a
    // record. The name alone is correct in that case.
    const fixes = run([lead({ status: 'New', pipeline: SIGNED })]);
    expect(fixes[0]!.to).toBe('Signed');
    expect(fixes[0]!.toId).toBeUndefined();
  });
});

describe('findStatusFixes — what it must leave alone', () => {
  it('reports NOTHING for a book of leads with no pipeline recorded', () => {
    // Otherwise every untouched lead becomes a "fix" that writes New over New.
    const fixes = run([
      lead({ id: 'a', status: 'New' }),
      lead({ id: 'b', status: 'New' }),
    ]);
    expect(fixes).toEqual([]);
  });

  it('ignores converted leads — their status belongs to the deal now', () => {
    expect(run([lead({ status: 'New', isConverted: true, pipeline: SIGNED })])).toEqual([]);
  });

  it('does nothing at all before the status list has loaded', () => {
    // Guessing against a list we have not seen is how you write a status the
    // organisation does not have.
    expect(findStatusFixes({
      leads: [lead({ status: 'New', pipeline: SIGNED })],
      statusOptions: VOCAB,
      statusesLoaded: false,
    })).toEqual([]);
    expect(findStatusFixes({
      leads: [lead({ status: 'New', pipeline: SIGNED })],
      statusOptions: [],
      statusesLoaded: true,
    })).toEqual([]);
  });

  it('survives unparseable pipeline JSON instead of throwing', () => {
    const l = lead({ status: 'New' });
    expect(() => run([{ ...l, pipelineState: '{not json' }])).not.toThrow();
  });
});

describe('safeFixes — restraint is the whole point', () => {
  it('does NOT pre-select a backward move', () => {
    // Status ahead of the pipeline is usually a person marking something early.
    // Demoting them silently would be the automation overruling a human.
    const fixes = run([lead({ status: 'Signed', pipeline: CONTACTED })]);
    expect(fixes).toHaveLength(1);
    expect(fixes[0]!.advances).toBe(false);
    expect(safeFixes(fixes)).toHaveLength(0);
  });

  it('does NOT pre-select a deliberately parked lead', () => {
    const fixes = findStatusFixes({
      leads: [lead({ status: 'Do Not Contact', pipeline: MET })],
      statusOptions: [...VOCAB, { id: 'dnc', name: 'Do Not Contact', displayOrder: 99 }],
      statusesLoaded: true,
    });
    expect(fixes).toHaveLength(1);
    expect(fixes[0]!.parked).toBe(true);
    expect(safeFixes(fixes)).toHaveLength(0);
  });

  it('does NOT pre-select a status it cannot even classify', () => {
    // Blocklists are never complete, so anything unrecognised gets the same
    // protection as an explicit park.
    const fixes = findStatusFixes({
      leads: [lead({ status: 'Waiting On Legal', pipeline: MET })],
      statusOptions: [...VOCAB, { id: 'wl', name: 'Waiting On Legal', displayOrder: 98 }],
      statusesLoaded: true,
    });
    expect(fixes[0]!.parked).toBe(true);
    expect(safeFixes(fixes)).toHaveLength(0);
  });

  it('DOES pre-select a plain forward move, including to a terminal', () => {
    const forward = run([lead({ id: 'a', status: 'New', pipeline: MET })]);
    expect(safeFixes(forward)).toHaveLength(1);

    // A terminal sits off the ladder, so reaching one is not a regression.
    const lost = run([lead({ id: 'b', status: 'Contacted', pipeline: { stillInterested: false } })]);
    expect(lost[0]!.advances).toBe(true);
    expect(safeFixes(lost)).toHaveLength(1);
  });

  it('every unsafe fix is still REPORTED, never hidden', () => {
    const fixes = run([
      lead({ id: 'a', status: 'New', pipeline: MET }),        // safe
      lead({ id: 'b', status: 'Signed', pipeline: CONTACTED }), // backward
    ]);
    const s = summariseFixes(fixes);
    expect(s.total).toBe(2);
    expect(s.safe).toBe(1);
    expect(s.needsReview).toBe(1);
    expect(s.safe + s.needsReview).toBe(s.total);
  });
});

describe('groupFixesByTarget', () => {
  it('summarises what would change, most common first', () => {
    const fixes = run([
      lead({ id: 'a', status: 'New', pipeline: MET }),
      lead({ id: 'b', status: 'New', pipeline: MET }),
      lead({ id: 'c', status: 'New', pipeline: SIGNED }),
    ]);
    expect(groupFixesByTarget(fixes)).toEqual([
      { to: 'Contract Pending', count: 2 },
      { to: 'Signed', count: 1 },
    ]);
  });

  it('accounts for every fix exactly once', () => {
    const fixes = run([
      lead({ id: 'a', status: 'New', pipeline: CONTACTED }),
      lead({ id: 'b', status: 'New', pipeline: MET }),
      lead({ id: 'c', status: 'New', pipeline: SIGNED }),
    ]);
    const counted = groupFixesByTarget(fixes).reduce((n, g) => n + g.count, 0);
    expect(counted).toBe(fixes.length);
  });
});
