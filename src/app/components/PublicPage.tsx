// The frame of a page people outside the workspace open from a link: an application form,
// an enquiry form, an unsubscribe link, a pay link. One card on a plain background, and the
// language picker above it, so a visitor who cannot read the page as it opened can find
// their own language before anything else.

import type { ReactNode } from 'react';
import { LanguageSwitcher } from '@/app/i18n/LanguageSwitcher';

export function PublicPage({ children, wide = false }: { children: ReactNode; wide?: boolean }) {
  return (
    <main className="min-h-screen bg-slate-50 px-4 py-16">
      <div className={`mx-auto ${wide ? 'max-w-lg' : 'max-w-md'}`}>
        <div className="mb-3 flex justify-end">
          <LanguageSwitcher />
        </div>
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">{children}</div>
      </div>
    </main>
  );
}
