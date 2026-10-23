// Extra records the sales rules do not already cover.
// One step per record, from a status or a date that is already saved.
// Nothing here writes, sends, or invents a follow-up the record does not have.

import { nextActionHint, type ContractStatus } from '@/app/pages/contracts/contractLifecycle';
import { calendarKey, daysBetween, todayKey } from './calendarDay';
import type { WorkSuggestion } from './workspace';

export interface CoverageMember {
  id: string;
  firstName: string;
  lastName: string;
  status: string;
  renewsAtUtc?: string | null;
  houseAccountBalance?: number | null;
}

export interface CoverageApplication {
  id: string;
  firstName: string;
  lastName: string;
  status: string;
  referredByName?: string | null;
}

export interface CoverageCharge {
  id: string;
  memberName: string;
  status: string;
  amount?: number | null;
  dueAtUtc?: string | null;
}

export interface CoverageReservation {
  id: string;
  memberName: string;
  resourceName: string;
  status: string;
  startAtUtc: string;
}

export interface CoverageSubscription {
  id: string;
  memberName: string;
  name: string;
  status: string;
}

export interface CoverageCampaign {
  id: string;
  name: string;
  status: string;
}

export interface CoverageEvent {
  id: string;
  name: string;
  status: string;
  startAtUtc: string;
  registeredCount?: number | null;
}

export interface CoverageNotification {
  id: string;
  title: string;
  read: boolean;
  link?: string | null;
}

export interface CoverageContract {
  id: string;
  title: string;
  status: string;
  leadId?: string | null;
  counterpartyName?: string | null;
}

export interface CoverageJoinRequest {
  id: string;
  userName: string;
  userEmail: string;
}

export interface CoverageGiftCard {
  id: string;
  recipientName: string;
  status: string;
  expiresAtUtc?: string | null;
}

export interface CoverageTab {
  id: string;
  memberName: string;
  status: string;
  total?: number | null;
}

export interface CoverageCommunication {
  id: string;
  memberName: string;
  subject: string;
  status: string;
}

export interface CoverageDonation {
  id: string;
  memberName: string;
  acknowledged: boolean;
}

export interface CoverageActivity {
  id: string;
  type: string;
  subject?: string | null;
  leadId?: string | null;
  dealId?: string | null;
  contactId?: string | null;
  leadName?: string | null;
  dealName?: string | null;
  contactName?: string | null;
  createdAt: string;
}

export interface CoverageOpenTask {
  leadId?: string | null;
  dealId?: string | null;
  contactId?: string | null;
  status?: string | null;
  completed?: boolean | null;
}

export interface CoverageVisit {
  memberId: string;
  visitedAtUtc: string;
}

export interface CoverageAudit {
  id: string;
  action: string;
  entityLabel?: string | null;
  entityType?: string | null;
  createdAtUtc: string;
}

export interface CoverageFailedEmail {
  id: string;
  to: string;
  subject?: string | null;
}

export interface CoverageHousehold {
  id: string;
  name: string;
}

export interface CoverageLoyalty {
  memberId: string;
  memberName: string;
  points: number;
}

export interface CoverageInput {
  now?: Date;
  members?: CoverageMember[];
  applications?: CoverageApplication[];
  charges?: CoverageCharge[];
  reservations?: CoverageReservation[];
  subscriptions?: CoverageSubscription[];
  campaigns?: CoverageCampaign[];
  events?: CoverageEvent[];
  notifications?: CoverageNotification[];
  contracts?: CoverageContract[];
  joinRequests?: CoverageJoinRequest[];
  giftCards?: CoverageGiftCard[];
  tabs?: CoverageTab[];
  communications?: CoverageCommunication[];
  donations?: CoverageDonation[];
  households?: CoverageHousehold[];
  /** Member ids that already have at least one person on the household. */
  householdMemberIds?: string[];
  loyalty?: CoverageLoyalty[];
  activities?: CoverageActivity[];
  openTasks?: CoverageOpenTask[];
  visits?: CoverageVisit[];
  audits?: CoverageAudit[];
  failedEmails?: CoverageFailedEmail[];
  /** Lead webhook, when the URL is a real one. A demo placeholder is ignored. */
  webhook?: { hasApiKey: boolean; webhookUrl: string } | null;
}

