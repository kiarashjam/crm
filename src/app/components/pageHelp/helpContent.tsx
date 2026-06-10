// Per-page help content for the floating "?" button. Keyed by the first path
// segment (with explicit entries for detail pages). Each entry renders as an
// illustrated modal: a hero icon + tagline, then a grid of "what you can do"
// feature cards, and optional tips.

import {
  LayoutDashboard, Users, UserCircle, Building2, Kanban, CheckSquare, Activity,
  Workflow, BarChart3, Zap, Copy, ScrollText, UsersRound, Settings, LayoutTemplate,
  History, Send, Sparkles, Mail, Plus, Filter,
  MessageSquarePlus, ArrowRightCircle, UserPlus, Paperclip, SlidersHorizontal,
  Bell, FileText, FileSpreadsheet, Pencil, GitMerge, Target, TrendingUp,
  Building, Briefcase, Clock, Download, Search, Bot,
} from 'lucide-react';

export interface HelpFeature {
  icon: React.ElementType;
  title: string;
  body: string;
}

export interface HelpEntry {
  icon: React.ElementType;
  /** Tailwind gradient classes for the hero badge, e.g. "from-indigo-500 to-violet-500". */
  gradient: string;
  title: string;
  tagline: string;
  features: HelpFeature[];
  tips?: string[];
}

