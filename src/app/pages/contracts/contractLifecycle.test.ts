// The transitions that must be IMPOSSIBLE.
//
// A contract flow is mostly gates, and a missing gate does not look like a bug —
// it looks like a working button. Each test here names the document you would be
// left holding if the gate were absent.

import { describe, it, expect } from 'vitest';
import {
  can, next, allowedActions, isTerminal, TERMINAL_STATUSES,
  isSignatureNameValid, checkSignature, pipelineEffect, nextActionHint,
  STATUS_LABELS, timeAgo, describeState, CONTRACT_STEPS, MAX_SIGNATURE_NAME_LENGTH,
  type ContractStatus, type ContractAction, type DescribeInput,
} from './contractLifecycle';

const ALL_STATUSES: ContractStatus[] = [
  'draft', 'sent', 'signed_by_client', 'countersigned', 'declined', 'voided',
];
const ALL_ACTIONS: ContractAction[] = [
  'edit', 'send', 'client_sign', 'decline', 'countersign', 'void', 'resend',
];

describe('the happy path', () => {
  it('walks draft to fully executed', () => {
    expect(next('draft', 'send')).toBe('sent');
    expect(next('sent', 'client_sign')).toBe('signed_by_client');
    expect(next('signed_by_client', 'countersign')).toBe('countersigned');
    expect(isTerminal('countersigned')).toBe(true);
  });

  it('lets a draft be edited as often as needed', () => {
    expect(next('draft', 'edit')).toBe('draft');
  });
});

describe('gates that must hold', () => {
  it('CANNOT countersign before the client has signed', () => {
    // Otherwise you produce an "executed" contract carrying only our own
    // signature — worse than no contract, because it looks complete.
    expect(can('draft', 'countersign')).toBe(false);
    expect(can('sent', 'countersign')).toBe(false);
    expect(next('sent', 'countersign')).toBeNull();
  });

  it('CANNOT edit the body once it has been sent', () => {
    // The single failure that would make the whole feature worthless as
    // evidence: they sign one text and we keep another.
    expect(can('sent', 'edit')).toBe(false);
    expect(can('signed_by_client', 'edit')).toBe(false);
    expect(can('countersigned', 'edit')).toBe(false);
  });

  it('CANNOT sign twice', () => {
    expect(can('signed_by_client', 'client_sign')).toBe(false);
    expect(can('countersigned', 'countersign')).toBe(false);
  });

  it('CANNOT send twice — a second link would leave the first one live', () => {
    expect(can('sent', 'send')).toBe(false);
    // Resending is a separate action, and it is expected to reuse the same token
    // rather than mint a new one.
    expect(next('sent', 'resend')).toBe('sent');
  });

  it('CANNOT do anything at all from a terminal state', () => {
    for (const status of TERMINAL_STATUSES) {
      expect(allowedActions(status), status).toEqual([]);
      for (const action of ALL_ACTIONS) {
        expect(can(status, action), `${status} + ${action}`).toBe(false);
      }
    }
  });

  it('CANNOT resurrect a declined contract by sending it again', () => {
    expect(can('declined', 'send')).toBe(false);
    expect(can('declined', 'resend')).toBe(false);
    expect(can('declined', 'client_sign')).toBe(false);
  });
});

describe('the table itself', () => {
  it('every reachable target is a real status', () => {
    for (const status of ALL_STATUSES) {
      for (const action of ALL_ACTIONS) {
        const target = next(status, action);
        if (target !== null) expect(ALL_STATUSES, `${status}+${action}`).toContain(target);
      }
    }
  });

  it('every status can be abandoned until it is terminal', () => {
    // A contract stuck with no way out is a support ticket. Void is the escape
    // hatch and it must exist from every non-terminal state.
    for (const status of ALL_STATUSES) {
      if (!isTerminal(status)) expect(can(status, 'void'), status).toBe(true);
    }
  });

  it('every status is labelled and every non-terminal one says what to do next', () => {
    for (const status of ALL_STATUSES) {
      expect(STATUS_LABELS[status], status).toBeTruthy();
      if (!isTerminal(status)) expect(nextActionHint(status), status).toBeTruthy();
      else expect(nextActionHint(status), status).toBeNull();
    }
  });

  it('allowedActions agrees with can(), so the UI cannot offer a refused button', () => {
    for (const status of ALL_STATUSES) {
      const allowed = allowedActions(status);
      for (const action of ALL_ACTIONS) {
        expect(allowed.includes(action), `${status}+${action}`).toBe(can(status, action));
      }
    }
  });
});

