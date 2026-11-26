import { ArrowRight, BookOpen, CheckCircle2, Clock, Lightbulb, ListChecks, MessageSquare, Play } from 'lucide-react';
import { LAUNCH_OFFER_COPY } from '@/app/lib/launchOffer';
import { Breadcrumbs } from './components/Breadcrumbs';
import { CtaLink } from './components/CtaLink';
import { FaqList } from './components/FaqList';
import { useLaunchOfferActive, useSignupLabel } from './components/LaunchOffer';
import { MarketingLayout } from './components/MarketingLayout';
import { PageCard } from './components/PageCard';
import { RichText } from './components/RichText';
import type { GuideContent, GuideSection } from './content/guides/types';
import type { TemplateStrings } from './content/types';
import { CTA_COPY } from './ctaCopy';
import { SITE_STRINGS } from './meta';
import { GUIDE_PAGE_IDS, marketingPage, type GuideId, type MarketingPageId } from './routes';
import { breadcrumbsFor } from './seo';
import type { SiteLang } from './site';

const LOCALES: Record<SiteLang, string> = { en: 'en-GB', fr: 'fr-CH', de: 'de-CH' };

/** "26 November 2026", in the page's language; the same at build and in the browser. */
export function guideDate(iso: string, lang: SiteLang): string {
  return new Intl.DateTimeFormat(LOCALES[lang], { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${iso}T00:00:00Z`));
}

export interface GuidePageProps {
  id: GuideId;
  lang: SiteLang;
  guide: GuideContent;
  template: TemplateStrings;
}

/**
 * One guide, laid out as an article: the H1 and what the reader will learn, the guide in brief,
 * its contents, the sections (H2, H3, steps, tips), CadenceE where it helps, the FAQ (with its
 * FAQPage data), the topic pages it goes deeper on and the other guides. Plain HTML that reads
 * the same with or without JavaScript, so it is quick and every word is in the prerender.
 */
export function GuidePage({ id, lang, guide, template }: GuidePageProps) {
  const page = marketingPage(id);
  const crumbs = breadcrumbsFor({ page, lang });
  const middle = Math.max(1, Math.floor(guide.sections.length / 2));
  const otherGuides = GUIDE_PAGE_IDS.filter((g) => g !== id).slice(0, 6);

  return (
    <MarketingLayout lang={lang} pageId={id}>
      <article>
        {/* Header */}
        <header className="relative overflow-hidden pt-32 pb-12 lg:pt-40 lg:pb-16">
          <div className="absolute inset-0 bg-gradient-to-br from-orange-50 via-white to-amber-50/60" aria-hidden="true" />
          <div className="relative w-full max-w-4xl mx-auto px-6 lg:px-12 text-center lg:text-left">
            <Breadcrumbs crumbs={crumbs} lang={lang} />
            <p className="inline-flex items-center gap-2 rounded-full border border-orange-200 bg-white px-4 py-2 text-sm font-semibold text-orange-800 shadow-sm mb-6">
              <BookOpen className="h-4 w-4" aria-hidden="true" />
              {guide.eyebrow}
            </p>
            <h1 className="text-3xl sm:text-4xl lg:text-5xl font-bold text-slate-900 mb-6 leading-[1.12] tracking-tight">{guide.h1}</h1>
            <p className="text-lg sm:text-xl text-slate-700 leading-relaxed mb-6">
              <RichText text={guide.lead} lang={lang} />
            </p>
            <p className="flex flex-wrap items-center justify-center lg:justify-start gap-x-4 gap-y-1 text-sm text-slate-600">
              <span className="inline-flex items-center gap-1.5">
                <Clock className="h-4 w-4" aria-hidden="true" />
                {guide.readingMinutes} {template.minRead}
              </span>
              <span aria-hidden="true">·</span>
              <span>
                {template.updated} <time dateTime={page.lastmod}>{guideDate(page.lastmod, lang)}</time>
              </span>
            </p>
          </div>
        </header>

        <div className="w-full max-w-6xl mx-auto px-6 lg:px-12 pb-16 lg:grid lg:grid-cols-[240px_minmax(0,1fr)] lg:gap-12">
          {/* Contents: a column beside the text on a wide screen, a disclosure above it on a phone */}
          <nav aria-label={template.onThisPage} className="mb-10 lg:mb-0">
            <details className="group rounded-2xl border border-slate-200 bg-white p-5 shadow-sm lg:sticky lg:top-28 lg:border-0 lg:p-0 lg:shadow-none" open>
              <summary className="cursor-pointer list-none [&::-webkit-details-marker]:hidden text-sm font-semibold uppercase tracking-wider text-slate-500">
                {template.onThisPage}
              </summary>
              <ol className="mt-4 space-y-2 text-sm">
                {guide.sections.map((section) => (
                  <li key={section.id}>
                    <a href={`#${section.id}`} className="block text-slate-700 hover:text-orange-700 leading-snug">
                      {section.heading}
                    </a>
                  </li>
                ))}
                <li>
                  <a href="#faq" className="block text-slate-700 hover:text-orange-700 leading-snug">
                    {guide.faq.heading}
                  </a>
                </li>
              </ol>
            </details>
          </nav>

          <div className="min-w-0 max-w-3xl">
            {/* In brief */}
            <aside aria-labelledby="key-takeaways" className="mb-12 rounded-3xl border border-emerald-200 bg-emerald-50/70 p-6 lg:p-8">
              <h2 id="key-takeaways" className="flex items-center gap-2 text-lg font-bold text-emerald-900 mb-4">
                <ListChecks className="h-5 w-5" aria-hidden="true" />
                {template.keyTakeaways}
              </h2>
              <ul className="space-y-3">
                {guide.keyTakeaways.map((point) => (
                  <li key={point} className="flex items-start gap-3 text-slate-800 leading-relaxed">
                    <CheckCircle2 className="h-5 w-5 mt-0.5 shrink-0 text-emerald-600" aria-hidden="true" />
                    <span>
                      <RichText text={point} lang={lang} />
                    </span>
                  </li>
                ))}
              </ul>
            </aside>

            {guide.sections.map((section, i) => (
              <div key={section.id}>
                <GuideSectionBlock section={section} lang={lang} template={template} />
                {i === middle - 1 && <GuideCtaBox guide={guide} lang={lang} />}
              </div>
            ))}

            {/* FAQ */}
            <section aria-labelledby="faq" className="mt-16">
              <h2 id="faq" className="scroll-mt-28 flex items-center gap-3 text-2xl md:text-3xl font-bold text-slate-900 mb-8">
                <MessageSquare className="h-6 w-6 text-orange-600" aria-hidden="true" />
                {guide.faq.heading}
              </h2>
              <FaqList items={guide.faq.items} lang={lang} />
              <CtaLink kind="contact" lang={lang} className="mt-8 inline-flex items-center gap-2 font-semibold text-orange-700 hover:gap-3 transition-all">
                {SITE_STRINGS[lang].contactUs}
                <ArrowRight className="h-4 w-4" aria-hidden="true" />
              </CtaLink>
            </section>
          </div>
        </div>
      </article>

      {/* The topic pages this guide goes deeper on */}
      {page.topics && page.topics.length > 0 && (
        <section className="py-16 lg:py-20 bg-slate-50">
          <div className="w-full max-w-[1280px] mx-auto px-6 lg:px-12">
            <h2 className="text-3xl font-bold text-slate-900 mb-10">{SITE_STRINGS[lang].relatedHeading}</h2>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {page.topics.slice(0, 3).map((topic: MarketingPageId) => (
                <PageCard key={topic} id={topic} lang={lang} />
              ))}
            </div>
          </div>
        </section>
      )}

      {/* The other guides */}
      <section className="py-16 lg:py-20 bg-white">
        <div className="w-full max-w-[1280px] mx-auto px-6 lg:px-12">
          <h2 className="text-3xl font-bold text-slate-900 mb-10">{template.moreGuides}</h2>
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {otherGuides.map((other) => (
              <PageCard key={other} id={other} lang={lang} />
            ))}
          </div>
        </div>
      </section>

      <GuideClosingCta guide={guide} lang={lang} />
    </MarketingLayout>
  );
}

