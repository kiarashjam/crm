// Making an image fit for an email before it is uploaded. A photo straight from a camera or
// exported from a design tool (a 1700 px PNG of 5 MB, say) was refused as too large, and
// the person was told to "make it smaller" with no way to here. Mail apps show an email
// at 600 px; twice that is sharp on any screen, and a photo saved as JPEG at that size
// is a few hundred kilobytes.

import { EMAIL_IMAGE_MAX_BYTES } from '@/app/api/emailImages';

/** Twice the 600 px of an email: sharp on a high-density screen, and no wider. */
export const EMAIL_IMAGE_MAX_WIDTH = 1200;
/** Lighter than this, and no wider than the above, an image is uploaded as it is. */
export const EMAIL_IMAGE_LIGHT_BYTES = 1024 * 1024;

export interface Shrunk {
  file: File;
  width?: number;
  height?: number;
  /** Re-saved smaller: what it was before, to say so. */
  from?: { width: number; height: number; bytes: number };
}

/**
 * The size and format to save an image at for an email, or null to keep it as it is.
 * A picture with see-through parts (a logo) stays PNG; anything else becomes JPEG.
 */
export function shrinkPlan(image: { width: number; height: number; bytes: number; type: string; transparent: boolean }):
  { width: number; height: number; type: 'image/png' | 'image/jpeg' } | null {
  const wide = image.width > EMAIL_IMAGE_MAX_WIDTH;
  const heavy = image.bytes > EMAIL_IMAGE_LIGHT_BYTES;
  const usable = ['image/png', 'image/jpeg', 'image/webp'].includes(image.type);
  if (!wide && !heavy && usable) return null;
  const scale = wide ? EMAIL_IMAGE_MAX_WIDTH / image.width : 1;
  return {
    width: Math.max(1, Math.round(image.width * scale)),
    height: Math.max(1, Math.round(image.height * scale)),
    type: image.transparent ? 'image/png' : 'image/jpeg',
  };
}

/** "4.9 MB", "312 KB". */
export function bytesLabel(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

type Canvas = HTMLCanvasElement | OffscreenCanvas;

function canvas(width: number, height: number): Canvas {
  if (typeof OffscreenCanvas === 'function') return new OffscreenCanvas(width, height);
  const c = document.createElement('canvas');
  c.width = width;
  c.height = height;
  return c;
}

function encode(c: Canvas, type: string, quality?: number): Promise<Blob | null> {
  if ('convertToBlob' in c) return c.convertToBlob({ type, quality }).catch(() => null);
  return new Promise((resolve) => (c as HTMLCanvasElement).toBlob(resolve, type, quality));
}

/** Whether any pixel is see-through, judged on a small copy. */
function hasTransparency(bitmap: ImageBitmap): boolean {
  const w = Math.min(96, bitmap.width);
  const h = Math.max(1, Math.round((bitmap.height / bitmap.width) * w));
  const c = canvas(w, h);
  const ctx = c.getContext('2d') as CanvasRenderingContext2D | OffscreenCanvasRenderingContext2D | null;
  if (!ctx) return false;
  ctx.drawImage(bitmap, 0, 0, w, h);
  const data = ctx.getImageData(0, 0, w, h).data;
  for (let i = 3; i < data.length; i += 4) if (data[i]! < 255) return true;
  return false;
}

function renamed(name: string, type: string): string {
  const base = name.replace(/\.[a-z0-9]+$/i, '') || 'image';
  return `${base}.${type === 'image/png' ? 'png' : 'jpg'}`;
}

/**
 * The image ready for an email: as it is when it already fits, else re-saved at most
 * 1200 px wide (JPEG, or PNG when it has see-through parts), and smaller still if it
 * would otherwise be over the upload limit. An image the browser cannot read (an animated
 * GIF is kept as it is on purpose) goes as it is, for the server to judge.
 */
export async function shrinkForEmail(file: File): Promise<Shrunk> {
  if (file.type === 'image/gif' || typeof createImageBitmap !== 'function') return { file };
  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    return { file };
  }
  try {
    const { width, height } = bitmap;
    const type = file.type || 'application/octet-stream';
    const transparent = type === 'image/jpeg' ? false : hasTransparency(bitmap);
    const plan = shrinkPlan({ width, height, bytes: file.size, type, transparent });
    if (!plan) return { file, width, height };

    // Smaller in steps until it fits under the limit: lower JPEG quality first, then fewer pixels.
    let { width: w, height: h } = plan;
    for (let attempt = 0; attempt < 6; attempt++) {
      const quality = plan.type === 'image/jpeg' ? [0.86, 0.78, 0.7][Math.min(attempt, 2)] : undefined;
      const c = canvas(w, h);
      const ctx = c.getContext('2d') as CanvasRenderingContext2D | OffscreenCanvasRenderingContext2D | null;
      if (!ctx) return { file, width, height };
      if (plan.type === 'image/jpeg') {
        // A JPEG has no transparency: what was see-through would turn black, so it is white.
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, w, h);
      }
      ctx.imageSmoothingQuality = 'high';
      ctx.drawImage(bitmap, 0, 0, w, h);
      const blob = await encode(c, plan.type, quality);
      if (!blob) return { file, width, height };
      if (blob.size <= EMAIL_IMAGE_MAX_BYTES) {
        // Re-saving a small image that was already fine is no gain.
        const fine = file.size <= EMAIL_IMAGE_MAX_BYTES && width <= EMAIL_IMAGE_MAX_WIDTH && ['image/png', 'image/jpeg', 'image/webp'].includes(type);
        if (fine && blob.size >= file.size) return { file, width, height };
        return {
          file: new File([blob], renamed(file.name, plan.type), { type: plan.type }),
          width: w,
          height: h,
          from: { width, height, bytes: file.size },
        };
      }
      if (plan.type !== 'image/jpeg' || attempt >= 2) {
        w = Math.max(1, Math.round(w * 0.8));
        h = Math.max(1, Math.round(h * 0.8));
      }
    }
    return { file, width, height };
  } finally {
    bitmap.close?.();
  }
}
