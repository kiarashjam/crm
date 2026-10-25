import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { Users, Zap } from 'lucide-react';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

import PageHero from './PageHero';

function renderHero(props: Partial<Parameters<typeof PageHero>[0]> = {}) {
  return render(
    <MemoryRouter>
      <PageHero icon={Users} title="Members" subtitle="Tiers, dues and renewals." {...props} />
    </MemoryRouter>,
  );
}

describe('PageHero', () => {
  it('names the page and holds its actions', () => {
    renderHero({ actions: <button type="button">Add member</button> });
    expect(screen.getByRole('heading', { level: 1, name: 'Members' })).toBeTruthy();
    expect(screen.getByText('Tiers, dues and renewals.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Add member' })).toBeTruthy();
  });

  it('carries the key numbers, formatted, with what is behind them', () => {
    renderHero({
      stats: [
        { label: 'Total members', value: 1280, icon: Users, hint: '120 active' },
        { label: 'Dues', value: 4200, icon: Zap, format: (n) => `$${Math.round(n).toLocaleString()}` },
        { label: 'Top tier', value: 'Platinum', icon: Zap },
      ],
    });
    const list = screen.getByRole('list');
    expect(list.textContent).toContain('1,280');
    expect(list.textContent).toContain('Total members');
    expect(list.textContent).toContain('120 active');
    expect(list.textContent).toContain('$4,200');
    expect(list.textContent).toContain('Platinum');
  });

  it('a figure that filters is a toggle button that says whether it is on', () => {
    const onClick = vi.fn();
    renderHero({ stats: [{ label: 'Active', value: 6, icon: Users, onClick, pressed: true, actionLabel: 'Active, 6. Filter is on.' }] });
    const toggle = screen.getByRole('button', { name: 'Active, 6. Filter is on.' });
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
    expect(toggle.textContent).toContain('On');
    fireEvent.click(toggle);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('links back to where the page sits', () => {
    renderHero({ back: { to: '/leads', label: 'Back to Leads' } });
    expect(screen.getByRole('link', { name: 'Back to Leads' }).getAttribute('href')).toBe('/leads');
  });

  it('leaves the slot for the next step after the title, unless asked not to', () => {
    const { container, unmount } = renderHero();
    expect(container.querySelector('[data-next-actions-slot]')).not.toBeNull();
    unmount();
    const again = renderHero({ noAgentSlot: true });
    expect(again.container.querySelector('[data-next-actions-slot]')).toBeNull();
  });
});
