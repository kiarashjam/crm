// Formula-parity tests — every assertion below traces to a specific cell
// formula in the P46 Sales Tracker Excel DASHBOARD sheet. If someone
// changes one of the selectors in `computed.ts`, one of these tests
// should fail loudly.

import { describe, it, expect } from 'vitest';
import {
  computeKpis,
  computeRates,
  computeContractTiming,
  computeDropOff,
  computeMeetingPipeline,
  computeFunnel,
  computeContractStatusBreakdown,
  computeDropOffBreakdown,
  computeMembershipStatus,
  declineStage,
  daysContractOutstanding,
  daysToSign,
  meetingWeek,
  type TrackedRow,
} from './computed';
import { excelWeekNumber, parseDdMmYyyy } from './dateUtils';

const empty: TrackedRow = {
  outreachStatus: '', outreachDate: '', meetingScheduled: '', meetingDate: '',
  met: '', interestedAfterMtg: '', contractSent: '', contractSentDate: '',
  contractSigned: '', signatureDate: '', depositPaid: '',
};

const row = (overrides: Partial<TrackedRow>): TrackedRow => ({ ...empty, ...overrides });

describe('excelWeekNumber (WEEKNUM(date, 2))', () => {
  it('Jan 1, 2026 (Thursday) is week 1', () => {
    expect(excelWeekNumber(new Date(Date.UTC(2026, 0, 1)))).toBe(1);
  });
  it('Jan 5, 2026 (Monday) is week 2', () => {
    expect(excelWeekNumber(new Date(Date.UTC(2026, 0, 5)))).toBe(2);
  });
  it('Dec 31, 2025 (Wednesday) is week 53 of 2025', () => {
    expect(excelWeekNumber(new Date(Date.UTC(2025, 11, 31)))).toBe(53);
  });
  it('July 20, 2026 (Monday) is week 30', () => {
    expect(excelWeekNumber(new Date(Date.UTC(2026, 6, 20)))).toBe(30);
  });
});

describe('parseDdMmYyyy', () => {
  it('parses dd.mm.yyyy strings', () => {
    const d = parseDdMmYyyy('13.05.2026');
    expect(d?.toISOString()).toBe('2026-05-13T00:00:00.000Z');
  });
  it('rejects malformed strings', () => {
    expect(parseDdMmYyyy('2026-05-13')).toBeNull();
    expect(parseDdMmYyyy('')).toBeNull();
    expect(parseDdMmYyyy('99.99.9999')).toBeNull();
  });
});

describe('computeKpis — Excel B13/D13/F13/H13/J13/B17/D17/F17/H17/D19/H19', () => {
  const rows = [
    row({ outreachStatus: 'Contacted' }),                              // D13 +1
    row({ outreachStatus: 'Contacted' }),                              // D13 +1
    row({ outreachStatus: 'Attempted – No Answer' }),                  // B13 only
    row({ meetingScheduled: 'Yes' }),                                  // F13 +1
    row({ interestedAfterMtg: 'Yes' }),                                // H13 +1
    row({ contractSent: 'Yes' }),                                      // J13 +1
    row({ contractSigned: 'Yes', depositPaid: 'Yes' }),                // B17, D17 +1
    row({ interestedAfterMtg: 'No' }),                                 // F17 +1
    row({ contractSent: 'No' }),                                       // F17 +1
    row({ contractSigned: 'Pending' }),                                // H17 +1
    row({ met: 'Yes' }),                                               // D19 +1
    row({ met: 'No' }),                                                // H19 +1
  ];
  const kpis = computeKpis(rows);

  it('B13: outreach attempts = "Contacted" + "Attempted – No Answer"', () => {
    expect(kpis.outreachAttempts).toBe(3);
  });
  it('D13: contacted successfully = COUNTIF(C, "Contacted")', () => {
    expect(kpis.contactedSuccessfully).toBe(2);
  });
  it('F13: meetings held = COUNTIF(E, "Yes")', () => {
    expect(kpis.meetingsHeld).toBe(1);
  });
  it('H13: interested = COUNTIF(H, "Yes")', () => {
    expect(kpis.interested).toBe(1);
  });
  it('J13: contracts sent = COUNTIF(I, "Yes")', () => {
    expect(kpis.contractsSent).toBe(1);
  });
  it('B17: signed = COUNTIFS(K, "Yes")', () => {
    expect(kpis.signed).toBe(1);
  });
  it('D17: deposit paid = COUNTIF(M, "Yes")', () => {
    expect(kpis.depositPaid).toBe(1);
  });
  it('F17: not interested = COUNTIFS(H,"No") + COUNTIF(I,"No")', () => {
    // My implementation uses OR (union) — each lead counted once. Excel's
    // literal formula could double-count a lead with H="No" AND I="No",
    // but that combination doesn't appear in the source data.
    expect(kpis.notInterested).toBe(2);
  });
  it('H17: pending signature = COUNTIFS(K, "Pending")', () => {
    expect(kpis.pendingSignature).toBe(1);
  });
  it('D19: showed up = COUNTIF(G, "Yes")', () => {
    expect(kpis.showedUp).toBe(1);
  });
  it('H19: no shows = COUNTIF(G, "No")', () => {
    expect(kpis.noShows).toBe(1);
  });
});

