import { describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { askAgent } from './brain/answer';
import { judgeAll } from './brain/judge';
import { resetAgentDataForTests } from './agentData';
import { collectWork } from './workspace';
import { NextActions } from './NextActions';

const DELAY_MS = 40;

function delayed<T>(value: T): Promise<T> {
  return new Promise((resolve) => {
    setTimeout(() => resolve(value), DELAY_MS);
  });
}

vi.mock('@/app/api', () => ({
  getLeads: vi.fn(),
  getDeals: vi.fn(),
  getTasks: vi.fn(),
  getContacts: vi.fn(),
  getCompanies: vi.fn(),
}));
vi.mock('@/app/api/sequences', () => ({ getEnrollments: vi.fn(), getSequences: vi.fn() }));
vi.mock('@/app/api/automations', () => ({ getAutomations: vi.fn() }));
vi.mock('@/app/api/contracts', () => ({ listContractsForLead: vi.fn() }));
vi.mock('@/app/api/organizations', () => ({
  listPendingInvitesForOrg: vi.fn(),
  listPendingJoinRequestsForOrg: vi.fn(),
}));
vi.mock('@/app/api/members', () => ({ getMembers: vi.fn(async () => []) }));
vi.mock('@/app/api/applications', () => ({ getApplications: vi.fn(async () => []) }));
vi.mock('@/app/api/charges', () => ({ getCharges: vi.fn(async () => []) }));
vi.mock('@/app/api/reservations', () => ({ getReservations: vi.fn(async () => []) }));
vi.mock('@/app/api/subscriptions', () => ({ getSubscriptions: vi.fn(async () => []) }));
vi.mock('@/app/api/campaigns', () => ({ getCampaigns: vi.fn(async () => []) }));
vi.mock('@/app/api/events', () => ({ getEvents: vi.fn(async () => []) }));
vi.mock('@/app/api/notifications', () => ({ getNotifications: vi.fn(async () => []) }));
vi.mock('@/app/api/giftCards', () => ({ getGiftCards: vi.fn(async () => []) }));
vi.mock('@/app/api/tabs', () => ({ getTabs: vi.fn(async () => []) }));
vi.mock('@/app/api/communications', () => ({ getCommunications: vi.fn(async () => []) }));
vi.mock('@/app/api/donations', () => ({ getDonations: vi.fn(async () => []) }));
vi.mock('@/app/api/pipelines', () => ({ getPipelines: vi.fn(async () => []) }));
vi.mock('@/app/api/reporting', () => ({ getPipelineValueByStage: vi.fn(async () => []) }));
vi.mock('@/app/api/copyHistory', () => ({ getCopyHistory: vi.fn(async () => []) }));
vi.mock('@/app/api/customFields', () => ({ getFieldDefinitions: vi.fn(async () => []) }));
vi.mock('@/app/api/webhook', () => ({ getWebhookInfo: vi.fn(async () => null) }));
vi.mock('@/app/api/contacts', () => ({ getContactLookalikes: vi.fn(async () => []), createContact: vi.fn(async () => ({ contact: null, error: null })) }));
vi.mock('@/app/api/products', () => ({ getDealLineItems: vi.fn(async () => []) }));
vi.mock('@/app/api/attachments', () => ({ getAttachments: vi.fn(async () => []) }));
vi.mock('@/app/api/households', () => ({ getHouseholds: vi.fn(async () => []), getHouseholdMembers: vi.fn(async () => []) }));
vi.mock('@/app/api/loyalty', () => ({ getLoyaltyLedger: vi.fn(async () => []) }));
vi.mock('@/app/api/activities', () => ({ getActivities: vi.fn(async () => []), createActivity: vi.fn(async () => null) }));
vi.mock('@/app/api/visits', () => ({ getVisits: vi.fn(async () => []) }));
vi.mock('@/app/api/audit', () => ({ getAuditLog: vi.fn(async () => []) }));
vi.mock('@/app/api/deals', () => ({ createDeal: vi.fn(async () => null), updateDeal: vi.fn(async () => null) }));
vi.mock('@/app/api/settings', () => ({
  getUserSettings: vi.fn(async () => ({ defaultFollowUpDays: 3 })),
  saveUserSettings: vi.fn(async (value: unknown) => value),
}));
vi.mock('@/app/api/email', () => ({ listRecentEmails: vi.fn(async () => []), sendEmail: vi.fn(async () => null) }));

import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { listContractsForLead } from '@/app/api/contracts';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { getMembers } from '@/app/api/members';

const leads = vi.mocked(getLeads);
const deals = vi.mocked(getDeals);
const tasks = vi.mocked(getTasks);
const contacts = vi.mocked(getContacts);
const companies = vi.mocked(getCompanies);
const automations = vi.mocked(getAutomations);
const enrollments = vi.mocked(getEnrollments);
const sequences = vi.mocked(getSequences);
const members = vi.mocked(getMembers);
const contracts = vi.mocked(listContractsForLead);

const ada = {
  id: 'l1',
  name: 'Ada',
  email: 'a@example.com',
  status: 'New',
  pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }),
};

