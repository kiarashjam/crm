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

const { default: RecentImports } = await import('./RecentImports');

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
