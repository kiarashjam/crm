import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { CONTACT_EMAIL, type SiteLang } from '../site';

export type CtaKind = 'signup' | 'login' | 'demo' | 'contact';

export const SIGNUP_PATH = '/login?mode=register';
export const LOGIN_PATH = '/login';

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

/** Where a call to action leads. Demo and contact write to CONTACT_EMAIL, or lead to the help page without one. */
export function ctaHref(kind: CtaKind, lang: SiteLang): string {
  switch (kind) {
    case 'signup':
      return SIGNUP_PATH;
    case 'login':
      return LOGIN_PATH;
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
  children,
}: {
  kind: CtaKind;
  lang: SiteLang;
  className?: string;
  children: ReactNode;
}) {
  const href = ctaHref(kind, lang);
  const track = `cta-${kind}`;
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
