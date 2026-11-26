import { ArrowRight, BookOpen, Clock, Play } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Breadcrumbs } from './components/Breadcrumbs';
import { CtaLink } from './components/CtaLink';
import { useSignupLabel } from './components/LaunchOffer';
import { CTA_COPY } from './ctaCopy';
import { MarketingLayout } from './components/MarketingLayout';
import { PageCard } from './components/PageCard';
import type { LanguageContent } from './content/types';
import { PAGE_META, SITE_STRINGS } from './meta';
import { GUIDE_PAGE_IDS, LANDING_PAGE_IDS, marketingPage, marketingPath } from './routes';
import { breadcrumbsFor } from './seo';
import type { SiteLang } from './site';

/**
 * The guides' index: every guide with what it answers and how long it takes to read, then the
 * topic pages. The page search engines and readers start from, and that links every guide.
 */
export function GuidesHub({ lang, content }: { lang: SiteLang; content: LanguageContent }) {
  const page = marketingPage('guides');
  const crumbs = breadcrumbsFor({ page, lang });
  const { template } = content;
  const signupLabel = useSignupLabel(lang);

  return (
    <MarketingLayout lang={lang} pageId="guides">
      <section className="relative overflow-hidden pt-32 pb-12 lg:pt-40 lg:pb-16">
        <div className="absolute inset-0 bg-gradient-to-br from-orange-50 via-white to-amber-50/60" aria-hidden="true" />
        <div className="relative w-full max-w-[1280px] mx-auto px-6 lg:px-12 text-center lg:text-left">
          <Breadcrumbs crumbs={crumbs} lang={lang} />
          <p className="inline-flex items-center gap-2 rounded-full border border-orange-200 bg-white px-4 py-2 text-sm font-semibold text-orange-800 shadow-sm mb-6">
            <BookOpen className="h-4 w-4" aria-hidden="true" />
            {template.guidesEyebrow}
          </p>
          <h1 className="text-4xl sm:text-5xl lg:text-6xl font-bold text-slate-900 mb-6 leading-[1.08] tracking-tight max-w-4xl">{template.guidesH1}</h1>
          <p className="text-lg sm:text-xl text-slate-700 leading-relaxed max-w-3xl mx-auto lg:mx-0">{template.guidesLead}</p>
        </div>
      </section>

      <section className="pb-20 bg-white">
        <ul className="w-full max-w-[1280px] mx-auto px-6 lg:px-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {GUIDE_PAGE_IDS.map((id) => {
            const guide = content.guides[id];
            const meta = PAGE_META[lang][id];
            return (
              <li key={id}>
                <Link
                  to={marketingPath(id, lang)}
                  data-track={`nav-${id}`}
                  className="group flex h-full flex-col rounded-3xl border border-slate-200 bg-white p-6 lg:p-7 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-orange-300 hover:shadow-xl"
                >
                  <span className="text-sm font-semibold text-orange-700">{guide.eyebrow}</span>
                  <h2 className="mt-2 text-xl font-bold text-slate-900 group-hover:text-orange-700 transition-colors">{guide.h1}</h2>
                  <p className="mt-3 flex-1 text-slate-700 leading-relaxed">{meta.description}</p>
                  <span className="mt-5 flex items-center justify-between text-sm">
                    <span className="inline-flex items-center gap-1.5 text-slate-500">
                      <Clock className="h-4 w-4" aria-hidden="true" />
                      {guide.readingMinutes} {template.minRead}
                    </span>
                    <span className="inline-flex items-center gap-2 font-semibold text-orange-700">
                      {SITE_STRINGS[lang].learnMore}
                      <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-1" aria-hidden="true" />
                    </span>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      </section>

      <section className="py-16 lg:py-20 bg-slate-50">
        <div className="w-full max-w-[1280px] mx-auto px-6 lg:px-12">
          <h2 className="text-3xl font-bold text-slate-900 mb-10">{SITE_STRINGS[lang].relatedHeading}</h2>
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {['features' as const, ...LANDING_PAGE_IDS.slice(0, 5)].map((id) => (
              <PageCard key={id} id={id} lang={lang} />
            ))}
          </div>
        </div>
      </section>

      {/* From reading to doing */}
      <section className="py-16 lg:py-20 bg-white">
        <div className="w-full max-w-4xl mx-auto px-6 lg:px-12 text-center">
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <CtaLink kind="signup" lang={lang} className="group bg-slate-900 hover:bg-slate-800 text-white px-8 py-4 rounded-2xl font-semibold text-lg flex items-center justify-center gap-3 shadow-xl">
              {signupLabel}
              <ArrowRight className="h-5 w-5 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
            </CtaLink>
            <CtaLink kind="try" lang={lang} className="bg-white hover:bg-slate-50 text-slate-900 px-8 py-4 rounded-2xl font-semibold text-lg border-2 border-slate-200 hover:border-orange-300 flex items-center justify-center gap-3 shadow-lg">
              <Play className="h-5 w-5 text-orange-600" aria-hidden="true" />
              {CTA_COPY[lang].tryDemo}
            </CtaLink>
          </div>
          <CtaLink kind="contact" lang={lang} className="mt-6 inline-flex items-center gap-2 font-semibold text-orange-700 hover:gap-3 transition-all">
            {SITE_STRINGS[lang].contactUs}
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </CtaLink>
        </div>
      </section>
    </MarketingLayout>
  );
}

export default GuidesHub;
