// Campaigns and member messages, until Cadence can deliver them.
//
// "Send message" and a campaign's "Send now" only ever wrote log entries stamped
// "Sent": no email, SMS or push left the server, and a campaign scheduled for Friday
// stayed "Scheduled" for ever. The pages said "Message sent", "Sent to 40 members"
// and promised open and click tracking, and staff told members "we emailed you" on
// the strength of it. These pin the honest wording that replaced it; the data is
// unchanged.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Campaign } from '@/app/api/campaigns';
import type { Communication } from '@/app/api/communications';
import type { Member } from '@/app/api/members';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const MEMBER: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Platinum', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 599, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [MEMBER],
}));

const DRAFT: Campaign = {
  id: 'cmp-1', name: 'Founders dinner', channel: 'Email', category: 'Event', subject: 'Founders dinner — Platinum only',
  body: 'Save the date.', segment: { tiers: ['Platinum'] }, status: 'Draft', createdAtUtc: '2026-10-01T00:00:00Z',
};
const PLANNED: Campaign = {
  ...DRAFT, id: 'cmp-2', name: 'Autumn gala', status: 'Scheduled', scheduledAtUtc: '2026-11-06T17:00:00Z',
};
const LOGGED: Campaign = {
  ...DRAFT, id: 'cmp-3', name: 'Rooftop opening', status: 'Sent', sentAtUtc: '2026-10-02T09:00:00Z', recipientCount: 40,
};
const campaigns = vi.fn<() => Promise<Campaign[]>>();
const sendCampaign = vi.fn<(id: string) => Promise<Campaign | null>>();
const createCampaign = vi.fn();
vi.mock('@/app/api/campaigns', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/campaigns')>()),
  getCampaigns: () => campaigns(),
  sendCampaign: (id: string) => sendCampaign(id),
  createCampaign: (input: unknown) => createCampaign(input),
  previewSegment: async () => [MEMBER],
}));

const MESSAGE: Communication = {
  id: 'com-1', memberId: 'm1', memberName: 'Jean Dupont', channel: 'Email', category: 'Dues',
  subject: 'We were unable to process your dues', preview: 'Your card declined.', status: 'Sent',
  sentAtUtc: new Date().toISOString(),
};
const sendCommunication = vi.fn();
vi.mock('@/app/api/communications', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/communications')>()),
  getCommunications: async () => [MESSAGE],
  sendCommunication: (input: unknown) => sendCommunication(input),
}));

const Campaigns = (await import('./Campaigns')).default;
const Communications = (await import('./Communications')).default;

const show = (page: React.ReactNode) => render(<MemoryRouter>{page}</MemoryRouter>);
const confirm = vi.fn<(message: string) => boolean>();

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  campaigns.mockReset().mockResolvedValue([DRAFT, PLANNED, LOGGED]);
  sendCampaign.mockReset().mockResolvedValue({ ...DRAFT, status: 'Sent', recipientCount: 1, sentAtUtc: new Date().toISOString() });
  createCampaign.mockReset().mockResolvedValue(DRAFT);
  sendCommunication.mockReset().mockResolvedValue(MESSAGE);
  confirm.mockReset().mockReturnValue(true);
  vi.stubGlobal('confirm', confirm);
});
afterEach(() => vi.unstubAllGlobals());

describe('campaigns', () => {
  it('says a campaign is logged, not delivered, and that a planned one will not send itself', async () => {
    show(<Campaigns />);

    expect(await screen.findByText('Rooftop opening')).toBeInTheDocument();
    expect(screen.getByRole('note')).toHaveTextContent(/does not deliver/);
    expect(screen.getAllByText('Logged, not delivered').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Planned, will not send itself').length).toBeGreaterThan(0);
    expect(screen.queryByText(/^Sent$/)).toBeNull();
    expect(screen.queryByText(/^Scheduled$/)).toBeNull();
    expect(screen.queryByRole('button', { name: /Send now/ })).toBeNull();
  });

  it('logs a campaign as sent only after saying nobody will receive it', async () => {
    const user = userEvent.setup();
    show(<Campaigns />);

    await user.click((await screen.findAllByRole('button', { name: /Log as sent/ }))[0]!);

    expect(confirm).toHaveBeenCalledWith(expect.stringMatching(/nobody receives it/));
    await waitFor(() => expect(sendCampaign).toHaveBeenCalledWith('cmp-1'));
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/Nothing was delivered/));
    expect(toast.success).not.toHaveBeenCalledWith(expect.stringMatching(/^Sent to/));
  });

  it('does not offer a send time that nothing would act on', async () => {
    const user = userEvent.setup();
    show(<Campaigns />);

    await user.click(await screen.findByRole('button', { name: /New campaign/ }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByLabelText(/Schedule/)).toBeNull();
    expect(within(dialog).queryByText(/will reach/)).toBeNull();
  });
});

describe('member messages', () => {
  it('is a log, not a sender: no tracking claims, and "Sent" reads as not delivered', async () => {
    show(<Communications />);

    expect(await screen.findByText(MESSAGE.subject)).toBeInTheDocument();
    expect(screen.getByRole('note')).toHaveTextContent(/does not deliver/);
    expect(screen.getAllByText('Logged, not delivered').length).toBeGreaterThan(0);
    expect(screen.queryByText(/open and click tracking/)).toBeNull();
    expect(screen.queryByText(/Open rate/)).toBeNull();
    expect(screen.queryByRole('button', { name: /Send message/ })).toBeNull();
  });

  it('logs a message and says it was not delivered', async () => {
    const user = userEvent.setup();
    show(<Communications />);

    await user.click(await screen.findByRole('button', { name: /Log a message/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByText(/delivery events/)).toBeNull();
    await user.type(within(dialog).getByLabelText('Subject / message'), 'Dues reminder');
    await user.click(within(dialog).getByRole('button', { name: 'Log message' }));

    await waitFor(() => expect(sendCommunication).toHaveBeenCalled());
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/did not deliver/));
    expect(toast.success).not.toHaveBeenCalledWith('Message sent');
  });
});
