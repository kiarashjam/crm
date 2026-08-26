// The badge that replaced the status picker.
//
// Its whole job is to answer "why does this say Contract Sent?", so the one thing
// it must never do is answer wrongly. Rendering it caught exactly that: it showed
// "New" under a tooltip claiming the pipeline had set it to Profile Rejected,
// because it drew the lead's status and described the DERIVED stage without ever
// checking whether the two agreed.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DerivedStatusBadge } from './DerivedStatusBadge';
import type { LeadPipeline } from '../leadPipeline';

const badge = (status: string, pipeline: LeadPipeline) => {
  render(<DerivedStatusBadge status={status} pipeline={pipeline} />);
  // The name carries the cause, which is how it reaches a screen reader; `title`
  // only reaches a pointer.
  return screen.getByText(status || 'No status').closest('span[title]')!;
};

describe('DerivedStatusBadge — it explains itself', () => {
  it('names the step that set the status', () => {
    const el = badge('Awaiting Signature', { contractStatus: 'yes' });
    expect(el.getAttribute('title')).toMatch(/Set automatically from the sales pipeline/);
    expect(el.getAttribute('title')).toMatch(/Contract sent \(Phase 3\)/);
    expect(el.getAttribute('title')).toMatch(/Change it by editing that step/);
  });

  it('says so plainly when nothing has been recorded yet', () => {
    const el = badge('New', {});
    expect(el.getAttribute('title')).toMatch(/No pipeline steps recorded yet/);
    // And it must not claim a phase it does not have.
    expect(el.getAttribute('title')).not.toMatch(/Phase/);
  });

  it('ADMITS a disagreement instead of claiming the pipeline set it', () => {
    // The bug this test exists for. An imported lead, a colleague's edit, or an
    // organisation whose status list has not loaded — all leave the status behind
    // the pipeline, and the badge used to assert the pipeline had set it anyway.
    const el = badge('New', { contractStatus: 'profile_rejected' });
    const title = el.getAttribute('title')!;

    expect(title).not.toMatch(/Set automatically/);
    expect(title).toMatch(/This says "New"/);
    expect(title).toMatch(/but the sales pipeline records Profile rejected \(Phase 3\)/);
    expect(title).toMatch(/Reconcile/);
  });

  it('makes a disagreement look different from every settled state', () => {
    // A stale "Signed" painted emerald asserts a signature the tracker does not
    // have. And the first attempt at a warning tone was amber — which is already
    // the settled colour for a contacted lead, so it was indistinguishable from
    // an ordinary one. The dashed border is the part that cannot be confused.
    const stale = badge('Signed', { outreachStatus: 'contacted' });
    expect(stale.className).toContain('border-dashed');
    expect(stale.className).not.toContain('emerald');
  });

  it('does not flag a settled status as a disagreement', () => {
    const signed = badge('Signed', { contractSigned: 'yes' });
    expect(signed.className).toContain('emerald');
    expect(signed.className).not.toContain('border-dashed');
  });

  it('does not cry stale over two names for the same stage', () => {
    // "Connected" and "Meeting held" are one stage in different vocabularies.
    // Comparing the LABEL instead of the stage would flag every organisation that
    // renamed its statuses.
    const el = badge('Connected', { meetingAttended: true });
    expect(el.getAttribute('title')).toMatch(/Set automatically/);
    expect(el.className).not.toContain('border-dashed');
  });

  it('is not a control', () => {
    render(<DerivedStatusBadge status="Qualified" pipeline={{ stillInterested: true }} />);
    // No button, no listbox, nothing focusable: the status is a result, and
    // offering a control that the next pipeline edit would overwrite is worse
    // than offering none.
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.queryByRole('combobox')).toBeNull();
  });

  it('renders something rather than nothing when the status is missing', () => {
    // An empty badge reads as a rendering fault rather than as absent data.
    render(<DerivedStatusBadge status="" pipeline={{}} />);
    expect(screen.getByText('No status')).toBeTruthy();
  });
});
