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
  getApiBaseUrl: () => 'https://api.cadencee.ai',
}));
const ID = (n: number) => `00000000-0000-4000-8000-00000000009${n}`;
const library: EmailImage[] = [
  { id: ID(1), url: `https://api.cadencee.ai/api/public/email-images/${ID(1)}.jpg`, fileName: 'hero-founders.jpg', contentType: 'image/jpeg', size: 290000, width: 1200, createdAtUtc: '' },
  { id: ID(2), url: `https://api.cadencee.ai/api/public/email-images/${ID(2)}.jpg`, fileName: 'indoor-pool.jpg', contentType: 'image/jpeg', size: 60000, width: 594, createdAtUtc: '' },
];
const uploaded: File[] = [];
let next = 0;
vi.mock('@/app/api/emailImages', async (original) => ({
  ...(await original<typeof import('@/app/api/emailImages')>()),
  listEmailImages: async () => library,
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

  it("knows CadenceE's own image with a localhost address, and its alt text as it reads", () => {
    const html = `<img alt="Pool &middot; spa &ndash; Lutry" width="594" src="http://localhost:5160/api/public/email-images/${ID(2)}.jpg">`;
    expect(imageSources(html)).toEqual([{ src: `http://localhost:5160/api/public/email-images/${ID(2)}.jpg`, alt: 'Pool · spa – Lutry', kind: 'local' }]);
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
    expect(alerts[1]).toHaveTextContent('An image cannot be shown (an address no mail app can reach)');
    const panel = screen.getByRole('region', { name: 'Images in this email' });
    expect(panel).toHaveTextContent('C:/Users/kia/logo.png');
    expect(panel).toHaveTextContent('Not on the web');
  });

  it('shows each image in the email, and puts a localhost link right with one click', async () => {
    const local = `http://localhost:5160/api/public/email-images/${ID(2)}.jpg`;
    render(<Editor start={`<img alt="indoor pool" src="${local}"><img alt="hero" src="${library[0]!.url}">`} />);
    const panel = screen.getByRole('region', { name: 'Images in this email' });
    expect(panel).toHaveTextContent('Images in this email (2)');
    // Seen at the address it will have, not the one it points at.
    expect(screen.getByRole('img', { name: 'indoor pool' })).toHaveAttribute('src', library[1]!.url);
    expect(panel).toHaveTextContent('Link points at localhost');
    expect(screen.queryByText(/on the web, so mail apps show them/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Correct it/ }));
    const html = (screen.getByLabelText('HTML') as HTMLTextAreaElement).value;
    expect(html).not.toContain('localhost');
    expect(html).toContain(library[1]!.url);
  });

  it('replaces an image with a new upload, everywhere it is used', async () => {
    const old = library[0]!.url;
    render(<Editor start={`<img alt="hero" src="${old}"><p>…</p><img alt="hero again" src="${old}">`} />);
    fireEvent.click(screen.getByRole('button', { name: 'Replace hero with a new upload' }));
    const file = new File([new Uint8Array([0xff, 0xd8, 0xff])], 'new-hero.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText('Choose images'), { target: { files: [file] } });
    await waitFor(() => expect((screen.getByLabelText('HTML') as HTMLTextAreaElement).value).not.toContain(old));
    expect((screen.getByLabelText('HTML') as HTMLTextAreaElement).value.match(/email-images\/00000000-0000-4000-8000-00000000000\d\.png/g)).toHaveLength(2);
  });

  it('exchanges an image for one already in the library', async () => {
    render(<Editor start={`<img alt="pool" src="${library[0]!.url}">`} />);
    await screen.findByRole('button', { name: /Your images \(2\)/ });
    fireEvent.click(screen.getByRole('button', { name: 'Exchange pool for one of your images' }));
    expect(await screen.findByText(/Choose the image to put in place of/)).toHaveTextContent('pool');
    const uses = screen.getAllByRole('button', { name: /Use this/ });
    fireEvent.click(uses[1]!);
    const html = (screen.getByLabelText('HTML') as HTMLTextAreaElement).value;
    expect(html).toContain(library[1]!.url);
    expect(html).not.toContain(library[0]!.url);
    expect(screen.queryByText(/Choose the image to put in place of/)).not.toBeInTheDocument();
  });

  it('says when an image can no longer be loaded (taken out of the library, say)', async () => {
    const gone = `https://api.cadencee.ai/api/public/email-images/${ID(7)}.jpg`;
    render(<Editor start={`<img alt="reception" src="${gone}">`} />);
    fireEvent.error(screen.getByRole('img', { name: 'reception' }));
    expect(await screen.findByText('Cannot be loaded')).toBeInTheDocument();
    expect(screen.getAllByRole('alert').some((a) => /gone from the server/.test(a.textContent ?? ''))).toBe(true);
  });

  it('takes an image out of the email, leaving the rest', () => {
    render(<Editor start={`<p>Hi</p>\n<img alt="extra" src="${library[1]!.url}">\n<img alt="hero" src="${library[0]!.url}">`} />);
    fireEvent.click(screen.getByRole('button', { name: 'Take extra out of the email' }));
    const html = (screen.getByLabelText('HTML') as HTMLTextAreaElement).value;
    expect(html).not.toContain(library[1]!.url);
    expect(html).toContain(library[0]!.url);
    expect(html).toContain('<p>Hi</p>');
  });
});
