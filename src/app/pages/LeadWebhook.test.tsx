// The lead webhook key is kept only as a hash, so the page cannot show an existing key:
// it shows a new one once, right after it is made, and otherwise says there is one.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const getWebhookInfo = vi.fn(async (_org: string) => ({
  webhookUrl: 'https://api.example.ch/api/webhook/leads',
  apiKeyCreatedAt: '2026-10-01T09:00:00Z',
  hasApiKey: true,
  usesDefaultWebhookPassword: true,
  passwordWebhookUrl: '',
}));
const generateWebhookApiKey = vi.fn(async (_org: string) => 'aci_brand_new_key_1234567890');

vi.mock('@/app/api/webhook', () => ({
  getWebhookInfo: (org: string) => getWebhookInfo(org),
  generateWebhookApiKey: (org: string) => generateWebhookApiKey(org),
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'me', isOwner: true, role: 0 } }),
  useOrgOptional: () => null,
}));

import LeadWebhook from './LeadWebhook';

beforeEach(() => {
  getWebhookInfo.mockClear();
  generateWebhookApiKey.mockClear();
});

describe('the lead webhook key', () => {
  it('says a key exists without showing one, and shows a new key once it is made', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LeadWebhook />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/It is shown only once, when it is made/)).toBeInTheDocument();
    expect(screen.queryByDisplayValue(/^aci_/)).toBeNull();

    await user.click(screen.getByRole('button', { name: /Regenerate Key/ }));
    await user.click(await screen.findByRole('button', { name: /^Regenerate$/ }));

    await waitFor(() => expect(screen.getByDisplayValue('aci_brand_new_key_1234567890')).toBeInTheDocument());
    expect(screen.getByText(/cannot show it again/)).toBeInTheDocument();
    expect(generateWebhookApiKey).toHaveBeenCalledWith('org-1');
  });
});
