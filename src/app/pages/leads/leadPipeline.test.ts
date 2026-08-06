// Rules for where a lead dropped out, and what each phase card says about itself.

import { describe, it, expect } from 'vitest';
import {
  lostReason,
  lostPhase,
  phaseCaptions,
  phaseCompletion,
  currentPhase,
  PHASE_TITLES,
  type LeadPipeline,
} from './leadPipeline';

/** A pipeline with every phase satisfied. */
const COMPLETE: LeadPipeline = {
  outreachStatus: 'contacted',
  contactOutcome: 'meeting_scheduled',
  meetingDate: '2026-08-20',
  meetingAttended: true,
  stillInterested: true,
  contractStatus: 'yes',
  contractSentDate: '2026-08-22',
  contractSigned: 'yes',
  signatureDate: '2026-08-25',
  depositPaid: true,
  paymentDate: '2026-08-28',
};

describe('lostReason — unchanged by the move to a rule table', () => {
  const CASES: [LeadPipeline, string | null][] = [
    [{ contactOutcome: 'not_interested' }, 'Not interested'],
    [{ stillInterested: false }, 'Not interested after meeting'],
    [{ meetingAttended: false }, 'No-show at meeting'],
    [{ contractStatus: 'profile_rejected' }, 'Profile rejected'],
    [{ contractStatus: 'no_longer_interested' }, 'No longer interested'],
    [{ contractSigned: 'no' }, 'Contract declined'],
    [{}, null],
    [COMPLETE, null],
  ];
  it.each(CASES)('%j → %s', (pipeline, expected) => {
    expect(lostReason(pipeline)).toBe(expected);
  });

  it('still lets a confirmed interest override a no-show', () => {
    // The precedence that keeps the tracker banner agreeing with the lead status.
    const p: LeadPipeline = { meetingAttended: false, stillInterested: true };
    expect(lostReason(p)).toBeNull();
  });
});

describe('lostPhase — the phase that RECORDED the loss', () => {
  it.each([
    [{ contactOutcome: 'not_interested' } as LeadPipeline, 1],
    [{ stillInterested: false } as LeadPipeline, 2],
    [{ meetingAttended: false } as LeadPipeline, 2],
    [{ contractStatus: 'profile_rejected' } as LeadPipeline, 3],
    [{ contractStatus: 'no_longer_interested' } as LeadPipeline, 3],
    [{ contractSigned: 'no' } as LeadPipeline, 4],
  ])('%j → phase %i', (pipeline, expected) => {
    expect(lostPhase(pipeline)).toBe(expected);
  });

  it('is null when the lead has not dropped out', () => {
    expect(lostPhase({})).toBeNull();
    expect(lostPhase(COMPLETE)).toBeNull();
  });

  it('DIVERGES from currentPhase when a later phase records the loss', () => {
    // The bug this function exists to fix: rejecting a profile at Contract while
    // the Meeting phase is still incomplete. Marking the stopped card by current
    // phase would put the rose treatment on Meeting.
    const p: LeadPipeline = { contractStatus: 'profile_rejected' };
    expect(lostPhase(p)).toBe(3);
    expect(currentPhase(p)).toBe(1);
    expect(lostPhase(p)).not.toBe(currentPhase(p));
  });

  it('agrees with lostReason about which rule fired', () => {
    // Two projections of one table: whenever there is a reason there is a phase.
    const STATES: LeadPipeline[] = [
      {}, COMPLETE,
      { contactOutcome: 'not_interested' },
      { meetingAttended: false },
      { meetingAttended: false, stillInterested: true },
      { stillInterested: false },
      { contractStatus: 'profile_rejected' },
      { contractSigned: 'no' },
      { contactOutcome: 'not_interested', depositPaid: true, paymentDate: '2026-08-28' },
    ];
    for (const p of STATES) {
      expect(lostPhase(p) === null).toBe(lostReason(p) === null);
    }
  });
});

describe('phaseCaptions', () => {
  it('returns one caption per phase', () => {
    expect(phaseCaptions({})).toHaveLength(PHASE_TITLES.length);
  });

  it('INVARIANT: a completed phase never says it is waiting on something', () => {
    // The caption reads phaseCompletion() directly, so a green check can never
    // sit beside "Waiting on: …". Swept across a matrix of real pipelines.
    const STATES: LeadPipeline[] = [
      {},
      { outreachStatus: 'attempted_no_answer' },
      { outreachStatus: 'contacted' },
      { outreachStatus: 'contacted', contactOutcome: 'follow_up' },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20' },
      { meetingAttended: true },
      { meetingAttended: true, stillInterested: true },
      { contractStatus: 'yes' },
      { contractStatus: 'yes', contractSentDate: '2026-08-22' },
      { contractSigned: 'yes' },
      { contractSigned: 'yes', signatureDate: '2026-08-25' },
      { depositPaid: true },
      { depositPaid: true, paymentDate: '2026-08-28' },
      COMPLETE,
    ];
    for (const p of STATES) {
      const done = phaseCompletion(p);
      const captions = phaseCaptions(p);
      done.forEach((isDone, i) => {
        if (isDone) {
          expect(captions[i], `${JSON.stringify(p)} phase ${i + 1}`).not.toMatch(/waiting on/i);
        }
      });
    }
  });

  it('names what is outstanding on the phase in progress', () => {
    expect(phaseCaptions({ outreachStatus: 'contacted' })[0]).toMatch(/result of contact/i);
    expect(phaseCaptions({ meetingAttended: true })[1]).toMatch(/still interested/i);
    expect(phaseCaptions({ contractSigned: 'yes' })[3]).toMatch(/signature date/i);
  });

  it('reports what happened on a completed phase, with the date', () => {
    const captions = phaseCaptions(COMPLETE);
    expect(captions[1]).toBe('Attended · still interested');
    // Rendered in the runner's locale; assert on the day number rather than a format.
    expect(captions[2]).toMatch(/Contract sent .*22/);
    expect(captions[4]).toMatch(/Deposit paid .*28/);
  });

  it('shows the drop-out reason on the phase that recorded it', () => {
    const p: LeadPipeline = { contractStatus: 'profile_rejected' };
    // Phase 3 (index 2), not the current phase.
    expect(phaseCaptions(p)[2]).toBe('Profile rejected');
  });

  it('renders a date as the day it was picked, not the day before', () => {
    // A bare YYYY-MM-DD parses as UTC midnight and slips a day in any negative
    // offset. The caption pins the time to keep it local.
    expect(phaseCaptions({ contractStatus: 'yes', contractSentDate: '2026-08-22' })[2]).toMatch(/22/);
  });

  it('drops the date rather than rendering "Invalid Date" when it cannot be parsed', () => {
    // `phaseCompletion` only checks that the date is truthy, so a garbage value
    // still marks the phase done. The caption must degrade to the bare fact
    // rather than leaking "Invalid Date" into the UI.
    const p: LeadPipeline = { contractStatus: 'yes', contractSentDate: 'not-a-date' };
    expect(phaseCompletion(p)[2]).toBe(true);
    expect(phaseCaptions(p)[2]).toBe('Contract sent');
    expect(phaseCaptions(p).join(' ')).not.toMatch(/Invalid Date|NaN/);
  });
});
