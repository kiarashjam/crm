// A member's reply page for an event invitation, as the member who followed their link sees it:
// in their language, with the club, the event, the price and their own answer; "I'll come" says
// plainly whether they have a seat or are waiting; and it refuses with a reason once replies close.

import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { I18nProvider, loadCatalogue } from '@/app/i18n';
import type { InvitationPage } from '@/app/api/eventInvitations';

const api = vi.hoisted(() => ({ get: vi.fn(), answer: vi.fn() }));
vi.mock('@/app/api/eventInvitations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/eventInvitations')>()),
  getInvitationPage: (...a: unknown[]) => api.get(...a),
  answerInvitation: (...a: unknown[]) => api.answer(...a),
}));

const { InvitationPageError } = await import('@/app/api/eventInvitations');
const InvitationReply = (await import('./InvitationReply')).default;

const PAGE: InvitationPage = {
  club: 'Pavillon 46', firstName: 'Anna', language: 'en', locale: 'de-CH', timeZone: 'Europe/Zurich',
  eventName: 'Wine night', description: 'Six wines from the Lavaux.', location: 'Library Bar',
  startAtUtc: '2026-12-11T18:00:00Z', endAtUtc: '2026-12-11T21:00:00Z', coverEmoji: '🍷',
  memberPrice: 40, currency: 'CHF', answer: 'none',
};

const show = (token = 'tok') => render(
  <I18nProvider>
    <MemoryRouter initialEntries={[`/invitation/${token}`]}>
      <Routes><Route path="/invitation/:token" element={<InvitationReply />} /></Routes>
    </MemoryRouter>
  </I18nProvider>,
);

beforeAll(async () => {
  await loadCatalogue('fr');
  await loadCatalogue('de');
});

beforeEach(() => {
  localStorage.clear();
  api.get.mockReset().mockResolvedValue(PAGE);
  api.answer.mockReset().mockImplementation(async (_token: string, answer: string) => ({ ...PAGE, answer, place: answer === 'coming' ? 'seat' : null }));
});

describe('the reply page', () => {
  it('shows the club, the event, its time and the price, and changes nothing until a button is pressed', async () => {
    show();
    expect(await screen.findByRole('heading', { name: /Wine night/ })).toBeInTheDocument();
    expect(screen.getByText('Hello Anna,')).toBeInTheDocument();
    expect(screen.getByText('Pavillon 46 invites you to')).toBeInTheDocument();
    expect(screen.getByText('Library Bar')).toBeInTheDocument();
    // 18:00 UTC is 19:00 in Lausanne in December: the club's time zone, whatever the reader's.
    expect(screen.getByText(/December 2026/)).toHaveTextContent(/19:00|7:00/);
    expect(screen.getByText(/CHF/)).toHaveTextContent(/40/);
    expect(api.answer).not.toHaveBeenCalled();
  });

  it('opens in the member’s own language', async () => {
    api.get.mockResolvedValue({ ...PAGE, language: 'fr' });
    show();
    expect(await screen.findByText('Bonjour Anna,')).toBeInTheDocument();
    expect(screen.getByText('Pavillon 46 vous invite à')).toBeInTheDocument();
    expect(screen.getByText(/décembre 2026/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Je viendrai' })).toBeInTheDocument();
  });

  it('says plainly that “I’ll come” gave them a seat', async () => {
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'I’ll come' }));

    expect(api.answer).toHaveBeenCalledWith('tok', 'coming');
    expect(await screen.findByRole('status')).toHaveTextContent('You have a seat.');
    expect(screen.getByRole('button', { name: 'I’ll come' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('or that the event is full and they are on the waitlist, at their place in it', async () => {
    api.answer.mockResolvedValue({ ...PAGE, answer: 'coming', place: 'waitlist', waitlistPosition: 3 });
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'I’ll come' }));

    expect(await screen.findByRole('status')).toHaveTextContent('you are on the waitlist, number 3');
  });

  it('records “I can’t come”, and lets them change their mind', async () => {
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'I can’t come' }));
    expect(api.answer).toHaveBeenCalledWith('tok', 'declined');
    expect(await screen.findByRole('status')).toHaveTextContent('Thank you for letting us know.');

    await user.click(screen.getByRole('button', { name: 'I’ll come' }));
    expect(api.answer).toHaveBeenLastCalledWith('tok', 'coming');
    expect(await screen.findByText('You have a seat. We look forward to seeing you.')).toBeInTheDocument();
  });

  it('refuses once the event has started, for a cancelled event, and for a link that is not one of ours', async () => {
    api.get.mockRejectedValueOnce(new InvitationPageError('closed', 409, 'Invitation.Started.Conflict'));
    const { unmount } = show();
    expect(await screen.findByRole('heading', { name: 'Replies are closed' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'I’ll come' })).toBeNull();
    unmount();

    api.get.mockRejectedValueOnce(new InvitationPageError('cancelled', 409, 'Invitation.Cancelled.Conflict'));
    const second = show();
    expect(await screen.findByRole('heading', { name: 'This event was cancelled' })).toBeInTheDocument();
    second.unmount();

    api.get.mockRejectedValueOnce(new InvitationPageError('gone', 404, 'Invitation.NotFound'));
    const third = show();
    expect(await screen.findByRole('heading', { name: 'This link is not valid' })).toBeInTheDocument();
    third.unmount();

    api.get.mockRejectedValueOnce(new InvitationPageError('off', 403, 'Module.Off'));
    show();
    expect(await screen.findByText(/not taking replies here at the moment/)).toBeInTheDocument();
  });

  it('a test invitation’s link says it is a sample, and asks the server nothing', async () => {
    show('sample');
    expect(await screen.findByRole('heading', { name: 'This is a sample link' })).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });

  it('too many presses say so, and change nothing on the page', async () => {
    api.answer.mockRejectedValue(new InvitationPageError('slow down', 429, null));
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'I’ll come' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Too many attempts. Please try again in a minute.');
    expect(screen.queryByRole('status')).toBeNull();
  });
});
