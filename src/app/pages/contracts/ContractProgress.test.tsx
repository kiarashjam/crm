// What the panel actually SAYS in each state.
//
// `describeState` is tested separately as logic. This is the other half: that the
// component puts those words on screen, marks the right step, and shows the turn
// badge — because a tracker whose steps and prose disagree is worse than no
// tracker, and that disagreement is invisible in a unit test of either alone.

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ContractProgress } from './ContractProgress';
import { CONTRACT_STEPS, type DescribeInput } from './contractLifecycle';

// The real hook reads a media query and the theme context; neither is interesting
// here and the provider is not mounted.
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const base = { counterpartyName: 'Jean Dupont' };
const show = (over: Partial<DescribeInput> & { status: DescribeInput['status'] }) =>
  render(<ContractProgress contract={{ ...base, ...over }} />);

/** Ticks are the completed steps: the check marks, not the numbers. */
const completedCount = (c: HTMLElement) => c.querySelectorAll('svg.lucide-check').length;

describe('every state names all four steps', () => {
  it('so the shape of the process is always visible', () => {
    const { container } = show({ status: 'draft' });
    for (const label of CONTRACT_STEPS) {
      expect(screen.getByText(label), label).toBeInTheDocument();
    }
    expect(container).toBeTruthy();
  });
});

describe('the words on screen', () => {
  it('a draft says nobody has seen it, and it is your move', () => {
    show({ status: 'draft' });
    expect(screen.getByText('Not sent yet')).toBeInTheDocument();
    expect(screen.getByText('Your turn')).toBeInTheDocument();
    expect(screen.getByText(/Nobody outside your team/)).toBeInTheDocument();
  });

  it('a sent contract names the person and says it is theirs', () => {
    show({ status: 'sent', sentAtUtc: new Date(Date.now() - 3 * 864e5).toISOString() });
    expect(screen.getByText('Waiting for Jean Dupont to sign')).toBeInTheDocument();
    expect(screen.getByText('Their turn')).toBeInTheDocument();
    expect(screen.getByText(/have not opened it yet/)).toBeInTheDocument();
  });

  it('their signature hands it back to you', () => {
    show({ status: 'signed_by_client', clientSignedAtUtc: new Date().toISOString() });
    expect(screen.getByText('Jean Dupont has signed')).toBeInTheDocument();
    expect(screen.getByText('Your turn')).toBeInTheDocument();
  });

  it('a fully delivered contract says nobody is waiting', () => {
    show({
      status: 'countersigned',
      counterSignedAtUtc: new Date().toISOString(),
      executedCopySentAtUtc: new Date().toISOString(),
    });
    expect(screen.getByText('Signed by both parties')).toBeInTheDocument();
    expect(screen.queryByText('Your turn')).not.toBeInTheDocument();
    expect(screen.queryByText('Their turn')).not.toBeInTheDocument();
  });

  it('EXECUTED BUT UNDELIVERED still asks something of you', () => {
    // The state that matters most: binding, but nobody has been sent it. If this
    // rendered as finished, a member would never receive their copy.
    show({ status: 'countersigned', counterSignedAtUtc: new Date().toISOString() });
    expect(screen.getByText(/copies not sent/i)).toBeInTheDocument();
    expect(screen.getByText('Your turn')).toBeInTheDocument();
  });
});

describe('the ticks match the prose', () => {
  it('counts up as the contract progresses', () => {
    expect(completedCount(show({ status: 'draft' }).container)).toBe(0);
    expect(completedCount(show({ status: 'sent' }).container)).toBe(1);
    expect(completedCount(show({ status: 'signed_by_client' }).container)).toBe(2);
    expect(completedCount(show({
      status: 'countersigned', executedCopySentAtUtc: new Date().toISOString(),
    }).container)).toBe(4);
  });

  it('does NOT show four of four for a contract that stopped', () => {
    // A declined or voided contract showing a full row of ticks would read as
    // successfully completed.
    expect(completedCount(show({ status: 'declined' }).container)).toBeLessThan(4);
    expect(completedCount(show({ status: 'voided' }).container)).toBe(0);
  });

  it('does not tick the last step until the copies have actually gone', () => {
    expect(completedCount(show({
      status: 'countersigned', counterSignedAtUtc: new Date().toISOString(),
    }).container)).toBe(3);
  });
});

describe('accessibility', () => {
  it('announces the state once, not twice', () => {
    // The step row duplicates the band in pictures. Announcing both would make a
    // screen reader read the whole state twice on every change.
    const { container } = show({ status: 'sent' });
    expect(container.querySelectorAll('[role="status"]')).toHaveLength(1);
    expect(container.querySelector('ol')).toHaveAttribute('aria-hidden');
  });

  it('never renders an empty explanation, whatever the state', () => {
    for (const status of ['draft', 'sent', 'signed_by_client', 'countersigned', 'declined', 'voided'] as const) {
      const { container, unmount } = show({ status });
      const band = container.querySelector('[role="status"]');
      expect(band?.textContent?.trim(), status).toBeTruthy();
      expect(band?.textContent, status).not.toMatch(/undefined|null|NaN/);
      unmount();
    }
  });
});
