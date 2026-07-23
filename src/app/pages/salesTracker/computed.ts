// Derived values for the Sales Tracker dashboard.
//
// Every selector below traces to a specific Excel formula in the source
// workbook so the numbers a user reads here match what they'd read there.

import { daysBetween, excelWeekNumber, parseDdMmYyyy, todayUtc } from './dateUtils';

/** Every field the Excel CONTACTS sheet tracks per row — the minimum shape
 *  computed metrics work with. Callers pass either raw sheet rows or the
 *  per-lead extras stored client-side; both satisfy this contract. */
export interface TrackedRow {
  outreachStatus: string;
  outreachDate: string;
  meetingScheduled: string;
  meetingDate: string;
  met: string;
  interestedAfterMtg: string;
  contractSent: string;
  contractSentDate: string;
  contractSigned: string;
  signatureDate: string;
  depositPaid: string;
}

type DeclineStage = 'After Meeting' | 'After Contract' | '';

/** S4 in the Excel: After Meeting if `H=No`, After Contract if `I=Yes AND K=No`. */
export function declineStage(c: TrackedRow): DeclineStage {
  if (c.interestedAfterMtg === 'No') return 'After Meeting';
  if (c.contractSent === 'Yes' && c.contractSigned === 'No') return 'After Contract';
  return '';
}

/** Q4: today − contract-sent-date when the contract is still pending. */
export function daysContractOutstanding(c: TrackedRow): number | null {
  if (c.contractSent !== 'Yes') return null;
  const sent = parseDdMmYyyy(c.contractSentDate);
  if (!sent) return null;
  const isPending = c.contractSigned === 'Pending' || c.contractSigned === '';
  if (!isPending) return null;
  return daysBetween(sent, todayUtc());
}

/** R4: signature-date − contract-sent-date, only when both are present. */
export function daysToSign(c: TrackedRow): number | null {
  const sent = parseDdMmYyyy(c.contractSentDate);
  const signed = parseDdMmYyyy(c.signatureDate);
  if (!sent || !signed) return null;
  return daysBetween(sent, signed);
}

export function meetingWeek(c: TrackedRow): number | null {
  const d = parseDdMmYyyy(c.meetingDate);
  return d ? excelWeekNumber(d) : null;
}

// ---- KPI counts ----------------------------------------------------------

const countBy = <T>(items: T[], pred: (t: T) => boolean) =>
  items.reduce((n, t) => (pred(t) ? n + 1 : n), 0);

export interface Kpis {
  outreachAttempts: number;
  contactedSuccessfully: number;
  meetingsHeld: number;
  interested: number;
  contractsSent: number;
  signed: number;
  depositPaid: number;
  notInterested: number;
  pendingSignature: number;
  showedUp: number;
  noShows: number;
}

export function computeKpis(contacts: TrackedRow[]): Kpis {
  return {
    // B13: contacted + attempted no answer
    outreachAttempts: countBy(
      contacts,
      (c) => c.outreachStatus === 'Contacted' || c.outreachStatus === 'Attempted – No Answer',
    ),
    contactedSuccessfully: countBy(contacts, (c) => c.outreachStatus === 'Contacted'),
    meetingsHeld: countBy(contacts, (c) => c.meetingScheduled === 'Yes'),
    interested: countBy(contacts, (c) => c.interestedAfterMtg === 'Yes'),
    contractsSent: countBy(contacts, (c) => c.contractSent === 'Yes'),
    signed: countBy(contacts, (c) => c.contractSigned === 'Yes'),
    depositPaid: countBy(contacts, (c) => c.depositPaid === 'Yes'),
    // F17 in Excel: interested = "No" plus contract = "No"
    notInterested: countBy(
      contacts,
      (c) => c.interestedAfterMtg === 'No' || c.contractSent === 'No',
    ),
    pendingSignature: countBy(contacts, (c) => c.contractSigned === 'Pending'),
    showedUp: countBy(contacts, (c) => c.met === 'Yes'),
    noShows: countBy(contacts, (c) => c.met === 'No'),
  };
}

