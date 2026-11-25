// "How is this score calculated?": closed until asked, then the six signs in plain words with
// the club's current weights as shares of 100 (adding up to 100), the watch and at-risk
// thresholds, the windows, the member's own past, the club's settings and the months of club life
// the score needs; a failed read of the weights says so, with a retry.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ApiError } from '@/app/api/apiClient';
import { DEFAULT_RETENTION_SETTINGS, type RetentionSettings } from '@/app/api/retention';
import { withQueries } from '@/test/queryWrapper';

const api = vi.hoisted(() => ({ settings: vi.fn<() => Promise<RetentionSettings>>() }));
vi.mock('@/app/api/retention', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/retention')>()),
  getRetentionSettings: () => api.settings(),
}));

const { RiskScoreExplainer, sharesOf100, RETENTION_SETTINGS_PATH } = await import('./RiskScoreExplainer');

beforeEach(() => {
  api.settings.mockReset();
  api.settings.mockResolvedValue(DEFAULT_RETENTION_SETTINGS);
});

const renderIt = () => render(withQueries(<MemoryRouter><RiskScoreExplainer /></MemoryRouter>));

/** Each line of the list as "share label", heaviest first. */
const signs = () =>
  within(screen.getByRole('list', { name: 'The six signs and their weights' })).getAllByRole('listitem')
    .map((li) => li.textContent?.replace(/\.\s.*$/, '') ?? '');

describe('the shares of 100', () => {
  it('add up to 100 whatever the club typed', () => {
    expect(sharesOf100([30, 25, 15, 15, 10, 5])).toEqual([30, 25, 15, 15, 10, 5]);
    expect(sharesOf100([40, 15, 25, 10, 15, 5])).toEqual([36, 14, 23, 9, 14, 4]);
    expect(sharesOf100([1, 1, 1])).toEqual([34, 33, 33]);
    expect(sharesOf100([0, 0, 0])).toEqual([0, 0, 0]);
    expect(sharesOf100([-5, 10])).toEqual([0, 100]);
  });
});

describe('how is this score calculated?', () => {
  it('stays closed, and reads nothing, until asked', () => {
    renderIt();
    expect(screen.getByRole('button', { name: 'How is this score calculated?' })).toHaveAttribute('aria-expanded', 'false');
    expect(api.settings).not.toHaveBeenCalled();
  });

  it('explains the six signs with their weights, the thresholds, the windows and what the score needs', async () => {
    const user = userEvent.setup();
    renderIt();

    await user.click(screen.getByRole('button', { name: 'How is this score calculated?' }));

    expect(await screen.findByRole('list', { name: 'The six signs and their weights' })).toBeInTheDocument();
    expect(signs()).toEqual([
      '30 of 100Visits', '25 of 100Unpaid balance', '15 of 100Spending', '15 of 100Complaints and concerns',
      '10 of 100Events', '5 of 100Length of membership',
    ]);
    expect(screen.getByText(/worth watching/).closest('p')).toHaveTextContent('A member is worth watching from 35 and at risk from 60.');
    expect(screen.getByText(/over the last 90 days, against the 270 days before/)).toBeInTheDocument();
    expect(screen.getByText(/in the last 6 months, against the 6 months before/)).toBeInTheDocument();
    expect(screen.getByText(/not the club’s average|not the club's average/)).toBeInTheDocument();
    expect(screen.getByText(/needs a few months of club life \(visits, spending, events\)/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Club settings › Retention & referrals/ })).toHaveAttribute('href', RETENTION_SETTINGS_PATH);
  });

  it('shows the club’s current weights and thresholds, heaviest first', async () => {
    api.settings.mockResolvedValue({ ...DEFAULT_RETENTION_SETTINGS, visitWeight: 40, eventWeight: 25, atRiskThreshold: 55, watchThreshold: 30 });
    const user = userEvent.setup();
    renderIt();

    await user.click(screen.getByRole('button', { name: 'How is this score calculated?' }));

    expect(await screen.findByRole('list', { name: 'The six signs and their weights' })).toBeInTheDocument();
    // 40, 25, 15, 25, 15, 5 of 125.
    expect(signs()).toEqual([
      '32 of 100Visits', '20 of 100Unpaid balance', '20 of 100Events', '12 of 100Spending', '12 of 100Complaints and concerns',
      '4 of 100Length of membership',
    ]);
    expect(screen.getByText(/worth watching/).closest('p')).toHaveTextContent('worth watching from 30 and at risk from 55');
  });

  it('says so when the club weighs every sign at 0', async () => {
    api.settings.mockResolvedValue({
      ...DEFAULT_RETENTION_SETTINGS, visitWeight: 0, spendWeight: 0, balanceWeight: 0, eventWeight: 0, concernWeight: 0, tenureWeight: 0,
    });
    const user = userEvent.setup();
    renderIt();
    await user.click(screen.getByRole('button', { name: 'How is this score calculated?' }));
    expect(await screen.findByText(/so every score is 0/)).toBeInTheDocument();
  });

  it('a failed read of the weights says so, with a retry', async () => {
    api.settings.mockRejectedValueOnce(new ApiError('The server is down for a moment', 503, '/api/retention/settings', true));
    const user = userEvent.setup();
    renderIt();
    await user.click(screen.getByRole('button', { name: 'How is this score calculated?' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('How the score is weighed');
    await user.click(screen.getByRole('button', { name: /Try again/ }));
    expect(await screen.findByRole('list', { name: 'The six signs and their weights' })).toBeInTheDocument();
  });
});
