import { authFetchJson, isUsingRealApi } from './apiClient';

export interface EmailSequenceStep {
  id: string;
  order: number;
  delayDays: number;
  copyTypeId: string;
  subject: string;
  body: string;
  createdAt: string;
}

export interface EmailSequence {
  id: string;
  name: string;
  description: string;
  steps: EmailSequenceStep[];
  isActive: boolean;
  totalEnrollments: number;
  createdAt: string;
  updatedAt: string;
}

export interface EmailSequenceEnrollment {
  id: string;
  sequenceId: string;
  sequenceName: string;
  contactId?: string;
  leadId?: string;
  status: 'active' | 'paused' | 'completed' | 'unsubscribed';
  currentStep: number;
  nextSendAt?: string;
  enrolledAt: string;
}

export interface CreateEmailSequenceRequest {
  name: string;
  description?: string;
  steps: Omit<EmailSequenceStep, 'id' | 'createdAt'>[];
}

export interface UpdateEmailSequenceRequest {
  name?: string;
  description?: string;
  isActive?: boolean;
}

export interface CreateSequenceStepRequest {
  order: number;
  delayDays: number;
  copyTypeId: string;
  subject: string;
  body: string;
}

export interface EnrollInSequenceRequest {
  sequenceId: string;
  contactId?: string;
  leadId?: string;
}

