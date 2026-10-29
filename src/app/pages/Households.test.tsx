// Household "roll up billing".
//
// The add-member dialog offered a switch promising "Charges this member incurs land
// on the primary member's account", and each such member was marked "rolls up". The
// flag was saved and nothing read it: a spouse's spa bill stayed on the spouse's
// account and statement, so the club billed the wrong person or missed the charge.
// Until roll-up is applied, the page says what really happens instead.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const Households = (await import('./Households')).default;

beforeEach(() => {
  // Demo data: the Hartwell household has a spouse and a dependent saved with roll-up on.
  localStorage.clear();
});

describe('households', () => {
  it('does not claim that anyone’s charges roll up to the primary member', async () => {
    render(<MemoryRouter><Households /></MemoryRouter>);

    expect((await screen.findAllByText(/Hartwell/)).length).toBeGreaterThan(0);
    expect(screen.queryByText(/rolls up/)).toBeNull();
    expect(screen.queryByText(/roll-up billing\./i)).toBeNull();
  });

  it('offers no roll-up switch when a member is added, and says the charges stay with them', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><Households /></MemoryRouter>);

    await user.click((await screen.findAllByRole('button', { name: 'Add member' }))[0]!);

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByRole('switch')).toBeNull();
    expect(within(dialog).queryByText(/land on the primary member/)).toBeNull();
    expect(within(dialog).getByText(/stay on their own house account and statement/)).toBeInTheDocument();
    expect(within(dialog).getByText(/not applied yet/)).toBeInTheDocument();
  });
});
