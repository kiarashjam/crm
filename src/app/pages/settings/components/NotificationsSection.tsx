import { BarChart3, Bell, Mail, Sun } from 'lucide-react';
import type { UserSettings, UpdateUserSettingsRequest } from '@/app/api/types';

type ToggleSwitchProps = {
  checked: boolean;
  onChange: (val: boolean) => void;
  label: string;
  description: string;
  /** Marks a switch whose delivery is not built yet. A switch that works says nothing. */
  comingSoon?: boolean;
};

function ToggleSwitch({ checked, onChange, label, description, comingSoon = true }: ToggleSwitchProps) {
  return (
    <label className="flex items-center justify-between p-4 bg-slate-50 rounded-xl cursor-pointer hover:bg-slate-100 transition-colors">
      <div className="flex-1">
        <div className="flex items-center gap-2">
          <p className="font-medium text-slate-900">{label}</p>
          {comingSoon && (
            <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 border border-amber-200 px-2 py-0.5 text-[10px] font-medium text-amber-700">
              <span className="w-1 h-1 rounded-full bg-amber-400 animate-pulse" />
              Coming soon
            </span>
          )}
        </div>
        <p className="text-sm text-slate-500">{description}</p>
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative w-12 h-7 rounded-full transition-colors ${checked ? 'bg-orange-600' : 'bg-slate-300'}`}
      >
        <span className={`absolute top-0.5 left-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform ${checked ? 'translate-x-5' : ''}`} />
      </button>
    </label>
  );
}

export type NotificationsSectionProps = {
  settings: UserSettings;
  updateSettings: (updates: UpdateUserSettingsRequest) => void;
};

export function NotificationsSection({
  settings,
  updateSettings,
}: NotificationsSectionProps) {
  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Bell className="w-5 h-5 text-orange-600" />
          Notifications
        </h2>
        <p className="text-slate-600 text-sm mt-1">Manage how you receive notifications</p>
        <div className="mt-2 inline-flex items-center gap-1.5 rounded-md bg-amber-50 border border-amber-200 px-3 py-1.5 text-xs font-medium text-amber-700">
          <span className="w-1.5 h-1.5 rounded-full bg-amber-400 animate-pulse" />
          Switches marked coming soon are saved, but nothing is sent for them yet. The morning brief and the weekly report digest are live.
        </div>
      </div>

      {/* Email Notifications */}
      <div>
        <div className="flex items-center gap-2 mb-4">
          <Mail className="w-5 h-5 text-slate-500" />
          <h3 className="font-semibold text-slate-900">Email Notifications</h3>
        </div>
        <div className="space-y-3">
          <ToggleSwitch
            checked={settings.emailNotificationsEnabled}
            onChange={(val) => updateSettings({ emailNotificationsEnabled: val })}
            label="Enable Email Notifications"
            description="Receive notifications via email"
          />
          {settings.emailNotificationsEnabled && (
            <div className="pl-4 border-l-2 border-orange-200 space-y-3">
              <ToggleSwitch
                checked={settings.emailOnNewLead}
                onChange={(val) => updateSettings({ emailOnNewLead: val })}
                label="New Lead Alerts"
                description="When a new lead is assigned to you"
              />
              <ToggleSwitch
                checked={settings.emailOnDealUpdate}
                onChange={(val) => updateSettings({ emailOnDealUpdate: val })}
                label="Deal Updates"
                description="When deals you're assigned to are updated"
              />
              <ToggleSwitch
                checked={settings.emailOnTaskDue}
                onChange={(val) => updateSettings({ emailOnTaskDue: val })}
                label="Task Reminders"
                description="When tasks are due soon"
              />
              <ToggleSwitch
                checked={settings.emailOnTeamMention}
                onChange={(val) => updateSettings({ emailOnTeamMention: val })}
                label="Team Mentions"
                description="When someone mentions you"
              />
            </div>
          )}
        </div>
      </div>

      {/* The morning brief: sent for real, as a notification and, when asked for, by email. */}
      <div className="pt-6 border-t border-slate-200">
        <div className="flex items-center gap-2 mb-4">
          <Sun className="w-5 h-5 text-slate-500" />
          <h3 className="font-semibold text-slate-900">Morning brief</h3>
        </div>
        <div className="space-y-3">
          <ToggleSwitch
            comingSoon={false}
            checked={settings.morningBriefEnabled !== false}
            onChange={(val) => updateSettings({ morningBriefEnabled: val })}
            label="Morning brief"
            description="From 7:00 on your workspace's calendar, a notification with your tasks due, your deals gone quiet and, as your role allows, members past due, today's bookings and events, and renewals coming up. Nothing to say, nothing sent."
          />
          {settings.morningBriefEnabled !== false && (
            <div className="pl-4 border-l-2 border-orange-200">
              <ToggleSwitch
                comingSoon={false}
                checked={settings.morningBriefEmail === true}
                onChange={(val) => updateSettings({ morningBriefEmail: val })}
                label="Also by email"
                description="The same brief to your own address."
              />
            </div>
          )}
        </div>
      </div>

      {/* Reports digest: delivered for real, unlike the switches marked coming soon. */}
      <div className="pt-6 border-t border-slate-200">
        <div className="flex items-center gap-2 mb-4">
          <BarChart3 className="w-5 h-5 text-slate-500" />
          <h3 className="font-semibold text-slate-900">Reports</h3>
        </div>
        <ToggleSwitch
          comingSoon={false}
          checked={settings.weeklyDigestEnabled === true}
          onChange={(val) => updateSettings({ weeklyDigestEnabled: val })}
          label="Weekly report digest"
          description="Every Monday from 7:00 in your time zone (set under Profile), an email with last week's won, lost and new deals, the open pipeline, this month's forecast, leads and activity — for each workspace you own or manage."
        />
      </div>

      {/* In-App Notifications */}
      <div className="pt-6 border-t border-slate-200">
        <div className="flex items-center gap-2 mb-4">
          <Bell className="w-5 h-5 text-slate-500" />
          <h3 className="font-semibold text-slate-900">In-App Notifications</h3>
        </div>
        <div className="space-y-3">
          <ToggleSwitch
            checked={settings.inAppNotificationsEnabled}
            onChange={(val) => updateSettings({ inAppNotificationsEnabled: val })}
            label="Enable In-App Notifications"
            description="Show notifications within the app"
          />
          <ToggleSwitch
            checked={settings.inAppSoundEnabled}
            onChange={(val) => updateSettings({ inAppSoundEnabled: val })}
            label="Notification Sounds"
            description="Play a sound for new notifications"
          />
          <ToggleSwitch
            checked={settings.browserNotificationsEnabled}
            onChange={(val) => updateSettings({ browserNotificationsEnabled: val })}
            label="Browser Notifications"
            description="Show desktop notifications (requires permission)"
          />
        </div>
      </div>
    </div>
  );
}
