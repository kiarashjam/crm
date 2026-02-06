/**
 * Lead configuration constants
 * Extracted from Leads.tsx
 */

export interface LeadStatusConfig {
  bg: string;
  text: string;
  border: string;
}

/**
 * Lead status color configuration
 */
export const LEAD_STATUS_COLORS: Record<string, LeadStatusConfig> = {
  new: { bg: 'bg-blue-100', text: 'text-blue-800', border: 'border-blue-200' },
  contacted: { bg: 'bg-amber-100', text: 'text-amber-800', border: 'border-amber-200' },
  qualified: { bg: 'bg-emerald-100', text: 'text-emerald-800', border: 'border-emerald-200' },
  proposal: { bg: 'bg-purple-100', text: 'text-purple-800', border: 'border-purple-200' },
  negotiation: { bg: 'bg-indigo-100', text: 'text-indigo-800', border: 'border-indigo-200' },
  won: { bg: 'bg-green-100', text: 'text-green-800', border: 'border-green-200' },
  lost: { bg: 'bg-red-100', text: 'text-red-800', border: 'border-red-200' },
  default: { bg: 'bg-slate-100', text: 'text-slate-800', border: 'border-slate-200' },
} as const;

/**
 * Lead source icons
 */
export const LEAD_SOURCE_ICONS: Record<string, string> = {
  website: '🌐',
  referral: '🤝',
  linkedin: '💼',
  cold_call: '📞',
  email: '📧',
  event: '📅',
  advertisement: '📢',
  partner: '🤝',
  social_media: '📱',
  other: '📌',
} as const;

/**
 * Wizard steps for lead creation
 */
export interface WizardStep {
  id: string;
  title: string;
  description: string;
  icon: string;
}

export const LEAD_WIZARD_STEPS: WizardStep[] = [
  { 
    id: 'basic', 
    title: 'Basic Info', 
    description: 'Name and contact details',
    icon: '👤'
  },
  { 
    id: 'company', 
    title: 'Company', 
    description: 'Company information',
    icon: '🏢'
  },
  { 
    id: 'source', 
    title: 'Source', 
    description: 'How did they find you?',
    icon: '🔍'
  },
  { 
    id: 'details', 
    title: 'Details', 
    description: 'Additional information',
    icon: '📝'
  },
] as const;

/**
 * Default lead form values
 */
export const DEFAULT_LEAD_FORM = {
  name: '',
  email: '',
  phone: '',
  companyId: '',
  source: '',
  status: 'new',
  leadSourceId: '',
  leadStatusId: '',
  leadScore: undefined as number | undefined,
  lastContactedAt: '',
  description: '',
  lifecycleStage: '',
} as const;

/**
 * Get status color config
 */
export function getLeadStatusColor(status: string): LeadStatusConfig {
  return LEAD_STATUS_COLORS[status.toLowerCase()] ?? LEAD_STATUS_COLORS['default']!;
}

/**
 * Get source icon
 */
export function getLeadSourceIcon(source: string): string {
  return LEAD_SOURCE_ICONS[source.toLowerCase()] ?? LEAD_SOURCE_ICONS['other']!;
}
