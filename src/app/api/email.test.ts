// Sending email: never report a send that did not happen.
import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.resetModules();
  localStorage.clear();
});

describe('sendEmail against the live API', () => {
  it('fails, instead of recording a send that never happened', async () => {
    // The server has no send endpoint. This used to fall back to the demo store:
    // "Email sent" in a toast and on the timeline, and nothing sent.
    vi.stubEnv('VITE_API_URL', 'https://api.example.test');
    const fetch = vi.fn();
    vi.stubGlobal('fetch', fetch);
    const { sendEmail, canSendEmail, getEmailsByLead } = await import('./email');

    expect(canSendEmail()).toBe(false);
    await expect(sendEmail({ to: 'jean@example.ch', subject: 'Hello', body: 'Hi', leadId: 'l1' }))
      .rejects.toThrow('Sending email from the CRM is not set up yet.');
    expect(fetch).not.toHaveBeenCalled();
    expect(await getEmailsByLead('l1')).toEqual([]);
  });
});

describe('sendEmail in demo mode', () => {
  it('still simulates the send', async () => {
    vi.stubEnv('VITE_API_URL', '');
    const { sendEmail, canSendEmail } = await import('./email');
    expect(canSendEmail()).toBe(true);
    const sent = await sendEmail({ to: 'jean@example.ch', subject: 'Hello', body: 'Hi' });
    expect(sent?.status).toBe('sent');
  });
});

describe('mailtoHref', () => {
  it('carries recipients, subject and body, with spaces a mail app shows as spaces', async () => {
    const { mailtoHref } = await import('./email');
    const href = mailtoHref({ to: 'jean@example.ch, anne@example.ch', cc: 'boss@club.ch', subject: 'Votre visite', body: 'Bonjour Jean,\nÀ bientôt' });
    expect(href.startsWith('mailto:jean%40example.ch,anne%40example.ch?')).toBe(true);
    const query = new URLSearchParams(href.split('?')[1]);
    expect(query.get('cc')).toBe('boss@club.ch');
    expect(query.get('subject')).toBe('Votre visite');
    expect(query.get('body')).toBe('Bonjour Jean,\nÀ bientôt');
    expect(href).not.toContain('+');
  });
});
