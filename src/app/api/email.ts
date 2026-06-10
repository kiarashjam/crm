// Outbound email.
//
// `sendEmail` is the single integration point for transactional email. Against
// the real backend it POSTs to `/api/email/send`, which is where a provider
// (SendGrid, etc.) actually delivers the message. In demo mode it simulates a
// successful send and records it in a local "sent" store so the email history
// on a record still works offline.

import { apiWithFallback, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

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
  return apiWithFallback(
    () => authFetchJson<EmailMessage>('/api/email/send', { method: 'POST', body: JSON.stringify(req) }),
    async () => {
      // No provider endpoint — record locally so the timeline still works.
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
    },
  );
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
