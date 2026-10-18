import { describe, expect, it } from 'vitest';
import { serializePipeline, type LeadPipeline } from '@/app/pages/leads/leadPipeline';
import { nextActionForLead, nextActions, type SuggestableLead } from './nextAction';

const now = new Date(2026, 9, 16);

function lead(pipeline: LeadPipeline, extra: Partial<SuggestableLead> = {}): SuggestableLead {
  return { id: 'l1', name: 'Ada', pipelineState: serializePipeline(pipeline), ...extra };
}

describe('nextActionForLead', () => {
  it('starts outreach when the pipeline is empty', () => {
    expect(nextActionForLead(lead({}), now)?.kind).toBe('start_outreach');
  });

  it('asks for another call after no answer', () => {
    expect(nextActionForLead(lead({ outreachStatus: 'attempted_no_answer' }), now)?.kind).toBe('try_again');
  });

  it('asks for the dropout reason before any other work on that lead', () => {
    const action = nextActionForLead(lead({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }), now);
    expect(action?.kind).toBe('record_reason');
    expect(action?.priority).toBe(95);
  });

  it('a meeting happening today outranks recording a reason on another lead', () => {
    // It used to be the other way round: "record why Luca dropped out" sat at
    // the top of the queue, above "the meeting with Jean-Michel is today".
    const ranked = nextActions([
      { id: 'lost', name: 'Luca', pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }) },
      { id: 'today', name: 'Jean-Michel', pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-10-16' }) },
    ], now);
    expect(ranked.map((a) => a.kind)).toEqual(['hold_meeting', 'record_reason']);
  });

  it('a missing reason still outranks every routine step', () => {
    const ranked = nextActions([
      { id: 'lost', name: 'Luca', pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }) },
      { id: 'sig', name: 'Anaïs', pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-09-01', meetingAttended: true, stillInterested: true, contractStatus: 'yes', contractSentDate: '2026-09-10', contractSigned: 'pending' }) },
      { id: 'dep', name: 'Marc', pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-09-01', meetingAttended: true, stillInterested: true, contractStatus: 'yes', contractSentDate: '2026-09-10', contractSigned: 'yes', signatureDate: '2026-09-12' }) },
    ], now);
    expect(ranked[0]?.kind).toBe('record_reason');
  });

  it('says nothing once a lost lead has its reason', () => {
    const action = nextActionForLead(lead({
      outreachStatus: 'contacted',
      contactOutcome: 'not_interested',
      dropoutReason: 'no_response',
    }), now);
    expect(action).toBeNull();
  });

  it('holds a meeting dated today, and prepares the day before', () => {
    const booked = {
      outreachStatus: 'contacted' as const,
      contactOutcome: 'meeting_scheduled' as const,
      meetingDate: '2026-10-16',
    };
    expect(nextActionForLead(lead(booked), now)?.kind).toBe('hold_meeting');
    expect(nextActionForLead(lead({ ...booked, meetingDate: '2026-10-17' }), now)?.priority).toBe(66);
  });

  it('waits for a later window when the no-answer was already today', () => {
    const action = nextActionForLead(
      lead({ outreachStatus: 'attempted_no_answer' }, { lastContactedAt: '2026-10-16T09:00:00' }),
      now,
    );
    expect(action?.priority).toBe(55);
    expect(action?.action).toMatch(/later today/);
  });

  it('books another meeting when a no-show is still interested', () => {
    const missed = {
      outreachStatus: 'contacted' as const,
      contactOutcome: 'meeting_scheduled' as const,
      meetingDate: '2026-10-01',
      meetingAttended: false as const,
    };
    expect(nextActionForLead(lead(missed), now)?.action).toMatch(/no-show is not a loss|Reschedule/);
    expect(nextActionForLead(lead({ ...missed, stillInterested: true }), now)?.action).toMatch(/another meeting/);
  });

  it('prepares for a future meeting and logs attendance after it', () => {
    const scheduled = {
      outreachStatus: 'contacted' as const,
      contactOutcome: 'meeting_scheduled' as const,
      meetingDate: '2026-10-20',
    };
    expect(nextActionForLead(lead(scheduled), now)?.kind).toBe('prepare_meeting');
    expect(nextActionForLead(lead({ ...scheduled, meetingDate: '2026-10-01' }), now)?.kind).toBe('log_attendance');
  });

  it('sends a contract that is waiting, then chases a pending signature', () => {
    const ready = {
      outreachStatus: 'contacted' as const,
      contactOutcome: 'meeting_scheduled' as const,
      meetingDate: '2026-10-01',
      meetingAttended: true,
      stillInterested: true,
    };
    expect(nextActionForLead(lead({ ...ready, contractStatus: 'to_be_sent' }), now)?.kind).toBe('send_contract');
    expect(nextActionForLead(lead({
      ...ready,
      contractStatus: 'yes',
      contractSentDate: '2026-10-02',
      contractSigned: 'pending',
    }), now)?.kind).toBe('chase_signature');
  });

  it('asks for the deposit after signature, and converts when it is paid', () => {
    const signed = {
      outreachStatus: 'contacted' as const,
      contactOutcome: 'meeting_scheduled' as const,
      meetingDate: '2026-10-01',
      meetingAttended: true,
      stillInterested: true,
      contractStatus: 'yes' as const,
      contractSentDate: '2026-10-02',
      contractSigned: 'yes' as const,
      signatureDate: '2026-10-03',
    };
    expect(nextActionForLead(lead(signed), now)?.kind).toBe('ask_deposit');
    expect(nextActionForLead(lead({
      ...signed,
      depositStatus: 'paid',
      paymentDate: '2026-10-04',
    }), now)?.kind).toBe('convert');
  });

  it('skips converted leads', () => {
    expect(nextActionForLead(lead({}, { isConverted: true }), now)).toBeNull();
  });
});

describe('nextActions', () => {
  it('puts the missing reason ahead of a first call', () => {
    const ranked = nextActions([
      lead({}, { id: 'new', name: 'New' }),
      lead({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }, { id: 'lost', name: 'Lost' }),
    ], now);
    expect(ranked.map((item) => item.leadId)).toEqual(['lost', 'new']);
  });
});
