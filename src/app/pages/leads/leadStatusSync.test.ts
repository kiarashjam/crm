// Rules for deriving a lead's status from its pipeline.
//
// The mapping is a product decision, so these tests are written as statements
// about behaviour ("a mailed contract does not look like a won deal") rather
// than as assertions about the implementation.

import { describe, it, expect } from 'vitest';
import {
  deriveStage,
  resolveStatus,
  classifyStatusLabel,
  isStickyStatus,
  planStatusSync,
  statusDrift,
  previewStatusChange,
  STAGE_TIER,
  STAGE_PHASE,
  CONTACTED_OR_BEYOND,
  QUALIFIED_OR_BEYOND,
  SIGNED_STATUSES,
  LOST_STATUSES,
  statusIn,
  type CanonicalStage,
  type StatusOption,
} from './leadStatusSync';
import { lostReason, type LeadPipeline } from './leadPipeline';

const FALLBACK = [
  'New', 'Open', 'Attempted Contact', 'Contacted', 'Connected',
  'In Progress', 'Qualified', 'Unqualified', 'Open Deal', 'Lost',
];
const opts = (names: string[]): StatusOption[] => names.map((n) => ({ id: n, name: n }));
const DEFAULT_OPTS = opts(FALLBACK);

const ctx = (over: Partial<Parameters<typeof planStatusSync>[0]> = {}) => ({
  pipeline: {} as LeadPipeline,
  currentStatus: 'New',
  statusOptions: DEFAULT_OPTS,
  statusesLoaded: true,
  enabled: true,
  ...over,
});

// ── deriveStage: one rule per phase ──────────────────────────────────────────

