import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BulkActionsBar } from './BulkActionsBar';

const show = (onBulkApply = vi.fn().mockResolvedValue(undefined)) => {
  render(
    <BulkActionsBar
      count={3}
      members={[{ userId: 'u1', name: 'Ann' }]}
      onClear={vi.fn()}
      onBulkDelete={vi.fn().mockResolvedValue(undefined)}
      onBulkApply={onBulkApply}
    />,
  );
  return onBulkApply;
};

describe('BulkActionsBar', () => {
  it('offers no status field — status follows each lead’s pipeline', () => {
    show();
    expect(screen.queryByLabelText(/status/i)).toBeNull();
    expect(screen.getByRole('toolbar', { name: 'Actions for 3 selected leads' })).toBeInTheDocument();
  });

  it('Apply waits until something is chosen, then sends only that', async () => {
    const apply = show();
    const user = userEvent.setup();
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
    await user.selectOptions(screen.getByLabelText('Set owner'), 'Ann');
    await user.click(screen.getByRole('button', { name: 'Apply' }));
    expect(apply).toHaveBeenCalledWith({ source: undefined, assignedToUserId: 'u1' });
  });
});
