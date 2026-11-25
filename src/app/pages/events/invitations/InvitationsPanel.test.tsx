// An event's invitations on its page: who was invited and where each stands, the counts, one
// reminder for those who have not answered, the expected attendance labelled as an estimate,
// and "Invite members" in three steps (who, the letter, the send) sent once with its own id.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { ClubEvent } from '@/app/api/events';
import type { EventInvitations, ExpectedAttendance, InvitationPreview, InvitationSetup } from '@/app/api/eventInvitations';
import type { MemberAudience } from '@/app/api/memberAudience';
import { withQueries } from '@/test/queryWrapper';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), warning: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const api = vi.hoisted(() => ({
  list: vi.fn(), expected: vi.fn(), remind: vi.fn(), setup: vi.fn(), audience: vi.fn(), preview: vi.fn(), test: vi.fn(), send: vi.fn(),
}));
vi.mock('@/app/api/eventInvitations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/eventInvitations')>()),
  getEventInvitations: (...a: unknown[]) => api.list(...a),
  getExpectedAttendance: (...a: unknown[]) => api.expected(...a),
  remindInvitations: (...a: unknown[]) => api.remind(...a),
  getInvitationSetup: (...a: unknown[]) => api.setup(...a),
  getInvitationAudience: (...a: unknown[]) => api.audience(...a),
  previewInvitation: (...a: unknown[]) => api.preview(...a),
  sendInvitationTest: (...a: unknown[]) => api.test(...a),
  sendInvitations: (...a: unknown[]) => api.send(...a),
}));

const { InvitationsPanel } = await import('./InvitationsPanel');

const EVENT: ClubEvent = {
  id: 'ev-1', name: 'Wine night', location: 'Library Bar', startAtUtc: new Date(Date.now() + 7 * 86_400_000).toISOString(),
  endAtUtc: new Date(Date.now() + 7 * 86_400_000 + 3 * 3_600_000).toISOString(), capacity: 20, registeredCount: 5, waitlistCount: 1,
  memberPrice: 40, guestPrice: 60, visibility: 'MembersAndGuests', status: 'Published', coverEmoji: '🍷',
};

const LIST: EventInvitations = {
  open: true,
  counts: { invited: 4, coming: 1, waitlist: 1, declined: 1, noAnswer: 1, cancelled: 0, came: 0, noShow: 0, reminded: 0, canRemind: 1 },
  invitations: [
    { id: 'i1', memberId: 'm1', memberName: 'Anna Keller', language: 'en', state: 'coming', invitedAtUtc: '2026-11-20T10:00:00Z', answeredAtUtc: '2026-11-21T10:00:00Z', emailStatus: 'opened' },
    { id: 'i2', memberId: 'm2', memberName: 'Bruno Keller', language: 'de', state: 'waitlist', waitlistPosition: 2, invitedAtUtc: '2026-11-20T10:00:00Z' },
    { id: 'i3', memberId: 'm3', memberName: 'Clara Keller', language: 'fr', state: 'declined', invitedAtUtc: '2026-11-20T10:00:00Z' },
    { id: 'i4', memberId: 'm4', memberName: 'Dario Keller', language: 'en', state: 'no_answer', invitedAtUtc: '2026-11-20T10:00:00Z', emailStatus: 'bounced' },
  ],
};

const EXPECTED: ExpectedAttendance = { expected: 3.6, seats: 5, members: 4, membersWithHistory: 2, guests: 1, checkedIn: 0, clubRate: 0.6667, clubHasHistory: true };

const SETUP: InvitationSetup = {
  defaults: {
    en: { subject: 'You are invited: {{eventName}}', body: 'Dear {{firstName}},\nWill you come? {{replyLink}}' },
    de: { subject: 'Einladung: {{eventName}}', body: 'Guten Tag {{firstName}},\nKommen Sie? {{replyLink}}' },
    fr: { subject: 'Invitation : {{eventName}}', body: 'Bonjour {{firstName}},\nSerez-vous des nôtres ? {{replyLink}}' },
  },
  fields: ['firstName', 'eventName', 'eventDate', 'replyLink'],
  fromName: 'Pavillon 46', replyTo: 'events@pavillon46.ch', timeZone: 'Europe/Zurich', maxRecipients: 2000,
};