describe('deriveStage — phase 1 outreach', () => {
  it('no pipeline at all yields no signal', () => {
    const d = deriveStage({});
    expect(d.stage).toBe('new');
    expect(d.phase).toBe(0);
  });
  it('attempted, no answer', () => {
    expect(deriveStage({ outreachStatus: 'attempted_no_answer' }).stage).toBe('attempted');
  });
  it('contacted', () => {
    expect(deriveStage({ outreachStatus: 'contacted' }).stage).toBe('contacted');
  });
  it('a booked meeting outranks a bare "contacted"', () => {
    const d = deriveStage({ outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' });
    expect(d.stage).toBe('meeting_scheduled');
  });
  it('a follow-up is still only "contacted" — it must not outrank a booked meeting', () => {
    const followUp = deriveStage({ outreachStatus: 'contacted', contactOutcome: 'follow_up' });
    const booked = deriveStage({ outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' });
    expect(followUp.stage).toBe('contacted');
    expect(STAGE_TIER[followUp.stage]!).toBeLessThan(STAGE_TIER[booked.stage]!);
  });
  it('not interested is an explicit terminal', () => {
    const d = deriveStage({ outreachStatus: 'contacted', contactOutcome: 'not_interested' });
    expect(d.stage).toBe('lost');
    expect(d.terminal).toBe(true);
    expect(d.certainty).toBe('explicit');
  });
});

describe('deriveStage — phase 2 meeting', () => {
  it('attended', () => {
    expect(deriveStage({ meetingAttended: true }).stage).toBe('meeting_held');
  });
  it('still interested after the meeting is Qualified', () => {
    expect(deriveStage({ meetingAttended: true, stillInterested: true }).stage).toBe('qualified');
  });
  it('not interested after the meeting is lost', () => {
    const d = deriveStage({ meetingAttended: true, stillInterested: false });
    expect(d.stage).toBe('lost');
    expect(d.terminal).toBe(true);
  });
  it('a bare no-show is INFERRED, so it is never auto-written', () => {
    const d = deriveStage({ meetingAttended: false });
    expect(d.stage).toBe('lost');
    expect(d.certainty).toBe('inferred');
  });
  it('a no-show who is still interested is Qualified, not lost', () => {
    // Someone who missed a call but has since confirmed interest has not
    // dropped out — and the badge must not contradict the tracker banner.
    const p: LeadPipeline = { meetingAttended: false, stillInterested: true };
    expect(deriveStage(p).stage).toBe('qualified');
    expect(lostReason(p)).toBeNull();
  });
});

describe('deriveStage — phase 3 contract', () => {
  it('contract sent', () => {
    expect(deriveStage({ contractStatus: 'yes' }).stage).toBe('contract_sent');
  });
  it('contract still to be sent has its own stage', () => {
    expect(deriveStage({ contractStatus: 'to_be_sent' }).stage).toBe('contract_pending');
  });
  it('we rejected them is Unqualified, not Lost', () => {
    const d = deriveStage({ contractStatus: 'profile_rejected' });
    expect(d.stage).toBe('unqualified');
    expect(d.terminal).toBe(true);
  });
  it('they walked away is Lost', () => {
    expect(deriveStage({ contractStatus: 'no_longer_interested' }).stage).toBe('lost');
  });
});

describe('deriveStage — phase 4/5 signature and deposit', () => {
  it('signed', () => {
    expect(deriveStage({ contractSigned: 'yes' }).stage).toBe('signed');
  });
  it('awaiting signature reads as "contract out", never as a won deal', () => {
    expect(deriveStage({ contractSigned: 'pending' }).stage).toBe('contract_sent');
  });
  it('declined signature is lost', () => {
    expect(deriveStage({ contractSigned: 'no' }).stage).toBe('lost');
  });
  it('deposit paid', () => {
    expect(deriveStage({ depositPaid: true }).stage).toBe('deposit_paid');
  });
  it('deposit NOT paid is not a setback — it is just the default', () => {
    const d = deriveStage({ contractSigned: 'yes', depositPaid: false });
    expect(d.stage).toBe('signed');
  });
});

describe('deriveStage — precedence: the deepest phase wins', () => {
  it('a banked deposit outranks a stale phase-1 "not interested"', () => {
    const d = deriveStage({ contactOutcome: 'not_interested', depositPaid: true });
    expect(d.stage).toBe('deposit_paid');
    expect(d.terminal).toBe(false);
  });
  it('a declined signature outranks "still interested" from the meeting', () => {
    const d = deriveStage({ stillInterested: true, contractSigned: 'no' });
    expect(d.stage).toBe('lost');
  });
  it('a stated "not interested" outranks an unsigned contract still sitting in their inbox', () => {
    // "Awaiting signature" is the absence of a signature, not a new fact about
    // intent — so it must not resurrect a lead who told us they are out.
    const d = deriveStage({ stillInterested: false, contractSigned: 'pending' });
    expect(d.stage).toBe('lost');
  });

  it('a pending signature still registers when nothing else is known', () => {
    expect(deriveStage({ contractSigned: 'pending' }).stage).toBe('contract_sent');
  });

  it('a pending signature agrees with a sent contract', () => {
    expect(deriveStage({ contractStatus: 'yes', contractSigned: 'pending' }).stage).toBe('contract_sent');
  });
  it('every stage declares the phase it came from', () => {
    for (const stage of Object.keys(STAGE_PHASE) as CanonicalStage[]) {
      expect(typeof STAGE_PHASE[stage]).toBe('number');
    }
  });
});

// ── Invariant 1: dates never move the status ─────────────────────────────────

describe('INVARIANT — dates are never derivation inputs', () => {
  const DATE_FIELDS: (keyof LeadPipeline)[] = [
    'outreachDate', 'meetingDate', 'contractSentDate', 'signatureDate', 'paymentDate',
  ];
  const CATEGORICAL_STATES: LeadPipeline[] = [
    {},
    { outreachStatus: 'attempted_no_answer' },
    { outreachStatus: 'contacted' },
    { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
    { outreachStatus: 'contacted', contactOutcome: 'follow_up' },
    { outreachStatus: 'contacted', contactOutcome: 'not_interested' },
    { meetingAttended: true },
    { meetingAttended: false },
    { meetingAttended: true, stillInterested: true },
    { meetingAttended: true, stillInterested: false },
    { contractStatus: 'yes' },
    { contractStatus: 'to_be_sent' },
    { contractStatus: 'profile_rejected' },
    { contractStatus: 'no_longer_interested' },
    { contractSigned: 'yes' },
    { contractSigned: 'pending' },
    { contractSigned: 'no' },
    { depositPaid: true },
    { depositPaid: false },
  ];

  it('adding or clearing any date leaves the stage identical', () => {
    for (const base of CATEGORICAL_STATES) {
      const expected = deriveStage(base).stage;
      for (const field of DATE_FIELDS) {
        expect(deriveStage({ ...base, [field]: '2026-05-13' }).stage).toBe(expected);
        expect(deriveStage({ ...base, [field]: '' }).stage).toBe(expected);
      }
      const allDates = DATE_FIELDS.reduce<LeadPipeline>((acc, f) => ({ ...acc, [f]: '2026-05-13' }), {});
      expect(deriveStage({ ...base, ...allDates }).stage).toBe(expected);
    }
  });
});

// ── Invariant: recording progress never moves the lead backwards ─────────────

describe('INVARIANT — a positive edit never lowers the derived stage', () => {
  // Exhaustive over every combination of the 7 categorical fields. This caught
  // a real bug: with `contractSigned:'pending'` evaluated as a last-resort
  // fallback, recording the earlier outreach on a lead awaiting signature
  // dropped it from contract_sent all the way back to contacted.
  const OUTREACH = [undefined, 'attempted_no_answer', 'contacted'] as const;
  const OUTCOME = [undefined, 'meeting_scheduled', 'follow_up', 'not_interested'] as const;
  const BOOL = [undefined, true, false] as const;
  const CSTATUS = [undefined, 'yes', 'to_be_sent', 'profile_rejected', 'no_longer_interested'] as const;
  const CSIGNED = [undefined, 'yes', 'pending', 'no'] as const;

  function* everyState(): Generator<LeadPipeline> {
    for (const o of OUTREACH) for (const c of OUTCOME) for (const ma of BOOL) for (const si of BOOL)
      for (const cs of CSTATUS) for (const sg of CSIGNED) for (const dp of BOOL) {
        const p: LeadPipeline = {};
        if (o !== undefined) p.outreachStatus = o;
        if (c !== undefined) p.contactOutcome = c;
        if (ma !== undefined) p.meetingAttended = ma;
        if (si !== undefined) p.stillInterested = si;
        if (cs !== undefined) p.contractStatus = cs;
        if (sg !== undefined) p.contractSigned = sg;
        if (dp !== undefined) p.depositPaid = dp;
        yield p;
      }
  }

  const POSITIVE_EDITS: { label: string; patch: Partial<LeadPipeline> }[] = [
    { label: "outreachStatus:'attempted_no_answer'", patch: { outreachStatus: 'attempted_no_answer' } },
    { label: "outreachStatus:'contacted'", patch: { outreachStatus: 'contacted' } },
    { label: "contactOutcome:'follow_up'", patch: { contactOutcome: 'follow_up' } },
    { label: "contactOutcome:'meeting_scheduled'", patch: { contactOutcome: 'meeting_scheduled' } },
    { label: 'meetingAttended:true', patch: { meetingAttended: true } },
    { label: 'stillInterested:true', patch: { stillInterested: true } },
    { label: "contractStatus:'to_be_sent'", patch: { contractStatus: 'to_be_sent' } },
    { label: "contractStatus:'yes'", patch: { contractStatus: 'yes' } },
    { label: "contractSigned:'pending'", patch: { contractSigned: 'pending' } },
    { label: "contractSigned:'yes'", patch: { contractSigned: 'yes' } },
    { label: 'depositPaid:true', patch: { depositPaid: true } },
  ];

  it('holds for every reachable pipeline state', () => {
    const regressions: string[] = [];
    for (const base of everyState()) {
      const before = deriveStage(base);
      const tBefore = STAGE_TIER[before.stage];
      if (tBefore === null) continue;          // terminal: off the ladder
      for (const edit of POSITIVE_EDITS) {
        const field = Object.keys(edit.patch)[0] as keyof LeadPipeline;
        if (base[field] !== undefined) continue; // only genuinely new information
        const after = deriveStage({ ...base, ...edit.patch });
        const tAfter = STAGE_TIER[after.stage];
        if (tAfter === null) continue;          // moved to a terminal
        if (tAfter < tBefore) {
          regressions.push(
            `${edit.label}: ${before.stage}(${tBefore}) -> ${after.stage}(${tAfter}) from ${JSON.stringify(base)}`,
          );
        }
      }
    }
    expect(regressions.slice(0, 5)).toEqual([]);
  });
});

// ── Invariant 2: hostile JSON reads as unset ─────────────────────────────────

describe('INVARIANT — malformed pipeline JSON reads as unset, never as progress', () => {
  const HOSTILE: Record<string, unknown>[] = [
    { depositPaid: 'yes' },
    { depositPaid: 1 },
    { meetingAttended: 1 },
    { meetingAttended: 'true' },
    { stillInterested: 'false' },
    { contractSigned: 'YES' },
    { contractSigned: 'Yes' },
    { contractStatus: 'Yes' },
    { outreachStatus: 'Contacted' },
    { contactOutcome: 'Meeting Scheduled' },
  ];
  it('none of these look like progress', () => {
    for (const raw of HOSTILE) {
      const d = deriveStage(raw as LeadPipeline);
      expect(d.stage).toBe('new');
      expect(d.phase).toBe(0);
    }
  });
});

// ── Vocabulary resolution ────────────────────────────────────────────────────

describe('resolveStatus — default vocabulary', () => {
  it('maps each stage onto the shipped fallback list sensibly', () => {
    const got = (s: CanonicalStage) => resolveStatus(s, DEFAULT_OPTS)?.name;
    expect(got('attempted')).toBe('Attempted Contact');
    expect(got('contacted')).toBe('Contacted');
    // 'Connected' now belongs to meeting_HELD, per the client's definition
    // ("Showed up? = Yes"). The old list has no booked-meeting label at all, so
    // meeting_scheduled correctly degrades one rung to Contacted rather than
    // over-claiming that we have met them.
    expect(got('meeting_scheduled')).toBe('Contacted');
    expect(got('meeting_held')).toBe('Connected');
    expect(got('qualified')).toBe('Qualified');
    expect(got('signed')).toBe('Open Deal');
    expect(got('deposit_paid')).toBe('Open Deal');
    expect(got('unqualified')).toBe('Unqualified');
    expect(got('lost')).toBe('Lost');
  });

  it('the contract stages degrade to Qualified when the org has no contract status', () => {
    const sent = resolveStatus('contract_sent', DEFAULT_OPTS);
    expect(sent?.name).toBe('Qualified');
    expect(sent?.match).toBe('fallback');
    expect(sent?.canonical).toBe('qualified');
  });

  it('never resolves a forward move onto bare "Open"', () => {
    // "Open" reads as "untouched" to every rep, so it must never be a target.
    const stages: CanonicalStage[] = [
      'attempted', 'contacted', 'meeting_scheduled', 'meeting_held',
      'qualified', 'contract_pending', 'contract_sent', 'signed', 'deposit_paid',
    ];
    for (const s of stages) {
      expect(resolveStatus(s, DEFAULT_OPTS)?.name).not.toBe('Open');
    }
  });
});

describe('resolveStatus — richer and stranger vocabularies', () => {
  it('prefers the precise status when an org configures several', () => {
    const rich = opts(['New', 'Contacted', 'Negotiation', 'Contract Signed', 'Qualified', 'Lost']);
    // "Contract Signed" must win over the coarser "Open Deal"-style catch-alls.
    expect(resolveStatus('signed', rich)?.name).toBe('Contract Signed');
    // With no "Contract Sent" configured, "Negotiation" is the closest fit.
    expect(resolveStatus('contract_sent', rich)?.name).toBe('Negotiation');
  });

  it('an explicit "Contract Sent" beats the coarser "Negotiation"', () => {
    const rich = opts(['New', 'Negotiation', 'Contract Sent', 'Lost']);
    expect(resolveStatus('contract_sent', rich)?.name).toBe('Contract Sent');
  });

  it('paying a deposit never moves a lead backwards out of the org’s top status', () => {
    // Regression: 'Open Deal' and 'Closed Won' both read as the top of the
    // ladder, so if `signed` and `deposit_paid` resolved to different ones,
    // banking a deposit demoted a Closed Won lead to Open Deal — and the
    // forward-only guard could not see it, because both classify to one tier.
    const org = opts(['New', 'Contacted', 'Qualified', 'Open Deal', 'Closed Won', 'Lost']);
    const signed = resolveStatus('signed', org)?.name;
    const deposit = resolveStatus('deposit_paid', org)?.name;
    expect(deposit).toBe(signed);

    const plan = planStatusSync(ctx({
      pipeline: { contractSigned: 'yes', depositPaid: true },
      currentStatus: signed!,
      statusOptions: org,
    }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'noop' });
  });

  it('still prefers a deposit-specific status when the org has one', () => {
    const org = opts(['New', 'Qualified', 'Closed Won', 'Deposit Paid', 'Lost']);
    expect(resolveStatus('deposit_paid', org)?.name).toBe('Deposit Paid');
    expect(resolveStatus('signed', org)?.name).toBe('Closed Won');
  });

  it('degrades downward only — never claims more progress than the vocabulary has', () => {
    const thin = opts(['New', 'Contacted', 'Lost']);
    const signed = resolveStatus('signed', thin);
    expect(signed?.name).toBe('Contacted');
    expect(signed?.canonical).toBe('contacted');
  });

  it('returns null rather than inventing a status', () => {
    expect(resolveStatus('qualified', opts(['Alpha', 'Beta']))).toBeNull();
    expect(resolveStatus('qualified', [])).toBeNull();
  });

  it('terminals never degrade — a lead is lost or it is not', () => {
    expect(resolveStatus('lost', opts(['New', 'Contacted']))).toBeNull();
    expect(resolveStatus('unqualified', opts(['New', 'Contacted']))).toBeNull();
  });

  it('an org override rescues a fully renamed vocabulary', () => {
    const french = opts(['Nouveau', 'Contacté', 'Qualifié', 'Perdu']);
    expect(resolveStatus('qualified', french)).toBeNull();
    const resolved = resolveStatus('qualified', french, { qualified: 'Qualifié' });
    expect(resolved?.name).toBe('Qualifié');
  });

  it('overrides are honoured on the downgrade path too', () => {
    // A renamed vocabulary pins several stages; if only the derived stage
    // consulted the pins, the very orgs overrides exist for would still get
    // nothing back for a stage they had not pinned.
    const french = opts(['Nouveau', 'Contacté', 'Qualifié', 'Perdu']);
    const resolved = resolveStatus('deposit_paid', french, { qualified: 'Qualifié' });
    expect(resolved?.name).toBe('Qualifié');
    expect(resolved?.match).toBe('fallback');
    expect(resolved?.canonical).toBe('qualified');
  });

  it('matching ignores case, spacing and separators', () => {
    expect(resolveStatus('qualified', opts(['  QUALIFIED  ']))?.name).toBe('  QUALIFIED  ');
    expect(resolveStatus('attempted', opts(['attempted-contact']))?.name).toBe('attempted-contact');
  });

  it('only exposes an id when it is a real GUID', () => {
    const guid = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';
    expect(resolveStatus('qualified', [{ id: guid, name: 'Qualified' }])?.id).toBe(guid);
    expect(resolveStatus('qualified', [{ id: 'Qualified', name: 'Qualified' }])?.id).toBeUndefined();
  });
});

describe('classifyStatusLabel / isStickyStatus', () => {
  it('recognises the shipped vocabulary', () => {
    expect(classifyStatusLabel('Qualified')).toBe('qualified');
    expect(classifyStatusLabel('Open Deal')).toBe('signed');
    expect(classifyStatusLabel('Attempted Contact')).toBe('attempted');
  });
  it('refuses to guess at an unfamiliar label', () => {
    expect(classifyStatusLabel('Cold Outreach Q3')).toBeNull();
    expect(classifyStatusLabel('Partner Referral — Pending')).toBeNull();
    expect(classifyStatusLabel('')).toBeNull();
  });
  it('flags deliberate parking statuses', () => {
    expect(isStickyStatus('Do Not Contact')).toBe(true);
    expect(isStickyStatus('unsubscribed')).toBe(true);
    expect(isStickyStatus('Qualified')).toBe(false);
  });
});

// ── planStatusSync: the disposition ladder ───────────────────────────────────

describe('planStatusSync — applies a straightforward advance', () => {
  it('recording a booked meeting moves New to Contacted, not Connected', () => {
    // Booking is not connecting — a large share of booked meetings no-show, so
    // the status must not claim contact was made until attendance is recorded.
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
      currentStatus: 'New',
    }));
    expect(plan.kind).toBe('apply');
    if (plan.kind !== 'apply') return;
    expect(plan.to.name).toBe('Contacted');
    expect(plan.from).toBe('New');
  });

  it('recording ATTENDANCE is what moves a lead to Connected', () => {
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingAttended: true },
      currentStatus: 'New',
    }));
    expect(plan.kind).toBe('apply');
    if (plan.kind !== 'apply') return;
    expect(plan.to.name).toBe('Connected');
  });

  it('names the cause and carries a machine-readable trailer', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Connected',
    }));
    if (plan.kind !== 'apply') throw new Error('expected apply');
    expect(plan.activity.body).toContain('Still interested after the meeting');
    expect(plan.activity.body).toMatch(/\[auto-status rule=phase2_qualified canonical=qualified match=\w+ phase=2\]/);
  });

  it('agrees with lostReason() when only one negative is recorded', () => {
    const p: LeadPipeline = { contactOutcome: 'not_interested' };
    const plan = planStatusSync(ctx({ pipeline: p, currentStatus: 'Contacted' }));
    if (plan.kind !== 'apply') throw new Error('expected apply');
    expect(plan.derived.because).toBe(lostReason(p));
    expect(plan.to.name).toBe('Lost');
  });

  it('names the signal that actually decided the stage, not the shallowest one', () => {
    // lostReason() scans shallowest-first, so it would say "Not interested"
    // here; the status was decided by the phase-4 declined signature.
    const p: LeadPipeline = { contactOutcome: 'not_interested', contractSigned: 'no' };
    const d = deriveStage(p);
    expect(d.rule).toBe('phase4_declined');
    expect(d.because).toBe('Contract declined');
    expect(lostReason(p)).toBe('Not interested');
  });

  it('an Unqualified stage says we rejected them, not that they lost interest', () => {
    const d = deriveStage({ stillInterested: false, contractStatus: 'profile_rejected' });
    expect(d.stage).toBe('unqualified');
    expect(d.because).toBe('Profile rejected');
  });
});

