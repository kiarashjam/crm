// Settings › Notifications › Recap emails: the person's own daily, weekly and monthly recap in
// this workspace, each switch saving at once; a person who does not see all deals is told why
// they get none; a failed read says so, with a retry.

import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import type { MyRecaps } from '@/app/api/recaps';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { MyRecapsCard } = await import('./MyRecapsCard');

const BASE = 'http://api.test';

let answer: () => Response;
let saved: unknown[];

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-miguel', name: 'Miguel', email: 'miguel@pavillon46.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  saved = [];
  const mine: MyRecaps = { eligible: true, daily: true, weekly: true, monthly: false, timeZone: 'Europe/Zurich' };
  answer = () => HttpResponse.json(mine);
  server.use(
    http.get(`${BASE}/api/settings/recaps`, () => answer()),
    http.put(`${BASE}/api/settings/recaps`, async ({ request }) => {
      const body = (await request.json()) as Partial<MyRecaps>;
      saved.push(body);
      return HttpResponse.json({ ...mine, ...body });
    }),
  );
});
afterEach(() => vi.unstubAllEnvs());

const toggle = (label: string) => within(screen.getByText(label).closest('label')!).getByRole('switch');

describe('my recap emails', () => {
  it('shows my three recaps and saves one switched off at once', async () => {
    const user = userEvent.setup();
    render(withQueries(<MyRecapsCard />));

    await screen.findByText('Weekly recap');
    expect(toggle('Daily recap')).toHaveAttribute('aria-checked', 'true');
    expect(toggle('Weekly recap')).toHaveAttribute('aria-checked', 'true');
    expect(toggle('Monthly recap')).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByText(/Europe\/Zurich/)).toBeInTheDocument();

    await user.click(toggle('Weekly recap'));

    await waitFor(() => expect(saved).toEqual([{ weekly: false }]));
    await waitFor(() => expect(toggle('Weekly recap')).toHaveAttribute('aria-checked', 'false'));
    expect(toast.success).toHaveBeenCalledWith('Saved.');
  });

  it('someone who does not see all deals is told why there is none', async () => {
    answer = () => HttpResponse.json({ eligible: false, daily: false, weekly: false, monthly: false, timeZone: 'Europe/Zurich' });
    render(withQueries(<MyRecapsCard />));

    expect(await screen.findByText(/they go to people who see all deals, which your role in this workspace does not/)).toBeInTheDocument();
    expect(screen.queryByRole('switch')).toBeNull();
  });

  it('a failed read says so, with a retry', async () => {
    answer = () => HttpResponse.json({ detail: 'Down for a moment' }, { status: 503 });
    const user = userEvent.setup();
    render(withQueries(<MyRecapsCard />));

    expect(await screen.findByRole('alert')).toHaveTextContent('Your recap emails');
    answer = () => HttpResponse.json({ eligible: true, daily: false, weekly: false, monthly: true, timeZone: 'Europe/Zurich' });
    await user.click(screen.getByRole('button', { name: /Try again/ }));
    expect(await screen.findByText('Monthly recap')).toBeInTheDocument();
  });

  it('shows nothing outside a workspace', () => {
    localStorage.removeItem('aci_org_id');
    const { container } = render(withQueries(<MyRecapsCard />));
    expect(container).toBeEmptyDOMElement();
  });
});