const HELP: Record<string, HelpEntry> = {
  dashboard: {
    icon: LayoutDashboard, gradient: 'from-indigo-500 to-violet-500',
    title: 'Dashboard', tagline: 'Your home base — a snapshot of the pipeline and a launchpad to everything.',
    features: [
      { icon: TrendingUp, title: 'At-a-glance stats', body: 'See active leads, open deals, pipeline value and what’s won/lost.' },
      { icon: Bot, title: 'AI Sales Writer', body: 'Generate emails, follow-ups and notes in your brand voice, right here.' },
      { icon: ArrowRightCircle, title: 'Quick navigation', body: 'Jump straight to Leads, Deals, Tasks and the rest from the shortcuts.' },
    ],
    tips: ['Use the top nav (or the search) to move between sections fast.'],
  },
  leads: {
    icon: Users, gradient: 'from-orange-500 to-amber-500',
    title: 'Leads', tagline: 'Capture, qualify and route inbound interest before it becomes a deal.',
    features: [
      { icon: Plus, title: 'Add leads', body: 'Create one manually, or bring many in via CSV Import or the inbound Webhook.' },
      { icon: Filter, title: 'Filter & sort', body: 'Filter by status, source, assignment or conversion; sort by date, name, status.' },
      { icon: MessageSquarePlus, title: 'Log without leaving', body: 'Hit “Log” on a card to record a call/email/meeting/note inline.' },
      { icon: UserPlus, title: 'Assign owners', body: 'Click the owner chip on a card to (re)assign the lead to a teammate.' },
      { icon: Download, title: 'Export', body: 'Export the filtered list to CSV for Sheets/Excel.' },
    ],
    tips: ['Click a lead to open its full detail page.', 'Press “n” to add a lead, “/” to focus search.'],
  },
  leadDetail: {
    icon: UserCircle, gradient: 'from-orange-500 to-amber-500',
    title: 'Lead detail', tagline: 'Everything about one lead — edit, log, assign, and move it forward.',
    features: [
      { icon: Pencil, title: 'Edit every field', body: 'Use Edit for the full editor (incl. “Referred by”), or edit inline.' },
      { icon: Mail, title: 'Email & log', body: 'Send an email (logged to the timeline) or quick-log any activity.' },
      { icon: UserPlus, title: 'Owner', body: 'Set the owner from the sidebar — it stays in sync with the list.' },
      { icon: Sparkles, title: 'AI assist', body: 'A derived score and the recommended next best action.' },
      { icon: ArrowRightCircle, title: 'Convert / Save as contact', body: 'Promote to a deal, or create a contact from the lead’s details.' },
    ],
  },
  deals: {
    icon: Kanban, gradient: 'from-emerald-500 to-teal-500',
    title: 'Deals pipeline', tagline: 'A drag-and-drop board of open opportunities from qualification to close.',
    features: [
      { icon: Kanban, title: 'Drag to move stages', body: 'Drag a deal card between columns to update its stage.' },
      { icon: Plus, title: 'Create & edit', body: 'Add deals with value, owner, stage and expected close date.' },
      { icon: Filter, title: 'Filter & focus', body: 'Filter by owner, value, close date or “my deals”.' },
      { icon: TrendingUp, title: 'Pipeline value', body: 'See totals and a stage funnel update as you work.' },
    ],
  },
  dealDetail: {
    icon: Briefcase, gradient: 'from-emerald-500 to-teal-500',
    title: 'Deal detail', tagline: 'Manage one opportunity end to end.',
    features: [
      { icon: Briefcase, title: 'Products & line items', body: 'Break the deal into products/quantities; sync the total to the value.' },
      { icon: SlidersHorizontal, title: 'Custom fields', body: 'Fill any custom fields your workspace defined for deals.' },
      { icon: Paperclip, title: 'Attachments', body: 'Attach contracts, proposals and docs.' },
      { icon: MessageSquarePlus, title: 'Activity & tasks', body: 'Log activity and track linked tasks.' },
    ],
  },
  tasks: {
    icon: CheckSquare, gradient: 'from-cyan-500 to-blue-500',
    title: 'Tasks', tagline: 'Stay on top of follow-ups so nothing slips.',
    features: [
      { icon: Plus, title: 'Create tasks', body: 'Add tasks with a title, due date, reminder and priority.' },
      { icon: Clock, title: 'Due & overdue', body: 'Overdue and due-today tasks are flagged and surface in notifications.' },
      { icon: CheckSquare, title: 'Track status', body: 'Move tasks through to-do, in-progress, completed or cancelled.' },
    ],
  },
  contacts: {
    icon: UserCircle, gradient: 'from-blue-500 to-cyan-500',
    title: 'Contacts', tagline: 'Your people — the humans behind the deals.',
    features: [
      { icon: Plus, title: 'Add contacts', body: 'Create contacts with role, company and preferences.' },
      { icon: Mail, title: 'Email & log', body: 'Email a contact (with an AI draft) and keep the history.' },
      { icon: SlidersHorizontal, title: 'Custom fields & files', body: 'Capture custom fields and attach documents per contact.' },
      { icon: Download, title: 'Export', body: 'Export the filtered contacts to CSV.' },
    ],
  },
  companies: {
    icon: Building2, gradient: 'from-violet-500 to-purple-500',
    title: 'Companies', tagline: 'The accounts your contacts and deals belong to.',
    features: [
      { icon: Building, title: 'Manage accounts', body: 'Add companies with domain, industry, size and location.' },
      { icon: Users, title: 'Linked records', body: 'See the contacts and deals tied to each company.' },
      { icon: Download, title: 'Export', body: 'Export the filtered companies to CSV.' },
    ],
  },
  activities: {
    icon: Activity, gradient: 'from-amber-500 to-orange-500',
    title: 'Activities', tagline: 'The full history of calls, emails, meetings and notes.',
    features: [
      { icon: MessageSquarePlus, title: 'Log anything', body: 'Record calls, emails, meetings, demos, notes and more.' },
      { icon: Filter, title: 'Filter the feed', body: 'Filter by type, search, or a specific contact/deal/lead.' },
      { icon: FileText, title: 'Export as PDF', body: 'Generate a clean, print-ready report of the history.' },
      { icon: FileSpreadsheet, title: 'Export to Sheets', body: 'Download a CSV that opens in Google Sheets or Excel.' },
    ],
  },
  sequences: {
    icon: Workflow, gradient: 'from-indigo-500 to-blue-500',
    title: 'Sequences', tagline: 'Automated, multi-step outreach cadences for leads and contacts.',
    features: [
      { icon: Plus, title: 'Build a cadence', body: 'Chain email / call / task / wait steps with day offsets.' },
      { icon: UserPlus, title: 'Enroll people', body: 'Add leads to a sequence; track who’s active.' },
      { icon: Zap, title: 'Activate / pause', body: 'Toggle a sequence on or off and edit steps anytime.' },
    ],
  },
  reports: {
    icon: BarChart3, gradient: 'from-indigo-500 to-violet-500',
    title: 'Reports', tagline: 'Pipeline, forecast, lead and activity analytics.',
    features: [
      { icon: TrendingUp, title: 'Pipeline & forecast', body: 'Open pipeline, weighted forecast, win rate and avg deal size.' },
      { icon: Target, title: 'Lead analytics', body: 'Conversion rate, lead funnel, new-leads trend and lifecycle.' },
      { icon: BarChart3, title: 'Breakdowns', body: 'By stage, owner, status and source — with charts.' },
    ],
  },
  automations: {
    icon: Zap, gradient: 'from-amber-500 to-orange-500',
    title: 'Automations', tagline: 'Run actions automatically when something happens.',
    features: [
      { icon: Zap, title: 'Trigger → actions', body: 'e.g. “When a lead is created → send email + create a task”.' },
      { icon: Plus, title: 'Build rules', body: 'Pick a trigger, then one or more actions to run.' },
      { icon: CheckSquare, title: 'Enable / disable', body: 'Toggle any rule on or off without deleting it.' },
    ],
  },
  duplicates: {
    icon: Copy, gradient: 'from-rose-500 to-pink-500',
    title: 'Duplicates', tagline: 'Keep your data clean by finding and merging duplicates.',
    features: [
      { icon: Search, title: 'Auto-detect', body: 'Scans contacts (email/name) and companies (domain/name).' },
      { icon: GitMerge, title: 'Pick & merge', body: 'Choose which record to keep; the rest merge into it.' },
    ],
  },
  audit: {
    icon: ScrollText, gradient: 'from-slate-500 to-slate-600',
    title: 'Audit log', tagline: 'A chronological record of changes across the CRM.',
    features: [
      { icon: ScrollText, title: 'Change history', body: 'See created/updated/deleted, status changes, merges and more.' },
      { icon: Filter, title: 'Filter by type', body: 'Focus on leads, contacts, companies or deals.' },
    ],
  },
  team: {
    icon: UsersRound, gradient: 'from-teal-500 to-emerald-500',
    title: 'Team', tagline: 'Your workspace members and what they can do.',
    features: [
      { icon: UsersRound, title: 'Members', body: 'See everyone in the organization and their roles.' },
      { icon: UserPlus, title: 'Assignment', body: 'Members become available as owners on leads and deals.' },
    ],
  },
  templates: {
    icon: LayoutTemplate, gradient: 'from-fuchsia-500 to-pink-500',
    title: 'Templates', tagline: 'Reusable recipes for the AI Sales Writer.',
    features: [
      { icon: Sparkles, title: 'Start faster', body: 'Pick a template to seed the AI generator with a goal and tone.' },
      { icon: LayoutTemplate, title: 'Browse by type', body: 'Find templates by category and copy type.' },
    ],
  },
  history: {
    icon: History, gradient: 'from-slate-500 to-slate-600',
    title: 'Copy history', tagline: 'Everything the AI Sales Writer has generated for you.',
    features: [
      { icon: History, title: 'Revisit & reuse', body: 'Find past generations and copy them again.' },
    ],
  },
  send: {
    icon: Send, gradient: 'from-orange-500 to-rose-500',
    title: 'Send to CRM', tagline: 'Push generated copy and details into your CRM records.',
    features: [
      { icon: Send, title: 'Route content', body: 'Send drafted copy into the right record as a note or message.' },
    ],
  },
  settings: {
    icon: Settings, gradient: 'from-slate-600 to-slate-700',
    title: 'Settings', tagline: 'Configure your profile, brand, pipeline and custom fields.',
    features: [
      { icon: Sparkles, title: 'Brand & AI', body: 'Set your brand voice/tone for the AI writer.' },
      { icon: SlidersHorizontal, title: 'Custom fields', body: 'Add your own fields to leads, contacts, companies and deals.' },
      { icon: Kanban, title: 'Pipelines', body: 'Customize your deal pipeline stages.' },
      { icon: Bell, title: 'Notifications & more', body: 'Tune notifications, appearance, security and account.' },
    ],
  },
  organizations: {
    icon: Building2, gradient: 'from-orange-500 to-amber-500',
    title: 'Organizations', tagline: 'Switch between or manage your workspaces.',
    features: [
      { icon: Building2, title: 'Workspaces', body: 'Each organization has its own leads, deals and team.' },
    ],
  },
};

