// What a person sees when an upload is refused.
//
// Attachments now live on the server, which refuses some uploads: a file over
// 10 MB, a Viewer adding one. The card used to have no catch at all, because the
// old browser-only fallback swallowed every failure; against the server, a refusal
// would have vanished with no message. These tests pin that the server's own words
// reach the person, and that an oversized file is stopped before it is sent.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import AttachmentsCard from './AttachmentsCard';

const api = vi.hoisted(() => ({
  getAttachments: vi.fn(),
  uploadAttachment: vi.fn(),
  deleteAttachment: vi.fn(),
  attachmentHref: vi.fn(),
}));
vi.mock('@/app/api', () => api);

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

function chooseFiles(...files: File[]) {
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  fireEvent.change(input, { target: { files } });
}

beforeEach(() => {
  vi.clearAllMocks();
  api.getAttachments.mockResolvedValue([]);
});

describe('AttachmentsCard uploads', () => {
  it("shows the server's reason when it refuses a file", async () => {
    api.uploadAttachment.mockRejectedValue(new Error('You have read-only access to this organization.'));
    render(<AttachmentsCard entityType="lead" recordId="lead-1" />);
    await screen.findByText(/attachments/i);

    chooseFiles(new File(['terms'], 'terms.txt', { type: 'text/plain' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('You have read-only access to this organization.'));
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('stops a file over 10 MB before sending it', async () => {
    render(<AttachmentsCard entityType="lead" recordId="lead-1" />);
    await screen.findByText(/attachments/i);
    const big = new File(['x'], 'scan.pdf', { type: 'application/pdf' });
    Object.defineProperty(big, 'size', { value: 10 * 1024 * 1024 + 1 });

    chooseFiles(big);

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('scan.pdf is over 10 MB'));
    expect(api.uploadAttachment).not.toHaveBeenCalled();
  });

  it('uploads the rest when one of several files is refused', async () => {
    api.uploadAttachment
      .mockRejectedValueOnce(new Error('Files can be up to 10 MB'))
      .mockResolvedValueOnce({
        id: 'att-2', entityType: 'lead', recordId: 'lead-1', fileName: 'ok.txt',
        contentType: 'text/plain', size: 2, uploadedAtUtc: new Date().toISOString(),
      });
    render(<AttachmentsCard entityType="lead" recordId="lead-1" />);
    await screen.findByText(/attachments/i);

    chooseFiles(new File(['a'], 'refused.txt'), new File(['ok'], 'ok.txt'));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Uploaded'));
    expect(toast.error).toHaveBeenCalledWith('Files can be up to 10 MB');
    expect(await screen.findByText('ok.txt')).toBeInTheDocument();
  });
});
