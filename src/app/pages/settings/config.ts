import { User, Building2, Plug, Bell, Palette, Shield, Tag, Sparkles, Workflow } from 'lucide-react';
import type { TabConfig } from './types';

export const SETTINGS_TABS: TabConfig[] = [
  { id: 'profile', label: 'Profile', icon: User, description: 'Your personal information' },
  { id: 'company', label: 'Company', icon: Building2, description: 'Company branding and details' },
  { id: 'lead-statuses', label: 'Lead Statuses', icon: Tag, description: 'Customize lead status options' },
  { id: 'lead-sources', label: 'Lead Sources', icon: Sparkles, description: 'Manage lead source tracking' },
  { id: 'pipelines', label: 'Pipelines', icon: Workflow, description: 'Configure sales pipelines' },
  { id: 'integrations', label: 'Integrations', icon: Plug, description: 'Connect external services' },
  { id: 'notifications', label: 'Notifications', icon: Bell, description: 'Email and alert preferences' },
  { id: 'appearance', label: 'Appearance', icon: Palette, description: 'Theme and display settings' },
  { id: 'security', label: 'Security', icon: Shield, description: 'Password and 2FA settings' },
];

export const BRAND_TONES = [
  { value: 'Professional', label: 'Professional', description: 'Formal and business-appropriate' },
  { value: 'Friendly', label: 'Friendly', description: 'Warm and approachable' },
  { value: 'Casual', label: 'Casual', description: 'Relaxed and conversational' },
  { value: 'Formal', label: 'Formal', description: 'Highly professional and traditional' },
  { value: 'Enthusiastic', label: 'Enthusiastic', description: 'Energetic and passionate' },
] as const;

export const THEMES = [
  { value: 'system', label: 'System', description: 'Match your device settings' },
  { value: 'light', label: 'Light', description: 'Light mode' },
  { value: 'dark', label: 'Dark', description: 'Dark mode' },
] as const;

export const DIGEST_FREQUENCIES = [
  { value: 'daily', label: 'Daily' },
  { value: 'weekly', label: 'Weekly' },
  { value: 'never', label: 'Never' },
] as const;
