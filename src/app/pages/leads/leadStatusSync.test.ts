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
  it('a bare no-show is real progress, not a write-off', () => {
    // This used to derive `lost` with certainty 'inferred' and be withheld as a
    // suggestion, which made it the one recorded step that did NOT move the
    // status. Now that every phase has an explicit failure option, silence about
    // intent means the lead is still open: a missed meeting usually needs
    // rescheduling, not writing off.
    const d = deriveStage({ meetingAttended: false });
    expect(d.stage).toBe('contacted');
    expect(d.terminal).toBe(false);
    expect(d.certainty).toBe('explicit');
    expect(d.because).toMatch(/missed the meeting/i);
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
    expect(deriveStage({ depositStatus: 'paid' }).stage).toBe('deposit_paid');
  });
  it('deposit NOT paid is not a setback — it is just the default', () => {
    const d = deriveStage({ contractSigned: 'yes', depositStatus: 'pending' });
    expect(d.stage).toBe('signed');
  });
});

describe('deriveStage — precedence: the deepest phase wins', () => {
  it('a banked deposit outranks a stale phase-1 "not interested"', () => {
    const d = deriveStage({ contactOutcome: 'not_interested', depositStatus: 'paid' });
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
    { depositStatus: 'paid' },
    { depositStatus: 'pending' },
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
    { label: 'depositPaid:true', patch: { depositStatus: 'paid' } },
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

describe('resolveStatus — vocabularies that predate the agreed stages', () => {
  // The list this application actually seeded before the eight stages were agreed.
  // Verified against git history, not guessed: the first version of this block
  // used an invented list and asserted the wrong thing about it.
  const LEGACY = opts([
    'New', 'Open', 'Attempted Contact', 'Contacted', 'Connected',
    'In Progress', 'Qualified', 'Unqualified', 'Open Deal', 'Lost',
  ]);

  it('the legacy list resolves every stage, so an established org still syncs', () => {
    // Worth pinning: it would be easy to assume the old vocabulary is the reason
    // statuses stopped moving. It is not — driving the real app in a browser
    // showed this list writing the status correctly. The cause was an org with NO
    // statuses at all. If this ever regresses, the diagnosis changes.
    expect(resolveStatus('attempted', LEGACY)?.name).toBe('Attempted Contact');
    expect(resolveStatus('attempted', LEGACY)?.match).toBe('exact');
    expect(resolveStatus('contacted', LEGACY)?.name).toBe('Contacted');
    expect(resolveStatus('qualified', LEGACY)?.name).toBe('Qualified');
    expect(resolveStatus('signed', LEGACY)?.name).toBe('Open Deal');
    // No exact match for a booked meeting; one rung down understates progress but
    // is true as far as it goes.
    expect(resolveStatus('meeting_scheduled', LEGACY)?.match).toBe('fallback');
  });

  it('the agreed list resolves every stage EXACTLY, which is the point of it', () => {
    const AGREED = opts([
      'New', 'Attempted Contact', 'Contacted', 'Connected',
      'Contract Pending', 'Awaiting Signature', 'Signed', 'Lost / Not Interested',
    ]);
    expect(resolveStatus('attempted', AGREED)?.match).toBe('exact');
    expect(resolveStatus('meeting_held', AGREED)?.name).toBe('Connected');
    expect(resolveStatus('contract_sent', AGREED)?.name).toBe('Awaiting Signature');
    expect(resolveStatus('signed', AGREED)?.name).toBe('Signed');
  });
});

describe('resolveStatus — New is never reachable as a FALLBACK', () => {
  // `new` is the one rung of the downgrade chain that asserts nothing has
  // happened. Every other step down understates progress, which is a tolerable
  // loss; degrading to `new` would write "New" over "New" on a lead that has
  // demonstrably been worked — a write that changes nothing, reported as
  // success. Resolving to nothing instead surfaces as drift to reconcile.
  //
  // A vocabulary with a start and an end and nothing in between is the shape
  // that provokes it.
  const SPARSE = opts(['New', 'Won', 'Lost']);

  it('a worked lead does not degrade to New', () => {
    const stages: CanonicalStage[] = [
      'attempted', 'contacted', 'meeting_scheduled', 'meeting_held',
      'qualified', 'contract_pending', 'contract_sent',
    ];
    for (const stage of stages) {
      const resolved = resolveStatus(stage, SPARSE);
      if (resolved?.name === 'New') {
        throw new Error(`${stage} resolved to New via a ${resolved.match} match`);
      }
    }
  });

  it('but New itself still resolves, or a new lead could not be labelled', () => {
    expect(resolveStatus('new', SPARSE)?.name).toBe('New');
    expect(resolveStatus('new', SPARSE)?.match).toBe('exact');
  });

  it('and an override may still pin a stage to New, because that is the org asking', () => {
    // The guard is against silently degrading to New, not against a deliberate pin.
    expect(resolveStatus('attempted', SPARSE, { attempted: 'New' })?.name).toBe('New');
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
      pipeline: { contractSigned: 'yes', depositStatus: 'paid' },
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
      pipeline: { depositStatus: 'paid' }, statusesLoaded: false,
    }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'statuses_loading' });
  });

  it('says the list is UNAVAILABLE, not loading, when it is never arriving', () => {
    // This distinction is the whole bug. An org with no statuses configured left
    // `statusesLoaded` false permanently, so every pipeline edit reported
    // 'statuses_loading' — a transient-sounding reason for a state that never
    // changes. The user logged step after step, the status sat on New, and
    // nothing anywhere could explain why. Holding the write is still correct;
    // reporting it as a temporary condition was not.
    const plan = planStatusSync(ctx({
      pipeline: { depositStatus: 'paid' },
      statusesLoaded: false,
      statusesUnavailable: true,
    }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'statuses_unavailable' });
  });

  it('an unavailable list still blocks the write — the reason changed, not the rule', () => {
    // The gate exists so auto-sync cannot write a status the org does not have.
    // Naming the cause must not have turned it into a write.
    const plan = planStatusSync(ctx({
      pipeline: { depositStatus: 'paid' },
      statusesLoaded: false,
      statusesUnavailable: true,
    }));
    expect(plan.kind).toBe('skip');
  });

  it('once the real list arrives, the flag is ignored and the write happens', () => {
    // `statusesUnavailable` must not be able to keep the gate shut on its own —
    // a stale flag would recreate the bug it exists to describe.
    const plan = planStatusSync(ctx({
      pipeline: { depositStatus: 'paid' },
      statusesLoaded: true,
      statusesUnavailable: true,
    }));
    expect(plan.kind).toBe('apply');
  });

  it('never touches a converted lead', () => {
    const plan = planStatusSync(ctx({ pipeline: { depositStatus: 'paid' }, isConverted: true }));
    expect(plan).toMatchObject({ kind: 'skip', reason: 'converted' });
  });

  it('honours the per-user opt-out', () => {
    const plan = planStatusSync(ctx({ pipeline: { depositStatus: 'paid' }, enabled: false }));
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

  it('DOES bring a stale Lost back in line, now that nobody can have typed it', () => {
    // This used to be held as a human's call. There is no such call any more: the
    // status is derived and read-only, so a Lost sitting above a pipeline that
    // records live outreach is simply out of date. Holding it would leave the
    // badge contradicting the tracker with no way to reconcile them.
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted' },
      currentStatus: 'Lost',
    }));
    expect(plan.kind).toBe('apply');
  });

  it('still applies when the live status is the one we last wrote', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: true },
      currentStatus: 'Connected',
      lastAutoStatus: 'Connected',
    }));
    expect(plan.kind).toBe('apply');
  });

  it('still never marks a lead dead for one missed meeting', () => {
    // Same guarantee, reached differently: a bare no-show no longer DERIVES a
    // loss at all, so there is nothing to withhold. Previously it derived Lost
    // and was suppressed as a guess, which meant recording the step appeared to
    // do nothing.
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: false }, currentStatus: 'Connected',
    }));
    // It writes — but it writes CONTACTED, not Lost. Nothing about a missed
    // meeting says the lead is gone.
    expect(plan.kind).toBe('apply');
    if (plan.kind !== 'apply') return;
    expect(plan.to.canonical).toBe('contacted');
    expect(deriveStage({ meetingAttended: false }).terminal).toBe(false);
  });

  it('auto-writes a STATED loss', () => {
    const plan = planStatusSync(ctx({
      pipeline: { meetingAttended: true, stillInterested: false }, currentStatus: 'Connected',
    }));
    expect(plan.kind).toBe('apply');
  });
});

