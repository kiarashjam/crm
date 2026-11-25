// Settings › Recap emails: the people who see all deals, each with a daily, weekly and monthly
// switch that saves at once; a refusal said in the server's words; a failed read with a retry;
// and nothing to change for someone who does not manage the settings.

import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { RecapRecipients } from '@/app/api/recaps';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-a',
    organizations: [],
    hasFetched: true,
    currentOrg: { id: 'org-a', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 2, permissions },
  };
};

const { RecapEmailsSection } = await import('./RecapEmailsSection');

const BASE = 'http://api.test';

const list = (): RecapRecipients => ({
  timeZone: 'Europe/Zurich',
  notEligible: 2,
  people: [
    { userId: 'u-owner', name: 'Olga Owner', email: 'olga@pavillon46.ch', preset: 'owner', daily: false, weekly: true, monthly: false },
    { userId: 'u-miguel', name: 'Miguel van Ackere', email: 'miguel@pavillon46.ch', preset: 'read_only', daily: false, weekly: false, monthly: true },
  ],
});

let reads: number;
let answer: () => Response;
let saved: { id: string; body: unknown }[];
let refuse: boolean;

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-owner', name: 'Olga', email: 'olga@pavillon46.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  as([PERMISSION.ManageSettings, PERMISSION.EditRecords]);
  reads = 0;
  saved = [];
  refuse = false;
  answer = () => HttpResponse.json(list());
  server.use(
    http.get(`${BASE}/api/recaps/recipients`, () => {
      reads++;
      return answer();
    }),
    http.put(`${BASE}/api/recaps/recipients/:id`, async ({ params, request }) => {
      const body = (await request.json()) as Record<string, boolean>;
      saved.push({ id: String(params.id), body });
      if (refuse) {
        return HttpResponse.json(
          { status: 400, detail: 'Recap emails are the whole team’s figures, so they go only to people who see all deals.', errorCode: 'Recap.NotEligible' },
          { status: 400 });
      }
      const person = list().people.find((p) => p.userId === params.id)!;
      return HttpResponse.json({ ...person, ...body });
    }),
  );
});
afterEach(() => vi.unstubAllEnvs());

describe('Settings › Recap emails', () => {
  it('lists the people who see all deals with their frequencies, says when they go and who cannot have one', async () => {
    render(withQueries(<RecapEmailsSection />));

    const miguel = (await screen.findByText('Miguel van Ackere')).closest('tr')!;
    expect(within(miguel).getByText('miguel@pavillon46.ch · Read-only')).toBeInTheDocument();
    expect(within(miguel).getByRole('switch', { name: 'Daily recap for Miguel van Ackere' })).toHaveAttribute('aria-checked', 'false');
    expect(within(miguel).getByRole('switch', { name: 'Monthly recap for Miguel van Ackere' })).toHaveAttribute('aria-checked', 'true');
    const olga = screen.getByText('Olga Owner').closest('tr')!;
    expect(within(olga).getByRole('switch', { name: 'Weekly recap for Olga Owner' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByText(/Every Monday from 7:00, for Monday to Sunday just ended/)).toBeInTheDocument();
    expect(screen.getByText(/Europe\/Zurich/)).toBeInTheDocument();
    expect(screen.getByText(/2 others on the team do not/)).toBeInTheDocument();
    expect(screen.getByText(/give them the Read-only preset on the Team page/)).toBeInTheDocument();
  });

  it('a switch saves that one frequency at once and shows what was saved', async () => {
    const user = userEvent.setup();
    render(withQueries(<RecapEmailsSection />));

    await user.click(await screen.findByRole('switch', { name: 'Daily recap for Miguel van Ackere' }));

    await waitFor(() => expect(saved).toEqual([{ id: 'u-miguel', body: { daily: true } }]));
    await waitFor(() => expect(screen.getByRole('switch', { name: 'Daily recap for Miguel van Ackere' })).toHaveAttribute('aria-checked', 'true'));
    expect(toast.success).toHaveBeenCalledWith('Saved.');
  });

  it('a refusal is said in the server’s words and the switch stays as it was', async () => {
    refuse = true;
    const user = userEvent.setup();
    render(withQueries(<RecapEmailsSection />));

    await user.click(await screen.findByRole('switch', { name: 'Daily recap for Olga Owner' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringContaining('only to people who see all deals')));
    expect(screen.getByRole('switch', { name: 'Daily recap for Olga Owner' })).toHaveAttribute('aria-checked', 'false');
  });

  it('a failed read says so, with a retry, never an empty list', async () => {
    answer = () => HttpResponse.json({ detail: 'The database went away' }, { status: 500 });
    const user = userEvent.setup();
    render(withQueries(<RecapEmailsSection />));

    expect(await screen.findByRole('alert')).toHaveTextContent('Who receives the recaps');
    expect(screen.queryByText('Nobody here sees all deals yet.')).toBeNull();
    answer = () => HttpResponse.json(list());
    await user.click(screen.getByRole('button', { name: /Try again/ }));
    expect(await screen.findByText('Miguel van Ackere')).toBeInTheDocument();
  });

  it('someone who does not manage the settings is pointed to their own switches, and nothing is read', async () => {
    as([PERMISSION.EditRecords]);
    render(withQueries(<RecapEmailsSection />));

    expect(screen.getByText(/You can switch your own in Notifications/)).toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(reads).toBe(0);
    expect(screen.queryByRole('switch')).toBeNull();
  });
});