describe('planStatusSync — refuses to write when a human might disagree', () => {
  it('stays silent while the org status list is still loading', () => {
    const plan = planStatusSync(ctx({
      pipeline: { depositPaid: true }, statusesLoaded: false,
    }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'statuses_loading' });
  });

  it('never touches a converted lead', () => {
    const plan = planStatusSync(ctx({ pipeline: { depositPaid: true }, isConverted: true }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'converted' });
  });

  it('honours the per-user opt-out', () => {
    const plan = planStatusSync(ctx({ pipeline: { depositPaid: true }, enabled: false }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'disabled' });
  });

  it('does nothing for an empty pipeline, so clearing does not drag the status back', () => {
    const plan = planStatusSync(ctx({ pipeline: {}, currentStatus: 'Qualified' }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'no_signal' });
  });

  it('is a no-op when the status already matches', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true }, currentStatus: 'Qualified',
    }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'noop' });
  });

  it('stays silent when the org has no suitable status at all', () => {
    const plan = planStatusSync(ctx({
      pipeline: { contactOutcome: 'not_interested' },
      currentStatus: 'Alpha',
      statusOptions: opts(['Alpha', 'Beta']),
    }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'unresolvable' });
  });

  it('only SUGGESTS over a deliberate parking status', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Do Not Contact',
      statusOptions: opts([...FALLBACK, 'Do Not Contact']),
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'sticky_status' });
  });

  it('only SUGGESTS over an unrecognised custom status', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Cold Outreach Q3',
      statusOptions: opts([...FALLBACK, 'Cold Outreach Q3']),
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'unrecognised_status' });
  });

  it('never resurrects a lead a human marked Lost by hand', () => {
    // Off-ladder terminal as the current status: the pipeline showing earlier
    // outreach activity is not grounds for overriding a human's call.
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted' },
      currentStatus: 'Lost',
      manualStatus: 'Lost',
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'manual_change' });
  });

  it('still applies when the live status is the one we last wrote', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Connected',
      lastAutoStatus: 'Connected',
    }));
    expect(plan.kind).toBe('apply');
  });

  it('never marks a lead dead for one missed meeting', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: false }, currentStatus: 'Connected',
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'inferred' });
  });

  it('auto-writes a STATED loss, unlike an inferred one', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: false }, currentStatus: 'Connected',
    }));
    expect(plan.kind).toBe('apply');
  });
});

