import { PageSkeleton, ContentSkeleton, CenteredLoader } from './PageSkeleton';

/**
 * Full page loading state for lazy-loaded routes
 * Shows a beautiful skeleton that matches the page layout
 */
export function PageLoader() {
  return <PageSkeleton />;
}

/**
 * Content area loading state with skeleton
 */
export function ContentLoader({ rows = 5 }: { rows?: number }) {
  return <ContentSkeleton rows={rows} />;
}

/**
 * Simple centered spinner loader
 */
export function SpinnerLoader({ text = 'Loading...' }: { text?: string }) {
  return <CenteredLoader text={text} />;
}

export { PageSkeleton, ContentSkeleton, CenteredLoader };
