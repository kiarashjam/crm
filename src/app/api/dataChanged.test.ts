import { describe, expect, it, vi } from 'vitest';
import { announceDataChanged, changesRecords, onDataChanged, resourcesChangedBy, touches } from './dataChanged';

describe('which writes count as a record change', () => {
  it('writes to records do', () => {
    expect(changesRecords('PUT', '/api/leads/abc')).toBe(true);
    expect(changesRecords('POST', '/api/activities')).toBe(true);
    expect(changesRecords('DELETE', '/api/sequences/enrollments/x')).toBe(true);
    expect(changesRecords('patch', '/api/deals/1')).toBe(true);
    expect(changesRecords('POST', 'https://api.example.com/api/contracts/1/send')).toBe(true);
  });

  it('reads never do', () => {
    expect(changesRecords(undefined, '/api/leads/all')).toBe(false);
    expect(changesRecords('GET', '/api/leads/all')).toBe(false);
  });

  it('sign-in, notifications, the writer and personal settings do not', () => {
    // The Settings page POSTs /api/auth/2fa/setup every time it opens; counting
    // that as a change made every listener re-read eight lists for nothing.
    expect(changesRecords('POST', '/api/auth/2fa/setup')).toBe(false);
    expect(changesRecords('POST', 'https://api.example.com/api/auth/login')).toBe(false);
    expect(changesRecords('PUT', '/api/notifications/read-all')).toBe(false);
    expect(changesRecords('POST', '/api/copy/generate')).toBe(false);
    expect(changesRecords('PUT', '/api/settings')).toBe(false);
  });

  it('lead routing setup does not, but a bulk edit of leads does', () => {
    // Adding a queue on the routing page used to make the sales agent re-read
    // all eight of its lists.
    expect(changesRecords('POST', '/api/lead-ops/queues')).toBe(false);
    expect(changesRecords('PUT', '/api/lead-ops/scoring')).toBe(false);
    expect(changesRecords('DELETE', '/api/lead-ops/assignment/1')).toBe(false);
    expect(changesRecords('POST', '/api/lead-ops/bulk')).toBe(true);
  });

  it("an import's reading of its file and its dry run do not, but the import and its undo do", () => {
    // Both carry the file, so they are POSTs, and they write nothing.
    expect(changesRecords('POST', '/api/imports/analyze')).toBe(false);
    expect(changesRecords('POST', '/api/imports/preview')).toBe(false);
    expect(changesRecords('POST', '/api/imports')).toBe(true);
    expect(changesRecords('POST', '/api/imports/0b1c/undo')).toBe(true);
  });
});

