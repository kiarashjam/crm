import { lazy, type ComponentType } from 'react';

export type PreloadableComponent<P extends object> = ComponentType<P> & {
  /** Fetches the component's code; once it is in, the component renders without suspending. */
  preload: () => Promise<unknown>;
};

/**
 * React.lazy that can be fetched ahead of rendering. The public pages are split out of the
 * main bundle, and main.tsx loads the one for the address before the app first renders:
 * the app then replaces the prerendered HTML with the same page in one go, with no
 * loading screen in between. Rendered before its code is in, it suspends like React.lazy.
 */
export function lazyWithPreload<P extends object>(
  factory: () => Promise<{ default: ComponentType<P> }>,
): PreloadableComponent<P> {
  let loaded: ComponentType<P> | undefined;
  let pending: Promise<{ default: ComponentType<P> }> | undefined;
  const load = () => {
    pending ??= factory().then(
      (module) => {
        loaded = module.default;
        return module;
      },
      (error: unknown) => {
        // A chunk that failed to arrive (a deploy in between, a dropped connection) is
        // asked for again next time rather than failing for good.
        pending = undefined;
        throw error;
      },
    );
    return pending;
  };
  const Lazy = lazy(load);
  function Preloadable(props: P) {
    const Component = (loaded ?? Lazy) as ComponentType<P>;
    return <Component {...props} />;
  }
  Preloadable.preload = load;
  return Preloadable;
}
