import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { toast } from 'sonner';
import { AlertTriangle, CheckCircle2, ChevronDown, Copy, ImageOff, ImagePlus, Images, Loader2, Plus, Replace, Search, Trash2, UploadCloud, Wrench, X } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { cn } from '@/app/components/ui/utils';
import { getApiBaseUrl, servesDemoData } from '@/app/api/apiClient';
import {
  EMAIL_IMAGE_MAX_BYTES, EMAIL_IMAGE_TYPES, deleteEmailImage, isOwnEmailImage, listEmailImages, uploadEmailImage,
  type EmailImage,
} from '@/app/api/emailImages';
import { insertAt } from './WelcomeFields';
import { bytesLabel, shrinkForEmail } from './shrinkImage';

/** Wider than this, an image overflows most mail apps' reading pane. */
const EMAIL_WIDTH = 600;

const escapeAttr = (s: string) => s.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');

/** The tag for an image in an email: sized for the reading pane, scaling down on a phone. */
export function imageTag(image: Pick<EmailImage, 'url' | 'fileName' | 'width'>): string {
  const alt = image.fileName.replace(/\.[a-z0-9]+$/i, '').replace(/[-_]+/g, ' ').trim();
  const width = image.width ? ` width="${Math.min(image.width, EMAIL_WIDTH)}"` : '';
  return `<img src="${escapeAttr(image.url)}" alt="${escapeAttr(alt)}"${width} style="display:block;max-width:100%;height:auto;border:0">`;
}

export type ImageSource = { src: string; alt?: string; kind: 'hosted' | 'embedded' | 'local' | 'unreachable' };

const LOOPBACK = /^https?:\/\/(localhost|127(?:\.\d{1,3}){3}|\[::1\])(:\d+)?\//i;

/**
 * One of CadenceE's own images given the address of the computer it was uploaded on
 * (the server's public address was not set then): the same image, at the API's address.
 * Null when it is not one, or when this page has no public API address to give it.
 */
