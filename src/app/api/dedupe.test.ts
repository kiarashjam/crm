import { describe, it, expect } from 'vitest';
import {
  duplicateContactGroups, duplicateCompanyGroups, duplicateDealGroups, duplicateLeadGroups, duplicateMemberGroups,
  mergeRecords, suggestKeep, normalizePhone, normalizeDomain, normalizeName,
  LEAD_MERGE_FIELDS, DEAL_MERGE_FIELDS, MEMBER_MERGE_FIELDS,
} from './dedupe';
import type { Deal, Lead } from './types';
import type { Member } from './members';

const lead = (id: string, over: Partial<Lead> = {}): Lead => ({ id, name: 'Ada Lovelace', email: '', status: 'New', ...over });

describe('finding duplicates', () => {
  it('normalises what people type differently', () => {
    expect(normalizeName('  Zoë   MARTIN ')).toBe('zoe martin');
    expect(normalizePhone('+41 79 123 45 67')).toBe(normalizePhone('079 123 45 67'));
    expect(normalizePhone('123')).toBe('');
    expect(normalizeDomain('https://www.Acme.ch/about?x=1')).toBe('acme.ch');
  });

  it('groups people with the same email, ignoring case and spaces', () => {
    const groups = duplicateContactGroups([
      { id: 'a', name: 'Ada', email: 'Ada@Example.com ' },
      { id: 'b', name: 'A. Lovelace', email: 'ada@example.com' },
      { id: 'c', name: 'Grace', email: 'grace@example.com' },
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0]?.records.map((r) => r.id)).toEqual(['a', 'b']);
    expect(groups[0]?.reason).toBe('Same email');
  });

  it('does not offer two different people who only share a name', () => {
    expect(duplicateLeadGroups([
      lead('a', { email: 'john@one.com' }),
      lead('b', { email: 'john@two.com' }),
    ])).toEqual([]);
    expect(duplicateLeadGroups([
      lead('a', { phone: '079 111 22 33' }),
      lead('b', { phone: '079 999 88 77' }),
    ])).toEqual([]);
    expect(duplicateLeadGroups([
      lead('a', { companyId: 'acme' }),
      lead('b', { companyId: 'globex' }),
    ])).toEqual([]);
  });

  it('groups a shared name when nothing known disagrees', () => {
    const groups = duplicateLeadGroups([
      lead('a', { email: 'ada@example.com' }),
      lead('b', { phone: '+41 79 123 45 67' }),
      lead('c', { name: 'ada  lovelace', phone: '0791234567' }),
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0]?.records.map((r) => r.id).sort()).toEqual(['a', 'b', 'c']);
  });

  it('chains matches into one group, and puts each record in one group only', () => {
    const groups = duplicateContactGroups([
      { id: 'a', name: 'Sam Lee', email: 'sam@x.com' },
      { id: 'b', name: 'Samuel Lee', email: 'sam@x.com' },
      { id: 'c', name: 'Samuel Lee', email: '' },
      { id: 'a', name: 'Sam Lee', email: 'sam@x.com' },
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0]?.records.map((r) => r.id).sort()).toEqual(['a', 'b', 'c']);
    expect(groups[0]?.reason).toBe('Same email · Same name');
    expect(groups[0]?.key).toBe('a|b|c');
  });

  it('matches companies by domain or by name with no other domain', () => {
    const groups = duplicateCompanyGroups([
      { id: '1', name: 'Acme', website: 'https://www.acme.ch' },
      { id: '2', name: 'ACME SA', domain: 'acme.ch' },
      { id: '3', name: 'Globex', domain: 'globex.com' },
      { id: '4', name: 'Globex', domain: 'globex.ch' },
    ]);
    expect(groups.map((g) => g.records.map((r) => r.id))).toEqual([['1', '2']]);
  });

  it('matches deals by name for the same company and outcome only', () => {
    const deal = (id: string, over: Partial<Deal> = {}): Deal => ({ id, name: 'Renewal', value: '100', ...over });
    expect(duplicateDealGroups([deal('a', { companyId: 'acme' }), deal('b', { companyId: 'acme' })])).toHaveLength(1);
    expect(duplicateDealGroups([deal('a', { companyId: 'acme' }), deal('b', { companyId: 'globex' })])).toEqual([]);
    expect(duplicateDealGroups([deal('a', { isWon: true }), deal('b')])).toEqual([]);
  });

  it('matches members by email or full name', () => {
    const member = (id: string, over: Partial<Member>): Member => ({
      id, firstName: 'Amelia', lastName: 'Hartwell', email: '', tier: 'Gold', status: 'Active', joinedAtUtc: '', renewsAtUtc: '',
      duesAmount: 0, duesFrequency: 'Monthly', houseAccountBalance: 0, ...over,
    });
    expect(duplicateMemberGroups([member('a', { email: 'amelia@x.com' }), member('b', {})])).toHaveLength(1);
    expect(duplicateMemberGroups([member('a', { email: 'a@x.com' }), member('b', { email: 'b@x.com' })])).toEqual([]);
  });
});