// ---- Conversion rates ----------------------------------------------------

const safeDiv = (num: number, den: number) => (den > 0 ? num / den : 0);

export interface ConversionRates {
  contactedOverWaitlist: number;
  meetingRate: number;
  interestRate: number;
  contractRate: number;
  signatureRate: number;
  closeRate: number;
}

export function computeRates(kpis: Kpis, waitlistTotal: number): ConversionRates {
  return {
    contactedOverWaitlist: safeDiv(kpis.contactedSuccessfully, waitlistTotal),
    meetingRate: safeDiv(kpis.meetingsHeld, kpis.contactedSuccessfully),
    interestRate: safeDiv(kpis.interested, kpis.showedUp),
    contractRate: safeDiv(kpis.contractsSent, kpis.interested),
    signatureRate: safeDiv(kpis.signed, kpis.contractsSent),
    closeRate: safeDiv(kpis.signed, kpis.contactedSuccessfully),
  };
}

// ---- Contract timing ----------------------------------------------------

export interface ContractTiming {
  avgDaysToSign: number | null;
  avgDaysOutstanding: number | null;
  maxDaysOutstanding: number | null;
  outstandingOver30: number;
  outstandingOver14: number;
}

export function computeContractTiming(contacts: TrackedRow[]): ContractTiming {
  const daysToSignValues: number[] = [];
  const daysOutstandingValues: number[] = [];
  for (const c of contacts) {
    const dts = daysToSign(c);
    if (dts != null && dts >= 0) daysToSignValues.push(dts);
    const dout = daysContractOutstanding(c);
    if (dout != null && dout >= 0) daysOutstandingValues.push(dout);
  }
  const avg = (xs: number[]) => (xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : null);
  return {
    avgDaysToSign: avg(daysToSignValues),
    avgDaysOutstanding: avg(daysOutstandingValues),
    maxDaysOutstanding: daysOutstandingValues.length ? Math.max(...daysOutstandingValues) : null,
    outstandingOver30: daysOutstandingValues.filter((d) => d >= 30).length,
    outstandingOver14: daysOutstandingValues.filter((d) => d >= 14).length,
  };
}

// ---- Drop-off analysis --------------------------------------------------

export interface DropOff {
  declinedAfterMeeting: number;
  declinedAfterContract: number;
  declinedAfterMeetingPct: number;
  declinedAfterContractPct: number;
  notInterestedInMeeting: number;
  noShowAtMeeting: number;
  rejectedOrNoLongerInterested: number;
}

export function computeDropOff(contacts: TrackedRow[]): DropOff {
  const declinedAfterMeeting = countBy(contacts, (c) => declineStage(c) === 'After Meeting');
  const declinedAfterContract = countBy(contacts, (c) => declineStage(c) === 'After Contract');
  const meetingsHeld = countBy(contacts, (c) => c.met === 'Yes');
  const contractsSent = countBy(contacts, (c) => c.contractSent === 'Yes');
  return {
    declinedAfterMeeting,
    declinedAfterContract,
    declinedAfterMeetingPct: safeDiv(declinedAfterMeeting, meetingsHeld),
    declinedAfterContractPct: safeDiv(declinedAfterContract, contractsSent),
    notInterestedInMeeting: countBy(contacts, (c) => c.meetingScheduled === 'Not interested in meeting'),
    noShowAtMeeting: countBy(contacts, (c) => c.met === 'No'),
    rejectedOrNoLongerInterested: countBy(
      contacts,
      (c) => c.contractSent === 'Rejected' || c.contractSent === 'No longer interested',
    ),
  };
}

// ---- Weekly meetings ----------------------------------------------------

export interface WeeklyRow {
  weekNumber: number;
  weekStarting: Date;
  meetingsHeld: number;
  contractsSent: number;
  signed: number;
}

