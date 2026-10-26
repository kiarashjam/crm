import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

const reduced = { value: false };
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => reduced.value }));

import { RouteProgress } from './RouteProgress';

describe('RouteProgress', () => {
  it('draws a line along the top, hidden from assistive technology', () => {
    reduced.value = false;
    const { container } = render(<MemoryRouter initialEntries={['/leads']}><RouteProgress /></MemoryRouter>);
    const bar = container.firstElementChild as HTMLElement;
    expect(bar).not.toBeNull();
    expect(bar.getAttribute('aria-hidden')).toBe('true');
    expect(bar.className).toContain('fixed');
  });

  it('is not there at all with reduced motion', () => {
    reduced.value = true;
    const { container } = render(<MemoryRouter><RouteProgress /></MemoryRouter>);
    expect(container.firstElementChild).toBeNull();
  });
});
