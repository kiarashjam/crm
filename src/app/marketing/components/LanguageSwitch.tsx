import { Link } from 'react-router-dom';
import { SITE_STRINGS } from '../meta';
import { marketingPath, type MarketingPageId } from '../routes';
import { SITE_LANGS, SITE_LANG_NAMES, type SiteLang } from '../site';

/**
 * EN · FR · DE: the same page in the other languages (or their home pages where the page
 * has no translation, as on the 404 page).
 */
export function LanguageSwitch({
  lang,
  pageId,
  tone = 'light',
  className = '',
}: {
  lang: SiteLang;
  pageId?: MarketingPageId;
  tone?: 'light' | 'dark';
  className?: string;
}) {
  return (
    <nav aria-label={SITE_STRINGS[lang].language} className={className}>
      <ul className={`flex items-center gap-0.5 rounded-full p-1 ${tone === 'dark' ? 'bg-white/10' : 'bg-slate-100/80'}`}>
        {SITE_LANGS.map((code) => {
          const current = code === lang;
          return (
            <li key={code}>
              <Link
                to={marketingPath(pageId ?? 'home', code)}
                hrefLang={code}
                lang={code}
                aria-label={`${SITE_LANG_NAMES[code]} (${code.toUpperCase()})`}
                aria-current={current ? 'true' : undefined}
                data-track={`nav-lang-${code}`}
                className={`block rounded-full px-2.5 py-1 text-xs font-bold tracking-wide transition-colors ${
                  current
                    ? tone === 'dark'
                      ? 'bg-white text-slate-900'
                      : 'bg-white text-orange-700 shadow-sm'
                    : tone === 'dark'
                      ? 'text-slate-300 hover:text-white'
                      : 'text-slate-600 hover:text-orange-700'
                }`}
              >
                {code.toUpperCase()}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

export default LanguageSwitch;
