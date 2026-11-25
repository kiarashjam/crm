// Whose visits the site analytics show: the website's visitors (the default), the people
// signed in to the app (the owner's own browsing among them), or everyone. Like the period,
// the choice lives in the address (?audience=signed-in), so a link or the back button brings
// back the same view; the website's visitors need no parameter.

import {
  DEFAULT_SITE_ANALYTICS_AUDIENCE, SITE_ANALYTICS_AUDIENCES, type SiteAnalyticsAudience,
} from '@/app/api/siteAnalytics';
import type { KpiKey } from './analyticsMath';

export const AUDIENCE_OPTIONS: { value: SiteAnalyticsAudience; label: string; description: string }[] = [
  {
    value: 'visitors',
    label: 'Website visitors',
    description: 'People on www.cadencee.ai who are not signed in, and their sign-ups. Your own use of the CRM is left out.',
  },
  {
    value: 'signed-in',
    label: 'Signed-in',
    description: 'People using the CRM while signed in, you included.',
  },
  {
    value: 'everyone',
    label: 'Everyone',
    description: 'Website visitors and signed-in people together.',
  },
];

/** The audience the address holds; the website's visitors when it holds none (or nonsense). */
export function readAudience(params: URLSearchParams): SiteAnalyticsAudience {
  const raw = params.get('audience');
  return SITE_ANALYTICS_AUDIENCES.find((a) => a === raw) ?? DEFAULT_SITE_ANALYTICS_AUDIENCE;
}

/** The address's parameters for an audience: none for the website's visitors. */
export function writeAudience(audience: SiteAnalyticsAudience, params: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(params);
  if (audience === DEFAULT_SITE_ANALYTICS_AUDIENCE) next.delete('audience');
  else next.set('audience', audience);
  return next;
}

/** Who "they" are in the Live card: "Website visitors in the last 5 minutes…". */
export const AUDIENCE_NOUN: Record<SiteAnalyticsAudience, string> = {
  visitors: 'Website visitors',
  'signed-in': 'Signed-in people',
  everyone: 'Everyone',
};

/**
 * The headline figures' hints that change with the audience, short enough for a narrow
 * card; the other figures keep their own.
 */
export const AUDIENCE_HINTS: Record<SiteAnalyticsAudience, Partial<Record<KpiKey, string>>> = {
  visitors: {
    visitors: 'website visitors, once a day',
    sessions: 'visits to the website',
    pageViews: 'website pages opened',
  },
  'signed-in': {
    visitors: 'signed-in people, once a day',
    sessions: 'signed-in sessions in the app',
    pageViews: 'pages opened signed in',
  },
  everyone: {
    visitors: 'everyone, once a day',
    sessions: 'all visits, signed in or not',
    pageViews: 'all pages opened',
  },
};
