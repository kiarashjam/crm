import { isLaunchOfferActive } from '@/app/lib/launchOffer';
import type { LanguageContent } from '../content/types';
import { MarketingPage } from '../MarketingPage';
import type { MarketingPageId } from '../routes';
import type { SiteLang } from '../site';

export interface LanguagePageProps {
  id: MarketingPageId;
}

/** The page component of one language: its words bound to the template. One chunk per language. */
export function languagePage(lang: SiteLang, content: LanguageContent) {
  function LanguagePage({ id }: LanguagePageProps) {
    const words = content.pages[id];
    if (!words) throw new Error(`No ${lang} content for marketing page "${id}"`);
    // The launch offer ends on 31 December 2026: a page about it then says what is true after.
    const page = words.afterOffer && !isLaunchOfferActive(Date.now()) ? { ...words, ...words.afterOffer } : words;
    return <MarketingPage id={id} lang={lang} content={page} template={content.template} />;
  }
  LanguagePage.displayName = `MarketingPage_${lang}`;
  return LanguagePage;
}
