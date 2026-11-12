// Images for the workspace's emails (the welcome email first). Each is kept on the
// server and served at a public https address, because mail apps only show images they
// can fetch: one embedded in the HTML is blocked by most, and one on a computer reaches
// nobody. Server only, like Welcome & portal itself.

import { authFetch, authFetchJson, ensureOk, servesDemoData } from './apiClient';

export interface EmailImage {
  id: string;
  /** The public address to put in an email. */
  url: string;
  fileName: string;
  contentType: string;
  size: number;
  width?: number | null;
  height?: number | null;
  createdAtUtc: string;
}

/** The kinds mail apps show. */
export const EMAIL_IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];
export const EMAIL_IMAGE_MAX_BYTES = 3 * 1024 * 1024;

const DEMO_MESSAGE = 'Email images are kept on the server, so they work with the server, not in demo mode.';

export async function listEmailImages(): Promise<EmailImage[]> {
  if (servesDemoData()) return [];
  return (await authFetchJson<EmailImage[]>('/api/email-images')) ?? [];
}

export async function uploadEmailImage(file: File, size?: { width?: number; height?: number }): Promise<EmailImage> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  const form = new FormData();
  form.append('file', file);
  if (size?.width) form.append('width', String(Math.round(size.width)));
  if (size?.height) form.append('height', String(Math.round(size.height)));
  return authFetchJson<EmailImage>('/api/email-images', { method: 'POST', body: form });
}

export async function deleteEmailImage(id: string): Promise<void> {
  if (servesDemoData()) throw new Error(DEMO_MESSAGE);
  await ensureOk(await authFetch(`/api/email-images/${id}`, { method: 'DELETE' }));
}

/**
 * Whether an address is one of CadenceE's own email images. Its host is the server's
 * public address, which need not be the one this page calls (a proxy, a dev setup).
 */
export function isOwnEmailImage(src: string): boolean {
  return /^https?:\/\/[^/]+\/api\/public\/email-images\/[0-9a-f-]{36}\.(png|jpg|gif|webp)$/i.test(src.trim());
}
