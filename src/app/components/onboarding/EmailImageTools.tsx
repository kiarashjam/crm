import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { toast } from 'sonner';
import { AlertTriangle, CheckCircle2, ChevronDown, Copy, ImagePlus, Images, Loader2, Plus, Trash2, UploadCloud } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import { servesDemoData } from '@/app/api/apiClient';
import {
  EMAIL_IMAGE_MAX_BYTES, EMAIL_IMAGE_TYPES, deleteEmailImage, isOwnEmailImage, listEmailImages, uploadEmailImage,
  type EmailImage,
} from '@/app/api/emailImages';
import { insertAt } from './WelcomeFields';

/** Wider than this, an image overflows most mail apps' reading pane. */
const EMAIL_WIDTH = 600;

const escapeAttr = (s: string) => s.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');

/** The tag for an image in an email: sized for the reading pane, scaling down on a phone. */
export function imageTag(image: Pick<EmailImage, 'url' | 'fileName' | 'width'>): string {
  const alt = image.fileName.replace(/\.[a-z0-9]+$/i, '').replace(/[-_]+/g, ' ').trim();
  const width = image.width ? ` width="${Math.min(image.width, EMAIL_WIDTH)}"` : '';
  return `<img src="${escapeAttr(image.url)}" alt="${escapeAttr(alt)}"${width} style="display:block;max-width:100%;height:auto;border:0">`;
}

export type ImageSource = { src: string; kind: 'hosted' | 'embedded' | 'unreachable' };

/**
 * Every image in the HTML, and whether a mail app can show it: one CadenceE hosts, or
 * any https address, it can; one embedded as data most block; one on a computer, over
 * plain http, or a cid: reference from another tool it cannot fetch at all.
 */
