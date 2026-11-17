// Leads › Invite to a meeting: a newsletter inviting the leads chosen to book a meeting, each
// with a link and a code of their own. The server counts who it goes to, renders the email
// and sends it; the dialog chooses, shows and confirms. Only someone who may send it sees it.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import type { Organization } from '@/app/api/organizations';
import type {
  NewsletterAudience, NewsletterAudienceRequest, NewsletterPreview, NewsletterSendRequest, NewsletterSetup,
} from '@/app/api/leadNewsletter';

const state = vi.hoisted(() => ({ org: null as Organization | null, readOnly: false }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: state.org, currentOrgId: state.org?.id ?? null, isReadOnly: state.readOnly }),
}));

const { LeadNewsletterButton } = await import('./LeadNewsletterButton');

const BASE = 'http://api.test';
const API = `${BASE}/api/leads/newsletter`;
const STATUSES = ['New', 'Contacted', 'Connected', 'Lost / Not Interested'];
const LEA = '11111111-1111-4111-8111-111111111111';
const MARC = '22222222-2222-4222-8222-222222222222';

const owner = (over: Partial<Organization> = {}): Organization => ({
  id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: true, role: 0, ...over,
});

function setup(over: Partial<NewsletterSetup> = {}): NewsletterSetup {
  return {
    bookingLinkAvailable: true, bookingProblem: null, bookingPageUrl: 'https://pavillon46.ch/rendez-vous',
    fields: ['firstName', 'lastName', 'name', 'bookingUrl', 'bookingCode', 'workspace'],
    workspace: 'Pavillon 46', defaultLanguage: 'fr', fromName: 'Pavillon 46', replyTo: 'hello@pavillon46.ch', maxRecipients: 2000,
    ...over,
  };
}

function audience(over: Partial<NewsletterAudience> = {}): NewsletterAudience {
  return {
    matching: 27, willReceive: 23, noEmail: 2, doNotContact: 1, suppressed: 1, duplicates: 0,
    recipients: [
      { leadId: LEA, name: 'Léa Martin', email: 'lea@example.ch', status: 'New' },
      { leadId: MARC, name: 'Marc Favre', email: 'marc@example.ch', status: 'Contacted' },
    ],
    leftOut: [
      { leadId: 'e', name: 'Eva Roth', email: null, status: 'New', reason: 'no_email' },
      { leadId: 'b', name: 'Bruno Keller', email: 'bruno@example.ch', status: 'Contacted', reason: 'do_not_contact' },
      { leadId: 'n', name: 'Noémie Blanc', email: 'noemie@example.ch', status: 'New', reason: 'suppressed' },
    ],
    maxRecipients: 2000,
    ...over,
  };
}

function preview(over: Partial<NewsletterPreview> = {}): NewsletterPreview {
  return {
    subject: 'Léa, prenons rendez-vous', html: '<!DOCTYPE html><html><body><p>Bonjour Léa,</p><a href="https://pavillon46.ch/rendez-vous?invite=x.y">Réserver</a></body></html>',
    text: 'Bonjour Léa,', sampleName: 'Léa Martin', sampleEmail: 'lea@example.ch', bookingCode: 'RDV-7K2Q', unknown: [],
    bookingProblem: null, fromName: 'Pavillon 46', replyTo: 'hello@pavillon46.ch',
    ...over,
  };
}

/** What the dialog asked the server, in order. */
const asked = vi.hoisted(() => ({
  audience: [] as NewsletterAudienceRequest[],
  preview: [] as Record<string, unknown>[],
  test: [] as Record<string, unknown>[],
  send: [] as NewsletterSendRequest[],
}));

