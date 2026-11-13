// The full lead editor follows what the workspace's leads are: a person's steps as they
// always were, an establishment's first step asking what and where it is, and in a
// workspace with both, the choice first.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import type { Lead } from '@/app/api/types';
import type { LeadForm } from './types';

const org = vi.hoisted(() => ({ current: { id: 'org-1', name: 'Workspace', leadKinds: undefined as string | undefined } }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => ({ currentOrg: org.current }) }));

const { AddLeadDialog } = await import('./AddLeadDialog');
const { EMPTY_LEAD_FORM } = await import('./config');

const latest: { form: LeadForm | null } = { form: null };

function Harness({ initial, editingLead = null }: { initial: LeadForm; editingLead?: Lead | null }) {
  const [form, setForm] = useState(initial);
  latest.form = form;
  return (
    <AddLeadDialog
      open
      onOpenChange={vi.fn()}
      editingLead={editingLead}
      form={form}
      setForm={setForm}
      companies={[]}
      contacts={[]}
      sourceOptions={[]}
      statusOptions={[{ id: 's1', name: 'New' }]}
      onSubmit={vi.fn()}
      saving={false}
    />
  );
}

beforeEach(() => {
  org.current = { id: 'org-1', name: 'Workspace', leadKinds: undefined };
  latest.form = null;
});

describe('the full lead editor', () => {
  it('is a person’s, as always, in a people-only workspace', () => {
    render(<Harness initial={EMPTY_LEAD_FORM} />);
    expect(screen.queryByRole('radiogroup', { name: 'What is this lead?' })).not.toBeInTheDocument();
    expect(screen.getByText('Step 1: Contact Info')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /continue/i })).toBeDisabled();
  });

  it('offers the choice first where the workspace has both, and asks an establishment for its place', async () => {
    org.current.leadKinds = 'both';
    render(<Harness initial={EMPTY_LEAD_FORM} />);

    fireEvent.click(screen.getByRole('radio', { name: /establishment/i }));
    expect(screen.getByText('Step 1: Establishment')).toBeInTheDocument();
    expect(screen.getByText('Place')).toBeInTheDocument();

    fireEvent.change(await screen.findByLabelText(/establishment name/i, undefined, { timeout: 3000 }), { target: { value: 'Trattoria Bella' } });
    fireEvent.change(screen.getByLabelText('Street and number'), { target: { value: 'Rue du Lac 4' } });
    // Only its name is required.
    expect(screen.getByRole('button', { name: /continue/i })).toBeEnabled();
    expect(latest.form).toMatchObject({ kind: 'establishment', name: 'Trattoria Bella', addressLine: 'Rue du Lac 4', email: '' });

    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    expect(screen.getByText('Step 2: Contact person & Source')).toBeInTheDocument();
    expect(screen.getByText('Contact person')).toBeInTheDocument();
  });

  it('starts a new lead as an establishment where every lead is one', () => {
    org.current.leadKinds = 'establishment';
    render(<Harness initial={EMPTY_LEAD_FORM} />);
    expect(screen.queryByRole('radiogroup', { name: 'What is this lead?' })).not.toBeInTheDocument();
    expect(latest.form?.kind).toBe('establishment');
    expect(screen.getByText('Step 1: Establishment')).toBeInTheDocument();
  });

  it('edits a saved person as a person, whatever the workspace now takes', () => {
    org.current.leadKinds = 'establishment';
    const ada: Lead = { id: 'l1', name: 'Ada Lovelace', email: 'ada@example.ch', status: 'New' };
    render(<Harness initial={{ ...EMPTY_LEAD_FORM, name: ada.name, email: ada.email }} editingLead={ada} />);
    expect(latest.form?.kind).toBe('person');
    expect(screen.getByText('Step 1: Contact Info')).toBeInTheDocument();
  });
});
