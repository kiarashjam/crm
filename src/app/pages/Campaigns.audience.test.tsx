// The campaign editor's audience: beyond tiers and statuses, the members' interests and tags (the
// Members list's own filters), a saved Members view and their habits at the club, with the
// server's count of who it reaches and who it leaves out and why, and the segment saved as set.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Campaign, CampaignAudience } from '@/app/api/campaigns';
import type { CustomFieldDef } from '@/app/api/customFields';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), warning: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const AUDIENCE: CampaignAudience = {
  purpose: 'marketing', chosen: 12, matching: 10, withAddress: 9, agreed: 7, willReceive: 6, suppressed: 1, doNotContact: 0,
  statusExcluded: 2, erased: 0, noEmail: 1, noConsent: 2, alreadyRegistered: 0, alreadyInvited: 0, duplicates: 0,
  recipients: [{ memberId: 'm1', name: 'Anna Keller', email: 'anna@example.ch', tier: 'Gold', status: 'Active', language: 'fr' }],
  leftOut: [{ memberId: 'm2', name: 'Bruno Keller', email: null, tier: 'Gold', status: 'Active', reason: 'no_email', language: 'en' }],
};

const api = vi.hoisted(() => ({
  audience: vi.fn(),
  create: vi.fn(),
  fields: vi.fn(),
}));
vi.mock('@/app/api/campaigns', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/campaigns')>()),
  getCampaigns: async () => [] as Campaign[],
  getCampaignAudience: (...a: unknown[]) => api.audience(...a),
  createCampaign: (...a: unknown[]) => api.create(...a),
}));
vi.mock('@/app/api/customFields', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/customFields')>()),
  getFieldDefinitions: (...a: unknown[]) => api.fields(...a),
}));

const Campaigns = (await import('./Campaigns')).default;

const INTERESTS: CustomFieldDef = {
  id: 'f1', entityType: 'member', key: 'interests', label: 'Interests', type: 'multiselect', options: ['Wine', 'Art', 'Golf'], order: 0,
};

const show = () => render(withQueries(<MemoryRouter><Campaigns /></MemoryRouter>));

async function openEditor() {
  const user = userEvent.setup();
  show();
  await user.click(await screen.findByRole('button', { name: /New campaign/ }));
  const dialog = await screen.findByRole('dialog');
  return { user, dialog };
}

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  api.audience.mockReset().mockResolvedValue(AUDIENCE);
  api.create.mockReset().mockImplementation(async (input: object) => ({ id: 'cmp-new', status: 'Draft', ...input }));
  api.fields.mockReset().mockResolvedValue([INTERESTS]);
});

describe('the campaign editor’s audience', () => {
  it('says who it reaches and who it leaves out, for each reason, as the server counted it', async () => {
    const { dialog } = await openEditor();
    const section = within(dialog).getByRole('region', { name: 'Who it is for' });

    expect(within(section).getByRole('group', { name: 'Tiers' })).toBeInTheDocument();
    expect(within(section).getByRole('group', { name: 'Interests and tags' })).toBeInTheDocument();
    expect(within(section).getByLabelText('Start from a saved Members view')).toBeInTheDocument();
    expect(within(section).getByRole('group', { name: 'Habits at the club' })).toBeInTheDocument();
    await waitFor(() => expect(within(section).getByTestId('audience-will-receive')).toHaveTextContent('6'));
    const reasons = within(section).getByRole('list', { name: 'Left out, by reason' });
    expect(reasons).toHaveTextContent('2 not in the statuses chosen');
    expect(reasons).toHaveTextContent('1 no email address');
    expect(reasons).toHaveTextContent('2 no consent to marketing by email');
    expect(reasons).toHaveTextContent('1 address unsubscribed, bounced or complained');
    expect(api.audience).toHaveBeenCalledWith({ statuses: ['Active'] }, 'marketing', expect.anything());
  });

  it('chooses members by their habits and saves the segment as set', async () => {
    const { user, dialog } = await openEditor();
    await waitFor(() => expect(api.audience).toHaveBeenCalled());

    await user.click(within(dialog).getByLabelText('Has not visited in the last'));
    await user.click(within(dialog).getByLabelText('Came to at least'));
    await waitFor(() => expect(api.audience).toHaveBeenLastCalledWith(
      { statuses: ['Active'], minEvents: 2, eventMonths: 6, notVisitedDays: 60 }, 'marketing', expect.anything(),
    ));

    await user.type(within(dialog).getByLabelText('Name'), 'We miss you');
    await user.type(within(dialog).getByLabelText('Subject'), 'Come back to the club');
    await user.type(within(dialog).getByLabelText('Message'), 'A glass is waiting for you.');
    await user.click(within(dialog).getByRole('button', { name: 'Save draft' }));

    await waitFor(() => expect(api.create).toHaveBeenCalled());
    expect(api.create.mock.calls[0]![0]).toMatchObject({
      segment: { statuses: ['Active'], minEvents: 2, eventMonths: 6, notVisitedDays: 60 },
    });
  });

  it('chooses members by an interest, with the Members list’s own field filter', async () => {
    const { user, dialog } = await openEditor();
    await user.click(await within(dialog).findByRole('button', { name: /Field filter/ }));
    // The comparison for a multiselect starts at "has any of": pick the options it takes.
    await user.click(await screen.findByRole('checkbox', { name: 'Wine' }));
    await user.click(screen.getByRole('checkbox', { name: 'Art' }));
    await user.click(screen.getByRole('button', { name: /Add filter/ }));

    await waitFor(() => expect(api.audience).toHaveBeenLastCalledWith(
      { statuses: ['Active'], fields: ['interests:any:Wine|Art'] }, 'marketing', expect.anything(),
    ));
    expect(within(dialog).getByText('Interests has any of Wine, Art')).toBeInTheDocument();
  });

  it('says how to add an Interests field while the club has no member fields', async () => {
    api.fields.mockResolvedValue([]);
    const { dialog } = await openEditor();
    expect(await within(dialog).findByText(/add a field such as “Interests”/)).toBeInTheDocument();
    expect(within(dialog).getByRole('link', { name: 'Settings › Fields & Tags' })).toHaveAttribute('href', '/settings?tab=customFields');
  });

  it('a count that did not load says so, with a way to try again', async () => {
    api.audience.mockRejectedValueOnce(new Error('The server is not answering.'));
    const { user, dialog } = await openEditor();
    expect(await within(dialog).findByText(/Who it reaches could not be worked out/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /Retry/ }));
    await waitFor(() => expect(within(dialog).getByTestId('audience-will-receive')).toHaveTextContent('6'));
  });
});
