// The audit log shows the privacy lines as what they are: a person's data downloaded, a person
// erased (named by their pseudonym, never their name), a retention run, and older lines whose
// name the retention settings removed.

import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { AuditEvent } from '@/app/api';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const LINES: AuditEvent[] = [
  {
    id: 'a1', entityType: 'privacy', entityId: undefined, entityLabel: 'Erased member 7', action: 'erased',
    summary: 'A person was erased at their request (3 records, erasure 7)', actorName: 'Owner', createdAtUtc: new Date().toISOString(),
  },
  {
    id: 'a2', entityType: 'privacy', entityId: 'l1', entityLabel: null, action: 'exported',
    summary: 'Everything held about this person downloaded (12 items from 2 records)', actorName: 'Owner', createdAtUtc: new Date().toISOString(),
  },
  {
    id: 'a3', entityType: 'lead', entityId: 'l2', entityLabel: null, action: 'updated',
    summary: 'Status changed', actorName: 'Rep', createdAtUtc: new Date().toISOString(),
  },
];

vi.mock('@/app/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api')>()),
  getAuditLog: async () => LINES,
}));

const { default: AuditLog } = await import('./AuditLog');

describe('the audit log', () => {
  it('shows the privacy lines, and names a line whose name was removed by what it is about', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><AuditLog /></MemoryRouter>);

    expect(await screen.findByText('Erased member 7')).toBeInTheDocument();
    expect(screen.getByText('a person')).toBeInTheDocument();
    expect(screen.getByText('a lead')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Privacy' }));
    expect(screen.getByText('Erased member 7')).toBeInTheDocument();
    expect(screen.queryByText('a lead')).toBeNull();
  });
});
