// Custom fields, tags and saved views as the pages talk to the server: lists' conditions in
// the address, batch reads in slices the server takes, only changed values sent, and reads
// that are POSTs not announced as writes.
import { beforeEach, describe, expect, it, vi } from 'vitest';

const client = { json: vi.fn() };
vi.mock('./apiClient', async () => {
  const actual = await vi.importActual<typeof import('./apiClient')>('./apiClient');
  return {
    ...actual,
    isUsingRealApi: () => true,
    servesDemoData: () => false,
    apiWithFallback: <T,>(real: () => Promise<T>) => real(),
    authFetchJson: (...a: unknown[]) => client.json(...a),
  };
});

const customFields = await import('./customFields');
const tags = await import('./tags');
const flex = await import('./flexFilters');
const views = await import('./savedViews');
const { changesRecords, resourcesChangedBy } = await import('./dataChanged');

beforeEach(() => {
  client.json.mockReset();
});

describe("a list's conditions on tags and fields", () => {
  it('go into the query string under the names the server reads, and come back out of the address', () => {
    const filters = { tags: ['t1', 't2'], tagMatch: 'all' as const, notTags: ['t3'], fields: ['tier:is:Gold', 'budget:gte:1000'] };
    const query = flex.appendFlexFilters(new URLSearchParams('search=ana'), filters);

    expect(query.getAll('tag')).toEqual(['t1', 't2']);
    expect(query.get('tagMatch')).toBe('all');
    expect(query.getAll('notTag')).toEqual(['t3']);
    expect(query.getAll('field')).toEqual(['tier:is:Gold', 'budget:gte:1000']);
    expect(flex.readFlexParams(query)).toEqual(filters);
  });

  it('match any tag unless all is asked, and only ask for all with more than one tag', () => {
    expect(flex.appendFlexFilters(new URLSearchParams(), { tags: ['t1'], tagMatch: 'all' }).has('tagMatch')).toBe(false);
    expect(flex.readFlexParams(new URLSearchParams('tag=t1&tag=t2'))).toEqual({ tags: ['t1', 't2'] });
  });

  it('replace the ones in the address and go back to the first page', () => {
    const next = flex.writeFlexParams(new URLSearchParams('page=3&tag=old&field=x:empty&status=archived'), { tags: ['new'] });
    expect(next.getAll('tag')).toEqual(['new']);
    expect(next.has('field')).toBe(false);
    expect(next.get('page')).toBe('1');
    expect(next.get('status')).toBe('archived');
  });

  it('are written and read back as the server parses them, and said in words', () => {
    expect(flex.fieldCondition('budget', 'between', ['100', '500'])).toBe('budget:between:100..500');
    expect(flex.fieldCondition('interests', 'any', ['Golf', 'Wine'])).toBe('interests:any:Golf|Wine');
    expect(flex.fieldCondition('vip', 'true')).toBe('vip:true');
    expect(flex.parseFieldCondition('budget:between:100..500')).toMatchObject({ key: 'budget', op: 'between', value: '100', to: '500' });
    expect(flex.parseFieldCondition('site:contains:https://x.ch')).toMatchObject({ op: 'contains', value: 'https://x.ch' });
    const fields = [{ id: 'f', entityType: 'contact', key: 'budget', label: 'Budget', type: 'money', order: 0 }] as const;
    expect(flex.describeCondition('budget:between:100..500', [...fields])).toBe('Budget is between 100 and 500');
    expect(flex.opsFor('checkbox').map((o) => o.op)).toEqual(['true', 'false']);
  });
});

describe('batch reads for a list', () => {
  it('ask the server for at most 500 records at a time and put the answers together', async () => {
    const ids = Array.from({ length: 1200 }, (_, i) => `r${i}`);
    client.json.mockImplementation((_path: string, init: RequestInit) => {
      const asked = JSON.parse(init.body as string) as { recordIds: string[] };
      return Promise.resolve(Object.fromEntries(asked.recordIds.slice(0, 1).map((id) => [id, { tier: 'Gold' }])));
    });

    const values = await customFields.getFieldValuesBatch('contact', ids);

    expect(client.json).toHaveBeenCalledTimes(3);
    expect(client.json.mock.calls.map((c) => (JSON.parse((c[1] as RequestInit).body as string) as { recordIds: string[] }).recordIds.length))
      .toEqual([500, 500, 200]);
    expect(Object.keys(values)).toEqual(['r0', 'r500', 'r1000']);
    expect(client.json.mock.calls[0]![0]).toBe('/api/custom-fields/values/batch');
  });

  it('read nothing for no records', async () => {
    expect(await tags.getRecordTagsBatch('deal', [])).toEqual({});
    expect(client.json).not.toHaveBeenCalled();
  });

  it('are reads: the app does not refresh every list after one, as it does after a write', () => {
    expect(changesRecords('POST', '/api/custom-fields/values/batch')).toBe(false);
    expect(changesRecords('POST', '/api/tags/records/batch')).toBe(false);
    expect(changesRecords('PUT', '/api/custom-fields/values')).toBe(true);
    expect(resourcesChangedBy('PUT', '/api/tags/records')).toEqual(expect.arrayContaining(['tags', 'contacts', 'members', 'deals']));
    expect(resourcesChangedBy('POST', '/api/custom-fields')).toEqual(expect.arrayContaining(['custom-fields', 'leads', 'automations']));
  });
});

describe('saving a record\'s values', () => {
  it('sends only the fields that changed, an emptied one as null', () => {
    const before = { tier: 'Gold', seats: 4, interests: ['Golf'], note: 'kept' };
    const after = { tier: 'Silver', seats: 4, interests: [], note: 'kept', joined: '2026-01-02' };
    expect(customFields.changedValues(before, after)).toEqual({ tier: 'Silver', interests: null, joined: '2026-01-02' });
  });

  it('writes a value as a list cell shows it', () => {
    expect(customFields.valueText({ type: 'money' }, { amount: 1200, currency: 'CHF' })).toBe('1200.00 CHF');
    expect(customFields.valueText({ type: 'multiselect' }, ['Golf', 'Wine'])).toBe('Golf, Wine');
    expect(customFields.valueText({ type: 'link' }, { id: 'c1', name: 'Banque Léman' })).toBe('Banque Léman');
    expect(customFields.valueText({ type: 'checkbox' }, false)).toBe('No');
    expect(customFields.valueText({ type: 'text' }, '  ')).toBe('');
  });
});

describe('a saved view', () => {
  it("keeps a list's filters and order, not its page", () => {
    const query = views.queryFromParams(new URLSearchParams('page=4&pageSize=50&status=archived&tag=t1&field=tier:is:Gold&sort=cf:spend-desc'));
    expect(query).toEqual({ status: 'archived', tag: ['t1'], field: ['tier:is:Gold'], sort: 'cf:spend-desc' });

    const params = views.paramsFromQuery(query, new URLSearchParams('pageSize=50&page=4'));
    expect(params.get('pageSize')).toBe('50');
    expect(params.has('page')).toBe(false);
    expect(params.getAll('tag')).toEqual(['t1']);
    expect(params.get('sort')).toBe('cf:spend-desc');
  });
});
