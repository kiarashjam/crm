import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';

const reduced = { value: true };
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => reduced.value }));

import { CadenceLoader, Spinner } from './CadenceLoader';

describe('CadenceLoader', () => {
  it('is decoration, hidden from assistive technology, at the size asked for', () => {
    const { container } = render(<CadenceLoader size="lg" />);
    const root = container.firstElementChild as HTMLElement;
    expect(root.getAttribute('aria-hidden')).toBe('true');
    expect(root.style.width).toBe('96px');
    // The four orbiting dots are there.
    expect(container.querySelectorAll('span[style*="border-radius"], span.rounded-full[style*="background"]').length).toBeGreaterThanOrEqual(4);
  });

  it('holds still with reduced motion but keeps its shape', () => {
    reduced.value = true;
    const { container } = render(<CadenceLoader />);
    expect(container.querySelector('svg')).not.toBeNull();
  });
});

describe('Spinner', () => {
  it('is the size asked for and hidden from assistive technology', () => {
    const { container } = render(<Spinner size={28} />);
    const root = container.firstElementChild as HTMLElement;
    expect(root.style.width).toBe('28px');
    expect(root.getAttribute('aria-hidden')).toBe('true');
  });
});