function step(partial: WorkSuggestion): WorkSuggestion {
  return partial;
}

function person(first: string, last: string): string {
  return `${first} ${last}`.trim() || 'Unnamed';
}

/** Steps owed by club, calendar, contract, and team records. */
export function coverageSteps(input: CoverageInput): WorkSuggestion[] {
  const now = input.now ?? new Date();
  const today = todayKey(now);
  const items: WorkSuggestion[] = [];

  for (const member of input.members ?? []) {
    const name = person(member.firstName, member.lastName);
    const href = `/members/${member.id}`;
    if (member.status === 'Lapsed') {
      items.push(step({
        key: `member:${member.id}`,
        href,
        name,
        list: 'Members',
        action: 'Renew this membership, or record why it lapsed.',
        because: 'The member status is Lapsed.',
        priority: 82,
      }));
      continue;
    }
    if (member.status === 'Pending') {
      items.push(step({
        key: `member:${member.id}`,
        href,
        name,
        list: 'Members',
        action: 'Finish activating this membership.',
        because: 'The member status is still Pending.',
        priority: 74,
      }));
      continue;
    }
    const renews = calendarKey(member.renewsAtUtc);
    if (member.status === 'Active' && renews && renews <= today) {
      items.push(step({
        key: `member:${member.id}`,
        href,
        name,
        list: 'Members',
        action: 'The renewal date has passed. Renew it, or mark the membership lapsed.',
        because: `Renewal was saved as ${renews}.`,
        priority: 80,
      }));
      continue;
    }
    if (typeof member.houseAccountBalance === 'number' && member.houseAccountBalance > 0 && member.status === 'Active') {
      items.push(step({
        key: `member:${member.id}`,
        href,
        name,
        list: 'Members',
        action: 'Collect the house-account balance.',
        because: `The house account shows ${member.houseAccountBalance}.`,
        priority: 62,
      }));
    }
  }

  for (const application of input.applications ?? []) {
    if (application.status !== 'Submitted' && application.status !== 'UnderReview' && application.status !== 'Waitlisted') continue;
    const referred = application.referredByName?.trim();
    items.push(step({
      key: `application:${application.id}`,
      href: '/applications',
      name: person(application.firstName, application.lastName),
      list: 'Applications',
      action: application.status === 'Waitlisted'
        ? 'Decide whether to offer a place from the waitlist.'
        : 'Review this application.',
      because: referred
        ? `Status is ${application.status}. Referred by ${referred}.`
        : `Status is ${application.status}.`,
      priority: application.status === 'Submitted' ? 76 : 70,
    }));
  }

  for (const charge of input.charges ?? []) {
    if (charge.status !== 'Pending' && charge.status !== 'PartiallyPaid' && charge.status !== 'Overdue') continue;
    const due = calendarKey(charge.dueAtUtc);
    const overdue = charge.status === 'Overdue' || (due !== null && due < today);
    items.push(step({
      key: `charge:${charge.id}`,
      href: '/charges',
      name: charge.memberName || 'Charge',
      list: 'Charges',
      action: overdue ? 'Collect this overdue charge.' : 'Collect this open charge.',
      because: due ? `Status is ${charge.status}. Due ${due}.` : `Status is ${charge.status}.`,
      priority: overdue ? 84 : 66,
    }));
  }

  for (const reservation of input.reservations ?? []) {
    if (reservation.status === 'Cancelled' || reservation.status === 'Completed' || reservation.status === 'CheckedIn') continue;
    const day = calendarKey(reservation.startAtUtc);
    const todayBooking = day === today && (reservation.status === 'Pending' || reservation.status === 'Confirmed');
    if (reservation.status === 'NoShow') {
      items.push(step({
        key: `reservation:${reservation.id}`,
        href: '/reservations',
        name: reservation.memberName || reservation.resourceName,
        list: 'Reservations',
        action: 'Record the follow-up for this no-show.',
        because: `${reservation.resourceName} is marked NoShow. A no-show is not a cancellation.`,
        priority: 78,
      }));
    } else if (todayBooking || reservation.status === 'Pending') {
      items.push(step({
        key: `reservation:${reservation.id}`,
        href: '/reservations',
        name: reservation.memberName || reservation.resourceName,
        list: 'Reservations',
        action: todayBooking ? 'This reservation is today. Confirm it is held.' : 'Confirm this pending reservation.',
        because: `${reservation.resourceName}, status ${reservation.status}${day ? `, ${day}` : ''}.`,
        priority: todayBooking ? 90 : 68,
      }));
    }
  }

  for (const subscription of input.subscriptions ?? []) {
    if (subscription.status !== 'PastDue' && subscription.status !== 'Paused') continue;
    items.push(step({
      key: `subscription:${subscription.id}`,
      href: '/subscriptions',
      name: subscription.memberName || subscription.name,
      list: 'Subscriptions',
      action: subscription.status === 'PastDue' ? 'Collect the past-due subscription.' : 'Decide whether to resume this paused subscription.',
      because: `${subscription.name} is ${subscription.status}.`,
      priority: subscription.status === 'PastDue' ? 80 : 58,
    }));
  }

  for (const campaign of input.campaigns ?? []) {
    if (campaign.status !== 'Draft' && campaign.status !== 'Scheduled') continue;
    items.push(step({
      key: `campaign:${campaign.id}`,
      href: '/campaigns',
      name: campaign.name,
      list: 'Campaigns',
      action: campaign.status === 'Draft' ? 'Finish this draft, or discard it.' : 'Check this scheduled campaign before it sends.',
      because: `Campaign status is ${campaign.status}.`,
      priority: 60,
    }));
  }

  for (const event of input.events ?? []) {
    if (event.status !== 'Published' && event.status !== 'Draft') continue;
    const day = calendarKey(event.startAtUtc);
    if (!day || day < today) continue;
    const soon = day <= today;
    if (event.status === 'Draft') {
      items.push(step({
        key: `event:${event.id}`,
        href: `/events/${event.id}`,
        name: event.name,
        list: 'Events',
        action: 'Publish this event, or cancel the draft.',
        because: `It is still a draft and starts ${day}.`,
        priority: 64,
      }));
    } else if ((event.registeredCount ?? 0) === 0) {
      items.push(step({
        key: `event:${event.id}`,
        href: `/events/${event.id}`,
        name: event.name,
        list: 'Events',
        action: 'Invite members. Nobody is registered yet.',
        because: `The event is published for ${day} and registered count is 0.`,
        priority: soon ? 72 : 55,
      }));
    }
  }

  for (const notice of input.notifications ?? []) {
    if (notice.read) continue;
    items.push(step({
      key: `notification:${notice.id}`,
      href: notice.link || '/dashboard',
      name: notice.title || 'Notification',
      list: 'Notifications',
      action: 'Read this unread alert.',
      because: 'It is still marked unread.',
      priority: 48,
    }));
  }

  for (const contract of input.contracts ?? []) {
    const hint = nextActionHint(contract.status as ContractStatus);
    if (!hint) continue;
    const href = contract.leadId ? `/leads/${contract.leadId}` : '/leads';
    items.push(step({
      key: `contract:${contract.id}`,
      href,
      name: contract.title || contract.counterpartyName || 'Contract',
      list: 'Contracts',
      action: hint,
      because: `Contract status is ${contract.status}.`,
      priority: contract.status === 'signed_by_client' ? 87 : contract.status === 'sent' ? 75 : 70,
    }));
  }

  for (const request of input.joinRequests ?? []) {
    items.push(step({
      key: `join:${request.id}`,
      href: '/team',
      name: request.userName || request.userEmail,
      list: 'Team',
      action: 'Accept or decline this request to join the workspace.',
      because: `${request.userEmail} is waiting on a decision.`,
      priority: 62,
    }));
  }

  for (const card of input.giftCards ?? []) {
    const expires = calendarKey(card.expiresAtUtc);
    if (card.status !== 'Active' || !expires || expires > today) continue;
    items.push(step({
      key: `gift:${card.id}`,
      href: '/gift-cards',
      name: card.recipientName || 'Gift card',
      list: 'Gift cards',
      action: 'This gift card is past its expiry date. Void it or extend it.',
      because: `Status is Active and expiry was ${expires}.`,
      priority: 57,
    }));
  }

  for (const tab of input.tabs ?? []) {
    if (tab.status !== 'Open') continue;
    items.push(step({
      key: `tab:${tab.id}`,
      href: '/tabs',
      name: tab.memberName || 'Open tab',
      list: 'Tabs',
      action: 'Close or settle this open tab.',
      because: typeof tab.total === 'number' ? `The tab is open and the total is ${tab.total}.` : 'The tab is still open.',
      priority: 63,
    }));
  }

  for (const message of input.communications ?? []) {
    if (message.status !== 'Failed' && message.status !== 'Bounced') continue;
    items.push(step({
      key: `communication:${message.id}`,
      href: '/communications',
      name: message.memberName || message.subject,
      list: 'Communications',
      action: 'This club message failed. Resend it from Communications, or correct the address.',
      because: `"${message.subject}" is ${message.status}.`,
      priority: 69,
    }));
  }

  for (const donation of input.donations ?? []) {
    if (donation.acknowledged) continue;
    items.push(step({
      key: `donation:${donation.id}`,
      href: '/donations',
      name: donation.memberName || 'Donation',
      list: 'Donations',
      action: 'Acknowledge this donation.',
      because: 'It is saved and not marked acknowledged.',
      priority: 54,
    }));
  }

  const linkedHouseholds = new Set(input.householdMemberIds ?? []);
  for (const household of input.households ?? []) {
    if (linkedHouseholds.has(household.id)) continue;
    items.push(step({
      key: `household:${household.id}`,
      href: '/households',
      name: household.name || 'Household',
      list: 'Households',
      action: 'Add a member to this household, or remove the empty household.',
      because: 'The household has nobody on it.',
      priority: 58,
    }));
  }

  for (const balance of input.loyalty ?? []) {
    if (balance.points >= 0) continue;
    items.push(step({
      key: `loyalty:${balance.memberId}`,
      href: '/loyalty',
      name: balance.memberName || 'Member',
      list: 'Loyalty',
      action: 'Review this loyalty balance. It is below zero.',
      because: `The ledger sums to ${balance.points} points.`,
      priority: 67,
    }));
  }

  const openKeys = new Set<string>();
  for (const task of input.openTasks ?? []) {
    if (task.completed || task.status === 'completed' || task.status === 'cancelled') continue;
    if (task.leadId) openKeys.add(`lead:${task.leadId}`);
    if (task.dealId) openKeys.add(`deal:${task.dealId}`);
    if (task.contactId) openKeys.add(`contact:${task.contactId}`);
  }
  let activityCount = 0;
  for (const activity of input.activities ?? []) {
    if (activityCount >= 8) break;
    if (!/call|meeting/i.test(activity.type)) continue;
    const day = calendarKey(activity.createdAt);
    if (!day || daysBetween(day, today) > 2 || daysBetween(day, today) < 0) continue;
    const recordKey = activity.leadId ? `lead:${activity.leadId}` : activity.dealId ? `deal:${activity.dealId}` : activity.contactId ? `contact:${activity.contactId}` : '';
    if (!recordKey || openKeys.has(recordKey)) continue;
    const href = activity.leadId ? `/leads/${activity.leadId}` : activity.dealId ? `/deals/${activity.dealId}` : activity.contactId ? `/contacts/${activity.contactId}` : '/activities';
    items.push(step({
      key: `activity:${activity.id}`,
      href,
      name: activity.leadName || activity.dealName || activity.contactName || activity.subject || 'Activity',
      list: 'Activities',
      action: 'Set the next step. This call or meeting has no open task on the same record.',
      because: `Logged on ${day} as ${activity.type}.`,
      priority: 64,
    }));
    activityCount += 1;
  }

  const lastVisit = new Map<string, string>();
  if (input.visits) {
  const byHref = new Map<string, WorkSuggestion>();
  for (const item of items) {
    if (!byHref.has(item.href)) byHref.set(item.href, item);
  }
  for (const visit of input.visits) {
    const day = calendarKey(visit.visitedAtUtc);
    if (!day) continue;
    const previous = lastVisit.get(visit.memberId);
    if (!previous || day > previous) lastVisit.set(visit.memberId, day);
  }
  let quietMembers = 0;
  for (const member of input.members ?? []) {
    if (member.status !== 'Active') continue;
    const seen = lastVisit.get(member.id);
    if (seen && daysBetween(seen, today) <= 45) continue;
    const href = `/members/${member.id}`;
    const sentence = seen ? `Last visit was ${seen}.` : 'No visit date is saved.';
    const existing = byHref.get(href);
    if (existing) {
      existing.because = `${existing.because} ${sentence}`;
      continue;
    }
    if (quietMembers >= 8) continue;
    items.push(step({
      key: `visit:${member.id}`,
      href,
      name: person(member.firstName, member.lastName),
      list: 'Visits',
      action: seen ? 'This active member has not visited in 45 days.' : 'This active member has no visit on file.',
      because: sentence,
      priority: 56,
    }));
    quietMembers += 1;
  }
  }

  let deletions = 0;
  for (const event of input.audits ?? []) {
    if (deletions >= 5 || event.action !== 'deleted') continue;
    const day = calendarKey(event.createdAtUtc);
    if (!day || daysBetween(day, today) > 7) continue;
    items.push(step({
      key: `audit:${event.id}`,
      href: '/audit',
      name: event.entityLabel || 'Deleted record',
      list: 'Audit',
      action: 'Review this deletion from the last seven days.',
      because: `${event.entityType ?? 'Record'} deleted on ${day}.`,
      priority: 50,
    }));
    deletions += 1;
  }

  for (const message of (input.failedEmails ?? []).slice(0, 5)) {
    items.push(step({
      key: `email:${message.id}`,
      href: '/history',
      name: message.to || 'Email',
      list: 'History',
      action: 'This outbound email failed. Correct the address or send it again.',
      because: message.subject ? `Subject: ${message.subject}.` : 'The saved status is failed.',
      priority: 71,
    }));
  }

  if (input.webhook && !input.webhook.hasApiKey && !input.webhook.webhookUrl.startsWith('demo:')) {
    items.push(step({
      key: 'webhook:key',
      href: '/leads/webhook',
      name: 'Lead webhook',
      list: 'Leads',
      action: 'Create an API key for the lead webhook.',
      because: 'The webhook URL is real and no API key is saved.',
      priority: 44,
    }));
  }

  return items;
}

export interface CoverageFacts {
  pipelineLines: string[];
  copyCount: number;
  requiredFieldCount: number;
}

/** Sentences for questions, from totals that are not themselves a next step. */
export function coverageFactLines(facts: CoverageFacts): string[] {
  const lines = [...facts.pipelineLines];
  if (facts.copyCount > 0) lines.push(`${facts.copyCount} draft${facts.copyCount === 1 ? '' : 's'} sit in copy history.`);
  if (facts.requiredFieldCount > 0) {
    lines.push(`${facts.requiredFieldCount} custom field${facts.requiredFieldCount === 1 ? ' is' : 's are'} marked required. Each record is not checked here.`);
  }
  return lines;
}