function armLists() {
  leads.mockImplementation(() => delayed([ada]));
  deals.mockImplementation(() => delayed([]));
  tasks.mockImplementation(() => delayed([]));
  contacts.mockImplementation(() => delayed([]));
  companies.mockImplementation(() => delayed([]));
  automations.mockImplementation(() => delayed([]));
  enrollments.mockImplementation(() => delayed([]));
  sequences.mockImplementation(() => delayed([]));
  members.mockImplementation(() => delayed([]));
  contracts.mockImplementation(() => delayed([]));
}

function callCount(): number {
  return leads.mock.calls.length + deals.mock.calls.length + tasks.mock.calls.length
    + contacts.mock.calls.length + companies.mock.calls.length + automations.mock.calls.length
    + enrollments.mock.calls.length + sequences.mock.calls.length + members.mock.calls.length
    + contracts.mock.calls.length;
}

function resetCounts() {
  leads.mockClear();
  deals.mockClear();
  tasks.mockClear();
  contacts.mockClear();
  companies.mockClear();
  automations.mockClear();
  enrollments.mockClear();
  sequences.mockClear();
  members.mockClear();
  contracts.mockClear();
}

describe('agent timing', () => {
  it('measures cold loads and in-memory answers', async () => {
    armLists();
    async function sample(): Promise<{ first: number; full: number; calls: number }> {
      resetCounts();
      const start = performance.now();
      render(<MemoryRouter><NextActions /></MemoryRouter>);
      await screen.findByRole('link', { name: 'Ada' });
      const first = performance.now() - start;
      const full = first;
      const row = { first: Math.round(first), full: Math.round(full), calls: callCount() };
      cleanup();
      return row;
    }
    const cold: { first: number; full: number; calls: number }[] = [];
    for (let i = 0; i < 5; i += 1) {
      resetAgentDataForTests();
      cold.push(await sample());
    }
    const warm: { first: number; full: number; calls: number }[] = [];
    for (let i = 0; i < 5; i += 1) {
      warm.push(await sample());
    }

    const manyLeads = Array.from({ length: 400 }, (_, index) => ({
      id: `l${index}`,
      name: `Lead ${index}`,
      email: `l${index}@example.com`,
      lastContactedAt: '2026-01-01',
    }));
    const manyDeals = Array.from({ length: 200 }, (_, index) => ({
      id: `d${index}`,
      name: `Deal ${index}`,
      value: String(1000 + index),
      stage: index % 2 === 0 ? 'Proposal' : 'Qualification',
    }));
    const items = collectWork({ leads: manyLeads, deals: manyDeals }, new Date('2026-10-22T12:00:00Z'), Number.MAX_SAFE_INTEGER);
    const answerStart = performance.now();
    for (let i = 0; i < 30; i += 1) {
      askAgent('where is the money?', { items, leads: manyLeads, deals: manyDeals, plan: null, page: { mode: 'focus' }, patterns: { lines: [], wonDeals: 0, lostDeals: 0, lostLeads: 0 }, invites: [], contractHint: null });
      judgeAll(items, manyLeads, manyDeals, new Date('2026-10-22T12:00:00Z'));
    }
    const answerMs = Math.round(performance.now() - answerStart);

    console.log(JSON.stringify({ delayMs: DELAY_MS, samples: 5, cold, warm, answerLoops: 30, answerAndJudgeMs: answerMs, records: { leads: 400, deals: 200, suggestions: items.length } }));
    expect(cold).toHaveLength(5);
    expect(cold.every((row) => row.calls > 0)).toBe(true);
    expect(warm.every((row) => row.calls === 0)).toBe(true);
  }, 30000);
});
