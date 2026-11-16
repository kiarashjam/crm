// A record's tags and custom fields on its page: tags put on and taken off and new ones made,
// values saved field by field (only what changed is sent), required fields kept filled, a
// failed read offered again, and none of the controls for whoever may not change the record.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { CustomFieldDef, CustomFieldValues, Tag } from '@/app/api';
import { withQueries } from '@/test/queryWrapper';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' },
  };
};
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const api = vi.hoisted(() => ({
  tags: vi.fn<() => Promise<Tag[]>>(),
  recordTags: vi.fn<() => Promise<string[]>>(),
  setRecordTags: vi.fn<(entity: string, id: string, tags: string[]) => Promise<string[]>>(),
  createTag: vi.fn<(input: { name: string }) => Promise<Tag>>(),
  fields: vi.fn<() => Promise<CustomFieldDef[]>>(),
  values: vi.fn<() => Promise<CustomFieldValues>>(),
  save: vi.fn<(entity: string, id: string, values: CustomFieldValues) => Promise<boolean>>(),
}));
vi.mock('@/app/api/tags', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/tags')>()),
  getTags: () => api.tags(),
  getRecordTags: () => api.recordTags(),
  setRecordTags: (entity: string, id: string, tags: string[]) => api.setRecordTags(entity, id, tags),
  createTag: (input: { name: string }) => api.createTag(input),
}));
vi.mock('@/app/api/customFields', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/customFields')>()),
  getFieldDefinitions: () => api.fields(),
  getFieldValues: () => api.values(),
  saveFieldValues: (entity: string, id: string, values: CustomFieldValues) => api.save(entity, id, values),
}));

const { default: RecordTagsEditor } = await import('./RecordTagsEditor');
const { default: CustomFieldsCard } = await import('../CustomFieldsCard');

const VIP: Tag = { id: 't-vip', name: 'VIP', color: 'amber', uses: 3 };
const GOLF: Tag = { id: 't-golf', name: 'Golf', color: 'green', uses: 1 };

const FIELDS: CustomFieldDef[] = [
  { id: 'f1', entityType: 'contact', key: 'tier', label: 'Tier', type: 'select', options: ['Gold', 'Silver'], order: 0, required: true },
  { id: 'f2', entityType: 'contact', key: 'seats', label: 'Seats', type: 'number', order: 1, required: true },
  { id: 'f3', entityType: 'contact', key: 'interests', label: 'Interests', type: 'multiselect', options: ['Golf', 'Wine'], order: 2 },
];

function renderIn(ui: React.ReactElement) {
  return render(withQueries(<MemoryRouter>{ui}</MemoryRouter>));
}

beforeEach(() => {
  vi.clearAllMocks();
  as([PERMISSION.EditRecords]);
  api.tags.mockResolvedValue([GOLF, VIP]);
  api.recordTags.mockResolvedValue([VIP.id]);
  api.setRecordTags.mockImplementation(async (_e, _id, tags) => tags);
  api.fields.mockResolvedValue(FIELDS);
  api.values.mockResolvedValue({ tier: 'Gold', seats: 4, interests: ['Golf'] });
  api.save.mockResolvedValue(true);
});