describe('planStatusSync — forward-only', () => {
  it('suggests rather than demotes when the pipeline falls behind the status', () => {
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted' }, currentStatus: 'Open Deal',
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'would_regress' });
  });

  it('a terminal is never a regression — moving to Lost always applies', () => {
    const plan = planStatusSync(ctx({
      pipeline: { contractSigned: 'no' }, currentStatus: 'Open Deal',
    }));
    expect(plan.kind).toBe('apply');
    if (plan.kind !== 'apply') return;
    expect(plan.to.name).toBe('Lost');
  });

  it('moving away from a terminal is always forward', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true }, currentStatus: 'Lost',
    }));
    expect(plan.kind).toBe('apply');
    if (plan.kind !== 'apply') return;
    expect(plan.to.name).toBe('Qualified');
  });
});

// ── Drift ────────────────────────────────────────────────────────────────────

describe('statusDrift — computed from state, so it survives a reload', () => {
  it('reports a status that has run ahead of the pipeline', () => {
    const drift = statusDrift(ctx({
      pipeline: { outreachStatus: 'contacted' }, currentStatus: 'Open Deal',
    }));
    expect(drift?.suggested.name).toBe('Contacted');
  });

  it('reports a cleared pipeline still wearing a progressed status', () => {
    const drift = statusDrift(ctx({ pipeline: {}, currentStatus: 'Qualified' }));
    expect(drift?.suggested.name).toBe('New');
  });

  it('is quiet when a cleared pipeline already reads New', () => {
    expect(statusDrift(ctx({ pipeline: {}, currentStatus: 'New' }))).toBeNull();
  });

  it('is quiet when status and pipeline agree', () => {
    const drift = statusDrift(ctx({
      pipeline: { meetingAttended: true, stillInterested: true }, currentStatus: 'Qualified',
    }));
    expect(drift).toBeNull();
  });

  it('is quiet on converted leads', () => {
    const drift = statusDrift(ctx({
      pipeline: { depositPaid: true }, currentStatus: 'New', isConverted: true,
    }));
    expect(drift).toBeNull();
  });
});

