// The two failure modes of an entry animation, pinned.
//
// Neither is visible in a screenshot: a stagger that grows without limit looks
// identical to a correct one until the list is long, and a replaying animation
// looks correct on load and only misbehaves once someone types in a filter.

import { describe, it, expect } from 'vitest';
import {
  staggerDelay, isRevealing, revealMotion, REVEAL_WINDOW_MS,
} from './pageEnter';

describe('staggerDelay', () => {
  it('cascades the first few items', () => {
    expect(staggerDelay(0)).toBe(0);
    expect(staggerDelay(1)).toBeCloseTo(0.045);
    expect(staggerDelay(3)).toBeCloseTo(0.135);
  });

  it('CAPS the delay, so a long list still finishes arriving', () => {
    // The whole point. At 45ms per item an uncapped stagger puts lead 200 nine
    // seconds after lead 1, which reads as a broken page, not a flourish.
    const last = staggerDelay(11, { cap: 12 });
    expect(staggerDelay(200, { cap: 12 })).toBeCloseTo(staggerDelay(12, { cap: 12 }));
    expect(staggerDelay(5000, { cap: 12 })).toBeLessThan(last + 0.05);
    expect(staggerDelay(5000)).toBeLessThan(0.6);
  });

  it('never returns a negative or non-finite delay', () => {
    // Framer rejects those, and the source of a bad index is usually an
    // indexOf() that missed.
    for (const bad of [-1, -100, NaN, Infinity, -Infinity]) {
      const d = staggerDelay(bad);
      expect(Number.isFinite(d)).toBe(true);
      expect(d).toBeGreaterThanOrEqual(0);
    }
  });

  it('honours a base offset for content that should wait its turn', () => {
    expect(staggerDelay(0, { base: 0.2 })).toBeCloseTo(0.2);
    expect(staggerDelay(2, { base: 0.2, step: 0.05 })).toBeCloseTo(0.3);
  });

  it('is monotone up to the cap, so nothing arrives out of order', () => {
    let prev = -1;
    for (let i = 0; i <= 12; i += 1) {
      const d = staggerDelay(i);
      expect(d).toBeGreaterThanOrEqual(prev);
      prev = d;
    }
  });
});

describe('isRevealing', () => {
  it('is closed before there is anything to show', () => {
    // openedAt stays null while the page is loading. Animating then would mean
    // animating a skeleton.
    expect(isRevealing(null, 0)).toBe(false);
    expect(isRevealing(null, 10_000)).toBe(false);
  });

  it('is open the moment the content appears', () => {
    expect(isRevealing(1000, 1000)).toBe(true);
    expect(isRevealing(1000, 1000 + REVEAL_WINDOW_MS - 1)).toBe(true);
  });

  it('SHUTS, so a later remount is placed rather than replayed', () => {
    // This is the guard against the Leads list re-cascading on every keystroke:
    // filtering remounts rows long after the window has closed.
    expect(isRevealing(1000, 1000 + REVEAL_WINDOW_MS)).toBe(false);
    expect(isRevealing(1000, 60_000)).toBe(false);
  });

  it('takes the window length as an argument, for slower pages', () => {
    expect(isRevealing(0, 1500, 2000)).toBe(true);
    expect(isRevealing(0, 2500, 2000)).toBe(false);
  });
});

describe('revealMotion', () => {
  const VARIANTS = ['up', 'pop', 'fade', 'slide'] as const;

  it('always ends fully visible and untransformed', () => {
    // If a variant forgets to return a property to its resting value, the
    // element is left permanently nudged — a whole page half a pixel off.
    for (const v of VARIANTS) {
      const m = revealMotion(v);
      expect(m.animate.opacity, v).toBe(1);
      for (const key of Object.keys(m.initial)) {
        expect(m.animate, `${v}.${key}`).toHaveProperty(key);
      }
      expect(m.animate.y ?? 0, v).toBe(0);
      expect(m.animate.x ?? 0, v).toBe(0);
      expect(m.animate.scale ?? 1, v).toBe(1);
    }
  });

  it('starts invisible, so nothing flashes before it moves', () => {
    for (const v of VARIANTS) expect(revealMotion(v).initial.opacity, v).toBe(0);
  });

  it('animates ONLY compositor properties', () => {
    // Animating height or width would reflow every frame and shift content under
    // the reader while they are looking at it.
    const allowed = new Set(['opacity', 'x', 'y', 'scale']);
    for (const v of VARIANTS) {
      const m = revealMotion(v);
      for (const key of [...Object.keys(m.initial), ...Object.keys(m.animate)]) {
        expect(allowed.has(key), `${v} animates ${key}`).toBe(true);
      }
    }
  });

  it('keeps every entry short enough to feel like arrival, not loading', () => {
    for (const v of VARIANTS) {
      expect(revealMotion(v).duration, v).toBeGreaterThan(0);
      expect(revealMotion(v).duration, v).toBeLessThanOrEqual(0.6);
    }
  });

  it('the whole cascade lands inside the reveal window', () => {
    // The window gates whether a NEWLY MOUNTING child animates. If it shuts
    // mid-cascade, an item arriving at the boundary snaps while the one just
    // before it animated — visibly inconsistent. So the window has to outlast
    // the slowest possible entry, not merely most of it.
    const slowest = Math.max(...VARIANTS.map((v) => revealMotion(v).duration));
    const cascadeEndsMs = (staggerDelay(5000) + slowest) * 1000;
    expect(cascadeEndsMs).toBeLessThanOrEqual(REVEAL_WINDOW_MS);
  });

  it('the window is still short enough to guard against replay', () => {
    // It only has to close before a human could plausibly type into a filter.
    expect(REVEAL_WINDOW_MS).toBeLessThan(2500);
  });

  it('falls back to a sane variant for an unknown name', () => {
    expect(revealMotion('nonsense' as never)).toEqual(revealMotion('up'));
  });
});
