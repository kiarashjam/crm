// Maps lead.pipelineState ↔ Excel-shaped TrackedRow fields.
// The Leads funnel / tracker dashboards read from leads only (server JSON),
// not from browser-local sales extras.

import type { Lead } from '@/app/api/types';
import type { TrackedRow } from '../salesTracker/computed';
import { ddMmYyyyToIso, isoToDdMmYyyy } from '../salesTracker/dateUtils';
import {
  parsePipeline,
  serializePipeline,
  type LeadPipeline,
  type OutreachStatus as PipelineOutreach,
  type ContactOutcome,
  type ContractStatus,
  type ContractSigned as PipelineSigned,
} from './leadPipeline';
import type { SalesExtras } from './salesExtrasStore';
import { EMPTY_SALES_EXTRAS } from './salesExtrasStore';

function isoToExcel(iso?: string): string {
  if (!iso) return '';
  // Already dd.mm.yyyy?
  if (/^\d{2}\.\d{2}\.\d{4}$/.test(iso.trim())) return iso.trim();
  // yyyy-mm-dd or datetime
  const day = iso.slice(0, 10);
  if (/^\d{4}-\d{2}-\d{2}$/.test(day)) return isoToDdMmYyyy(day);
  return '';
}

function excelToIso(excel?: string): string | undefined {
  if (!excel || !String(excel).trim()) return undefined;
  const s = String(excel).trim();
  if (/^\d{4}-\d{2}-\d{2}/.test(s)) return s.slice(0, 10);
  const iso = ddMmYyyyToIso(s);
  return iso || undefined;
}

/** Lead pipeline JSON → Excel-parity row used by KPI/funnel math. */
export function pipelineToTrackedRow(p: LeadPipeline): TrackedRow {
  let outreachStatus = '';
  if (p.outreachStatus === 'contacted') outreachStatus = 'Contacted';
  else if (p.outreachStatus === 'attempted_no_answer') outreachStatus = 'Attempted – No Answer';

  let meetingScheduled = '';
  if (p.contactOutcome === 'meeting_scheduled') meetingScheduled = 'Yes';
  else if (p.contactOutcome === 'follow_up') meetingScheduled = 'Meeting to be scheduled';
  else if (p.contactOutcome === 'not_interested') meetingScheduled = 'Not interested in meeting';

  let met = '';
  if (p.meetingAttended === true) met = 'Yes';
  else if (p.meetingAttended === false) met = 'No';

  let interestedAfterMtg = '';
  if (p.stillInterested === true) interestedAfterMtg = 'Yes';
  else if (p.stillInterested === false) interestedAfterMtg = 'No';

  let contractSent = '';
  if (p.contractStatus === 'yes') contractSent = 'Yes';
  else if (p.contractStatus === 'to_be_sent') contractSent = 'To be sent';
  else if (p.contractStatus === 'profile_rejected') contractSent = 'Rejected';
  else if (p.contractStatus === 'no_longer_interested') contractSent = 'No longer interested';

  let contractSigned = '';
  if (p.contractSigned === 'yes') contractSigned = 'Yes';
  else if (p.contractSigned === 'pending') contractSigned = 'Pending';
  else if (p.contractSigned === 'no') contractSigned = 'No';

  let depositPaid = '';
  if (p.depositPaid === true) depositPaid = 'Yes';
  else if (p.depositPaid === false) depositPaid = 'No';

  return {
    outreachStatus,
    outreachDate: isoToExcel(p.outreachDate),
    meetingScheduled,
    meetingDate: isoToExcel(p.meetingDate),
    met,
    interestedAfterMtg,
    contractSent,
    contractSentDate: isoToExcel(p.contractSentDate),
    contractSigned,
    signatureDate: isoToExcel(p.signatureDate),
    depositPaid,
  };
}

