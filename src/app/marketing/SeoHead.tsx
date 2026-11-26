import { useLayoutEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { APP_TITLE } from '@/app/hooks/useDocumentTitle';
import { applyHead, headForPath } from './seo';

/**
 * Keeps the document head in step with the route: the marketing pages' titles,
 * descriptions, canonicals, hreflang and structured data, the public pages' heads, and a
 * noindex on everything else. Renders nothing.
 *
 * The app's own pages keep setting their titles (useDocumentTitle); this only puts back
 * the plain app title when the last title it set was a marketing page's.
 */
export function SeoHead() {
  const { pathname } = useLocation();
  const lastTitle = useRef<string | null>(null);

  useLayoutEffect(() => {
    const head = headForPath(pathname);
    if (head.title === null && lastTitle.current !== null && document.title === lastTitle.current) {
      document.title = APP_TITLE;
    }
    applyHead(document, head);
    lastTitle.current = head.title;
  }, [pathname]);

  return null;
}

export default SeoHead;
