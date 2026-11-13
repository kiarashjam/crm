// Establishments are matched as places: the same email, the same name in the same city,
// the same phone or the same website. A chain's branches share a name, so a name alone
// says nothing; and a person is never an establishment's duplicate.
import { describe, expect, it } from 'vitest';
import { duplicateLeadGroups, LEAD_MERGE_FIELDS, mergeRecords } from './dedupe';
import type { Lead } from './types';

const place = (id: string, over: Partial<Lead> = {}): Lead => ({
  id, kind: 'establishment', name: 'Trattoria Bella', email: '', status: 'New', ...over,
});

describe('duplicate establishments', () => {
  it('groups the same name in the same city, however it is written', () => {
    const groups = duplicateLeadGroups([
      place('a', { city: 'Lausanne' }),
      place('b', { name: 'trattoria  bella', city: ' lausanne' }),
      place('c', { city: 'Genève' }),
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0]?.records.map((r) => r.id)).toEqual(['a', 'b']);
    expect(groups[0]?.reason).toBe('Same name and city');
  });

  it('does not group a name alone: branches in two cities, or a city not given', () => {
    expect(duplicateLeadGroups([place('a', { city: 'Lausanne' }), place('b', { city: 'Genève' })])).toEqual([]);
    expect(duplicateLeadGroups([place('a'), place('b')])).toEqual([]);
  });

  it('groups the same email, phone or website, and leaves an empty email out of it', () => {
    const groups = duplicateLeadGroups([
      place('a', { name: 'Bella', email: 'info@bella.ch' }),
      place('b', { name: 'Trattoria Bella SA', email: 'INFO@bella.ch' }),
      place('c', { name: 'Café du Marché', phone: '+41 21 000 00 00' }),
      place('d', { name: 'Cafe du Marche Genève', phone: '021 000 00 00' }),
      place('e', { name: 'Hotel Lac', website: 'https://www.hotel-lac.ch/' }),
      place('f', { name: 'Hôtel du Lac', website: 'hotel-lac.ch' }),
      place('g', { name: 'Bakery One' }),
      place('h', { name: 'Bakery Two' }),
    ]);
    expect(groups.map((g) => [g.records.map((r) => r.id).join(','), g.reason]).sort()).toEqual([
      ['a,b', 'Same email'],
      ['c,d', 'Same phone'],
      ['e,f', 'Same website'],
    ]);
  });

  it('never makes a person an establishment’s duplicate', () => {
    const groups = duplicateLeadGroups([
      place('a', { email: 'marco@trattoria.ch' }),
      { id: 'b', kind: 'person', name: 'Marco Rossi', email: 'marco@trattoria.ch', status: 'New' },
    ]);
    expect(groups).toEqual([]);
  });

  it('still groups people as people, with no kind given', () => {
    const groups = duplicateLeadGroups([
      { id: 'a', name: 'Ada Lovelace', email: 'ada@example.ch', status: 'New' },
      { id: 'b', name: 'A. Lovelace', email: 'ADA@example.ch', status: 'New' },
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0]?.reason).toBe('Same email');
  });

  it('fills an establishment’s missing fields from its duplicate when merged', () => {
    const { merged, rows } = mergeRecords(
      LEAD_MERGE_FIELDS,
      place('a', { city: 'Lausanne' }),
      [place('b', { city: 'Lausanne', addressLine: 'Rue du Lac 4', contactPersonName: 'Marco Rossi', seats: 60 })],
    );
    expect(merged).toMatchObject({ addressLine: 'Rue du Lac 4', contactPersonName: 'Marco Rossi', seats: 60 });
    expect(rows.find((r) => r.label === 'Seats')?.result).toBe('60');
  });
});
