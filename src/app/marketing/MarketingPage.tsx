import { Link } from 'react-router-dom';
import { ArrowRight, CheckCircle2, Compass, MapPin, MessageSquare, Rocket, Sparkles } from 'lucide-react';
import { Breadcrumbs } from './components/Breadcrumbs';
import { CtaLink } from './components/CtaLink';
import { FaqList } from './components/FaqList';
import { FEATURE_ICON } from './components/featureIcons';
import { LaunchOfferBadge, useLaunchOfferActive, useSignupLabel } from './components/LaunchOffer';
import { LAUNCH_OFFER_COPY } from '@/app/lib/launchOffer';
import { MarketingLayout } from './components/MarketingLayout';
import { PageCard } from './components/PageCard';
import { RichText } from './components/RichText';
import type { PageContent, TemplateStrings } from './content/types';
import { PAGE_META, SITE_STRINGS } from './meta';
import { LANDING_PAGE_IDS, marketingPage, marketingPath, type MarketingPageId } from './routes';
import { breadcrumbsFor } from './seo';
import type { SiteLang } from './site';

export interface MarketingPageProps {
  id: MarketingPageId;
  lang: SiteLang;
  content: PageContent;
  template: TemplateStrings;
}

const container = 'w-full max-w-[1280px] mx-auto px-6 lg:px-12';

/**
 * One marketing page laid out from its words: hero with the H1, an introduction, the
 * topic's sections, the features, how it works, the FAQ (with its FAQPage data), related
 * pages and a closing call to action. In the home page's look, without its heavy motion,
 * so the page is quick and reads the same with or without JavaScript.
 */