/** Last N Excel weeks up to and including the current week — mirrors the
 *  Excel dashboard's WEEKLY MEETINGS table (rows 49-57):
 *    F = COUNTIF(P, weekNum)                           — meetings held
 *    H = COUNTIFS(I,"Yes", P, weekNum)                 — contracts sent
 *    J = COUNTIFS(K,"Yes", P, weekNum)                 — signed
 *  Excel's P column is `WEEKNUM(F, 2)`, only populated when the meeting
 *  date (F) is set — so filtering by `meetingWeek(c) === weekNumber`
 *  matches Excel's semantics without an extra `meetingScheduled === 'Yes'`
 *  clause. */
export function computeWeeklyMeetings(contacts: TrackedRow[], weeksBack = 10): WeeklyRow[] {
  const today = todayUtc();
  const currentWeek = excelWeekNumber(today);
  const monday = new Date(today);
  const day = monday.getUTCDay() || 7;
  monday.setUTCDate(monday.getUTCDate() - (day - 1));

  const rows: WeeklyRow[] = [];
  for (let i = weeksBack - 1; i >= 0; i -= 1) {
    const weekStart = new Date(monday);
    weekStart.setUTCDate(monday.getUTCDate() - i * 7);
    const weekNumber = currentWeek - i;
    const meetingsHeld = countBy(contacts, (c) => meetingWeek(c) === weekNumber);
    const contractsSent = countBy(
      contacts,
      (c) => meetingWeek(c) === weekNumber && c.contractSent === 'Yes',
    );
    const signed = countBy(
      contacts,
      (c) => meetingWeek(c) === weekNumber && c.contractSigned === 'Yes',
    );
    rows.push({ weekNumber, weekStarting: weekStart, meetingsHeld, contractsSent, signed });
  }
  return rows;
}

// ---- Meeting pipeline ---------------------------------------------------

export interface MeetingPipeline {
  notInterestedInMeeting: number;
  meetingToBeScheduled: number;
  contractsToSend: number;
  meetingRefusalRate: number;
}

export function computeMeetingPipeline(contacts: TrackedRow[]): MeetingPipeline {
  const notInterested = countBy(contacts, (c) => c.meetingScheduled === 'Not interested in meeting');
  const contacted = countBy(contacts, (c) => c.outreachStatus === 'Contacted');
  return {
    notInterestedInMeeting: notInterested,
    meetingToBeScheduled: countBy(contacts, (c) => c.meetingScheduled === 'Meeting to be scheduled'),
    contractsToSend: countBy(contacts, (c) => c.contractSent === 'To be sent'),
    meetingRefusalRate: safeDiv(notInterested, contacted),
  };
}

// ---- Funnel & breakdowns for charts -------------------------------------

export interface FunnelStep {
  name: string;
  value: number;
}

/** Funnel stages Q28-Q34 of the Excel workbook:
 *    Contacted → Mtg Scheduled → Showed Up → Interested → Contract Sent
 *    → Signed → Deposit Paid
 *  Contract Sent matches Q32 which unions "Yes" + "To be sent".            */
export function computeFunnel(contacts: TrackedRow[]): FunnelStep[] {
  const contacted = countBy(
    contacts,
    (c) => c.outreachStatus === 'Contacted' || c.outreachStatus === 'Attempted – No Answer',
  );
  return [
    { name: 'Contacted', value: contacted },
    { name: 'Mtg Scheduled', value: countBy(contacts, (c) => c.meetingScheduled === 'Yes') },
    { name: 'Showed Up', value: countBy(contacts, (c) => c.met === 'Yes') },
    { name: 'Interested', value: countBy(contacts, (c) => c.interestedAfterMtg === 'Yes') },
    {
      name: 'Contract Sent',
      value: countBy(
        contacts,
        (c) => c.contractSent === 'Yes' || c.contractSent === 'To be sent',
      ),
    },
    { name: 'Signed', value: countBy(contacts, (c) => c.contractSigned === 'Yes') },
    { name: 'Deposit Paid', value: countBy(contacts, (c) => c.depositPaid === 'Yes') },
  ];
}