describe('isSignatureNameValid', () => {
  it('accepts a real name, including initials and accents', () => {
    for (const name of ['Jean Dupont', 'J. Dupont', 'Léa', 'Ng Wei', "O'Brien", 'Zoë']) {
      expect(isSignatureNameValid(name), name).toBe(true);
    }
  });

  it('rejects a placeholder, which would leave nothing to point at later', () => {
    for (const bad of ['', ' ', '.', 'x', '..', '--', '1234', '   ']) {
      expect(isSignatureNameValid(bad), JSON.stringify(bad)).toBe(false);
    }
  });
});

describe('checkSignature', () => {
  it('accepts a signature when the status, name and consent are all there', () => {
    expect(checkSignature({
      status: 'sent', action: 'client_sign', name: 'Jean Dupont', agreed: true,
    })).toEqual({ ok: true, problem: null });
  });

  it('refuses without the consent tick — that tick is what makes it a signature', () => {
    const r = checkSignature({ status: 'sent', action: 'client_sign', name: 'Jean Dupont', agreed: false });
    expect(r.ok).toBe(false);
    expect(r.problem).toMatch(/tick the box/i);
  });

  it('refuses an empty mark and says what to do', () => {
    const r = checkSignature({ status: 'sent', action: 'client_sign', name: '  ', agreed: true });
    expect(r.ok).toBe(false);
    expect(r.problem).toMatch(/full name/i);
  });

  it('refuses a countersignature on a contract they have not signed', () => {
    const r = checkSignature({ status: 'sent', action: 'countersign', name: 'Kia', agreed: true });
    expect(r.ok).toBe(false);
    expect(r.problem).toMatch(/not ready/i);
  });

  it('explains itself when the contract is already finished', () => {
    const r = checkSignature({ status: 'countersigned', action: 'client_sign', name: 'Jean Dupont', agreed: true });
    expect(r.ok).toBe(false);
    expect(r.problem).toMatch(/fully executed/i);
  });

  it('checks the status gate BEFORE the name, so a stale link never asks for a signature', () => {
    // Order matters for what the signer is told: someone opening a voided link
    // should read "this is voided", not "type your name".
    const r = checkSignature({ status: 'voided', action: 'client_sign', name: '', agreed: false });
    expect(r.problem).toMatch(/voided/i);
  });
});

describe('pipelineEffect', () => {
  const DATE = '2026-08-26';

  it('sending satisfies the Contract phase AND resets the signature phase', () => {
    // The reset is the load-bearing half. A lead whose first contract was declined
    // carries contractSigned:'no'; sending a replacement used to write only the
    // phase-3 keys, so the lead went on reading "Contract declined" and Lost while
    // a live signing link was out with the counterparty.
    expect(pipelineEffect('sent', DATE)).toEqual({
      contractStatus: 'yes', contractSentDate: DATE, contractSigned: 'pending',
    });
  });

  it('full execution satisfies the Signature phase', () => {
    // Without this, a fully signed contract would sit next to a lead still
    // reading "Contacted", and the status auto-sync would never fire.
    expect(pipelineEffect('countersigned', DATE)).toEqual({ contractSigned: 'yes', signatureDate: DATE });
  });

  it('a decline records their decision', () => {
    expect(pipelineEffect('declined', DATE)).toEqual({ contractSigned: 'no' });
  });

  it('voiding records NOTHING against the customer', () => {
    // Voiding a draft we should not have sent is our decision, not theirs, and
    // writing `contractSigned: 'no'` would report it as a customer drop-out and
    // corrupt the drop-off report.
    expect(pipelineEffect('voided', DATE)).toBeNull();
  });

  it('a draft or an awaited countersignature changes nothing yet', () => {
    expect(pipelineEffect('draft', DATE)).toBeNull();
    expect(pipelineEffect('signed_by_client', DATE)).toBeNull();
  });

  it('only ever writes fields the pipeline actually has', () => {
    // Guards against a typo silently adding a field nothing reads — the effect
    // would look applied and change no report.
    const known = new Set([
      'contractStatus', 'contractSentDate', 'contractSigned', 'signatureDate',
    ]);
    for (const status of ALL_STATUSES) {
      const effect = pipelineEffect(status, DATE);
      if (!effect) continue;
      for (const key of Object.keys(effect)) expect(known, `${status}.${key}`).toContain(key);
    }
  });
});

