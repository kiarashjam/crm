// A member's consent, as staff see and record it on the member's page.
//
// Marketing goes only where the member said yes, so the panel has to say plainly where
// each channel stands ("Not asked" is a no), how and when each answer was given, and
// keep the answers it replaces. Service messages go whatever is recorded, and the panel
// says so rather than offering a switch that would not stop them.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ConsentInput, MemberConsent } from '@/app/api/consent';
import { withQueries } from '@/test/queryWrapper';

const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const api = {
  get: vi.fn<(memberId: string) => Promise<MemberConsent>>(),
  record: vi.fn<(memberId: string, input: ConsentInput) => Promise<MemberConsent>>(),
};
vi.mock('@/app/api/consent', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/consent')>()),
  getMemberConsent: (memberId: string) => api.get(memberId),
  recordMemberConsent: (memberId: string, input: ConsentInput) => api.record(memberId, input),
}));

const MemberConsentPanel = (await import('./MemberConsentPanel')).default;

const WITHDRAWN_BY_LINK: MemberConsent = {
  memberId: 'm1',
  current: [
    { channel: 'email', purpose: 'marketing', state: 'withdrawn', source: 'unsubscribe', recordedAtUtc: '2026-10-05T10:00:00Z' },
  ],
  history: [
    { id: 'c2', channel: 'email', purpose: 'marketing', granted: false, source: 'unsubscribe', address: 'jean@example.ch', recordedAtUtc: '2026-10-05T10:00:00Z', isCurrent: true },
    { id: 'c1', channel: 'email', purpose: 'marketing', granted: true, source: 'form', detail: 'Ticked the box on the membership form', recordedAtUtc: '2025-03-01T10:00:00Z', recordedByName: 'Claire', isCurrent: false },
  ],
};

const show = () => render(withQueries(<MemberConsentPanel memberId="m1" />));

beforeEach(() => {
  toast.success.mockReset();
  toast.error.mockReset();
  api.get.mockReset().mockResolvedValue(WITHDRAWN_BY_LINK);
  api.record.mockReset();
});

describe('member consent', () => {
  it('says where each channel stands, how it was given, and keeps the answer it replaced', async () => {
    show();

    const emailMarketing = await screen.findByTestId('consent-email-marketing');
    expect(emailMarketing).toHaveTextContent('Said no');
    expect(emailMarketing).toHaveTextContent('Unsubscribe link');
    expect(screen.getByTestId('consent-sms-marketing')).toHaveTextContent('Not asked');
    expect(screen.getByTestId('consent-email-service')).toHaveTextContent('Sent regardless');

    const history = screen.getByRole('region', { name: 'History' });
    expect(within(history).getByText(/Said no to marketing by Email/)).toBeInTheDocument();
    expect(within(history).getByText(/Agreed to marketing by Email/)).toHaveTextContent('(replaced since)');
    expect(within(history).getByText('Ticked the box on the membership form')).toBeInTheDocument();
  });

  it('records a new answer with how it was given, and shows it at once', async () => {
    api.record.mockResolvedValue({
      memberId: 'm1',
      current: [{ channel: 'email', purpose: 'marketing', state: 'granted', source: 'member', recordedAtUtc: new Date().toISOString(), recordedByName: 'Claire' }],
      history: [
        { id: 'c3', channel: 'email', purpose: 'marketing', granted: true, source: 'member', detail: 'Asked at the desk to hear about events', recordedAtUtc: new Date().toISOString(), recordedByName: 'Claire', isCurrent: true },
        ...WITHDRAWN_BY_LINK.history.map((h) => ({ ...h, isCurrent: false })),
      ],
    });
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: 'Change marketing by Email' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Agreed' }));
    await user.type(within(dialog).getByLabelText('Note'), 'Asked at the desk to hear about events');
    await user.click(within(dialog).getByRole('button', { name: 'Record' }));

    await waitFor(() => expect(api.record).toHaveBeenCalledWith('m1', {
      channel: 'email', purpose: 'marketing', granted: true, source: 'member', detail: 'Asked at the desk to hear about events',
    }));
    await waitFor(() => expect(screen.getByTestId('consent-email-marketing')).toHaveTextContent('Agreed'));
    expect(toast.success).toHaveBeenCalledWith('Consent recorded');
  });

  it('a read that failed says so, rather than showing every channel as not asked', async () => {
    api.get.mockRejectedValue(new Error('The server could not be reached.'));
    show();

    expect(await screen.findByText("This member's consent could not be loaded.")).toBeInTheDocument();
    expect(screen.queryByText('Not asked')).toBeNull();
  });
});
