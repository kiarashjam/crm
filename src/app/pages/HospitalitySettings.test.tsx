// Hospitality settings: what saving here changes, and what it does not.
//
// The page said "Changes apply to all new transactions once saved", and showed
// "Settings saved", but nothing read the tiers, dues, loyalty rates or venue points:
// a manager who set Platinum to 3x saw every tab close still award 2x. The page now
// says which settings are not applied yet. The club currency is the one that is.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { HospitalitySettings as Settings } from '@/app/api/hospitalitySettings';
import { resetClubCurrency, useClubMoney } from '@/app/hooks/useClubMoney';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const api = {
  get: vi.fn<() => Promise<Settings>>(),
  update: vi.fn<(patch: Partial<Settings>) => Promise<Settings>>(),
};
vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: () => api.get(),
  updateHospitalitySettings: (patch: Partial<Settings>) => api.update(patch),
}));

const { default: HospitalitySettings } = await import('./HospitalitySettings');
const actual = await vi.importActual<typeof import('@/app/api/hospitalitySettings')>('@/app/api/hospitalitySettings');

/** Somewhere else in the app that shows money, open at the same time. */
function OtherClubPage() {
  const { format } = useClubMoney();
  return <p data-testid="elsewhere">{format(12.5)}</p>;
}

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

let defaults: Settings;
beforeEach(async () => {
  localStorage.clear();
  resetClubCurrency();
  // The demo store's defaults, read through the real module.
  defaults = await actual.getHospitalitySettings();
  api.get.mockReset().mockResolvedValue(defaults);
  api.update.mockReset().mockImplementation(async (patch) => ({ ...defaults, ...patch }));
});

const show = () => render(
  <MemoryRouter>
    <HospitalitySettings />
    <OtherClubPage />
  </MemoryRouter>,
);

describe('hospitality settings', () => {
  it('says which settings are saved but not applied yet', async () => {
    show();

    expect(await screen.findByText(/Only the club currency is applied today/)).toBeInTheDocument();
    expect(screen.getAllByText('Not applied yet').length).toBeGreaterThan(0);
    expect(screen.queryByText(/Changes apply to all new transactions/)).toBeNull();
  });

  it('defaults the club currency to francs and shows it in the money labels', async () => {
    show();

    expect(await screen.findByLabelText('Club currency')).toHaveTextContent('CHF');
    expect(screen.getAllByLabelText('Monthly dues (CHF)').length).toBe(4);
  });

  it('saves a new club currency and every open club page switches to it', async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByLabelText('Club currency'));
    await user.click(await screen.findByRole('option', { name: /EUR/ }));
    await user.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(api.update).toHaveBeenCalled());
    expect(api.update.mock.calls[0]![0]).toMatchObject({ currency: 'EUR' });
    await waitFor(() => expect(screen.getByTestId('elsewhere')).toHaveTextContent('€'));
  });
});
