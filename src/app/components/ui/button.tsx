import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "./utils";
import { useMotionPreference } from "@/app/hooks/useMotionPreference";

// Every button lifts a pixel on hover, gives a little when pressed, and lets ink
// spread from the point it was pressed. The lift and the press are transforms on
// their own properties (`translate`, `scale`), so a caller's own positioning
// survives them; the ink is drawn inside a clipped layer of its own, so a badge
// hanging off the button's corner is not cut off.
const buttonVariants = cva(
  "relative inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium transition-[color,background-color,border-color,box-shadow,translate,scale,opacity] duration-200 ease-out active:scale-[0.97] active:duration-75 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4 shrink-0 [&_svg]:shrink-0 [&_svg]:transition-transform [&_svg]:duration-200 hover:[&_svg]:scale-110 outline-none focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px] aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive",
  {
    variants: {
      variant: {
        default:
          "bg-primary text-primary-foreground hover:bg-primary/90 hover:-translate-y-px hover:shadow-md hover:shadow-primary/25",
        destructive:
          "bg-destructive text-white hover:bg-destructive/90 hover:-translate-y-px hover:shadow-md hover:shadow-destructive/25 focus-visible:ring-destructive/20 dark:focus-visible:ring-destructive/40 dark:bg-destructive/60",
        outline:
          "border bg-background text-foreground hover:bg-accent hover:text-accent-foreground hover:-translate-y-px hover:shadow-sm dark:bg-input/30 dark:border-input dark:hover:bg-input/50",
        secondary:
          "bg-secondary text-secondary-foreground hover:bg-secondary/80 hover:-translate-y-px hover:shadow-sm",
        ghost:
          "hover:bg-accent hover:text-accent-foreground dark:hover:bg-accent/50",
        link: "text-primary underline-offset-4 hover:underline active:scale-100 hover:[&_svg]:scale-100",
      },
      size: {
        default: "h-9 px-4 py-2 has-[>svg]:px-3",
        sm: "h-8 rounded-md gap-1.5 px-3 has-[>svg]:px-2.5",
        lg: "h-10 rounded-md px-6 has-[>svg]:px-4",
        icon: "size-9 rounded-md",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
);

interface Ripple {
  id: number;
  x: number;
  y: number;
  size: number;
}

/** How long a ripple stays in the tree; a little longer than its animation. */
const RIPPLE_MS = 650;
let nextRipple = 0;

const Button = React.forwardRef<
  HTMLButtonElement,
  React.ComponentProps<"button"> &
    VariantProps<typeof buttonVariants> & {
      asChild?: boolean;
    }
>(({ className, variant, size, asChild = false, onPointerDown, children, ...props }, ref) => {
  const reduceMotion = useMotionPreference();
  const [ripples, setRipples] = React.useState<Ripple[]>([]);
  const timers = React.useRef<number[]>([]);
  React.useEffect(() => () => timers.current.forEach((t) => window.clearTimeout(t)), []);

  // A slotted child is someone else's element with a single child of its own, so
  // there is nowhere to put the ink layer; it still gets the lift and the press.
  const inks = !asChild && !reduceMotion && variant !== "link";

  const handlePointerDown = (event: React.PointerEvent<HTMLButtonElement>) => {
    onPointerDown?.(event);
    if (!inks || event.defaultPrevented || event.button !== 0) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const diameter = Math.max(rect.width, rect.height) * 2;
    const id = ++nextRipple;
    setRipples((current) => [
      ...current,
      { id, x: event.clientX - rect.left - diameter / 2, y: event.clientY - rect.top - diameter / 2, size: diameter },
    ]);
    timers.current.push(
      window.setTimeout(() => setRipples((current) => current.filter((r) => r.id !== id)), RIPPLE_MS),
    );
  };

  const classes = cn(buttonVariants({ variant, size, className }));

  if (asChild) {
    return (
      <Slot ref={ref} data-slot="button" className={classes} onPointerDown={onPointerDown} {...props}>
        {children}
      </Slot>
    );
  }

  return (
    <button ref={ref} data-slot="button" className={classes} onPointerDown={handlePointerDown} {...props}>
      {children}
      {ripples.length > 0 && (
        <span aria-hidden data-slot="button-ink" className="pointer-events-none absolute inset-0 overflow-hidden rounded-[inherit]">
          {ripples.map((r) => (
            <span key={r.id} className="cadence-ripple" style={{ left: r.x, top: r.y, width: r.size, height: r.size }} />
          ))}
        </span>
      )}
    </button>
  );
});
Button.displayName = "Button";

export { Button, buttonVariants };
