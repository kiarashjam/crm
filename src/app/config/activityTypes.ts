import { 
  Phone, 
  Mail, 
  FileText, 
  Video, 
  Presentation, 
  CheckCircle, 
  Users,
  CalendarClock,
  Clock,
  LucideIcon
} from 'lucide-react';

export interface ActivityTypeConfig {
  id: string;
  label: string;
  icon: LucideIcon;
  color: string;
  bgColor: string;
  textColor: string;
  borderColor: string;
}

/**
 * Activity type configuration
 * Used in Activities page and activity-related components
 */
export const ACTIVITY_TYPES: readonly ActivityTypeConfig[] = [
  { 
    id: 'call', 
    label: 'Call', 
    icon: Phone, 
    color: 'emerald', 
    bgColor: 'bg-emerald-100', 
    textColor: 'text-emerald-600', 
    borderColor: 'border-emerald-200' 
  },
  { 
    id: 'meeting', 
    label: 'Meeting', 
    icon: Users, 
    color: 'blue', 
    bgColor: 'bg-blue-100', 
    textColor: 'text-blue-600', 
    borderColor: 'border-blue-200' 
  },
  { 
    id: 'email', 
    label: 'Email', 
    icon: Mail, 
    color: 'amber', 
    bgColor: 'bg-amber-100', 
    textColor: 'text-amber-600', 
    borderColor: 'border-amber-200' 
  },
  { 
    id: 'note', 
    label: 'Note', 
    icon: FileText, 
    color: 'slate', 
    bgColor: 'bg-slate-100', 
    textColor: 'text-slate-600', 
    borderColor: 'border-slate-200' 
  },
  { 
    id: 'video', 
    label: 'Video Call', 
    icon: Video, 
    color: 'purple', 
    bgColor: 'bg-purple-100', 
    textColor: 'text-purple-600', 
    borderColor: 'border-purple-200' 
  },
  { 
    id: 'demo', 
    label: 'Demo', 
    icon: Presentation, 
    color: 'rose', 
    bgColor: 'bg-rose-100', 
    textColor: 'text-rose-600', 
    borderColor: 'border-rose-200' 
  },
  { 
    id: 'task', 
    label: 'Task Completed', 
    icon: CheckCircle, 
    color: 'cyan', 
    bgColor: 'bg-cyan-100', 
    textColor: 'text-cyan-600', 
    borderColor: 'border-cyan-200' 
  },
  { 
    id: 'follow_up', 
    label: 'Follow-up', 
    icon: CalendarClock, 
    color: 'orange', 
    bgColor: 'bg-orange-100', 
    textColor: 'text-orange-600', 
    borderColor: 'border-orange-200' 
  },
  { 
    id: 'deadline', 
    label: 'Deadline', 
    icon: Clock, 
    color: 'red', 
    bgColor: 'bg-red-100', 
    textColor: 'text-red-600', 
    borderColor: 'border-red-200' 
  },
] as const;

/**
 * Get activity type config by ID
 */
export function getActivityType(id: string): ActivityTypeConfig | undefined {
  return ACTIVITY_TYPES.find(type => type.id === id);
}

/**
 * Get activity type icon component
 */
export function getActivityTypeIcon(id: string): LucideIcon {
  return getActivityType(id)?.icon ?? FileText;
}
