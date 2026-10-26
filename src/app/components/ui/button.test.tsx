import { describe, it, expect, vi, afterEach } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { Button } from './button';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: vi.fn(() => false) }));
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

const ink = (button: HTMLElement) => button.querySelectorAll('.cadence-ripple');

describe('Button', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.mocked(useMotionPreference).mockReturnValue(false);
  });

  it('spreads ink from a press and clears it afterwards', () => {
    vi.useFakeTimers();
    render(<Button>Save</Button>);
    const button = screen.getByRole('button', { name: 'Save' });
    fireEvent.pointerDown(button, { button: 0, clientX: 10, clientY: 10 });
    expect(ink(button)).toHaveLength(1);
    act(() => { vi.advanceTimersByTime(700); });
    expect(ink(button)).toHaveLength(0);
  });

  it('keeps its accessible name and still calls the caller’s pointer handler', () => {
    const onPointerDown = vi.fn();
    render(<Button onPointerDown={onPointerDown}>Save</Button>);
    const button = screen.getByRole('button', { name: 'Save' });
    fireEvent.pointerDown(button, { button: 0 });
    expect(onPointerDown).toHaveBeenCalledTimes(1);
    expect(button).toHaveAccessibleName('Save');
    expect(button.querySelector('[data-slot="button-ink"]')).toHaveAttribute('aria-hidden');
  });

  it('draws no ink with reduced motion, on links, on a right click, or when slotted', () => {
    vi.mocked(useMotionPreference).mockReturnValue(true);
    const { unmount } = render(<Button>Still</Button>);
    fireEvent.pointerDown(screen.getByRole('button'), { button: 0 });
    expect(ink(screen.getByRole('button'))).toHaveLength(0);
    unmount();

    vi.mocked(useMotionPreference).mockReturnValue(false);
    const link = render(<Button variant="link">Link</Button>);
    fireEvent.pointerDown(screen.getByRole('button'), { button: 0 });
    expect(ink(screen.getByRole('button'))).toHaveLength(0);
    link.unmount();

    const right = render(<Button>Menu</Button>);
    fireEvent.pointerDown(screen.getByRole('button'), { button: 2 });
    expect(ink(screen.getByRole('button'))).toHaveLength(0);
    right.unmount();

    render(<Button asChild><a href="/x">Go</a></Button>);
    const anchor = screen.getByRole('link', { name: 'Go' });
    fireEvent.pointerDown(anchor, { button: 0 });
    expect(ink(anchor)).toHaveLength(0);
    expect(anchor).toHaveAttribute('data-slot', 'button');
  });

  it('lets a caller’s own positioning win over the lift', () => {
    render(<Button className="absolute">Pinned</Button>);
    const button = screen.getByRole('button');
    expect(button).toHaveClass('absolute');
    expect(button).not.toHaveClass('relative');
  });
});
