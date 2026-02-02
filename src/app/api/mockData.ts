import type { Contact, Company, Deal, Lead, TaskItem, Activity, Template, CopyHistoryItem } from './types';

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

export const mockLeads: Lead[] = [
  { id: '1', name: 'Alex Turner', email: 'alex.turner@example.com', phone: '+1 555-0101', companyId: '1', source: 'website', status: 'New' },
  { id: '2', name: 'Jordan Lee', email: 'jordan.lee@example.com', companyId: '2', source: 'referral', status: 'Contacted' },
  { id: '3', name: 'Sam Rivera', email: 'sam.rivera@example.com', phone: '+1 555-0103', companyId: '3', source: 'events', status: 'Qualified' },
  { id: '4', name: 'Casey Morgan', email: 'casey.morgan@example.com', companyId: '1', source: 'ads', status: 'New' },
  { id: '5', name: 'Riley Clark', email: 'riley.clark@example.com', phone: '+1 555-0105', companyId: '2', source: 'manual', status: 'Contacted' },
];

const now = new Date();
const tomorrow = new Date(now);
tomorrow.setDate(tomorrow.getDate() + 1);
const nextWeek = new Date(now);
nextWeek.setDate(nextWeek.getDate() + 7);
const lastWeek = new Date(now);
lastWeek.setDate(lastWeek.getDate() - 7);

export const mockTasks: TaskItem[] = [
  { id: '1', title: 'Follow up with Acme Corp', description: 'Send proposal and schedule call', dueDateUtc: tomorrow.toISOString(), completed: false, leadId: '1', dealId: '1' },
  { id: '2', title: 'Prepare demo for TechStart', description: 'Custom demo deck', dueDateUtc: nextWeek.toISOString(), completed: false, dealId: '2' },
  { id: '3', title: 'Send contract to Global Solutions', description: 'Final review', dueDateUtc: lastWeek.toISOString(), completed: true, dealId: '3' },
  { id: '4', title: 'Call Jordan Lee', description: 'Intro call', dueDateUtc: tomorrow.toISOString(), completed: false, leadId: '2' },
  { id: '5', title: 'Update CRM notes', description: 'Log last meeting', dueDateUtc: nextWeek.toISOString(), completed: false },
  { id: '6', title: 'Overdue: Send follow-up email', description: 'Missed deadline', dueDateUtc: lastWeek.toISOString(), completed: false, leadId: '4' },
];

export const mockActivities: Activity[] = [
  { id: '1', type: 'call', subject: 'Intro call with John', body: 'Discussed pricing and timeline.', contactId: '1', dealId: '1', createdAt: new Date(Date.now() - 86400000 * 2).toISOString() },
  { id: '2', type: 'email', subject: 'Proposal sent', body: 'Sent enterprise proposal to Acme.', contactId: '1', dealId: '1', createdAt: new Date(Date.now() - 86400000).toISOString() },
  { id: '3', type: 'meeting', subject: 'Demo with Sarah', body: 'Product walkthrough completed.', contactId: '2', dealId: '2', createdAt: new Date(Date.now() - 86400000 * 3).toISOString() },
  { id: '4', type: 'note', subject: 'Follow-up reminder', body: 'Check in on contract next week.', contactId: '3', dealId: '3', createdAt: new Date(Date.now() - 86400000 * 5).toISOString() },
  { id: '5', type: 'call', subject: 'Discovery with Mike', body: 'Needs assessment done.', contactId: '3', createdAt: new Date(Date.now() - 86400000 * 1).toISOString() },
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

const historyNow = new Date();
const historyYesterday = new Date(historyNow.getTime() - 86400000);
const historyLastWeek = new Date(historyNow.getTime() - 86400000 * 5);

export const mockCopyHistory: CopyHistoryItem[] = [
  {
    id: 'hist_demo_1',
    type: 'Sales Email',
    copy: 'Hi John,\n\nI wanted to follow up on our conversation about the Enterprise Plan. Would you have 15 minutes this week for a quick call to discuss next steps?\n\nBest regards',
    recipientName: 'John Smith',
    recipientType: 'contact',
    recipientId: '1',
    createdAt: historyNow.toISOString(),
  },
  {
    id: 'hist_demo_2',
    type: 'Follow-up',
    copy: 'Hi Sarah,\n\nThank you for the demo yesterday. As discussed, here are the key points we covered and the proposal link. Let me know if you have any questions.\n\nBest',
    recipientName: 'TechStart Inc - Pro Package',
    recipientType: 'deal',
    recipientId: '2',
    createdAt: historyYesterday.toISOString(),
  },
  {
    id: 'hist_demo_3',
    type: 'CRM Note',
    copy: 'Called Mike to confirm timeline. He will review the contract by Friday and get back with feedback.',
    recipientName: 'Mike Williams',
    recipientType: 'contact',
    recipientId: '3',
    createdAt: historyLastWeek.toISOString(),
  },
];
