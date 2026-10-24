import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';

const reduced = { value: false };
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => reduced.value }));

import { Confetti } from './Confetti';

describe('Confetti', () => {
  it('bursts when fired, and is hidden from assistive technology', () => {
    reduced.value = false;
    const { container, rerender } = render(<div><Confetti fire={0} /></div>);
    expect(container.querySelector('[aria-hidden]')).toBeNull();
    rerender(<div><Confetti fire={1} count={10} /></div>);
    const burst = container.querySelector('[aria-hidden]');
    expect(burst).not.toBeNull();
    expect(burst?.children.length).toBe(10);
  });

  it('never appears with reduced motion', () => {
    reduced.value = true;
    const { container } = render(<div><Confetti fire={1} /></div>);
    expect(container.querySelector('[aria-hidden]')).toBeNull();
  });
});