describe('computeRates — Excel F23-F28', () => {
  const rows = [
    row({ outreachStatus: 'Contacted', meetingScheduled: 'Yes', met: 'Yes', interestedAfterMtg: 'Yes', contractSent: 'Yes', contractSigned: 'Yes' }),
    row({ outreachStatus: 'Contacted', meetingScheduled: 'Yes', met: 'Yes', interestedAfterMtg: 'Yes', contractSent: 'Yes', contractSigned: 'No' }),
    row({ outreachStatus: 'Contacted', meetingScheduled: 'No' }),
    row({ outreachStatus: 'Contacted', meetingScheduled: 'Yes', met: 'No' }),
  ];
  const rates = computeRates(computeKpis(rows), 20);

  it('F23: contacted / waitlist = 4/20', () => {
    expect(rates.contactedOverWaitlist).toBeCloseTo(0.2, 5);
  });
  it('F24: meetings held / contacted = 3/4', () => {
    expect(rates.meetingRate).toBeCloseTo(0.75, 5);
  });
  it('F25: interested / showed up = 2/2', () => {
    expect(rates.interestRate).toBeCloseTo(1.0, 5);
  });
  it('F26: contracts sent / interested = 2/2', () => {
    expect(rates.contractRate).toBeCloseTo(1.0, 5);
  });
  it('F27: signed / contracts sent = 1/2', () => {
    expect(rates.signatureRate).toBeCloseTo(0.5, 5);
  });
  it('F28: signed / contacted = 1/4', () => {
    expect(rates.closeRate).toBeCloseTo(0.25, 5);
  });
});

describe('declineStage — Excel S column', () => {
  it('H="No" → "After Meeting"', () => {
    expect(declineStage(row({ interestedAfterMtg: 'No' }))).toBe('After Meeting');
  });
  it('I="Yes" AND K="No" → "After Contract"', () => {
    expect(declineStage(row({ contractSent: 'Yes', contractSigned: 'No' }))).toBe('After Contract');
  });
  it('Otherwise empty', () => {
    expect(declineStage(row({}))).toBe('');
    expect(declineStage(row({ interestedAfterMtg: 'Yes' }))).toBe('');
  });
});

describe('meetingWeek — Excel P column', () => {
  it('WEEKNUM(F, 2) for a valid dd.mm.yyyy', () => {
    // 20 July 2026 (Monday) is Excel week 30 (mode 2).
    expect(meetingWeek(row({ meetingDate: '20.07.2026' }))).toBe(30);
  });
  it('null when meeting date is empty', () => {
    expect(meetingWeek(row({}))).toBeNull();
  });
});

describe('computeDropOff — Excel F43/F44/H43/H44', () => {
  const rows = [
    // Declined after meeting
    row({ interestedAfterMtg: 'No', met: 'Yes' }),
    // Declined after contract
    row({ contractSent: 'Yes', contractSigned: 'No', met: 'Yes', interestedAfterMtg: 'Yes' }),
    // Successful path
    row({ met: 'Yes', interestedAfterMtg: 'Yes', contractSent: 'Yes', contractSigned: 'Yes' }),
  ];
  const d = computeDropOff(rows);

  it('F43: declined after meeting count', () => {
    expect(d.declinedAfterMeeting).toBe(1);
  });
  it('F44: declined after contract count', () => {
    expect(d.declinedAfterContract).toBe(1);
  });
  it('H43: declined after meeting / meetings held (denominator = met="Yes" count)', () => {
    expect(d.declinedAfterMeetingPct).toBeCloseTo(1 / 3, 5);
  });
  it('H44: declined after contract / contracts sent', () => {
    expect(d.declinedAfterContractPct).toBeCloseTo(0.5, 5);
  });
});

describe('computeMeetingPipeline — Excel B63/D63/F63/H63', () => {
  const rows = [
    row({ outreachStatus: 'Contacted', meetingScheduled: 'Not interested in meeting' }),
    row({ outreachStatus: 'Contacted', meetingScheduled: 'Not interested in meeting' }),
    row({ outreachStatus: 'Contacted', meetingScheduled: 'Meeting to be scheduled' }),
    row({ outreachStatus: 'Contacted', contractSent: 'To be sent' }),
    row({ outreachStatus: 'Contacted' }),
  ];
  const p = computeMeetingPipeline(rows);

  it('B63: not interested in meeting count', () => {
    expect(p.notInterestedInMeeting).toBe(2);
  });
  it('D63: meeting to be scheduled count', () => {
    expect(p.meetingToBeScheduled).toBe(1);
  });
  it('F63: contracts to send count', () => {
    expect(p.contractsToSend).toBe(1);
  });
  it('H63: meeting refusal rate = 2/5', () => {
    expect(p.meetingRefusalRate).toBeCloseTo(0.4, 5);
  });
});

