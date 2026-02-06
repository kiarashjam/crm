import { authFetchJson, isUsingRealApi } from './apiClient';

export interface ABTestVariant {
  id: string;
  name: string;
  copyTypeId: string;
  subject: string;
  body: string;
  impressions: number;
  clicks: number;
  conversions: number;
  clickRate: number;
  conversionRate: number;
}

export interface ABTest {
  id: string;
  name: string;
  description: string;
  copyTypeId: string;
  status: 'draft' | 'running' | 'paused' | 'completed';
  variants: ABTestVariant[];
  winnerId?: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateABTestRequest {
  name: string;
  description?: string;
  copyTypeId: string;
  variants: {
    name: string;
    subject: string;
    body: string;
  }[];
}

export interface UpdateABTestRequest {
  name?: string;
  description?: string;
  status?: 'draft' | 'running' | 'paused' | 'completed';
  winnerId?: string;
}

// Mock data for development
const mockTests: ABTest[] = [
  {
    id: '1',
    name: 'Cold Email Subject Test',
    description: 'Testing different subject lines for cold outreach',
    copyTypeId: 'cold-email',
    status: 'running',
    variants: [
      { id: 'v1', name: 'Variant A', copyTypeId: 'cold-email', subject: 'Quick question about your business', body: 'Hi {{name}}, I noticed...', impressions: 150, clicks: 45, conversions: 12, clickRate: 30, conversionRate: 8 },
      { id: 'v2', name: 'Variant B', copyTypeId: 'cold-email', subject: 'Idea for {{company}}', body: 'Hello {{name}}, I had an idea...', impressions: 148, clicks: 52, conversions: 15, clickRate: 35.1, conversionRate: 10.1 },
    ],
    startedAt: new Date(Date.now() - 7 * 86400000).toISOString(),
    createdAt: new Date(Date.now() - 7 * 86400000).toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: '2',
    name: 'Follow-up Tone Test',
    description: 'Casual vs formal follow-up emails',
    copyTypeId: 'follow-up',
    status: 'completed',
    variants: [
      { id: 'v3', name: 'Casual', copyTypeId: 'follow-up', subject: 'Hey, just checking in!', body: 'Hey {{name}}, wanted to touch base...', impressions: 200, clicks: 70, conversions: 20, clickRate: 35, conversionRate: 10 },
      { id: 'v4', name: 'Formal', copyTypeId: 'follow-up', subject: 'Following up on our conversation', body: 'Dear {{name}}, I am following up...', impressions: 198, clicks: 55, conversions: 14, clickRate: 27.8, conversionRate: 7.1 },
    ],
    winnerId: 'v3',
    startedAt: new Date(Date.now() - 30 * 86400000).toISOString(),
    completedAt: new Date(Date.now() - 14 * 86400000).toISOString(),
    createdAt: new Date(Date.now() - 30 * 86400000).toISOString(),
    updatedAt: new Date(Date.now() - 14 * 86400000).toISOString(),
  },
];

const USE_MOCK = () => !isUsingRealApi(); // Use mock when not connected to real API

export async function getABTests(): Promise<ABTest[]> {
  if (USE_MOCK()) {
    return [...mockTests];
  }
  return authFetchJson<ABTest[]>('/api/abtests');
}

export async function getABTestById(id: string): Promise<ABTest | null> {
  if (USE_MOCK()) {
    return mockTests.find(t => t.id === id) || null;
  }
  try {
    return await authFetchJson<ABTest>(`/api/abtests/${id}`);
  } catch {
    return null;
  }
}

export async function createABTest(request: CreateABTestRequest): Promise<ABTest> {
  if (USE_MOCK()) {
    const newTest: ABTest = {
      id: `test-${Date.now()}`,
      name: request.name,
      description: request.description || '',
      copyTypeId: request.copyTypeId,
      status: 'draft',
      variants: request.variants.map((v, i) => ({
        id: `var-${Date.now()}-${i}`,
        name: v.name,
        copyTypeId: request.copyTypeId,
        subject: v.subject,
        body: v.body,
        impressions: 0,
        clicks: 0,
        conversions: 0,
        clickRate: 0,
        conversionRate: 0,
      })),
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    mockTests.push(newTest);
    return newTest;
  }
  return authFetchJson<ABTest>('/api/abtests', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function updateABTest(id: string, request: UpdateABTestRequest): Promise<ABTest> {
  if (USE_MOCK()) {
    const test = mockTests.find(t => t.id === id);
    if (!test) throw new Error('Not found');
    
    if (request.name !== undefined) test.name = request.name;
    if (request.description !== undefined) test.description = request.description;
    if (request.status !== undefined) {
      test.status = request.status;
      if (request.status === 'running' && !test.startedAt) {
        test.startedAt = new Date().toISOString();
      }
      if (request.status === 'completed') {
        test.completedAt = new Date().toISOString();
      }
    }
    if (request.winnerId !== undefined) test.winnerId = request.winnerId;
    test.updatedAt = new Date().toISOString();
    
    return { ...test };
  }
  return authFetchJson<ABTest>(`/api/abtests/${id}`, {
    method: 'PUT',
    body: JSON.stringify(request),
  });
}

export async function deleteABTest(id: string): Promise<void> {
  if (USE_MOCK()) {
    const idx = mockTests.findIndex(t => t.id === id);
    if (idx >= 0) mockTests.splice(idx, 1);
    return;
  }
  await authFetchJson(`/api/abtests/${id}`, { method: 'DELETE' });
}

export async function trackVariantEvent(variantId: string, eventType: 'impression' | 'click' | 'conversion'): Promise<ABTestVariant> {
  if (USE_MOCK()) {
    for (const test of mockTests) {
      const variant = test.variants.find(v => v.id === variantId);
      if (variant) {
        if (eventType === 'impression') variant.impressions++;
        else if (eventType === 'click') variant.clicks++;
        else if (eventType === 'conversion') variant.conversions++;
        
        variant.clickRate = variant.impressions > 0 ? (variant.clicks / variant.impressions) * 100 : 0;
        variant.conversionRate = variant.impressions > 0 ? (variant.conversions / variant.impressions) * 100 : 0;
        
        return { ...variant };
      }
    }
    throw new Error('Variant not found');
  }
  return authFetchJson<ABTestVariant>(`/api/abtests/variants/${variantId}/track?eventType=${eventType}`, {
    method: 'POST',
    body: JSON.stringify({}),
  });
}

export function calculateWinner(test: ABTest): ABTestVariant | null {
  if (test.variants.length === 0) return null;
  
  // Need minimum sample size
  const minSamples = 50;
  const eligibleVariants = test.variants.filter(v => v.impressions >= minSamples);
  
  if (eligibleVariants.length === 0) return null;
  
  // Simple winner by conversion rate
  return eligibleVariants.reduce((best, current) => 
    current.conversionRate > best.conversionRate ? current : best
  );
}

export function getTestStatusColor(status: ABTest['status']): string {
  switch (status) {
    case 'draft': return 'bg-gray-100 text-gray-800';
    case 'running': return 'bg-green-100 text-green-800';
    case 'paused': return 'bg-yellow-100 text-yellow-800';
    case 'completed': return 'bg-blue-100 text-blue-800';
    default: return 'bg-gray-100 text-gray-800';
  }
}
