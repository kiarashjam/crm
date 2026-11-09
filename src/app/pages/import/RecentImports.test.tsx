// Past imports and their undo.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { ImportBatch } from '@/app/api/imports';

const api = { listImports: vi.fn(), previewUndo: vi.fn(), undoImport: vi.fn() };

vi.mock('@/app/api/imports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/imports')>()),
  listImports: (...a: unknown[]) => api.listImports(...a),
  previewUndo: (...a: unknown[]) => api.previewUndo(...a),
  undoImport: (...a: unknown[]) => api.undoImport(...a),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const { default: RecentImports, UndoImportDialog, removalSummary } = await import('./RecentImports');

function batch(overrides: Partial<ImportBatch>): ImportBatch {
  return {
    id: 'b1', kind: 'lead', fileName: 'waitlist.csv', importedBy: 'Kia', startedAtUtc: '2026-10-01T08:00:00Z',
    finishedAtUtc: '2026-10-01T08:01:00Z', duplicates: 'skip', ranAutomations: false, totalRows: 10, created: 10,
    updated: 0, skipped: 0, failed: 0, undoneAtUtc: null, removed: 0, remaining: 20, running: false, canUndo: true,
    ...overrides,
  };
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
});

describe('past imports', () => {
  it('says an import is still running, and offers no undo until it has finished', async () => {
    api.listImports.mockResolvedValue([batch({ finishedAtUtc: null, running: true, canUndo: false })]);

    render(<RecentImports kind="lead" refreshKey={0} />);

    expect(await screen.findByText('Still running. It can be undone once it has finished.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Undo' })).not.toBeInTheDocument();
  });

  it('says when one stopped part way, so it can be undone and imported again', async () => {
    api.listImports.mockResolvedValue([batch({ finishedAtUtc: null, running: false })]);

    render(<RecentImports kind="lead" refreshKey={0} />);

    expect(await screen.findByText(/Stopped part way through/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Undo' })).toBeInTheDocument();
  });
});

describe('the undo dialog', () => {
  it('reads what undoing would remove once, however often the page around it renders', async () => {
    api.previewUndo.mockResolvedValue({ batchId: 'b1', dryRun: true, leads: 2, contacts: 2, companies: 0, kept: [] });
    const onClose = vi.fn();
    const view = render(<UndoImportDialog kind="lead" batch={{ id: 'b1', fileName: 'waitlist.csv' }} onClose={onClose} onUndone={vi.fn()} />);
    expect(await screen.findByText('2 leads and the 2 contacts made for them')).toBeInTheDocument();

    // The page builds a new object for the same import each time it renders.
    view.rerender(<UndoImportDialog kind="lead" batch={{ id: 'b1', fileName: 'waitlist.csv' }} onClose={onClose} onUndone={vi.fn()} />);

    expect(screen.getByText('2 leads and the 2 contacts made for them')).toBeInTheDocument();
    expect(api.previewUndo).toHaveBeenCalledTimes(1);
  });

  it('names what goes the way a person would say it', () => {
    expect(removalSummary('lead', { leads: 798, contacts: 798, companies: 12 })).toBe('798 leads, the 798 contacts made for them and 12 companies');
    expect(removalSummary('contact', { leads: 0, contacts: 1, companies: 1 })).toBe('1 contact and 1 company');
    expect(removalSummary('company', { leads: 0, contacts: 0, companies: 3 })).toBe('3 companies');
  });
});