// ── Preview ──────────────────────────────────────────────────────────────────

describe('previewStatusChange — a hint can never promise what the click would not do', () => {
  it('shows the status the edit would produce', () => {
    const to = previewStatusChange({ outreachStatus: 'contacted' }, { meetingAttended: true }, ctx());
    expect(to?.name).toBe('Connected');
  });

  it('shows nothing when the edit would not move the status', () => {
    const to = previewStatusChange(
      { meetingAttended: true, stillInterested: true },
      { signatureDate: '2026-05-13' },
      ctx({ currentStatus: 'Qualified' }),
    );
    expect(to).toBeNull();
  });

  it('agrees with planStatusSync for every single-field edit', () => {
    const base: LeadPipeline = { outreachStatus: 'contacted' };
    const patches: Partial<LeadPipeline>[] = [
      { contactOutcome: 'meeting_scheduled' },
      { contactOutcome: 'follow_up' },
      { contactOutcome: 'not_interested' },
      { meetingAttended: true },
      { stillInterested: true },
      { stillInterested: false },
      { contractStatus: 'yes' },
      { contractStatus: 'profile_rejected' },
      { contractSigned: 'yes' },
      { depositPaid: true },
    ];
    for (const patch of patches) {
      const preview = previewStatusChange(base, patch, ctx());
      const plan = planStatusSync(ctx({ pipeline: { ...base, ...patch } }));
      if (plan.kind === 'apply') expect(preview?.name).toBe(plan.to.name);
      else expect(preview).toBeNull();
    }
  });
});

// ── Cross-module agreement ───────────────────────────────────────────────────

