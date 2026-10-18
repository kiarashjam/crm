// Sending email: never report a send that did not happen.
import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.resetModules();
  localStorage.clear();
});

const message = { to: 'jean@example.ch', subject: 'Hello', body: 'Hi', leadId: 'l1' };

async function live(response: Response) {
  vi.stubEnv('VITE_API_URL', 'https://api.example.test');
  const fetch = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetch);
  const { sendEmail } = await import('./email');
  return { sendEmail, fetch };
}

describe('sendEmail against the live API', () => {
  it('posts the message to the server, which sends it', async () => {
    const sent = { id: 'e1', to: 'jean@example.ch', subject: 'Hello', body: 'Hi', status: 'sent', direction: 'outbound', sentAtUtc: '2026-10-18T08:00:00Z' };
    const { sendEmail, fetch } = await live(new Response(JSON.stringify(sent), { status: 200 }));

    await expect(sendEmail(message)).resolves.toMatchObject({ status: 'sent' });
    const [url, init] = fetch.mock.calls[0]!;
    expect(url).toBe('https://api.example.test/api/email/send');
    expect(JSON.parse(init.body)).toMatchObject({ to: 'jean@example.ch', subject: 'Hello' });
  });

  it('fails with the server reason, instead of recording a send that never happened', async () => {
    // This used to fall back to the demo store: "Email sent" in a toast and on
    // the timeline, and nothing sent.
    const problem = { title: 'Service Unavailable', status: 503, detail: 'Sending email is not set up on the server yet.' };
    const { sendEmail } = await live(new Response(JSON.stringify(problem), { status: 503 }));

    await expect(sendEmail(message)).rejects.toThrow('Sending email is not set up on the server yet.');
    expect(localStorage.getItem('crm.mock.emails.v1') ?? '[]').not.toContain('Hello');
  });

  it('explains the per-minute limit, which comes without a body', async () => {
    const { sendEmail } = await live(new Response(null, { status: 429, statusText: 'Too Many Requests' }));

    await expect(sendEmail(message)).rejects.toThrow(/Wait a moment/);
  });
});

describe('sendEmail in demo mode', () => {
  it('still simulates the send', async () => {
    vi.stubEnv('VITE_API_URL', '');
    const { sendEmail } = await import('./email');
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
