import { useState } from 'react';
import { useLocation } from 'react-router-dom';
import { HelpCircle, Lightbulb } from 'lucide-react';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription,
} from '@/app/components/ui/dialog';
import { cn } from '@/app/components/ui/utils';
import { resolvePageHelp } from './pageHelp/helpContent';

/**
 * Floating "?" button shown on every app page. Opens an illustrated modal
 * explaining what the current page can do (content resolved by route).
 */
export default function PageHelp() {
  const [open, setOpen] = useState(false);
  const { pathname } = useLocation();
  const help = resolvePageHelp(pathname);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={`Help: what can I do on the ${help.title} page?`}
        title="What can I do here?"
        className="fixed bottom-5 right-5 z-40 flex h-12 w-12 items-center justify-center rounded-full bg-gradient-to-br from-indigo-600 to-violet-600 text-white shadow-lg shadow-indigo-500/30 ring-1 ring-white/20 transition-transform hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-2 print:hidden"
      >
        <HelpCircle className="h-6 w-6" />
      </button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[88vh] overflow-y-auto sm:max-w-[580px]">
          <DialogHeader>
            <div className="flex items-start gap-4">
              <span className={cn('flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br text-3xl shadow-lg', help.gradient)}>
                {help.emoji}
              </span>
              <div className="min-w-0">
                <DialogTitle className="text-left text-xl">{help.title}</DialogTitle>
                <DialogDescription className="mt-1 text-left text-slate-600">{help.tagline}</DialogDescription>
              </div>
            </div>
          </DialogHeader>

          <div className="mt-1">
            <p className="mb-2 text-[11px] font-semibold uppercase tracking-wider text-slate-400">What you can do</p>
            <div className="grid gap-2.5 sm:grid-cols-2">
              {help.features.map((f) => (
                <div key={f.title} className="flex items-start gap-3 rounded-xl border border-slate-200 bg-white p-3">
                  <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-50 text-xl">
                    {f.emoji}
                  </span>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-800">{f.title}</p>
                    <p className="mt-0.5 text-xs leading-relaxed text-slate-500">{f.body}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {help.tips && help.tips.length > 0 && (
            <div className="mt-1 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2.5">
              <Lightbulb className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" aria-hidden />
              <ul className="space-y-0.5 text-xs text-amber-900">
                {help.tips.map((t) => <li key={t}>{t}</li>)}
              </ul>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
