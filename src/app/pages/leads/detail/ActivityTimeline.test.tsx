// A lead's timeline files each activity under the day it happened at the club.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { Activity } from '@/app/api/types';

const workspace = vi.hoisted(() => ({ current: null as null | { id: string; name: string; timeZone?: string } }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => (workspace.current ? { currentOrg: workspace.current } : null),
}));

const { ActivityTimeline, relativeDayLabel } = await import('./ActivityTimeline');

const activity = (id: string, createdAt: string, subject: string): Activity =>
  ({ id, type: 'call', subject, createdAt } as Activity);

// A call at ten past midnight on 1 November in Zurich, and one late on 31 October there.
const afterMidnight = activity('a', '2026-10-31T23:10:00Z', 'Called back after the gala');
const lateEvening = activity('b', '2026-10-31T21:00:00Z', 'Left a message');

describe("a lead's timeline", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    // Half past midnight on 1 November in Zurich; still 31 October in UTC and New York.
    vi.setSystemTime(new Date('2026-10-31T23:30:00Z'));
  });
  afterEach(() => {
    vi.useRealTimers();
    workspace.current = null;
  });

  it("files activities under the club's days, with the club's times", () => {
    workspace.current = { id: 'o', name: 'Pavillon 46', timeZone: 'Europe/Zurich' };
    render(<ActivityTimeline activities={[lateEvening, afterMidnight]} />);

    const today = screen.getByText('Today').closest('section')!;
    expect(within(today).getByText('Called back after the gala')).toBeInTheDocument();
    const clubTime = new Date(afterMidnight.createdAt).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit', timeZone: 'Europe/Zurich' });
    expect(within(today).getByText(clubTime)).toBeInTheDocument();
    const yesterday = screen.getByText('Yesterday').closest('section')!;
    expect(within(yesterday).getByText('Left a message')).toBeInTheDocument();
  });

  it("keeps to a workspace's own days wherever the browser is", () => {
    workspace.current = { id: 'o', name: 'Brooklyn Club', timeZone: 'America/New_York' };
    render(<ActivityTimeline activities={[lateEvening, afterMidnight]} />);

    const today = screen.getByText('Today').closest('section')!;
    expect(within(today).getByText('Called back after the gala')).toBeInTheDocument();
    expect(within(today).getByText('Left a message')).toBeInTheDocument();
    expect(screen.queryByText('Yesterday')).toBeNull();
  });
});

describe('a day heading', () => {
  it('names an older day as the day it is, with its year when it is another year', () => {
    const now = new Date('2026-11-04T10:00:00Z');
    expect(relativeDayLabel('2026-11-02', 'Europe/Zurich', now)).toMatch(/Monday/);
    expect(relativeDayLabel('2026-11-02', 'Europe/Zurich', now)).not.toMatch(/2026/);
    expect(relativeDayLabel('2025-12-31', 'Europe/Zurich', now)).toMatch(/2025/);
  });
});