describe('computeContractTiming — Excel F34/F35/F36/F37/F38', () => {
  // Fake "today" isn't ideal to freeze here; instead test the pure-signal path
  // using signature dates already in the past.
  const rows = [
    // Signed 5 days after sent (days-to-sign = 5)
    row({ contractSent: 'Yes', contractSentDate: '01.05.2026', contractSigned: 'Yes', signatureDate: '06.05.2026' }),
    // Signed 20 days after sent (days-to-sign = 20)
    row({ contractSent: 'Yes', contractSentDate: '01.05.2026', contractSigned: 'Yes', signatureDate: '21.05.2026' }),
    // Outstanding — no signature date yet
    row({ contractSent: 'Yes', contractSentDate: '01.01.2026', contractSigned: 'Pending' }),
  ];
  const t = computeContractTiming(rows);

  it('F34: avg days to sign', () => {
    expect(t.avgDaysToSign).toBeCloseTo(12.5, 5);
  });
  it('F35/F36: avg + max days outstanding are populated', () => {
    expect(t.avgDaysOutstanding).not.toBeNull();
    expect(t.maxDaysOutstanding).not.toBeNull();
  });
  it('F37/F38: outstanding thresholds hit', () => {
    // Contract sent 01.01.2026 vs today — well past 30 days.
    expect(t.outstandingOver30).toBeGreaterThanOrEqual(1);
    expect(t.outstandingOver14).toBeGreaterThanOrEqual(1);
  });
});

describe('computeFunnel — Excel Q28-Q34', () => {
  const rows = [
    row({ outreachStatus: 'Contacted', meetingScheduled: 'Yes', met: 'Yes',
          interestedAfterMtg: 'Yes', contractSent: 'Yes', contractSigned: 'Yes',
          depositPaid: 'Yes' }),
    row({ outreachStatus: 'Attempted – No Answer' }),
    row({ outreachStatus: 'Contacted', contractSent: 'To be sent' }),
  ];
  const f = computeFunnel(rows);

  it('funnel has 7 stages ending at Deposit Paid', () => {
    expect(f.map((s) => s.name)).toEqual([
      'Contacted', 'Mtg Scheduled', 'Showed Up', 'Interested',
      'Contract Sent', 'Signed', 'Deposit Paid',
    ]);
  });
  it('Q28: Contacted = "Contacted" + "Attempted – No Answer"', () => {
    expect(f[0]?.value).toBe(3);
  });
  it('Q32: Contract Sent = "Yes" + "To be sent"', () => {
    expect(f[4]?.value).toBe(2);
  });
  it('Q34: Deposit Paid count', () => {
    expect(f[6]?.value).toBe(1);
  });
});

describe('computeContractStatusBreakdown — Excel Q21-Q24', () => {
  const rows = [
    row({ contractSigned: 'Yes' }),                     // Signed
    row({ contractSigned: 'Pending' }),                 // Pending signature
    row({ contractSent: 'To be sent' }),                // To be sent
    row({ contractSent: 'No' }),                        // Declined
  ];
  const cs = computeContractStatusBreakdown(rows);
  it('has expected shape', () => {
    expect(cs.map((r) => r.name)).toEqual(['Signed', 'Pending Signature', 'To Be Sent', 'Declined']);
    expect(cs.map((r) => r.value)).toEqual([1, 1, 1, 1]);
  });
});

describe('computeMembershipStatus — Excel Q36-Q38', () => {
  const rows = [
    row({ contractSigned: 'Yes', depositPaid: 'Yes' }),   // Deposit paid
    row({ contractSigned: 'Yes' }),                       // Signed, no deposit
    row({ contractSigned: 'Pending' }),                   // Pending signature
  ];
  const m = computeMembershipStatus(rows);
  it('has expected values', () => {
    expect(m.map((r) => r.value)).toEqual([1, 1, 1]);
  });
});

describe('computeDropOffBreakdown — Excel Q41-Q45', () => {
  const rows = [
    row({ meetingScheduled: 'Not interested in meeting' }),
    row({ met: 'No' }),
    row({ interestedAfterMtg: 'No' }),                   // Declined after meeting
    row({ contractSent: 'Yes', contractSigned: 'No' }),  // Declined after contract
    row({ contractSent: 'Rejected' }),                   // Rejected
    row({ contractSent: 'No longer interested' }),       // Rejected
  ];
  const db = computeDropOffBreakdown(rows);
  it('has 5 categories', () => {
    expect(db).toHaveLength(5);
  });
  it('final Rejected/No longer interested sums both variants', () => {
    expect(db[4]?.value).toBe(2);
  });
});

describe('daysContractOutstanding / daysToSign', () => {
  it('daysToSign is signature − sent (R col formula)', () => {
    expect(daysToSign(row({ contractSentDate: '01.05.2026', signatureDate: '06.05.2026' }))).toBe(5);
  });
  it('daysContractOutstanding null when contract not sent Yes', () => {
    expect(daysContractOutstanding(row({ contractSent: 'To be sent', contractSentDate: '01.01.2026' }))).toBeNull();
  });
  it('daysContractOutstanding null when already signed', () => {
    expect(daysContractOutstanding(row({
      contractSent: 'Yes', contractSentDate: '01.01.2026',
      contractSigned: 'Yes', signatureDate: '10.01.2026',
    }))).toBeNull();
  });
});
