// Welcome & portal on the CadenceE side: what a welcome turns into as a request, how the
// portal's reports light up the timeline, and the confirm step that shows the email as
// it will go and will not send one with a placeholder nobody fills.
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { Onboarding, OnboardingPreview } from '@/app/api/onboarding';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const previewOnboarding = vi.fn<(r: unknown) => Promise<OnboardingPreview>>();
vi.mock('@/app/api/onboarding', async (original) => ({
  ...(await original<typeof import('@/app/api/onboarding')>()),
  previewOnboarding: (r: unknown) => previewOnboarding(r),
}));

const { draftFrom, toStartRequest, toPreviewRequest } = await import('./welcome');
const { stagesOf, OnboardingTimeline } = await import('./OnboardingTimeline');
const { WelcomeConfirmDialog } = await import('./WelcomeConfirmDialog');

const settings = {
  hasSecret: true, enabled: true, defaultLanguage: 'en' as const, addToClubMembers: false, configured: true,
  emailSubject: 'Hi {{firstName}}', emailHtml: '<p>{{welcomeMessage}}</p>', cadenceFields: [], receiverFields: [],
};

function handoff(status: Onboarding['status'], keys: string[], extra: Partial<Onboarding> = {}): Onboarding {
  return {
    id: 'o1', dealId: 'd1', name: 'Jean Dupont', email: 'jean@example.ch', language: 'fr', startUndetermined: true,
    status, canResend: false, createdAtUtc: '2026-11-12T09:00:00Z', updatedAtUtc: '2026-11-12T09:00:00Z',
    steps: keys.map((key, i) => ({ key, atUtc: `2026-11-12T09:0${i}:00Z` })),
    ...extra,
  };
}

describe('a welcome draft', () => {
  it('starts from the workspace settings, on when the portal is connected', () => {
    const d = draftFrom(settings);
    expect(d).toMatchObject({ send: true, language: 'en', addToClubMembers: false, undetermined: true, customEmail: false });
    expect(draftFrom(null).send).toBe(false);
    expect(draftFrom({ ...settings, configured: false }).send).toBe(false);
  });

  it('is a date or still to be decided, never neither', () => {
    const d = { ...draftFrom(settings), undetermined: false, startsOn: '2027-01-15', welcomeMessage: '  Welcome!  ' };
    expect(toStartRequest(d, 'lead-1')).toMatchObject({ leadId: 'lead-1', startsOnUtc: '2027-01-15', startUndetermined: false, welcomeMessage: 'Welcome!' });
    expect(toStartRequest({ ...d, startsOn: '' })).toMatchObject({ startsOnUtc: null, startUndetermined: true });
    expect(toStartRequest({ ...d, undetermined: true })).toMatchObject({ startsOnUtc: null, startUndetermined: true });
  });

  it("sends an email of its own only when asked to, else the workspace's template stands", () => {
    const d = { ...draftFrom(settings), emailHtml: '<p>mine</p>', emailSubject: 'Mine' };
    expect(toStartRequest(d).emailHtml).toBeUndefined();
    expect(toStartRequest({ ...d, customEmail: true })).toMatchObject({ emailHtml: '<p>mine</p>', emailSubject: 'Mine' });
    expect(toPreviewRequest({ ...d, customEmail: true }, { name: 'Ana' })).toMatchObject({ name: 'Ana', emailHtml: '<p>mine</p>' });
  });
});

describe('the handoff timeline', () => {
  it('lights up each stage the portal reports, and waits on the next', () => {
    const s = stagesOf(handoff('account_ready', ['queued', 'delivered', 'account_created']));
    expect(s.map((x) => x.state)).toEqual(['done', 'done', 'active', 'pending']);
  });

  it('counts a skipped report as done, and says when they already had an account', () => {
    const s = stagesOf(handoff('welcomed', ['queued', 'account_existing', 'email_sent']));
    expect(s.map((x) => x.state)).toEqual(['done', 'done', 'done', 'active']);
    expect(s[1]!.detail).toBe('They already had one');
  });

  it('shows who the welcome went out from, as the portal reports it', () => {
    const o = handoff('welcomed', ['queued', 'account_created', 'email_sent']);
    o.steps[2]!.detail = 'Sent from Pavillon 46 <hello@pavillon46.ch>';
    expect(stagesOf(o)[2]!.detail).toBe('Sent from Pavillon 46 <hello@pavillon46.ch>');
  });

  it('marks where it went wrong', () => {
    expect(stagesOf(handoff('failed', ['queued', 'undeliverable'])).map((x) => x.state)).toEqual(['error', 'pending', 'pending', 'pending']);
    const email = stagesOf(handoff('failed', ['queued', 'delivered', 'account_created', 'email_failed'])).map((x) => x.state);
    expect(email).toEqual(['done', 'done', 'error', 'pending']);
  });

  it('starts again from a resend', () => {
    const s = stagesOf(handoff('queued', ['queued', 'delivered', 'account_created', 'email_failed', 'resent']));
    expect(s.map((x) => x.state)).toEqual(['active', 'pending', 'pending', 'pending']);
  });

  it('says the portal is being retried', () => {
    render(<OnboardingTimeline onboarding={handoff('queued', ['queued'], {
      delivery: { status: 'queued', attempts: 2, lastStatusCode: 503, nextAttemptAtUtc: '2026-11-12T10:00:00Z' },
    })} />);
    expect(screen.getByText(/did not take it yet \(it answered 503\)/)).toBeInTheDocument();
  });
});

describe('confirming the welcome', () => {
  it('shows the email as it will go, and sends it on confirm', async () => {
    previewOnboarding.mockResolvedValueOnce({ subject: 'Hi Jean', html: '<p>Welcome, Jean</p>', usesReceiverDesign: false, unknown: [] });
    const onConfirm = vi.fn();
    render(
      <WelcomeConfirmDialog open onOpenChange={() => {}} request={{ name: 'Jean', language: 'en', startUndetermined: true }}
        recipient={{ name: 'Jean Dupont', email: 'jean@example.ch' }} confirmLabel="Convert & send welcome" onConfirm={onConfirm} />,
    );
    expect(await screen.findByText('Hi Jean')).toBeInTheDocument();
    expect(screen.getByTitle('Email preview')).toHaveAttribute('srcdoc', expect.stringContaining('<p>Welcome, Jean</p>'));
    expect(screen.getByTitle('Email preview').getAttribute('sandbox')).not.toContain('allow-scripts');
    expect(screen.getByText(/Starts: To be decided/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Convert & send welcome/ }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it('will not send an email with a placeholder nobody fills', async () => {
    previewOnboarding.mockResolvedValueOnce({ subject: 'Hi', html: '<p></p>', usesReceiverDesign: false, unknown: ['{{favouriteColour}}'] });
    render(
      <WelcomeConfirmDialog open onOpenChange={() => {}} request={{ name: 'Jean' }} recipient={{ name: 'Jean' }}
        confirmLabel="Send welcome" onConfirm={() => {}} />,
    );
    await waitFor(() => expect(screen.getByText(/Nothing fills \{\{favouriteColour\}\}/)).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /Send welcome/ })).toBeDisabled();
  });
});
