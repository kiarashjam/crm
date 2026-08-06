// The behaviours the transition choreography has to preserve.
//
// These are not animation tests — jsdom/happy-dom has no compositor and Framer's
// output is unobservable here. They pin the things the animation work could
// plausibly break: that opening an old lead does not replay its history, that a
// real transition IS announced, and that exactly one place is "here" at a time.

import { describe, it, expect, vi } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LeadPipelineTracker } from './LeadPipelineTracker';
import type { LeadPipeline } from '../leadPipeline';

/** Phases 1–3 satisfied, phase 4 in progress. */
const AT_PHASE_4: LeadPipeline = {
  outreachStatus: 'contacted',
  contactOutcome: 'meeting_scheduled',
  meetingDate: '2026-08-20',
  meetingAttended: true,
  stillInterested: true,
  contractStatus: 'yes',
  contractSentDate: '2026-08-22',
};

/** The same lead one edit later: phase 4 now complete too. */
const AT_PHASE_5: LeadPipeline = {
  ...AT_PHASE_4,
  contractSigned: 'yes',
  signatureDate: '2026-08-25',
};

const liveRegion = (c: HTMLElement) => c.querySelector('[aria-live="polite"]');
const steps = (c: HTMLElement) => Array.from(c.querySelectorAll('[aria-current="step"]'));

describe('LeadPipelineTracker — transition behaviour', () => {
  it('does NOT replay history when a part-finished lead is opened', () => {
    // The single most important guard: three phases are already complete on the
    // very first render, and none of them is news. A live region populated here
    // would mean a screen reader recites the lead's whole past on every open.
    const { container } = render(
      <LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />,
    );

    expect(liveRegion(container)).toHaveTextContent('');
    // …while still showing the completed state itself.
    expect(screen.getByText(/Currently in Phase 4: Signature/)).toBeInTheDocument();
    expect(screen.getByText('3/5')).toBeInTheDocument();
  });

  it('announces a phase that completes while the tracker is open', () => {
    const { container, rerender } = render(
      <LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />,
    );
    expect(liveRegion(container)).toHaveTextContent('');

    rerender(<LeadPipelineTracker value={AT_PHASE_5} onChange={vi.fn()} />);

    expect(liveRegion(container)).toHaveTextContent(
      'Phase 4 of 5, Signature, complete. Now on phase 5: Deposit.',
    );
    expect(screen.getByText('4/5')).toBeInTheDocument();
  });

  it('says the pipeline is finished rather than naming a next phase', () => {
    const { container, rerender } = render(
      <LeadPipelineTracker value={AT_PHASE_5} onChange={vi.fn()} />,
    );
    rerender(
      <LeadPipelineTracker
        value={{ ...AT_PHASE_5, depositPaid: true, paymentDate: '2026-08-28' }}
        onChange={vi.fn()}
      />,
    );

    expect(liveRegion(container)).toHaveTextContent(/All five phases complete/);
  });

  it('clears the announcement so it is not re-read on the next focus', async () => {
    vi.useFakeTimers();
    try {
      const { container, rerender } = render(
        <LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />,
      );
      rerender(<LeadPipelineTracker value={AT_PHASE_5} onChange={vi.fn()} />);
      expect(liveRegion(container)).not.toHaveTextContent('');

      act(() => { vi.advanceTimersByTime(2500); });
      expect(liveRegion(container)).toHaveTextContent('');
    } finally {
      vi.useRealTimers();
    }
  });

  it('marks exactly one pip and one card as the current step, and moves both', () => {
    // Two elements carry aria-current: the rail pip and the phase card. More than
    // that and assistive tech reports several "here"s; fewer and the position is
    // only conveyed by colour.
    const { container, rerender } = render(
      <LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />,
    );
    let marked = steps(container);
    expect(marked).toHaveLength(2);
    expect(marked.some((el) => el.textContent?.includes('Phase 4, Signature: in progress'))).toBe(true);
    expect(marked.some((el) => el.tagName === 'LI' && el.textContent?.includes('Signature status'))).toBe(true);

    rerender(<LeadPipelineTracker value={AT_PHASE_5} onChange={vi.fn()} />);
    marked = steps(container);
    expect(marked).toHaveLength(2);
    expect(marked.some((el) => el.textContent?.includes('Phase 5, Deposit: in progress'))).toBe(true);
  });

  it('marks nothing as current once the pipeline has stopped', () => {
    // A dropped-out lead is not "in progress" anywhere. The stopped phase gets
    // its own treatment; aria-current would claim the user is working on it.
    const { container } = render(
      <LeadPipelineTracker
        value={{ ...AT_PHASE_4, contractSigned: 'no' }}
        onChange={vi.fn()}
      />,
    );

    expect(steps(container)).toHaveLength(0);
    expect(screen.getByText(/Stopped — Contract declined/)).toBeInTheDocument();
    expect(container.querySelector('[aria-live="polite"]')).toHaveTextContent('');
  });

  it('announces a drop-out that completes no phase at all', () => {
    // Nothing flips false → true here, so the transition detector is silent —
    // but this is the most important thing that just happened.
    const { container, rerender } = render(
      <LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />,
    );
    rerender(
      <LeadPipelineTracker value={{ ...AT_PHASE_4, contractSigned: 'no' }} onChange={vi.fn()} />,
    );

    expect(liveRegion(container)).toHaveTextContent('Pipeline stopped: Contract declined.');
  });

  it('still narrates a stopped pipeline when the tracker is read-only', () => {
    // `disabled` gates EDITING and the celebratory beats. It must not gate the
    // words: a Viewer watching a colleague's change still needs to hear it.
    const { container, rerender } = render(
      <LeadPipelineTracker value={AT_PHASE_4} disabled onChange={vi.fn()} />,
    );
    rerender(
      <LeadPipelineTracker value={{ ...AT_PHASE_4, contractSigned: 'no' }} disabled onChange={vi.fn()} />,
    );

    expect(liveRegion(container)).toHaveTextContent(/Pipeline stopped/);
    // Every EDITING control is disabled…
    for (const name of ['Signed', 'Pending', 'Not signed']) {
      expect(screen.getByRole('button', { name })).toBeDisabled();
    }
    for (const input of container.querySelectorAll('input')) {
      expect(input).toBeDisabled();
    }
    // …but navigating is not editing. A Viewer must still be able to fold cards
    // open and jump around, or the read-only view is unreadable.
    expect(screen.getByRole('button', { name: /expand all/i })).toBeEnabled();
    expect(screen.getByRole('button', { name: /Phase 1, Outreach/ })).toBeEnabled();
  });

  it('offers the convert action only when every phase is complete', () => {
    const onConvert = vi.fn();
    const { rerender } = render(
      <LeadPipelineTracker value={AT_PHASE_5} onChange={vi.fn()} onConvert={onConvert} />,
    );
    expect(screen.queryByRole('button', { name: /convert to deal/i })).toBeNull();

    rerender(
      <LeadPipelineTracker
        value={{ ...AT_PHASE_5, depositPaid: true, paymentDate: '2026-08-28' }}
        onChange={vi.fn()}
        onConvert={onConvert}
      />,
    );
    expect(screen.getByRole('button', { name: /convert to deal/i })).toBeInTheDocument();
  });
});