describe('planStatusSync — forward-only', () => {
  it('DEMOTES when the pipeline falls behind the status, because that is a correction', () => {
    // Forward-only existed to protect a status somebody might have typed. Nobody
    // can, so a lower pipeline is a correction and the status must follow it —
    // otherwise unticking a step you recorded by mistake silently does nothing,
    // which is the worst kind of nothing.
    const plan = planStatusSync(ctx({
      pipeline: { outreachStatus: 'contacted' }, currentStatus: 'Open Deal',
    }));
    expect(plan.kind).toBe('apply');
    if (plan.kind !== 'apply') return;
    expect(plan.to.canonical).toBe('contacted');
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
      pipeline: { depositStatus: 'paid' }, currentStatus: 'New', isConverted: true,
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
      { depositStatus: 'paid' },
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
    { contactOutcome: 'not_interested', depositStatus: 'paid' },
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
    const d = deriveStage({ contactOutcome: 'not_interested', depositStatus: 'paid' });
    expect(d.stage).toBe('deposit_paid');
    expect(d.conflicts).toContain('Not interested at outreach');
  });

  it('a clean positive pipeline declares no conflicts', () => {
    const d = deriveStage({ outreachStatus: 'contacted', meetingAttended: true, stillInterested: true });
    expect(d.conflicts).toEqual([]);
  });
});


