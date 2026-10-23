import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

vi.mock('@/app/api/leads', () => ({ getLeadsReferredBy: vi.fn() }));

import { getLeadsReferredBy } from '@/app/api/leads';
import ReferredLeadsCard from './ReferredLeadsCard';

describe('ReferredLeadsCard', () => {
  it('links to each lead the contact referred', async () => {
    vi.mocked(getLeadsReferredBy).mockResolvedValue([
      { id: 'l-ada', name: 'Ada Lovelace', email: 'a@example.ch', status: 'Contacted', isConverted: false },
      { id: 'l-bea', name: 'Bea Smith', email: 'b@example.ch', status: 'Signed', isConverted: true },
    ] as never);
    render(<MemoryRouter><ReferredLeadsCard contactId="c-sam" contactName="Sam Lee" /></MemoryRouter>);
    const ada = await screen.findByRole('link', { name: /Ada Lovelace/ });
    expect(ada.getAttribute('href')).toBe('/leads/l-ada');
    expect(screen.getByRole('link', { name: /Bea Smith/ }).getAttribute('href')).toBe('/leads/l-bea');
    expect(screen.getByText('1 converted')).toBeTruthy();
    expect(getLeadsReferredBy).toHaveBeenCalledWith('c-sam');
  });

  it('says so when they have referred nobody', async () => {
    vi.mocked(getLeadsReferredBy).mockResolvedValue([]);
    render(<MemoryRouter><ReferredLeadsCard contactId="c-sam" contactName="Sam Lee" /></MemoryRouter>);
    expect(await screen.findByText('Sam has not referred anyone yet.')).toBeTruthy();
  });
});