/**
 * The header button for a phase, which is also its disclosure control.
 * Keyed on `data-phase` rather than the accessible name, which is deliberately
 * the whole header row — title, phase number, pill and caption.
 */
const header = (title: string) =>
  document.querySelector<HTMLButtonElement>(`button[data-phase="${title}"]`)!;
const openTitles = () =>
  Array.from(document.querySelectorAll('button[data-phase][aria-expanded="true"]'))
    .map((b) => b.getAttribute('data-phase'));

describe('LeadPipelineTracker — disclosure', () => {
  it('opens the phase you are on and folds the rest away', () => {
    // The whole point: four of the five phases are asking about things that
    // already happened or have not happened yet.
    render(<LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />);

    expect(openTitles()).toEqual(['Signature']);
    // The folded cards are still listed — folded, not hidden.
    expect(document.querySelectorAll('button[data-phase][aria-expanded="false"]')).toHaveLength(4);
  });

  it('opens the phase that STOPPED the pipeline, not the current one', () => {
    // Rejecting a profile with no meeting logged: current phase is 2, but the
    // story ended at 3, and that is the card worth reading.
    render(
      <LeadPipelineTracker value={{ contractStatus: 'profile_rejected' }} onChange={vi.fn()} />,
    );
    expect(openTitles()).toEqual(['Contract']);
  });

  it('folds everything away once the pipeline is finished', () => {
    // Nothing is outstanding, so nothing needs a form open — the convert action
    // becomes the only thing asking for attention.
    render(
      <LeadPipelineTracker
        value={{ ...AT_PHASE_5, depositPaid: true, paymentDate: '2026-08-28' }}
        onChange={vi.fn()}
        onConvert={vi.fn()}
      />,
    );
    expect(openTitles()).toEqual([]);
    expect(screen.getByRole('button', { name: /convert to deal/i })).toBeInTheDocument();
  });

  it('lets you open any phase by hand and keeps it open', async () => {
    const user = userEvent.setup();
    render(<LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />);

    await user.click(header('Outreach'));
    expect(openTitles()).toEqual(['Outreach', 'Signature']);

    await user.click(header('Outreach'));
    expect(openTitles()).toEqual(['Signature']);
  });

  it('hands over cleanly when a phase completes, without pinning the old one open', async () => {
    // The subtle one. Opening phase 4 by hand records a manual choice; when
    // phase 4 then completes, that choice must not survive, or the finished
    // card stays expanded forever and the fold never advances.
    const user = userEvent.setup();
    const { rerender } = render(<LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />);

    await user.click(header('Contract'));       // an unrelated card, opened on purpose
    expect(openTitles()).toEqual(['Contract', 'Signature']);

    rerender(<LeadPipelineTracker value={AT_PHASE_5} onChange={vi.fn()} />);

    // Signature handed over to Deposit; Contract was the user's own choice and stays.
    expect(openTitles()).toEqual(['Contract', 'Deposit']);
  });

  it('expands and collapses everything from one control', async () => {
    const user = userEvent.setup();
    render(<LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: /expand all/i }));
    expect(openTitles()).toHaveLength(5);

    await user.click(screen.getByRole('button', { name: /^collapse/i }));
    expect(openTitles()).toEqual([]);
  });

  it('jumps to a phase from the step rail', async () => {
    const user = userEvent.setup();
    render(<LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: /Phase 2, Meeting/ }));
    expect(openTitles()).toEqual(['Meeting', 'Signature']);
  });

  it('summarises a folded phase instead of leaving it blank', () => {
    render(<LeadPipelineTracker value={AT_PHASE_4} onChange={vi.fn()} />);
    // Phase 1 is folded, but its header still reports what was recorded.
    expect(header('Outreach')).toHaveAttribute('aria-expanded', 'false');
    expect(header('Outreach')).toHaveTextContent(/Contacted · meeting/);
  });
});
