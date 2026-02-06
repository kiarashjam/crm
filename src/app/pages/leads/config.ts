import { User, Building2, Activity, Briefcase, Phone, Mail, Calendar, RefreshCw } from 'lucide-react';
import type { WizardStep } from './types';

// Step configuration with descriptions
export const WIZARD_STEPS: { id: WizardStep; label: string; shortLabel: string; description: string; icon: React.ElementType }[] = [
  { 
    id: 'contact', 
    label: 'Contact Info', 
    shortLabel: 'Contact',
    description: 'Basic identification details to reach the lead',
    icon: User 
  },
  { 
    id: 'company', 
    label: 'Company & Source', 
    shortLabel: 'Source',
    description: 'Organization affiliation and how they found you',
    icon: Building2 
  },
  { 
    id: 'qualification', 
    label: 'Qualification', 
    shortLabel: 'Qualify',
    description: 'Assess lead quality and track their journey',
    icon: Activity 
  },
  { 
    id: 'notes', 
    label: 'Notes', 
    shortLabel: 'Notes',
    description: 'Additional context and observations',
    icon: Briefcase 
  },
];

// Activity type definitions for the timeline
export const ACTIVITY_TYPES = [
  { id: 'call', label: 'Call', icon: Phone, color: 'blue' },
  { id: 'email', label: 'Email', icon: Mail, color: 'purple' },
  { id: 'meeting', label: 'Meeting', icon: Calendar, color: 'green' },
  { id: 'note', label: 'Note', icon: Briefcase, color: 'amber' },
  { id: 'system', label: 'Update', icon: RefreshCw, color: 'slate' },
];

// Fallback statuses when API doesn't return any
export const FALLBACK_STATUSES = [
  'New', 'Open', 'Attempted Contact', 'Contacted', 'Connected', 
  'In Progress', 'Qualified', 'Unqualified', 'Open Deal', 'Lost'
];

// Fallback sources when API doesn't return any
export const FALLBACK_SOURCES = [
  'Website', 'Referral', 'Social Media', 'Paid Search', 'Email Campaign',
  'Cold Call', 'Events', 'Partner', 'LinkedIn', 'Manual'
];

// Lifecycle stages
export const LIFECYCLE_STAGES = ['MQL', 'SQL', 'Nurture', 'Cold', 'Hot'];

// Source icons mapping
export const SOURCE_ICONS: Record<string, string> = {
  website: '🌐',
  referral: '🤝',
  'social media': '📱',
  'paid search': '📢',
  'email campaign': '📧',
  'cold call': '📞',
  events: '🎯',
  partner: '🤝',
  linkedin: '💼',
  manual: '✏️',
};

// Status colors mapping for AddLeadDialog
export const STATUS_COLORS: Record<string, { bg: string; text: string; border: string; selectedBg: string; selectedBorder: string; icon: string }> = {
  New: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', selectedBg: 'bg-blue-500', selectedBorder: 'border-blue-500', icon: '✦' },
  Open: { bg: 'bg-cyan-50', text: 'text-cyan-700', border: 'border-cyan-200', selectedBg: 'bg-cyan-500', selectedBorder: 'border-cyan-500', icon: '○' },
  'Attempted Contact': { bg: 'bg-yellow-50', text: 'text-yellow-700', border: 'border-yellow-200', selectedBg: 'bg-yellow-500', selectedBorder: 'border-yellow-500', icon: '◎' },
  Contacted: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', selectedBg: 'bg-amber-500', selectedBorder: 'border-amber-500', icon: '◉' },
  Connected: { bg: 'bg-purple-50', text: 'text-purple-700', border: 'border-purple-200', selectedBg: 'bg-purple-500', selectedBorder: 'border-purple-500', icon: '⟡' },
  'In Progress': { bg: 'bg-indigo-50', text: 'text-indigo-700', border: 'border-indigo-200', selectedBg: 'bg-indigo-500', selectedBorder: 'border-indigo-500', icon: '◈' },
  Qualified: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', selectedBg: 'bg-emerald-500', selectedBorder: 'border-emerald-500', icon: '★' },
  Unqualified: { bg: 'bg-orange-50', text: 'text-orange-700', border: 'border-orange-200', selectedBg: 'bg-orange-500', selectedBorder: 'border-orange-500', icon: '✗' },
  'Open Deal': { bg: 'bg-green-50', text: 'text-green-700', border: 'border-green-200', selectedBg: 'bg-green-500', selectedBorder: 'border-green-500', icon: '◆' },
  Lost: { bg: 'bg-slate-100', text: 'text-slate-500', border: 'border-slate-200', selectedBg: 'bg-slate-400', selectedBorder: 'border-slate-400', icon: '—' },
};

// Status colors for badges in LeadDetailModal
export const STATUS_BADGE_COLORS: Record<string, { bg: string; text: string; border: string; dot: string }> = {
  New: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', dot: 'bg-blue-500' },
  Open: { bg: 'bg-cyan-50', text: 'text-cyan-700', border: 'border-cyan-200', dot: 'bg-cyan-500' },
  'Attempted Contact': { bg: 'bg-yellow-50', text: 'text-yellow-700', border: 'border-yellow-200', dot: 'bg-yellow-500' },
  Contacted: { bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200', dot: 'bg-amber-500' },
  Connected: { bg: 'bg-purple-50', text: 'text-purple-700', border: 'border-purple-200', dot: 'bg-purple-500' },
  'In Progress': { bg: 'bg-indigo-50', text: 'text-indigo-700', border: 'border-indigo-200', dot: 'bg-indigo-500' },
  Qualified: { bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200', dot: 'bg-emerald-500' },
  Unqualified: { bg: 'bg-orange-50', text: 'text-orange-700', border: 'border-orange-200', dot: 'bg-orange-500' },
  'Open Deal': { bg: 'bg-green-50', text: 'text-green-700', border: 'border-green-200', dot: 'bg-green-500' },
  Lost: { bg: 'bg-slate-100', text: 'text-slate-500', border: 'border-slate-200', dot: 'bg-slate-400' },
};

// Default empty form
export const EMPTY_LEAD_FORM = {
  name: '',
  email: '',
  phone: '',
  companyId: '',
  source: 'Manual',
  status: 'New',
  leadSourceId: '',
  leadStatusId: '',
  leadScore: '',
  description: '',
  lifecycleStage: '',
};

// Default convert form state
export const DEFAULT_CONVERT_FORM = {
  createContact: true,
  createDeal: false,
  dealName: '',
  dealValue: '',
  dealStage: 'Qualification',
  pipelineId: undefined,
  dealStageId: undefined,
  createNewCompany: false,
  newCompanyName: '',
  existingCompanyId: undefined,
  existingContactId: undefined,
  existingDealId: undefined,
};
