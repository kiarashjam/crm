// Campaigns and member messages, now that CadenceE sends them.
//
// Every member email goes through the outbox, one message per member, and each says
// what came of it. These pin what the pages promise: a campaign is sent once, and only
// after saying who it reaches and who it does not; a test goes to the person who asked
// for it and to nobody else; a campaign that has gone can no longer be changed; a
// marketing email to a member who has not agreed is not sent, and the page says so
// before and after; SMS and push are not offered as if they worked.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Campaign, CampaignAudience, CampaignTestResult } from '@/app/api/campaigns';
import type { Communication } from '@/app/api/communications';
import type { MemberConsent } from '@/app/api/consent';
import type { Member } from '@/app/api/members';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = { success: vi.fn(), error: vi.fn(), warning: vi.fn() };
vi.mock('sonner', () => ({ toast }));

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const MEMBER: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Platinum', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 599, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [MEMBER],
}));

const consent = vi.fn<(memberId: string) => Promise<MemberConsent>>();
vi.mock('@/app/api/consent', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/consent')>()),
  getMemberConsent: (memberId: string) => consent(memberId),
}));

const NO_TOTALS = { recipients: 0, queued: 0, sent: 0, delivered: 0, opened: 0, bounced: 0, suppressed: 0, failed: 0, skipped: 0 };
const DRAFT: Campaign = {
  id: 'cmp-1', name: 'Founders dinner', channel: 'Email', category: 'Event', purpose: 'marketing',
  subject: 'Founders dinner — Platinum only', body: 'Save the date.', segment: { tiers: ['Platinum'] },
  status: 'Draft', createdAtUtc: '2026-10-01T00:00:00Z', recipientCount: 0, version: 'v1',
};
const SCHEDULED: Campaign = { ...DRAFT, id: 'cmp-2', name: 'Autumn gala', status: 'Scheduled', scheduledAtUtc: '2026-12-06T17:00:00Z', version: 'v2' };
const SENT: Campaign = {
  ...DRAFT, id: 'cmp-3', name: 'Members night', status: 'Sent', sentAtUtc: '2026-10-02T09:00:00Z', recipientCount: 3,
  totals: { ...NO_TOTALS, recipients: 3, sent: 2, delivered: 2, opened: 1, suppressed: 1 },
};
const LOGGED: Campaign = { ...DRAFT, id: 'cmp-4', name: 'Rooftop opening', status: 'Logged', sentAtUtc: '2026-09-02T09:00:00Z', recipientCount: 40 };
const TEXTS: Campaign = { ...DRAFT, id: 'cmp-5', name: 'Text blast', channel: 'SMS' };
const AUDIENCE: CampaignAudience = { purpose: 'marketing', matching: 5, withAddress: 4, agreed: 2, willReceive: 2 };

const campaignsApi = {
  list: vi.fn<() => Promise<Campaign[]>>(),
  audience: vi.fn<(...args: unknown[]) => Promise<CampaignAudience>>(),
  recipients: vi.fn<(id: string) => Promise<Communication[]>>(),
  create: vi.fn<(input: unknown) => Promise<Campaign>>(),
  send: vi.fn<(id: string, version?: string) => Promise<Campaign>>(),
  test: vi.fn<(id: string) => Promise<CampaignTestResult>>(),
};
vi.mock('@/app/api/campaigns', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/campaigns')>()),
  getCampaigns: () => campaignsApi.list(),
  getCampaignAudience: (...args: unknown[]) => campaignsApi.audience(...args),
  getCampaignRecipients: (id: string) => campaignsApi.recipients(id),
  createCampaign: (input: unknown) => campaignsApi.create(input),
  sendCampaign: (id: string, version?: string) => campaignsApi.send(id, version),
  sendCampaignTest: (id: string) => campaignsApi.test(id),
}));

const DELIVERED: Communication = {
  id: 'com-1', memberId: 'm1', memberName: 'Jean Dupont', email: 'jean@example.ch', channel: 'Email', category: 'Dues',
  purpose: 'service', subject: 'Your dues statement', preview: 'Your statement for October.', status: 'delivered',
  createdAtUtc: new Date().toISOString(), sentAtUtc: new Date().toISOString(),
};
const REFUSED: Communication = {
  ...DELIVERED, id: 'com-2', category: 'Event', purpose: 'marketing', subject: 'Wine evening', status: 'suppressed',
  reason: 'This member has not agreed to marketing by email.', sentAtUtc: null,
};
const LOGGED_TEXT: Communication = {
  ...DELIVERED, id: 'com-3', channel: 'SMS', email: null, purpose: null, subject: 'Court 2 at 6pm', status: 'logged', sentAtUtc: null,
};
const commsApi = {
  list: vi.fn<() => Promise<Communication[]>>(),
  send: vi.fn<(input: unknown) => Promise<Communication>>(),
  log: vi.fn<(input: unknown) => Promise<Communication>>(),
  remove: vi.fn<(id: string) => Promise<boolean>>(),
};
vi.mock('@/app/api/communications', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/communications')>()),
  getCommunications: () => commsApi.list(),
  sendMemberEmail: (input: unknown) => commsApi.send(input),
  logCommunication: (input: unknown) => commsApi.log(input),
  deleteCommunication: (id: string) => commsApi.remove(id),
}));

