// The lead import: the server reads the file and says what every row would do before
// anything is written, and the import can be undone. Its rows are people the workspace
// already knows, so they must not set off what a brand-new inquiry does.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { ImportAnalysis, ImportResult } from '@/app/api/imports';

const api = {
  analyzeImport: vi.fn(),
  previewImport: vi.fn(),
  runImport: vi.fn(),
  listImports: vi.fn(),
  previewUndo: vi.fn(),
  undoImport: vi.fn(),
};
const demo = { on: false };

vi.mock('@/app/api/imports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/imports')>()),
  analyzeImport: (...a: unknown[]) => api.analyzeImport(...a),
  previewImport: (...a: unknown[]) => api.previewImport(...a),
  runImport: (...a: unknown[]) => api.runImport(...a),
  listImports: (...a: unknown[]) => api.listImports(...a),
  previewUndo: (...a: unknown[]) => api.previewUndo(...a),
  undoImport: (...a: unknown[]) => api.undoImport(...a),
}));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => demo.on,
}));
vi.mock('@/app/api', () => ({
  getLeadStatuses: () => Promise.resolve([{ id: 's1', organizationId: 'o', name: 'Contacted', displayOrder: 1 }]),
  getLeadSources: () => Promise.resolve([]),
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const LeadImport = (await import('./LeadImport')).default;

// What Excel writes in Switzerland: semicolons, and an umlaut.
const FILE_TEXT = 'Vorname;Nachname;E-Mail;Firma\nJürg;Meier;juerg@example.ch;Acme SA\n';

const analysis: ImportAnalysis = {
  kind: 'lead',
  separator: 'semicolon',
  encoding: 'UTF-8',
  rowCount: 3,
  columns: [
    { index: 0, header: 'Vorname', samples: ['Jürg'], field: 'firstName' },
    { index: 1, header: 'Nachname', samples: ['Meier'], field: 'lastName' },
    { index: 2, header: 'E-Mail', samples: ['juerg@example.ch'], field: 'email' },
    { index: 3, header: 'Firma', samples: ['Acme SA'], field: 'company' },
  ],
  fields: [
    { key: 'name', label: 'Full name', required: true, custom: false },
    { key: 'firstName', label: 'First name', required: false, custom: false },
    { key: 'lastName', label: 'Last name', required: false, custom: false },
    { key: 'email', label: 'Email', required: true, custom: false },
    { key: 'company', label: 'Company', required: false, custom: false },
    { key: 'custom:tier', label: 'Tier', required: false, custom: true },
  ],
};

const preview: ImportResult = {
  batchId: null,
  dryRun: true,
  kind: 'lead',
  totalRows: 3,
  created: 1,
  updated: 1,
  skipped: 1,
  failed: 0,
  rows: [
    { row: 2, action: 'create', label: 'Jürg Meier', message: null, changes: [], notes: ['Adds the company “Acme SA”'] },
    { row: 3, action: 'update', label: 'Anne Roth', message: 'Updates Anne Roth (same email)', changes: ['Phone: empty → “079 123 45 67”'], notes: [] },
    { row: 4, action: 'skip', label: 'Jean Dupont', message: 'Already in your leads: Jean Dupont (same email)', changes: [], notes: [] },
  ],
};

const imported: ImportResult = { ...preview, batchId: 'batch-1', dryRun: false };

async function upload() {
  const user = userEvent.setup();
  const view = render(<MemoryRouter><LeadImport /></MemoryRouter>);
  const file = new File([FILE_TEXT], 'waitlist.csv', { type: 'text/csv' });
  fireEvent.change(view.container.querySelector('input[type="file"]') as HTMLInputElement, { target: { files: [file] } });
  await screen.findByRole('heading', { name: 'What is in each column?' });
  return { user, view };
}

beforeEach(() => {
  demo.on = false;
  for (const fn of Object.values(api)) fn.mockReset();
  api.analyzeImport.mockResolvedValue(analysis);
  api.previewImport.mockResolvedValue(preview);
  api.runImport.mockResolvedValue(imported);
  api.listImports.mockResolvedValue([]);
});

describe('importing leads', () => {
  it('sends the file as its bytes, so the server can tell how they are encoded', async () => {
    await upload();

    expect(api.analyzeImport).toHaveBeenCalledWith('lead', expect.objectContaining({ fileName: 'waitlist.csv' }));
    const sent = api.analyzeImport.mock.calls[0]![1] as { content: string };
    const bytes = Uint8Array.from(atob(sent.content), (c) => c.charCodeAt(0));
    expect(new TextDecoder().decode(bytes)).toBe(FILE_TEXT);
    expect(screen.getByText(/3 rows, columns separated by semicolons, read as UTF-8/)).toBeInTheDocument();
  });

  it('checks the file with the suggested columns, quietly unless automations are asked for', async () => {
    const { user } = await upload();
    expect(screen.getByRole('combobox', { name: 'Import Firma into' })).toHaveValue('company');
    expect(screen.getByRole('checkbox', { name: 'Run automations for these leads' })).not.toBeChecked();

    await user.click(screen.getByRole('button', { name: 'Check the file' }));

    expect(api.previewImport).toHaveBeenCalledWith('lead', expect.objectContaining({ fileName: 'waitlist.csv' }), expect.objectContaining({
      columns: ['firstName', 'lastName', 'email', 'company'],
      duplicates: 'skip',
      runAutomations: false,
    }));
  });

  it('runs the automations when the person importing ticks the box', async () => {
    const { user } = await upload();
    await user.click(screen.getByRole('checkbox', { name: 'Run automations for these leads' }));
    await user.click(screen.getByRole('button', { name: 'Check the file' }));
    await user.click(await screen.findByRole('button', { name: 'Add 1 lead and update 1' }));

    await waitFor(() => expect(api.runImport).toHaveBeenCalled());
    expect(api.runImport.mock.calls[0]![2]).toEqual(expect.objectContaining({ runAutomations: true }));
  });

  it('shows what each row would do before anything is written', async () => {
    const { user } = await upload();
    await user.click(screen.getByRole('button', { name: 'Check the file' }));

    expect(await screen.findByText('Checked: nothing has been written yet')).toBeInTheDocument();
    expect(screen.getByText('Adds the company “Acme SA”')).toBeInTheDocument();
    expect(screen.getByText('Phone: empty → “079 123 45 67”')).toBeInTheDocument();
    expect(screen.getByText('Already in your leads: Jean Dupont (same email)')).toBeInTheDocument();
    expect(api.runImport).not.toHaveBeenCalled();

    // The tiles narrow the table to one kind of outcome.
    await user.click(screen.getByRole('button', { name: /To update/ }));
    expect(screen.queryByText('Jürg Meier')).not.toBeInTheDocument();
    expect(screen.getByText('Anne Roth')).toBeInTheDocument();
  });

  it('puts a field into one column only', async () => {
    const { user } = await upload();

    await user.selectOptions(screen.getByRole('combobox', { name: 'Import Firma into' }), 'email');

    expect(screen.getByRole('combobox', { name: 'Import Firma into' })).toHaveValue('email');
    // The field left the column it was in.
    expect(screen.getByRole('combobox', { name: 'Import E-Mail into' })).toHaveValue('');

    await user.selectOptions(screen.getByRole('combobox', { name: 'Import Firma into' }), '');
    expect(screen.getByText('Choose the column that holds the email address.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Check the file' })).toBeDisabled();
  });

  it('needs a name to add people, but not to update them', async () => {
    const { user } = await upload();
    await user.selectOptions(screen.getByRole('combobox', { name: 'Import Vorname into' }), '');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Import Nachname into' }), '');
    expect(screen.getByRole('button', { name: 'Check the file' })).toBeDisabled();

    await user.click(screen.getByRole('radio', { name: /Update them with the file's values/ }));

    expect(screen.getByRole('button', { name: 'Check the file' })).toBeEnabled();
    await user.click(screen.getByRole('button', { name: 'Check the file' }));
    expect(api.previewImport.mock.calls[0]![2]).toEqual(expect.objectContaining({ duplicates: 'update', columns: [null, null, 'email', 'company'] }));
  });

  it('can undo the import, saying first what would go and what stays', async () => {
    api.previewUndo.mockResolvedValue({
      batchId: 'batch-1', dryRun: true, leads: 1, contacts: 1, companies: 1,
      kept: [{ kind: 'lead', id: 'l2', label: 'Anne Roth', reason: 'has activities logged' }],
    });
    api.undoImport.mockResolvedValue({ batchId: 'batch-1', dryRun: false, leads: 1, contacts: 1, companies: 1, kept: [] });
    const { user } = await upload();
    await user.click(screen.getByRole('button', { name: 'Check the file' }));
    await user.click(await screen.findByRole('button', { name: 'Add 1 lead and update 1' }));
    await user.click(await screen.findByRole('button', { name: 'Undo this import' }));

    const dialog = await screen.findByRole('alertdialog');
    expect(await within(dialog).findByText('1 lead, the 1 contact made for them and 1 company')).toBeInTheDocument();
    expect(within(dialog).getByText(/has activities logged/)).toBeInTheDocument();
    expect(api.previewUndo).toHaveBeenCalledWith('batch-1');
    expect(api.undoImport).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: 'Undo import' }));
    await waitFor(() => expect(api.undoImport).toHaveBeenCalledWith('batch-1'));
  });

  it('says why a file cannot be read', async () => {
    api.analyzeImport.mockRejectedValue(new Error('Row 3 opens a quoted cell that never closes'));
    const view = render(<MemoryRouter><LeadImport /></MemoryRouter>);
    fireEvent.change(view.container.querySelector('input[type="file"]') as HTMLInputElement, {
      target: { files: [new File(['name\n"x\n'], 'broken.csv', { type: 'text/csv' })] },
    });

    expect(await screen.findByRole('alert')).toHaveTextContent('Row 3 opens a quoted cell that never closes');
  });

  it('is not faked in demo mode', async () => {
    demo.on = true;
    render(<MemoryRouter><LeadImport /></MemoryRouter>);

    expect(screen.getByText(/Importing needs a connected backend/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Choose a file' })).not.toBeInTheDocument();
    expect(api.listImports).not.toHaveBeenCalled();
  });

  it('lists past imports, with an undo only where the person may undo', async () => {
    api.listImports.mockResolvedValue([
      {
        id: 'b1', kind: 'lead', fileName: 'waitlist.csv', importedBy: 'Kia', startedAtUtc: '2026-10-01T08:00:00Z', finishedAtUtc: '2026-10-01T08:01:00Z',
        duplicates: 'skip', ranAutomations: false, totalRows: 800, created: 798, updated: 0, skipped: 2, failed: 0,
        undoneAtUtc: null, removed: 0, remaining: 1596, canUndo: true,
      },
      {
        id: 'b2', kind: 'lead', fileName: 'someone-elses.csv', importedBy: 'Anne', startedAtUtc: '2026-09-01T08:00:00Z', finishedAtUtc: '2026-09-01T08:01:00Z',
        duplicates: 'skip', ranAutomations: false, totalRows: 10, created: 10, updated: 0, skipped: 0, failed: 0,
        undoneAtUtc: null, removed: 0, remaining: 20, canUndo: false,
      },
    ]);
    render(<MemoryRouter><LeadImport /></MemoryRouter>);

    expect(await screen.findByText('waitlist.csv')).toBeInTheDocument();
    expect(api.listImports).toHaveBeenCalledWith('lead');
    expect(screen.getAllByRole('button', { name: 'Undo' })).toHaveLength(1);
  });
});