export interface MonthlyRow {
  month: string;
  cumulativeContacted: number;
  cumulativeSigned: number;
}

/** Cumulative monthly contacted/signed for the calendar year of the workbook (defaults to today's year). */
export function computeMonthlyCumulative(
  contacts: TrackedRow[],
  year: number = todayUtc().getUTCFullYear(),
): MonthlyRow[] {
  const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
  let contactedRun = 0;
  let signedRun = 0;
  return months.map((m, idx) => {
    const monthNum = idx + 1;
    const contactedThis = countBy(contacts, (c) => {
      const d = parseDdMmYyyy(c.outreachDate);
      return !!d && d.getUTCFullYear() === year && d.getUTCMonth() + 1 === monthNum;
    });
    const signedThis = countBy(contacts, (c) => {
      const d = parseDdMmYyyy(c.signatureDate);
      return !!d && d.getUTCFullYear() === year && d.getUTCMonth() + 1 === monthNum;
    });
    contactedRun += contactedThis;
    signedRun += signedThis;
    return { month: m, cumulativeContacted: contactedRun, cumulativeSigned: signedRun };
  });
}

export interface CountRow {
  name: string;
  value: number;
}

export function computeContractStatusBreakdown(contacts: TrackedRow[]): CountRow[] {
  return [
    { name: 'Signed', value: countBy(contacts, (c) => c.contractSigned === 'Yes') },
    { name: 'Pending Signature', value: countBy(contacts, (c) => c.contractSigned === 'Pending') },
    { name: 'To Be Sent', value: countBy(contacts, (c) => c.contractSent === 'To be sent') },
    {
      name: 'Declined',
      value: countBy(
        contacts,
        (c) => c.contractSigned === 'No' || c.contractSent === 'No',
      ),
    },
  ];
}

export function computeDropOffBreakdown(contacts: TrackedRow[]): CountRow[] {
  return [
    { name: 'Not interested in meeting', value: countBy(contacts, (c) => c.meetingScheduled === 'Not interested in meeting') },
    { name: 'No-show at meeting', value: countBy(contacts, (c) => c.met === 'No') },
    { name: 'Declined after meeting', value: countBy(contacts, (c) => declineStage(c) === 'After Meeting') },
    { name: 'Declined after contract', value: countBy(contacts, (c) => declineStage(c) === 'After Contract') },
    {
      name: 'Rejected / No longer interested',
      value: countBy(
        contacts,
        (c) => c.contractSent === 'Rejected' || c.contractSent === 'No longer interested',
      ),
    },
  ];
}

export function computeMembershipStatus(contacts: TrackedRow[]): CountRow[] {
  return [
    { name: 'Deposit Paid', value: countBy(contacts, (c) => c.depositPaid === 'Yes') },
    {
      name: 'Signed, No Deposit',
      value: countBy(contacts, (c) => c.contractSigned === 'Yes' && c.depositPaid !== 'Yes'),
    },
    { name: 'Pending Signature', value: countBy(contacts, (c) => c.contractSigned === 'Pending') },
  ];
}

// ---- Outcome breakdowns (Interested / Not / Pending, etc.) -------------

export function computeOutcomeBreakdown(contacts: TrackedRow[]): CountRow[] {
  const interested = countBy(contacts, (c) => c.interestedAfterMtg === 'Yes');
  const notInterested = countBy(
    contacts,
    (c) => c.interestedAfterMtg === 'No' || c.contractSent === 'No',
  );
  const met = countBy(contacts, (c) => c.met === 'Yes');
  const pending = Math.max(met - interested - countBy(contacts, (c) => c.interestedAfterMtg === 'No'), 0);
  return [
    { name: 'Interested', value: interested },
    { name: 'Not Interested', value: notInterested },
    { name: 'Pending / TBD', value: pending },
  ];
}
