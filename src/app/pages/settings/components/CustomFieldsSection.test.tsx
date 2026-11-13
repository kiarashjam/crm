// Settings for the workspace's own fields and tags: every kind of field with what it needs
// (options, a currency, what a link points at, a default), fields put in order and changed
// in place, members only when the club module is on, and tags made, renamed and deleted by
// whoever may.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { CustomFieldDef, Tag } from '@/app/api';
import { withQueries } from '@/test/queryWrapper';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const modules = vi.hoisted(() => ({ club: true }));
vi.mock('@/app/hooks/useModules', () => ({ useModules: () => ({ sales: true, club: modules.club }) }));
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
  fields: vi.fn<(entity?: string) => Promise<CustomFieldDef[]>>(),
  create: vi.fn<(input: Record<string, unknown>) => Promise<CustomFieldDef>>(),
  update: vi.fn<(id: string, patch: Record<string, unknown>) => Promise<CustomFieldDef>>(),
  reorder: vi.fn<(entity: string, ids: string[]) => Promise<CustomFieldDef[]>>(),
  remove: vi.fn<(id: string) => Promise<boolean>>(),
  tags: vi.fn<() => Promise<Tag[]>>(),
  createTag: vi.fn<(input: { name: string; color?: string }) => Promise<Tag>>(),
  updateTag: vi.fn<(id: string, patch: { name?: string; color?: string }) => Promise<Tag>>(),
}));
vi.mock('@/app/api/customFields', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/customFields')>()),
  getFieldDefinitions: (entity?: string) => api.fields(entity),
  createFieldDefinition: (input: Record<string, unknown>) => api.create(input),
  updateFieldDefinition: (id: string, patch: Record<string, unknown>) => api.update(id, patch),
  reorderFieldDefinitions: (entity: string, ids: string[]) => api.reorder(entity, ids),
  deleteFieldDefinition: (id: string) => api.remove(id),
}));
vi.mock('@/app/api/tags', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/tags')>()),
  getTags: () => api.tags(),
  createTag: (input: { name: string; color?: string }) => api.createTag(input),
  updateTag: (id: string, patch: { name?: string; color?: string }) => api.updateTag(id, patch),
}));

const { CustomFieldsSection } = await import('./CustomFieldsSection');

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const BUDGET: CustomFieldDef = { id: 'f-budget', entityType: 'lead', key: 'budget', label: 'Budget', type: 'number', order: 0 };
const SOURCE: CustomFieldDef = { id: 'f-source', entityType: 'lead', key: 'source', label: 'Source', type: 'select', options: ['Web', 'Fair'], order: 1 };
const VIP: Tag = { id: 't-vip', name: 'VIP', color: 'amber', uses: 2 };

beforeEach(() => {
  vi.clearAllMocks();
  modules.club = true;
  as([PERMISSION.EditRecords, PERMISSION.ManageSettings]);
  api.fields.mockImplementation(async (entity) => (entity === 'lead' ? [BUDGET, SOURCE] : []));
  api.create.mockImplementation(async (input) => ({ ...BUDGET, id: 'f-new', ...input }) as CustomFieldDef);
  api.update.mockImplementation(async (id, patch) => ({ ...BUDGET, id, ...patch }) as CustomFieldDef);
  api.reorder.mockResolvedValue([SOURCE, BUDGET]);
  api.tags.mockResolvedValue([VIP]);
  api.createTag.mockImplementation(async (input) => ({ id: 't-new', name: input.name, color: (input.color ?? 'slate') as Tag['color'], uses: 0 }));
  api.updateTag.mockImplementation(async (id, patch) => ({ ...VIP, id, ...patch }) as Tag);
});

describe("the workspace's own fields", () => {
  it('adds a money field in the currency it is kept in', async () => {
    const user = userEvent.setup();
    render(withQueries(<CustomFieldsSection />));
    await screen.findByText('Budget');

    await user.type(screen.getByLabelText('Label'), 'Annual fee');
    await user.click(screen.getByLabelText('Type'));
    await user.click(await screen.findByRole('option', { name: 'Money' }));
    await user.click(screen.getByRole('button', { name: /add field/i }));
    await waitFor(() => expect(api.create).toHaveBeenCalledWith(expect.objectContaining({
      entityType: 'lead', label: 'Annual fee', type: 'money', currency: 'CHF',
    })));
  });

  it('puts fields in order and changes one in place, its default included', async () => {
    const user = userEvent.setup();
    render(withQueries(<CustomFieldsSection />));
    await user.click(await screen.findByRole('button', { name: 'Move Budget down' }));
    await waitFor(() => expect(api.reorder).toHaveBeenCalledWith('lead', ['f-source', 'f-budget']));

    await user.click(screen.getByRole('button', { name: 'Edit Budget' }));
    const form = screen.getByLabelText('Edit Budget');
    await user.clear(within(form).getByLabelText('Label'));
    await user.type(within(form).getByLabelText('Label'), 'Budget (CHF)');
    await user.type(within(form).getByLabelText('Default for new records'), '1000');
    await user.click(within(form).getByRole('button', { name: /save/i }));
    await waitFor(() => expect(api.update).toHaveBeenCalledWith('f-budget', { label: 'Budget (CHF)', required: false, defaultValue: 1000 }));
  });

  it('offers members only with the club module on, and changes nothing for whoever may not manage settings', async () => {
    modules.club = false;
    as([PERMISSION.EditRecords]);
    render(withQueries(<CustomFieldsSection />));
    await screen.findByText('Budget');
    expect(screen.queryByRole('tab', { name: 'Members' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add field/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit Budget' })).not.toBeInTheDocument();
    // A tag can still be made by anyone who edits records; renaming one is for a manager.
    expect(screen.getByRole('button', { name: /add tag/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit VIP' })).not.toBeInTheDocument();
  });
});

describe("the workspace's tags", () => {
  it('makes a coloured tag and renames one', async () => {
    const user = userEvent.setup();
    render(withQueries(<CustomFieldsSection />));
    await screen.findByText('VIP');

    await user.type(screen.getByLabelText('Name'), 'Board');
    await user.click(screen.getByRole('radio', { name: 'green' }));
    await user.click(screen.getByRole('button', { name: /add tag/i }));
    await waitFor(() => expect(api.createTag).toHaveBeenCalledWith({ name: 'Board', color: 'green' }));

    await user.click(screen.getByRole('button', { name: 'Edit VIP' }));
    const name = screen.getByDisplayValue('VIP');
    await user.clear(name);
    await user.type(name, 'Very important');
    await user.click(screen.getByRole('button', { name: /^save$/i }));
    await waitFor(() => expect(api.updateTag).toHaveBeenCalledWith('t-vip', { name: 'Very important', color: 'amber' }));
  });
});
