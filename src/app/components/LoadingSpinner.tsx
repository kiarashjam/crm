interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  label?: string;
  className?: string;
}

const sizeClasses = {
  sm: 'h-5 w-5 border-2',
  md: 'h-8 w-8 border-2',
  lg: 'h-10 w-10 border-[3px]',
};

export default function LoadingSpinner({ size = 'md', label, className = '' }: LoadingSpinnerProps) {
  const ariaLabel = label ?? 'Loading';
  const spinner = (
    <div
      className={`animate-spin rounded-full border-orange-500 border-t-transparent ${sizeClasses[size]} ${label ? '' : className}`}
      role="status"
      aria-label={ariaLabel}
    >
      <span className="sr-only">{ariaLabel}</span>
    </div>
  );
  if (label) {
    return (
      <div className={`flex flex-col items-center justify-center gap-3 ${className}`}>
        {spinner}
        <p className="text-sm text-slate-600">{label}</p>
      </div>
    );
  }
  return spinner;
}
