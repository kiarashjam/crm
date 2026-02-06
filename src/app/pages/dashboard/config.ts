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
  BarChart3,
  Send,
  History,
  UsersRound,
  HelpCircle,
  FlaskConical,
  LayoutTemplate,
  Settings,
  Zap,
  Target,
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

// Primary navigation - main CRM features
export const quickNavPrimary = [
  { to: '/leads', icon: Users, label: 'Leads', description: 'Manage prospects', gradient: 'from-blue-500 to-indigo-600', glow: 'shadow-blue-500/40', bgHover: 'hover:bg-blue-50', shortcut: 'L' },
  { to: '/contacts', icon: UserCircle, label: 'Contacts', description: 'People directory', gradient: 'from-emerald-500 to-teal-600', glow: 'shadow-emerald-500/40', bgHover: 'hover:bg-emerald-50', shortcut: 'C' },
  { to: '/deals', icon: Target, label: 'Deals', description: 'Track pipeline', gradient: 'from-orange-500 to-amber-600', glow: 'shadow-orange-500/40', bgHover: 'hover:bg-orange-50', shortcut: 'D' },
  { to: '/tasks', icon: CheckSquare, label: 'Tasks', description: 'To-do items', gradient: 'from-violet-500 to-purple-600', glow: 'shadow-violet-500/40', bgHover: 'hover:bg-violet-50', shortcut: 'T' },
  { to: '/companies', icon: Building2, label: 'Companies', description: 'Organizations', gradient: 'from-cyan-500 to-blue-600', glow: 'shadow-cyan-500/40', bgHover: 'hover:bg-cyan-50', shortcut: 'O' },
  { to: '/activities', icon: Activity, label: 'Activities', description: 'Recent actions', gradient: 'from-rose-500 to-pink-600', glow: 'shadow-rose-500/40', bgHover: 'hover:bg-rose-50', shortcut: 'A' },
];

// Secondary navigation - tools & analytics
export const quickNavSecondary = [
  { to: '/analytics', icon: BarChart3, label: 'Analytics', gradient: 'from-indigo-500 to-blue-600', bgHover: 'hover:bg-indigo-50' },
  { to: '/sequences', icon: Send, label: 'Sequences', gradient: 'from-pink-500 to-rose-600', bgHover: 'hover:bg-pink-50' },
  { to: '/team', icon: UsersRound, label: 'Team', gradient: 'from-amber-500 to-orange-600', bgHover: 'hover:bg-amber-50' },
  { to: '/ab-tests', icon: FlaskConical, label: 'A/B Tests', gradient: 'from-purple-500 to-violet-600', bgHover: 'hover:bg-purple-50' },
];

// Utility navigation
export const quickNavUtility = [
  { to: '/templates', icon: LayoutTemplate, label: 'Templates' },
  { to: '/history', icon: History, label: 'History' },
  { to: '/settings', icon: Settings, label: 'Settings' },
  { to: '/help', icon: HelpCircle, label: 'Help' },
];

// Legacy export for backwards compatibility
export const quickNavItems = quickNavPrimary;