describe('timeAgo', () => {
  const NOW = new Date('2026-08-26T12:00:00Z').getTime();
  const at = (iso: string) => timeAgo(iso, NOW);

  it('reads naturally across the scales', () => {
    expect(at('2026-08-26T11:59:50Z')).toBe('just now');
    expect(at('2026-08-26T11:30:00Z')).toBe('30 minutes ago');
    expect(at('2026-08-26T11:00:00Z')).toBe('1 hour ago');
    expect(at('2026-08-26T04:00:00Z')).toBe('8 hours ago');
    expect(at('2026-08-25T12:00:00Z')).toBe('yesterday');
    expect(at('2026-08-23T12:00:00Z')).toBe('3 days ago');
    expect(at('2026-06-26T12:00:00Z')).toBe('2 months ago');
    expect(at('2025-08-26T12:00:00Z')).toBe('1 year ago');
  });

  it('gets singular and plural right', () => {
    expect(at('2026-08-26T11:59:00Z')).toBe('1 minute ago');
    expect(at('2026-08-26T11:58:00Z')).toBe('2 minutes ago');
    expect(at('2026-07-26T12:00:00Z')).toBe('1 month ago');
  });

  it('never renders a future timestamp as negative', () => {
    // Not hypothetical: server and browser clocks disagree by seconds routinely,
    // so a just-created contract would otherwise read "in -2 seconds ago".
    expect(at('2026-08-26T12:00:05Z')).toBe('just now');
    expect(at('2026-08-27T12:00:00Z')).toBe('just now');
  });

  it('returns null rather than a fake date for missing or unparseable input', () => {
    for (const bad of [null, undefined, '', 'not a date']) {
      expect(timeAgo(bad, NOW), String(bad)).toBeNull();
    }
  });
});

