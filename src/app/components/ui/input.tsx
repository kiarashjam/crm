import * as React from "react";

import { cn } from "./utils";

/**
 * Forwards its ref, so a caller that needs the element gets it.
 *
 * It was a plain function component, and pages that pass a ref — Leads, to focus
 * its search box — got React's "Function components cannot be given refs" warning
 * and a ref that stayed null. The warning was the visible half; the silently
 * broken `.focus()` was the half that mattered.
 */
const Input = React.forwardRef<HTMLInputElement, React.ComponentProps<"input">>(
  ({ className, type, ...props }, ref) => (
    <input
      ref={ref}
      type={type}
      data-slot="input"
      className={cn(
        "file:text-foreground placeholder:text-muted-foreground selection:bg-primary selection:text-primary-foreground dark:bg-input/30 border-input flex h-9 w-full min-w-0 rounded-md border px-3 py-1 text-base bg-input-background transition-[color,box-shadow,border-color,background-color] duration-200 ease-out outline-none hover:border-ring/40 file:inline-flex file:h-7 file:border-0 file:bg-transparent file:text-sm file:font-medium disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm",
        "focus-visible:border-orange-300 focus-visible:ring-orange-400/20 focus-visible:ring-[3px] focus-visible:shadow-[0_8px_24px_-14px_rgba(249,115,22,0.55)] placeholder:transition-opacity placeholder:duration-200 focus-visible:placeholder:opacity-60",
        "aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive aria-invalid:animate-[field-shake_0.4s_ease-in-out]",
        className,
      )}
      {...props}
    />
  ),
);
Input.displayName = "Input";

export { Input };
