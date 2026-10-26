import { Spinner } from '@/app/components/motion/CadenceLoader';
import { cn } from '@/app/components/ui/utils';

interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  label?: string;
  className?: string;
}

const sizeClasses = {
  sm: 'h-5 w-5',
  md: 'h-8 w-8',
  lg: 'h-10 w-10',
};

const pixels = { sm: 20, md: 32, lg: 40 };

/** An inline spinner: the app's gradient arc, with an optional label under it. */
export default function LoadingSpinner({ size = 'md', label, className = '' }: LoadingSpinnerProps) {
  const ariaLabel = label ?? 'Loading';
  const spinner = (
    <div
      className={cn('inline-flex items-center justify-center', sizeClasses[size], !label && className)}
      role="status"
      aria-label={ariaLabel}
    >
      <Spinner size={pixels[size]} />
      <span className="sr-only">{ariaLabel}</span>
    </div>
  );
  if (label) {
    return (
      <div className={cn('flex flex-col items-center justify-center gap-3', className)}>
        {spinner}
        <p className="bg-gradient-to-r from-slate-500 via-fuchsia-500 to-slate-500 bg-[length:200%_100%] bg-clip-text text-sm font-medium text-transparent motion-safe:animate-[shimmer_3s_linear_infinite]">
          {label}
        </p>
      </div>
    );
  }
  return spinner;
}
