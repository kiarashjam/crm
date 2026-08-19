// The guard that matters: content mounted after the reveal window must be PLACED,
// not animated. Without it, every keystroke in the Leads filter re-cascades the
// page — which looks correct on first load and only misbehaves once someone uses
// the page for real.
//
// happy-dom has no compositor, so these read the props Framer was handed rather
// than pixels. That is the right level: the bug is a wrong decision about whether
// to animate, not a wrong curve.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PageEnter, Reveal } from './PageEnter';
import { REVEAL_WINDOW_MS } from './pageEnter';

// Capture what each motion element is constructed with.
const seen: { initial: unknown; transition?: { delay?: number } }[] = [];

interface MockProps {
  children?: React.ReactNode;
  initial?: unknown;
  animate?: unknown;
  transition?: { delay?: number };
  className?: string;
}

vi.mock('motion/react', () => {
  const make = (tag: string) => ({ children, initial, transition, className }: MockProps) => {
    seen.push({ initial, transition });
    const T = tag as 'div';
    return (
      <T className={className} data-animated={initial !== false ? 'yes' : 'no'}>{children}</T>
    );
  };
  return {
    motion: { div: make('div'), section: make('section'), li: make('li'), nav: make('nav'), article: make('article') },
    useReducedMotion: () => reduced,
  };
});

let reduced = false;

beforeEach(() => { seen.length = 0; reduced = false; vi.useFakeTimers({ shouldAdvanceTime: true }); });
afterEach(() => { vi.useRealTimers(); });

const animatedCount = () => seen.filter((s) => s.initial !== false).length;

describe('Reveal inside an open window', () => {
  it('animates the content that is there on arrival', () => {
    render(
      <PageEnter>
        <Reveal index={0}><p>first</p></Reveal>
        <Reveal index={1}><p>second</p></Reveal>
      </PageEnter>,
    );
    expect(screen.getByText('first')).toBeInTheDocument();
    expect(animatedCount()).toBe(2);
  });

  it('staggers them, and the first one does not wait', () => {
    render(
      <PageEnter>
        <Reveal index={0}><p>a</p></Reveal>
        <Reveal index={4}><p>b</p></Reveal>
      </PageEnter>,
    );
    const delays = seen.map((s) => s.transition?.delay ?? 0);
    expect(delays[0]).toBe(0);
    expect(delays[1]).toBeGreaterThan(delays[0]!);
  });

  it('caps the delay so a long list is not still arriving seconds later', () => {
    render(<PageEnter><Reveal index={500}><p>x</p></Reveal></PageEnter>);
    expect(seen[0]!.transition?.delay).toBeLessThan(0.6);
  });
});

describe('Reveal after the window shuts', () => {
  it('places later content instead of replaying the cascade', () => {
    const { rerender } = render(
      <PageEnter>
        <Reveal index={0}><p>original</p></Reveal>
      </PageEnter>,
    );
    expect(animatedCount()).toBe(1);
    seen.length = 0;

    vi.advanceTimersByTime(REVEAL_WINDOW_MS + 50);

    // A filter change: a brand-new row mounts long after load.
    rerender(
      <PageEnter>
        <Reveal index={0}><p>original</p></Reveal>
        <Reveal index={1}><p>filtered in</p></Reveal>
      </PageEnter>,
    );
    expect(screen.getByText('filtered in')).toBeInTheDocument();
    expect(screen.getByText('filtered in').closest('[data-animated]'))
      .toHaveAttribute('data-animated', 'no');
  });

  it('does not yank a mid-flight element back when the window shuts under it', () => {
    // Reveal freezes its decision at mount. If it re-read the window on every
    // render, a re-render at the boundary would flip an animating element back to
    // its initial state — a visible snap backwards.
    const { rerender } = render(
      <PageEnter><Reveal index={0}><p>flying</p></Reveal></PageEnter>,
    );
    expect(animatedCount()).toBe(1);
    seen.length = 0;
    vi.advanceTimersByTime(REVEAL_WINDOW_MS + 50);
    rerender(<PageEnter><Reveal index={0}><p>flying</p></Reveal></PageEnter>);
    expect(seen.every((s) => s.initial !== false)).toBe(true);
  });
});

describe('PageEnter opens its window twice', () => {
  it('animates content that does not depend on the request', () => {
    // A page header owes nothing to the fetch. It should arrive immediately, not
    // sit still until the data comes back.
    render(
      <PageEnter ready={false}>
        <Reveal index={0}><p>header</p></Reveal>
      </PageEnter>,
    );
    expect(animatedCount()).toBe(1);
  });

  it('opens again when the data finally arrives, however slow it was', () => {
    const { rerender } = render(<PageEnter ready={false}><div /></PageEnter>);
    seen.length = 0;
    vi.advanceTimersByTime(4000); // a slow request, far past one window's length
    rerender(
      <PageEnter ready>
        <Reveal index={0}><p>rows</p></Reveal>
      </PageEnter>,
    );
    // Measured from when the content appeared. With only a mount-time window, a
    // page slower than 1.4s animated its header and then snapped the rows in.
    expect(animatedCount()).toBe(1);
  });

  it('does not open a THIRD time, so a refetch cannot replay the page', () => {
    const { rerender } = render(<PageEnter ready={false}><div /></PageEnter>);
    rerender(<PageEnter ready><Reveal index={0}><p>rows</p></Reveal></PageEnter>);
    seen.length = 0;
    // A later refetch toggles loading again. The window must stay shut.
    vi.advanceTimersByTime(REVEAL_WINDOW_MS + 50);
    rerender(<PageEnter ready={false}><div /></PageEnter>);
    rerender(
      <PageEnter ready>
        <Reveal index={0}><p>rows</p></Reveal>
        <Reveal index={1}><p>more rows</p></Reveal>
      </PageEnter>,
    );
    expect(screen.getByText('more rows')).toBeInTheDocument();
    expect(animatedCount()).toBe(0);
  });
});

describe('reduced motion', () => {
  it('turns the entry off rather than shortening it', () => {
    reduced = true;
    render(
      <PageEnter>
        <Reveal index={0}><p>a</p></Reveal>
        <Reveal index={1} variant="pop"><p>b</p></Reveal>
      </PageEnter>,
    );
    expect(screen.getByText('a')).toBeInTheDocument();
    expect(animatedCount()).toBe(0);
  });
});

describe('a Reveal with no provider', () => {
  it('still shows its content', () => {
    // Failing closed. A Reveal that started at opacity 0 with nothing to trigger
    // it would leave the block permanently invisible — the worst outcome for a
    // decorative wrapper.
    render(<Reveal index={0}><p>orphan</p></Reveal>);
    expect(screen.getByText('orphan')).toBeInTheDocument();
    expect(animatedCount()).toBe(0);
  });
});