describe('the status strip and the tracker banner never contradict each other', () => {
  const STATES: LeadPipeline[] = [
    { outreachStatus: 'contacted', contactOutcome: 'not_interested' },
    { meetingAttended: false },
    { meetingAttended: false, stillInterested: true },
    { meetingAttended: true, stillInterested: false },
    { contractStatus: 'profile_rejected' },
    { contractStatus: 'no_longer_interested' },
    { contractSigned: 'no' },
    { contactOutcome: 'not_interested', depositPaid: true },
    { stillInterested: false, contractSigned: 'pending' },
    { stillInterested: true, contractSigned: 'pending' },
  ];
  it('a terminal stage always has a lostReason() to show in the banner', () => {
    for (const p of STATES) {
      const d = deriveStage(p);
      if (d.terminal) expect(lostReason(p), JSON.stringify(p)).not.toBeNull();
    }
  });

  it('a positive stage that overrides a recorded negative declares the conflict', () => {
    // Silently discarding a negative is what makes users distrust automation —
    // so whenever the banner would still say "dropped out", the derived stage
    // must name what it overrode.
    for (const p of STATES) {
      const d = deriveStage(p);
      if (!d.terminal && lostReason(p) !== null) {
        expect(d.conflicts, JSON.stringify(p)).not.toHaveLength(0);
      }
    }
  });

  it('a banked deposit overrides an earlier refusal and says so', () => {
    const d = deriveStage({ contactOutcome: 'not_interested', depositPaid: true });
    expect(d.stage).toBe('deposit_paid');
    expect(d.conflicts).toContain('Not interested at outreach');
  });

  it('a clean positive pipeline declares no conflicts', () => {
    const d = deriveStage({ outreachStatus: 'contacted', meetingAttended: true, stillInterested: true });
    expect(d.conflicts).toEqual([]);
  });
});

describe('planStatusSync — releasable manual pin', () => {
  it('holds while the pipeline has not moved past the hand-picked status', () => {
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
      currentStatus: 'In Progress',   // human moved it ahead of Connected
      manualStatus: 'In Progress',
      manualHeldTier: STAGE_TIER.meeting_scheduled,
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'manual_change' });
  });

  it('releases once the pipeline genuinely overtakes the hand-picked status', () => {
    const plan = planStatusSync(ctx({
      pipeline: { depositPaid: true },
      currentStatus: 'In Progress',
      manualStatus: 'In Progress',
      manualHeldTier: STAGE_TIER.meeting_scheduled,
    }));
    expect(plan.kind).toBe('apply');
  });

  it('holds for good on a hand-set terminal, even when the pipeline advances', () => {
    // Terminals are off-ladder (tier null) so the release condition can never be
    // met. Deliberate: a human writing a lead off outranks our inference, and
    // the drift strip still offers one-click revival.
    const plan = planStatusSync(ctx({
      pipeline: { depositPaid: true },
      currentStatus: 'Lost',
      manualStatus: 'Lost',
      manualHeldTier: STAGE_TIER.deposit_paid,   // already banked when written off
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'manual_change' });
  });
});

describe('statusDrift stays visible when auto-sync is switched off', () => {
  it('still reports the disagreement, because the strip is then the only fix', () => {
    // Drift is a statement about the data, not about the automation.
    const drift = statusDrift({
      pipeline: { depositPaid: true },
      currentStatus: 'New',
      statusOptions: DEFAULT_OPTS,
      statusesLoaded: true,
    });
    expect(drift?.suggested.name).toBe('Open Deal');
  });
});

describe('planStatusSync — reports every applicable hold', () => {
  it('lists both the pin and the unrecognised status', () => {
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted' },
      currentStatus: 'Cold Outreach Q3',
      statusOptions: opts([...FALLBACK, 'Cold Outreach Q3']),
      manualStatus: 'Cold Outreach Q3',
    }));
    if (plan.kind !== 'suggest') throw new Error('expected suggest');
    expect(plan.reasons).toContain('unrecognised_status');
    expect(plan.reasons).toContain('manual_change');
  });
});

describe('planStatusSync — defers to an org whose ladder is shaped differently', () => {
  it('suggests instead of applying when displayOrder contradicts our tiers', () => {
    // This org considers "Qualified" EARLIER than "Contacted".
    const weird: StatusOption[] = [
      { id: 'q', name: 'Qualified', displayOrder: 1 },
      { id: 'c', name: 'Contacted', displayOrder: 9 },
    ];
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Contacted',
      statusOptions: weird,
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'vocabulary_mismatch' });
  });

  it('applies happily when the org order agrees with ours', () => {
    const sane: StatusOption[] = [
      { id: 'c', name: 'Contacted', displayOrder: 1 },
      { id: 'q', name: 'Qualified', displayOrder: 5 },
    ];
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Contacted',
      statusOptions: sane,
    }));
    expect(plan.kind).toBe('apply');
  });
});


// ── Regression: the manual hold must be ARMED by a pick, not destroyed by it ──

