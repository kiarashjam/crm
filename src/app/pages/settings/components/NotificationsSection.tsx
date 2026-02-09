import { Bell, Mail } from 'lucide-react';
import type { UserSettings, UpdateUserSettingsRequest } from '@/app/api/types';

type ToggleSwitchProps = {
  checked: boolean;
  onChange: (val: boolean) => void;
  label: string;
  description: string;
};

function ToggleSwitch({ checked, onChange, label, description }: ToggleSwitchProps) {
  return (
    <label className="flex items-center justify-between p-4 bg-slate-50 rounded-xl cursor-pointer hover:bg-slate-100 transition-colors">
      <div className="flex-1">
        <div className="flex items-center gap-2">
          <p className="font-medium text-slate-900">{label}</p>
          <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 border border-amber-200 px-2 py-0.5 text-[10px] font-medium text-amber-600">
            <span className="w-1 h-1 rounded-full bg-amber-400 animate-pulse" />
            Coming soon
          </span>
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
          Coming soon — notification delivery is being set up. Your preferences are saved and will take effect once the backend service is live.
        </div>
      </div>

      {/* Email Notifications */}
      <div>
        <div className="flex items-center gap-2 mb-4">
          <Mail className="w-5 h-5 text-slate-400" />
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

      {/* In-App Notifications */}
      <div className="pt-6 border-t border-slate-200">
        <div className="flex items-center gap-2 mb-4">
          <Bell className="w-5 h-5 text-slate-400" />
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