const Campaigns = (await import('./Campaigns')).default;
const Communications = (await import('./Communications')).default;

const show = (page: React.ReactElement) => render(withQueries(<MemoryRouter>{page}</MemoryRouter>));
const confirm = vi.fn<(message: string) => boolean>();
/** The row a campaign or message is shown in. */
const rowOf = (text: string) => screen.getByText(text).closest('article') as HTMLElement;

/** A week from now, as a datetime-local field holds it (local time). */
function nextWeekLocal(): string {
  const d = new Date(Date.now() + 7 * 86_400_000);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T18:30`;
}

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  toast.warning.mockReset();
  consent.mockReset().mockResolvedValue({ memberId: 'm1', current: [], history: [] });
  campaignsApi.list.mockReset().mockResolvedValue([DRAFT, SCHEDULED, SENT, LOGGED, TEXTS]);
  campaignsApi.audience.mockReset().mockResolvedValue(AUDIENCE);
  campaignsApi.recipients.mockReset().mockResolvedValue([
    { ...DELIVERED, campaignId: 'cmp-3', subject: SENT.subject, status: 'opened' },
    { ...REFUSED, id: 'com-5', memberId: 'm2', memberName: 'Ana Weber', email: 'ana@example.ch', campaignId: 'cmp-3' },
  ]);
  campaignsApi.create.mockReset().mockImplementation(async (input) => ({ ...DRAFT, ...(input as Partial<Campaign>), id: 'cmp-9' }));
  // By the time the answer comes back the outbox has already handed one of the two to the mail server.
  campaignsApi.send.mockReset().mockResolvedValue({ ...DRAFT, status: 'Sent', totals: { ...NO_TOTALS, recipients: 5, queued: 1, sent: 1, suppressed: 3 } });
  campaignsApi.test.mockReset().mockResolvedValue({ messageId: 'msg-1', to: 'me@club.ch', status: 'sent' });
  commsApi.list.mockReset().mockResolvedValue([DELIVERED, REFUSED, LOGGED_TEXT]);
  commsApi.send.mockReset().mockResolvedValue({ ...DELIVERED, id: 'com-8', status: 'queued', sentAtUtc: null });
  commsApi.log.mockReset().mockResolvedValue({ ...LOGGED_TEXT, id: 'com-7' });
  commsApi.remove.mockReset().mockResolvedValue(true);
  confirm.mockReset().mockReturnValue(true);
  vi.stubGlobal('confirm', confirm);
});
afterEach(() => vi.unstubAllGlobals());

describe('campaigns', () => {
  it('says where each campaign stands, and that marketing goes only to members who agreed', async () => {
    show(<Campaigns />);

    expect(await screen.findByText('Members night')).toBeInTheDocument();
    expect(screen.getByRole('note')).toHaveTextContent(/only to members who agreed/);
    expect(within(rowOf('Members night')).getByText(/3 members · 2 sent · 2 delivered · 1 opened · 1 not sent/)).toBeInTheDocument();
    expect(within(rowOf('Rooftop opening')).getByText('Logged, not delivered')).toBeInTheDocument();
    expect(within(rowOf('Rooftop opening')).getByText(/nobody received it/)).toBeInTheDocument();
    expect(within(rowOf('Autumn gala')).getByText(/^Sends /)).toBeInTheDocument();
    expect(within(rowOf('Text blast')).getByText(/SMS is not available/)).toBeInTheDocument();
  });

  it('sends a campaign once someone has seen who it reaches and who it does not', async () => {
    const user = userEvent.setup();
    show(<Campaigns />);

    await screen.findByText('Founders dinner');
    await user.click(within(rowOf('Founders dinner')).getByRole('button', { name: /Send now/ }));

    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText(/It goes to/)).toHaveTextContent('It goes to 2 addresses.');
    expect(within(dialog).getByText(/3 of the 5 members/)).toHaveTextContent(/no consent to marketing by email/);
    expect(campaignsApi.audience).toHaveBeenCalledWith({ tiers: ['Platinum'] }, 'marketing', expect.anything());
    expect(campaignsApi.send).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: 'Send now' }));

    await waitFor(() => expect(campaignsApi.send).toHaveBeenCalledWith('cmp-1', 'v1'));
    expect(campaignsApi.send).toHaveBeenCalledTimes(1);
    expect(toast.success).toHaveBeenCalledWith('"Founders dinner" is on its way: 2 emails sent or queued.');
  });

  it('sends a test to the person who asked for it, and to no member', async () => {
    const user = userEvent.setup();
    show(<Campaigns />);

    await screen.findByText('Founders dinner');
    await user.click(within(rowOf('Founders dinner')).getByRole('button', { name: /Send a test to me/ }));

    await waitFor(() => expect(campaignsApi.test).toHaveBeenCalledWith('cmp-1'));
    expect(toast.success).toHaveBeenCalledWith('Test sent to me@club.ch. Members have not received it.');
    expect(campaignsApi.send).not.toHaveBeenCalled();
  });

  it('says a test the mail server did not take at once is queued, not sent', async () => {
    campaignsApi.test.mockResolvedValue({ messageId: 'msg-2', to: 'me@club.ch', status: 'queued' });
    const user = userEvent.setup();
    show(<Campaigns />);

    await screen.findByText('Founders dinner');
    await user.click(within(rowOf('Founders dinner')).getByRole('button', { name: /Send a test to me/ }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/^Test queued for me@club\.ch: .*tried again shortly/)));
    expect(toast.success).not.toHaveBeenCalledWith(expect.stringMatching(/^Test sent/));
  });

  it('offers nothing that would change a campaign that has gone, and shows what came of each message', async () => {
    const user = userEvent.setup();
    show(<Campaigns />);

    await screen.findByText('Members night');
    expect(within(rowOf('Founders dinner')).getByRole('button', { name: /Delete Founders dinner/ })).toBeInTheDocument();
    for (const name of ['Members night', 'Rooftop opening']) {
      const row = rowOf(name);
      expect(within(row).queryByRole('button', { name: /Send now/ })).toBeNull();
      expect(within(row).queryByRole('button', { name: /Edit/ })).toBeNull();
      expect(within(row).queryByRole('button', { name: /Delete/ })).toBeNull();
    }
    // Nothing would send a text: the campaign is said to be unavailable, not offered.
    expect(within(rowOf('Text blast')).queryByRole('button', { name: /Send/ })).toBeNull();

    await user.click(within(rowOf('Members night')).getByRole('button', { name: /Recipients/ }));

    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText('Ana Weber')).toBeInTheDocument();
    expect(within(dialog).getByText('Opened')).toBeInTheDocument();
    expect(within(dialog).getByText('Not sent')).toBeInTheDocument();
    expect(within(dialog).getByText('This member has not agreed to marketing by email.')).toBeInTheDocument();
    expect(campaignsApi.recipients).toHaveBeenCalledWith('cmp-3');
  });

  it('schedules a campaign for a time it will be sent, says whom it would reach, and offers email only', async () => {
    const user = userEvent.setup();
    show(<Campaigns />);

    await user.click(await screen.findByRole('button', { name: /New campaign/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/SMS and push are not available/)).toBeInTheDocument();
    await waitFor(() => expect(within(dialog).getByText(/It would go to/)).toHaveTextContent(/It would go to 2 addresses/));

    await user.type(within(dialog).getByLabelText('Name'), 'Winter ball');
    await user.type(within(dialog).getByLabelText('Subject'), 'The winter ball');
    await user.type(within(dialog).getByLabelText('Message'), 'Join us in the ballroom.');
    const at = nextWeekLocal();
    fireEvent.change(within(dialog).getByLabelText('Send at'), { target: { value: at } });
    await user.click(within(dialog).getByRole('button', { name: 'Schedule' }));

    await waitFor(() => expect(campaignsApi.create).toHaveBeenCalled());
    expect(campaignsApi.create.mock.calls[0]![0]).toMatchObject({
      name: 'Winter ball', purpose: 'marketing', subject: 'The winter ball', body: 'Join us in the ballroom.',
      segment: { statuses: ['Active'] }, scheduledAtUtc: new Date(at).toISOString(),
    });
  });

  it('does not take a send time in the past for one that would never go', async () => {
    const user = userEvent.setup();
    show(<Campaigns />);

    await user.click(await screen.findByRole('button', { name: /New campaign/ }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Name'), 'Late');
    await user.type(within(dialog).getByLabelText('Subject'), 'Late');
    await user.type(within(dialog).getByLabelText('Message'), 'Too late.');
    fireEvent.change(within(dialog).getByLabelText('Send at'), { target: { value: '2020-01-01T09:00' } });
    await user.click(within(dialog).getByRole('button', { name: 'Schedule' }));

    expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/time in the future/));
    expect(campaignsApi.create).not.toHaveBeenCalled();
  });

  it('a list that could not be read says so, rather than showing no campaigns, and tries again', async () => {
    campaignsApi.list.mockRejectedValueOnce(new Error('The server is down for maintenance.'));
    const user = userEvent.setup();
    show(<Campaigns />);

    expect(await screen.findByText('Campaigns could not be loaded.')).toBeInTheDocument();
    expect(screen.queryByText('No campaigns yet')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Members night')).toBeInTheDocument();
  });
});

describe('member messages', () => {
  it('shows what came of each email, why one was not sent, and that a logged text was not sent by CadenceE', async () => {
    show(<Communications />);

    expect(await screen.findByText('Your dues statement')).toBeInTheDocument();
    expect(within(rowOf('Your dues statement')).getByText('Delivered')).toBeInTheDocument();
    expect(within(rowOf('Wine evening')).getByText('Not sent')).toBeInTheDocument();
    expect(within(rowOf('Wine evening')).getByText('This member has not agreed to marketing by email.')).toBeInTheDocument();
    expect(within(rowOf('Court 2 at 6pm')).getByText('Logged, not sent by CadenceE')).toBeInTheDocument();
    expect(screen.getByRole('note')).toHaveTextContent(/cannot send SMS or push/);

    // What CadenceE sent, or refused to, is the record of what went to whom: only a logged one can go.
    expect(within(rowOf('Court 2 at 6pm')).getByRole('button', { name: /Delete logged message/ })).toBeInTheDocument();
    expect(within(rowOf('Your dues statement')).queryByRole('button', { name: /Delete/ })).toBeNull();
    expect(within(rowOf('Wine evening')).queryByRole('button', { name: /Delete/ })).toBeNull();
  });

  it('emails a member about their dues as a service message', async () => {
    const user = userEvent.setup();
    show(<Communications />);

    await user.click(await screen.findByRole('button', { name: /Email a member/ }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByLabelText('To'));
    await user.click(await screen.findByRole('option', { name: /Jean Dupont/ }));
    await user.click(within(dialog).getByLabelText('Category'));
    await user.click(await screen.findByRole('option', { name: 'Dues' }));
    expect(within(dialog).getByLabelText('What is it?')).toHaveTextContent(/Service/);
    await user.type(within(dialog).getByLabelText('Subject'), 'Your October statement');
    await user.type(within(dialog).getByLabelText('Message'), 'Your statement is attached.');
    await user.click(within(dialog).getByRole('button', { name: 'Send email' }));

    await waitFor(() => expect(commsApi.send).toHaveBeenCalledWith({
      memberId: 'm1', category: 'Dues', purpose: 'service', subject: 'Your October statement', body: 'Your statement is attached.',
    }));
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/^Queued for Jean/));
  });

  it('says before and after that marketing does not go to a member who has not agreed', async () => {
    commsApi.send.mockResolvedValue({ ...REFUSED, id: 'com-9' });
    const user = userEvent.setup();
    show(<Communications />);

    await user.click(await screen.findByRole('button', { name: /Email a member/ }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByLabelText('To'));
    await user.click(await screen.findByRole('option', { name: /Jean Dupont/ }));
    expect(within(dialog).getByLabelText('What is it?')).toHaveTextContent(/Marketing/);
    expect(await within(dialog).findByText(/has not agreed to marketing by email/)).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Subject'), 'Wine evening');
    await user.type(within(dialog).getByLabelText('Message'), 'Join us on Friday.');
    await user.click(within(dialog).getByRole('button', { name: 'Send email' }));

    await waitFor(() => expect(commsApi.send).toHaveBeenCalled());
    expect(toast.warning).toHaveBeenCalledWith('Not sent to Jean. This member has not agreed to marketing by email.');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('logs a text sent from a phone, and says CadenceE did not send it', async () => {
    const user = userEvent.setup();
    show(<Communications />);

    await user.click(await screen.findByRole('button', { name: /Log a message/ }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/CadenceE cannot send SMS/);
    await user.click(within(dialog).getByLabelText('To'));
    await user.click(await screen.findByRole('option', { name: /Jean Dupont/ }));
    await user.type(within(dialog).getByLabelText('What it was about'), 'Court booking moved');
    await user.click(within(dialog).getByRole('button', { name: 'Log message' }));

    await waitFor(() => expect(commsApi.log).toHaveBeenCalledWith(expect.objectContaining({
      memberId: 'm1', channel: 'SMS', subject: 'Court booking moved',
    })));
    expect(toast.success).toHaveBeenCalledWith('Logged. CadenceE did not send it.');
  });
});
