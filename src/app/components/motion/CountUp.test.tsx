import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';

const reduced = { value: true };
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => reduced.value }));

import { CountUp } from './CountUp';

describe('CountUp', () => {
  it('is just the number with reduced motion', () => {
    reduced.value = true;
    const { container } = render(<p><CountUp value={1234} /></p>);
    expect(container.querySelector('[aria-hidden]')?.textContent).toBe('1,234');
  });

  it('gives screen readers the final number while it counts', () => {
    reduced.value = false;
    const { container } = render(<p><CountUp value={42} format={(n) => `${Math.round(n)}%`} /></p>);
    expect(container.querySelector('.sr-only')?.textContent).toBe('42%');
    // The visible number starts low and climbs; it has not jumped straight there.
    expect(container.querySelector('[aria-hidden]')?.textContent).not.toBe('42%');
  });
});
