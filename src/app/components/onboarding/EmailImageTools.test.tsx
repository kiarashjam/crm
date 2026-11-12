// Images in a welcome email: added from a file (placed at the cursor), recognised by
// whether a mail app can show them, and embedded ones hosted with one click.
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import type { EmailImage } from '@/app/api/emailImages';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
const uploaded: File[] = [];
let next = 0;
vi.mock('@/app/api/emailImages', async (original) => ({
  ...(await original<typeof import('@/app/api/emailImages')>()),
  listEmailImages: async () => [],
  uploadEmailImage: async (file: File): Promise<EmailImage> => {
    uploaded.push(file);
    const id = `00000000-0000-4000-8000-00000000000${++next}`;
    return { id, url: `https://api.cadencee.ai/api/public/email-images/${id}.png`, fileName: file.name, contentType: 'image/png', size: file.size, width: 1200, createdAtUtc: '' };
  },
}));

const { EmailImageTools, imageSources, imageTag } = await import('./EmailImageTools');

function Editor({ start }: { start: string }) {
  const [html, setHtml] = useState(start);
  return (
    <>
      <textarea id="html" aria-label="HTML" value={html} onChange={(e) => setHtml(e.target.value)} />
      <EmailImageTools html={html} onChange={setHtml} textareaId="html" />
    </>
  );
}

beforeEach(() => { uploaded.length = 0; });

describe('images a mail app can show', () => {
  it('tells hosted, embedded and unreachable images apart', () => {
    const html = `<img src="https://api.cadencee.ai/api/public/email-images/00000000-0000-4000-8000-000000000001.png">
      <img alt="x" src='data:image/png;base64,iVBOR'> <img src="logo.png"><img src="http://old.example.com/a.jpg">
      <img src="{{portalUrl}}/logo.png"><img src="https://cdn.example.com/b.png">`;
    expect(imageSources(html).map((s) => s.kind)).toEqual(['hosted', 'embedded', 'unreachable', 'unreachable', 'hosted']);
  });

  it('writes an image sized for the reading pane, scaling down on a phone', () => {
    const tag = imageTag({ url: 'https://x.test/a.png', fileName: 'club-logo_dark.png', width: 1200 });
    expect(tag).toBe('<img src="https://x.test/a.png" alt="club logo dark" width="600" style="display:block;max-width:100%;height:auto;border:0">');
  });
});

describe('the image tools', () => {
  it('uploads a chosen image and places it in the HTML', async () => {
    render(<Editor start="<p>Hello</p>" />);
    const file = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'logo.png', { type: 'image/png' });
    fireEvent.change(screen.getByLabelText('Choose images'), { target: { files: [file] } });
    await waitFor(() => expect((screen.getByLabelText('HTML') as HTMLTextAreaElement).value).toContain('/api/public/email-images/'));
    expect(uploaded.map((f) => f.name)).toEqual(['logo.png']);
    expect((screen.getByLabelText('HTML') as HTMLTextAreaElement).value).toContain('width="600"');
  });

  it('refuses what a mail app cannot show, before uploading it', async () => {
    render(<Editor start="" />);
    const svg = new File(['<svg/>'], 'logo.svg', { type: 'image/svg+xml' });
    fireEvent.change(screen.getByLabelText('Choose images'), { target: { files: [svg] } });
    await new Promise((r) => setTimeout(r, 20));
    expect(uploaded).toHaveLength(0);
  });

  it('says when images are embedded or out of reach', () => {
    render(<Editor start={'<img src="data:image/png;base64,iVBORw0KGgo="><img src="C:/Users/kia/logo.png">'} />);
    const alerts = screen.getAllByRole('alert');
    expect(alerts[0]).toHaveTextContent('An image is embedded in the HTML');
    expect(screen.getByRole('button', { name: /Host it/ })).toBeInTheDocument();
    expect(alerts[1]).toHaveTextContent('An image points where no mail app can reach');
    expect(alerts[1]).toHaveTextContent('C:/Users/kia/logo.png');
  });
});
