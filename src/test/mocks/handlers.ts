import { http, HttpResponse } from 'msw';

export const handlers = [
  // Leads
  http.get('/api/leads', () => {
    return HttpResponse.json([
      { 
        id: '1', 
        name: 'Test Lead', 
        email: 'test@example.com',
        status: 'new',
        source: 'website',
        createdAt: new Date().toISOString(),
      },
    ]);
  }),

  // Companies
  http.get('/api/companies', () => {
    return HttpResponse.json([
      { id: '1', name: 'Test Company', industry: 'Technology' },
    ]);
  }),

  // Contacts
  http.get('/api/contacts', () => {
    return HttpResponse.json([
      { 
        id: '1', 
        firstName: 'John', 
        lastName: 'Doe',
        email: 'john@example.com',
      },
    ]);
  }),

  // Deals
  http.get('/api/deals', () => {
    return HttpResponse.json([
      { 
        id: '1', 
        name: 'Test Deal', 
        value: 10000,
        stage: 'prospecting',
      },
    ]);
  }),

  // Tasks
  http.get('/api/tasks', () => {
    return HttpResponse.json([
      { 
        id: '1', 
        title: 'Test Task', 
        status: 'todo',
        priority: 'medium',
      },
    ]);
  }),

  // Activities
  http.get('/api/activities', () => {
    return HttpResponse.json([
      { 
        id: '1', 
        type: 'call', 
        description: 'Test Activity',
        createdAt: new Date().toISOString(),
      },
    ]);
  }),

  // Templates
  http.get('/api/templates', () => {
    return HttpResponse.json([
      { 
        id: '1', 
        name: 'Test Template', 
        category: 'email',
      },
    ]);
  }),

  // User settings
  http.get('/api/settings', () => {
    return HttpResponse.json({
      theme: 'light',
      emailDigest: 'daily',
    });
  }),
];