describe('statusDrift stays visible when auto-sync is switched off', () => {
  it('still reports the disagreement, because the strip is then the only fix', () => {
    // Drift is a statement about the data, not about the automation.
    const drift = statusDrift({
      pipeline: { depositStatus: 'paid' },
      currentStatus: 'New',
      statusOptions: DEFAULT_OPTS,
      statusesLoaded: true,
    });
    expect(drift?.suggested.name).toBe('Open Deal');
  });
});




// ── Regression: the manual hold must be ARMED by a pick, not destroyed by it ──


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
    expect(statusFor({ contractSigned: 'yes', depositStatus: 'paid' })).toBe('Signed');
    expect(statusFor({ depositStatus: 'paid' })).toBe('Signed');
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
      { depositStatus: 'paid' },
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
      { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-08-20', meetingAttended: true, stillInterested: true, contractStatus: 'yes', contractSentDate: '2026-08-22', contractSigned: 'yes', signatureDate: '2026-08-25', depositStatus: 'paid', paymentDate: '2026-08-28' },
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

describe('the status is DERIVED — there is no way for a person to pin it', () => {
  // What replaced four blocks of hold-and-release logic. The status used to be
  // editable, so most of planStatusSync existed to avoid overwriting somebody's
  // decision: a manual pin with a releasable tier, a forward-only rule, a
  // withheld guess for an ambiguous no-show, and a deference to the org's own
  // display order. None of those has a premise any more, and each of them was a
  // case where recording a pipeline step visibly did nothing.

  it('every recorded step moves the status, in either direction', () => {
    const steps: { pipeline: LeadPipeline; expect: CanonicalStage }[] = [
      { pipeline: { outreachStatus: 'attempted_no_answer' }, expect: 'attempted' },
      { pipeline: { outreachStatus: 'contacted' }, expect: 'contacted' },
      { pipeline: { contactOutcome: 'meeting_scheduled' }, expect: 'meeting_scheduled' },
      { pipeline: { meetingAttended: true }, expect: 'meeting_held' },
      { pipeline: { stillInterested: true }, expect: 'qualified' },
      { pipeline: { contractStatus: 'to_be_sent' }, expect: 'contract_pending' },
      { pipeline: { contractStatus: 'yes' }, expect: 'contract_sent' },
      { pipeline: { contractSigned: 'yes' }, expect: 'signed' },
      { pipeline: { depositStatus: 'paid' }, expect: 'deposit_paid' },
    ];

    // Forwards from New, then BACKWARDS from the top. Both directions must write:
    // correcting a step you mis-recorded has to move the status back, or the undo
    // silently fails.
    for (const step of steps) {
      const forward = planStatusSync(ctx({ pipeline: step.pipeline, currentStatus: 'New' }));
      expect(forward.kind, `forward to ${step.expect}`).toBe('apply');

      // Backwards, the plan may legitimately be a no-op: in this vocabulary
      // "signed" resolves to the same string as "Open Deal", so there is nothing
      // to write. What must NEVER happen is a suggestion — that is the old
      // forward-only rule, and it is what made an undo appear to do nothing.
      const back = planStatusSync(ctx({ pipeline: step.pipeline, currentStatus: 'Open Deal' }));
      expect(back.kind, `back to ${step.expect}`).not.toBe('suggest');
      if (back.kind === 'skip') {
        expect(back.reason, `back to ${step.expect}`).toBe('noop');
      }
    }
  });

  it('every failure moves the status, at every phase', () => {
    const failures: LeadPipeline[] = [
      { contactOutcome: 'not_interested' },
      { meetingAttended: true, stillInterested: false },
      { contractStatus: 'no_longer_interested' },
      { contractStatus: 'profile_rejected' },
      { contractSigned: 'no' },
      { depositStatus: 'not_paid' },
    ];
    for (const pipeline of failures) {
      const plan = planStatusSync(ctx({ pipeline, currentStatus: 'Open Deal' }));
      expect(plan.kind, JSON.stringify(pipeline)).toBe('apply');
      if (plan.kind !== 'apply') continue;
      expect(['lost', 'unqualified'], JSON.stringify(pipeline)).toContain(plan.to.canonical);
    }
  });

  it('holds only for a status that is not a stage at all', () => {
    // The two remaining holds, and neither is about progress. A lead somebody
    // parked must not be dragged back into the funnel by a sales step, and an
    // unfamiliar label gets the same protection because a list of parking words
    // is never complete.
    const parked = planStatusSync(ctx({
      pipeline: { stillInterested: true }, currentStatus: 'Do Not Contact',
      statusOptions: opts([...FALLBACK, 'Do Not Contact']),
    }));
    expect(parked).toMatchObject({ kind: 'suggest', reason: 'sticky_status' });

    const custom = planStatusSync(ctx({
      pipeline: { stillInterested: true }, currentStatus: 'Cold Outreach Q3',
      statusOptions: opts([...FALLBACK, 'Cold Outreach Q3']),
    }));
    expect(custom).toMatchObject({ kind: 'suggest', reason: 'unrecognised_status' });
  });

  it('does not touch a lead with no pipeline activity at all', () => {
    // Clearing a tracker must not drag a lead's starting status around.
    expect(planStatusSync(ctx({ pipeline: {}, currentStatus: 'New' })))
      .toMatchObject({ kind: 'skip', reason: 'no_signal' });
  });

  it('the org can still shape the ladder — by pinning stages, not by typing', () => {
    // An unusual vocabulary is handled by overrides, which is the right tool.
    // Deferring a write because the org's displayOrder disagreed with our tiers
    // was the wrong one: it made some steps silently not move the status.
    const plan = planStatusSync(ctx({
      pipeline: { stillInterested: true },
      currentStatus: 'New',
      statusOptions: opts([...FALLBACK, 'Ready for contract']),
      overrides: { qualified: 'Ready for contract' },
    }));
    expect(plan.kind).toBe('apply');
    if (plan.kind !== 'apply') return;
    expect(plan.to.name).toBe('Ready for contract');
  });
});
