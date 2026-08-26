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
export const MAX_SIGNATURE_NAME_LENGTH = 300;

export function isSignatureNameValid(name: string): boolean {
  const trimmed = name.trim();
  if (trimmed.length < 2) return false;
  // The column is nvarchar(300). Refusing it here means the signer is told under
  // the field, rather than the server throwing "String or binary data would be
  // truncated" at the moment they try to sign.
  if (trimmed.length > MAX_SIGNATURE_NAME_LENGTH) return false;
  // No control characters. The signature is interpolated line by line into the
  // signature record both parties are emailed as evidence, so a name containing
  // newlines could forge lines in it — a second "Signed:" with an earlier date,
  // a different document hash — indistinguishable from the real ones.
  // eslint-disable-next-line no-control-regex
  if (/[\u0000-\u001F\u007F]/.test(trimmed)) return false;
  // At least two letters somewhere — rules out "..", "--", "1234". The `u` flag
  // matters: it counts letters by code point, which is what keeps this in step
  // with the server's rune count for scripts outside the basic plane.
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
      // `contractSigned: 'pending'` matters as much as the other two. Without it a
      // lead whose first contract was declined kept `contractSigned: 'no'` for
      // good: sending a replacement wrote only the phase-3 keys, so the lead went
      // on reading "Contract declined" and Lost while a live signing link was out.
      return { contractStatus: 'yes', contractSentDate: onDate, contractSigned: 'pending' };
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

/* ------------------------------------------------------- explaining the state */

/**
 * How long ago something happened, in words.
 *
 * Own implementation rather than Intl.RelativeTimeFormat because the phrasing
 * wanted here is "3 days ago" and "just now", not "3 days ago" and "in 0
 * seconds". The future case matters and is not hypothetical: server and browser
 * clocks disagree by seconds routinely, and a naive difference renders a
 * just-created contract as "in 2 seconds ago".
 */
export function timeAgo(iso: string | null | undefined, now: number = Date.now()): string | null {
  if (!iso) return null;
  const then = new Date(iso).getTime();
  if (!Number.isFinite(then)) return null;

  const seconds = Math.round((now - then) / 1000);
  // Clock skew, or a genuinely future timestamp. Either way "just now" is the
  // honest reading — never "in -4 seconds".
  if (seconds < 45) return 'just now';

  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? '' : 's'} ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} hour${hours === 1 ? '' : 's'} ago`;
  const days = Math.round(hours / 24);
  if (days === 1) return 'yesterday';
  if (days < 31) return `${days} days ago`;
  const months = Math.round(days / 30);
  if (months < 12) return `${months} month${months === 1 ? '' : 's'} ago`;
  const years = Math.round(months / 12);
  return `${years} year${years === 1 ? '' : 's'} ago`;
}

/** The four steps, as the UI labels them. */
export const CONTRACT_STEPS = [
  'Draft',
  'Their signature',
  'Your signature',
  'Copies sent',
] as const;

export type ContractTurn = 'you' | 'them' | 'nobody';
export type StateTone = 'neutral' | 'waiting' | 'action' | 'done' | 'stopped';

export interface ContractStateDescription {
  /** 1-based step reached. 0 for a contract that stopped before step 1 finished. */
  step: number;
  /** How many of the four steps are complete. */
  completed: number;
  turn: ContractTurn;
  /** One short line: the fact. */
  headline: string;
  /** One short line: what it means or what to do. Never repeats the headline. */
  detail: string;
  tone: StateTone;
}

export interface DescribeInput {
  status: ContractStatus;
  counterpartyName: string;
  sentAtUtc?: string | null;
  firstViewedAtUtc?: string | null;
  clientSignedAtUtc?: string | null;
  counterSignedAtUtc?: string | null;
  executedCopySentAtUtc?: string | null;
  /**
   * When the counterparty's link stops working.
   *
   * Needed to tell "waiting on them" apart from "they can no longer open it".
   * Without it the panel kept reporting the counterparty as the one holding
   * things up for a month after their link had died — while the signing page was
   * telling them the link had expired and to ask for a new one.
   */
  signingLinkExpiresAtUtc?: string | null;
  closedReason?: string | null;
}

/**
 * Plain language for where a contract is, whose move it is, and what happens next.
 *
 * Exists because a status pill reading "sent" answers none of the questions
 * somebody actually has: sent to whom, how long ago, did they open it, and am I
 * waiting on them or are they waiting on me. Kept out of the component and tested
 * because the awkward cases are all here, not in the markup — a contract executed
 * but whose copies never went out must NOT read as finished, and a declined one
 * must not render as four steps of four.
 */
export function describeState(c: DescribeInput, now: number = Date.now()): ContractStateDescription {
  const who = c.counterpartyName.trim() || 'the counterparty';

  switch (c.status) {
    case 'draft':
      return {
        step: 1, completed: 0, turn: 'you', tone: 'action',
        headline: 'Not sent yet',
        detail: `Nobody outside your team has seen this. Read it through, then send it to ${who}.`,
      };

    case 'sent': {
      const sent = timeAgo(c.sentAtUtc, now);
      const viewed = timeAgo(c.firstViewedAtUtc, now);

      // An expired link is OUR move, not theirs. Reporting "waiting for them" is
      // wrong in the way that matters most: it points at the wrong person, so
      // nobody chases the thing that would fix it.
      const expiry = c.signingLinkExpiresAtUtc
        ? Date.parse(c.signingLinkExpiresAtUtc)
        : Number.NaN;
      if (!Number.isNaN(expiry) && expiry <= now) {
        return {
          step: 2, completed: 1, turn: 'you', tone: 'action',
          headline: 'The signing link has expired',
          detail: `${who} can no longer open it${viewed ? `, and last looked ${viewed}` : ''}. `
            + 'Resend to issue a new link.',
        };
      }

      return {
        step: 2, completed: 1, turn: 'them', tone: 'waiting',
        headline: `Waiting for ${who} to sign`,
        detail: viewed
          ? `Sent ${sent ?? 'recently'}. They opened it ${viewed}.`
          : `Sent ${sent ?? 'recently'}. They have not opened it yet.`,
      };
    }

    case 'signed_by_client': {
      const signed = timeAgo(c.clientSignedAtUtc, now);
      return {
        step: 3, completed: 2, turn: 'you', tone: 'action',
        headline: `${who} has signed`,
        detail: `Signed ${signed ?? 'recently'}. Your signature executes the contract and sends the finished copy to both of you.`,
      };
    }

    case 'countersigned': {
      const executed = timeAgo(c.counterSignedAtUtc, now);
      if (!c.executedCopySentAtUtc) {
        // Deliberately NOT "done". The contract is binding, but nobody has been
        // sent it — usually because SMTP is unconfigured. Reading this as
        // finished is how a member never receives their copy.
        return {
          step: 4, completed: 3, turn: 'you', tone: 'waiting',
          headline: 'Signed by both parties — copies not sent',
          detail: `Executed ${executed ?? 'recently'}, but the finished copy has not reached anyone yet. Send it, or pass it on yourself.`,
        };
      }
      return {
        step: 4, completed: 4, turn: 'nobody', tone: 'done',
        headline: 'Signed by both parties',
        detail: `Executed ${executed ?? 'recently'}. Both of you have been emailed the finished contract and the signature record.`,
      };
    }

    case 'declined':
      return {
        step: 2, completed: 1, turn: 'nobody', tone: 'stopped',
        headline: `${who} declined`,
        detail: c.closedReason?.trim()
          ? `Reason given: ${c.closedReason.trim()}`
          : 'No reason was given. Draft a new contract if the terms have changed.',
      };

    case 'voided': {
      // How far it actually got before being cancelled, from the timestamps.
      // Reporting 0 of 4 for a contract the counterparty had already signed
      // would misrepresent the record — and that is exactly the case where
      // somebody later asks what happened.
      const reached = c.clientSignedAtUtc ? 2 : c.sentAtUtc ? 1 : 0;
      return {
        step: 0, completed: reached, turn: 'nobody', tone: 'stopped',
        headline: 'Voided',
        detail: c.closedReason?.trim()
          ? `${c.closedReason.trim()} The signing link no longer works.`
          : 'You cancelled this contract. The signing link no longer works.',
      };
    }

    default:
      return {
        step: 0, completed: 0, turn: 'nobody', tone: 'neutral',
        headline: 'Unknown state', detail: 'Reload the page.',
      };
  }
}
