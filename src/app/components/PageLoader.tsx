import { PageSkeleton, ContentSkeleton, CenteredLoader } from './PageSkeleton';
import { usePageLoading } from './LoadingOverlay';

/**
 * Route-level loading. The shared overlay plays the entrance and exit;
 * this fallback only holds that overlay while the next page is still arriving.
 */
export function PageLoader() {
  usePageLoading(true);
  return null;
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
