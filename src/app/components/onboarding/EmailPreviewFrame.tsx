import { useEffect, useRef, useState } from 'react';
import { Mail } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';

/**
 * An email as the person will see it, in a sandbox: no script runs, no form posts, and
 * nothing in it can reach this page. The height follows the content.
 */
export function EmailPreviewFrame({ subject, html, from, to, replyTo, className }: {
  subject?: string;
  html: string;
  from?: string;
  to?: string;
  replyTo?: string;
  className?: string;
}) {
  const frame = useRef<HTMLIFrameElement>(null);
  const [height, setHeight] = useState(420);

  // The document is the email's own: base styles only where it has none, links open
  // elsewhere, and nothing it says is run.
  const doc = `<!doctype html><html><head><meta charset="utf-8"><base target="_blank"><style>body{margin:0;padding:16px;background:#f8fafc;font-family:system-ui,sans-serif}img{max-width:100%;height:auto}</style></head><body>${html}</body></html>`;

  useEffect(() => {
    const el = frame.current;
    if (!el) return;
    const measure = () => {
      try {
        const body = el.contentDocument?.body;
        if (body) setHeight(Math.min(Math.max(body.scrollHeight + 8, 240), 900));
      } catch {
        // A sandbox without same-origin cannot be measured; the default height stands.
      }
    };
    el.addEventListener('load', measure);
    return () => el.removeEventListener('load', measure);
  }, [doc]);

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
      <iframe
        ref={frame}
        title="Email preview"
        sandbox="allow-same-origin allow-popups"
        srcDoc={doc}
        style={{ height }}
        className="block w-full border-0 bg-white"
      />
    </div>
  );
}
