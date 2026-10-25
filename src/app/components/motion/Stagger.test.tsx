import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';

const reduced = { value: true };
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => reduced.value }));

import { StaggerItem } from './Stagger';

describe('StaggerItem', () => {
  it('renders as the element asked for and passes its props through', () => {
    const onClick = vi.fn();
    render(<ul><StaggerItem as="li" index={3} className="row" role="button" onClick={onClick}>Ada</StaggerItem></ul>);
    const item = screen.getByRole('button', { name: 'Ada' });
    expect(item.tagName).toBe('LI');
    expect(item.className).toContain('row');
    fireEvent.click(item);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('is simply there with reduced motion', () => {
    reduced.value = true;
    render(<StaggerItem>Still</StaggerItem>);
    expect(screen.getByText('Still').getAttribute('style') ?? '').not.toContain('opacity: 0');
  });
});