/** Excel-parity row → lead pipeline JSON persisted on the lead. */
export function trackedRowToPipeline(row: TrackedRow, base: LeadPipeline = {}): LeadPipeline {
  const next: LeadPipeline = { ...base };

  const ost = (row.outreachStatus || '').trim();
  if (ost === 'Contacted') next.outreachStatus = 'contacted';
  else if (ost === 'Attempted – No Answer' || ost.startsWith('Attempted')) next.outreachStatus = 'attempted_no_answer';
  else if (!ost) delete next.outreachStatus;

  const od = excelToIso(row.outreachDate);
  if (od) next.outreachDate = od;
  else delete next.outreachDate;

  const ms = (row.meetingScheduled || '').trim();
  if (ms === 'Yes') next.contactOutcome = 'meeting_scheduled';
  else if (ms === 'Meeting to be scheduled') next.contactOutcome = 'follow_up';
  else if (ms === 'Not interested in meeting') next.contactOutcome = 'not_interested';
  else if (!ms) delete next.contactOutcome;

  const md = excelToIso(row.meetingDate);
  if (md) next.meetingDate = md;
  else delete next.meetingDate;

  if (row.met === 'Yes') next.meetingAttended = true;
  else if (row.met === 'No') next.meetingAttended = false;
  else delete next.meetingAttended;

  if (row.interestedAfterMtg === 'Yes') next.stillInterested = true;
  else if (row.interestedAfterMtg === 'No') next.stillInterested = false;
  else delete next.stillInterested;

  const cs = (row.contractSent || '').trim();
  if (cs === 'Yes') next.contractStatus = 'yes';
  else if (cs === 'To be sent' || cs === 'Pending') next.contractStatus = 'to_be_sent';
  else if (cs === 'Rejected') next.contractStatus = 'profile_rejected';
  else if (cs === 'No longer interested' || cs === 'Not interested' || cs === 'No') next.contractStatus = 'no_longer_interested';
  else if (!cs) delete next.contractStatus;

  const csd = excelToIso(row.contractSentDate);
  if (csd) next.contractSentDate = csd;
  else delete next.contractSentDate;

  const signed = (row.contractSigned || '').trim();
  if (signed === 'Yes') next.contractSigned = 'yes';
  else if (signed === 'Pending') next.contractSigned = 'pending';
  else if (signed === 'No') next.contractSigned = 'no';
  else if (!signed) delete next.contractSigned;

  const sd = excelToIso(row.signatureDate);
  if (sd) next.signatureDate = sd;
  else delete next.signatureDate;

  if (row.depositPaid === 'Yes') next.depositPaid = true;
  else if (row.depositPaid === 'No') next.depositPaid = false;
  else delete next.depositPaid;

  return next;
}

export function salesExtrasToTrackedRow(ex: SalesExtras): TrackedRow {
  return {
    outreachStatus: ex.outreachStatus || '',
    outreachDate: ex.outreachDate || '',
    meetingScheduled: ex.meetingScheduled || '',
    meetingDate: ex.meetingDate || '',
    met: ex.met || '',
    interestedAfterMtg: ex.interestedAfterMtg || '',
    contractSent: ex.contractSent || '',
    contractSentDate: ex.contractSentDate || '',
    contractSigned: ex.contractSigned || '',
    signatureDate: ex.signatureDate || '',
    depositPaid: ex.depositPaid || '',
  };
}

export function trackedRowToSalesExtras(row: TrackedRow, notes?: string, lastContact?: string): SalesExtras {
  return {
    ...EMPTY_SALES_EXTRAS,
    outreachStatus: (row.outreachStatus || '') as SalesExtras['outreachStatus'],
    outreachDate: row.outreachDate || '',
    meetingScheduled: (row.meetingScheduled || '') as SalesExtras['meetingScheduled'],
    meetingDate: row.meetingDate || '',
    met: (row.met || '') as SalesExtras['met'],
    interestedAfterMtg: (row.interestedAfterMtg || '') as SalesExtras['interestedAfterMtg'],
    contractSent: (row.contractSent || '') as SalesExtras['contractSent'],
    contractSentDate: row.contractSentDate || '',
    contractSigned: (row.contractSigned || '') as SalesExtras['contractSigned'],
    signatureDate: row.signatureDate || '',
    depositPaid: (row.depositPaid || '') as SalesExtras['depositPaid'],
    lastContactDate: lastContact || '',
    salesNotes: notes || '',
  };
}

/** Build tracker row from a lead (server pipelineState). */
export function leadToTrackedRow(lead: Lead): TrackedRow {
  return pipelineToTrackedRow(parsePipeline(lead.pipelineState));
}

/** All leads → rows for funnel / KPI computation. */
export function buildTrackedRowsFromLeads(leads: Lead[]): TrackedRow[] {
  return leads.map(leadToTrackedRow);
}

export function leadHasTrackerData(lead: Lead): boolean {
  const row = leadToTrackedRow(lead);
  return Object.values(row).some((v) => v && String(v).trim() !== '');
}

export function serializeTrackedRowAsPipeline(row: TrackedRow, existingRaw?: string | null): string {
  const base = parsePipeline(existingRaw);
  return serializePipeline(trackedRowToPipeline(row, base));
}

// Re-export helpers useful at call sites
export type { LeadPipeline, PipelineOutreach, ContactOutcome, ContractStatus, PipelineSigned };
