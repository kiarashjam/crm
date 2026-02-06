import {
  Briefcase,
  Target,
  Handshake,
  Trophy,
  XCircle,
} from 'lucide-react';

export const STAGE_COLORS = [
  'from-blue-500 to-cyan-500',
  'from-violet-500 to-purple-500',
  'from-orange-500 to-amber-500',
  'from-emerald-500 to-teal-500',
  'from-rose-500 to-pink-500',
  'from-indigo-500 to-blue-500',
] as const;

export const DEFAULT_STAGES = [
  { name: 'Qualification', probability: 10 },
  { name: 'Proposal', probability: 30 },
  { name: 'Negotiation', probability: 60 },
  { name: 'Closed Won', probability: 100 },
  { name: 'Closed Lost', probability: 0 },
] as const;

export const FALLBACK_STAGES = ['Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'] as const;

export const STAGE_COLORS_MAP: Record<string, { bar: string; accent: string; bg: string; border: string }> = {
  Qualification: { bar: '#3b82f6', accent: 'text-blue-600', bg: 'bg-blue-500/10', border: 'border-l-blue-500' },
  Proposal: { bar: '#f59e0b', accent: 'text-amber-600', bg: 'bg-amber-500/10', border: 'border-l-amber-500' },
  Negotiation: { bar: '#8b5cf6', accent: 'text-violet-600', bg: 'bg-violet-500/10', border: 'border-l-violet-500' },
  'Closed Won': { bar: '#10b981', accent: 'text-emerald-600', bg: 'bg-emerald-500/10', border: 'border-l-emerald-500' },
  'Closed Lost': { bar: '#64748b', accent: 'text-slate-500', bg: 'bg-slate-500/10', border: 'border-l-slate-400' },
};

export const STAGE_ICONS: Record<string, typeof Briefcase> = {
  Qualification: Target,
  Proposal: Briefcase,
  Negotiation: Handshake,
  'Closed Won': Trophy,
  'Closed Lost': XCircle,
};

export const DEAL_CARD_TYPE = 'DEAL_CARD';

export const PROBABILITY_COLORS = {
  low: { bg: 'bg-red-100', text: 'text-red-700' },
  medium: { bg: 'bg-amber-100', text: 'text-amber-700' },
  high: { bg: 'bg-emerald-100', text: 'text-emerald-700' },
} as const;

export function getProbabilityColor(probability: number) {
  if (probability >= 70) return PROBABILITY_COLORS.high;
  if (probability >= 30) return PROBABILITY_COLORS.medium;
  return PROBABILITY_COLORS.low;
}
