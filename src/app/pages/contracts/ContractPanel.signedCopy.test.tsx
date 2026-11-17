// A contract signed outside CadenceE, recorded on the deal by uploading its signed copy: from a
// deal with no contract yet, or closing one sent here that was signed another way. The deal is
// read again afterwards (it is won now), and the contract shows as signed, with its copy.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Contract } from '@/app/api/contracts';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), warning: vi.fn(), error: vi.fn() } }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/api/apiClient', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/apiClient')>('@/app/api/apiClient');
  return { ...actual, isUsingRealApi: () => true, servesDemoData: () => false };
});
vi.mock('@/app/api/catalog', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/catalog')>('@/app/api/catalog');
  return { ...actual, getCatalog: () => Promise.resolve([]) };
});
vi.mock('@/app/hooks/useMay', () => ({ useMay: () => () => true }));

const api = vi.hoisted(() => ({
  forDeal: [] as Contract[],
  uploads: [] as Array<Record<string, unknown>>,
}));
vi.mock('@/app/api/contracts', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/contracts')>('@/app/api/contracts');
  return {
    ...actual,
    listContractsForDeal: () => Promise.resolve(api.forDeal),
    uploadSignedContract: (args: Record<string, unknown>) => {
      api.uploads.push(args);
      const signed = {
        ...(api.forDeal[0] ?? {}),
        id: (args.contractId as string) ?? 'c-new', dealId: 'd1', status: 'countersigned', signedElsewhere: true,
        title: 'Signed contract — Anna Muster', body: '', counterpartyName: 'Anna Muster', counterpartyEmail: 'anna@example.ch',
        clientSignatureName: args.signedBy, counterSignedAtUtc: new Date().toISOString(),
        createdAtUtc: '2026-11-01T09:00:00Z', updatedAtUtc: '2026-11-17T09:00:00Z', allowedActions: [], unresolvedFields: [], events: [],
      } as Contract;
      api.forDeal = [signed];
      return Promise.resolve(signed);
    },
  };
});

const { ContractPanel } = await import('./ContractPanel');

const pdf = () => new File(['%PDF-1.4 signed'], 'Contrat signé.pdf', { type: 'application/pdf' });

beforeEach(() => {
  api.forDeal = [];
  api.uploads = [];
});

describe('a contract signed elsewhere, on a deal', () => {
  it('is uploaded from a deal with no contract yet, wins the deal and shows as signed with its copy', async () => {
    const user = userEvent.setup();
    const changed = vi.fn();
    render(<MemoryRouter><ContractPanel dealId="d1" leadName="Anna Muster" onLeadChanged={changed} /></MemoryRouter>);

    await user.click(await screen.findByRole('button', { name: /Upload the signed contract/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/the deal is marked won/)).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: /Record as signed/ })).toBeDisabled();
    expect(within(dialog).getByLabelText('Signed by')).toHaveValue('Anna Muster');

    await user.upload(within(dialog).getByLabelText('The signed contract'), pdf());
    expect(within(dialog).getByText('Contrat signé.pdf')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /Record as signed/ }));

    await waitFor(() => expect(api.uploads).toHaveLength(1));
    expect(api.uploads[0]).toMatchObject({ dealId: 'd1', signedBy: 'Anna Muster' });
    expect(api.uploads[0]?.contractId).toBeUndefined();
    expect(await screen.findByText('Signed outside CadenceE')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Signed copy/ })).toBeInTheDocument();
    // Nothing was emailed, so neither the warning about it nor a button to send it again.
    expect(screen.queryByText(/copy has not been emailed/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Email the signed copy/ })).not.toBeInTheDocument();
    expect(changed).toHaveBeenCalled();
  });

  it('closes a contract sent here that was signed another way, by its copy', async () => {
    api.forDeal = [{
      id: 'c1', dealId: 'd1', status: 'sent', title: 'Membership Agreement — Anna Muster', body: 'The agreement.',
      counterpartyName: 'Anna Muster', counterpartyEmail: 'anna@example.ch', createdAtUtc: '2026-11-01T09:00:00Z',
      updatedAtUtc: '2026-11-02T09:00:00Z', sentAtUtc: '2026-11-02T09:00:00Z', allowedActions: ['void'], unresolvedFields: [], events: [],
    } as Contract];
    const user = userEvent.setup();
    render(<MemoryRouter><ContractPanel dealId="d1" leadName="Anna Muster" /></MemoryRouter>);

    await user.click(await screen.findByRole('button', { name: /Upload the signed copy/ }));
    const dialog = await screen.findByRole('dialog');
    await user.upload(within(dialog).getByLabelText('The signed contract'), pdf());
    await user.click(within(dialog).getByRole('button', { name: /Record as signed/ }));

    await waitFor(() => expect(api.uploads).toHaveLength(1));
    expect(api.uploads[0]).toMatchObject({ contractId: 'c1' });
    expect(api.uploads[0]?.dealId).toBeUndefined();
  });

  it('takes only a PDF or a photo', async () => {
    const user = userEvent.setup({ applyAccept: false });
    render(<MemoryRouter><ContractPanel dealId="d1" leadName="Anna Muster" /></MemoryRouter>);
    await user.click(await screen.findByRole('button', { name: /Upload the signed contract/ }));
    const dialog = await screen.findByRole('dialog');

    await user.upload(within(dialog).getByLabelText('The signed contract'), new File(['words'], 'contract.docx'));
    expect(within(dialog).getByRole('alert')).toHaveTextContent(/PDF, or a JPEG or PNG photo/);
    expect(within(dialog).getByRole('button', { name: /Record as signed/ })).toBeDisabled();
  });
});
