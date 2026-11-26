import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { CONTACT_EMAIL, type SiteLang } from '../site';

export type CtaKind = 'signup' | 'login' | 'try' | 'demo' | 'contact';

export const SIGNUP_PATH = '/login?mode=register';
export const LOGIN_PATH = '/login';
/** The demo workspace: sample data in the browser, no account (Login starts it on ?demo=1). */
export const TRY_DEMO_PATH = '/login?demo=1';

const DEMO_SUBJECT: Record<SiteLang, string> = {
  en: 'CadenceE demo',
  fr: 'Démo CadenceE',
  de: 'CadenceE Demo',
};

const CONTACT_SUBJECT: Record<SiteLang, string> = {
  en: 'CadenceE',
  fr: 'CadenceE',
  de: 'CadenceE',
};

/**
 * Where a call to action leads. "try" opens the demo workspace; demo and contact write to
 * CONTACT_EMAIL, or lead to the help page without one.
 */
export function ctaHref(kind: CtaKind, lang: SiteLang): string {
  switch (kind) {
    case 'signup':
      return SIGNUP_PATH;
    case 'login':
      return LOGIN_PATH;
    case 'try':
      return TRY_DEMO_PATH;
    case 'demo':
    case 'contact': {
      if (!CONTACT_EMAIL) return '/help';
      const subject = kind === 'demo' ? DEMO_SUBJECT[lang] : CONTACT_SUBJECT[lang];
      return `mailto:${CONTACT_EMAIL}?subject=${encodeURIComponent(subject)}`;
    }
  }
}

/** A call to action, tagged for the site's click counts (data-track="cta-…"). */
export function CtaLink({
  kind,
  lang,
  className,
  track: trackAs,
  children,
}: {
  kind: CtaKind;
  lang: SiteLang;
  className?: string;
  /** The click's name in the site analytics, when this button should be told apart ("cta-signup-bar"). */
  track?: string;
  children: ReactNode;
}) {
  const href = ctaHref(kind, lang);
  const track = trackAs ?? `cta-${kind}`;
  if (href.startsWith('mailto:')) {
    return (
      <a href={href} className={className} data-track={track}>
        {children}
      </a>
    );
  }
  return (
    <Link to={href} className={className} data-track={track}>
      {children}
    </Link>
  );
}

export default CtaLink;
