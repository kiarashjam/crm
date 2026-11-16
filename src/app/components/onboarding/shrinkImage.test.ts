// What an image is re-saved as before it goes into an email. The re-saving itself needs a
// real browser's canvas, and is checked in the browser walkthrough; this is the decision.
import { describe, expect, it } from 'vitest';
import { bytesLabel, shrinkForEmail, shrinkPlan } from './shrinkImage';

const MB = 1024 * 1024;

describe('making an image fit for an email', () => {
  it('leaves a light image, no wider than 1200 px, as it is', () => {
    expect(shrinkPlan({ width: 1200, height: 800, bytes: 400 * 1024, type: 'image/jpeg', transparent: false })).toBeNull();
    expect(shrinkPlan({ width: 448, height: 84, bytes: 30 * 1024, type: 'image/png', transparent: true })).toBeNull();
  });

  it('makes a wide design export 1200 px wide, as a JPEG', () => {
    // Pavillon 46's photos are PNGs of 1700 px and 5 MB, straight from the design tool.
    expect(shrinkPlan({ width: 1687, height: 1166, bytes: 4.9 * MB, type: 'image/png', transparent: false }))
      .toEqual({ width: 1200, height: 829, type: 'image/jpeg' });
  });

  it('keeps a picture with see-through parts a PNG, so a logo keeps its background', () => {
    expect(shrinkPlan({ width: 2400, height: 450, bytes: 1.2 * MB, type: 'image/png', transparent: true }))
      .toEqual({ width: 1200, height: 225, type: 'image/png' });
  });

  it('re-saves a heavy image even when it is not too wide', () => {
    expect(shrinkPlan({ width: 1000, height: 1400, bytes: 2.5 * MB, type: 'image/png', transparent: false }))
      .toEqual({ width: 1000, height: 1400, type: 'image/jpeg' });
  });

  it('turns a kind mail apps do not show (HEIC from a phone) into a JPEG, when the browser can read it', () => {
    expect(shrinkPlan({ width: 900, height: 600, bytes: 300 * 1024, type: 'image/heic', transparent: false }))
      .toEqual({ width: 900, height: 600, type: 'image/jpeg' });
  });

  it('keeps a GIF as it is, so an animation stays one', async () => {
    const gif = new File([new Uint8Array([0x47, 0x49, 0x46, 0x38])], 'wave.gif', { type: 'image/gif' });
    expect((await shrinkForEmail(gif)).file).toBe(gif);
  });

  it('says a size the way a person reads it', () => {
    expect(bytesLabel(4.9 * MB)).toBe('4.9 MB');
    expect(bytesLabel(312 * 1024)).toBe('312 KB');
    expect(bytesLabel(200)).toBe('1 KB');
  });
});