describe('describeState', () => {
  const NOW = new Date('2026-08-26T12:00:00Z').getTime();
  const base = { counterpartyName: 'Jean Dupont' };
  const d = (over: Partial<DescribeInput> & { status: ContractStatus }) =>
    describeState({ ...base, ...over }, NOW);

  it('a draft is your move and says nobody has seen it', () => {
    const s = d({ status: 'draft' });
    expect(s.turn).toBe('you');
    expect(s.completed).toBe(0);
    expect(s.headline).toBe('Not sent yet');
    expect(s.detail).toMatch(/Nobody outside your team/);
    expect(s.detail).toContain('Jean Dupont');
  });

  it('a sent contract says who we are waiting on, and whether they opened it', () => {
    const unopened = d({ status: 'sent', sentAtUtc: '2026-08-23T12:00:00Z' });
    expect(unopened.turn).toBe('them');
    expect(unopened.headline).toBe('Waiting for Jean Dupont to sign');
    expect(unopened.detail).toBe('Sent 3 days ago. They have not opened it yet.');

    const opened = d({
      status: 'sent',
      sentAtUtc: '2026-08-23T12:00:00Z',
      firstViewedAtUtc: '2026-08-25T12:00:00Z',
    });
    expect(opened.detail).toBe('Sent 3 days ago. They opened it yesterday.');
  });

  it('their signature makes it your move again', () => {
    const s = d({ status: 'signed_by_client', clientSignedAtUtc: '2026-08-26T10:00:00Z' });
    expect(s.turn).toBe('you');
    expect(s.tone).toBe('action');
    expect(s.headline).toBe('Jean Dupont has signed');
    expect(s.detail).toMatch(/Signed 2 hours ago/);
  });

  it('EXECUTED BUT NOT DELIVERED does not read as finished', () => {
    // The case that matters most: binding, but nobody has been sent it — usually
    // because SMTP is unconfigured. Reading this as done is how a member never
    // receives their copy.
    const s = d({ status: 'countersigned', counterSignedAtUtc: '2026-08-26T11:00:00Z' });
    expect(s.tone).not.toBe('done');
    expect(s.completed).toBe(3);
    expect(s.turn).toBe('you');
    expect(s.headline).toMatch(/copies not sent/i);
  });

  it('fully delivered is done, and nobody is waiting', () => {
    const s = d({
      status: 'countersigned',
      counterSignedAtUtc: '2026-08-26T11:00:00Z',
      executedCopySentAtUtc: '2026-08-26T11:00:05Z',
    });
    expect(s.tone).toBe('done');
    expect(s.completed).toBe(4);
    expect(s.turn).toBe('nobody');
    expect(s.detail).toMatch(/emailed the finished contract/);
  });

  it('a decline is not four steps of four', () => {
    const s = d({ status: 'declined', closedReason: 'Not within budget' });
    expect(s.completed).toBeLessThan(4);
    expect(s.tone).toBe('stopped');
    expect(s.headline).toBe('Jean Dupont declined');
    expect(s.detail).toContain('Not within budget');
  });

  it('explains a decline with no reason rather than showing an empty one', () => {
    for (const reason of [undefined, null, '   ']) {
      const s = d({ status: 'declined', closedReason: reason });
      expect(s.detail).toMatch(/No reason was given/);
    }
  });

  it('a void reports how far it actually got before being cancelled', () => {
    // Reporting 0 of 4 for a contract the counterparty had already signed would
    // misrepresent the record — and that is precisely the case where somebody
    // later asks what happened.
    expect(d({ status: 'voided' }).completed).toBe(0);
    expect(d({ status: 'voided', sentAtUtc: '2026-08-20T12:00:00Z' }).completed).toBe(1);
    expect(d({
      status: 'voided',
      sentAtUtc: '2026-08-20T12:00:00Z',
      clientSignedAtUtc: '2026-08-22T12:00:00Z',
    }).completed).toBe(2);
    // Still halted, however far it got.
    expect(d({ status: 'voided', clientSignedAtUtc: '2026-08-22T12:00:00Z' }).tone).toBe('stopped');
  });

  it('a void says the link is dead, because that is the consequence people ask about', () => {
    const s = d({ status: 'voided' });
    expect(s.completed).toBe(0);
    expect(s.tone).toBe('stopped');
    expect(s.detail).toMatch(/no longer works/);
  });

  it('never leaves a headline or detail empty, whatever the state', () => {
    for (const status of ALL_STATUSES) {
      const s = d({ status });
      expect(s.headline.trim(), status).not.toBe('');
      expect(s.detail.trim(), status).not.toBe('');
      expect(s.completed, status).toBeLessThanOrEqual(CONTRACT_STEPS.length);
      expect(s.completed, status).toBeGreaterThanOrEqual(0);
    }
  });

  it('copes with a missing counterparty name instead of saying "undefined has signed"', () => {
    const s = describeState({ status: 'signed_by_client', counterpartyName: '   ' }, NOW);
    expect(s.headline).toBe('the counterparty has signed');
    expect(s.headline).not.toMatch(/undefined|null/);
  });

  it('survives a missing timestamp on every state that shows one', () => {
    // A row written by an older build, or a clock that has not caught up.
    for (const status of ['sent', 'signed_by_client', 'countersigned'] as ContractStatus[]) {
      const s = d({ status });
      expect(s.detail, status).not.toMatch(/null|undefined|NaN/);
    }
  });
});

