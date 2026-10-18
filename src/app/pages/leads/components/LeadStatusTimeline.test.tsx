import { describe, expect, it, onTestFinished, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

const history = vi.fn();
vi.mock('@/app/api/leadOps', () => ({ getLeadHistory: (...a: unknown[]) => history(...a) }));
const { LeadStatusTimeline } = await import('./LeadStatusTimeline');

describe('LeadStatusTimeline', () => {
  it('loads again when the lead moves on, so the new step shows without a reload', async () => {
    history.mockResolvedValueOnce([{ id: 'e1', fromStatus: null, toStatus: 'New', changedAtUtc: '2026-10-01T08:00:00' }]);
    const { rerender } = render(<LeadStatusTimeline leadId="l1" status="New" />);
    expect(await screen.findByText(/New/)).toBeInTheDocument();

    history.mockResolvedValueOnce([
      { id: 'e2', fromStatus: 'New', toStatus: 'Attempted Contact', changedAtUtc: '2026-10-02T08:00:00' },
      { id: 'e1', fromStatus: null, toStatus: 'New', changedAtUtc: '2026-10-01T08:00:00' },
    ]);
    rerender(<LeadStatusTimeline leadId="l1" status="Attempted Contact" />);
    expect(await screen.findByText(/New → Attempted Contact/)).toBeInTheDocument();
    expect(history).toHaveBeenCalledTimes(2);
  });

  it('reads the server time as UTC', async () => {
    // Pinned to Zurich: in a UTC test run, reading it as local time looks right.
    const tz = process.env.TZ;
    process.env.TZ = 'Europe/Zurich';
    onTestFinished(() => { process.env.TZ = tz; });
    history.mockResolvedValueOnce([{ id: 'e1', fromStatus: null, toStatus: 'New', changedAtUtc: '2026-10-01T08:00:00' }]);
    render(<LeadStatusTimeline leadId="l2" status="New" />);
    const expected = new Date(Date.UTC(2026, 9, 1, 8)).toLocaleString();
    await waitFor(() => expect(screen.getByText(new RegExp(expected.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))).toBeInTheDocument());
  });
});
