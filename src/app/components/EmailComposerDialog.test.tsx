// The email composer: "Email sent" only when it was.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const api = { sendEmail: vi.fn(), createActivity: vi.fn(), generateCopy: vi.fn() };
vi.mock('@/app/api', async () => {
  const { mailtoHref } = await vi.importActual<typeof import('@/app/api/email')>('@/app/api/email');
  return {
    sendEmail: (...a: unknown[]) => api.sendEmail(...a),
    createActivity: (...a: unknown[]) => api.createActivity(...a),
    generateCopy: (...a: unknown[]) => api.generateCopy(...a),
    isUsingRealApi: () => true,
    servesDemoData: () => false,
    mailtoHref,
  };
});
const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const EmailComposerDialog = (await import('./EmailComposerDialog')).default;
const { ApiError } = await import('@/app/api/apiClient');

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(toast)]) fn.mockReset();
  api.createActivity.mockResolvedValue({});
});

async function send() {
  const onOpenChange = vi.fn();
  render(<EmailComposerDialog open onOpenChange={onOpenChange} to="jean@example.ch" defaultSubject="Votre visite" defaultBody="Bonjour" context={{ leadId: 'l1' }} />);
  await userEvent.setup().click(screen.getByRole('button', { name: 'Send' }));
  return onOpenChange;
}

describe('EmailComposerDialog', () => {
  it('says sent, and logs it, when the server sent it', async () => {
    api.sendEmail.mockResolvedValue({ id: 'e1', status: 'sent' });
    const onOpenChange = await send();

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Email sent'));
    expect(api.createActivity).toHaveBeenCalledWith(expect.objectContaining({ type: 'email', leadId: 'l1' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('when the send fails: the reason, an email-app way out, no "sent" and no timeline entry', async () => {
    api.sendEmail.mockRejectedValue(new Error('Sending email is not set up on the server yet.'));
    const onOpenChange = await send();

    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    const [text, options] = toast.error.mock.calls[0]!;
    expect(text).toBe('Sending email is not set up on the server yet.');
    expect(options.action.label).toBe('Open in email app');
    expect(toast.success).not.toHaveBeenCalled();
    expect(api.createActivity).not.toHaveBeenCalled();
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });

  it('when the person said stop: the reason, and no email-app way round it', async () => {
    // The server refuses a do-not-contact recipient. Offering "Open in email app"
    // then sent the same message from the rep's own mailbox and noted it on the
    // timeline as if nothing had been refused.
    api.sendEmail.mockRejectedValue(new ApiError(
      'jean@example.ch is marked do not contact, so nothing was sent.', 400, '/api/email/send', true, 'Contact.DoNotContact'));
    const onOpenChange = await send();

    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    const [text, options] = toast.error.mock.calls[0]!;
    expect(text).toBe('jean@example.ch is marked do not contact, so nothing was sent.');
    expect(options?.action).toBeUndefined();
    expect(api.createActivity).not.toHaveBeenCalled();
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });
});
