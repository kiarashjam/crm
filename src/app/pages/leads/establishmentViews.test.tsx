// An establishment on the leads gallery and on its lead page: its type for a picture,
// what and where it is, and the person to speak to there. A person's card is as before.
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Lead } from '@/app/api/types';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

import { LeadGalleryCard } from './LeadGalleryCard';
import type { useLeadStatusSync } from './useLeadStatusSync';
import { ContactPersonDetails, EstablishmentDetails } from './detail/EstablishmentCards';

const trattoria: Lead = {
  id: 'l2',
  kind: 'establishment',
  name: 'Trattoria Bella',
  email: '',
  phone: '',
  status: 'New',
  establishmentType: 'restaurant',
  addressLine: 'Rue du Lac 4',
  postalCode: '1003',
  city: 'Lausanne',
  country: 'Switzerland',
  website: 'trattoria.ch',
  seats: 60,
  contactPersonName: 'Marco Rossi',
  contactPersonRole: 'Owner',
  contactPersonEmail: 'marco@trattoria.ch',
  contactPersonPhone: '+41 79 000 00 00',
};

function card(lead: Lead, company: string | null = null) {
  render(
    <MemoryRouter>
      <LeadGalleryCard
        lead={lead}
        index={0}
        company={company}
        assignee={null}
        members={[]}
        interactions={[]}
        readOnly={false}
        selected={false}
        selecting={false}
        statusesUnavailable={false}
        statusSync={{} as ReturnType<typeof useLeadStatusSync>}
        onOpen={vi.fn()}
        onToggleSelected={vi.fn()}
        onAssign={vi.fn()}
        onQuickLog={vi.fn(async () => {})}
        onSaved={vi.fn()}
      />
    </MemoryRouter>,
  );
}

describe('an establishment in the leads gallery', () => {
  it('says what and where it is, who to speak to, and reaches them', () => {
    card(trattoria, 'Trattoria Bella');

    expect(screen.getByRole('button', { name: 'Open Trattoria Bella' })).toBeInTheDocument();
    expect(screen.getByText('Restaurant · Lausanne')).toBeInTheDocument();
    expect(screen.getByText('Marco Rossi · Owner')).toBeInTheDocument();
    // No email or number of its own: the contact person's.
    expect(screen.getByRole('link', { name: 'marco@trattoria.ch' })).toHaveAttribute('href', 'mailto:marco@trattoria.ch');
    expect(screen.getByRole('link', { name: '+41 79 000 00 00' })).toHaveAttribute('href', 'tel:+41 79 000 00 00');
    // Not initials ("TB"): the type's picture.
    expect(screen.queryByText('TB')).not.toBeInTheDocument();
  });

  it('leaves a person’s card as it was', () => {
    card({ id: 'l1', name: 'Ada Lovelace', email: 'ada@example.ch', status: 'New', jobTitle: 'Engineer' }, 'Analytical Engines');

    expect(screen.getByText('AL')).toBeInTheDocument();
    expect(screen.getByText('Engineer · Analytical Engines')).toBeInTheDocument();
    expect(screen.queryByText(/Restaurant/)).not.toBeInTheDocument();
  });
});

describe('an establishment on its lead page', () => {
  it('shows its address with a map, its website, seats and type, each editable in place', () => {
    const patch = vi.fn(async () => true);
    render(<EstablishmentDetails lead={trattoria} readOnly={false} patch={patch} />);

    expect(screen.getByRole('link', { name: /Open Rue du Lac 4, 1003 Lausanne, Switzerland in Google Maps/ }))
      .toHaveAttribute('href', expect.stringContaining('https://www.google.com/maps/search/?api=1&query=Trattoria%20Bella'));
    expect(screen.getByRole('link', { name: 'trattoria.ch' })).toHaveAttribute('href', 'https://trattoria.ch');
    expect(screen.getByText('60')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Type of establishment' })).toHaveTextContent('Restaurant');
    expect(screen.getByText('No email of its own')).toBeInTheDocument();
  });

  it('saves an emptied field as null, which takes it off', async () => {
    const patch = vi.fn(async () => true);
    render(<EstablishmentDetails lead={trattoria} readOnly={false} patch={patch} />);

    fireEvent.click(screen.getByText('Lausanne'));
    const input = screen.getByDisplayValue('Lausanne');
    fireEvent.change(input, { target: { value: '' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await vi.waitFor(() => expect(patch).toHaveBeenCalledWith({ city: null }, { subject: 'City updated' }));
  });

  it('refuses seats that are not a whole number', async () => {
    const patch = vi.fn(async () => true);
    render(<EstablishmentDetails lead={trattoria} readOnly={false} patch={patch} />);

    fireEvent.click(screen.getByText('60'));
    const input = screen.getByDisplayValue('60');
    fireEvent.change(input, { target: { value: '6.5' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await new Promise((r) => setTimeout(r, 0));
    expect(patch).not.toHaveBeenCalled();
  });

  it('shows the contact person', () => {
    render(<ContactPersonDetails lead={trattoria} readOnly patch={vi.fn()} />);
    expect(screen.getByText('Marco Rossi')).toBeInTheDocument();
    expect(screen.getByText('Owner')).toBeInTheDocument();
    expect(screen.getByText('marco@trattoria.ch')).toBeInTheDocument();
  });
});
