export const EMBEDDABLE_SEGMENTS: string[];
export const APP_SHELL: string;

export interface SpaRouteSegment {
  segment: string;
  exact: boolean;
  deeper: boolean;
}

export function spaRouteSegments(appSource: string, prerenderedPaths: string[]): SpaRouteSegment[];

export interface SwaRoute {
  route: string;
  rewrite?: string;
  headers?: Record<string, string>;
}

export interface SwaConfig {
  navigationFallback?: { rewrite: string; exclude?: string[] };
  routes?: SwaRoute[];
  responseOverrides?: Record<string, { rewrite?: string; statusCode?: number; redirect?: string }>;
  globalHeaders?: Record<string, string>;
  mimeTypes?: Record<string, string>;
  trailingSlash?: string;
}

export function staticWebAppConfig(base: SwaConfig, segments: SpaRouteSegment[], prerenderedPaths?: string[]): SwaConfig;
