// The audience as the browser sends it: the filters set and nothing else, the Members list's
// tag and field conditions read in and out unchanged, and the left-out reasons in the server's order.

import { describe, expect, it } from 'vitest';
import {
  cleanAudience, flexOf, habitsInWords, leftOutCounts, usesMoreThanTiersAndStatuses, withFlex, type MemberAudience,
} from './memberAudience';

describe('the member audience', () => {
  it('sends only the filters set: no empty lists, no habit half-filled, no saved view’s kept copy', () => {
    expect(cleanAudience({
      tiers: [], statuses: ['Active'], tags: ['t1'], allTags: true, fields: [], viewId: 'v1',
      view: { id: 'v1', name: 'Wine lovers', tags: [], allTags: false, notTags: [], fields: [] },
      minEvents: 2, eventMonths: null, minVisits: null, visitDays: 30, notVisitedDays: 0,
    })).toEqual({ statuses: ['Active'], tags: ['t1'], viewId: 'v1', minEvents: 2 });
  });

  it('reads the Members list’s tag and field conditions in and out as they are', () => {
    const flex = { tags: ['a', 'b'], tagMatch: 'all' as const, notTags: ['c'], fields: ['interests:any:Wine|Art'] };
    const filter = withFlex({ statuses: ['Active'] }, flex);
    expect(filter).toMatchObject({ statuses: ['Active'], tags: ['a', 'b'], allTags: true, notTags: ['c'], fields: ['interests:any:Wine|Art'] });
    expect(flexOf(filter)).toEqual(flex);
    expect(withFlex(filter, {})).toMatchObject({ tags: null, allTags: null, notTags: null, fields: null });
  });

  it('knows a segment of tiers and statuses alone from one that narrows further', () => {
    expect(usesMoreThanTiersAndStatuses({ tiers: ['Gold'], statuses: ['Active'] })).toBe(false);
    expect(usesMoreThanTiersAndStatuses({ notVisitedDays: 30 })).toBe(true);
    expect(habitsInWords({ attendedEventIds: ['e1'], minVisits: 3, visitDays: 30 }, () => 'Wine night'))
      .toEqual(['came to Wine night', 'visited at least 3 times in the last 30 days']);
  });

  it('lists the reasons someone is left out in the order the server checks them, those with none left out', () => {
    const audience = {
      statusExcluded: 2, erased: 0, noEmail: 1, noConsent: 0, suppressed: 3, doNotContact: 0, alreadyRegistered: 1, alreadyInvited: 0, duplicates: 4,
    } as MemberAudience;
    expect(leftOutCounts(audience).map((r) => `${r.count} ${r.reason}`))
      .toEqual(['2 status', '1 no_email', '3 suppressed', '1 already_registered', '4 duplicate']);
  });
});