function GuideSectionBlock({ section, lang, template }: { section: GuideSection; lang: SiteLang; template: TemplateStrings }) {
  return (
    <section aria-labelledby={section.id} className="mb-14">
      <h2 id={section.id} className="scroll-mt-28 text-2xl md:text-3xl font-bold text-slate-900 mb-5 leading-tight">
        {section.heading}
      </h2>
      <div className="space-y-5 text-lg text-slate-700 leading-relaxed">
        {section.paragraphs.map((p) => (
          <p key={p}>
            <RichText text={p} lang={lang} />
          </p>
        ))}
      </div>
      {section.bullets && (
        <ul className="mt-6 space-y-3">
          {section.bullets.map((b) => (
            <li key={b} className="flex items-start gap-3 text-lg text-slate-700 leading-relaxed">
              <CheckCircle2 className="h-5 w-5 mt-1 shrink-0 text-orange-600" aria-hidden="true" />
              <span>
                <RichText text={b} lang={lang} />
              </span>
            </li>
          ))}
        </ul>
      )}
      {section.steps && (
        <ol className="mt-8 space-y-5">
          {section.steps.map((step, i) => (
            <li key={step.title} className="flex items-start gap-4">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-orange-500 to-amber-500 text-sm font-bold text-white shadow" aria-hidden="true">
                {i + 1}
              </span>
              <div>
                <h3 className="text-lg font-bold text-slate-900">
                  <span className="sr-only">{template.stepLabel} {i + 1}: </span>
                  {step.title}
                </h3>
                <p className="mt-1 text-lg text-slate-700 leading-relaxed">
                  <RichText text={step.text} lang={lang} />
                </p>
              </div>
            </li>
          ))}
        </ol>
      )}
      {section.subsections?.map((sub) => (
        <div key={sub.heading} className="mt-8">
          <h3 className="text-xl font-bold text-slate-900 mb-3">{sub.heading}</h3>
          <div className="space-y-4 text-lg text-slate-700 leading-relaxed">
            {sub.paragraphs.map((p) => (
              <p key={p}>
                <RichText text={p} lang={lang} />
              </p>
            ))}
          </div>
          {sub.bullets && (
            <ul className="mt-4 list-disc space-y-2 pl-6 text-lg text-slate-700 leading-relaxed marker:text-orange-500">
              {sub.bullets.map((b) => (
                <li key={b}>
                  <RichText text={b} lang={lang} />
                </li>
              ))}
            </ul>
          )}
        </div>
      ))}
      {section.tip && (
        <p className="mt-8 flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-5 text-slate-800 leading-relaxed">
          <Lightbulb className="h-5 w-5 mt-0.5 shrink-0 text-amber-600" aria-hidden="true" />
          <span>
            <RichText text={section.tip} lang={lang} />
          </span>
        </p>
      )}
    </section>
  );
}

