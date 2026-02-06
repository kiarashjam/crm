import {
  Mail,
  MessageSquare,
  FileText,
  Briefcase,
  Workflow,
  Users,
  UserCircle,
  Building2,
  CheckSquare,
  Activity,
} from 'lucide-react';
import type { CopyTypeId } from '@/app/api/types';

export const copyTypes = [
  { id: 'sales-email' as CopyTypeId, icon: Mail, title: 'Sales Email', desc: 'Personalized outreach', color: 'from-blue-500 to-cyan-500' },
  { id: 'follow-up' as CopyTypeId, icon: MessageSquare, title: 'Follow-up', desc: 'Keep conversations going', color: 'from-violet-500 to-purple-500' },
  { id: 'crm-note' as CopyTypeId, icon: FileText, title: 'CRM Note', desc: 'Document interactions', color: 'from-emerald-500 to-teal-500' },
  { id: 'deal-message' as CopyTypeId, icon: Briefcase, title: 'Deal Message', desc: 'Update stakeholders', color: 'from-orange-500 to-amber-500' },
  { id: 'workflow-message' as CopyTypeId, icon: Workflow, title: 'Workflow', desc: 'Automated sequences', color: 'from-rose-500 to-pink-500' },
];

export const goals = [
  'Schedule a meeting',
  'Follow up after demo',
  'Request feedback',
  'Share resources',
  'Check in on progress',
  'Close the deal',
];

export const quickNavItems = [
  { to: '/leads', icon: Users, label: 'Leads', color: 'bg-blue-500' },
  { to: '/contacts', icon: UserCircle, label: 'Contacts', color: 'bg-emerald-500' },
  { to: '/deals', icon: Briefcase, label: 'Deals', color: 'bg-orange-500' },
  { to: '/tasks', icon: CheckSquare, label: 'Tasks', color: 'bg-violet-500' },
  { to: '/companies', icon: Building2, label: 'Companies', color: 'bg-cyan-500' },
  { to: '/activities', icon: Activity, label: 'Activities', color: 'bg-rose-500' },
];
