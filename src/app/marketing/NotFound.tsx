import { useEffect, useLayoutEffect } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ArrowRight, SearchX } from 'lucide-react';
import { CtaLink } from './components/CtaLink';
import { MarketingLayout } from './components/MarketingLayout';
import { PAGE_META, SITE_STRINGS } from './meta';
import { LANDING_PAGE_IDS, langOfPath, marketingPath, type MarketingPageId } from './routes';
import { applyHead, notFoundHead } from './seo';
import type { SiteLang } from './site';

// A layout effect in the browser (the head is right before paint); none at build time.
const useHeadEffect = typeof window === 'undefined' ? useEffect : useLayoutEffect;

const SUGGESTED: MarketingPageId[] = ['features', ...LANDING_PAGE_IDS.slice(0, 5)];

/**
 * An address that is no page: said plainly, with links to real pages, in the address's
 * language. Never indexed. The server answers such addresses with 404 and this page,
 * prerendered (staticwebapp.config.json, responseOverrides).
 */
export function NotFound({ lang: forced }: { lang?: SiteLang }) {
  const { pathname } = useLocation();
  const lang = forced ?? langOfPath(pathname);
  const strings = SITE_STRINGS[lang];
  const meta = PAGE_META[lang];

  useHeadEffect(() => {
    applyHead(document, notFoundHead(lang));
  }, [lang, pathname]);

  return (
    <MarketingLayout lang={lang}>
      <section className="relative overflow-hidden pt-36 pb-24 lg:pt-44 lg:pb-32">
        <div className="absolute inset-0 bg-gradient-to-br from-orange-50 via-white to-amber-50/60" aria-hidden="true" />
        <div className="relative w-full max-w-3xl mx-auto px-6 text-center">
          <span className="mx-auto mb-8 flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-600 to-amber-500 shadow-xl shadow-orange-500/30">
            <SearchX className="h-8 w-8 text-white" aria-hidden="true" />
          </span>
          <p className="text-sm font-semibold uppercase tracking-wider text-orange-700 mb-4">404</p>
          <h1 className="text-4xl sm:text-5xl font-bold text-slate-900 mb-6 tracking-tight">{strings.notFoundHeading}</h1>
          <p className="text-lg text-slate-700 mb-10">{strings.notFoundText}</p>
          <ul className="grid gap-3 sm:grid-cols-2 text-left mb-10">
            {SUGGESTED.map((id) => (
              <li key={id}>
                <Link
                  to={marketingPath(id, lang)}
                  data-track={`nav-${id}`}
                  className="group flex items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white px-5 py-4 font-semibold text-slate-800 shadow-sm hover:border-orange-300 hover:text-orange-700"
                >
                  {meta[id].nav}
                  <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-1" aria-hidden="true" />
                </Link>
              </li>
            ))}
          </ul>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link
              to={marketingPath('home', lang)}
              data-track="nav-home"
              className="bg-slate-900 hover:bg-slate-800 text-white px-8 py-4 rounded-2xl font-semibold flex items-center justify-center gap-2"
            >
              {strings.notFoundHome}
            </Link>
            <CtaLink
              kind="login"
              lang={lang}
              className="bg-white text-slate-900 px-8 py-4 rounded-2xl font-semibold border-2 border-slate-200 hover:border-orange-300 flex items-center justify-center"
            >
              {strings.signIn}
            </CtaLink>
          </div>
        </div>
      </section>
    </MarketingLayout>
  );
}

export default NotFound;
