import { cn } from '@/app/components/ui/utils';

interface ScoreGaugeProps {
  /** 0–100 */
  score: number;
  /** outer SVG diameter in pixels. Default 96. */
  size?: number;
  /** stroke width of the arc. Default 8. */
  strokeWidth?: number;
  className?: string;
}

/**
 * Compact circular gauge for the lead score. Renders a 270° arc (so 0 starts
 * at "7 o'clock" and 100 ends at "5 o'clock") with a gradient stroke that
 * tracks the score band — cold (slate), warm (amber), hot (emerald).
 */
export function ScoreGauge({ score, size = 96, strokeWidth = 8, className }: ScoreGaugeProps) {
  const clamped = Math.max(0, Math.min(100, score));
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  // Arc covers 270° of the circle (3/4 of full); save 90° as the bottom gap.
  const arcFraction = 0.75;
  const arcLength = circumference * arcFraction;
  const visibleLength = (clamped / 100) * arcLength;
  const dash = `${visibleLength} ${circumference}`;
  // Hide the remaining 25% (the bottom gap) by offsetting the start.
  const rotation = 135; // start at "7 o'clock"
  const tone = clamped >= 70 ? 'emerald' : clamped >= 40 ? 'amber' : 'slate';
  const stops = {
    emerald: { from: '#10b981', to: '#14b8a6' },
    amber:   { from: '#f59e0b', to: '#fb923c' },
    slate:   { from: '#94a3b8', to: '#cbd5e1' },
  }[tone];
  const ringTint = {
    emerald: 'from-emerald-50 via-white to-teal-50/80',
    amber:   'from-amber-50 via-white to-orange-50/80',
    slate:   'from-slate-50 via-white to-slate-50',
  }[tone];
  const valueTone = {
    emerald: 'text-emerald-700',
    amber:   'text-amber-700',
    slate:   'text-slate-600',
  }[tone];
  const gradientId = `score-gradient-${tone}`;

  return (
    <div
      className={cn(
        'relative inline-flex items-center justify-center rounded-full bg-gradient-to-br shadow-inner ring-1 ring-slate-200/70',
        ringTint,
        className,
      )}
      style={{ width: size, height: size }}
    >
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden>
        <defs>
          <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={stops.from} />
            <stop offset="100%" stopColor={stops.to} />
          </linearGradient>
        </defs>
        {/* Track */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="rgb(226 232 240)"
          strokeWidth={strokeWidth}
          strokeDasharray={`${arcLength} ${circumference}`}
          strokeLinecap="round"
          transform={`rotate(${rotation} ${size / 2} ${size / 2})`}
        />
        {/* Value arc */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={`url(#${gradientId})`}
          strokeWidth={strokeWidth}
          strokeDasharray={dash}
          strokeLinecap="round"
          transform={`rotate(${rotation} ${size / 2} ${size / 2})`}
          className="transition-[stroke-dasharray] duration-500"
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
        <span className={cn('text-xl font-bold tabular-nums leading-none', valueTone)}>{clamped}</span>
        <span className="mt-0.5 text-[9px] uppercase tracking-widest text-slate-400">Score</span>
      </div>
    </div>
  );
}
