// The one property of the launch overlay that must hold: it hands control back
// exactly once.
//
// Completion has two independent triggers — the progress rail finishing, and a
// timeout that backstops it for a backgrounded tab where rAF never ticks. Two
// triggers is two chances to navigate twice, which in a router means a duplicate
// history entry and a second organisation switch. The animation itself is not
// tested; happy-dom has no compositor and there is nothing to observe.

import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import type { Organization } from '@/app/api/organizations';
import LaunchSequence from './LaunchSequence';

const ORG: Organization = {
  id: 'o-1', name: 'Lac Léman SA', ownerUserId: 'me', isOwner: true, role: 0,
};

const renderIt = (onComplete: () => void, reduceMotion = false) =>
  render(
    <LaunchSequence
      org={ORG}
      destinationLabel="Dashboard"
      reduceMotion={reduceMotion}
      onComplete={onComplete}
    />,
  );

afterEach(() => { vi.useRealTimers(); });

describe('LaunchSequence', () => {
  it('names the workspace and the destination, so neither is a surprise', () => {
    renderIt(vi.fn());
    expect(screen.getByText('Lac Léman SA')).toBeInTheDocument();
    expect(screen.getByText(/Taking you to Dashboard/)).toBeInTheDocument();
    // Announced, because the screen has just changed under a screen-reader user.
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('completes even if the animation never ticks', async () => {
    // A backgrounded tab suspends requestAnimationFrame. Without the backstop the
    // user comes back to an overlay that will never leave.
    vi.useFakeTimers();
    const onComplete = vi.fn();
    renderIt(onComplete);
    expect(onComplete).not.toHaveBeenCalled();
    await act(async () => { await vi.advanceTimersByTimeAsync(2500); });
    expect(onComplete).toHaveBeenCalledTimes(1);
  });

  it('completes exactly once, however long you wait', async () => {
    vi.useFakeTimers();
    const onComplete = vi.fn();
    renderIt(onComplete);
    await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
    expect(onComplete).toHaveBeenCalledTimes(1);
  });

  it('gets out of the way quickly when motion is reduced', async () => {
    vi.useFakeTimers();
    const onComplete = vi.fn();
    renderIt(onComplete, true);
    await act(async () => { await vi.advanceTimersByTimeAsync(1000); });
    expect(onComplete).toHaveBeenCalledTimes(1);
  });

  it('does not call back after it has been unmounted', async () => {
    // The page unmounts this the moment it navigates. A late timer firing into a
    // dead component is how you navigate a second time from a page you left.
    vi.useFakeTimers();
    const onComplete = vi.fn();
    const { unmount } = renderIt(onComplete);
    unmount();
    await act(async () => { await vi.advanceTimersByTimeAsync(5000); });
    expect(onComplete).not.toHaveBeenCalled();
  });
});
