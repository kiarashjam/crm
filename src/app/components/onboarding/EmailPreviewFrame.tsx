import { useEffect, useRef, useState } from 'react';
import { Mail } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';

/** A desktop mail app's reading pane, room for a 600 px email; a phone is a common handset's width. */
const DESKTOP_WIDTH = 640;
const PHONE_WIDTH = 375;

/**
 * The document the frame shows: a whole email document as it is (its own head and body,
 * so its styles and phone layout apply as in a mail app), a fragment in a plain page.
 * Links open elsewhere either way, and nothing it says is run.
 */
export function previewDocument(html: string): string {
  if (/<html[\s>]|<body[\s>]/i.test(html)) {
    const base = '<base target="_blank">';
    return /<head[\s>]/i.test(html) ? html.replace(/<head([^>]*)>/i, `<head$1>${base}`) : base + html;
  }
  return `<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><base target="_blank"><style>body{margin:0;padding:16px;background:#f8fafc;font-family:system-ui,sans-serif}img{max-width:100%;height:auto}</style></head><body>${html}</body></html>`;
}

/**
 * An email as the person will see it, in a sandbox: no script runs, no form posts, and
 * nothing in it can reach this page. The height follows the content; on "phone" it is
 * shown at a handset's width, where a design's phone layout takes over.
 */
export function EmailPreviewFrame({ subject, html, from, to, replyTo, className, device = 'desktop' }: {
  subject?: string;
  html: string;
  from?: string;
  to?: string;
  replyTo?: string;
  className?: string;
  device?: 'desktop' | 'phone';
}) {
  const frame = useRef<HTMLIFrameElement>(null);
  const box = useRef<HTMLDivElement>(null);
  const [height, setHeight] = useState(420);
  const [room, setRoom] = useState<number | null>(null);
  const doc = previewDocument(html);

  // Shown at the device's own width, made smaller to fit the column it is in.
  useEffect(() => {
    const el = box.current;
    if (!el || typeof ResizeObserver !== 'function') return;
    const observer = new ResizeObserver(([entry]) => setRoom(entry?.contentRect.width ?? null));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);
  const width = device === 'phone' ? PHONE_WIDTH : DESKTOP_WIDTH;
  const inset = device === 'phone' ? 32 : 0;
  const scale = room ? Math.min(1, (room - inset) / width) : 1;

  useEffect(() => {
    const el = frame.current;
    if (!el) return;
    const measure = () => {
      try {
        const body = el.contentDocument?.body;
        const page = el.contentDocument?.documentElement;
        if (body) setHeight(Math.min(Math.max(Math.max(body.scrollHeight, page?.scrollHeight ?? 0) + 8, 240), 1400));
      } catch {
        // A sandbox without same-origin cannot be measured; the default height stands.
      }
    };
    el.addEventListener('load', measure);
    // Images arrive after the document: measured again as they do.
    const later = window.setTimeout(measure, 1200);
    return () => { el.removeEventListener('load', measure); window.clearTimeout(later); };
  }, [doc, device]);

  return (
    <div className={cn('overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm dark:border-slate-700', className)}>
      <div className="space-y-1 border-b border-slate-100 bg-slate-50/80 px-4 py-3 text-xs dark:border-slate-700 dark:bg-slate-800/60">
        {from && <p className="text-slate-500"><span className="inline-block w-14 font-medium text-slate-400">From</span>{from}</p>}
        {to && <p className="text-slate-500"><span className="inline-block w-14 font-medium text-slate-400">To</span>{to}</p>}
        {replyTo && <p className="text-slate-500"><span className="inline-block w-14 font-medium text-slate-400">Reply to</span>{replyTo}</p>}
        <p className="flex items-center gap-1.5 text-sm font-semibold text-slate-800 dark:text-slate-100">
          <Mail className="h-3.5 w-3.5 text-orange-500" aria-hidden />
          {subject || <span className="font-normal italic text-slate-400">The portal's own subject</span>}
        </p>
      </div>
      {/* allow-same-origin only so the height can be read: scripts stay off, so nothing in it runs. */}
      <div ref={box} className={cn('overflow-hidden', device === 'phone' && 'bg-slate-100 py-4 dark:bg-slate-900')}>
        <div className="mx-auto" style={{ width: width * scale, height: height * scale }}>
          <iframe
            ref={frame}
            title="Email preview"
            sandbox="allow-same-origin allow-popups"
            srcDoc={doc}
            style={{ height, width, transform: scale < 1 ? `scale(${scale})` : undefined, transformOrigin: 'top left' }}
            className={cn('block border-0 bg-white', device === 'phone' && 'rounded-2xl shadow-lg ring-1 ring-slate-200')}
          />
        </div>
      </div>
    </div>
  );
}
