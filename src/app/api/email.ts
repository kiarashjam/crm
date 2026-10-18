// Outbound email.
//
// `sendEmail` is the single integration point for transactional email. Against
// the real backend it POSTs to `/api/email/send`, which is where a provider
// (SendGrid, etc.) would deliver the message. In demo mode it simulates a
// successful send and records it in a local "sent" store so the email history
// on a record still works offline.
//
// The backend has no such endpoint yet. Sending used to fall back to the demo
// store on the 404 even against the live API: the composer said "Email sent",
// logged it on the timeline, and nothing left the building. Now a live send
// that the server cannot take fails, and `canSendEmail()` lets the composer
// offer the person's own email app instead.

import { ApiError, apiWithFallback, authFetchJson, isUsingRealApi } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { isNotOnServer } from './notOnServer';

const SEND_PATH = '/api/email/send';

/** Whether "Send" can really send: in demo mode (simulated), or when the server has the endpoint. */
export function canSendEmail(): boolean {
  return !isUsingRealApi() || !isNotOnServer(SEND_PATH);
}

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
  if (isUsingRealApi()) {
    try {
      return await authFetchJson<EmailMessage>(SEND_PATH, { method: 'POST', body: JSON.stringify(req) });
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        throw new ApiError('Sending email from the CRM is not set up yet.', 404);
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

async function getEmails(params: { leadId?: string; contactId?: string; dealId?: string }): Promise<EmailMessage[]> {
  return apiWithFallback(
    async () => {
      const q = new URLSearchParams();
      if (params.leadId) q.set('leadId', params.leadId);
      if (params.contactId) q.set('contactId', params.contactId);
      if (params.dealId) q.set('dealId', params.dealId);
      const res = await authFetchJson<EmailMessage[]>(`/api/email?${q.toString()}`);
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return emailStore
        .list()
        .filter(
          (m) =>
            (!params.leadId || m.leadId === params.leadId) &&
            (!params.contactId || m.contactId === params.contactId) &&
            (!params.dealId || m.dealId === params.dealId),
        )
        .sort((a, b) => Date.parse(b.sentAtUtc) - Date.parse(a.sentAtUtc));
    },
  );
}

export const getEmailsByLead = (leadId: string) => getEmails({ leadId });
export const getEmailsByContact = (contactId: string) => getEmails({ contactId });
export const getEmailsByDeal = (dealId: string) => getEmails({ dealId });
