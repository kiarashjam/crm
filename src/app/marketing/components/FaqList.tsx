import { ChevronDown } from 'lucide-react';
import type { FaqItem } from '../content/types';
import { faqPageJsonLd, serializeJsonLd } from '../jsonld';
import type { SiteLang } from '../site';
import { RichText } from './RichText';

/**
 * Questions and answers as native disclosures: the answers are in the HTML and open
 * without JavaScript, and the FAQPage structured data beside them says the same words.
 */
export function FaqList({ items, lang }: { items: FaqItem[]; lang: SiteLang }) {
  return (
    <>
      <div className="space-y-4">
        {items.map((item, i) => (
          <details
            key={item.q}
            className="group relative rounded-2xl border border-slate-100 bg-white shadow-sm transition-shadow hover:shadow-xl open:ring-2 open:ring-orange-500 open:ring-offset-2"
            open={i === 0}
          >
            <span className="absolute left-0 top-0 bottom-0 w-1.5 rounded-l-2xl bg-gradient-to-b from-orange-500 to-amber-500" aria-hidden="true" />
            <summary className="list-none [&::-webkit-details-marker]:hidden cursor-pointer p-6 pl-8 flex items-center justify-between gap-4">
              <h3 className="text-left text-lg font-semibold text-slate-900 group-hover:text-orange-700 transition-colors">{item.q}</h3>
              <span className="w-8 h-8 rounded-full flex items-center justify-center shrink-0 bg-slate-100 transition-all duration-300 group-open:rotate-180 group-open:bg-slate-900" aria-hidden="true">
                <ChevronDown className="w-4 h-4 text-slate-600 group-open:text-white" />
              </span>
            </summary>
            <div className="px-8 pb-6 -mt-2">
              <p className="text-slate-700 leading-relaxed">
                <RichText text={item.a} lang={lang} />
              </p>
            </div>
          </details>
        ))}
      </div>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: serializeJsonLd(faqPageJsonLd(items, lang)) }} />
    </>
  );
}

export default FaqList;
