import { Link } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { PAGE_META, SITE_STRINGS } from '../meta';
import { marketingPath, type MarketingPageId } from '../routes';
import type { SiteLang } from '../site';

/** A card that leads to another page of the site: its name, what it covers, a link. */
export function PageCard({ id, lang, headingLevel = 3 }: { id: MarketingPageId; lang: SiteLang; headingLevel?: 2 | 3 }) {
  const meta = PAGE_META[lang][id];
  const Heading = headingLevel === 2 ? 'h2' : 'h3';
  return (
    <Link
      to={marketingPath(id, lang)}
      data-track={`nav-${id}`}
      className="group flex h-full flex-col rounded-3xl border border-slate-200 bg-white p-6 lg:p-7 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-orange-300 hover:shadow-xl"
    >
      <Heading className="text-xl font-bold text-slate-900 group-hover:text-orange-700 transition-colors">{meta.nav}</Heading>
      <p className="mt-3 flex-1 text-slate-700 leading-relaxed">{meta.description}</p>
      <span className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-orange-700">
        {SITE_STRINGS[lang].learnMore}
        <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-1" aria-hidden="true" />
      </span>
    </Link>
  );
}

export default PageCard;
