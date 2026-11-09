// Logging a call on a deal or contact: when it happened and how it went.
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Activity } from '@/app/api/types';
import { ActivityLogForm, ActivityRow, withActivity, type ActivityDraft } from './RecordActivity';

vi.mock('@/app/components/ui/select', () => ({
  Select: ({ value, onValueChange, children }: { value?: string; onValueChange?: (v: string) => void; children?: React.ReactNode }) => (
    <select aria-label="Activity type" value={value} onChange={(e) => onValueChange?.(e.target.value)}>{children}</select>
  ),
  SelectTrigger: () => null,
  SelectValue: () => null,
  SelectContent: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children?: React.ReactNode }) => <option value={value}>{children}</option>,
}));

function Form({ onSubmit }: { onSubmit: (draft: ActivityDraft) => void }) {
  const [draft, setDraft] = useState<ActivityDraft>({ type: 'note', subject: 'Pricing call', body: '' });
  return <ActivityLogForm draft={draft} onChange={setDraft} saving={false} onSubmit={(e) => { e.preventDefault(); onSubmit(draft); }} />;
}

describe('ActivityLogForm', () => {
  it('asks a note only when it happened, and a call how it went too', async () => {
    const user = userEvent.setup();
    render(<Form onSubmit={vi.fn()} />);

    expect(screen.getByLabelText('When')).toBeInTheDocument();
    expect(screen.queryByLabelText('Outcome')).toBeNull();

    await user.selectOptions(screen.getByRole('combobox', { name: 'Activity type' }), 'call');

    expect(screen.getByLabelText('Outcome')).toBeInTheDocument();
    expect(screen.getByLabelText('Direction')).toBeInTheDocument();
    expect(screen.getByLabelText('Minutes')).toBeInTheDocument();
  });

  it('keeps what was chosen for the call in the draft it logs', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<Form onSubmit={onSubmit} />);
    await user.selectOptions(screen.getByRole('combobox', { name: 'Activity type' }), 'call');
    await user.selectOptions(screen.getByLabelText('Outcome'), 'no_answer');
    await user.selectOptions(screen.getByLabelText('Direction'), 'outbound');
    await user.type(screen.getByLabelText('Minutes'), '2');

    await user.click(screen.getByRole('button', { name: /Log Activity/ }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
      type: 'call',
      facts: expect.objectContaining({ outcome: 'no_answer', direction: 'outbound', duration: '2' }),
    }));
  });

  it('will not log something dated in the future', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<Form onSubmit={onSubmit} />);
    const tomorrow = new Date(Date.now() + 86_400_000);
    const pad = (n: number) => String(n).padStart(2, '0');
    const value = `${tomorrow.getFullYear()}-${pad(tomorrow.getMonth() + 1)}-${pad(tomorrow.getDate())}T09:00`;

    await user.type(screen.getByLabelText('When'), value);

    expect(screen.getByRole('alert')).toHaveTextContent(/task/);
    expect(screen.getByRole('button', { name: /Log Activity/ })).toBeDisabled();
  });
});

describe('ActivityRow', () => {
  it('shows how the call went and when it happened, not when it was written down', () => {
    const activity: Activity = {
      id: 'a1', type: 'call', subject: 'Called back', createdAt: '2026-11-09T20:00:00Z',
      occurredAt: '2026-11-02T08:30:00Z', outcome: 'connected', direction: 'outbound', durationMinutes: 12,
    };
    render(<ul><ActivityRow activity={activity} index={0} /></ul>);

    expect(screen.getByText(/Outbound · Connected · 12 min/)).toBeInTheDocument();
    expect(screen.getByText((_, el) => el?.tagName === 'TIME' && el.getAttribute('datetime') === '2026-11-02T08:30:00.000Z')).toBeInTheDocument();
  });
});

describe('withActivity', () => {
  it('puts a call logged late below what happened since', () => {
    const at = (id: string, occurredAt: string): Activity => ({ id, type: 'note', createdAt: '2026-11-09T20:00:00Z', occurredAt });
    const list = [at('evening', '2026-11-09T18:00:00Z'), at('morning', '2026-11-09T07:00:00Z')];

    expect(withActivity(list, at('noon', '2026-11-09T12:00:00Z')).map((a) => a.id)).toEqual(['evening', 'noon', 'morning']);
    expect(withActivity(list, at('now', '2026-11-09T19:00:00Z')).map((a) => a.id)).toEqual(['now', 'evening', 'morning']);
    expect(withActivity(list, at('dawn', '2026-11-09T05:00:00Z')).map((a) => a.id)).toEqual(['evening', 'morning', 'dawn']);
  });
});