export function publicAddressFor(src: string): string | null {
  if (!isOwnEmailImage(src) || !LOOPBACK.test(src)) return null;
  const base = (getApiBaseUrl() ?? '').trim().replace(/\/+$/, '');
  if (!/^https:\/\//i.test(base) || LOOPBACK.test(`${base}/`)) return null;
  return src.replace(/^https?:\/\/[^/]+/i, base);
}

const attribute = (tag: string, name: string) =>
  tag.match(new RegExp(`\\b${name}\\s*=\\s*(["'])(.*?)\\1`, 'is'))?.[2]?.trim();

/** Text as the HTML means it: "Pavillon 46 &middot; Lutry" reads "Pavillon 46 · Lutry". */
function decodeEntities(text: string): string {
  if (!text.includes('&') || typeof document === 'undefined') return text;
  const box = document.createElement('textarea');
  box.innerHTML = text;
  return box.value;
}

/**
 * Every image in the HTML, and whether a mail app can show it: one CadenceE hosts, or
 * any https address, it can; one embedded as data most block; one of CadenceE's with a
 * localhost address (uploaded before the server knew its own) only once corrected; one
 * on a computer, over plain http, or a cid: reference from another tool it cannot fetch.
 */
export function imageSources(html: string): ImageSource[] {
  const seen = new Map<string, ImageSource>();
  for (const match of html.matchAll(/<img\b[^>]*>/gis)) {
    const src = attribute(match[0], 'src');
    if (!src || src.includes('{{') || seen.has(src)) continue;
    const kind = isOwnEmailImage(src) && LOOPBACK.test(src)
      ? 'local'
      : isOwnEmailImage(src) || /^https:\/\//i.test(src)
        ? 'hosted'
        : /^data:image\//i.test(src) ? 'embedded' : 'unreachable';
    const alt = attribute(match[0], 'alt');
    seen.set(src, { src, alt: alt ? decodeEntities(alt) : undefined, kind });
  }
  return [...seen.values()];
}

/** What an image is called on its card: its alt text, else its file's name. */
function labelOf(source: ImageSource): string {
  if (source.alt) return source.alt;
  if (source.kind === 'embedded') return 'Embedded image';
  const name = source.src.split(/[?#]/)[0]!.split('/').pop() ?? '';
  return decodeURIComponent(name) || 'Image';
}

/** Why a file, as it will be uploaded, cannot be, or null. */
function problem(file: File): string | null {
  if (!EMAIL_IMAGE_TYPES.includes(file.type)) return `${file.name}: use a PNG, JPEG, GIF or WebP image. Mail apps do not show others, such as SVG or HEIC.`;
  if (file.size > EMAIL_IMAGE_MAX_BYTES) return `${file.name} is over 3 MB even made smaller. Save it as a JPEG about 1200 px wide, then add it again.`;
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
  // The image being exchanged for one from the library, while the library is open for it.
  const [picking, setPicking] = useState<ImageSource | null>(null);
  // Images whose address answered with nothing to show (removed from the library, say).
  const [broken, setBroken] = useState<ReadonlySet<string>>(new Set());
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
    // Anything that may be a picture is tried: a file the system gave no type is often one.
    const pictures = files.filter((f) => {
      const maybe = !f.type || f.type.startsWith('image/');
      if (!maybe) toast.error(`${f.name}: use a PNG, JPEG, GIF or WebP image.`);
      return maybe;
    });
    if (pictures.length === 0) return [];
    return track(async () => {
      const made: EmailImage[] = [];
      // One file at a time, each on its own: one that fails does not stop the others.
      for (const original of pictures) {
        try {
          // Made fit for an email first: a 5 MB export becomes a light JPEG 1200 px wide.
          const ready = await shrinkForEmail(original);
          const why = problem(ready.file);
          if (why) {
            toast.error(why);
            continue;
          }
          made.push(await uploadEmailImage(ready.file, { width: ready.width, height: ready.height }));
          if (ready.from) {
            toast.success(`${original.name} made lighter for email`, {
              description: `${ready.from.width} × ${ready.from.height}, ${bytesLabel(ready.from.bytes)} → ${ready.width} × ${ready.height}, ${bytesLabel(ready.file.size)}`,
            });
          }
        } catch (e) {
          toast.error(`${original.name} could not be uploaded`, {
            description: e instanceof Error && e.message ? e.message : 'Try again in a moment.',
          });
        }
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
  const local = sources.filter((s) => s.kind === 'local');
  const fixable = local.filter((s) => publicAddressFor(s.src));
  const hosted = sources.filter((s) => s.kind === 'hosted');
  const missing = hosted.filter((s) => broken.has(s.src));

  /** Every use of one address swapped for another, in the HTML as it is now. */
  const swap = (from: string, to: string) => write(latest.current.split(from).join(to));

  const correctLocal = () => {
    for (const s of fixable) swap(s.src, publicAddressFor(s.src)!);
    toast.success(fixable.length === 1 ? 'Its link now has the public address' : `${fixable.length} links now have the public address`);
  };

  /** The image's every use taken out of the HTML (the image stays in the library). */
  const takeOut = (source: ImageSource) => {
    const escaped = source.src.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    write(latest.current.replace(new RegExp(`[ \\t]*<img\\b[^>]*\\bsrc\\s*=\\s*(["'])${escaped}\\1[^>]*>[ \\t]*\\n?`, 'gi'), ''));
  };

  /** The image's first use selected in the HTML box, so it can be found and edited. */
  const find = (source: ImageSource) => {
    const box = document.getElementById(textareaId) as HTMLTextAreaElement | null;
    const at = box?.value.indexOf(source.src) ?? -1;
    if (!box || at < 0) return;
    box.focus();
    box.setSelectionRange(at, at + source.src.length);
    // Scrolled to the selection: the lines before it, at the box's line height.
    const lines = box.value.slice(0, at).split('\n').length;
    const lineHeight = parseFloat(getComputedStyle(box).lineHeight) || 16;
    box.scrollTop = Math.max(0, (lines - 3) * lineHeight);
  };

  const pickFromLibrary = (source: ImageSource) => {
    if (!library || library.length === 0) {
      toast.info('Your library has no images yet', { description: 'Use Replace to upload the photo for it instead.' });
      return;
    }
    setPicking(source);
    setOpen(true);
  };

  const use = (image: EmailImage) => {
    if (!picking) return;
    swap(picking.src, image.url);
    toast.success(`${labelOf(picking)} exchanged for ${image.fileName}`);
    setPicking(null);
  };

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
        accept="image/*"
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
          <Button type="button" size="sm" variant="ghost" onClick={() => { setPicking(null); setOpen((o) => !o); }} aria-expanded={open}>
            <Images className="h-4 w-4" /> Your images ({library.length})
            <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', open && 'rotate-180')} />
          </Button>
        )}
        {!disabled && <span className="text-[11px] text-slate-500">or paste or drop an image onto the HTML</span>}
        {hosted.length > 0 && embedded.length === 0 && unreachable.length === 0 && local.length === 0 && missing.length === 0 && (
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

      {local.length > 0 && (
        <div role="alert" className="flex flex-wrap items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600" />
          <span className="flex-1">
            {local.length === 1 ? 'An image\'s link points' : `${local.length} images' links point`} at localhost: uploaded before the server knew its public address, so no mail app can fetch {local.length === 1 ? 'it' : 'them'}.
            {fixable.length > 0 ? ' The images are on the server; only the links need correcting (saving does it too).' : ''}
          </span>
          {!disabled && fixable.length > 0 && (
            <Button type="button" size="sm" onClick={correctLocal} className="h-7 bg-amber-500 text-white hover:bg-amber-600">
              <Wrench className="h-3.5 w-3.5" /> Correct {fixable.length === 1 ? 'it' : 'them'}
            </Button>
          )}
        </div>
      )}

      {(unreachable.length > 0 || missing.length > 0) && (
        <p role="alert" className="flex items-start gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-900">
          <AlertTriangle className="mt-px h-4 w-4 shrink-0 text-rose-600" />
          <span>
            {unreachable.length + missing.length === 1 ? 'An image' : `${unreachable.length + missing.length} images`} cannot be shown
            {unreachable.length > 0 ? ' (an address no mail app can reach)' : ''}{missing.length > 0 ? ' (gone from the server)' : ''}:
            replace {unreachable.length + missing.length === 1 ? 'it' : 'them'} below, with a new upload or one of your images.
          </span>
        </p>
      )}

      {sources.length > 0 && (
        <section aria-label="Images in this email" className="rounded-xl border border-slate-200 bg-white p-2 dark:border-slate-700 dark:bg-slate-900">
          <p className="px-1 pb-1.5 text-[11px] font-semibold uppercase tracking-wide text-slate-500">Images in this email ({sources.length})</p>
          <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
            {sources.map((source) => {
              const shown = source.kind === 'local' ? publicAddressFor(source.src) : source.kind === 'unreachable' ? null : source.src;
              const gone = broken.has(source.src) || (shown != null && broken.has(shown));
              const label = labelOf(source);
              const status = gone
                ? { text: 'Cannot be loaded', tone: 'text-rose-700' }
                : source.kind === 'hosted' ? { text: 'Shown in mail apps', tone: 'text-emerald-700' }
                  : source.kind === 'local' ? { text: 'Link points at localhost', tone: 'text-amber-700' }
                    : source.kind === 'embedded' ? { text: 'Embedded: host it', tone: 'text-amber-700' }
                      : { text: 'Not on the web', tone: 'text-rose-700' };
              return (
                <li
                  key={source.src}
                  className={cn(
                    'overflow-hidden rounded-lg border bg-white dark:bg-slate-900',
                    picking?.src === source.src ? 'border-orange-400 ring-2 ring-orange-200' : gone || source.kind === 'unreachable' ? 'border-rose-200' : 'border-slate-200',
                  )}
                >
                  {/* A mid-grey checkerboard: a cream logo shows on it as well as a dark photo. */}
                  <div className="relative flex h-24 items-center justify-center bg-[repeating-conic-gradient(#cbd5e1_0_25%,#e2e8f0_0_50%)] bg-[length:16px_16px]">
                    {shown && !gone ? (
                      <img
                        src={shown}
                        alt={label}
                        className="max-h-24 max-w-full object-contain"
                        loading="lazy"
                        onError={() => setBroken((b) => new Set(b).add(source.src))}
                      />
                    ) : (
                      <span className="flex flex-col items-center gap-1 px-2 text-center text-[10px] text-rose-600">
                        <ImageOff className="h-5 w-5" aria-hidden />
                        <span className="line-clamp-2 break-all font-mono" title={source.src}>{short(source.src)}</span>
                      </span>
                    )}
                  </div>
                  <div className="space-y-1 p-1.5">
                    <p className="truncate text-[11px] font-medium text-slate-700 dark:text-slate-200" title={label}>{label}</p>
                    <p className={cn('text-[10px] font-medium', status.tone)}>{status.text}</p>
                    {!disabled && (
                      <div className="space-y-1">
                        <Button
                          type="button"
                          size="sm"
                          className="h-6 w-full px-1.5 text-[11px]"
                          disabled={busy}
                          aria-label={`Replace ${label} with a new upload`}
                          onClick={() => { replacing.current = source.src; picker.current?.click(); }}
                        >
                          <UploadCloud className="h-3 w-3" /> Replace
                        </Button>
                        <div className="grid grid-cols-3 gap-1">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          className="h-6 w-full px-1.5"
                          aria-label={`Exchange ${label} for one of your images`}
                          title="Exchange for one of your images"
                          onClick={() => pickFromLibrary(source)}
                        >
                          <Replace className="h-3 w-3" />
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          className="h-6 w-full px-1.5"
                          aria-label={`Find ${label} in the HTML`}
                          title="Find it in the HTML"
                          onClick={() => find(source)}
                        >
                          <Search className="h-3 w-3" />
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          className="h-6 w-full px-1.5 hover:text-rose-600"
                          aria-label={`Take ${label} out of the email`}
                          title="Take it out of the email"
                          onClick={() => takeOut(source)}
                        >
                          <X className="h-3 w-3" />
                        </Button>
                        </div>
                      </div>
                    )}
                  </div>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      <AnimatePresence initial={false}>
        {open && library && library.length > 0 && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="overflow-hidden"
          >
            {picking && (
              <div className="mb-1.5 flex items-center gap-2 rounded-lg bg-orange-50 px-3 py-1.5 text-xs text-orange-900">
                <Replace className="h-3.5 w-3.5 shrink-0" aria-hidden />
                <span className="flex-1">Choose the image to put in place of <strong>{labelOf(picking)}</strong>.</span>
                <Button type="button" size="sm" variant="ghost" className="h-6 px-2 text-[11px]" onClick={() => setPicking(null)}>Cancel</Button>
              </div>
            )}
            <ul className="grid grid-cols-2 gap-2 rounded-xl border border-slate-200 bg-slate-50/60 p-2 sm:grid-cols-3 lg:grid-cols-4">
              {library.map((image) => (
                <li key={image.id} className="group overflow-hidden rounded-lg border border-slate-200 bg-white">
                  <div className="flex h-24 items-center justify-center bg-[repeating-conic-gradient(#f1f5f9_0_25%,#fff_0_50%)] bg-[length:16px_16px]">
                    <img src={image.url} alt={image.fileName} className="max-h-24 max-w-full object-contain" loading="lazy" />
                  </div>
                  <div className="space-y-1 p-1.5">
                    <p className="truncate text-[11px] text-slate-600" title={image.fileName}>
                      {html.includes(image.url) && <span className="mr-1 rounded bg-emerald-100 px-1 text-[10px] font-medium text-emerald-700">In email</span>}
                      {image.fileName}
                    </p>
                    <div className="flex gap-1">
                      {!disabled && (picking ? (
                        <Button type="button" size="sm" className="h-6 flex-1 bg-orange-500 px-1.5 text-[11px] text-white hover:bg-orange-600" onClick={() => use(image)}>
                          <Replace className="h-3 w-3" /> Use this
                        </Button>
                      ) : (
                        <Button type="button" size="sm" className="h-6 flex-1 px-1.5 text-[11px]" onClick={() => insert([image])}>
                          <Plus className="h-3 w-3" /> Insert
                        </Button>
                      ))}
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
