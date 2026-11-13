// Quick add asks what the workspace's leads are. People only (Pavillon 46, and every
// workspace that has not said otherwise) shows the dialog as it always was: no choice,
// no establishment. Establishments only asks for the place directly. Both asks first.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';

const org = vi.hoisted(() => ({ current: { id: 'org-1', name: 'Workspace', leadKinds: undefined as string | undefined } }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => ({ currentOrg: org.current }) }));

const { QuickAddLeadDialog } = await import('./QuickAddLeadDialog');

function show() {
  const onSubmit = vi.fn(async () => true);
  const onOpenFullEditor = vi.fn();
  render(
    <QuickAddLeadDialog
      open
      onOpenChange={vi.fn()}
      onSubmit={onSubmit}
      onOpenFullEditor={onOpenFullEditor}
      statusOptions={[{ id: 's1', name: 'New' }]}
      sourceOptions={[]}
      companies={[]}
    />,
  );
  return { onSubmit, onOpenFullEditor };
}

const addButton = () => screen.getByRole('button', { name: /create lead/i });

beforeEach(() => {
  org.current = { id: 'org-1', name: 'Workspace', leadKinds: undefined };
});

describe('quick add in a people-only workspace', () => {
  it('asks for a person as before, with no choice and nothing of an establishment', () => {
    const { onSubmit } = show();

    expect(screen.queryByRole('radiogroup', { name: 'What is this lead?' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/establishment name/i)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/^name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^email/i)).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: 'Ada Lovelace' } });
    expect(addButton()).toBeDisabled();
    fireEvent.change(screen.getByLabelText(/^email/i), { target: { value: 'ada@example.ch' } });
    fireEvent.click(addButton());

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ kind: 'person', name: 'Ada Lovelace', email: 'ada@example.ch' }));
  });

  it('reads an unknown setting as people only', () => {
    org.current.leadKinds = 'organisations';
    show();
    expect(screen.queryByRole('radiogroup', { name: 'What is this lead?' })).not.toBeInTheDocument();
  });
});

describe('quick add in a workspace with both', () => {
  it('asks first whether the lead is a person or an establishment', async () => {
    org.current.leadKinds = 'both';
    const { onSubmit } = show();

    const choice = screen.getByRole('radiogroup', { name: 'What is this lead?' });
    expect(screen.getByRole('radio', { name: /person/i })).toHaveAttribute('aria-checked', 'true');
    expect(choice).toBeInTheDocument();

    fireEvent.click(screen.getByRole('radio', { name: /establishment/i }));
    expect(screen.getByRole('radio', { name: /establishment/i })).toHaveAttribute('aria-checked', 'true');

    // An establishment needs its name only: no email.
    expect(addButton()).toBeDisabled();
    // The person's fields leave first, then the establishment's arrive.
    fireEvent.change(await screen.findByLabelText(/establishment name/i, undefined, { timeout: 3000 }), { target: { value: 'Trattoria Bella' } });
    fireEvent.change(screen.getByLabelText('City'), { target: { value: 'Lausanne' } });
    fireEvent.change(screen.getByLabelText('Contact person'), { target: { value: 'Marco Rossi' } });
    expect(addButton()).toBeEnabled();
    fireEvent.click(addButton());

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
      kind: 'establishment', name: 'Trattoria Bella', email: '', city: 'Lausanne', contactPersonName: 'Marco Rossi',
    }));
  });
});

describe('quick add in an establishments-only workspace', () => {
  it('asks for the establishment directly, and checks what was typed', () => {
    org.current.leadKinds = 'establishment';
    const { onSubmit, onOpenFullEditor } = show();

    expect(screen.queryByRole('radiogroup', { name: 'What is this lead?' })).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/establishment name/i), { target: { value: 'Café du Marché' } });
    fireEvent.change(screen.getByLabelText('Their email'), { target: { value: 'lea at cafe' } });

    expect(screen.getByRole('alert')).toHaveTextContent('The contact person’s email is not an email address.');
    expect(addButton()).toBeDisabled();

    fireEvent.change(screen.getByLabelText('Their email'), { target: { value: 'lea@cafedumarche.ch' } });
    fireEvent.click(screen.getByRole('button', { name: /full editor/i }));
    expect(onOpenFullEditor).toHaveBeenCalledWith(expect.objectContaining({
      kind: 'establishment', name: 'Café du Marché', contactPersonEmail: 'lea@cafedumarche.ch',
    }));
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