describe('a hand-picked status survives later pipeline edits', () => {
  // The hold used to be keyed on `lastAutoStatus` being truthy, while a manual
  // pick *cleared* that field — so picking a status removed the only thing
  // protecting it, and the next save wrote straight over the human's choice.
  it('a rep who writes a lead off as Lost is not overruled by an unrelated edit', () => {
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
      currentStatus: 'Lost',
      manualStatus: 'Lost',
    }));
    expect(plan.kind).not.toBe('apply');
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'manual_change' });
  });

  it('holds even for a lead auto-sync has never written to', () => {
    // `lastAutoStatus` divergence cannot help here — there is no sync history —
    // so only an explicit record of the pick protects it.
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Contacted',
      manualStatus: 'Contacted',
      manualHeldTier: STAGE_TIER.qualified,
      lastAutoStatus: undefined,
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'manual_change' });
  });

  it('a date-only edit never overrules a hand-set status', () => {
    // Invariant 1 says dates cannot move the status; combined with the hold, a
    // bookkeeping edit must be inert.
    const base = { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' } as const;
    for (const dateField of ['outreachDate', 'meetingDate', 'contractSentDate'] as const) {
      const plan = planStatusSync(ctx({
        pipeline: { ...base, [dateField]: '2026-05-13' },
        currentStatus: 'Lost',
        manualStatus: 'Lost',
      }));
      expect(plan.kind, dateField).not.toBe('apply');
    }
  });

  it('still catches a change made on another device, with no local record', () => {
    // Cross-client backstop: we last wrote "Connected" but the live status is
    // something else, so someone intervened where we cannot see it.
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
      currentStatus: 'Lost',
      lastAutoStatus: 'Connected',
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'manual_change' });
  });

  it('an undone status stays undone', () => {
    // undoAutoStatus records the revert as a manual pick, which is what makes
    // its own activity copy ("will suggest, not apply") true.
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'New',
      manualStatus: 'New',
      // Baseline = what the pipeline already derived when they undid it.
      manualHeldTier: STAGE_TIER.qualified,
    }));
    expect(plan).toMatchObject({ kind: 'suggest', reason: 'manual_change' });
  });

  it('a hold measured against the picked status would void itself instantly', () => {
    // Auto-sync only wrote because the pipeline derived something ABOVE the
    // status, so releasing on "derived > current status" can never hold. The
    // baseline must be the pipeline's tier at intervention time.
    const held = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },  // qualified (5)
      currentStatus: 'New',                                        // tier 0
      manualStatus: 'New',
      manualHeldTier: STAGE_TIER.qualified,
    }));
    expect(held.kind).toBe('suggest');
  });

  it('releases once the pipeline records something deeper than at intervention', () => {
    const plan = planStatusSync(ctx({
      pipeline: { depositPaid: true },                 // deposit_paid (9)
      currentStatus: 'New',
      manualStatus: 'New',
      manualHeldTier: STAGE_TIER.qualified,            // was 5 when they intervened
    }));
    expect(plan.kind).toBe('apply');
  });
});

// ── The client's requested vocabulary ────────────────────────────────────────
// The eight stages agreed with the client, in their order. Every row of their
// table is pinned below, because these labels ARE the contract.
const NEW_VOCAB = [
  'New', 'Attempted Contact', 'Contacted', 'Connected',
  'Contract Pending', 'Awaiting Signature', 'Signed', 'Lost / Not Interested',
].map((name, i) => ({ id: `s${i}`, name, displayOrder: i }));

