import { Phone, Mail, FileText, Video, Users, Presentation, CheckCircle } from 'lucide-react';

export const ACTIVITY_TYPES = [
  { id: 'call', label: 'Call', icon: Phone, color: 'emerald', bgColor: 'bg-emerald-100', textColor: 'text-emerald-700', borderColor: 'border-emerald-200' },
  { id: 'meeting', label: 'Meeting', icon: Users, color: 'blue', bgColor: 'bg-blue-100', textColor: 'text-blue-700', borderColor: 'border-blue-200' },
  { id: 'email', label: 'Email', icon: Mail, color: 'amber', bgColor: 'bg-amber-100', textColor: 'text-amber-700', borderColor: 'border-amber-200' },
  { id: 'note', label: 'Note', icon: FileText, color: 'slate', bgColor: 'bg-slate-100', textColor: 'text-slate-600', borderColor: 'border-slate-200' },
  { id: 'video', label: 'Video Call', icon: Video, color: 'purple', bgColor: 'bg-purple-100', textColor: 'text-purple-700', borderColor: 'border-purple-200' },
  { id: 'demo', label: 'Demo', icon: Presentation, color: 'rose', bgColor: 'bg-rose-100', textColor: 'text-rose-700', borderColor: 'border-rose-200' },
  { id: 'task', label: 'Task Completed', icon: CheckCircle, color: 'cyan', bgColor: 'bg-cyan-100', textColor: 'text-cyan-700', borderColor: 'border-cyan-200' },
] as const;

export type ActivityTypeId = typeof ACTIVITY_TYPES[number]['id'];