// Mock data for development
const mockSequences: EmailSequence[] = [
  {
    id: '1',
    name: 'Welcome Series',
    description: 'Introduction sequence for new leads',
    steps: [
      { id: '1-1', order: 1, delayDays: 0, copyTypeId: 'cold-email', subject: 'Welcome aboard!', body: 'Thank you for your interest...', createdAt: new Date().toISOString() },
      { id: '1-2', order: 2, delayDays: 2, copyTypeId: 'follow-up', subject: 'How can we help?', body: 'I wanted to follow up...', createdAt: new Date().toISOString() },
      { id: '1-3', order: 3, delayDays: 5, copyTypeId: 'follow-up', subject: 'Quick check-in', body: 'Just wanted to check...', createdAt: new Date().toISOString() },
    ],
    isActive: true,
    totalEnrollments: 45,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: '2',
    name: 'Re-engagement Campaign',
    description: 'Win back cold leads',
    steps: [
      { id: '2-1', order: 1, delayDays: 0, copyTypeId: 'cold-email', subject: 'We miss you!', body: 'It\'s been a while...', createdAt: new Date().toISOString() },
      { id: '2-2', order: 2, delayDays: 7, copyTypeId: 'follow-up', subject: 'Special offer inside', body: 'We have something special...', createdAt: new Date().toISOString() },
    ],
    isActive: false,
    totalEnrollments: 12,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
];

const mockEnrollments: EmailSequenceEnrollment[] = [
  { id: 'e1', sequenceId: '1', sequenceName: 'Welcome Series', contactId: 'c1', status: 'active', currentStep: 2, nextSendAt: new Date(Date.now() + 86400000).toISOString(), enrolledAt: new Date().toISOString() },
  { id: 'e2', sequenceId: '1', sequenceName: 'Welcome Series', leadId: 'l1', status: 'completed', currentStep: 3, enrolledAt: new Date().toISOString() },
];

const USE_MOCK = () => !isUsingRealApi(); // Use mock when not connected to real API

export async function getEmailSequences(): Promise<EmailSequence[]> {
  if (USE_MOCK()) {
    return [...mockSequences];
  }
  return authFetchJson<EmailSequence[]>('/api/emailsequences');
}

export async function getEmailSequenceById(id: string): Promise<EmailSequence | null> {
  if (USE_MOCK()) {
    return mockSequences.find(s => s.id === id) || null;
  }
  try {
    return await authFetchJson<EmailSequence>(`/api/emailsequences/${id}`);
  } catch {
    return null;
  }
}

export async function createEmailSequence(request: CreateEmailSequenceRequest): Promise<EmailSequence> {
  if (USE_MOCK()) {
    const newSequence: EmailSequence = {
      id: `seq-${Date.now()}`,
      name: request.name,
      description: request.description || '',
      steps: request.steps.map((s, i) => ({ ...s, id: `step-${Date.now()}-${i}`, createdAt: new Date().toISOString() })),
      isActive: true,
      totalEnrollments: 0,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    mockSequences.push(newSequence);
    return newSequence;
  }
  return authFetchJson<EmailSequence>('/api/emailsequences', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function updateEmailSequence(id: string, request: UpdateEmailSequenceRequest): Promise<EmailSequence> {
  if (USE_MOCK()) {
    const sequence = mockSequences.find(s => s.id === id);
    if (!sequence) throw new Error('Not found');
    Object.assign(sequence, request, { updatedAt: new Date().toISOString() });
    return { ...sequence };
  }
  return authFetchJson<EmailSequence>(`/api/emailsequences/${id}`, {
    method: 'PUT',
    body: JSON.stringify(request),
  });
}

export async function deleteEmailSequence(id: string): Promise<void> {
  if (USE_MOCK()) {
    const idx = mockSequences.findIndex(s => s.id === id);
    if (idx >= 0) mockSequences.splice(idx, 1);
    return;
  }
  await authFetchJson(`/api/emailsequences/${id}`, { method: 'DELETE' });
}

export async function addSequenceStep(sequenceId: string, step: CreateSequenceStepRequest): Promise<EmailSequence> {
  if (USE_MOCK()) {
    const sequence = mockSequences.find(s => s.id === sequenceId);
    if (!sequence) throw new Error('Not found');
    sequence.steps.push({ ...step, id: `step-${Date.now()}`, createdAt: new Date().toISOString() });
    sequence.updatedAt = new Date().toISOString();
    return { ...sequence };
  }
  return authFetchJson<EmailSequence>(`/api/emailsequences/${sequenceId}/steps`, {
    method: 'POST',
    body: JSON.stringify(step),
  });
}

export async function removeSequenceStep(sequenceId: string, stepId: string): Promise<void> {
  if (USE_MOCK()) {
    const sequence = mockSequences.find(s => s.id === sequenceId);
    if (sequence) {
      sequence.steps = sequence.steps.filter(s => s.id !== stepId);
      sequence.updatedAt = new Date().toISOString();
    }
    return;
  }
  await authFetchJson(`/api/emailsequences/${sequenceId}/steps/${stepId}`, { method: 'DELETE' });
}

export async function getEnrollments(sequenceId?: string): Promise<EmailSequenceEnrollment[]> {
  if (USE_MOCK()) {
    return sequenceId ? mockEnrollments.filter(e => e.sequenceId === sequenceId) : [...mockEnrollments];
  }
  const params = sequenceId ? `?sequenceId=${sequenceId}` : '';
  return authFetchJson<EmailSequenceEnrollment[]>(`/api/emailsequences/enrollments${params}`);
}

export async function enrollInSequence(request: EnrollInSequenceRequest): Promise<EmailSequenceEnrollment> {
  if (USE_MOCK()) {
    const sequence = mockSequences.find(s => s.id === request.sequenceId);
    const enrollment: EmailSequenceEnrollment = {
      id: `enroll-${Date.now()}`,
      sequenceId: request.sequenceId,
      sequenceName: sequence?.name || 'Unknown',
      contactId: request.contactId,
      leadId: request.leadId,
      status: 'active',
      currentStep: 1,
      nextSendAt: new Date(Date.now() + 86400000).toISOString(),
      enrolledAt: new Date().toISOString(),
    };
    mockEnrollments.push(enrollment);
    if (sequence) sequence.totalEnrollments++;
    return enrollment;
  }
  return authFetchJson<EmailSequenceEnrollment>('/api/emailsequences/enrollments', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function pauseEnrollment(enrollmentId: string): Promise<EmailSequenceEnrollment> {
  if (USE_MOCK()) {
    const enrollment = mockEnrollments.find(e => e.id === enrollmentId);
    if (!enrollment) throw new Error('Not found');
    enrollment.status = 'paused';
    return { ...enrollment };
  }
  return authFetchJson<EmailSequenceEnrollment>(`/api/emailsequences/enrollments/${enrollmentId}/pause`, {
    method: 'POST',
    body: JSON.stringify({}),
  });
}

export async function resumeEnrollment(enrollmentId: string): Promise<EmailSequenceEnrollment> {
  if (USE_MOCK()) {
    const enrollment = mockEnrollments.find(e => e.id === enrollmentId);
    if (!enrollment) throw new Error('Not found');
    enrollment.status = 'active';
    return { ...enrollment };
  }
  return authFetchJson<EmailSequenceEnrollment>(`/api/emailsequences/enrollments/${enrollmentId}/resume`, {
    method: 'POST',
    body: JSON.stringify({}),
  });
}

export async function unenroll(enrollmentId: string): Promise<void> {
  if (USE_MOCK()) {
    const idx = mockEnrollments.findIndex(e => e.id === enrollmentId);
    if (idx >= 0) {
      const enrollment = mockEnrollments[idx];
      if (enrollment) {
        const sequence = mockSequences.find(s => s.id === enrollment.sequenceId);
        if (sequence && sequence.totalEnrollments > 0) sequence.totalEnrollments--;
      }
      mockEnrollments.splice(idx, 1);
    }
    return;
  }
  await authFetchJson(`/api/emailsequences/enrollments/${enrollmentId}`, { method: 'DELETE' });
}
