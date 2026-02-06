import type { UserSettings } from '@/app/api/types';

export type SettingsTab = 
  | 'profile' 
  | 'company' 
  | 'integrations' 
  | 'notifications' 
  | 'appearance' 
  | 'security'
  | 'lead-statuses'
  | 'lead-sources'
  | 'pipelines';

export interface TabConfig {
  id: SettingsTab;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  description: string;
}

// Re-export
export type { UserSettings };