describe('what a write changes', () => {
  it('a task changes tasks and the figures that count them, not the other lists', () => {
    const changed = resourcesChangedBy('PUT', '/api/tasks/t-1');
    expect(changed).toContain('tasks');
    for (const other of ['leads', 'contacts', 'companies', 'deals', 'sequences', 'automations']) {
      expect(changed).not.toContain(other);
    }
  });

  it('a call logged on a lead changes leads, not every deal and contact', () => {
    const changed = resourcesChangedBy('POST', '/api/activities', JSON.stringify({ type: 'call', leadId: 'l-1' }));
    expect(changed).toEqual(expect.arrayContaining(['activities', 'leads']));
    expect(changed).not.toContain('deals');
    expect(changed).not.toContain('contacts');
  });

  it('an activity whose records are not known changes every kind it can touch', () => {
    // An edit or a delete does not say which record the activity was on.
    expect(resourcesChangedBy('DELETE', '/api/activities/a-1')).toEqual(expect.arrayContaining(['leads', 'contacts', 'deals']));
  });

  it("a lead's save changes its person's contact and what automations add", () => {
    expect(resourcesChangedBy('POST', '/api/leads')).toEqual(expect.arrayContaining(['leads', 'contacts', 'tasks', 'sequences']));
  });

  it("a deal's save and a countersigned contract change tasks: the automations they start add some", () => {
    // A deal moved to a stage, won or lost runs the workspace's automations, which
    // can make a task; so can the lead status a signature moves on.
    expect(resourcesChangedBy('PUT', '/api/deals/d-1')).toContain('tasks');
    expect(resourcesChangedBy('POST', '/api/contracts/c-1/countersign')).toContain('tasks');
  });

  it("removing a member changes the leads, deals and tasks handed to whoever takes them over", () => {
    expect(resourcesChangedBy('DELETE', '/api/organizations/o-1/members/u-2?handOverTo=u-3'))
      .toEqual(expect.arrayContaining(['team', 'leads', 'deals', 'tasks', 'work-queue', 'reports']));
  });

  it('a merge or a conversion changes everything linked', () => {
    for (const path of ['/api/contacts/c-1/merge', '/api/leads/l-1/convert']) {
      expect(resourcesChangedBy('POST', path)).toEqual(expect.arrayContaining(['leads', 'contacts', 'companies', 'deals', 'tasks', 'activities']));
    }
  });

  it('a club write changes only its own records, whatever the query string', () => {
    // A check-in writes the points it earns with the visit, so the loyalty ledger moves too.
    expect(resourcesChangedBy('POST', 'https://api.example.com/api/visits?memberId=m-1')).toEqual(['visits', 'loyalty']);
  });

  it("the club's money is one ledger: a charge, a payment or a tab moves balances and statements", () => {
    // A charge or a payment moves the member's balance on the members list and their
    // statement; a payment settles charges; a tab's close posts a charge and earns points.
    expect(resourcesChangedBy('POST', '/api/charges')).toEqual(expect.arrayContaining(['charges', 'members', 'statements']));
    expect(resourcesChangedBy('POST', '/api/payments/p-1/void')).toEqual(expect.arrayContaining(['payments', 'charges', 'members', 'statements']));
    expect(resourcesChangedBy('POST', '/api/tabs/t-1/close')).toEqual(expect.arrayContaining(['tabs', 'charges', 'members', 'loyalty', 'statements']));
    expect(resourcesChangedBy('POST', '/api/subscriptions/s-1/bill')).toEqual(expect.arrayContaining(['subscriptions', 'charges', 'members']));
    // A balance set by hand on a member is an entry on the ledger too.
    expect(resourcesChangedBy('PUT', '/api/members/m-1')).toEqual(expect.arrayContaining(['members', 'charges', 'statements']));
    // A member added can start their dues today: a subscription, and a period billing now would post.
    expect(resourcesChangedBy('POST', '/api/members')).toEqual(expect.arrayContaining(['subscriptions', 'billing']));
  });

  it("a club member's merge moves club records, not the sales lists", () => {
    const changed = resourcesChangedBy('POST', '/api/members/m-1/merge');
    expect(changed).toEqual(expect.arrayContaining(['members', 'charges', 'payments']));
    expect(changed).not.toContain('leads');
  });

  it("a club member's save or merge refreshes what both the ledger and the campaigns show of them", () => {
    // Two groups add to what a member write changes. The ledger: a balance set by hand is
    // an entry, and a merge moves the duplicate's entries, so their tabs and statement. The
    // communications: a tier or status changes who a campaign reaches, and a merge moves
    // consent and messages. Neither may push the other's out.
    expect(resourcesChangedBy('PUT', '/api/members/m-1')).toEqual(
      expect.arrayContaining(['members', 'charges', 'payments', 'statements', 'campaigns']),
    );
    expect(resourcesChangedBy('POST', '/api/members/m-1/merge')).toEqual(
      expect.arrayContaining(['members', 'charges', 'payments', 'statements', 'tabs', 'campaigns', 'communications', 'duplicates']),
    );
    // A consent answer is not money: it leaves the ledger's lists alone.
    expect(resourcesChangedBy('POST', '/api/members/m-1/consent')).not.toContain('charges');
  });

  it("sending a campaign fills the communications log, and a member's consent or merge refreshes what campaigns reach", () => {
    expect(resourcesChangedBy('POST', '/api/campaigns/c-1/send')).toEqual(['campaigns', 'communications']);
    expect(resourcesChangedBy('POST', '/api/members/m-1/consent')).toEqual(['members', 'campaigns']);
    expect(resourcesChangedBy('POST', '/api/members/m-1/merge')).toEqual(
      expect.arrayContaining(['members', 'campaigns', 'communications', 'duplicates', 'charges', 'visits']),
    );
  });

  it("a change to an event's list refreshes the money and messages it can touch", () => {
    // A registration posts a ticket, a cancellation takes it back (a payment for it becoming
    // credit) and emails the next in line, and a check-in gives the member points.
    for (const path of [
      '/api/events/e-1/attendees',
      '/api/events/e-1/attendees/walk-in',
      '/api/events/e-1/attendees/a-1',
      '/api/events/e-1',
    ]) {
      expect(resourcesChangedBy('POST', path)).toEqual(
        expect.arrayContaining(['events', 'charges', 'payments', 'members', 'statements', 'loyalty', 'communications']),
      );
    }
    expect(resourcesChangedBy('PUT', '/api/events/e-1/attendees/a-1')).not.toContain('leads');
  });

  it("a booking refreshes the money its deposit and fee touch, and a resource's settings its bookings", () => {
    expect(resourcesChangedBy('POST', '/api/reservations')).toEqual(
      expect.arrayContaining(['reservations', 'charges', 'payments', 'members', 'statements']),
    );
    expect(resourcesChangedBy('PUT', '/api/resources/r-1')).toEqual(['resources', 'reservations']);
    expect(resourcesChangedBy('POST', '/api/resources/defaults')).toEqual(['resources', 'reservations']);
  });

  it("a club member's merge moves their registrations too", () => {
    expect(resourcesChangedBy('POST', '/api/members/m-1/merge')).toEqual(expect.arrayContaining(['events', 'reservations']));
  });

  it("deleting a club member detaches their registrations and bookings, but an edit leaves those lists alone", () => {
    // The server keeps their places and bookings under the name, with no member: a page still
    // showing the member on one would send the id of someone who is gone.
    expect(resourcesChangedBy('DELETE', '/api/members/m-1')).toEqual(
      expect.arrayContaining(['members', 'charges', 'statements', 'events', 'reservations']),
    );
    expect(resourcesChangedBy('PUT', '/api/members/m-1')).not.toContain('events');
    expect(resourcesChangedBy('PUT', '/api/members/m-1')).not.toContain('reservations');
  });

  it('an approval refreshes the member it made, their dues, fee and welcome, and the lead and contact pages', () => {
    expect(resourcesChangedBy('POST', '/api/applications/a-1/approve')).toEqual(expect.arrayContaining([
      'applications', 'members', 'charges', 'subscriptions', 'statements', 'communications', 'households', 'leads', 'contacts',
    ]));
  });

  it("a household's merge refreshes the households, not every sales list", () => {
    // It used to be read as any merge, which refreshed the leads and deals and left the
    // households list showing the duplicates merged away.
    const merged = resourcesChangedBy('POST', '/api/households/h-1/merge');
    expect(merged).toEqual(expect.arrayContaining(['households', 'duplicates', 'members']));
    expect(merged).not.toContain('deals');
    expect(resourcesChangedBy('PUT', '/api/households/h-1/members/l-1')).toEqual(expect.arrayContaining(['households', 'members']));
  });

  it("a spouse recorded on a contact's or a lead's page refreshes the households: a couple is one", () => {
    expect(resourcesChangedBy('POST', '/api/contacts/c-1/relationships')).toContain('households');
    expect(resourcesChangedBy('DELETE', '/api/leads/l-1/relationships/r-1')).toContain('households');
    expect(resourcesChangedBy('PUT', '/api/contacts/c-1')).not.toContain('households');
  });

  it("a club member's merge moves their applications and household links", () => {
    expect(resourcesChangedBy('POST', '/api/members/m-1/merge')).toEqual(expect.arrayContaining(['applications', 'households']));
  });
});

describe('what the signal carries', () => {
  it('names the kinds a write changed', () => {
    const heard = vi.fn();
    const off = onDataChanged(heard);
    announceDataChanged({ resources: ['tasks'] });
    off();
    expect(heard).toHaveBeenCalledWith({ resources: ['tasks'] });
    expect(touches(heard.mock.calls[0]![0], ['tasks'])).toBe(true);
    expect(touches(heard.mock.calls[0]![0], ['leads'])).toBe(false);
  });

  it('says "anything" when the write did not say', () => {
    const heard = vi.fn();
    const off = onDataChanged(heard);
    announceDataChanged();
    off();
    expect(touches(heard.mock.calls[0]![0], ['leads'])).toBe(true);
  });
});

describe('the signal', () => {
  it('reaches listeners, and stops after unsubscribing', () => {
    const heard = vi.fn();
    const off = onDataChanged(heard);
    announceDataChanged();
    expect(heard).toHaveBeenCalledTimes(1);
    off();
    announceDataChanged();
    expect(heard).toHaveBeenCalledTimes(1);
  });
});
