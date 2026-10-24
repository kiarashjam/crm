import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Activity, Lead } from '@/app/api/types';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

import { LeadGalleryCard, describeLifecycle, type LeadGalleryCardProps } from './LeadGalleryCard';
import { parsePipeline } from './leadPipeline';
import type { useLeadStatusSync } from './useLeadStatusSync';

const base: Lead = { id: 'l1', name: 'Ada Lovelace', email: 'ada@example.ch', status: 'New' };

const meeting = JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-10-01' });

function activity(id: string, type: string, subject: string, daysAgo: number): Activity {
  return { id, type, subject, leadId: 'l1', createdAt: new Date(Date.now() - daysAgo * 86400000).toISOString() };
}

function renderCard(props: Partial<LeadGalleryCardProps> = {}) {
  const handlers = {
    onOpen: vi.fn(),
    onToggleSelected: vi.fn(),
    onAssign: vi.fn(),
    onQuickLog: vi.fn(async () => {}),
    onSaved: vi.fn(),
  };
  render(
    <MemoryRouter>
      <LeadGalleryCard
        lead={base}
        index={0}
        company="Analytical Engines"
        assignee={null}
        members={[]}
        interactions={[]}
        readOnly={false}
        selected={false}
        selecting={false}
        statusesUnavailable={false}
        statusSync={{} as ReturnType<typeof useLeadStatusSync>}
        {...handlers}
        {...props}
      />
    </MemoryRouter>,
  );
  return handlers;
}

describe('describeLifecycle', () => {
  it('a lead with no steps has not started, coloured by its status', () => {
    const life = describeLifecycle({ ...base, status: 'Contacted' }, parsePipeline(undefined));
    expect(life.title).toBe('Not started');
    expect(life.tone).toBe('amber');
    expect(life.segments).toEqual(['idle', 'idle', 'idle', 'idle', 'idle']);
  });

  it('a lead part-way through names the phase, what it waits on, and takes its colour', () => {
    const life = describeLifecycle(base, parsePipeline(meeting));
    expect(life.title).toBe('Phase 2 · Meeting');
    expect(life.caption).toBe('Waiting on: did they show up?');
    expect(life.tone).toBe('violet');
    expect(life.done).toBe(1);
    expect(life.segments).toEqual(['done', 'current', 'idle', 'idle', 'idle']);
  });

  it('a lead who said no stops at the phase that recorded it', () => {
    const life = describeLifecycle(base, parsePipeline(JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' })));
    expect(life.title).toBe('Stopped at Outreach');
    expect(life.tone).toBe('rose');
    expect(life.segments[0]).toBe('lost');
  });

  it('a converted lead says so, whatever its steps', () => {
    const life = describeLifecycle({ ...base, isConverted: true }, parsePipeline(meeting));
    expect(life.title).toBe('Converted');
    expect(life.tone).toBe('emerald');
  });
});

describe('LeadGalleryCard', () => {
  it('shows where the lead is, the numbers, and the latest two interactions', () => {
    renderCard({
      lead: { ...base, pipelineState: meeting, leadScore: 64 },
      interactions: [
        activity('a1', 'call', 'Booked a viewing', 1),
        activity('a2', 'email', 'Sent the brochure', 2),
        activity('a3', 'note', 'Asked about parking', 3),
      ],
    });
    expect(screen.getByText('Ada Lovelace')).toBeTruthy();
    expect(screen.getByText('Analytical Engines')).toBeTruthy();
    expect(screen.getByText('Phase 2 · Meeting')).toBeTruthy();
    expect(screen.getByRole('img', { name: '1 of 5 phases done. Phase 2 · Meeting.' })).toBeTruthy();
    expect(screen.getByText('64')).toBeTruthy();
    expect(screen.getByText('Last touch').parentElement?.textContent).toContain('1d ago');
    const latest = screen.getByRole('list', { name: 'Latest interactions' });
    expect(latest.textContent).toContain('Booked a viewing');
    expect(latest.textContent).toContain('Sent the brochure');
    expect(latest.textContent).not.toContain('Asked about parking');
    expect(latest.textContent).toContain('+1 more on the lead page');
  });

  it('an update the system wrote is not a touch', () => {
    renderCard({ interactions: [activity('s1', 'system', 'Assigned to Rep', 1)] });
    expect(screen.getByText('Never')).toBeTruthy();
  });

  it('with no interactions it invites the first one', () => {
    renderCard();
    expect(screen.getByText('No interactions yet. Log the first call, email, or note.')).toBeTruthy();
  });

  it('opens the lead from the card, but not from its controls', () => {
    const { onOpen } = renderCard();
    fireEvent.click(screen.getByRole('button', { name: 'Log activity for Ada Lovelace' }));
    expect(onOpen).not.toHaveBeenCalled();
    fireEvent.click(screen.getByText('Not started'));
    expect(onOpen).toHaveBeenCalledWith(base);
  });

  it('opens with Enter on the card itself', () => {
    const { onOpen } = renderCard();
    fireEvent.keyDown(screen.getByRole('button', { name: 'Open Ada Lovelace' }), { key: 'Enter' });
    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it('a viewer sees the owner but cannot select, track, or log', () => {
    renderCard({ readOnly: true });
    expect(screen.queryByRole('checkbox', { name: 'Select Ada Lovelace' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Lead pipeline for Ada Lovelace' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Log activity for Ada Lovelace' })).toBeNull();
    expect(screen.getByText('Unassigned')).toBeTruthy();
    expect(screen.getByText('No interactions yet.')).toBeTruthy();
  });
});
