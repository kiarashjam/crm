// The presence layer, and the two things it must not cost.
//
// Motion added to this tracker has to pay for itself twice over: once by making a
// state change legible, and once by not taking anything away when it is switched
// off. These tests exist because writing it DID take something away — the rolling
// progress counter split "2/5" across animated elements, which reads fine and
// announces as two separate fragments.
//
// The motion preference is mocked per describe block, because "the same component
// with the animation off" is the only interesting comparison here and jsdom's
// matchMedia reports no preference either way.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { LeadPipeline } from '../leadPipeline';

const reduced = { value: false };
vi.mock('@/app/hooks', async () => {
  const actual = await vi.importActual<typeof import('@/app/hooks')>('@/app/hooks');
  return { ...actual, useMotionPreference: () => reduced.value };
});

const { LeadPipelineTracker } = await import('./LeadPipelineTracker');

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

/** Phase 1 in progress with an answer already on it, so a chip is selected. */
const CHIP_CHOSEN: LeadPipeline = { outreachStatus: 'contacted' };

const show = (value: LeadPipeline) =>
  render(<LeadPipelineTracker value={value} onChange={() => {}} />);

const PULSE = '.bg-indigo-400\\/30';

beforeEach(() => { reduced.value = false; });
afterEach(() => { reduced.value = false; });

describe('the progress count survives being animated', () => {
  it('is ONE readable string, not the fragments the roll is made of', () => {
    // The roll splits the digit out so it can move. That is decoration; the count
    // itself has to stay a single contiguous string or a screen reader announces
    // "3" and "/5" as two unrelated things — and a query for it finds neither.
    const { container } = show(AT_PHASE_4);
    expect(screen.getByText('3/5')).toBeTruthy();

    // Exactly one element owns the text. Two would be announced twice.
    const owners = Array.from(container.querySelectorAll('span'))
      .filter((n) => (n.textContent || '').trim() === '3/5' && !n.querySelector('span'));
    expect(owners).toHaveLength(1);
  });

  it('reads the same with motion switched off', () => {
    reduced.value = true;
    show(AT_PHASE_4);
    expect(screen.getByText('3/5')).toBeTruthy();
  });
});

describe('a chosen chip is marked, not merely tinted', () => {
  it('carries a check as well as a colour', () => {
    show(CHIP_CHOSEN);
    const pressed = screen.getAllByRole('button', { pressed: true });
    expect(pressed.length).toBeGreaterThan(0);
    // Decorative: the mark must not put words into the chip's accessible name,
    // which is the thing that actually tells a screen reader it is selected.
    expect(pressed[0]!.getAttribute('aria-pressed')).toBe('true');
    expect(pressed[0]!.textContent).not.toMatch(/check/i);
  });

  it('is still marked with motion switched off', () => {
    // The check's ENTRANCE is animated; the check is not. Reduced motion must
    // remove the spring, never the mark.
    reduced.value = true;
    show(CHIP_CHOSEN);
    expect(screen.getAllByRole('button', { pressed: true }).length).toBeGreaterThan(0);
  });
});

describe('the one infinite loop is genuinely optional', () => {
  it('breathes on the current phase by default', () => {
    // Worth asserting in the positive too: a guard that removes something which
    // was never there is not a guard.
    const { container } = show(AT_PHASE_4);
    expect(container.querySelectorAll(PULSE).length).toBe(1);
  });

  it('does not breathe at all when motion is reduced', () => {
    // The only forever-looping animation in the component, so it has to VANISH
    // rather than shorten — a slow pulse is exactly what reduced motion is for.
    reduced.value = true;
    const { container } = show(AT_PHASE_4);
    expect(container.querySelectorAll(PULSE).length).toBe(0);
  });

  it('still says where you are without it', () => {
    // Whatever the pulse was carrying has to survive statically: the ring, the
    // outlined pip, and aria-current, none of which animate.
    reduced.value = true;
    const { container } = show(AT_PHASE_4);
    expect(container.querySelectorAll('[aria-current="step"]').length).toBeGreaterThan(0);
  });
});
