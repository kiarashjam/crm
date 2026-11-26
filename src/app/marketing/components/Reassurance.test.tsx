import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { Reassurance } from './Reassurance';
import { StickySignupBar } from './StickySignupBar';

afterEach(() => vi.useRealTimers());

const at = (iso: string) => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(iso));
};

describe('the reassurance under the hero', () => {
  it('answers the doubts while the launch offer runs, in the page language', () => {
    at('2026-11-26T10:00:00Z');
    render(<Reassurance lang="de" />);
    expect(screen.getByText('Keine Kreditkarte')).toBeInTheDocument();
    expect(screen.getByText('Alle Funktionen inklusive')).toBeInTheDocument();
  });

  it('is gone once the offer has ended', () => {
    at('2027-01-01T00:00:00+01:00');
    const { container } = render(<Reassurance lang="en" />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('the phone bar', () => {
  it('appears only once the hero has scrolled away, and leads to the sign-up form', () => {
    at('2026-11-26T10:00:00Z');
    render(<MemoryRouter><StickySignupBar lang="fr" /></MemoryRouter>);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();

    Object.defineProperty(window, 'scrollY', { value: 900, configurable: true });
    act(() => { window.dispatchEvent(new Event('scroll')); });

    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/login?mode=register');
    expect(link).toHaveAttribute('data-track', 'cta-signup-bar');
    expect(screen.getByText(/Gratuit jusqu’au 31\.12\.2026/)).toBeInTheDocument();
    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
  });
});
