import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Contact } from '@/app/api/types';
import type { ContactRelationship } from '@/app/api/relationships';

const api = vi.hoisted(() => ({
  getRelationships: vi.fn(),
  addRelationship: vi.fn(),
  updateRelationship: vi.fn(),
  removeRelationship: vi.fn(),
}));

vi.mock('@/app/api/relationships', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/relationships')>()),
  ...api,
}));
vi.mock('@/app/api/contacts', () => ({ getContacts: vi.fn(async () => []) }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import RelationshipsCard from './RelationshipsCard';

beforeAll(() => {
  // The searchable picker scrolls its highlighted row into view; jsdom has no layout.
  Element.prototype.scrollIntoView = vi.fn();
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
});

const contacts: Contact[] = [
  { id: 'c-claire', name: 'Claire Dubois', email: 'claire@example.ch', convertedFromLeadId: 'l1' },
  { id: 'c-marc', name: 'Marc Favre', email: 'marc@example.ch' },
  { id: 'c-gone', name: 'Gone Person', email: 'gone@example.ch', isArchived: true },
  { id: 'c-anne', name: 'Anne Martin', email: 'anne@example.ch' },
];

const spouse: ContactRelationship = {
  id: 'r1',
  type: 'spouse',
  relatedContactId: 'c-marc',
  relatedContactName: 'Marc Favre',
  relatedContactEmail: 'marc@example.ch',
  relatedContactIsArchived: false,
  relatedLeadId: 'l9',
  createdAtUtc: '2026-10-01T00:00:00Z',
};

function show(props: Partial<Parameters<typeof RelationshipsCard>[0]> = {}) {
  const onChanged = vi.fn();
  render(
    <MemoryRouter>
      <RelationshipsCard owner={{ leadId: 'l1' }} personName="Claire Dubois" contacts={contacts} readOnly={false} onChanged={onChanged} {...props} />
    </MemoryRouter>,
  );
  return onChanged;
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('RelationshipsCard', () => {
  it('shows the spouse as a link to their lead, marked as the couple', async () => {
    api.getRelationships.mockResolvedValue({ contactId: 'c-claire', items: [spouse] });
    show();
    const link = await screen.findByRole('link', { name: /Marc Favre/ });
    expect(link.getAttribute('href')).toBe('/leads/l9');
    expect(screen.getByText('Couple')).toBeTruthy();
  });

  it('adds a spouse from the picker, which leaves out archived contacts and the person themselves', async () => {
    api.getRelationships.mockResolvedValue({ contactId: 'c-claire', items: [] });
    api.addRelationship.mockResolvedValue(spouse);
    const onChanged = show();
    fireEvent.click(await screen.findByRole('button', { name: "Add Claire's spouse or a relative" }));
    expect(await screen.findByText('Marc Favre')).toBeTruthy();
    expect(screen.queryByText('Gone Person')).toBeNull();
    expect(screen.queryAllByText('Claire Dubois')).toHaveLength(0);
    fireEvent.click(screen.getByText('Marc Favre'));
    await waitFor(() => expect(api.addRelationship).toHaveBeenCalledWith({ leadId: 'l1' }, 'c-marc', 'spouse'));
    expect(onChanged).toHaveBeenCalledWith("Marc Favre added as Claire's spouse");
    expect(await screen.findByText('Couple')).toBeTruthy();
  });

  it('removes a relationship', async () => {
    api.getRelationships.mockResolvedValue({ contactId: 'c-claire', items: [spouse] });
    api.removeRelationship.mockResolvedValue(undefined);
    show();
    fireEvent.click(await screen.findByRole('button', { name: "Remove Marc Favre as Claire's spouse" }));
    await waitFor(() => expect(api.removeRelationship).toHaveBeenCalledWith({ leadId: 'l1' }, 'r1'));
    await waitFor(() => expect(screen.queryByText('Marc Favre')).toBeNull());
  });

  it('is read-only for a viewer', async () => {
    api.getRelationships.mockResolvedValue({ contactId: 'c-claire', items: [spouse] });
    show({ readOnly: true });
    expect(await screen.findByText('Spouse')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Remove/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Add/ })).toBeNull();
  });

  it('asks for a name and an email when the lead has no contact yet', async () => {
    api.getRelationships.mockResolvedValue({ contactId: null, items: [] });
    show();
    expect(await screen.findByText(/Give Claire a name and an email/)).toBeTruthy();
  });
});
