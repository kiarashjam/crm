import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { SITE_STRINGS } from '../meta';
import type { SiteLang } from '../site';

/** The visible trail the BreadcrumbList structured data describes. */
export function Breadcrumbs({ crumbs, lang }: { crumbs: { name: string; path: string }[]; lang: SiteLang }) {
  if (crumbs.length === 0) return null;
  return (
    <nav aria-label={SITE_STRINGS[lang].breadcrumb} className="mb-6">
      <ol className="flex flex-wrap items-center justify-center lg:justify-start gap-1 text-sm text-slate-600">
        {crumbs.map((crumb, i) => {
          const last = i === crumbs.length - 1;
          return (
            <li key={crumb.path} className="flex items-center gap-1">
              {i > 0 && <ChevronRight className="h-4 w-4 text-slate-400" aria-hidden="true" />}
              {last ? (
                <span aria-current="page" className="font-medium text-slate-800">
                  {crumb.name}
                </span>
              ) : (
                <Link to={crumb.path} className="hover:text-orange-700 underline-offset-4 hover:underline" data-track="nav-home">
                  {crumb.name}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

export default Breadcrumbs;
