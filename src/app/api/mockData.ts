import type { Contact, Company, Deal, Template } from './types';

export const mockContacts: Contact[] = [
  { id: '1', name: 'John Smith', email: 'john@example.com', companyId: '1' },
  { id: '2', name: 'Sarah Johnson', email: 'sarah@example.com', companyId: '2' },
  { id: '3', name: 'Mike Williams', email: 'mike@example.com', companyId: '1' },
  { id: '4', name: 'Emily Davis', email: 'emily@example.com', companyId: '3' },
  { id: '5', name: 'James Brown', email: 'james@example.com', companyId: '2' },
];

export const mockCompanies: Company[] = [
  { id: '1', name: 'Acme Corp' },
  { id: '2', name: 'TechStart Inc' },
  { id: '3', name: 'Global Solutions' },
];

export const mockDeals: Deal[] = [
  { id: '1', name: 'Acme Corp - Enterprise Plan', value: '$50,000', stage: 'Proposal', companyId: '1' },
  { id: '2', name: 'TechStart Inc - Pro Package', value: '$25,000', stage: 'Negotiation', companyId: '2' },
  { id: '3', name: 'Global Solutions - Custom', value: '$75,000', stage: 'Closed Won', companyId: '3' },
  { id: '4', name: 'Acme Corp - Renewal', value: '$45,000', stage: 'Qualification', companyId: '1' },
];

export const mockTemplates: Template[] = [
  {
    id: '1',
    title: 'First contact email',
    description: 'Initial outreach to new prospects',
    category: 'Sales',
    copyTypeId: 'sales-email',
    goal: 'Schedule a meeting',
    useCount: 245,
  },
  {
    id: '2',
    title: 'Follow-up after meeting',
    description: 'Thank you and next steps',
    category: 'Follow-up',
    copyTypeId: 'follow-up',
    goal: 'Follow up after demo',
    useCount: 189,
  },
  {
    id: '3',
    title: 'Demo reminder',
    description: 'Confirm upcoming product demo',
    category: 'Meetings',
    copyTypeId: 'sales-email',
    goal: 'Schedule a meeting',
    useCount: 156,
  },
  {
    id: '4',
    title: 'Closing deal message',
    description: 'Final push to close the sale',
    category: 'Sales',
    copyTypeId: 'deal-message',
    goal: 'Close the deal',
    useCount: 134,
  },
  {
    id: '5',
    title: 'Re-engagement email',
    description: 'Reconnect with cold leads',
    category: 'Re-engagement',
    copyTypeId: 'sales-email',
    goal: 'Check in on progress',
    useCount: 98,
  },
];