describe('describeState — an expired link is OUR move, not theirs', () => {
  const NOW = Date.parse('2026-08-26T12:00:00Z');
  const base: DescribeInput = {
    status: 'sent',
    counterpartyName: 'Jean Dupont',
    sentAtUtc: '2026-07-26T09:00:00Z',
    firstViewedAtUtc: '2026-07-27T10:00:00Z',
  };

  it('says the link expired instead of blaming the counterparty', () => {
    // This is the whole point: the panel used to report the counterparty as the
    // hold-up for a month after their link had died, while the signing page was
    // telling THEM to ask for a new one. Nobody chases the thing that would fix it.
    const d = describeState({ ...base, signingLinkExpiresAtUtc: '2026-08-25T09:00:00Z' }, NOW);
    expect(d.headline).toMatch(/link has expired/i);
    expect(d.turn).toBe('you');
    expect(d.tone).toBe('action');
    expect(d.detail).toMatch(/resend/i);
    // And it must not still be claiming they are the ones to wait for.
    expect(d.headline).not.toMatch(/waiting for/i);
  });

  it('still blames nobody while the link is live', () => {
    const d = describeState({ ...base, signingLinkExpiresAtUtc: '2026-08-27T09:00:00Z' }, NOW);
    expect(d.headline).toBe('Waiting for Jean Dupont to sign');
    expect(d.turn).toBe('them');
  });

  it('does not invent an expiry when the server did not send one', () => {
    // An older server, or a contract read before the field existed. Absent must
    // read as "live", never as "expired".
    for (const missing of [undefined, null, '']) {
      const d = describeState({ ...base, signingLinkExpiresAtUtc: missing }, NOW);
      expect(d.turn, JSON.stringify(missing)).toBe('them');
    }
    // And an unparseable one is treated the same way.
    expect(describeState({ ...base, signingLinkExpiresAtUtc: 'not a date' }, NOW).turn).toBe('them');
  });

  it('does not step on the other states', () => {
    // The expiry only decides anything while the contract is still out for
    // signature. A signed or executed contract whose link has since lapsed is not
    // waiting on a resend.
    const expired = { signingLinkExpiresAtUtc: '2026-01-01T00:00:00Z' };
    expect(describeState({ ...base, ...expired, status: 'signed_by_client', clientSignedAtUtc: '2026-08-01T09:00:00Z' }, NOW).turn)
      .toBe('you');
    expect(describeState({ ...base, ...expired, status: 'countersigned', counterSignedAtUtc: '2026-08-02T09:00:00Z', executedCopySentAtUtc: '2026-08-02T09:01:00Z' }, NOW).tone)
      .toBe('done');
  });
});

describe('isSignatureNameValid — what a signature may not contain', () => {
  it('refuses newlines, which could forge lines in the signature record', () => {
    expect(isSignatureNameValid('Jean Dupont\nSigned : 2019-04-01')).toBe(false);
    expect(isSignatureNameValid('Jean\rDupont')).toBe(false);
    expect(isSignatureNameValid('Jean\tDupont')).toBe(false);
    expect(isSignatureNameValid('Jean\u0000Dupont')).toBe(false);
  });

  it('refuses a name too long for its column, so the signer is told rather than 500ed', () => {
    expect(isSignatureNameValid('a'.repeat(MAX_SIGNATURE_NAME_LENGTH))).toBe(true);
    expect(isSignatureNameValid('a'.repeat(MAX_SIGNATURE_NAME_LENGTH + 1))).toBe(false);
  });

  it('agrees with the server about scripts outside the basic plane', () => {
    // Adlam, used for Fulani. The server counts letters by rune; this counts by
    // code point. They diverged, so the page accepted a name the server refused.
    expect(isSignatureNameValid('\u{1E900}\u{1E922}')).toBe(true);
  });
});