describe('what the merged record looks like', () => {
  it('keeps the kept record’s values and fills only what it is missing, in order', () => {
    const keep = lead('k', { status: 'Qualified', phone: '' });
    const first = lead('1', { status: 'New', phone: '+41 1', jobTitle: 'CTO' });
    const second = lead('2', { phone: '+41 2', jobTitle: 'CEO', country: 'CH' });
    const { merged, rows } = mergeRecords(LEAD_MERGE_FIELDS, keep, [first, second]);
    expect(merged.status).toBe('Qualified');
    expect(merged.phone).toBe('+41 1');
    expect(merged.jobTitle).toBe('CTO');
    expect(merged.country).toBe('CH');
    const phone = rows.find((r) => r.label === 'Phone');
    expect(phone).toMatchObject({ result: '+41 1', from: 1, changed: true, values: [undefined, '+41 1', '+41 2'] });
    expect(rows.find((r) => r.label === 'Status')).toMatchObject({ result: 'Qualified', from: 0, changed: false });
  });

  it('adds up counts and takes the earliest first-seen date', () => {
    const { merged } = mergeRecords(LEAD_MERGE_FIELDS,
      lead('k', { emailOpenCount: 2, createdAtUtc: '2026-05-01T00:00:00Z' }),
      [lead('d', { emailOpenCount: 3, createdAtUtc: '2025-01-01T00:00:00Z' })]);
    expect(merged.emailOpenCount).toBe(5);
    expect(merged.createdAtUtc).toBe('2025-01-01T00:00:00Z');
  });

  it('fills a deal value of zero with its currency, and a private duplicate keeps it private', () => {
    const { merged, rows } = mergeRecords(DEAL_MERGE_FIELDS,
      { id: 'k', name: 'Renewal', value: '0' },
      [{ id: 'd', name: 'Renewal', value: '12000', currency: 'CHF', isPrivate: true }]);
    expect(merged.value).toBe('12000');
    expect(merged.currency).toBe('CHF');
    expect(merged.isPrivate).toBe(true);
    expect(rows.find((r) => r.label === 'Value')?.result).toBe(`CHF ${(12000).toLocaleString()}`);
  });

  it('adds up house accounts and keeps both members’ notes', () => {
    const member = (id: string, over: Partial<Member>): Member => ({
      id, firstName: 'A', lastName: 'H', email: 'a@x.com', tier: 'Gold', status: 'Active', joinedAtUtc: '2026-01-01T00:00:00Z',
      renewsAtUtc: '2027-01-01T00:00:00Z', duesAmount: 10, duesFrequency: 'Monthly', houseAccountBalance: 0, ...over,
    });
    const { merged } = mergeRecords(MEMBER_MERGE_FIELDS,
      member('k', { houseAccountBalance: 100, notes: 'Booth 7' }),
      [member('d', { houseAccountBalance: 40.5, notes: 'Allergic to nuts', joinedAtUtc: '2024-01-01T00:00:00Z' })]);
    expect(merged.houseAccountBalance).toBe(140.5);
    expect(merged.notes).toBe('Booth 7\n\nAllergic to nuts');
    expect(merged.joinedAtUtc).toBe('2024-01-01T00:00:00Z');
  });

  it('suggests keeping the most complete record, then the oldest', () => {
    expect(suggestKeep(LEAD_MERGE_FIELDS, [
      lead('sparse', { createdAtUtc: '2020-01-01T00:00:00Z' }),
      lead('full', { phone: '1234567', jobTitle: 'CTO', createdAtUtc: '2026-01-01T00:00:00Z' }),
    ], (l) => l.createdAtUtc)).toBe('full');
    expect(suggestKeep(LEAD_MERGE_FIELDS, [
      lead('new', { createdAtUtc: '2026-01-01T00:00:00Z' }),
      lead('old', { createdAtUtc: '2020-01-01T00:00:00Z' }),
    ], (l) => l.createdAtUtc)).toBe('old');
  });
});

describe('households', () => {
  it('match by head of household or name, and the preview lists everyone once', async () => {
    const { duplicateHouseholdGroups, HOUSEHOLD_MERGE_FIELDS } = await import('./dedupe');
    const h = (id: string, over: Record<string, unknown>) => ({ id, name: 'The Hartwells', primaryMemberId: 'm1', createdAtUtc: '', memberNames: [] as string[], ...over });
    expect(duplicateHouseholdGroups([h('1', {}), h('2', { name: 'Hartwell family' })])).toHaveLength(1);
    expect(duplicateHouseholdGroups([h('1', {}), h('2', { primaryMemberId: 'm2' })])[0]?.reason).toBe('Same name');
    expect(duplicateHouseholdGroups([h('1', {}), h('2', { name: 'Okafors', primaryMemberId: 'm2' })])).toEqual([]);
    const { merged } = mergeRecords(HOUSEHOLD_MERGE_FIELDS,
      h('1', { memberNames: ['Amelia', 'Tom'] }),
      [h('2', { memberNames: ['Tom', 'Lily'] })]);
    expect(merged.memberNames).toEqual(['Amelia', 'Tom', 'Lily']);
  });
});
