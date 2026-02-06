import { cn } from '@/app/components/ui/utils';

/**
 * Shimmer animation for skeleton loading
 */
function Shimmer({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'animate-pulse rounded-xl bg-gradient-to-r from-slate-200 via-slate-100 to-slate-200 bg-[length:200%_100%]',
        className
      )}
      style={{
        animation: 'shimmer 1.5s ease-in-out infinite',
      }}
    />
  );
}

/**
 * Beautiful full-page skeleton loading state
 * Matches the typical CRM page layout with hero, stats, and content
 */
export function PageSkeleton() {
  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle animate-fade-in">
      {/* Header skeleton */}
      <header className="w-full px-[var(--page-padding)] py-4 border-b border-slate-200/60 bg-white/80 backdrop-blur-sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <Shimmer className="w-10 h-10 rounded-xl" />
            <Shimmer className="w-32 h-6 rounded-lg" />
          </div>
          <div className="flex items-center gap-3">
            <Shimmer className="w-8 h-8 rounded-full" />
            <Shimmer className="w-8 h-8 rounded-full" />
            <Shimmer className="w-10 h-10 rounded-full" />
          </div>
        </div>
      </header>

      {/* Main content skeleton */}
      <main className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]">
        {/* Hero section skeleton */}
        <div className="relative bg-gradient-to-br from-slate-200 via-slate-100 to-slate-200 rounded-2xl overflow-hidden mb-8 p-8">
          <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
            <div className="flex items-center gap-5">
              <Shimmer className="w-16 h-16 rounded-2xl" />
              <div className="space-y-2">
                <Shimmer className="w-48 h-8 rounded-lg" />
                <Shimmer className="w-64 h-4 rounded-md" />
              </div>
            </div>
            <Shimmer className="w-36 h-10 rounded-xl" />
          </div>
        </div>

        {/* Stats grid skeleton */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
          {[1, 2, 3, 4].map((i) => (
            <div
              key={i}
              className="bg-white rounded-2xl border border-slate-200 p-5"
              style={{ animationDelay: `${i * 0.1}s` }}
            >
              <div className="flex items-center justify-between mb-3">
                <Shimmer className="w-10 h-10 rounded-xl" />
                <Shimmer className="w-12 h-4 rounded-full" />
              </div>
              <Shimmer className="w-20 h-8 rounded-lg mb-1" />
              <Shimmer className="w-24 h-3 rounded-md" />
            </div>
          ))}
        </div>

        {/* Search/filter bar skeleton */}
        <div className="flex items-center gap-4 mb-6">
          <Shimmer className="flex-1 max-w-md h-11 rounded-xl" />
          <Shimmer className="w-28 h-11 rounded-xl" />
          <Shimmer className="w-28 h-11 rounded-xl" />
        </div>

        {/* Content list skeleton */}
        <div className="space-y-3">
          {[1, 2, 3, 4, 5].map((i) => (
            <div
              key={i}
              className="bg-white rounded-xl border border-slate-200 p-4 flex items-center gap-4"
              style={{ animationDelay: `${0.4 + i * 0.05}s` }}
            >
              <Shimmer className="w-12 h-12 rounded-xl flex-shrink-0" />
              <div className="flex-1 space-y-2">
                <Shimmer className="w-3/4 h-5 rounded-md" />
                <Shimmer className="w-1/2 h-3 rounded-md" />
              </div>
              <Shimmer className="w-20 h-6 rounded-full" />
              <Shimmer className="w-8 h-8 rounded-lg" />
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}

/**
 * Compact skeleton for content areas within a page
 */
export function ContentSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-4 animate-fade-in">
      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {[1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className="bg-white rounded-xl border border-slate-200 p-4"
          >
            <Shimmer className="w-10 h-10 rounded-lg mb-2" />
            <Shimmer className="w-16 h-6 rounded-md mb-1" />
            <Shimmer className="w-20 h-3 rounded-sm" />
          </div>
        ))}
      </div>

      {/* List */}
      <div className="space-y-3">
        {Array.from({ length: rows }).map((_, i) => (
          <div
            key={i}
            className="bg-white rounded-xl border border-slate-200 p-4 flex items-center gap-4"
          >
            <Shimmer className="w-10 h-10 rounded-lg flex-shrink-0" />
            <div className="flex-1 space-y-2">
              <Shimmer className="w-2/3 h-4 rounded-md" />
              <Shimmer className="w-1/3 h-3 rounded-md" />
            </div>
            <Shimmer className="w-16 h-6 rounded-full" />
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * Kanban-style skeleton for pipeline/tasks views
 */
export function KanbanSkeleton({ columns = 4 }: { columns?: number }) {
  return (
    <div className="animate-fade-in">
      {/* Header skeleton */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-4">
          <Shimmer className="w-48 h-8 rounded-lg" />
          <Shimmer className="w-24 h-8 rounded-lg" />
        </div>
        <div className="flex items-center gap-3">
          <Shimmer className="w-32 h-10 rounded-xl" />
          <Shimmer className="w-10 h-10 rounded-lg" />
          <Shimmer className="w-10 h-10 rounded-lg" />
        </div>
      </div>

      {/* Kanban columns */}
      <div className="flex gap-4 overflow-hidden">
        {Array.from({ length: columns }).map((_, col) => (
          <div
            key={col}
            className="flex-1 min-w-[280px] bg-slate-50 rounded-2xl p-4"
          >
            <div className="flex items-center justify-between mb-4">
              <Shimmer className="w-24 h-5 rounded-md" />
              <Shimmer className="w-6 h-6 rounded-full" />
            </div>
            <div className="space-y-3">
              {[1, 2, 3].map((card) => (
                <div
                  key={card}
                  className="bg-white rounded-xl border border-slate-200 p-4"
                >
                  <Shimmer className="w-full h-5 rounded-md mb-2" />
                  <Shimmer className="w-2/3 h-3 rounded-sm mb-3" />
                  <div className="flex items-center justify-between">
                    <Shimmer className="w-16 h-5 rounded-full" />
                    <Shimmer className="w-6 h-6 rounded-full" />
                  </div>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * Simple centered loading spinner with text
 */
export function CenteredLoader({ text = 'Loading...' }: { text?: string }) {
  return (
    <div className="flex flex-col items-center justify-center min-h-[400px] animate-fade-in">
      <div className="relative">
        {/* Outer glow ring */}
        <div className="absolute inset-0 rounded-full bg-orange-500/20 animate-ping" />
        
        {/* Spinner */}
        <div className="relative w-12 h-12 rounded-full border-3 border-slate-200 border-t-orange-500 animate-spin" />
      </div>
      
      <p className="mt-4 text-sm font-medium text-slate-500 animate-pulse">
        {text}
      </p>
    </div>
  );
}

export default PageSkeleton;