const AUDIENCE: MemberAudience = {
  purpose: 'marketing', chosen: 5, matching: 4, withAddress: 4, agreed: 3, willReceive: 3, suppressed: 0, doNotContact: 0,
  statusExcluded: 1, erased: 0, noEmail: 0, noConsent: 1, alreadyRegistered: 0, alreadyInvited: 0, duplicates: 0,
  recipients: [{ memberId: 'm5', name: 'Elsa Keller', email: 'elsa@example.ch', tier: 'Gold', status: 'Active', language: 'de' }],
  leftOut: [],
};

const PREVIEW: InvitationPreview = {
  language: 'de', subject: 'Einladung: Wine night', body: 'Guten Tag Elsa,\nKommen Sie? https://crm.example.ch/invitation/sample',
  sampleName: 'Elsa Keller', sampleEmail: 'elsa@example.ch', unknown: [], fromName: 'Pavillon 46', replyTo: 'events@pavillon46.ch',
};

const show = () => render(withQueries(<MemoryRouter><InvitationsPanel event={EVENT} /></MemoryRouter>));

beforeEach(() => {
  toast.success.mockReset();
  toast.error.mockReset();
  api.list.mockReset().mockResolvedValue(LIST);
  api.expected.mockReset().mockResolvedValue(EXPECTED);
  api.remind.mockReset().mockResolvedValue({ reminded: 1, alreadyReminded: 0, notSent: 0 });
  api.setup.mockReset().mockResolvedValue(SETUP);
  api.audience.mockReset().mockResolvedValue(AUDIENCE);
  api.preview.mockReset().mockResolvedValue(PREVIEW);
  api.test.mockReset().mockResolvedValue({ to: 'olga@pavillon46.ch', status: 'sent', language: 'de' });
  api.send.mockReset().mockResolvedValue({ id: 'send-1', status: 'sent', recipients: 3, leftOut: 2, alreadySent: false, createdAtUtc: '2026-11-24T10:00:00Z' });
});

describe('the invitations on an event’s page', () => {
  it('lists who was invited and where each stands, with the counts', async () => {
    show();
    expect(await screen.findByTestId('invitation-counts')).toHaveTextContent('Invited 4 · Coming 1 · Waitlist 1 · Declined 1 · No answer 1');
    const rows = within(screen.getByRole('list', { name: 'Who was invited' })).getAllByRole('listitem');
    expect(rows.map((r) => r.textContent)).toEqual([
      expect.stringContaining('Anna Keller'), expect.stringContaining('Bruno Keller'), expect.stringContaining('Clara Keller'), expect.stringContaining('Dario Keller'),
    ]);
    expect(rows[0]).toHaveTextContent('Coming');
    expect(rows[0]).toHaveTextContent('email opened');
    expect(rows[1]).toHaveTextContent('Waitlist #2');
    expect(rows[2]).toHaveTextContent('Declined');
    expect(rows[3]).toHaveTextContent('No answer');
    expect(rows[3]).toHaveTextContent('email bounced');
  });

  it('shows the expected attendance, labelled as an estimate with how it was worked out', async () => {
    show();
    const line = await screen.findByTestId('expected-attendance');
    expect(line).toHaveTextContent(/Expected attendance \(an estimate\): about 3[.,]6 of 5 with a seat/);
    expect(line).toHaveTextContent('club’s average of 67%');
  });

  it('reminds those who have not answered, once, and says how many went', async () => {
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /Remind those who haven’t answered \(1\)/ }));
    await waitFor(() => expect(api.remind).toHaveBeenCalledWith('ev-1'));
    expect(api.remind).toHaveBeenCalledTimes(1);
    expect(toast.success).toHaveBeenCalledWith('1 reminder on the way.');
  });

  it('a list that did not load says so, with a way to try again', async () => {
    api.list.mockRejectedValueOnce(new Error('The server is not answering.'));
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /Try again|Retry/ }));
    expect(await screen.findByTestId('invitation-counts')).toBeInTheDocument();
  });
});

