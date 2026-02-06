import { Crown, UserCircle, ShieldCheck } from 'lucide-react';

// Role mapping with icons and styles
export const ROLES = {
  0: { label: 'Owner', icon: Crown, color: 'text-amber-600', bg: 'bg-amber-100', bgGradient: 'from-amber-500 to-orange-500' },
  1: { label: 'Member', icon: UserCircle, color: 'text-slate-600', bg: 'bg-slate-100', bgGradient: 'from-slate-500 to-slate-600' },
  2: { label: 'Manager', icon: ShieldCheck, color: 'text-blue-600', bg: 'bg-blue-100', bgGradient: 'from-blue-500 to-indigo-500' },
} as const;

export function getRoleInfo(role: number) {
  return ROLES[role as keyof typeof ROLES] || ROLES[1];
}

export const ROLE_COLORS = {
  Owner: { bg: 'bg-amber-100', text: 'text-amber-700', border: 'border-amber-200' },
  Admin: { bg: 'bg-purple-100', text: 'text-purple-700', border: 'border-purple-200' },
  Member: { bg: 'bg-slate-100', text: 'text-slate-700', border: 'border-slate-200' },
} as const;

export const REQUEST_STATUS_COLORS = {
  Pending: { bg: 'bg-amber-100', text: 'text-amber-700' },
  Approved: { bg: 'bg-emerald-100', text: 'text-emerald-700' },
  Rejected: { bg: 'bg-red-100', text: 'text-red-700' },
} as const;