describe('client status vocabulary — one case per row of their table', () => {
  const statusFor = (p: LeadPipeline) => {
    const d = deriveStage(p);
    return resolveStatus(d.stage, NEW_VOCAB)?.name ?? null;
  };

  it('New ← nothing recorded yet', () => {
    expect(statusFor({})).toBe('New');
  });

  it('Attempted Contact ← outreach "attempted — no answer"', () => {
    expect(statusFor({ outreachStatus: 'attempted_no_answer' })).toBe('Attempted Contact');
  });

  it('Contacted ← outreach "contacted"', () => {
    expect(statusFor({ outreachStatus: 'contacted' })).toBe('Contacted');
    // A booked meeting is still only "we have spoken" in this vocabulary —
    // their table has no separate meeting-scheduled status, so it must not
    // over-claim by resolving to Connected.
    expect(statusFor({ outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' })).toBe('Contacted');
  });

  it('Connected ← "Showed up?" = Yes, NOT a merely booked meeting', () => {
    // The one mapping that had to be corrected: 'connected' used to be a synonym
    // for meeting_scheduled. Half of booked meetings no-show; attendance is what
    // earns the label.
    expect(statusFor({ meetingAttended: true })).toBe('Connected');
  });

  it('Contract Pending ← met and interested, and also contract-to-be-sent', () => {
    expect(statusFor({ meetingAttended: true, stillInterested: true })).toBe('Contract Pending');
    expect(statusFor({ contractStatus: 'to_be_sent' })).toBe('Contract Pending');
  });

  it('Awaiting Signature ← contract sent but not yet signed', () => {
    expect(statusFor({ contractStatus: 'yes', contractSentDate: '2026-08-22' })).toBe('Awaiting Signature');
    expect(statusFor({ contractSigned: 'pending' })).toBe('Awaiting Signature');
  });

  it('Signed ← contract executed, and a paid deposit holds there', () => {
    expect(statusFor({ contractSigned: 'yes' })).toBe('Signed');
    // Their table has no deposit status, so a deposit must NOT knock the lead
    // back down the ladder — it degrades onto the same string and holds.
    expect(statusFor({ contractSigned: 'yes', depositPaid: true })).toBe('Signed');
    expect(statusFor({ depositPaid: true })).toBe('Signed');
  });

  it('Lost / Not Interested ← every one of the three triggers they listed', () => {
    expect(statusFor({ contactOutcome: 'not_interested' })).toBe('Lost / Not Interested');
    expect(statusFor({ meetingAttended: true, stillInterested: false })).toBe('Lost / Not Interested');
    expect(statusFor({ contractStatus: 'no_longer_interested' })).toBe('Lost / Not Interested');
    // profile_rejected derives to `unqualified`, a TERMINAL that never degrades
    // down the ladder. Without the combined label on both terminals it would
    // resolve to nothing and the status would silently fail to move.
    expect(statusFor({ contractStatus: 'profile_rejected' })).toBe('Lost / Not Interested');
  });

  it('never invents a label outside the org list', () => {
    const allowed = new Set(NEW_VOCAB.map((o) => o.name));
    const STATES: LeadPipeline[] = [
      {}, { outreachStatus: 'attempted_no_answer' }, { outreachStatus: 'contacted' },
      { contactOutcome: 'follow_up' }, { contactOutcome: 'meeting_scheduled' },
      { contactOutcome: 'not_interested' }, { meetingAttended: true }, { meetingAttended: false },
      { meetingAttended: true, stillInterested: true }, { stillInterested: false },
      { contractStatus: 'to_be_sent' }, { contractStatus: 'yes' },
      { contractStatus: 'profile_rejected' }, { contractStatus: 'no_longer_interested' },
      { contractSigned: 'pending' }, { contractSigned: 'yes' }, { contractSigned: 'no' },
      { depositPaid: true },
    ];
    for (const p of STATES) {
      const name = statusFor(p);
      if (name !== null) expect(allowed, JSON.stringify(p)).toContain(name);
    }
  });

  it('is monotone across the happy path — the status never goes backwards', () => {
    const order = NEW_VOCAB.map((o) => o.name);
    const journey: LeadPipeline[] = [
      {},
      { outreachStatus: 'attempted_no_answer' },
      { outreachStatus: 'contacted' },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20' },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20', meetingAttended: true },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20', meetingAttended: true, stillInterested: true },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20', meetingAttended: true, stillInterested: true, contractStatus: 'yes', contractSentDate: '2026-08-22' },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20', meetingAttended: true, stillInterested: true, contractStatus: 'yes', contractSentDate: '2026-08-22', contractSigned: 'yes', signatureDate: '2026-08-25' },
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20', meetingAttended: true, stillInterested: true, contractStatus: 'yes', contractSentDate: '2026-08-22', contractSigned: 'yes', signatureDate: '2026-08-25', depositPaid: true, paymentDate: '2026-08-28' },
    ];
    let last = -1;
    for (const p of journey) {
      const idx = order.indexOf(statusFor(p)!);
      expect(idx, `${JSON.stringify(p)} → ${statusFor(p)}`).toBeGreaterThanOrEqual(last);
      last = idx;
    }
  });

  it('still works for an organisation left on the OLD vocabulary', () => {
    // Existing orgs are migrated, but the code must not depend on that having
    // happened — otherwise a half-migrated tenant silently stops syncing.
    const old = resolveStatus('meeting_held', DEFAULT_OPTS)?.name;
    expect(old).toBeTruthy();
    expect(resolveStatus('signed', DEFAULT_OPTS)?.name).toBeTruthy();
    expect(resolveStatus('lost', DEFAULT_OPTS)?.name).toBe('Lost');
  });
});

// ── Status groups ────────────────────────────────────────────────────────────
// These are now the single definition behind the lead score card, the Leads
// filter cards, the API stats, the backend stats and the reports. The bug that
// made this necessary was `status.includes('qualified')` in four files, so the
// membership itself is worth pinning.

describe('status groups — the shared definition four call sites depend on', () => {
  const CURRENT = [
    'New', 'Attempted Contact', 'Contacted', 'Connected',
    'Contract Pending', 'Awaiting Signature', 'Signed', 'Lost / Not Interested',
  ];

  it('QUALIFIED_OR_BEYOND is exactly met-and-interested onwards', () => {
    expect(CURRENT.filter((s) => statusIn(s, QUALIFIED_OR_BEYOND)))
      .toEqual(['Contract Pending', 'Awaiting Signature', 'Signed']);
  });

  it('CONTACTED_OR_BEYOND excludes New and Attempted Contact', () => {
    // "Attempted — no answer" is not contact. Counting it as such was one of the
    // ways the old funnel overstated progress.
    expect(statusIn('New', CONTACTED_OR_BEYOND)).toBe(false);
    expect(statusIn('Attempted Contact', CONTACTED_OR_BEYOND)).toBe(false);
    expect(statusIn('Contacted', CONTACTED_OR_BEYOND)).toBe(true);
    expect(statusIn('Connected', CONTACTED_OR_BEYOND)).toBe(true);
  });

  it('INVARIANT: qualified is a subset of contacted', () => {
    // If this ever fails a funnel built on these groups could widen.
    for (const s of QUALIFIED_OR_BEYOND) {
      expect(statusIn(s, CONTACTED_OR_BEYOND), `${s} qualified but not contacted`).toBe(true);
    }
  });

  it('INVARIANT: no status is both a positive stage and a terminal loss', () => {
    for (const s of [...CURRENT, 'Qualified', 'Open Deal', 'Unqualified', 'Lost']) {
      const positive = statusIn(s, CONTACTED_OR_BEYOND) || statusIn(s, QUALIFIED_OR_BEYOND);
      const lost = statusIn(s, LOST_STATUSES);
      expect(positive && lost, `${s} counted as both`).toBe(false);
    }
  });

  it('REGRESSION: "Unqualified" is never qualified', () => {
    // The original reported inaccuracy — substring matching said otherwise.
    expect(statusIn('Unqualified', QUALIFIED_OR_BEYOND)).toBe(false);
    expect(statusIn('Unqualified', CONTACTED_OR_BEYOND)).toBe(false);
    expect(statusIn('Unqualified', LOST_STATUSES)).toBe(true);
  });

  it('recognises both terminal spellings, old and new', () => {
    for (const s of ['Lost', 'Unqualified', 'Lost / Not Interested']) {
      expect(statusIn(s, LOST_STATUSES), s).toBe(true);
    }
  });

  it('is tolerant of casing and padding, and rejects empty', () => {
    expect(statusIn('  signed  ', SIGNED_STATUSES)).toBe(true);
    expect(statusIn('SIGNED', SIGNED_STATUSES)).toBe(true);
    expect(statusIn('', QUALIFIED_OR_BEYOND)).toBe(false);
    expect(statusIn(undefined, QUALIFIED_OR_BEYOND)).toBe(false);
  });

  it('does not match on a partial word', () => {
    // The failure mode the groups exist to prevent.
    expect(statusIn('Not Signed', SIGNED_STATUSES)).toBe(false);
    expect(statusIn('Pre-Qualified', QUALIFIED_OR_BEYOND)).toBe(false);
  });
});
