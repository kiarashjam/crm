import LoadingSpinner from './LoadingSpinner';

/**
 * Full page loading state for lazy-loaded routes
 */
export function PageLoader() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <LoadingSpinner size="lg" label="Loading..." />
    </div>
  );
}

/**
 * Content area loading state
 */
export function ContentLoader() {
  return (
    <div className="flex min-h-[400px] items-center justify-center">
      <LoadingSpinner size="md" label="Loading..." />
    </div>
  );
}
