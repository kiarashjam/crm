// A list's tag and field filters, its saved views, its tag and field columns, tagging many
// records at once, and an export's tag and field columns.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { CustomFieldDef, CustomFieldValues, SavedView, Tag } from '@/app/api';
import type { ViewPreference } from '@/app/lib/viewPreferences';
import { formatMoney } from '@/app/lib/money';
import { withQueries } from '@/test/queryWrapper';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), warning: vi.fn() }));
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
  fields: vi.fn<() => Promise<CustomFieldDef[]>>(),
  values: vi.fn<(entity: string, ids: string[]) => Promise<Record<string, CustomFieldValues>>>(),
  tags: vi.fn<() => Promise<Tag[]>>(),
  recordTags: vi.fn<(entity: string, ids: string[]) => Promise<Record<string, string[]>>>(),
  bulk: vi.fn<(input: Record<string, unknown>) => Promise<{ records: number; added: number; removed: number; skipped: number }>>(),
  views: vi.fn<() => Promise<SavedView[]>>(),
  createView: vi.fn<(input: Record<string, unknown>) => Promise<SavedView>>(),
}));
vi.mock('@/app/api/customFields', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/customFields')>()),
  getFieldDefinitions: () => api.fields(),
  getFieldValuesBatch: (entity: string, ids: string[]) => api.values(entity, ids),
}));
vi.mock('@/app/api/tags', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/tags')>()),
  getTags: () => api.tags(),
  getRecordTagsBatch: (entity: string, ids: string[]) => api.recordTags(entity, ids),
  bulkTag: (input: Record<string, unknown>) => api.bulk(input),
}));
vi.mock('@/app/api/savedViews', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/savedViews')>()),
  getSavedViews: () => api.views(),
  createSavedView: (input: Record<string, unknown>) => api.createView(input),
}));

const { FlexFilterBar } = await import('./FlexFilterBar');
const { SavedViewsMenu } = await import('./SavedViewsMenu');
const { BulkTagButton } = await import('./BulkTagDialog');
const { useFlexColumns } = await import('./useFlexColumns');
const { flexExportColumns } = await import('@/app/lib/flexExport');

const VIP: Tag = { id: 't-vip', name: 'VIP', color: 'amber', uses: 3 };
const LAPSED: Tag = { id: 't-lapsed', name: 'Lapsed', color: 'red', uses: 1 };
const FIELDS: CustomFieldDef[] = [
  { id: 'f1', entityType: 'contact', key: 'tier', label: 'Tier', type: 'select', options: ['Gold', 'Silver'], order: 0 },
  { id: 'f2', entityType: 'contact', key: 'spend', label: 'Spend', type: 'money', currency: 'CHF', order: 1 },
];

/** A list page's address, kept in state as the router would keep it. */
function Address({ initial = '', children }: { initial?: string; children: (params: URLSearchParams, set: (next: URLSearchParams) => void) => React.ReactNode }) {
  const [params, setParams] = useState(() => new URLSearchParams(initial));
  return (
    <>
      <output aria-label="address">{params.toString()}</output>
      {children(params, setParams)}
    </>
  );
}

const address = () => decodeURIComponent(screen.getByLabelText('address').textContent ?? '');

beforeEach(() => {
  vi.clearAllMocks();
  as([PERMISSION.EditRecords]);
  api.fields.mockResolvedValue(FIELDS);
  api.tags.mockResolvedValue([VIP, LAPSED]);
  api.values.mockResolvedValue({ c1: { tier: 'Gold', spend: { amount: 1200, currency: 'CHF' } } });
  api.recordTags.mockResolvedValue({ c1: [VIP.id], c2: [LAPSED.id] });
  api.views.mockResolvedValue([]);
  api.bulk.mockResolvedValue({ records: 2, added: 2, removed: 1, skipped: 0 });
});

