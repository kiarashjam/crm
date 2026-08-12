// Rules for where a lead dropped out, and what each phase card says about itself.

import { describe, it, expect } from 'vitest';
import {
  lostReason,
  lostPhase,
  phaseCaptions,
  phaseCompletion,
  phaseSteps,
  currentPhase,
  dropoutPhaseFor,
  isReasonRequired,
  isReasonComplete,
  dropoutReasonText,
  dropoutReasonBreakdown,
  DROPOUT_REASONS,
  DROPOUT_REASON_LABELS,
  PHASE_TITLES,
  type LeadPipeline,
} from './leadPipeline';
import { defaultFocus } from './usePhaseDisclosure';

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

/** A spread of pipelines covering every branch the two functions can take. */
const MATRIX: LeadPipeline[] = [
  {},
  COMPLETE,
  { outreachStatus: 'attempted_no_answer' },
  { outreachStatus: 'contacted' },
  { outreachStatus: 'contacted', contactOutcome: 'follow_up' },
  { outreachStatus: 'contacted', contactOutcome: 'not_interested' },
  { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
  { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20' },
  { meetingAttended: true },
  { meetingAttended: false },
  { meetingAttended: true, stillInterested: false },
  { meetingAttended: true, stillInterested: true },
  { contractStatus: 'to_be_sent' },
  { contractStatus: 'profile_rejected' },
  { contractStatus: 'yes' },
  { contractStatus: 'yes', contractSentDate: '2026-08-22' },
  { contractSigned: 'pending' },
  { contractSigned: 'no' },
  { contractSigned: 'yes', signatureDate: '2026-08-25' },
  { depositPaid: false },
  { depositPaid: true },
  { depositPaid: true, paymentDate: '2026-08-28' },
];

describe('phaseSteps', () => {
  it('INVARIANT: a phase is complete exactly when all of its steps are done', () => {
    // The folded card's dots and the card's own emerald check are two readings
    // of the same fact. If these ever disagree, one of them is lying to the user.
    for (const p of MATRIX) {
      const complete = phaseCompletion(p);
      phaseSteps(p).forEach((steps, i) => {
        expect(steps.every((s) => s.done), `${JSON.stringify(p)} phase ${i + 1}`).toBe(complete[i]);
      });
    }
  });

  it('returns one group per phase, none of them empty', () => {
    const groups = phaseSteps({});
    expect(groups).toHaveLength(PHASE_TITLES.length);
    for (const g of groups) expect(g.length).toBeGreaterThan(0);
  });

  it('distinguishes "answered but not advancing" from "untouched"', () => {
    // The reason the dots have three states rather than two: "To be sent" is a
    // real answer that leaves the phase incomplete, and a folded card that drew
    // it as blank would look like nobody had touched the contract at all.
    const [status] = phaseSteps({ contractStatus: 'to_be_sent' })[2]!;
    expect(status).toEqual({ label: 'Contract status', done: false, value: 'To be sent' });

    const [untouched] = phaseSteps({})[2]!;
    expect(untouched!.value).toBeNull();
    expect(untouched!.done).toBe(false);
  });

  it('never leaks an unparseable date into a summary', () => {
    const steps = phaseSteps({ contractStatus: 'yes', contractSentDate: 'not-a-date' })[2]!;
    // Truthy, so the phase counts as done — but there is no date worth showing.
    expect(steps[1]).toEqual({ label: 'Sent date', done: true, value: null });
  });
});

describe('defaultFocus — which card opens by itself', () => {
  it('opens the phase in progress', () => {
    expect(defaultFocus({ currentPhase: 4, lostPhase: null, complete: false })).toBe(3);
  });

  it('opens the phase that STOPPED the pipeline, not the current one', () => {
    // `{ contractStatus: 'profile_rejected' }` is current phase 1, lost at 3.
    // Opening phase 1 would show an empty outreach form while the reason the
    // lead is dead sits folded away two cards below.
    const p: LeadPipeline = { contractStatus: 'profile_rejected' };
    expect(currentPhase(p)).toBe(1);
    expect(defaultFocus({ currentPhase: currentPhase(p), lostPhase: lostPhase(p), complete: false })).toBe(2);
  });

  it('opens nothing at all when the pipeline is finished', () => {
    // Every card is a summary; the convert action is the only thing left to do.
    expect(defaultFocus({ currentPhase: 5, lostPhase: null, complete: true })).toBeNull();
  });

  it('always names a real phase for any reachable pipeline', () => {
    for (const p of MATRIX) {
      const focus = defaultFocus({
        currentPhase: currentPhase(p),
        lostPhase: lostPhase(p),
        complete: phaseCompletion(p).every(Boolean),
      });
      if (focus !== null) {
        expect(focus, JSON.stringify(p)).toBeGreaterThanOrEqual(0);
        expect(focus, JSON.stringify(p)).toBeLessThan(PHASE_TITLES.length);
      }
    }
  });
});

describe('drop-out reason capture', () => {
  const MET_NO: LeadPipeline = { meetingAttended: true, stillInterested: false };
  const CONTRACT_NO: LeadPipeline = { contractStatus: 'no_longer_interested' };

  it('asks for a reason at Meeting and at Contract, and nowhere else', () => {
    expect(dropoutPhaseFor(MET_NO)).toBe(2);
    expect(dropoutPhaseFor(CONTRACT_NO)).toBe(3);
    // Us rejecting them is not them rejecting us — none of the shared reasons
    // ("has kids", "not within budget") describes our own decision.
    expect(dropoutPhaseFor({ contractStatus: 'profile_rejected' })).toBeNull();
    // Nor do the other negatives, which are not "no longer interested".
    expect(dropoutPhaseFor({ contactOutcome: 'not_interested' })).toBeNull();
    expect(dropoutPhaseFor({ meetingAttended: false })).toBeNull();
    expect(dropoutPhaseFor({ contractSigned: 'no' })).toBeNull();
    expect(dropoutPhaseFor({})).toBeNull();
  });

  it('is satisfied only when the answer is actually complete', () => {
    expect(isReasonRequired({})).toBe(false);
    expect(isReasonComplete({})).toBe(true);

    expect(isReasonComplete(MET_NO)).toBe(false);
    expect(isReasonComplete({ ...MET_NO, dropoutReason: 'too_soon' })).toBe(true);
  });

  it('INVARIANT: "Other" with nothing typed is NOT complete', () => {
    // The precise hole this check exists to close — picking Other and moving on
    // would otherwise pass as a recorded reason while explaining nothing.
    expect(isReasonComplete({ ...MET_NO, dropoutReason: 'other' })).toBe(false);
    expect(isReasonComplete({ ...MET_NO, dropoutReason: 'other', dropoutReasonOther: '   ' })).toBe(false);
    expect(isReasonComplete({ ...MET_NO, dropoutReason: 'other', dropoutReasonOther: 'Moving abroad' })).toBe(true);
  });

  it('renders the free text as the reason when Other is chosen', () => {
    expect(dropoutReasonText({ ...MET_NO, dropoutReason: 'not_within_budget' })).toBe('Not within budget');
    expect(dropoutReasonText({ ...MET_NO, dropoutReason: 'other', dropoutReasonOther: 'Moving abroad' })).toBe('Moving abroad');
    expect(dropoutReasonText({ ...MET_NO, dropoutReason: 'other' })).toBeNull();
    expect(dropoutReasonText({})).toBeNull();
  });

  it('offers exactly the four agreed options, once', () => {
    expect(DROPOUT_REASONS).toEqual(['has_kids', 'not_within_budget', 'too_soon', 'other']);
    expect(new Set(DROPOUT_REASONS).size).toBe(DROPOUT_REASONS.length);
    for (const r of DROPOUT_REASONS) expect(DROPOUT_REASON_LABELS[r]).toBeTruthy();
  });

  it('says on the card that a reason is still owed', () => {
    expect(phaseCaptions(MET_NO)[1]).toBe('Not interested after meeting · waiting on: a reason');
    expect(phaseCaptions({ ...MET_NO, dropoutReason: 'too_soon' })[1])
      .toBe('Not interested after meeting · Too soon');
    expect(phaseCaptions(CONTRACT_NO)[2]).toBe('No longer interested · waiting on: a reason');
  });
});

describe('dropoutReasonBreakdown', () => {
  it('splits reasons by the phase they were logged at', () => {
    const rows = dropoutReasonBreakdown([
      { meetingAttended: true, stillInterested: false, dropoutReason: 'too_soon', dropoutReasonPhase: 2 },
      { meetingAttended: true, stillInterested: false, dropoutReason: 'has_kids', dropoutReasonPhase: 2 },
      { contractStatus: 'no_longer_interested', dropoutReason: 'not_within_budget', dropoutReasonPhase: 3 },
      {},                                        // not a drop-out at all
      { contractStatus: 'profile_rejected' },     // our decision, not theirs
    ]);
    const meeting = rows.find((r) => r.phase === 2)!;
    const contract = rows.find((r) => r.phase === 3)!;

    expect(meeting.total).toBe(2);
    expect(meeting.counts.too_soon).toBe(1);
    expect(meeting.counts.has_kids).toBe(1);
    expect(contract.total).toBe(1);
    expect(contract.counts.not_within_budget).toBe(1);
  });

  it('counts an unexplained drop-out as missing rather than hiding it', () => {
    // A stage with drop-outs but no reasons is a data-collection problem. Making
    // the chart look complete would bury exactly the thing worth acting on.
    const rows = dropoutReasonBreakdown([
      { meetingAttended: true, stillInterested: false },
      { meetingAttended: true, stillInterested: false, dropoutReason: 'other' }, // no text
    ]);
    const meeting = rows.find((r) => r.phase === 2)!;
    expect(meeting.total).toBe(2);
    expect(meeting.missing).toBe(2);
    expect(meeting.counts.other).toBe(0);
  });

  it('still counts rows written before the phase tag existed', () => {
    // Legacy pipelines have a reason but no dropoutReasonPhase. Attributing them
    // to the phase that currently shows the drop-out keeps the totals honest.
    const rows = dropoutReasonBreakdown([
      { meetingAttended: true, stillInterested: false, dropoutReason: 'too_soon' },
      { contractStatus: 'no_longer_interested', dropoutReason: 'has_kids' },
    ]);
    expect(rows.find((r) => r.phase === 2)!.counts.too_soon).toBe(1);
    expect(rows.find((r) => r.phase === 3)!.counts.has_kids).toBe(1);
  });

  it('always returns both stages, even with no data', () => {
    const rows = dropoutReasonBreakdown([]);
    expect(rows.map((r) => r.phase)).toEqual([2, 3]);
    for (const r of rows) expect(r.total).toBe(0);
  });

  it('does not let one lead be counted twice', () => {
    // Both negatives recorded on one lead. It is one departure, not two.
    const rows = dropoutReasonBreakdown([
      { stillInterested: false, contractStatus: 'no_longer_interested', dropoutReason: 'too_soon', dropoutReasonPhase: 2 },
    ]);
    expect(rows.reduce((n, r) => n + r.total, 0)).toBe(1);
  });
});
