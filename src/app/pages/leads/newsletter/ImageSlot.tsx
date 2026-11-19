// One of the letter's images (the cover, a photo, the logo): its thumbnail, and a small picker
// of the workspace's email images (the welcome letter's among them) with an upload of a new
// one and a way to leave it out.

import { useRef, useState } from 'react';
import { ImageOff, ImagePlus, Loader2, UploadCloud, X } from 'lucide-react';
import {
  EMAIL_IMAGE_MAX_BYTES, EMAIL_IMAGE_TYPES, listEmailImages, uploadEmailImage, type EmailImage,
} from '@/app/api/emailImages';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';

/** The workspace's email images, read once for every slot of the letter. */
export function useEmailImageLibrary() {
  const [images, setImages] = useState<EmailImage[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const asked = useRef(false);
  const load = () => {
    if (asked.current) return;
    asked.current = true;
    listEmailImages()
      .then(setImages)
      .catch((e: unknown) => { setImages([]); setProblem(e instanceof Error ? e.message : 'Your images could not be read.'); });
  };
  const added = (image: EmailImage) => setImages((now) => [image, ...(now ?? [])]);
  return { images, problem, load, added };
}

export type EmailImageLibrary = ReturnType<typeof useEmailImageLibrary>;

/** A PNG is most often a logo, light on a transparent ground: shown on the footer's green. */
const isPng = (url: string) => /\.png(\?|$)/i.test(url);

export function ImageSlot({ label, value, onChange, library, suggestions = [], wide = false, dark = false }: {
  label: string;
  value: string | null | undefined;
  onChange: (next: string | null) => void;
  library: EmailImageLibrary;
  /** The welcome letter's images: offered first, uploaded here or not. */
  suggestions?: string[];
  /** Shown wider than tall (the cover, the logo). */
  wide?: boolean;
  /** Shown on the footer's green, as a light logo is. */
  dark?: boolean;
}) {
  const uploaded = library.images ?? [];
  const known = new Set(uploaded.map((i) => i.url));
  const choices = [
    ...suggestions.filter((url, i, all) => url && all.indexOf(url) === i && !known.has(url)).map((url) => ({ id: url, url, fileName: 'From your welcome letter' })),
    ...uploaded.map((i) => ({ id: i.id, url: i.url, fileName: i.fileName })),
  ];
  const [open, setOpen] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const picker = useRef<HTMLInputElement>(null);

  const upload = async (file: File | undefined) => {
    if (!file) return;
    if (!EMAIL_IMAGE_TYPES.includes(file.type)) { setProblem('Choose a PNG, JPEG, GIF or WebP image.'); return; }
    if (file.size > EMAIL_IMAGE_MAX_BYTES) { setProblem('That image is over 3 MB.'); return; }
    setUploading(true);
    setProblem(null);
    try {
      const image = await uploadEmailImage(file);
      library.added(image);
      onChange(image.url);
      setOpen(false);
    } catch (e) {
      setProblem(e instanceof Error ? e.message : 'The image could not be uploaded.');
    } finally {
      setUploading(false);
    }
  };

  return (
    <Popover open={open} onOpenChange={(next) => { setOpen(next); if (next) library.load(); }}>
      <div className="min-w-0">
        <PopoverTrigger asChild>
          <button
            type="button"
            aria-label={value ? `${label}: change the image` : `${label}: choose an image`}
            className={cn(
              'group relative block w-full overflow-hidden rounded-lg border transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
              wide ? 'aspect-[16/9]' : 'aspect-[4/3]',
              value
                ? dark ? 'border-transparent bg-[#2C5545]' : 'border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/5'
                : 'border-dashed border-slate-300 bg-slate-50 hover:border-orange-300 hover:bg-orange-50/50 dark:border-white/15 dark:bg-white/[0.03]',
            )}
          >
            {value ? (
              <>
                <img src={value} alt="" className={cn('h-full w-full', dark ? 'object-contain p-2' : 'object-cover')} />
                <span className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/60 to-transparent px-2 pb-1 pt-4 text-left text-[11px] font-medium text-white opacity-0 transition group-hover:opacity-100 group-focus-visible:opacity-100">
                  Change
                </span>
              </>
            ) : (
              <span className="flex h-full flex-col items-center justify-center gap-1 text-slate-400">
                <ImagePlus className="h-4 w-4" aria-hidden />
                <span className="text-[11px]">Add</span>
              </span>
            )}
          </button>
        </PopoverTrigger>
        <p className="mt-1 truncate text-center text-[11px] font-medium text-slate-500 dark:text-slate-400">{label}</p>
      </div>
      <PopoverContent align="start" className="w-80 p-3">
        <div className="mb-2 flex items-center gap-2">
          <p className="text-sm font-semibold text-slate-800 dark:text-slate-100">{label}</p>
          {value && (
            <button
              type="button"
              onClick={() => { onChange(null); setOpen(false); }}
              className="ml-auto inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-xs text-slate-500 hover:bg-slate-100 hover:text-rose-600 dark:hover:bg-white/5"
            >
              <X className="h-3 w-3" aria-hidden /> Leave out
            </button>
          )}
        </div>
        {library.images === null && choices.length === 0 ? (
          <div className="flex h-24 items-center justify-center text-slate-400"><Loader2 className="h-4 w-4 animate-spin" aria-label="Loading your images" /></div>
        ) : choices.length === 0 ? (
          <p className="flex items-center gap-2 rounded-lg bg-slate-50 p-3 text-xs text-slate-500 dark:bg-white/5">
            <ImageOff className="h-4 w-4 shrink-0" aria-hidden /> {library.problem ?? 'No images yet: upload one.'}
          </p>
        ) : (
          <ul className="grid max-h-56 grid-cols-3 gap-1.5 overflow-y-auto pr-0.5" aria-label="Your images">
            {choices.map((image) => (
              <li key={image.id}>
                <button
                  type="button"
                  onClick={() => { onChange(image.url); setOpen(false); }}
                  aria-label={image.fileName}
                  aria-pressed={value === image.url}
                  className={cn(
                    'block aspect-square w-full overflow-hidden rounded-md ring-offset-1 transition hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
                    value === image.url ? 'ring-2 ring-orange-500' : 'ring-1 ring-black/5',
                    (dark || isPng(image.url)) && 'bg-[#2C5545]',
                  )}
                >
                  <img src={image.url} alt="" className={cn('h-full w-full', dark || isPng(image.url) ? 'object-contain p-1.5' : 'object-cover')} loading="lazy" />
                </button>
              </li>
            ))}
          </ul>
        )}
        <button
          type="button"
          onClick={() => picker.current?.click()}
          disabled={uploading}
          className="mt-2 inline-flex w-full items-center justify-center gap-1.5 rounded-lg border border-dashed border-slate-300 py-2 text-xs font-medium text-slate-600 hover:border-orange-300 hover:text-orange-600 disabled:opacity-60 dark:border-white/15 dark:text-slate-300"
        >
          {uploading ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <UploadCloud className="h-3.5 w-3.5" aria-hidden />}
          Upload an image
        </button>
        <input
          ref={picker}
          type="file"
          accept={EMAIL_IMAGE_TYPES.join(',')}
          className="sr-only"
          aria-label={`Upload an image for ${label}`}
          onChange={(e) => { void upload(e.target.files?.[0]); e.target.value = ''; }}
        />
        {problem && <p role="alert" className="mt-2 text-xs text-rose-700">{problem}</p>}
      </PopoverContent>
    </Popover>
  );
}
