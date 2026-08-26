// The transitions that must be IMPOSSIBLE.
//
// A contract flow is mostly gates, and a missing gate does not look like a bug —
// it looks like a working button. Each test here names the document you would be
// left holding if the gate were absent.

import { describe, it, expect } from 'vitest';
import {
  can, next, allowedActions, isTerminal, TERMINAL_STATUSES,
  isSignatureNameValid, checkSignature, pipelineEffect, nextActionHint,
  STATUS_LABELS, type ContractStatus, type ContractAction,
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

  it('sending satisfies the Contract phase', () => {
    expect(pipelineEffect('sent', DATE)).toEqual({ contractStatus: 'yes', contractSentDate: DATE });
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
