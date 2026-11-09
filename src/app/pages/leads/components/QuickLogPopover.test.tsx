// The quick log on a lead: a call can say how it went, and when it happened.
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { toLocalInputValue } from '@/app/lib/dateInputs';

const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const { QuickLogPopover } = await import('./QuickLogPopover');

async function openWith(onSubmit: (payload: unknown) => Promise<void>) {
  const user = userEvent.setup();
  render(<QuickLogPopover onSubmit={onSubmit} trigger={<button type="button">Log activity</button>} />);
  await user.click(screen.getByRole('button', { name: 'Log activity' }));
  return user;
}

describe('QuickLogPopover', () => {
  it('logs a call with its outcome, direction, length and the time it was made', async () => {
    const onSubmit = vi.fn(async () => {});
    const user = await openWith(onSubmit);
    const made = toLocalInputValue(new Date(Date.now() - 3 * 3_600_000).toISOString());

    await user.click(screen.getByRole('button', { name: 'Call' }));
    await user.selectOptions(screen.getByLabelText('Outcome'), 'left_voicemail');
    await user.selectOptions(screen.getByLabelText('Direction'), 'outbound');
    await user.type(screen.getByLabelText('Minutes'), '1');
    await user.type(screen.getByLabelText('When'), made);
    await user.type(screen.getByPlaceholderText(/What happened/), 'Left a message about the open evening');
    await user.click(screen.getByRole('button', { name: /^Log$/ }));

    expect(onSubmit).toHaveBeenCalledWith({
      type: 'call',
      body: 'Left a message about the open evening',
      outcome: 'left_voicemail',
      direction: 'outbound',
      durationMinutes: 1,
      occurredAtUtc: new Date(made).toISOString(),
    });
  });

  it('logs a note with nothing but its text when nothing else was said', async () => {
    const onSubmit = vi.fn(async () => {});
    const user = await openWith(onSubmit);

    expect(screen.queryByLabelText('Outcome')).toBeNull();
    await user.type(screen.getByPlaceholderText(/What happened/), 'Prefers mornings');
    await user.click(screen.getByRole('button', { name: /^Log$/ }));

    expect(onSubmit).toHaveBeenCalledWith({ type: 'note', body: 'Prefers mornings' });
  });

  it('says why the server refused it instead of only that it failed', async () => {
    const onSubmit = vi.fn(async () => { throw new Error('An activity is something that has happened.'); });
    const user = await openWith(onSubmit);

    await user.type(screen.getByPlaceholderText(/What happened/), 'x');
    await user.click(screen.getByRole('button', { name: /^Log$/ }));

    expect(toast.error).toHaveBeenCalledWith('An activity is something that has happened.');
  });
});
