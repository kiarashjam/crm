import { beforeAll, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Contact, Lead } from '@/app/api/types';

vi.mock('@/app/api/leads', () => ({ getLeadsReferredBy: vi.fn(async () => [{ id: 'l1' }, { id: 'l7' }]) }));

import { ReferralCard } from './ReferralCard';

beforeAll(() => {
  // The searchable picker scrolls its highlighted row into view; jsdom has no layout.
  Element.prototype.scrollIntoView = vi.fn();
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
});

const lead: Lead = { id: 'l1', name: 'Ada Lovelace', email: 'ada@example.ch', status: 'New' } as Lead;
const contacts: Contact[] = [
  { id: 'c-sam', name: 'Sam Lee', email: 'sam@example.ch', companyName: 'Pavillon', jobTitle: 'Member' },
  { id: 'c-gone', name: 'Gone Person', email: 'gone@example.ch', isArchived: true },
  { id: 'c-self', name: 'Ada Lovelace', email: 'ada@example.ch', convertedFromLeadId: 'l1' },
  { id: 'c-grace', name: 'Grace Hopper', email: 'grace@example.ch' },
];

function show(props: Partial<Parameters<typeof ReferralCard>[0]> = {}) {
  const onChange = vi.fn(async () => true);
  render(
    <MemoryRouter>
      <ReferralCard lead={lead} contacts={contacts} readOnly={false} onChange={onChange} {...props} />
    </MemoryRouter>,
  );
  return onChange;
}

describe('ReferralCard', () => {
  it('shows the referrer as a link to their contact, with how many they referred', async () => {
    show({ lead: { ...lead, referredByContactId: 'c-sam', referredByContactName: 'Sam Lee' } });
    const link = screen.getByRole('link', { name: /Sam Lee/ });
    expect(link.getAttribute('href')).toBe('/contacts/c-sam');
    expect(screen.getByText('Member · Pavillon')).toBeTruthy();
    expect(await screen.findByText('2 leads referred')).toBeTruthy();
  });

  it('adds a referrer from the picker, which leaves out archived contacts and the lead itself', async () => {
    const onChange = show();
    fireEvent.click(screen.getByRole('button', { name: 'Add who referred Ada' }));
    expect(await screen.findByText('Grace Hopper')).toBeTruthy();
    expect(screen.queryByText('Gone Person')).toBeNull();
    // The contact made from this lead is not offered as its own referrer.
    expect(screen.queryAllByText('Ada Lovelace')).toHaveLength(0);
    fireEvent.click(screen.getByText('Grace Hopper'));
    await waitFor(() => expect(onChange).toHaveBeenCalledWith('c-grace'));
  });

  it('clears the referrer', async () => {
    const onChange = show({ lead: { ...lead, referredByContactId: 'c-sam', referredByContactName: 'Sam Lee' } });
    fireEvent.click(screen.getByRole('button', { name: 'Remove Sam Lee as the referrer' }));
    await waitFor(() => expect(onChange).toHaveBeenCalledWith(null));
  });

  it('a read-only member sees the referrer and cannot change it', () => {
    show({ readOnly: true, lead: { ...lead, referredByContactId: 'c-sam', referredByContactName: 'Sam Lee' } });
    expect(screen.getByRole('link', { name: /Sam Lee/ })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Change|Remove/ })).toBeNull();
  });

  it('names a referrer that is not in the loaded contacts, from the lead itself', () => {
    show({ lead: { ...lead, referredByContactId: 'c-archived-elsewhere', referredByContactName: 'Old Friend' } });
    expect(screen.getByRole('link', { name: /Old Friend/ }).getAttribute('href')).toBe('/contacts/c-archived-elsewhere');
  });
});