/** Partway through the guide: where CadenceE does this job, with the launch offer while it runs. */
function GuideCtaBox({ guide, lang }: { guide: GuideContent; lang: SiteLang }) {
  const offer = useLaunchOfferActive();
  const signupLabel = useSignupLabel(lang);
  return (
    <aside className="mb-14 rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 p-6 lg:p-8 text-white shadow-xl">
      <p className="text-xl font-bold mb-2">{guide.cta.heading}</p>
      <p className="text-slate-300 leading-relaxed mb-6">
        <RichText text={guide.cta.text} lang={lang} />
      </p>
      <div className="flex flex-col sm:flex-row gap-3">
        <CtaLink kind="signup" lang={lang} track="cta-signup-guide" className="group inline-flex items-center justify-center gap-2 rounded-xl bg-white px-6 py-3 font-semibold text-slate-900 shadow-lg">
          {signupLabel}
          <ArrowRight className="h-4 w-4 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
        </CtaLink>
        <CtaLink kind="try" lang={lang} track="cta-try-guide" className="inline-flex items-center justify-center gap-2 rounded-xl border border-white/30 px-6 py-3 font-semibold text-white hover:bg-white/10">
          <Play className="h-4 w-4" aria-hidden="true" />
          {CTA_COPY[lang].tryDemo}
        </CtaLink>
      </div>
      {offer && (
        <p className="mt-4 text-sm font-semibold text-emerald-300" data-launch-offer="">
          {LAUNCH_OFFER_COPY[lang].badge} · {LAUNCH_OFFER_COPY[lang].noCard}
        </p>
      )}
    </aside>
  );
}

function GuideClosingCta({ guide, lang }: { guide: GuideContent; lang: SiteLang }) {
  const offer = useLaunchOfferActive();
  const signupLabel = useSignupLabel(lang);
  return (
    <section className="relative overflow-hidden py-20 lg:py-24">
      <div className="absolute inset-0 bg-gradient-to-br from-orange-600 via-amber-500 to-orange-600" aria-hidden="true" />
      <div className="relative w-full max-w-4xl mx-auto px-6 lg:px-12 text-center">
        <h2 className="text-3xl md:text-4xl font-bold text-white mb-5">{guide.cta.heading}</h2>
        <p className="text-xl text-white mb-10 max-w-2xl mx-auto">
          <RichText text={guide.cta.text} lang={lang} />
        </p>
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <CtaLink kind="signup" lang={lang} className="group bg-white text-orange-800 px-10 py-5 rounded-2xl font-bold text-lg flex items-center justify-center gap-3 shadow-2xl hover:scale-105 transition-transform duration-300">
            {signupLabel}
            <ArrowRight className="h-5 w-5 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
          </CtaLink>
          <CtaLink kind="try" lang={lang} className="bg-slate-900/20 border-2 border-white/60 text-white px-10 py-5 rounded-2xl font-bold text-lg flex items-center justify-center gap-2 hover:bg-slate-900/30 transition-colors duration-300">
            <Play className="h-5 w-5" aria-hidden="true" />
            {CTA_COPY[lang].tryDemo}
          </CtaLink>
        </div>
        {offer && (
          <p className="mt-6 text-sm font-semibold text-white" data-launch-offer="">
            {LAUNCH_OFFER_COPY[lang].badge} · {LAUNCH_OFFER_COPY[lang].noCard}
          </p>
        )}
      </div>
    </section>
  );
}

export default GuidePage;