describe('inviting members', () => {
  async function openDialog() {
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /Invite members/ }));
    const dialog = await screen.findByRole('dialog');
    return { user, dialog };
  }

  it('counts who it reaches, writes the letter in each language, and sends once with its own id', async () => {
    const { user, dialog } = await openDialog();
    await waitFor(() => expect(within(dialog).getAllByTestId('audience-will-receive')[0]).toHaveTextContent('3'));
    expect(api.audience).toHaveBeenCalledWith('ev-1', { statuses: ['Active'] }, expect.anything());
    expect(within(dialog).getByRole('list', { name: 'Left out, by reason' })).toHaveTextContent('1 no consent to marketing by email');

    await user.click(within(dialog).getByRole('button', { name: /Next: the letter/ }));
    expect(within(dialog).getByLabelText('Subject')).toHaveValue('You are invited: {{eventName}}');
    await user.click(within(dialog).getByRole('tab', { name: 'Deutsch' }));
    expect(within(dialog).getByLabelText('Subject')).toHaveValue('Einladung: {{eventName}}');
    // Previewed as the first member it goes to, in their language.
    expect(await within(dialog).findByText('Einladung: Wine night')).toBeInTheDocument();
    expect(api.preview).toHaveBeenLastCalledWith('ev-1', expect.objectContaining({ sampleMemberId: 'm5' }), expect.anything());

    await user.click(within(dialog).getByRole('button', { name: /Send a test to me/ }));
    await waitFor(() => expect(api.test).toHaveBeenCalledTimes(1));
    expect(toast.success).toHaveBeenCalledWith('Test sent to olga@pavillon46.ch. Members have not received it.');

    await user.click(within(dialog).getByRole('button', { name: /Next: review/ }));
    await user.click(within(dialog).getByRole('button', { name: 'Invite 3 members' }));
    await user.click(within(dialog).getByRole('button', { name: /Send the invitations/ }));

    await waitFor(() => expect(api.send).toHaveBeenCalledTimes(1));
    const [eventId, request] = api.send.mock.calls[0]! as [string, { sendId: string; audience: unknown; letters: Record<string, { subject: string; body: string }>; expectedRecipients: number }];
    expect(eventId).toBe('ev-1');
    expect(request.sendId).toMatch(/^[0-9a-f-]{36}$/);
    expect(request.audience).toEqual({ statuses: ['Active'] });
    expect(request.expectedRecipients).toBe(3);
    expect(Object.keys(request.letters).sort()).toEqual(['de', 'en', 'fr']);
    expect(await within(dialog).findByText('3 members invited.')).toBeInTheDocument();
  });

  it('a letter without the reply link goes no further, and a retried send keeps its id', async () => {
    const { user, dialog } = await openDialog();
    await waitFor(() => expect(within(dialog).getAllByTestId('audience-will-receive')[0]).toHaveTextContent('3'));
    await user.click(within(dialog).getByRole('button', { name: /Next: the letter/ }));
    const body = within(dialog).getByLabelText('Letter');
    await user.clear(body);
    await user.type(body, 'Dear member, come along.');
    expect(within(dialog).getAllByText(/The English letter has no \{\{replyLink\}\}/)[0]).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: /Next: review/ })).toBeDisabled();
    await user.click(within(dialog).getByRole('button', { name: /Back to the default text/ }));
    await waitFor(() => expect(within(dialog).getByRole('button', { name: /Next: review/ })).toBeEnabled());

    api.send.mockRejectedValueOnce(new Error('The connection was lost.'));
    await user.click(within(dialog).getByRole('button', { name: /Next: review/ }));
    // Not while the letter is still being checked: the button waits for the preview.
    await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Invite 3 members' })).toBeEnabled());
    await user.click(within(dialog).getByRole('button', { name: 'Invite 3 members' }));
    await user.click(within(dialog).getByRole('button', { name: /Send the invitations/ }));
    expect(await within(dialog).findByText('The connection was lost.')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Invite 3 members' }));
    await user.click(within(dialog).getByRole('button', { name: /Send the invitations/ }));
    await waitFor(() => expect(api.send).toHaveBeenCalledTimes(2));
    const ids = api.send.mock.calls.map((c) => (c[1] as { sendId: string }).sendId);
    expect(ids[0]).toBe(ids[1]);
  });
});
