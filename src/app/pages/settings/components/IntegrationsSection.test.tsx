// Outbound webhooks used to be one post with no record: a wrong address or a receiver
// that was down was only found when events went missing. The owner and managers now
// see what was posted and what the receiver answered.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Organization } from '@/app/api/organizations';
import type { WebhookDelivery, WebhookInfo } from '@/app/api/webhook';

const state = vi.hoisted(() => ({
  info: null as WebhookInfo | null,
  deliveries: [] as WebhookDelivery[],
  getDeliveries: vi.fn(),
}));
vi.mock('@/app/api/apiClient', () => ({ servesDemoData: () => false }));
vi.mock('@/app/api/webhook', () => ({
  getWebhookInfo: vi.fn(async () => state.info),
  getWebhookDeliveries: (...args: unknown[]) => state.getDeliveries(...args),
  updateOutboundWebhook: vi.fn(),
  updateWebhookPassword: vi.fn(),
}));
vi.mock('@/app/components/LoadingOverlay', () => ({ usePageLoading: () => undefined }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const { IntegrationsSection, deliveryText } = await import('./IntegrationsSection');

const org = (role: number): Organization => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'o', isOwner: role === 0, role });

const delivery = (over: Partial<WebhookDelivery>): WebhookDelivery => ({
  id: 'd1', eventName: 'lead.created', status: 'sent', attempts: 1, lastStatusCode: 200, lastError: null,
  createdAtUtc: '2026-11-09T10:00:00Z', sentAtUtc: '2026-11-09T10:00:01Z', nextAttemptAtUtc: null, ...over,
});

beforeEach(() => {
  state.info = {
    webhookUrl: '/api/webhook/leads', apiKeyCreatedAt: null, hasApiKey: false, usesDefaultWebhookPassword: true,
    passwordWebhookUrl: 'https://api.example.ch/api/webhook/organizations/org-1/leads',
    outboundWebhookUrl: 'https://hooks.example.com/cadence', hasOutboundWebhookSecret: true,
  };
  state.deliveries = [
    delivery({ id: 'd1' }),
    delivery({ id: 'd2', eventName: 'deal.won', status: 'queued', attempts: 2, lastStatusCode: 503, sentAtUtc: null, nextAttemptAtUtc: '2026-11-09T10:20:00Z' }),
  ];
  state.getDeliveries.mockReset();
  state.getDeliveries.mockImplementation(async () => state.deliveries);
});

describe('outbound webhook deliveries', () => {
  it('shows the owner what was posted and what the receiver answered', async () => {
    render(<MemoryRouter><IntegrationsSection currentOrg={org(0)} /></MemoryRouter>);

    expect(await screen.findByText('Recent deliveries', { selector: 'h4' })).toBeTruthy();
    expect(screen.getByText('Delivered')).toBeTruthy();
    expect(screen.getByText(/^Not taken yet \(answered 503\); trying again at/)).toBeTruthy();
    expect(state.getDeliveries).toHaveBeenCalledWith('org-1');
  });

  it('does not ask for them for a rep, whom the server refuses', async () => {
    render(<MemoryRouter><IntegrationsSection currentOrg={org(1)} /></MemoryRouter>);

    expect(await screen.findByText('Send events to your systems')).toBeTruthy();
    expect(state.getDeliveries).not.toHaveBeenCalled();
    expect(screen.queryByText('Recent deliveries')).toBeNull();
  });

  it('says how each one went, in words', () => {
    expect(deliveryText(delivery({ attempts: 3 }))).toBe('Delivered on try 3');
    expect(deliveryText(delivery({ status: 'queued', attempts: 0, lastStatusCode: null }))).toBe('Waiting to be posted');
    expect(deliveryText(delivery({ status: 'failed', attempts: 9, lastStatusCode: 500 }))).toBe('Given up after 9 tries (answered 500)');
    expect(deliveryText(delivery({ status: 'failed', attempts: 1, lastStatusCode: 410 }))).toBe('Given up after 1 try (answered 410)');
    expect(deliveryText(delivery({ status: 'cancelled' }))).toBe('Not posted: the address was removed first');
  });
});
