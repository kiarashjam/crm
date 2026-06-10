// Outreach sequences (a.k.a. cadences).
//
// A sequence is an ordered list of steps (email / call / task / wait) that a
// lead or contact gets "enrolled" into. The backend owns the scheduler that
// actually fires each step; in demo mode we persist sequences + enrollments
// locally and track progress so the UI is fully exercisable offline.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type SequenceStepType = 'email' | 'call' | 'task' | 'wait';
export type SequenceStatus = 'active' | 'paused' | 'draft';
export type EnrollmentStatus = 'active' | 'completed' | 'paused';

export interface SequenceStep {
  id: string;
  order: number;
  type: SequenceStepType;
  /** Days to wait (after enrollment / previous step) before this step fires. */
  dayOffset: number;
  /** For email steps. */
  subject?: string;
  body?: string;
  /** For task/call steps. */
  taskTitle?: string;
  note?: string;
}

export interface Sequence {
  id: string;
  name: string;
  description?: string;
  status: SequenceStatus;
  steps: SequenceStep[];
  createdAtUtc: string;
  updatedAtUtc?: string;
}

export interface SequenceEnrollment {
  id: string;
  sequenceId: string;
  targetType: 'lead' | 'contact';
  targetId: string;
  targetName: string;
  targetEmail?: string;
  status: EnrollmentStatus;
  /** Index of the next step to run. */
  currentStep: number;
  enrolledAtUtc: string;
  nextActionAtUtc?: string;
}

const DAY_MS = 24 * 3_600_000;

const sequenceStore = createMockStore<Sequence>({
  storageKey: 'crm.mock.sequences.v1',
  seed: [
    {
      id: 'seq-seed-1',
      name: 'New lead outreach',
      description: 'A 5-touch intro cadence for inbound leads.',
      status: 'active',
      createdAtUtc: new Date(Date.now() - 14 * DAY_MS).toISOString(),
      steps: [
        { id: 'st-1', order: 0, type: 'email', dayOffset: 0, subject: 'Quick hello from {{company}}', body: "Hi {{firstName}},\n\nThanks for reaching out — I'd love to learn more about what you're working on." },
        { id: 'st-2', order: 1, type: 'task', dayOffset: 2, taskTitle: 'Call the lead to qualify' },
        { id: 'st-3', order: 2, type: 'email', dayOffset: 2, subject: 'Following up', body: 'Just floating this back to the top of your inbox.' },
        { id: 'st-4', order: 3, type: 'wait', dayOffset: 3 },
        { id: 'st-5', order: 4, type: 'email', dayOffset: 0, subject: 'Last note', body: "I'll close the loop here, but the door's always open." },
      ],
    },
    {
      id: 'seq-seed-2',
      name: 'Re-engagement',
      description: 'Win back cold leads with a light touch.',
      status: 'draft',
      createdAtUtc: new Date(Date.now() - 3 * DAY_MS).toISOString(),
      steps: [
        { id: 'st-r1', order: 0, type: 'email', dayOffset: 0, subject: 'Still interested?', body: "It's been a while — should I keep you in the loop?" },
        { id: 'st-r2', order: 1, type: 'task', dayOffset: 4, taskTitle: 'Decide whether to archive' },
      ],
    },
  ],
  idOf: (s) => s.id,
});

const enrollmentStore = createMockStore<SequenceEnrollment>({
  storageKey: 'crm.mock.sequenceEnrollments.v1',
  seed: [
    {
      id: 'enr-seed-1', sequenceId: 'seq-seed-1', targetType: 'lead', targetId: '1',
      targetName: 'Alex Turner', targetEmail: 'alex.turner@example.com', status: 'active',
      currentStep: 2, enrolledAtUtc: new Date(Date.now() - 4 * DAY_MS).toISOString(),
      nextActionAtUtc: new Date(Date.now() + 1 * DAY_MS).toISOString(),
    },
  ],
  idOf: (e) => e.id,
});

function nowIso() { return new Date().toISOString(); }

// ---- Sequences CRUD ----
// Each call tries the backend, then falls back to the local store so these
// features work even when the API doesn't implement the endpoint yet.

