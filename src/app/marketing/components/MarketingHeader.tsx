import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, Menu } from 'lucide-react';
import { PAGE_META, SITE_STRINGS } from '../meta';
import { LANDING_PAGE_IDS, marketingPath, type MarketingPageId } from '../routes';
import type { SiteLang } from '../site';
import { BrandMark } from './BrandMark';
import { CtaLink } from './CtaLink';
import { LAUNCH_OFFER_COPY } from '@/app/lib/launchOffer';
import { useLaunchOfferActive, useSignupLabel } from './LaunchOffer';
import { LanguageSwitch } from './LanguageSwitch';

/** The pages the header links to, in order. Every other page is in the menu and the footer. */
export const HEADER_PAGES: MarketingPageId[] = ['features', 'private-clubs', 'hospitality', 'sales', 'swiss', 'guides', 'pricing'];

/**
 * The public site's header: transparent over the hero, frosted white once the page scrolls,
 * as on the home page. Links to the main topic pages, the language switch, sign-in and
 * sign-up; below the xl breakpoint they fold into a menu that works without JavaScript.
 */
export function MarketingHeader({ lang, pageId }: { lang: SiteLang; pageId?: MarketingPageId }) {
  const strings = SITE_STRINGS[lang];
  const meta = PAGE_META[lang];
  const [scrolled, setScrolled] = useState(false);
  const menu = useRef<HTMLDetailsElement>(null);
  const offer = useLaunchOfferActive();
  const signupLabel = useSignupLabel(lang);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 50);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  const closeMenu = () => {
    if (menu.current) menu.current.open = false;
  };

  return (
    <header
      className={`w-full px-4 sm:px-6 lg:px-12 py-4 flex justify-between items-center gap-4 fixed top-0 left-0 right-0 z-50 transition-all duration-700 ${
        scrolled ? 'bg-white/85 backdrop-blur-xl border-b border-slate-200/50 shadow-lg shadow-slate-900/5' : 'bg-transparent'
      }`}
    >
      <Link
        to={marketingPath('home', lang)}
        className="flex items-center gap-3 group shrink-0"
        aria-label={`CadenceE – ${meta.home.nav}`}
        data-track="nav-home"
      >
        <BrandMark />
      </Link>

      <nav aria-label={strings.mainNav} className="hidden xl:block">
        <ul className="flex items-center gap-1 bg-slate-100/80 backdrop-blur-sm rounded-full p-1.5">
          {HEADER_PAGES.map((id) => {
            const current = id === pageId;
            return (
              // The guides' link only where the header has room for it (it is in the footer, and
              // on every topic page, everywhere): French and German names are long.
              <li key={id} className={id === 'guides' ? 'hidden 2xl:block' : undefined}>
                <Link
                  to={marketingPath(id, lang)}
                  data-track={`nav-${id}`}
                  aria-current={current ? 'page' : undefined}
                  className={`px-2.5 2xl:px-4 py-2 text-sm font-medium rounded-full whitespace-nowrap transition-all duration-300 block ${
                    current ? 'bg-white text-orange-700 shadow-sm' : 'text-slate-700 hover:text-orange-700 hover:bg-white'
                  }`}
                >
                  {strings.headerNav[id] ?? meta[id].nav}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      <div className="flex items-center gap-2 sm:gap-3">
        <LanguageSwitch lang={lang} pageId={pageId} className="hidden sm:block" />
        <CtaLink
          kind="login"
          lang={lang}
          className="hidden xl:flex whitespace-nowrap text-slate-700 hover:text-orange-700 font-semibold px-3 2xl:px-4 py-2.5 rounded-xl transition-colors"
        >
          {strings.signIn}
        </CtaLink>
        <CtaLink
          kind="signup"
          lang={lang}
          className="group hidden sm:flex whitespace-nowrap bg-gradient-to-r from-orange-600 to-amber-500 text-white px-4 2xl:px-5 py-2.5 rounded-xl font-semibold shadow-lg shadow-orange-500/25 hover:shadow-xl hover:shadow-orange-500/40 items-center gap-2 transition-shadow"
        >
          <span>{offer ? LAUNCH_OFFER_COPY[lang].headerCta : strings.headerCta}</span>
          <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
        </CtaLink>

        <details ref={menu} className="xl:hidden relative">
          <summary
            className="list-none [&::-webkit-details-marker]:hidden cursor-pointer flex items-center gap-2 rounded-xl border border-slate-200 bg-white/90 px-3 py-2.5 text-sm font-semibold text-slate-800 shadow-sm"
          >
            <Menu className="w-5 h-5" aria-hidden="true" />
            <span>{strings.menu}</span>
          </summary>
          <div
            className="absolute right-0 mt-3 w-[min(20rem,calc(100vw-2rem))] max-h-[calc(100vh-6rem)] overflow-y-auto rounded-2xl border border-slate-200 bg-white p-4 shadow-2xl"
            onClick={closeMenu}
          >
            <nav aria-label={strings.mainNav}>
              <ul className="space-y-1">
                {(['features', ...LANDING_PAGE_IDS, 'pricing'] as MarketingPageId[]).map((id) => (
                  <li key={id}>
                    <Link
                      to={marketingPath(id, lang)}
                      data-track={`nav-${id}`}
                      aria-current={id === pageId ? 'page' : undefined}
                      className={`block rounded-lg px-3 py-2 text-sm font-medium ${
                        id === pageId ? 'bg-orange-50 text-orange-800' : 'text-slate-700 hover:bg-slate-50 hover:text-orange-700'
                      }`}
                    >
                      {meta[id].nav}
                    </Link>
                  </li>
                ))}
              </ul>
            </nav>
            <div className="mt-4 border-t border-slate-100 pt-4 space-y-3">
              <LanguageSwitch lang={lang} pageId={pageId} className="sm:hidden" />
              <CtaLink kind="login" lang={lang} className="block rounded-lg px-3 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-50">
                {strings.signIn}
              </CtaLink>
              <CtaLink
                kind="signup"
                lang={lang}
                className="flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-orange-600 to-amber-500 px-4 py-3 text-sm font-semibold text-white"
              >
                {signupLabel}
                <ArrowRight className="w-4 h-4" aria-hidden="true" />
              </CtaLink>
            </div>
          </div>
        </details>
      </div>
    </header>
  );
}

export default MarketingHeader;
