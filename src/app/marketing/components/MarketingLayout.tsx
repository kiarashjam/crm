import type { ReactNode } from 'react';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { usePageLanguage } from '@/app/i18n';
import type { MarketingPageId } from '../routes';
import type { SiteLang } from '../site';
import { MarketingFooter } from './MarketingFooter';
import { MarketingHeader } from './MarketingHeader';
import { LaunchOfferStrip } from './LaunchOffer';
import { StickySignupBar } from './StickySignupBar';

/**
 * The frame of every public page: the header, the page's <main>, the launch offer (until
 * 31 December 2026; not repeated on the pricing page, which is about it), the footer, and on a
 * phone the bar that keeps "Start free" in reach. The page's
 * language is the address's (/fr/… French), and the app's language follows it while the
 * page is open, so <html lang> matches what is written.
 */
export function MarketingLayout({
  lang,
  pageId,
  children,
}: {
  lang: SiteLang;
  pageId?: MarketingPageId;
  children: ReactNode;
}) {
  usePageLanguage(lang);
  return (
    <div className="min-h-screen bg-white text-slate-900 overflow-x-clip">
      <MarketingHeader lang={lang} pageId={pageId} />
      <main id={MAIN_CONTENT_ID} className="w-full" tabIndex={-1}>
        {children}
      </main>
      {pageId !== 'pricing' && <LaunchOfferStrip lang={lang} />}
      <MarketingFooter lang={lang} pageId={pageId} />
      <StickySignupBar lang={lang} />
    </div>
  );
}

export default MarketingLayout;