/** The server: the count follows the statuses chosen, everything else answers as given. */
function serve({ setupOver = {}, send }: { setupOver?: Partial<NewsletterSetup>; send?: () => Response } = {}) {
  server.use(
    http.get(`${API}/setup`, () => HttpResponse.json(setup(setupOver))),
    http.post(`${API}/audience`, async ({ request }) => {
      const body = (await request.json()) as NewsletterAudienceRequest;
      asked.audience.push(body);
      const without = (s: string) => body.excludeStatuses?.includes(s);
      return HttpResponse.json(without('Contacted') ? audience({ matching: 14, willReceive: 12, recipients: audience().recipients.slice(0, 1) }) : audience());
    }),
    http.post(`${API}/preview`, async ({ request }) => {
      asked.preview.push((await request.json()) as Record<string, unknown>);
      return HttpResponse.json(preview());
    }),
    http.post(`${API}/test`, async ({ request }) => {
      asked.test.push((await request.json()) as Record<string, unknown>);
      return HttpResponse.json({ to: 'owner@pavillon46.ch', status: 'sent' });
    }),
    http.post(`${API}/send`, async ({ request }) => {
      asked.send.push((await request.json()) as NewsletterSendRequest);
      return send?.() ?? HttpResponse.json({
        id: asked.send[0]?.sendId, status: 'sent', recipients: 23, leftOut: 4, alreadySent: false, createdAtUtc: '2026-11-17T10:00:00Z',
      });
    }),
  );
}

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'session');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-owner', name: 'Anna Keller', email: 'owner@pavillon46.ch' }));
  state.org = owner();
  state.readOnly = false;
  asked.audience = [];
  asked.preview = [];
  asked.test = [];
  asked.send = [];
});

function renderButton() {
  return render(
    <MemoryRouter>
      <LeadNewsletterButton
        statuses={STATUSES}
        sources={['Website', 'Referral']}
        members={[{ userId: 'u-owner', name: 'Anna Keller', email: 'owner@pavillon46.ch' }, { userId: 'u-ben', name: 'Ben Roth' }]}
      />
    </MemoryRouter>,
  );
}

const last = <T,>(list: T[]): T | undefined => list[list.length - 1];

const count = () => screen.getByText(/will receive it/).closest('[role="status"]');

async function open(user: ReturnType<typeof userEvent.setup>) {
  renderButton();
  await user.click(screen.getByRole('button', { name: 'Invite to a meeting' }));
  const dialog = await screen.findByRole('dialog', { name: 'Invite to a meeting' });
  await waitFor(() => expect(count()).toHaveTextContent('23 leads will receive it'), { timeout: 3000 });
  return dialog;
}

async function toEmail(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: /Next: the email/ }));
  return screen.findByTitle('Email preview', undefined, { timeout: 3000 });
}

