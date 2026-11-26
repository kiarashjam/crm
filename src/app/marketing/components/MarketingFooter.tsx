import { Link } from 'react-router-dom';
import { PAGE_META, SITE_STRINGS } from '../meta';
import { MARKETING_PAGES, marketingPath, type MarketingPageId } from '../routes';
import { SITE_LANGS, SITE_LANG_NAMES, type SiteLang } from '../site';
import { BrandMark } from './BrandMark';
import { CtaLink } from './CtaLink';
import { useLaunchOfferActive } from './LaunchOffer';

export const FOOTER_PRODUCT: MarketingPageId[] = ['features', 'pricing', 'ai', 'e-signature', 'email', 'billing'];
export const FOOTER_SOLUTIONS: MarketingPageId[] = ['private-clubs', 'membership', 'hospitality', 'sales', 'swiss', 'guides'];
/** The pages about the launch offer (the free-CRM page): in the Product column, after Pricing, only while it runs. */
export const FOOTER_OFFER: MarketingPageId[] = MARKETING_PAGES.filter((p) => p.offerOnly).map((p) => p.id);

/** The Product column at a moment: with the launch offer's pages while it runs. */
export function footerProduct(offer: boolean): MarketingPageId[] {
  if (!offer) return FOOTER_PRODUCT;
  const after = FOOTER_PRODUCT.indexOf('pricing') + 1;
  return [...FOOTER_PRODUCT.slice(0, after), ...FOOTER_OFFER, ...FOOTER_PRODUCT.slice(after)];
}

const linkClass = 'hover:text-white transition-colors inline-block py-0.5';

/** The public site's footer: every topic page, the app's public pages, and the languages. */
export function MarketingFooter({ lang, pageId }: { lang: SiteLang; pageId?: MarketingPageId }) {
  const strings = SITE_STRINGS[lang];
  const meta = PAGE_META[lang];
  const year = new Date().getFullYear();
  const offer = useLaunchOfferActive();

  const pageLinks = (ids: MarketingPageId[]) => (
    <ul className="space-y-2.5">
      {ids.map((id) => (
        // A link to a page about the offer is hidden before the first paint if HTML built in
        // 2026 is served after it has ended (index.html hides [data-launch-offer]).
        <li key={id} data-launch-offer={FOOTER_OFFER.includes(id) ? '' : undefined}>
          <Link to={marketingPath(id, lang)} className={linkClass} data-track={`nav-${id}`}>
            {meta[id].nav}
          </Link>
        </li>
      ))}
    </ul>
  );

  return (
    <footer className="bg-slate-900 text-slate-300 py-16 lg:py-20">
      <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12">
        <div className="grid gap-10 md:grid-cols-2 lg:grid-cols-6 mb-14">
          <div className="lg:col-span-2">
            <Link to={marketingPath('home', lang)} className="inline-flex group mb-6" aria-label={`CadenceE – ${meta.home.nav}`} data-track="nav-home">
              <BrandMark dark rings={false} />
            </Link>
            <p className="max-w-xs text-slate-300 mb-4">{strings.footerTagline}</p>
            <p className="max-w-xs text-sm text-slate-400">{strings.usedBy}</p>
          </div>

          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-white mb-4">{strings.footerProduct}</h2>
            {pageLinks(footerProduct(offer))}
          </div>

          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-white mb-4">{strings.footerSolutions}</h2>
            {pageLinks(FOOTER_SOLUTIONS)}
          </div>

          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-white mb-4">{strings.footerResources}</h2>
            <ul className="space-y-2.5">
              <li>
                <CtaLink kind="login" lang={lang} className={linkClass}>
                  {strings.signIn}
                </CtaLink>
              </li>
              <li>
                <CtaLink kind="signup" lang={lang} className={linkClass}>
                  {strings.getStarted}
                </CtaLink>
              </li>
              <li>
                <CtaLink kind="contact" lang={lang} className={linkClass}>
                  {strings.contactUs}
                </CtaLink>
              </li>
              <li>
                <Link to="/help" className={linkClass} data-track="nav-help">
                  {strings.help}
                </Link>
              </li>
              <li>
                <Link to="/privacy" className={linkClass} data-track="nav-privacy">
                  {strings.privacy}
                </Link>
              </li>
              <li>
                <Link to="/terms" className={linkClass} data-track="nav-terms">
                  {strings.terms}
                </Link>
              </li>
            </ul>
          </div>

          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-white mb-4">{strings.footerLanguages}</h2>
            <ul className="space-y-2.5">
              {SITE_LANGS.map((code) => (
                <li key={code}>
                  <Link
                    to={marketingPath(pageId ?? 'home', code)}
                    hrefLang={code}
                    lang={code}
                    className={linkClass}
                    aria-current={code === lang ? 'true' : undefined}
                    data-track={`nav-lang-${code}`}
                  >
                    {SITE_LANG_NAMES[code]}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        </div>

        <div className="border-t border-slate-800 pt-8 flex flex-col md:flex-row justify-between items-center gap-4 text-sm text-slate-400">
          <p>
            &copy; {year} CadenceE. {strings.rights}
          </p>
          <p>{strings.builtIn}</p>
        </div>
      </div>
    </footer>
  );
}

export default MarketingFooter;
