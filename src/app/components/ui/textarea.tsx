import * as React from "react";

import { cn } from "./utils";

function Textarea({ className, ...props }: React.ComponentProps<"textarea">) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(
        "resize-none border-input placeholder:text-muted-foreground placeholder:transition-opacity focus-visible:placeholder:opacity-60 focus-visible:border-orange-300 focus-visible:ring-orange-400/20 focus-visible:shadow-[0_8px_24px_-14px_rgba(249,115,22,0.55)] aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive dark:bg-input/30 flex field-sizing-content min-h-16 w-full rounded-md border bg-input-background px-3 py-2 text-base transition-[color,box-shadow,border-color,background-color] duration-200 ease-out outline-none hover:border-ring/40 aria-invalid:animate-[field-shake_0.4s_ease-in-out] focus-visible:ring-[3px] disabled:cursor-not-allowed disabled:opacity-50 md:text-sm",
        className,
      )}
      {...props}
    />
  );
}

export { Textarea };