export function MarketingPage({ id, lang, content, template }: MarketingPageProps) {
  const page = marketingPage(id);
  const strings = SITE_STRINGS[lang];
  const crumbs = breadcrumbsFor({ page, lang });
  const offer = useLaunchOfferActive();
  const signupLabel = useSignupLabel(lang);

  return (
    <MarketingLayout lang={lang} pageId={id}>
      {/* Hero */}
      <section className="relative overflow-hidden pt-32 pb-16 lg:pt-40 lg:pb-24">
        <div className="absolute inset-0 bg-gradient-to-br from-orange-50 via-white to-amber-50/60" aria-hidden="true" />
        <div className="absolute -top-24 -right-24 h-96 w-96 rounded-full bg-gradient-to-br from-orange-200/50 to-amber-200/40 blur-3xl" aria-hidden="true" />
        <div className={`${container} relative grid items-center gap-12 lg:grid-cols-[1.15fr_0.85fr] lg:gap-16`}>
          <div className="text-center lg:text-left">
            <Breadcrumbs crumbs={crumbs} lang={lang} />
            <div className="mb-4">
              <LaunchOfferBadge lang={lang} />
            </div>
            <p className="inline-flex items-center gap-2 rounded-full border border-orange-200 bg-white px-5 py-2.5 text-sm font-semibold text-orange-800 shadow-lg mb-8">
              <Sparkles className="h-4 w-4" aria-hidden="true" />
              {content.eyebrow}
            </p>
            <h1 className="text-4xl sm:text-5xl lg:text-6xl font-bold text-slate-900 mb-6 leading-[1.08] tracking-tight">
              {content.h1}
              {content.h1Accent && (
                <>
                  {' '}
                  <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-600 via-amber-500 to-orange-600">
                    {content.h1Accent}
                  </span>
                </>
              )}
            </h1>
            <p className="text-lg sm:text-xl text-slate-700 mb-10 max-w-2xl mx-auto lg:mx-0 leading-relaxed">
              <RichText text={content.lead} lang={lang} />
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center lg:justify-start mb-8">
              <CtaLink
                kind="signup"
                lang={lang}
                className="group bg-slate-900 hover:bg-slate-800 text-white px-8 py-4 rounded-2xl font-semibold text-lg flex items-center justify-center gap-3 shadow-xl hover:shadow-2xl transition-all duration-200"
              >
                <span>{signupLabel}</span>
                <ArrowRight className="w-5 h-5 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
              </CtaLink>
              <CtaLink
                kind="demo"
                lang={lang}
                className="bg-white hover:bg-slate-50 text-slate-900 px-8 py-4 rounded-2xl font-semibold text-lg border-2 border-slate-200 hover:border-orange-300 flex items-center justify-center gap-3 shadow-lg transition-all duration-200"
              >
                <MessageSquare className="w-5 h-5 text-orange-600" aria-hidden="true" />
                {strings.requestDemo}
              </CtaLink>
            </div>
            <p className="flex items-start justify-center lg:justify-start gap-2 text-sm text-slate-600 max-w-xl mx-auto lg:mx-0">
              <MapPin className="h-4 w-4 mt-0.5 shrink-0 text-orange-600" aria-hidden="true" />
              <span>
                <strong className="font-semibold text-slate-800">{strings.builtIn}.</strong> {strings.usedBy}
              </span>
            </p>
          </div>

          <aside className="relative" aria-label={content.heroCardTitle}>
            <div className="absolute inset-0 rotate-2 rounded-[2rem] bg-gradient-to-br from-orange-400 to-amber-500 shadow-2xl shadow-orange-500/30" aria-hidden="true" />
            <div className="relative rounded-[2rem] bg-gradient-to-br from-orange-600 to-amber-500 p-8 lg:p-10 text-white">
              <p className="text-sm font-semibold uppercase tracking-wider text-white/90 mb-6">{content.heroCardTitle}</p>
              <ul className="space-y-4">
                {content.heroPoints.map((point) => (
                  <li key={point} className="flex items-start gap-3">
                    <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-white/20">
                      <CheckCircle2 className="h-5 w-5 text-white" aria-hidden="true" />
                    </span>
                    <span className="text-base lg:text-lg font-medium leading-snug">{point}</span>
                  </li>
                ))}
              </ul>
            </div>
          </aside>
        </div>
      </section>

      {/* Introduction */}
      <section className="py-16 lg:py-24 bg-white">
        <div className={`${container} grid gap-8 lg:grid-cols-[0.85fr_1.15fr] lg:gap-16`}>
          <h2 className="text-3xl md:text-4xl font-bold text-slate-900 leading-tight">{content.intro.heading}</h2>
          <div className="space-y-5 text-lg text-slate-700 leading-relaxed">
            {content.intro.paragraphs.map((p) => (
              <p key={p}>
                <RichText text={p} lang={lang} />
              </p>
            ))}
          </div>
        </div>
      </section>

      {/* Every topic page, on a home page */}
      {content.showSolutions && (
        <section className="py-16 lg:py-24 bg-slate-50">
          <div className={container}>
            <div className="max-w-3xl mb-12">
              <p className="inline-flex items-center gap-2 rounded-full bg-orange-100 px-4 py-2 text-sm font-semibold text-orange-800 mb-5">
                <Compass className="h-4 w-4" aria-hidden="true" />
                {template.solutionsEyebrow}
              </p>
              <h2 className="text-3xl md:text-4xl font-bold text-slate-900 mb-4">{content.showSolutions.heading}</h2>
              <p className="text-lg text-slate-700">{content.showSolutions.intro}</p>
            </div>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {(['features', ...LANDING_PAGE_IDS] as MarketingPageId[]).map((pid) => (
                <PageCard key={pid} id={pid} lang={lang} />
              ))}
            </div>
          </div>
        </section>
      )}

      {/* The topic */}
      {content.sections.map((section, i) => (
        <section key={section.heading} className={`py-16 lg:py-20 ${i % 2 === (content.showSolutions ? 1 : 0) ? 'bg-slate-50' : 'bg-white'}`}>
          <div className={`${container} grid gap-8 lg:grid-cols-[0.85fr_1.15fr] lg:gap-16`}>
            <div>
              <h2 className="text-3xl font-bold text-slate-900 leading-tight">{section.heading}</h2>
            </div>
            <div>
              <div className="space-y-5 text-lg text-slate-700 leading-relaxed">
                {section.paragraphs.map((p) => (
                  <p key={p}>
                    <RichText text={p} lang={lang} />
                  </p>
                ))}
              </div>
              {section.bullets && (
                <ul className="mt-8 grid gap-3 sm:grid-cols-2">
                  {section.bullets.map((b) => (
                    <li key={b} className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                      <CheckCircle2 className="h-5 w-5 mt-0.5 shrink-0 text-emerald-600" aria-hidden="true" />
                      <span className="text-slate-800">
                        <RichText text={b} lang={lang} />
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              {section.link && (
                <Link
                  to={marketingPath(section.link, lang)}
                  data-track={`nav-${section.link}`}
                  className="mt-6 inline-flex items-center gap-2 font-semibold text-orange-700 hover:gap-3 transition-all"
                >
                  {strings.learnMore}: {pageName(section.link, lang)}
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </Link>
              )}
            </div>
          </div>
        </section>
      ))}

      {/* Features */}
      <section className="relative overflow-hidden py-20 lg:py-28 bg-slate-950 text-white">
        <div className="absolute top-0 left-1/4 h-[480px] w-[480px] rounded-full bg-gradient-to-br from-orange-500/20 to-amber-500/20 blur-[120px]" aria-hidden="true" />
        <div className="absolute bottom-0 right-1/4 h-[400px] w-[400px] rounded-full bg-gradient-to-br from-violet-500/15 to-purple-500/15 blur-[100px]" aria-hidden="true" />
        <div className={`${container} relative`}>
          <div className="text-center max-w-3xl mx-auto mb-14">
            <p className="inline-flex items-center gap-2 rounded-full border border-orange-500/30 bg-orange-500/10 px-5 py-2.5 text-sm font-semibold text-orange-300 mb-6">
              <Sparkles className="h-4 w-4" aria-hidden="true" />
              {template.featuresEyebrow}
            </p>
            <h2 className="text-3xl md:text-5xl font-bold mb-5">{content.features.heading}</h2>
            {content.features.intro && <p className="text-lg text-slate-300">{content.features.intro}</p>}
          </div>
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 lg:gap-6">
            {content.features.items.map((item) => {
              const Icon = FEATURE_ICON[item.icon];
              return (
                <li key={item.title} className="rounded-3xl border border-slate-800 bg-gradient-to-br from-slate-900 to-slate-800/80 p-6 lg:p-7 transition-colors hover:border-orange-500/40">
                  <span className="mb-5 flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 shadow-lg shadow-orange-500/20">
                    <Icon className="h-6 w-6 text-white" aria-hidden="true" />
                  </span>
                  <h3 className="text-lg font-bold text-white mb-2">{item.title}</h3>
                  <p className="text-slate-300 leading-relaxed">
                    <RichText text={item.text} lang={lang} />
                  </p>
                </li>
              );
            })}
          </ul>
        </div>
      </section>

      {/* How it works */}
      <section className="py-20 lg:py-28 bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 text-white">
        <div className={container}>
          <div className="text-center max-w-3xl mx-auto mb-14">
            <p className="inline-flex items-center gap-2 rounded-full border border-orange-500/30 bg-orange-500/20 px-4 py-2 text-sm font-semibold text-orange-200 mb-6">
              <Rocket className="h-4 w-4" aria-hidden="true" />
              {template.stepsEyebrow}
            </p>
            <h2 className="text-3xl md:text-5xl font-bold mb-5">{content.steps.heading}</h2>
            {content.steps.intro && <p className="text-lg text-slate-300">{content.steps.intro}</p>}
          </div>
          <ol className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
            {content.steps.items.map((step, i) => (
              <li key={step.title} className="relative rounded-2xl border border-white/10 bg-white/5 p-6 pt-10">
                <span
                  className={`absolute -top-6 left-6 flex h-12 w-12 items-center justify-center rounded-full bg-gradient-to-br text-lg font-bold text-white shadow-xl ${STEP_COLORS[i % STEP_COLORS.length]}`}
                  aria-hidden="true"
                >
                  {i + 1}
                </span>
                <h3 className="text-xl font-bold text-white mb-3">
                  <span className="sr-only">{template.stepLabel} {i + 1}: </span>
                  {step.title}
                </h3>
                <p className="text-slate-300 leading-relaxed">
                  <RichText text={step.text} lang={lang} />
                </p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      {/* FAQ */}
      <section className="relative py-20 lg:py-28 bg-white overflow-hidden">
        <div className="absolute top-0 right-0 h-full w-1/2 bg-gradient-to-l from-orange-50 to-transparent" aria-hidden="true" />
        <div className={`${container} relative grid gap-12 lg:grid-cols-5 lg:gap-20`}>
          <div className="lg:col-span-2 lg:sticky lg:top-32 lg:self-start">
            <p className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-orange-600 to-amber-500 px-4 py-2 text-sm font-semibold text-white mb-6">
              <MessageSquare className="h-4 w-4" aria-hidden="true" />
              {template.faqEyebrow}
            </p>
            <h2 className="text-3xl md:text-5xl font-bold text-slate-900 mb-6 leading-tight">{content.faq.heading}</h2>
            <CtaLink kind="contact" lang={lang} className="inline-flex items-center gap-2 font-semibold text-orange-700 hover:gap-3 transition-all">
              {strings.contactUs}
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </CtaLink>
          </div>
          <div className="lg:col-span-3">
            <FaqList items={content.faq.items} lang={lang} />
          </div>
        </div>
      </section>

      {/* Related pages */}
      {content.related.length > 0 && (
        <section className="py-16 lg:py-20 bg-slate-50">
          <div className={container}>
            <h2 className="text-3xl font-bold text-slate-900 mb-10">{strings.relatedHeading}</h2>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {content.related.map((rid) => (
                <PageCard key={rid} id={rid} lang={lang} />
              ))}
            </div>
          </div>
        </section>
      )}

      {/* Call to action */}
      <section className="relative overflow-hidden py-20 lg:py-28">
        <div className="absolute inset-0 bg-gradient-to-br from-orange-600 via-amber-500 to-orange-600" aria-hidden="true" />
        <div className="relative w-full max-w-4xl mx-auto px-6 lg:px-12 text-center">
          <h2 className="text-3xl md:text-5xl font-bold text-white mb-6">{content.cta.heading}</h2>
          <p className="text-xl text-white mb-10 max-w-2xl mx-auto">{content.cta.text}</p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <CtaLink
              kind="signup"
              lang={lang}
              className="group bg-white text-orange-800 px-10 py-5 rounded-2xl font-bold text-lg flex items-center justify-center gap-3 shadow-2xl hover:scale-105 transition-transform duration-300"
            >
              {signupLabel}
              <ArrowRight className="h-5 w-5 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
            </CtaLink>
            <CtaLink
              kind="demo"
              lang={lang}
              className="bg-slate-900/20 border-2 border-white/60 text-white px-10 py-5 rounded-2xl font-bold text-lg flex items-center justify-center gap-2 hover:bg-slate-900/30 transition-colors duration-300"
            >
              {strings.requestDemo}
            </CtaLink>
          </div>
          {offer && (
            <p className="mt-6 text-sm font-semibold text-white" data-launch-offer="">
              {LAUNCH_OFFER_COPY[lang].badge} · {LAUNCH_OFFER_COPY[lang].noCard}
            </p>
          )}
        </div>
      </section>
    </MarketingLayout>
  );
}

const STEP_COLORS = [
  'from-orange-500 to-red-500',
  'from-amber-500 to-orange-500',
  'from-emerald-500 to-teal-600',
  'from-violet-500 to-purple-600',
];

function pageName(id: MarketingPageId, lang: SiteLang): string {
  return PAGE_META[lang][id].nav;
}

export default MarketingPage;