export function imageSources(html: string): ImageSource[] {
  const seen = new Map<string, ImageSource>();
  for (const match of html.matchAll(/<img\b[^>]*?\bsrc\s*=\s*(["'])(.*?)\1/gis)) {
    const src = match[2]!.trim();
    if (!src || src.includes('{{') || seen.has(src)) continue;
    const kind = isOwnEmailImage(src) || /^https:\/\//i.test(src)
      ? 'hosted'
      : /^data:image\//i.test(src) ? 'embedded' : 'unreachable';
    seen.set(src, { src, kind });
  }
  return [...seen.values()];
}

/** The image's own size, when the browser can read it. */
async function pixels(file: Blob): Promise<{ width?: number; height?: number }> {
  try {
    if (typeof createImageBitmap !== 'function') return {};
    const bitmap = await createImageBitmap(file);
    const size = { width: bitmap.width, height: bitmap.height };
    bitmap.close?.();
    return size;
  } catch {
    return {};
  }
}

function problem(file: File): string | null {
  if (!EMAIL_IMAGE_TYPES.includes(file.type)) return `${file.name}: use a PNG, JPEG, GIF or WebP image.`;
  if (file.size > EMAIL_IMAGE_MAX_BYTES) return `${file.name} is over 3 MB. Make it smaller (1200 px wide is plenty).`;
  return null;
}

function short(src: string): string {
  if (src.startsWith('data:')) return 'an embedded image';
  return src.length > 60 ? `${src.slice(0, 28)}…${src.slice(-28)}` : src;
}

/**
 * Images for an email's HTML: add one (or paste or drop it on the HTML), insert one from
 * the workspace's library, and put right the images a mail app could not show.
 */
export function EmailImageTools({
  html,
  onChange,
  textareaId,
  disabled,
  compact,
}: {
  html: string;
  onChange: (html: string) => void;
  /** The HTML box: images are inserted at its cursor, and may be pasted or dropped on it. */
  textareaId: string;
  disabled?: boolean;
  compact?: boolean;
}) {
  const [library, setLibrary] = useState<EmailImage[] | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const picker = useRef<HTMLInputElement>(null);
  const replacing = useRef<string | null>(null);
  // An upload finishes after the person may have typed on: what it changes is the HTML
  // as it is then, through the handler of that moment, not the ones it started with.
  const latest = useRef(html);
  latest.current = html;
  const change = useRef(onChange);
  change.current = onChange;
  const write = useCallback((next: string) => {
    latest.current = next;
    change.current(next);
  }, []);
  // Busy while any upload is under way, however many overlap.
  const working = useRef(0);
  const track = useCallback(async <T,>(work: () => Promise<T>): Promise<T> => {
    working.current++;
    setBusy(true);
    try {
      return await work();
    } finally {
      if (--working.current === 0) setBusy(false);
    }
  }, []);

  useEffect(() => {
    if (servesDemoData()) return;
    listEmailImages().then(setLibrary).catch(() => setLibrary([]));
  }, []);

  const upload = useCallback(async (files: File[]): Promise<EmailImage[]> => {
    const ok = files.filter((f) => {
      const why = problem(f);
      if (why) toast.error(why);
      return !why;
    });
    if (ok.length === 0) return [];
    return track(async () => {
      const made: EmailImage[] = [];
      try {
        for (const file of ok) made.push(await uploadEmailImage(file, await pixels(file)));
      } catch (e) {
        toast.error(e instanceof Error && e.message ? e.message : 'The image could not be uploaded.');
      }
      setLibrary((current) => [...made, ...(current ?? [])]);
      return made;
    });
  }, [track]);

  const insert = useCallback((images: EmailImage[]) => {
    if (images.length === 0) return;
    const tags = images.map(imageTag).join('\n');
    write(insertAt(textareaId, latest.current, `\n${tags}\n`));
  }, [write, textareaId]);

  // Paste or drop an image straight onto the HTML.
  useEffect(() => {
    const box = document.getElementById(textareaId);
    if (!box || disabled || servesDemoData()) return;
    const filesOf = (list: FileList | null | undefined) => [...(list ?? [])].filter((f) => f.type.startsWith('image/'));
    const onPaste = (e: ClipboardEvent) => {
      const files = filesOf(e.clipboardData?.files);
      if (files.length === 0) return;
      e.preventDefault();
      void upload(files).then(insert);
    };
    const onDragOver = (e: DragEvent) => {
      if ([...(e.dataTransfer?.items ?? [])].some((i) => i.kind === 'file')) e.preventDefault();
    };
    const onDrop = (e: DragEvent) => {
      const files = filesOf(e.dataTransfer?.files);
      if (files.length === 0) return;
      e.preventDefault();
      void upload(files).then(insert);
    };
    box.addEventListener('paste', onPaste as EventListener);
    box.addEventListener('dragover', onDragOver as EventListener);
    box.addEventListener('drop', onDrop as EventListener);
    return () => {
      box.removeEventListener('paste', onPaste as EventListener);
      box.removeEventListener('dragover', onDragOver as EventListener);
      box.removeEventListener('drop', onDrop as EventListener);
    };
  }, [textareaId, disabled, upload, insert]);

  const sources = useMemo(() => imageSources(html), [html]);
  const embedded = sources.filter((s) => s.kind === 'embedded');
  const unreachable = sources.filter((s) => s.kind === 'unreachable');
  const hosted = sources.filter((s) => s.kind === 'hosted');

  /** Every use of one address swapped for another, in the HTML as it is now. */
  const swap = (from: string, to: string) => write(latest.current.split(from).join(to));

  const hostEmbedded = () => track(async () => {
    let n = 0;
    for (const { src } of embedded) {
      try {
        const blob = await (await fetch(src)).blob();
        const ext = blob.type.split('/')[1]?.replace('jpeg', 'jpg') ?? 'png';
        const [made] = await upload([new File([blob], `image-${++n}.${ext}`, { type: blob.type })]);
        if (made) swap(src, made.url);
      } catch {
        toast.error('An embedded image could not be read. Upload the file for it instead.');
      }
    }
  });

  const onPicked = async (files: File[]) => {
    const target = replacing.current;
    replacing.current = null;
    const made = await upload(target ? files.slice(0, 1) : files);
    if (target && made[0]) swap(target, made[0].url);
    else insert(made);
  };

  const remove = async (image: EmailImage) => {
    const used = latest.current.includes(image.url);
    if (!window.confirm(used
      ? 'This image is in the email. Remove it anyway? Emails already sent with it will show it no more.'
      : 'Remove this image? Emails already sent with it will show it no more.')) return;
    try {
      await deleteEmailImage(image.id);
      setLibrary((current) => (current ?? []).filter((i) => i.id !== image.id));
    } catch (e) {
      toast.error(e instanceof Error && e.message ? e.message : 'It could not be removed.');
    }
  };

  const copy = async (url: string) => {
    try {
      await navigator.clipboard.writeText(url);
      toast.success('Link copied');
    } catch {
      toast.error('Could not copy');
    }
  };

  if (servesDemoData()) return null;

  return (
    <div className={cn('space-y-2', compact && 'text-xs')}>
      <input
        ref={picker}
        type="file"
        accept={EMAIL_IMAGE_TYPES.join(',')}
        multiple
        className="hidden"
        aria-label="Choose images"
        onChange={(e) => { const files = [...(e.target.files ?? [])]; e.target.value = ''; void onPicked(files); }}
      />
      <div className="flex flex-wrap items-center gap-2">
        {!disabled && (
          <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => { replacing.current = null; picker.current?.click(); }}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />} Add image
          </Button>
        )}
        {library && library.length > 0 && (
          <Button type="button" size="sm" variant="ghost" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
            <Images className="h-4 w-4" /> Your images ({library.length})
            <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', open && 'rotate-180')} />
          </Button>
        )}
        {!disabled && <span className="text-[11px] text-slate-500">or paste or drop an image onto the HTML</span>}
        {hosted.length > 0 && embedded.length === 0 && unreachable.length === 0 && (
          <span className="ml-auto inline-flex items-center gap-1 text-[11px] text-emerald-700">
            <CheckCircle2 className="h-3.5 w-3.5" /> {hosted.length === 1 ? 'Its image is' : `All ${hosted.length} images are`} on the web, so mail apps show {hosted.length === 1 ? 'it' : 'them'}
          </span>
        )}
      </div>

      {embedded.length > 0 && (
        <div role="alert" className="flex flex-wrap items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600" />
          <span className="flex-1">
            {embedded.length === 1 ? 'An image is' : `${embedded.length} images are`} embedded in the HTML. Most mail apps, Gmail and Outlook among them, do not show those.
          </span>
          {!disabled && (
            <Button type="button" size="sm" onClick={() => void hostEmbedded()} disabled={busy} className="h-7 bg-amber-500 text-white hover:bg-amber-600">
              {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <UploadCloud className="h-3.5 w-3.5" />} Host {embedded.length === 1 ? 'it' : 'them'}
            </Button>
          )}
        </div>
      )}

      {unreachable.length > 0 && (
        <div role="alert" className="space-y-1.5 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-900">
          <p className="flex items-center gap-2 font-medium">
            <AlertTriangle className="h-4 w-4 shrink-0 text-rose-600" />
            {unreachable.length === 1 ? 'An image points' : `${unreachable.length} images point`} where no mail app can reach: upload the file for each.
          </p>
          {unreachable.map(({ src }) => (
            <div key={src} className="flex items-center gap-2 pl-6">
              <code className="min-w-0 flex-1 truncate rounded bg-white/70 px-1.5 py-0.5 font-mono text-[11px]" title={src}>{short(src)}</code>
              {!disabled && (
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  className="h-7 border-rose-200 bg-white"
                  disabled={busy}
                  onClick={() => { replacing.current = src; picker.current?.click(); }}
                >
                  <UploadCloud className="h-3.5 w-3.5" /> Upload it
                </Button>
              )}
            </div>
          ))}
        </div>
      )}

      <AnimatePresence initial={false}>
        {open && library && library.length > 0 && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="overflow-hidden"
          >
            <ul className="grid grid-cols-2 gap-2 rounded-xl border border-slate-200 bg-slate-50/60 p-2 sm:grid-cols-3 lg:grid-cols-4">
              {library.map((image) => (
                <li key={image.id} className="group overflow-hidden rounded-lg border border-slate-200 bg-white">
                  <div className="flex h-24 items-center justify-center bg-[repeating-conic-gradient(#f1f5f9_0_25%,#fff_0_50%)] bg-[length:16px_16px]">
                    <img src={image.url} alt={image.fileName} className="max-h-24 max-w-full object-contain" loading="lazy" />
                  </div>
                  <div className="space-y-1 p-1.5">
                    <p className="truncate text-[11px] text-slate-600" title={image.fileName}>{image.fileName}</p>
                    <div className="flex gap-1">
                      {!disabled && (
                        <Button type="button" size="sm" className="h-6 flex-1 px-1.5 text-[11px]" onClick={() => insert([image])}>
                          <Plus className="h-3 w-3" /> Insert
                        </Button>
                      )}
                      <Button type="button" size="sm" variant="outline" className="h-6 px-1.5" onClick={() => void copy(image.url)} aria-label={`Copy the link to ${image.fileName}`}>
                        <Copy className="h-3 w-3" />
                      </Button>
                      {!disabled && (
                        <Button type="button" size="sm" variant="outline" className="h-6 px-1.5 hover:text-rose-600" onClick={() => void remove(image)} aria-label={`Remove ${image.fileName}`}>
                          <Trash2 className="h-3 w-3" />
                        </Button>
                      )}
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
