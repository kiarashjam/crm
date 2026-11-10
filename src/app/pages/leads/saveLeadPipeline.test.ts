// Saving a pipeline sends the pipeline, and the status shown afterwards is the
// one the server settled on.
//
// The browser used to work the status out and send it with the pipeline. The
// server now does that for every writer (an e-signed contract, a merge, an
// integration), so the browser sending its own answer could only be wrong when
// it disagreed: a stale tab, or a status list that changed since it loaded.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Lead } from '@/app/api/types';
import type { LeadPipeline } from './leadPipeline';

const updateLead = vi.fn();
const createActivity = vi.fn();
const demo = vi.hoisted(() => ({ on: true }));
vi.mock('@/app/api/leads', () => ({ updateLead: (...a: unknown[]) => updateLead(...a) }));
vi.mock('@/app/api/activities', () => ({ createActivity: (...a: unknown[]) => createActivity(...a) }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => demo.on,
}));

const { saveLeadPipeline, planFromServer } = await import('./saveLeadPipeline');
const { planStatusSync } = await import('./leadStatusSync');

const AGREED = [
  'New', 'Attempted Contact', 'Contacted', 'Connected',
  'Contract Pending', 'Awaiting Signature', 'Signed', 'Lost / Not Interested',
].map((name, i) => ({ id: `00000000-0000-4000-8000-00000000000${i}`, name }));

const lead: Pick<Lead, 'id' | 'status' | 'isConverted'> = { id: 'l1', status: 'New', isConverted: false };

const serverLead = (over: Partial<Lead>): Lead => ({
  id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'New', ...over,
});

const save = (over: Partial<Parameters<typeof saveLeadPipeline>[0]> = {}) => saveLeadPipeline({
  lead,
  pipeline: { outreachStatus: 'contacted' },
  base: {},
  statusOptions: AGREED,
  statusesLoaded: true,
  ...over,
});

beforeEach(() => {
  updateLead.mockReset();
  createActivity.mockReset();
  createActivity.mockResolvedValue(null);
  demo.on = true;
});

describe('saveLeadPipeline', () => {
  it('sends the pipeline and nothing else, so the server decides the status', async () => {
    updateLead.mockResolvedValue(serverLead({ status: 'Contacted' }));

    await save();

    expect(updateLead).toHaveBeenCalledTimes(1);
    const [, patch] = updateLead.mock.calls[0]!;
    expect(patch).toEqual({ pipelineState: JSON.stringify({ outreachStatus: 'contacted' }) });
  });

  it('sends only the steps the edit changed, and a step taken off as null', async () => {
    // A step recorded elsewhere since this page loaded (the contract steps an
    // e-signature writes) is not in the change, so the server keeps it.
    updateLead.mockResolvedValue(serverLead({ status: 'Connected' }));

    await save({
      base: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingDate: '2026-11-20' },
      pipeline: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingAttended: true },
    });

    const [, patch] = updateLead.mock.calls[0]!;
    expect(JSON.parse(patch.pipelineState)).toEqual({ meetingDate: null, meetingAttended: true });
  });

  // A server from before the merge replaces the pipeline with whatever it is sent, so
  // the changed steps alone would wipe every other one. A lead read from such a server
  // (one still serving during a deploy, or after a rollback) carries no version.
  it('sends the whole pipeline to a server that replaces it', async () => {
    demo.on = false;
    updateLead.mockResolvedValue(serverLead({ status: 'Connected' }));
    const pipeline: LeadPipeline = { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingAttended: true };

    await save({ base: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' }, pipeline });

    const [, patch] = updateLead.mock.calls[0]!;
    expect(JSON.parse(patch.pipelineState)).toEqual(pipeline);
  });

  it('sends only the changed steps to a server that merges them', async () => {
    demo.on = false;
    updateLead.mockResolvedValue(serverLead({ status: 'Connected' }));

    await save({
      lead: { ...lead, version: 'v1' },
      base: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled' },
      pipeline: { outreachStatus: 'contacted', contactOutcome: 'meeting_scheduled', meetingAttended: true },
    });

    const [, patch] = updateLead.mock.calls[0]!;
    expect(JSON.parse(patch.pipelineState)).toEqual({ meetingAttended: true });
  });

  it('reports the move the server made, with the id the server stored', async () => {
    updateLead.mockResolvedValue(serverLead({ status: 'Contacted', leadStatusId: 'server-id' }));

    const result = await save();

    expect(result.ok).toBe(true);
    expect(result.lead?.status).toBe('Contacted');
    expect(result.plan).toMatchObject({ kind: 'apply', to: { name: 'Contacted', id: 'server-id' } });
  });

  it('announces a move even when this tab had no status list to preview with', async () => {
    // The preview holds back without the list; the server does not need it from us.
    updateLead.mockResolvedValue(serverLead({ status: 'Contacted' }));

    const result = await save({ statusOptions: [], statusesLoaded: false, statusesUnavailable: true });

    expect(result.plan).toMatchObject({ kind: 'apply', to: { name: 'Contacted' } });
  });

  it('leaves the explanation of the move to the server, and still logs the edit', async () => {
    updateLead.mockResolvedValue(serverLead({ status: 'Contacted' }));

    await save({ log: { subject: 'Outreach recorded' } });

    expect(createActivity).toHaveBeenCalledTimes(1);
    expect(createActivity.mock.calls[0]![0]).toMatchObject({ type: 'system', subject: 'Outreach recorded', leadId: 'l1' });
  });

  it('says it failed when the server refused, so the caller can roll back', async () => {
    updateLead.mockRejectedValue(new Error('409'));

    const result = await save();

    expect(result.ok).toBe(false);
    expect(createActivity).not.toHaveBeenCalled();
  });
});

describe('planFromServer — the server is the truth', () => {
  const preview = (pipeline: Parameters<typeof planStatusSync>[0]['pipeline'], currentStatus = 'New') =>
    planStatusSync({ pipeline, currentStatus, statusOptions: AGREED, statusesLoaded: true });

  it('keeps the preview when the server agreed', () => {
    const p = preview({ meetingAttended: true });
    expect(planFromServer(p, 'New', { status: 'Connected', leadStatusId: 'x' }))
      .toMatchObject({ kind: 'apply', to: { name: 'Connected', id: 'x' }, derived: { rule: 'phase2_meeting_held' } });
  });

  it('reports where the server put it when that was somewhere else', () => {
    // A tab holding an older status list expected "Contacted"; the server's list
    // has a status for a booked meeting.
    const p = preview({ contactOutcome: 'meeting_scheduled' });
    const plan = planFromServer(p, 'New', { status: 'Meeting Scheduled' });
    expect(plan).toMatchObject({
      kind: 'apply', from: 'New',
      to: { name: 'Meeting Scheduled', canonical: 'meeting_scheduled' },
      derived: { because: 'Meeting scheduled' },
    });
  });

  it('announces nothing when the server kept the status', () => {
    const p = preview({ contractSigned: 'yes' });
    expect(p.kind).toBe('apply');
    expect(planFromServer(p, 'New', { status: 'New' })).toMatchObject({ kind: 'skip', reason: 'server_kept' });
  });

  it('leaves a hold as it was when the server held too', () => {
    const p = planStatusSync({
      pipeline: { contractSigned: 'yes' }, currentStatus: 'Do Not Contact',
      statusOptions: [...AGREED, { id: 'dnc', name: 'Do Not Contact' }], statusesLoaded: true,
    });
    expect(planFromServer(p, 'Do Not Contact', { status: 'Do Not Contact' })).toBe(p);
  });
});