const DEFAULT_HELP: HelpEntry = {
  icon: Sparkles, gradient: 'from-indigo-500 to-violet-500',
  title: 'About this page', tagline: 'Here’s what you can do here.',
  features: [
    { icon: ArrowRightCircle, title: 'Navigate', body: 'Use the top navigation to move between sections.' },
    { icon: Search, title: 'Find anything', body: 'Use search to jump to contacts, companies and deals.' },
  ],
};

/** Resolve the help entry for a pathname (handles /section/:id detail pages). */
export function resolvePageHelp(pathname: string): HelpEntry {
  if (/^\/leads\/[^/]+$/.test(pathname) && !/\/(webhook|import)$/.test(pathname)) return HELP.leadDetail ?? DEFAULT_HELP;
  if (/^\/deals\/[^/]+$/.test(pathname)) return HELP.dealDetail ?? DEFAULT_HELP;
  if (/^\/contacts\/[^/]+$/.test(pathname)) return HELP.contacts ?? DEFAULT_HELP;
  if (/^\/companies\/[^/]+$/.test(pathname)) return HELP.companies ?? DEFAULT_HELP;
  if (/^\/tasks\/[^/]+$/.test(pathname)) return HELP.tasks ?? DEFAULT_HELP;
  const seg = pathname.split('/').filter(Boolean)[0] || 'dashboard';
  return HELP[seg] ?? DEFAULT_HELP;
}
