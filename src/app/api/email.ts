// Outbound email.
//
// `sendEmail` is the single integration point for email written in the CRM.
// Against the real backend it POSTs to `/api/email/send`, which sends it through
// the configured relay (SendGrid in production) from the workspace's address. In
// demo mode it simulates a successful send.
//
// A live send never pretends. It used to fall back to the demo store when the
// server had no such endpoint: the composer said "Email sent", logged it on the
// timeline, and nothing left the building. Now a send the server did not make
// fails with the server's reason, and the composer offers the person's own
// email app (`mailtoHref`) instead.

import { ApiError, authFetchJson, servesDemoData } from './apiClient';
import { createMockStore, mockId } from './mockStore';

const SEND_PATH = '/api/email/send';

/** A mailto: link carrying the message, for the person's own email app. */
export function mailtoHref(message: { to: string; cc?: string; subject: string; body: string }): string {
  const list = (value: string) => value.split(/[,;]/).map((a) => a.trim()).filter(Boolean).map(encodeURIComponent).join(',');
  const query = new URLSearchParams();
  if (message.cc?.trim()) query.set('cc', message.cc.trim());
  query.set('subject', message.subject);
  query.set('body', message.body);
  // URLSearchParams writes spaces as "+", which mail apps show literally.
  return `mailto:${list(message.to)}?${query.toString().replace(/\+/g, '%20')}`;
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export interface SendEmailRequest {
  to: string;
  cc?: string;
  bcc?: string;
  subject: string;
  body: string;
  /** Associate the message with a record so it shows on that timeline. */
  leadId?: string;
  contactId?: string;
  dealId?: string;
}

export type EmailStatus = 'sent' | 'queued' | 'failed';

export interface EmailMessage {
  id: string;
  to: string;
  cc?: string;
  bcc?: string;
  subject: string;
  body: string;
  status: EmailStatus;
  direction: 'outbound' | 'inbound';
  sentAtUtc: string;
  leadId?: string;
  contactId?: string;
  dealId?: string;
}

const emailStore = createMockStore<EmailMessage>({
  storageKey: 'crm.mock.emails.v1',
  seed: [],
  idOf: (m) => m.id,
});

export async function sendEmail(req: SendEmailRequest): Promise<EmailMessage | null> {
  if (!servesDemoData()) {
    try {
      return await authFetchJson<EmailMessage>(SEND_PATH, { method: 'POST', body: JSON.stringify(req) });
    } catch (err) {
      // The server's reason comes through as is ("… is not a valid email address",
      // "Sending email is not set up on the server yet"). These two have no body.
      if (err instanceof ApiError && err.status === 404) {
        throw new ApiError('Sending email from the CRM is not set up yet.', 404);
      }
      if (err instanceof ApiError && err.status === 429) {
        throw new ApiError('That is a lot of email in one minute. Wait a moment, then send again.', 429);
      }
      throw err;
    }
  }
  // Demo mode: no provider — record locally so the timeline still works.
  await delay(450);
  return emailStore.add({
    id: mockId('eml'),
    to: req.to,
    cc: req.cc,
    bcc: req.bcc,
    subject: req.subject,
    body: req.body,
    status: 'sent',
    direction: 'outbound',
    sentAtUtc: new Date().toISOString(),
    leadId: req.leadId,
    contactId: req.contactId,
    dealId: req.dealId,
  });
}