export async function getSequences(): Promise<Sequence[]> {
  return apiWithFallback(
    async () => { const res = await authFetchJson<Sequence[]>('/api/sequences'); return Array.isArray(res) ? res : []; },
    async () => { await delay(150); return [...sequenceStore.list()].sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc)); },
  );
}

export async function getSequence(id: string): Promise<Sequence | null> {
  return apiWithFallback(
    () => authFetchJson<Sequence>(`/api/sequences/${id}`),
    async () => { await delay(100); return sequenceStore.byId(id) ?? null; },
  );
}

export interface SequenceInput {
  name: string;
  description?: string;
  status?: SequenceStatus;
  steps: Omit<SequenceStep, 'id'>[];
}

export async function createSequence(input: SequenceInput): Promise<Sequence | null> {
  return apiWithFallback(
    () => authFetchJson<Sequence>('/api/sequences', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(200);
      return sequenceStore.add({
        id: mockId('seq'),
        name: input.name,
        description: input.description,
        status: input.status ?? 'draft',
        steps: input.steps.map((s, i) => ({ ...s, id: mockId('st'), order: i })),
        createdAtUtc: nowIso(),
      });
    },
  );
}

export async function updateSequence(id: string, input: Partial<SequenceInput>): Promise<Sequence | null> {
  return apiWithFallback(
    () => authFetchJson<Sequence>(`/api/sequences/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
    async () => {
      await delay(200);
      const patch: Partial<Sequence> = { ...input, updatedAtUtc: nowIso() } as Partial<Sequence>;
      if (input.steps) patch.steps = input.steps.map((s, i) => ({ ...s, id: mockId('st'), order: i }));
      return sequenceStore.update(id, patch);
    },
  );
}

export async function deleteSequence(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/sequences/${id}`, { method: 'DELETE' }); if (!(res.status === 204 || res.ok)) throw new Error('failed'); return true; },
    async () => { await delay(150); return sequenceStore.remove(id); },
  );
}

// ---- Enrollments ----

export async function getEnrollments(sequenceId?: string): Promise<SequenceEnrollment[]> {
  return apiWithFallback(
    async () => {
      const q = sequenceId ? `?sequenceId=${sequenceId}` : '';
      const res = await authFetchJson<SequenceEnrollment[]>(`/api/sequences/enrollments${q}`);
      return Array.isArray(res) ? res : [];
    },
    async () => { await delay(120); return enrollmentStore.list().filter((e) => !sequenceId || e.sequenceId === sequenceId); },
  );
}

export interface EnrollInput {
  sequenceId: string;
  targetType: 'lead' | 'contact';
  targetId: string;
  targetName: string;
  targetEmail?: string;
}

export async function enrollInSequence(input: EnrollInput): Promise<SequenceEnrollment | null> {
  return apiWithFallback(
    () => authFetchJson<SequenceEnrollment>('/api/sequences/enroll', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(180);
      const seq = sequenceStore.byId(input.sequenceId);
      const firstOffset = seq?.steps?.[0]?.dayOffset ?? 0;
      return enrollmentStore.add({
        id: mockId('enr'),
        sequenceId: input.sequenceId,
        targetType: input.targetType,
        targetId: input.targetId,
        targetName: input.targetName,
        targetEmail: input.targetEmail,
        status: 'active',
        currentStep: 0,
        enrolledAtUtc: nowIso(),
        nextActionAtUtc: new Date(Date.now() + firstOffset * DAY_MS).toISOString(),
      });
    },
  );
}

export async function setEnrollmentStatus(id: string, status: EnrollmentStatus): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/sequences/enrollments/${id}`, { method: 'PATCH', body: JSON.stringify({ status }) }); if (!res.ok) throw new Error('failed'); return true; },
    async () => { await delay(100); return enrollmentStore.update(id, { status }) != null; },
  );
}

export async function unenroll(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/sequences/enrollments/${id}`, { method: 'DELETE' }); if (!(res.status === 204 || res.ok)) throw new Error('failed'); return true; },
    async () => { await delay(100); return enrollmentStore.remove(id); },
  );
}
