// Contacts and companies are imported on the page leads use, with their own words and rules.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { ImportAnalysis } from '@/app/api/imports';

const api = { analyzeImport: vi.fn(), previewImport: vi.fn(), listImports: vi.fn() };

vi.mock('@/app/api/imports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/imports')>()),
  analyzeImport: (...a: unknown[]) => api.analyzeImport(...a),
  previewImport: (...a: unknown[]) => api.previewImport(...a),
  listImports: (...a: unknown[]) => api.listImports(...a),
}));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('@/app/api', () => ({ getLeadStatuses: () => Promise.resolve([]), getLeadSources: () => Promise.resolve([]) }));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const ContactImport = (await import('../ContactImport')).default;
const CompanyImport = (await import('../CompanyImport')).default;
const { missingChoice } = await import('./ImportPage');

function analysis(kind: ImportAnalysis['kind'], field: string | null): ImportAnalysis {
  return {
    kind,
    separator: 'comma',
    encoding: 'UTF-8',
    rowCount: 1,
    columns: [{ index: 0, header: 'Name', samples: ['Acme SA'], field }],
    fields: [{ key: 'name', label: 'Name', required: true, custom: false }],
  };
}

async function open(page: JSX.Element) {
  const view = render(<MemoryRouter>{page}</MemoryRouter>);
  fireEvent.change(view.container.querySelector('input[type="file"]') as HTMLInputElement, {
    target: { files: [new File(['Name\nAcme SA\n'], 'list.csv', { type: 'text/csv' })] },
  });
  await screen.findByRole('heading', { name: 'What is in each column?' });
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  api.listImports.mockResolvedValue([]);
});

describe('importing contacts and companies', () => {
  it('imports contacts as contacts, with no lead automations to offer', async () => {
    api.analyzeImport.mockResolvedValue(analysis('contact', 'name'));

    await open(<ContactImport />);

    expect(api.analyzeImport).toHaveBeenCalledWith('contact', expect.anything());
    expect(screen.getByRole('heading', { name: 'Import contacts' })).toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: 'Run automations for these leads' })).not.toBeInTheDocument();
    // A shared phone alone is not a match (a couple share a landline); the check notes it.
    expect(screen.getByText(
      'A row matches by the same email, or the same name and phone number. Someone who only shares a phone number '
      + 'with a person here is added, with a note to check they are not the same person.',
    )).toBeInTheDocument();
  });

  it('needs the company name to import companies', async () => {
    api.analyzeImport.mockResolvedValue(analysis('company', null));
    const user = userEvent.setup();

    await open(<CompanyImport />);

    expect(screen.getByText('Choose the column that holds the company name.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Check the file' })).toBeDisabled();
    await user.selectOptions(screen.getByRole('combobox', { name: 'Import Name into' }), 'name');
    expect(screen.getByRole('button', { name: 'Check the file' })).toBeEnabled();
    expect(screen.getByText('A row matches by the same name, or the same domain.')).toBeInTheDocument();
  });
});

describe('what still has to be chosen', () => {
  it('asks people imports for an email, and a name only when they add people', () => {
    expect(missingChoice('contact', ['name', null], 'skip')).toBe('Choose the column that holds the email address.');
    expect(missingChoice('contact', ['email', null], 'skip')).toMatch(/holds the name/);
    expect(missingChoice('lead', ['email', 'lastName'], 'skip')).toBeNull();
    expect(missingChoice('lead', ['email', null], 'update')).toBeNull();
    expect(missingChoice('company', ['domain'], 'update')).toBe('Choose the column that holds the company name.');
  });

  it('needs no email column where leads may be establishments, as the server says', () => {
    const fields = [
      { key: 'name', label: 'Establishment name', required: true },
      { key: 'email', label: 'Email', required: false },
    ];
    expect(missingChoice('lead', ['name', null], 'skip', fields)).toBeNull();
    expect(missingChoice('lead', [null, null], 'skip', fields)).toBe('Choose the column that holds the establishment name.');
    expect(missingChoice('lead', ['email', null], 'skip', fields)).toBe('Choose the column that holds the establishment name.');
    expect(missingChoice('lead', ['email', null], 'update', fields)).toBeNull();
    // A people-only workspace's email is required, as before.
    expect(missingChoice('lead', ['name', null], 'skip', [{ key: 'email', label: 'Email', required: true }]))
      .toBe('Choose the column that holds the email address.');
  });
});
