// The club's public membership application, for someone with no account.
//
// It sends no token: the link is the whole of it. What it sends carries the applicant's
// answer to news by email, false unless they ticked the box, and the hidden honeypot. A
// dead link says so, and a network failure offers to try again rather than looking dead.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';

const PublicApply = (await import('./PublicApply')).default;

const BASE = 'http://api.test';
let posted: { body: Record<string, unknown>; auth: string | null } | null;

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  // Someone signed in on this browser: the public form still sends no token.
  localStorage.setItem('aci_token', 'staff-token');
  posted = null;
  server.use(
    http.get(`${BASE}/api/public/apply/tok`, () => HttpResponse.json({
      clubName: 'Pavillon 46', intro: 'Tell us about yourself.', consentText: 'Yes, send me the club’s news by email.',
      tiers: [{ name: 'Gold', price: 299, cadence: 'Monthly', initiationFee: 1000, currency: 'CHF' }],
    })),
    http.post(`${BASE}/api/public/apply/tok`, async ({ request }) => {
      posted = { body: (await request.json()) as Record<string, unknown>, auth: request.headers.get('Authorization') };
      return HttpResponse.json({ message: 'Thank you. The committee meets on Tuesdays.' }, { status: 201 });
    }),
    http.get(`${BASE}/api/public/apply/gone`, () => new HttpResponse(null, { status: 404 })),
  );
});
afterEach(() => vi.unstubAllEnvs());

const show = (token: string) => render(
  <MemoryRouter initialEntries={[`/apply/${token}`]}>
    <Routes><Route path="/apply/:token" element={<PublicApply />} /></Routes>
  </MemoryRouter>,
);

describe('the public application form', () => {
  it("sends the application with the consent given, without anyone's sign-in, and shows the club's thanks", async () => {
    const user = userEvent.setup();
    show('tok');

    expect(await screen.findByRole('heading', { name: 'Apply to Pavillon 46' })).toBeInTheDocument();
    await user.type(screen.getByLabelText(/First name/), 'Lena');
    await user.type(screen.getByLabelText(/Last name/), 'Keller');
    await user.type(screen.getByLabelText(/^Email/), 'lena@example.ch');
    await user.selectOptions(screen.getByLabelText('Membership'), 'Gold');
    await user.click(screen.getByLabelText('Yes, send me the club’s news by email.'));
    await user.click(screen.getByRole('button', { name: 'Send my application' }));

    expect(await screen.findByText('Thank you. The committee meets on Tuesdays.')).toBeInTheDocument();
    expect(posted?.auth).toBeNull();
    expect(posted?.body).toMatchObject({
      firstName: 'Lena', lastName: 'Keller', email: 'lena@example.ch', requestedTier: 'Gold', marketingConsent: true, website: '',
    });
  });

  it('records no consent unless the box is ticked', async () => {
    const user = userEvent.setup();
    show('tok');

    await user.type(await screen.findByLabelText(/First name/), 'Marc');
    await user.type(screen.getByLabelText(/Last name/), 'Weber');
    await user.type(screen.getByLabelText(/^Email/), 'marc@example.ch');
    await user.click(screen.getByRole('button', { name: 'Send my application' }));

    await waitFor(() => expect(posted).not.toBeNull());
    expect(posted?.body.marketingConsent).toBe(false);
  });

  it('says a dead link is no longer available', async () => {
    show('gone');
    expect(await screen.findByRole('heading', { name: 'This form is no longer available' })).toBeInTheDocument();
  });

  it('says so when too many applications arrive at once, and keeps what was typed', async () => {
    server.use(http.post(`${BASE}/api/public/apply/tok`, () => new HttpResponse(null, { status: 429 })));
    const user = userEvent.setup();
    show('tok');

    await user.type(await screen.findByLabelText(/First name/), 'Lena');
    await user.type(screen.getByLabelText(/Last name/), 'Keller');
    await user.type(screen.getByLabelText(/^Email/), 'lena@example.ch');
    await user.click(screen.getByRole('button', { name: 'Send my application' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Please try again in a minute.');
    expect(screen.getByLabelText(/First name/)).toHaveValue('Lena');
  });

  it('leaves the news box out when the workspace has it off', async () => {
    server.use(http.get(`${BASE}/api/public/apply/tok`, () => HttpResponse.json({
      clubName: 'Pavillon 46', consentText: 'Yes, send me the club’s news by email.', tiers: [], marketingConsent: 'off',
    })));
    show('tok');
    expect(await screen.findByRole('heading', { name: 'Apply to Pavillon 46' })).toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.getByRole('link', { name: 'privacy notice' })).toHaveAttribute('href', '/privacy?form=tok');
  });

  it('makes the news box a condition of applying when the workspace requires it', async () => {
    server.use(http.get(`${BASE}/api/public/apply/tok`, () => HttpResponse.json({
      clubName: 'Pavillon 46', consentText: 'Yes, send me the club’s news by email.', tiers: [], marketingConsent: 'required',
    })));
    show('tok');
    expect(await screen.findByRole('checkbox', { name: /send me the club’s news/ })).toBeRequired();
  });
});
