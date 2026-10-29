// The CSV import: its rows are people the workspace already knows, so they must
// not set off what a brand-new inquiry does.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const api = { createLead: vi.fn(), getLeadStatuses: vi.fn(), getLeadSources: vi.fn() };
vi.mock('@/app/api', () => ({
  createLead: (...a: unknown[]) => api.createLead(...a),
  getLeadStatuses: (...a: unknown[]) => api.getLeadStatuses(...a),
  getLeadSources: (...a: unknown[]) => api.getLeadSources(...a),
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const LeadImport = (await import('./LeadImport')).default;

const csv = () => new File(['name,email,status\nJean Dupont,jean@example.ch,Contacted\n'], 'legacy.csv', { type: 'text/csv' });

async function importFile(options: { runAutomations?: boolean } = {}) {
  const user = userEvent.setup();
  const { container } = render(<MemoryRouter><LeadImport /></MemoryRouter>);
  // A change event, not user.upload: the drop zone around the input re-clicks it.
  fireEvent.change(container.querySelector('input[type="file"]') as HTMLInputElement, { target: { files: [csv()] } });
  await user.click(await screen.findByRole('button', { name: /Preview Import/ }));
  const box = screen.getByRole('checkbox', { name: 'Run automations for these leads' });
  expect(box).not.toBeChecked();
  if (options.runAutomations) await user.click(box);
  await user.click(screen.getByRole('button', { name: /Import 1 Leads/ }));
  await screen.findByText('Import Complete!');
}

beforeEach(() => {
  api.createLead.mockReset().mockResolvedValue({ id: 'lead-1' });
  api.getLeadStatuses.mockReset().mockResolvedValue([]);
  api.getLeadSources.mockReset().mockResolvedValue([]);
});

describe('importing leads', () => {
  it('marks every row as an import, with automations off unless asked for', async () => {
    await importFile();
    expect(api.createLead).toHaveBeenCalledWith(expect.objectContaining({
      name: 'Jean Dupont', email: 'jean@example.ch', status: 'Contacted', origin: 'import', runAutomations: false,
    }));
  });

  it('runs the automations when the person importing ticks the box', async () => {
    await importFile({ runAutomations: true });
    expect(api.createLead).toHaveBeenCalledWith(expect.objectContaining({ origin: 'import', runAutomations: true }));
  });
});
