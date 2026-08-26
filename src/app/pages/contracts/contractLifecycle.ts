// The contract lifecycle, as a state machine.
//
// Four steps, each with a gate that must not be jumped:
//
//   Draft ──edit──▶ Draft ──send──▶ Sent ──client signs──▶ SignedByClient
//                                                              │
//                                              countersign ────┘
//                                                              ▼
//                                                        Countersigned
//
// This is the half of the feature that can be wrong in ways no screenshot shows.
// The transitions that must be impossible:
//
//   · countersigning before the client has signed — an "executed" contract
//     carrying only our own signature;
//   · editing the body after it has been sent — the counterparty signs one text
//     and we hold another, which is the one failure that would make the whole
//     thing worthless as evidence;
//   · signing twice, or signing something already declined or voided;
//   · sending twice, which mints a second signing link and leaves the first live.
//
// `contractLifecycle.test.ts` pins each of those. The server enforces the same
// table in `ContractStateMachine.cs` — this copy exists so the UI can grey out
// what the server would refuse, and the two are tested against the same cases.

/** Where a contract is. Ordered by progress; terminals last. */
export type ContractStatus =
  | 'draft'
  | 'sent'
  | 'signed_by_client'
  | 'countersigned'
  | 'declined'
  | 'voided';

export type ContractAction =
  | 'edit'
  | 'send'
  | 'client_sign'
  | 'decline'
  | 'countersign'
  | 'void'
  | 'resend';

/** Statuses from which nothing further can happen. */
export const TERMINAL_STATUSES: ContractStatus[] = ['countersigned', 'declined', 'voided'];

export function isTerminal(status: ContractStatus): boolean {
  return TERMINAL_STATUSES.includes(status);
}

/**
 * The only permitted transitions. Anything absent is refused.
 *
 * A table rather than a chain of ifs: this is the single place the rules live,
 * so a new state cannot quietly acquire permissions nobody granted it.
 */
const TRANSITIONS: Record<ContractStatus, Partial<Record<ContractAction, ContractStatus>>> = {
  draft: {
    edit: 'draft',
    send: 'sent',
    void: 'voided',
  },
  sent: {
    // Deliberately NO `edit`. Once the counterparty holds a link to a text, that
    // text is frozen; changing it means they sign one document and we keep
    // another. To change terms, void and draft again.
    client_sign: 'signed_by_client',
    decline: 'declined',
    resend: 'sent',
    void: 'voided',
  },
  signed_by_client: {
    countersign: 'countersigned',
    void: 'voided',
  },
  countersigned: {},
  declined: {},
  voided: {},
};

/** True when `action` is legal in `status`. */
export function can(status: ContractStatus, action: ContractAction): boolean {
  return TRANSITIONS[status]?.[action] !== undefined;
}

/** The status after `action`, or null when the action is not permitted. */
export function next(status: ContractStatus, action: ContractAction): ContractStatus | null {
  return TRANSITIONS[status]?.[action] ?? null;
}

/** Every action legal right now, for rendering exactly the buttons that work. */
export function allowedActions(status: ContractStatus): ContractAction[] {
  return (Object.keys(TRANSITIONS[status] ?? {}) as ContractAction[]).sort();
}

export const STATUS_LABELS: Record<ContractStatus, string> = {
  draft: 'Draft',
  sent: 'Awaiting their signature',
  signed_by_client: 'Signed — awaiting your countersignature',
  countersigned: 'Fully executed',
  declined: 'Declined',
  voided: 'Voided',
};

/** What the CRM user should do next, or null when it is not their move. */
export function nextActionHint(status: ContractStatus): string | null {
  switch (status) {
    case 'draft':
      return 'Read it through, edit anything you need, then send it for signature.';
    case 'sent':
      return 'Waiting on them. You can resend the link or void the contract.';
    case 'signed_by_client':
      return 'They have signed. Add your countersignature to execute it.';
    case 'countersigned':
    case 'declined':
    case 'voided':
      return null;
    default:
      return null;
  }
}

/* ------------------------------------------------------- signature validity */

/**
 * A typed signature has to be a plausible name, not a placeholder.
 *
 * This is the whole substance of a simple electronic signature, so an empty or
 * one-character mark would leave nothing to point at later. Deliberately NOT a
 * check that it matches the counterparty's name on file: people sign as
 * "J. Dupont" for "Jean Dupont", and rejecting that would block a real signing
 * over a formatting opinion.
 */
export function isSignatureNameValid(name: string): boolean {
  const trimmed = name.trim();
  if (trimmed.length < 2) return false;
  // At least two letters somewhere — rules out "..", "--", "1234".
  return (trimmed.match(/\p{L}/gu) ?? []).length >= 2;
}

export interface SignatureGate {
  ok: boolean;
  /** Why not, for showing under the field rather than in a toast. */
  problem: string | null;
}

/**
 * Whether a signature may be accepted right now.
 *
 * Both halves matter: the status gate stops a second signature on an already
 * executed contract, and the consent gate is what makes the mark a signature
 * rather than a text box someone typed in.
 */
export function checkSignature(args: {
  status: ContractStatus;
  action: 'client_sign' | 'countersign';
  name: string;
  agreed: boolean;
}): SignatureGate {
  if (!can(args.status, args.action)) {
    return {
      ok: false,
      problem: isTerminal(args.status)
        ? `This contract is ${STATUS_LABELS[args.status].toLowerCase()} and can no longer be signed.`
        : 'This contract is not ready for that signature yet.',
    };
  }
  if (!isSignatureNameValid(args.name)) {
    return { ok: false, problem: 'Type your full name as your signature.' };
  }
  if (!args.agreed) {
    return { ok: false, problem: 'Tick the box to confirm you agree to be bound by this contract.' };
  }
  return { ok: true, problem: null };
}

/* --------------------------------------------------- pipeline consequences */

/**
 * The lead-pipeline fields a contract transition should write.
 *
 * The contract flow drives the existing 5-phase pipeline rather than sitting
 * beside it: sending satisfies phase 3, full execution satisfies phase 4, and
 * the status auto-sync already in place carries that through to the lead's
 * status. Without this the two would disagree — a fully signed contract sitting
 * next to a lead still reading "Contacted".
 *
 * Returns only the fields that change, so the caller can merge without
 * clobbering anything else on the pipeline.
 */
export function pipelineEffect(status: ContractStatus, onDate: string): Record<string, unknown> | null {
  switch (status) {
    case 'sent':
      return { contractStatus: 'yes', contractSentDate: onDate };
    case 'countersigned':
      return { contractSigned: 'yes', signatureDate: onDate };
    case 'declined':
      // Their decision, and the pipeline already has a word for it.
      return { contractSigned: 'no' };
    case 'voided':
      // Ours, not theirs. Voiding a draft we never should have sent is not a
      // customer decision, so nothing is recorded against them.
      return null;
    default:
      return null;
  }
}
