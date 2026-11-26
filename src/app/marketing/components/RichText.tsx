import { Fragment, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { MARKETING_PAGE_IDS, marketingPath, type MarketingPageId } from '../routes';
import type { SiteLang } from '../site';

const LINK = /\[([^\]]+)\]\((page:[a-z-]+|\/[^)]*)\)/g;

/**
 * Text with the content files' link markup turned into links: "[label](page:sales)" to a
 * marketing page in the same language, "[label](/login)" to a path on the site
 * ("/login?demo=1": the demo workspace).
 */
export function RichText({ text, lang }: { text: string; lang: SiteLang }) {
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(LINK)) {
    const [whole, label = '', target = ''] = match;
    const at = match.index ?? 0;
    if (at > last) parts.push(text.slice(last, at));
    let to = target;
    let track: string | undefined;
    if (target.startsWith('page:')) {
      const id = target.slice(5) as MarketingPageId;
      if (!(MARKETING_PAGE_IDS as readonly string[]).includes(id)) throw new Error(`Unknown page in link: ${target}`);
      to = marketingPath(id, lang);
      track = `nav-${id}`;
    } else if (target.startsWith('/login')) {
      // "/login?demo=1" opens the demo workspace (sample data, no account): counted apart from sign-ins.
      track = target.includes('mode=register') ? 'cta-signup' : target.includes('demo=1') ? 'cta-try-demo' : 'cta-login';
    }
    parts.push(
      <Link key={at} to={to} data-track={track} className="font-semibold text-orange-700 underline decoration-orange-300 underline-offset-4 hover:decoration-orange-600">
        {label}
      </Link>,
    );
    last = at + whole.length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return <Fragment>{parts}</Fragment>;
}

export default RichText;