describe("a list's tag and field filters", () => {
  it('put the conditions in the address under the names the server reads, and take them off again', async () => {
    const user = userEvent.setup();
    render(withQueries(
      <Address initial="page=3&status=archived">{(params, set) => <FlexFilterBar entity="contact" params={params} onChange={set} />}</Address>,
    ));

    await user.click(await screen.findByRole('button', { name: /tags/i }));
    await user.click(within(screen.getByRole('group', { name: 'Filter on VIP' })).getByRole('button', { name: 'With' }));
    await user.click(within(screen.getByRole('group', { name: 'Filter on Lapsed' })).getByRole('button', { name: 'Without' }));
    expect(address()).toBe('page=1&status=archived&tag=t-vip&notTag=t-lapsed');

    await user.keyboard('{Escape}');
    await user.click(screen.getByRole('button', { name: 'Field filter' }));
    await user.selectOptions(screen.getByLabelText('Field'), 'spend');
    await user.selectOptions(screen.getByLabelText('Comparison'), 'between');
    await user.type(screen.getByLabelText('From'), '500');
    await user.type(screen.getByLabelText('To'), '6000');
    await user.click(screen.getByRole('button', { name: /add filter/i }));
    expect(address()).toContain('field=spend:between:500..6000');
    expect(screen.getByText('Spend is between 500 and 6000')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Remove the filter Spend is between 500 and 6000' }));
    expect(address()).not.toContain('field=');
    await user.click(screen.getByRole('button', { name: /clear tag and field filters/i }));
    expect(address()).toBe('page=1&status=archived');
  });

  it('sorts by a field through the page', async () => {
    const user = userEvent.setup();
    const onSortField = vi.fn();
    render(withQueries(<FlexFilterBar entity="contact" params={new URLSearchParams()} onChange={vi.fn()} onSortField={onSortField} />));
    await user.selectOptions(await screen.findByLabelText('Sort by a custom field'), 'spend:desc');
    expect(onSortField).toHaveBeenCalledWith('spend', 'desc');
  });

  it('shows nothing where the workspace has no tags or fields', async () => {
    api.fields.mockResolvedValue([]);
    api.tags.mockResolvedValue([]);
    const { container } = render(withQueries(<FlexFilterBar entity="contact" params={new URLSearchParams()} onChange={vi.fn()} />));
    await waitFor(() => expect(api.tags).toHaveBeenCalled());
    expect(container.textContent).toBe('');
  });
});

/** A view preference that remembers which columns were turned on. */
function fakeView(): ViewPreference & { columns: Record<string, boolean> } {
  const columns: Record<string, boolean> = {};
  return {
    columns,
    layout: 'table', density: 'comfortable', layouts: ['table'], customised: false,
    shows: (id, byDefault = true) => columns[id] ?? byDefault,
    setLayout: vi.fn(), setDensity: vi.fn(), setColumn: (id, on) => { columns[id] = on; }, reset: vi.fn(),
    setColumns: (next) => Object.assign(columns, next),
  };
}

describe("a list's saved views", () => {
  const GOLD: SavedView = {
    id: 'v1', list: 'contacts', name: 'Gold members', shared: true, mine: false, isDefault: true,
    query: { field: ['tier:is:Gold'], sort: 'name-desc' }, columns: ['name', 'cf:tier'], ownerName: 'Olga', version: 'x', updatedAtUtc: '',
  };
  const COLUMNS = [{ id: 'name', label: 'Name', required: true }, { id: 'email', label: 'Email' }, { id: 'cf:tier', label: 'Tier', byDefault: false }];

  it('opens a bare list with the default view, filters and columns, once', async () => {
    api.views.mockResolvedValue([GOLD]);
    const view = fakeView();
    render(withQueries(
      <Address>{(params, set) => <SavedViewsMenu list="contacts" params={params} onChange={set} view={view} columns={COLUMNS} />}</Address>,
    ));
    await waitFor(() => expect(address()).toBe('field=tier:is:Gold&sort=name-desc'));
    expect(view.columns).toEqual({ email: false, 'cf:tier': true });
  });

  it('leaves a list opened with filters of its own as it is', async () => {
    api.views.mockResolvedValue([GOLD]);
    render(withQueries(
      <Address initial="status=archived">{(params, set) => <SavedViewsMenu list="contacts" params={params} onChange={set} />}</Address>,
    ));
    await waitFor(() => expect(api.views).toHaveBeenCalled());
    expect(address()).toBe('status=archived');
  });

  it("saves the list's filters and shown columns under a name, shared when asked", async () => {
    const user = userEvent.setup();
    api.createView.mockImplementation(async (input) => ({ ...GOLD, ...input, id: 'v2', mine: true }) as SavedView);
    const view = fakeView();
    render(withQueries(
      <Address initial="page=2&tag=t-vip&field=spend:gte:1000">{(params, set) => <SavedViewsMenu list="contacts" params={params} onChange={set} view={view} columns={COLUMNS} />}</Address>,
    ));
    await user.click(screen.getByRole('button', { name: /views/i }));
    await user.click(await screen.findByRole('menuitem', { name: /save these filters/i }));
    await user.type(screen.getByLabelText('Name'), 'Big VIP spenders');
    await user.click(screen.getByRole('checkbox', { name: /share/i }));
    await user.click(screen.getByRole('button', { name: /save view/i }));

    await waitFor(() => expect(api.createView).toHaveBeenCalledWith({
      list: 'contacts', name: 'Big VIP spenders', shared: true,
      query: { tag: ['t-vip'], field: ['spend:gte:1000'] }, columns: ['name', 'email'],
    }));
  });
});

describe('the tag and field columns of a list', () => {
  function Table({ ids }: { ids: string[] }) {
    const view = fakeView();
    view.columns['cf:tier'] = true;
    const columns = useFlexColumns('contact', ids.map((id) => ({ id })), (r) => r.id, view);
    return (
      <table>
        <tbody>
          {ids.map((id) => (
            <tr key={id}>{columns.map((c) => <td key={c.id} data-column={c.id}>{c.cell({ id })}</td>)}</tr>
          ))}
        </tbody>
      </table>
    );
  }

  it("read the page's values and tags in one batch each and show them", async () => {
    render(withQueries(<Table ids={['c1', 'c2']} />));
    expect(await screen.findByText('Gold')).toBeInTheDocument();
    expect(await screen.findByText('VIP')).toBeInTheDocument();
    expect(screen.getByText('Lapsed')).toBeInTheDocument();
    expect(api.values).toHaveBeenCalledTimes(1);
    expect(api.values).toHaveBeenCalledWith('contact', ['c1', 'c2']);
    expect(api.recordTags).toHaveBeenCalledTimes(1);
  });

  it('write an amount as every other amount in the workspace is written, not as the export does', async () => {
    function Spend() {
      const view = fakeView();
      view.columns['cf:spend'] = true;
      const columns = useFlexColumns('contact', [{ id: 'c1' }], (r) => r.id, view);
      const spend = columns.find((c) => c.id === 'cf:spend');
      return <output aria-label="spend">{spend?.cell({ id: 'c1' })}</output>;
    }
    render(withQueries(<Spend />));
    // The workspace writes money its own way (de-CH here), whatever separators this Node's ICU uses.
    await waitFor(() => expect(screen.getByLabelText('spend').textContent).toBe(formatMoney(1200, 'CHF', { locale: 'de-CH' })));
    expect(screen.getByLabelText('spend').textContent).not.toBe('1200.00 CHF');
  });
});

describe('tagging many records at once', () => {
  it('puts the tags picked on and takes the others off, and says what changed', async () => {
    const user = userEvent.setup();
    render(withQueries(<BulkTagButton entityType="contact" recordIds={['c1', 'c2']} what="the 2 contacts shown" />));
    await user.click(await screen.findByRole('button', { name: 'Tag the 2 contacts shown' }));
    await user.click(within(screen.getByRole('group', { name: 'Change VIP' })).getByRole('button', { name: 'Put on' }));
    await user.click(within(screen.getByRole('group', { name: 'Change Lapsed' })).getByRole('button', { name: 'Take off' }));
    await user.click(screen.getByRole('button', { name: 'Change tags' }));

    await waitFor(() => expect(api.bulk).toHaveBeenCalledWith({ entityType: 'contact', recordIds: ['c1', 'c2'], add: [VIP.id], remove: [LAPSED.id] }));
    expect(toast.success).toHaveBeenCalledWith('2 records changed: 2 tags put on, 1 taken off');
  });

  it('is not offered to whoever may not change those records', async () => {
    as([PERMISSION.EditRecords]);
    render(withQueries(<BulkTagButton entityType="member" recordIds={['m1']} what="the member shown" />));
    await waitFor(() => expect(api.tags).toHaveBeenCalled());
    expect(screen.queryByRole('button', { name: /tag/i })).not.toBeInTheDocument();
  });
});

describe("an export's tag and field columns", () => {
  it('add one column per field and one of tags, filled for each record', async () => {
    const columns = await flexExportColumns('contact', ['c1', 'c2']);
    expect(columns.headers).toEqual(['Tier', 'Spend', 'Tags']);
    expect(columns.cells('c1')).toEqual(['Gold', '1200.00 CHF', 'VIP']);
    expect(columns.cells('c2')).toEqual(['', '', 'Lapsed']);
  });
});
