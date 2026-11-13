// Keeping members: each member's at-risk score and the signs it is made of, complaints and
// concerns staff record, the club's retention and referral rules, the renewals board, who
// referred, proposed and seconded whom, the referral bonus, and the referrals report.
//
// All of it is worked out on the server: the score from the member's own records, every
// night and on demand; the board's stages from renewal dates, payments, contacts and the
// score; the bonus through the ledger. Demo data has none of it, so reads answer empty and
// writes say so rather than pretend.

import { authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import { downloadFile } from '@/app/lib/exportCsv';

export const RETENTION_DEMO_MESSAGE = 'Scores, renewals and referrals are worked out by the live app; demo data has none.';

function live(): void {
  if (servesDemoData()) throw new Error(RETENTION_DEMO_MESSAGE);
}

export interface CurrencyAmount { currency: string; amount: number }

// ── the score ────────────────────────────────────────────────────────────────

export type RiskLevel = 'low' | 'watch' | 'at_risk';

export const RISK_LEVEL_LABELS: Record<RiskLevel, string> = { low: 'Low', watch: 'Watch', at_risk: 'At risk' };

/** One sign the score was made of: how strongly it shows (0 to 1), what it adds, and why. */
export interface RiskFactor {
  key: string;
  label: string;
  weight: number;
  strength: number;
  points: number;
  detail: string;
}

export interface MemberRisk {
  memberId: string;
  score: number;
  level: RiskLevel;
  factors: RiskFactor[];
  computedAtUtc: string;
  previousScore?: number;
  atRiskSinceUtc?: string;
  saveActionsAtUtc?: string;
  saveTaskId?: string;
  atRiskThreshold: number;
  watchThreshold: number;
  /** What a recompute set off, in sentences; empty for a read. */
  notes: string[];
}

export interface AtRiskMember {
  memberId: string;
  name: string;
  tier: string;
  status: string;
  score: number;
  level: RiskLevel;
  computedAtUtc: string;
  atRiskSinceUtc?: string | null;
  ownerUserId: string;
  ownerName?: string | null;
  topFactors: RiskFactor[];
}

export interface ScoringRun {
  id: string;
  trigger: 'schedule' | 'manual' | string;
  status: 'running' | 'succeeded' | 'failed' | string;
  startedAtUtc: string;
  finishedAtUtc?: string | null;
  membersScored: number;
  becameAtRisk: number;
  noLongerAtRisk: number;
  tasksCreated: number;
  emailsQueued: number;
  renewalsTracked: number;
  failure?: string | null;
}

/** The member's score as last worked out, or null before their first. */
export async function getMemberRisk(memberId: string, req?: RequestOptions): Promise<MemberRisk | null> {
  if (servesDemoData()) return null;
  const answer = await authFetchJson<{ risk: MemberRisk | null }>(`/api/members/${memberId}/risk`, { signal: req?.signal });
  return answer?.risk ?? null;
}

/** Works the member's score out again now; a crossing into at risk sets the club's save actions off. */
export function recomputeMemberRisk(memberId: string): Promise<MemberRisk> {
  live();
  return authFetchJson<MemberRisk>(`/api/members/${memberId}/risk/recompute`, { method: 'POST', body: '{}' });
}

/** The members at risk (or to watch), highest score first. */
export async function getAtRiskMembers(level: 'at_risk' | 'watch' = 'at_risk', req?: RequestOptions): Promise<AtRiskMember[]> {
  if (servesDemoData()) return [];
  return authFetchJson<AtRiskMember[]>(`/api/retention/members?level=${level}`, { signal: req?.signal });
}

export async function getScoringRuns(req?: RequestOptions): Promise<ScoringRun[]> {
  if (servesDemoData()) return [];
  return authFetchJson<ScoringRun[]>('/api/retention/runs', { signal: req?.signal });
}

/** Scores every active member now, as the night's run does. */
export function runScoringNow(): Promise<ScoringRun> {
  live();
  return authFetchJson<ScoringRun>('/api/retention/run', { method: 'POST' });
}

// ── concerns ─────────────────────────────────────────────────────────────────

export type ConcernKind = 'complaint' | 'concern';

export interface Concern {
  id: string;
  memberId: string;
  kind: ConcernKind;
  note: string;
  recordedAtUtc: string;
  recordedByName?: string | null;
  resolvedAtUtc?: string | null;
  resolvedByName?: string | null;
  resolution?: string | null;
}

export async function getConcerns(memberId: string, req?: RequestOptions): Promise<Concern[]> {
  if (servesDemoData()) return [];
  return authFetchJson<Concern[]>(`/api/members/${memberId}/concerns`, { signal: req?.signal });
}

export function addConcern(memberId: string, kind: ConcernKind, note: string): Promise<Concern> {
  live();
  return authFetchJson<Concern>(`/api/members/${memberId}/concerns`, { method: 'POST', body: JSON.stringify({ kind, note }) });
}

export function resolveConcern(memberId: string, concernId: string, resolution: string): Promise<Concern> {
  live();
  return authFetchJson<Concern>(`/api/members/${memberId}/concerns/${concernId}/resolve`, {
    method: 'POST', body: JSON.stringify({ resolution: resolution.trim() || null }),
  });
}

export async function deleteConcern(memberId: string, concernId: string): Promise<void> {
  live();
  const res = await authFetch(`/api/members/${memberId}/concerns/${concernId}`, { method: 'DELETE' });
  await ensureOk(res);
}

// ── the rules ────────────────────────────────────────────────────────────────

export type ReferralBonusKind = 'none' | 'points' | 'credit';
export type SponsorRule = 'off' | 'optional' | 'required';
export type EmailPurpose = 'marketing' | 'service';

export interface RetentionSettings {
  visitWeight: number;
  spendWeight: number;
  balanceWeight: number;
  eventWeight: number;
  concernWeight: number;
  tenureWeight: number;
  atRiskThreshold: number;
  watchThreshold: number;
  saveTask: boolean;
  saveTaskDueDays: number;
  saveEmail: boolean;
  saveEmailPurpose: EmailPurpose;
  saveEmailSubject?: string | null;
  saveEmailBody?: string | null;
  saveCooldownDays: number;
  referralBonus: ReferralBonusKind;
  referralBonusPoints: number;
  referralBonusAmount: number;
  currency: string;
  sponsorRule: SponsorRule;
  sponsorMinTenureMonths: number;
  sponsorNoOverdue: boolean;
  /** Null until the club first saves its rules: the defaults apply. */
  version?: string | null;
  updatedAtUtc?: string | null;
  updatedByName?: string | null;
}

/** What a save changes: a field left out keeps its value; `version` refuses a save from an older copy (409). */
export type RetentionSettingsChange = Partial<Omit<RetentionSettings, 'currency' | 'updatedAtUtc' | 'updatedByName'>>;

/** The defaults the server applies while the club has saved nothing: every action off. */
export const DEFAULT_RETENTION_SETTINGS: RetentionSettings = {
  visitWeight: 30, spendWeight: 15, balanceWeight: 25, eventWeight: 10, concernWeight: 15, tenureWeight: 5,
  atRiskThreshold: 60, watchThreshold: 35,
  saveTask: false, saveTaskDueDays: 3, saveEmail: false, saveEmailPurpose: 'marketing', saveEmailSubject: null, saveEmailBody: null,
  saveCooldownDays: 60,
  referralBonus: 'none', referralBonusPoints: 500, referralBonusAmount: 50, currency: 'CHF',
  sponsorRule: 'off', sponsorMinTenureMonths: 12, sponsorNoOverdue: true,
  version: null,
};

export async function getRetentionSettings(req?: RequestOptions): Promise<RetentionSettings> {
  if (servesDemoData()) return DEFAULT_RETENTION_SETTINGS;
  return authFetchJson<RetentionSettings>('/api/retention/settings', { signal: req?.signal });
}

export function saveRetentionSettings(change: RetentionSettingsChange): Promise<RetentionSettings> {
  live();
  return authFetchJson<RetentionSettings>('/api/retention/settings', { method: 'PUT', body: JSON.stringify(change) });
}

// ── the renewals board ───────────────────────────────────────────────────────

export type RenewalStage = 'upcoming' | 'contacted' | 'at_risk' | 'renewed' | 'lapsed';

/** What staff can set; "auto" gives the renewal back to what the records say. */
export type RenewalStageChoice = Exclude<RenewalStage, 'upcoming'> | 'auto';

export interface RenewalCard {
  memberId: string;
  name: string;
  tier: string;
  memberStatus: string;
  source: 'membership' | 'subscription';
  subscriptionId?: string;
  subscriptionName?: string;
  endsAtUtc: string;
  daysLeft: number;
  stage: RenewalStage;
  stageReason: string;
  stageSetByHand: boolean;
  ownerUserId: string;
  ownerName?: string;
  annualValue: number;
  periodAmount: number;
  cadence: string;
  currency: string;
  lastContactAtUtc?: string;
  /** message (written to them), activity (a call, meeting or email logged) or marked (contacted on the board). */
  lastContactKind?: 'message' | 'activity' | 'marked';
  riskScore?: number;
  riskLevel?: RiskLevel;
  note?: string;
  renewedUntilUtc?: string;
  /** The renewal record's version, once staff have set anything on it. */
  version?: string;
}

export interface RenewalColumn {
  stage: RenewalStage;
  label: string;
  count: number;
  totals: CurrencyAmount[];
  cards: RenewalCard[];
}

export interface RenewalBoard {
  fromUtc: string;
  toUtc: string;
  days: number;
  pastDays: number;
  currency: string;
  atRiskThreshold: number;
  columns: RenewalColumn[];
}

export interface RenewalBoardQuery {
  /** How far ahead (1 to 365 days, 60 when left out). */
  days?: number;
  /** How far back, for renewals that just passed (0 to 365 days, 30 when left out). */
  pastDays?: number;
  /** "me", or a team member's id: only their members. */
  owner?: string;
  /** Monthly subscription periods too. */
  monthly?: boolean;
}

const EMPTY_STAGES: RenewalStage[] = ['upcoming', 'contacted', 'at_risk', 'renewed', 'lapsed'];
const STAGE_LABELS: Record<RenewalStage, string> = {
  upcoming: 'Upcoming', contacted: 'Contacted', at_risk: 'At risk', renewed: 'Renewed', lapsed: 'Lapsed',
};

export function boardQueryString(query: RenewalBoardQuery): string {
  const q = new URLSearchParams();
  if (query.days !== undefined) q.set('days', String(query.days));
  if (query.pastDays !== undefined) q.set('pastDays', String(query.pastDays));
  if (query.owner) q.set('owner', query.owner);
  if (query.monthly) q.set('monthly', 'true');
  const text = q.toString();
  return text ? `?${text}` : '';
}

export async function getRenewalBoard(query: RenewalBoardQuery = {}, req?: RequestOptions): Promise<RenewalBoard> {
  if (servesDemoData()) {
    const now = new Date();
    return {
      fromUtc: now.toISOString(), toUtc: now.toISOString(), days: query.days ?? 60, pastDays: query.pastDays ?? 30, currency: 'CHF',
      atRiskThreshold: 60, columns: EMPTY_STAGES.map((stage) => ({ stage, label: STAGE_LABELS[stage], count: 0, totals: [], cards: [] })),
    };
  }
  return authFetchJson<RenewalBoard>(`/api/renewals${boardQueryString(query)}`, { signal: req?.signal });
}

export interface RenewalStageChange {
  stage: RenewalStageChoice;
  note?: string;
  /** For a membership marked renewed: the new end (a year after the old one when left out). */
  renewUntilUtc?: string;
}

/** Sets where a renewal stands. Sent with the card's version, a change from an older copy is refused (409). */
export function setRenewalStage(card: Pick<RenewalCard, 'memberId' | 'source' | 'subscriptionId' | 'endsAtUtc' | 'version'>,
  change: RenewalStageChange): Promise<RenewalCard> {
  live();
  return authFetchJson<RenewalCard>('/api/renewals/stage', {
    method: 'POST',
    body: JSON.stringify({
      memberId: card.memberId, source: card.source, subscriptionId: card.subscriptionId ?? null, endsAtUtc: card.endsAtUtc,
      stage: change.stage, note: change.note, renewUntilUtc: change.renewUntilUtc, version: card.version ?? null,
    }),
  });
}

// ── referrals and sponsors ───────────────────────────────────────────────────

export interface ReferralPerson { memberId: string; name: string; status: string; tier: string; joinedAtUtc: string }

export interface SponsoredApplication { applicationId: string; name: string; status: string; submittedAtUtc: string; memberId?: string | null }

export type ReferralBonusStatus = 'awarded' | 'pending' | 'skipped';

export interface ReferralBonus {
  id: string;
  referrerMemberId: string;
  referrerName: string;
  referredMemberId: string;
  referredName: string;
  applicationId?: string | null;
  kind: ReferralBonusKind;
  status: ReferralBonusStatus;
  points: number;
  amount: number;
  currency: string;
  reason?: string | null;
  createdAtUtc: string;
  awardedAtUtc?: string | null;
}

export interface SponsorStanding { memberId: string; name: string; inGoodStanding: boolean; reasons: string[] }

export interface MemberReferrals {
  memberId: string;
  referredBy?: ReferralPerson;
  applicationId?: string;
  proposedBy?: ReferralPerson;
  secondedBy?: ReferralPerson;
  referred: ReferralPerson[];
  proposed: SponsoredApplication[];
  seconded: SponsoredApplication[];
  bonusesEarned: ReferralBonus[];
  bonusForThem?: ReferralBonus;
  bonusAvailable: boolean;
  standing?: SponsorStanding | null;
}

export async function getMemberReferrals(memberId: string, req?: RequestOptions): Promise<MemberReferrals | null> {
  if (servesDemoData()) return null;
  return authFetchJson<MemberReferrals>(`/api/members/${memberId}/referrals`, { signal: req?.signal });
}

/** Names the member who referred them, or nobody (null). */
export function setReferrer(memberId: string, referredByMemberId: string | null): Promise<MemberReferrals> {
  live();
  return authFetchJson<MemberReferrals>(`/api/members/${memberId}/referrer`, {
    method: 'PUT', body: JSON.stringify({ referredByMemberId }),
  });
}

/** Gives their referrer the club's bonus for them, once. A credit takes money.manage. */
export function awardReferralBonus(memberId: string): Promise<ReferralBonus> {
  live();
  return authFetchJson<ReferralBonus>(`/api/members/${memberId}/referral-bonus`, { method: 'POST', body: '{}' });
}

/** Whether each member may propose or second by the club's rule, and if not why. */
export async function getSponsorStanding(memberIds: string[], req?: RequestOptions): Promise<SponsorStanding[]> {
  if (servesDemoData() || memberIds.length === 0) return [];
  const q = new URLSearchParams();
  for (const id of memberIds) q.append('memberIds', id);
  return authFetchJson<SponsorStanding[]>(`/api/referrals/standing?${q.toString()}`, { signal: req?.signal });
}

// ── the referrals report ─────────────────────────────────────────────────────

export interface ReferredMemberRow {
  memberId: string;
  name: string;
  status: string;
  tier: string;
  joinedAtUtc: string;
  dues: CurrencyAmount[];
  spend: CurrencyAmount[];
}

export interface ReferrerRow {
  memberId: string;
  name: string;
  status: string;
  tier: string;
  referredAllTime: number;
  referredInPeriod: number;
  proposed: number;
  seconded: number;
  dues: CurrencyAmount[];
  spend: CurrencyAmount[];
  revenue: CurrencyAmount[];
  bonusPoints: number;
  bonusCredit: CurrencyAmount[];
  members: ReferredMemberRow[];
}

export interface ReferralReport {
  fromUtc: string;
  toUtc: string;
  currency: string;
  referrers: ReferrerRow[];
  totals: {
    referrers: number;
    referredAllTime: number;
    referredInPeriod: number;
    membersJoinedInPeriod: number;
    dues: CurrencyAmount[];
    spend: CurrencyAmount[];
    revenue: CurrencyAmount[];
    bonusPoints: number;
    bonusCredit: CurrencyAmount[];
  };
}

export interface ReferralReportRange { fromUtc: string; toUtc: string }

function rangeQuery(range: ReferralReportRange): string {
  return new URLSearchParams({ from: range.fromUtc, to: range.toUtc }).toString();
}

export async function getReferralReport(range: ReferralReportRange, req?: RequestOptions): Promise<ReferralReport> {
  if (servesDemoData()) {
    return {
      fromUtc: range.fromUtc, toUtc: range.toUtc, currency: 'CHF', referrers: [],
      totals: { referrers: 0, referredAllTime: 0, referredInPeriod: 0, membersJoinedInPeriod: 0, dues: [], spend: [], revenue: [], bonusPoints: 0, bonusCredit: [] },
    };
  }
  return authFetchJson<ReferralReport>(`/api/referrals/report?${rangeQuery(range)}`, { signal: req?.signal });
}

/** The report as the server's spreadsheet: one row per member brought in. Takes records.export. */
export async function downloadReferralReportCsv(range: ReferralReportRange): Promise<void> {
  live();
  const res = await authFetch(`/api/referrals/report/export?${rangeQuery(range)}`);
  await ensureOk(res);
  const header = res.headers.get('Content-Disposition') ?? '';
  const name = /filename="?([^";]+)"?/i.exec(header)?.[1] ?? 'referrals.csv';
  downloadFile(await res.blob(), name);
}

/** The amount in the currency, or nothing: what a list of amounts per currency holds for it. */
export function amountIn(amounts: CurrencyAmount[], currency: string): number {
  return amounts.filter((a) => a.currency === currency).reduce((sum, a) => sum + a.amount, 0);
}
