import { type ReactNode, useEffect, useState } from 'react';
import { cn } from '@/app/components/ui/utils';

type TransitionVariant = 'slide-up' | 'fade' | 'scale' | 'blur';

interface PageTransitionProps {
  children: ReactNode;
  className?: string;
  /** Animation variant - defaults to 'slide-up' for best UX */
  variant?: TransitionVariant;
  /** Whether to stagger children animations */
  stagger?: boolean;
  /** Optional delay before animation starts (ms) */
  delay?: number;
}

/**
 * PageTransition - Smooth, fast page loading animations
 * 
 * Wrap your page content with this component to get smooth entry animations.
 * Uses GPU-accelerated CSS animations for maximum performance.
 * 
 * @example
 * ```tsx
 * <PageTransition>
 *   <main>Your page content</main>
 * </PageTransition>
 * ```
 */
export function PageTransition({
  children,
  className,
  variant = 'slide-up',
  stagger = false,
  delay = 0,
}: PageTransitionProps) {
  const [isReady, setIsReady] = useState(delay === 0);

  useEffect(() => {
    if (delay > 0) {
      const timer = setTimeout(() => setIsReady(true), delay);
      return () => clearTimeout(timer);
    }
  }, [delay]);

  const variantClasses: Record<TransitionVariant, string> = {
    'slide-up': 'page-transition',
    'fade': 'page-transition-fade',
    'scale': 'page-transition-scale',
    'blur': 'page-transition-blur',
  };

  if (!isReady) {
    return <div className="opacity-0">{children}</div>;
  }

  return (
    <div
      className={cn(
        variantClasses[variant],
        stagger && 'page-transition-stagger',
        'gpu-accelerated',
        className
      )}
    >
      {children}
    </div>
  );
}

/**
 * AnimatedSection - For animating individual sections within a page
 * 
 * @example
 * ```tsx
 * <AnimatedSection delay={100}>
 *   <YourSection />
 * </AnimatedSection>
 * ```
 */
interface AnimatedSectionProps {
  children: ReactNode;
  className?: string;
  /** Delay in milliseconds */
  delay?: number;
  /** Animation type */
  animation?: 'content' | 'hero' | 'fast';
}

export function AnimatedSection({
  children,
  className,
  delay = 0,
  animation = 'content',
}: AnimatedSectionProps) {
  const animationClasses: Record<string, string> = {
    content: 'animate-content-in',
    hero: 'animate-hero-in',
    fast: 'animate-content-in-fast',
  };

  return (
    <div
      className={cn(animationClasses[animation], 'gpu-accelerated', className)}
      style={delay > 0 ? { animationDelay: `${delay}ms` } : undefined}
    >
      {children}
    </div>
  );
}

/**
 * AnimatedStatCard - For stat cards with staggered animations
 */
export function AnimatedStatCard({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn('animate-stat-card gpu-accelerated', className)}>
      {children}
    </div>
  );
}

/**
 * AnimatedRow - For list/table rows with cascade animations
 */
export function AnimatedRow({
  children,
  className,
  delay = 0,
}: {
  children: ReactNode;
  className?: string;
  delay?: number;
}) {
  return (
    <div
      className={cn('animate-row-in gpu-accelerated', className)}
      style={delay > 0 ? { animationDelay: `${delay}ms` } : undefined}
    >
      {children}
    </div>
  );
}

export default PageTransition;
