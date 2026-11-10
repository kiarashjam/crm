// The browser's status rule and the server's must agree, case for case.
//
// The server (LeadStatusDerivation.cs) decides a lead's status now; this module
// only previews it. A preview that disagrees with the decision promises a status
// the save will not give. So both sides run the same inputs from
// leadStatusSync.parity.json and hash the outcome, and both compare against the
// digest stored there. Change the rule here and this test fails until the file is
// updated; then the C# test fails until the server matches.
//
// To compare listings when the digests differ:
//   LEAD_STATUS_PARITY_DUMP=/tmp/parity npx vitest run leadStatusSync.parity
//   (and the same variable for dotnet test), then diff the two files.

import { describe, it, expect } from 'vitest';
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import parity from './leadStatusSync.parity.json';
import { parsePipeline } from './leadPipeline';
import { deriveStage, planStatusSync, type CanonicalStage, type StatusOption } from './leadStatusSync';

type FieldValue = string | boolean | null;

/** Every combination of the fields, first field outermost, as the JSON a lead would hold. */
function combinations(entries: [string, FieldValue[]][], at = 0, parts: string[] = []): string[] {
  if (at === entries.length) return [`{${parts.join(',')}}`];
  const [key, values] = entries[at]!;
  return values.flatMap((v) => combinations(
    entries,
    at + 1,
    v === null ? parts : [...parts, `"${key}":${typeof v === 'string' ? `"${v}"` : String(v)}`],
  ));
}

function digest(lines: string[]): string {
  return createHash('sha256').update(lines.join('\n'), 'utf8').digest('hex');
}

function dump(name: string, lines: string[]): void {
  const dir = process.env.LEAD_STATUS_PARITY_DUMP;
  if (!dir) return;
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, `ts-${name}.txt`), lines.join('\n'), 'utf8');
}

const pipelines = [
  ...combinations(Object.entries(parity.fields) as [string, FieldValue[]][]),
  ...parity.extraPipelines,
];

const deriveLines: string[] = [];
const firstForRule = new Map<string, string>();
for (const json of pipelines) {
  const d = deriveStage(parsePipeline(json));
  deriveLines.push([json, d.stage, d.phase, d.terminal, d.rule, d.because, d.conflicts.join(';')].join('\t'));
  if (!firstForRule.has(d.rule)) firstForRule.set(d.rule, json);
}

const planLines: string[] = [];
for (const vocabulary of parity.vocabularies) {
  const options: StatusOption[] = vocabulary.statuses.map((s) => ({ id: s.id, name: s.name }));
  const overrides = ('overrides' in vocabulary ? vocabulary.overrides : undefined) as
    Partial<Record<CanonicalStage, string>> | undefined;
  const currents = [...vocabulary.statuses.map((s) => s.name), ...parity.extraStatuses];
  for (const current of currents) {
    for (const json of firstForRule.values()) {
      const plan = planStatusSync({
        pipeline: parsePipeline(json),
        currentStatus: current,
        statusOptions: options,
        // How the server calls it: it always has the list, or knows it is empty.
        statusesLoaded: options.length > 0,
        statusesUnavailable: options.length === 0,
        overrides,
      });
      const to = plan.kind === 'skip' ? undefined : plan.to;
      planLines.push([
        vocabulary.name, current, json, plan.kind,
        plan.kind === 'apply' ? '' : plan.reason,
        plan.kind === 'suggest' ? plan.reasons.join(',') : '',
        to?.name ?? '', to?.id ?? '', to?.match ?? '', to?.canonical ?? '',
      ].join('\t'));
    }
  }
}

describe('leadStatusSync agrees with the server (LeadStatusDerivation.cs)', () => {
  it('derives the same stage for every reachable pipeline', () => {
    dump('derive', deriveLines);
    expect({ cases: deriveLines.length, sha256: digest(deriveLines) }).toEqual(parity.expected.derive);
  });

  it('makes the same decision for every rule, vocabulary and starting status', () => {
    dump('plan', planLines);
    expect({ cases: planLines.length, sha256: digest(planLines) }).toEqual(parity.expected.plan);
  });

  it('covers every rule the derivation has', () => {
    // A rule no pipeline reaches would be a rule the parity check never compared.
    expect([...firstForRule.keys()].sort()).toEqual([
      'no_signal',
      'phase1_attempted', 'phase1_contacted', 'phase1_follow_up', 'phase1_meeting_scheduled', 'phase1_not_interested',
      'phase2_meeting_held', 'phase2_no_show', 'phase2_not_interested', 'phase2_qualified',
      'phase3_contract_sent', 'phase3_contract_to_send', 'phase3_no_longer_interested', 'phase3_profile_rejected',
      'phase4_declined', 'phase4_pending', 'phase4_signed',
      'phase5_deposit_never_paid', 'phase5_deposit_paid',
    ].sort());
  });
});