describe('the tags on a record', () => {
  it('shows them, puts another on from the list and takes one off', async () => {
    const user = userEvent.setup();
    renderIn(<RecordTagsEditor entityType="contact" recordId="c-1" />);
    expect(await screen.findByText('VIP')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /tag/i }));
    await user.click(await screen.findByRole('option', { name: /golf/i }));
    await waitFor(() => expect(api.setRecordTags).toHaveBeenLastCalledWith('contact', 'c-1', [VIP.id, GOLF.id]));

    await user.keyboard('{Escape}');
    await user.click(await screen.findByRole('button', { name: 'Remove VIP' }));
    await waitFor(() => expect(api.setRecordTags).toHaveBeenLastCalledWith('contact', 'c-1', [GOLF.id]));
  });

  it('makes a tag the workspace does not have yet, and puts it on', async () => {
    const user = userEvent.setup();
    const NEW: Tag = { id: 't-new', name: 'Board', color: 'slate', uses: 0 };
    api.createTag.mockResolvedValue(NEW);
    renderIn(<RecordTagsEditor entityType="contact" recordId="c-1" />);
    await screen.findByText('VIP');

    await user.click(screen.getByRole('button', { name: /tag/i }));
    await user.type(screen.getByPlaceholderText('Find or make a tag…'), 'Board');
    await user.click(await screen.findByRole('option', { name: /make the tag/i }));

    await waitFor(() => expect(api.createTag).toHaveBeenCalledWith({ name: 'Board' }));
    await waitFor(() => expect(api.setRecordTags).toHaveBeenLastCalledWith('contact', 'c-1', [VIP.id, NEW.id]));
  });

  it('puts the tags back as they were, and says so, when the save fails', async () => {
    const user = userEvent.setup();
    api.setRecordTags.mockRejectedValue(new Error('Your role here does not let you create or edit records.'));
    renderIn(<RecordTagsEditor entityType="contact" recordId="c-1" />);
    await user.click(await screen.findByRole('button', { name: 'Remove VIP' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Your role here does not let you create or edit records.'));
    expect(await screen.findByText('VIP')).toBeInTheDocument();
  });

  it("offers no change to someone who may not change that kind of record: a member's tags are club work", async () => {
    renderIn(<RecordTagsEditor entityType="member" recordId="m-1" />);
    expect(await screen.findByText('VIP')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /tag/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove VIP' })).not.toBeInTheDocument();
  });

  it('offers to try again when they could not be read', async () => {
    const user = userEvent.setup();
    api.recordTags.mockRejectedValueOnce(new Error('offline'));
    renderIn(<RecordTagsEditor entityType="contact" recordId="c-1" />);
    await user.click(await screen.findByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('VIP')).toBeInTheDocument();
  });
});

describe('the custom fields on a record', () => {
  it('saves only the fields that changed', async () => {
    const user = userEvent.setup();
    renderIn(<CustomFieldsCard entityType="contact" recordId="c-1" />);
    const seats = await screen.findByLabelText(/^Seats/);

    await user.clear(seats);
    await user.type(seats, '6');
    await user.click(within(screen.getByRole('group', { name: /interests/i })).getByRole('checkbox', { name: /wine/i }));
    await user.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(api.save).toHaveBeenCalledWith('contact', 'c-1', { seats: 6, interests: ['Golf', 'Wine'] }));
    expect(toast.success).toHaveBeenCalledWith('Custom fields saved');
  });

  it('marks required fields and will not empty one that is filled', async () => {
    const user = userEvent.setup();
    renderIn(<CustomFieldsCard entityType="contact" recordId="c-1" />);
    await screen.findByLabelText(/^Seats/);
    expect(screen.getAllByLabelText('required').length).toBeGreaterThan(0);

    expect(screen.queryByRole('button', { name: 'Clear' })).not.toBeInTheDocument();
    await user.clear(screen.getByLabelText(/^Seats/));
    expect(await screen.findByRole('alert')).toHaveTextContent('Seats is required and cannot be emptied.');
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();
  });

  it('shows the values without controls to someone who may not change the record', async () => {
    as([PERMISSION.ExportRecords]);
    renderIn(<CustomFieldsCard entityType="contact" recordId="c-1" />);
    expect(await screen.findByLabelText(/^Seats/)).toBeDisabled();
  });

  it('offers to try again when the fields could not be read, and draws nothing when there are none', async () => {
    const user = userEvent.setup();
    api.fields.mockRejectedValueOnce(new Error('offline'));
    const { container } = renderIn(<CustomFieldsCard entityType="contact" recordId="c-1" />);
    await user.click(await screen.findByRole('button', { name: 'Try again' }));
    expect(await screen.findByLabelText(/^Seats/)).toBeInTheDocument();

    api.fields.mockResolvedValue([]);
    container.remove();
    const { container: empty } = renderIn(<CustomFieldsCard entityType="deal" recordId="d-1" />);
    await waitFor(() => expect(api.fields).toHaveBeenCalledTimes(3));
    await waitFor(() => expect(empty.textContent).toBe(''));
  });
});