describe('inviting leads to a meeting', () => {
  it('is offered only to someone who may send it', async () => {
    serve();
    state.org = owner({ isOwner: false, role: 1, permissions: ['records.export'] });
    const { unmount } = renderButton();
    expect(screen.queryByRole('button', { name: 'Invite to a meeting' })).not.toBeInTheDocument();
    unmount();

    // A viewer, whatever their role says.
    state.org = owner();
    state.readOnly = true;
    const again = renderButton();
    expect(screen.queryByRole('button', { name: 'Invite to a meeting' })).not.toBeInTheDocument();
    again.unmount();

    // Someone who may edit records, which sending takes.
    state.org = owner({ isOwner: false, role: 1, permissions: ['records.edit'] });
    state.readOnly = false;
    renderButton();
    expect(screen.getByRole('button', { name: 'Invite to a meeting' })).toBeInTheDocument();
  });

  it('is not offered in demo mode, where there is no server to send it', () => {
    vi.stubEnv('VITE_API_URL', '');
    renderButton();
    expect(screen.queryByRole('button', { name: 'Invite to a meeting' })).not.toBeInTheDocument();
  });

  it('leaves the lost and not interested out at first, and counts again as the statuses change', async () => {
    serve();
    const user = userEvent.setup();
    const dialog = await open(user);

    // Asked for once the filters rested: not the lost, only the statuses on.
    expect(asked.audience[0]).toEqual({
      includeStatuses: ['New', 'Contacted', 'Connected'],
      excludeStatuses: ['Lost / Not Interested'],
    });
    const statuses = within(dialog).getByRole('group', { name: 'Status' });
    expect(within(statuses).getByRole('button', { name: /Lost \/ Not Interested/ })).toHaveAttribute('aria-pressed', 'false');
    expect(within(statuses).getByRole('button', { name: /^New/ })).toHaveAttribute('aria-pressed', 'true');

    await user.click(within(statuses).getByRole('button', { name: /^Contacted/ }));
    await waitFor(() => expect(count()).toHaveTextContent('12 leads will receive it'), { timeout: 3000 });
    expect(last(asked.audience)).toMatchObject({
      includeStatuses: ['New', 'Connected'],
      excludeStatuses: ['Contacted', 'Lost / Not Interested'],
    });

    // More filters: a source, the leads of nobody, the converted too.
    await user.click(within(dialog).getByRole('button', { name: /More filters/ }));
    await user.click(within(dialog).getByRole('button', { name: /^Referral/ }));
    await user.click(within(dialog).getByRole('button', { name: /^Unassigned/ }));
    await user.click(within(dialog).getByRole('switch', { name: 'Converted leads too' }));
    await waitFor(() => expect(last(asked.audience)).toMatchObject({ sources: ['Referral'], unassigned: true, includeConverted: true }), { timeout: 3000 });

    // None at all: nothing to count, nowhere to go.
    for (const name of [/^New/, /^Connected/]) await user.click(within(statuses).getByRole('button', { name }));
    const panel = within(dialog).getByRole('complementary', { name: 'Who receives it' });
    expect(await within(panel).findByText('Pick at least one status.')).toBeInTheDocument();
    expect(count()).toHaveTextContent('0 will receive it');
    expect(within(dialog).getByRole('button', { name: /Next: the email/ })).toBeDisabled();
  });

  it('shows who is left out, and why', async () => {
    serve();
    const user = userEvent.setup();
    const dialog = await open(user);

    expect(within(dialog).getByText('Léa Martin')).toBeInTheDocument();
    expect(within(dialog).getByText('+ 21 more')).toBeInTheDocument();
    const toggle = within(dialog).getByRole('button', { name: /4 left out/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');

    const reasons = within(dialog).getByRole('list', { name: 'Why they are left out' });
    expect(within(reasons).getByText('No email').parentElement).toHaveTextContent('No email2');
    expect(within(reasons).getByText('Do not contact').parentElement).toHaveTextContent('Do not contact1');
    expect(within(reasons).getByText('Unsubscribed or bounced').parentElement).toHaveTextContent('Unsubscribed or bounced1');
    expect(within(reasons).queryByText('Same email as another')).not.toBeInTheDocument();

    const people = within(dialog).getByRole('list', { name: 'Left out' });
    expect(within(people).getByText('Bruno Keller').closest('li')).toHaveTextContent('Do not contact');
    expect(within(people).getByText('Eva Roth').closest('li')).toHaveTextContent('No email');
  });

  it("previews the server's email for the first lead it goes to, in a sandbox, at either width", async () => {
    serve();
    const user = userEvent.setup();
    await open(user);
    const frame = await toEmail(user);

    expect(frame).toHaveAttribute('sandbox', '');
    expect(frame.getAttribute('srcdoc')).toContain('Bonjour Léa,');
    expect(screen.getByText('Léa, prenons rendez-vous')).toBeInTheDocument();
    expect(screen.getByText('RDV-7K2Q')).toBeInTheDocument();
    // The letter, in French (the workspace's language), with the lead's link and code for the server to fill in.
    const sent = asked.preview[0]!;
    expect(sent).toMatchObject({ language: 'fr', subject: '{{firstName}}, prenons rendez-vous', sampleLeadId: LEA });
    expect(String(sent.html)).toContain('href="{{bookingUrl}}"');
    expect(String(sent.html)).toContain('{{bookingCode}}');
    expect(String(sent.html)).toContain('Votre code');

    await user.click(screen.getByRole('button', { name: 'Mobile' }));
    expect(screen.getByRole('button', { name: 'Mobile' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTitle('Email preview')).toHaveStyle({ width: '375px' });

    // In English, the words that came with it are in English too.
    await user.click(screen.getByRole('button', { name: 'EN' }));
    await waitFor(() => expect(last(asked.preview)).toMatchObject({ language: 'en', subject: '{{firstName}}, shall we meet?' }), { timeout: 3000 });
    expect(String(last(asked.preview)?.html)).toContain('Your code');
    expect(screen.getByLabelText('Button')).toHaveValue('Book my meeting');
  });

  it('sends a test to the person asking, and says where it went', async () => {
    serve();
    const user = userEvent.setup();
    await open(user);
    await toEmail(user);

    expect(screen.getByLabelText(/Send a test to/)).toHaveValue('owner@pavillon46.ch');
    await user.click(screen.getByRole('button', { name: /Send test/ }));
    expect(await screen.findByText('Sent to owner@pavillon46.ch.')).toBeInTheDocument();
    expect(asked.test).toHaveLength(1);
    expect(asked.test[0]).toMatchObject({ language: 'fr', sampleLeadId: LEA });
  });

  it('sends after a confirm, once, and shows what went', async () => {
    serve();
    const user = userEvent.setup();
    await open(user);
    await toEmail(user);
    await user.click(screen.getByRole('button', { name: /Next: review/ }));

    await user.click(await screen.findByRole('button', { name: 'Send to 23 leads' }));
    expect(asked.send).toHaveLength(0);
    expect(screen.getByText('Send it now?')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Yes, send/ }));

    expect(await screen.findByText('On its way')).toBeInTheDocument();
    const invited = screen.getByText('Invited').closest('div');
    expect(invited).toHaveTextContent('23');
    expect(screen.getByText('Left out').closest('div')).toHaveTextContent('4');
    expect(asked.send).toHaveLength(1);
    expect(asked.send[0]).toMatchObject({
      language: 'fr',
      subject: '{{firstName}}, prenons rendez-vous',
      expectedRecipients: 23,
      audience: { includeStatuses: ['New', 'Contacted', 'Connected'], excludeStatuses: ['Lost / Not Interested'] },
    });
    expect(asked.send[0]?.sendId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(asked.send[0]?.html).toContain('{{bookingUrl}}');
  });

  it('says "already sent", not an error, when the server had sent it before', async () => {
    serve({
      send: () => HttpResponse.json({ id: 'n1', status: 'sent', recipients: 21, leftOut: 3, alreadySent: true, createdAtUtc: '2026-11-17T09:00:00Z' }),
    });
    const user = userEvent.setup();
    await open(user);
    await toEmail(user);
    await user.click(screen.getByRole('button', { name: /Next: review/ }));
    await user.click(await screen.findByRole('button', { name: 'Send to 23 leads' }));
    await user.click(screen.getByRole('button', { name: /Yes, send/ }));

    expect(await screen.findByText('Already sent')).toBeInTheDocument();
    expect(screen.getByText('Invited').closest('div')).toHaveTextContent('21');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it("shows the server's refusal where it happened, and counts again when who it goes to changed", async () => {
    serve({
      send: () => HttpResponse.json(
        { title: 'Conflict', status: 409, detail: 'Who it goes to has changed since you confirmed: it would now go to 24 leads, not 23. Check the list and confirm again.', errorCode: 'Newsletter.Changed.Conflict' },
        { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    });
    const user = userEvent.setup();
    await open(user);
    await toEmail(user);
    await user.click(screen.getByRole('button', { name: /Next: review/ }));
    await user.click(await screen.findByRole('button', { name: 'Send to 23 leads' }));
    const counted = asked.audience.length;
    await user.click(screen.getByRole('button', { name: /Yes, send/ }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Who it goes to has changed since you confirmed');
    expect(screen.queryByText('On its way')).not.toBeInTheDocument();
    await waitFor(() => expect(asked.audience.length).toBeGreaterThan(counted), { timeout: 3000 });
    // The same send is still the one confirmed next: the server had claimed nothing.
    expect(screen.getByRole('button', { name: 'Send to 23 leads' })).toBeInTheDocument();
  });

  it('says where to set the booking page up when there is none, and does not let an email that needs it go', async () => {
    serve({ setupOver: { bookingLinkAvailable: false, bookingProblem: 'There is no meeting booking page yet. Add it in Settings › Welcome & portal to give each lead a link to book.', bookingPageUrl: null } });
    server.use(http.post(`${API}/preview`, async ({ request }) => {
      asked.preview.push((await request.json()) as Record<string, unknown>);
      return HttpResponse.json(preview({ bookingProblem: 'There is no meeting booking page yet.' }));
    }));
    const user = userEvent.setup();
    const dialog = await open(user);

    expect(within(dialog).getByText('No booking link yet.')).toBeInTheDocument();
    expect(within(dialog).getByRole('link', { name: /Set it up/ })).toHaveAttribute('href', '/settings?tab=welcome');

    await toEmail(user);
    expect(await screen.findByText('There is no meeting booking page yet.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Next: review/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Send test/ })).toBeDisabled();
  });
});
