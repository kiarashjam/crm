import { User, Clock } from 'lucide-react';
import type { UserSettings, UpdateUserSettingsRequest, TimezoneOption } from '@/app/api/types';
import { useRegional } from '@/app/hooks/useMoney';
import { useT } from '@/app/i18n';
import { LanguageSwitcher } from '@/app/i18n/LanguageSwitcher';

export interface ProfileSectionProps {
  settings: UserSettings;
  updateSettings: (updates: UpdateUserSettingsRequest) => void;
  timezones: TimezoneOption[];
}

export function ProfileSection({
  settings,
  updateSettings,
  timezones,
}: ProfileSectionProps) {
  const workspace = useRegional();
  const { t } = useT();
  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <User className="w-5 h-5 text-orange-600" />
          Profile
        </h2>
        <p className="text-slate-600 text-sm mt-1">Your personal information and preferences</p>
      </div>

      {/* Avatar placeholder */}
      <div className="flex items-center gap-6">
        <div className="w-20 h-20 rounded-full bg-gradient-to-br from-orange-400 to-orange-600 flex items-center justify-center text-white text-2xl font-bold">
          {settings.brandName?.charAt(0) || 'U'}
        </div>
        <div>
          <p className="font-medium text-slate-900">Profile Photo</p>
          <p className="text-sm text-slate-500 mb-2">JPG, PNG or GIF. Max size 2MB.</p>
          <button className="text-sm text-orange-700 hover:text-orange-800 font-medium">
            Upload photo
          </button>
        </div>
      </div>

      <div className="grid gap-6 sm:grid-cols-2">
        <div>
          <label htmlFor="brandName" className="block text-sm font-medium text-slate-700 mb-2">
            Brand Name
          </label>
          <input
            type="text"
            id="brandName"
            value={settings.brandName}
            onChange={(e) => updateSettings({ brandName: e.target.value })}
            className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
          />
        </div>

        <div>
          <label htmlFor="jobTitle" className="block text-sm font-medium text-slate-700 mb-2">
            Job Title
          </label>
          <input
            type="text"
            id="jobTitle"
            value={settings.jobTitle || ''}
            onChange={(e) => updateSettings({ jobTitle: e.target.value })}
            placeholder="e.g. Sales Manager"
            className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
          />
        </div>

        <div>
          <label htmlFor="phone" className="block text-sm font-medium text-slate-700 mb-2">
            Phone Number
          </label>
          <input
            type="tel"
            id="phone"
            value={settings.phone || ''}
            onChange={(e) => updateSettings({ phone: e.target.value })}
            placeholder="+1 (555) 123-4567"
            className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
          />
        </div>

        <div>
          <label htmlFor="timezone" className="block text-sm font-medium text-slate-700 mb-2">
            <Clock className="w-4 h-4 inline mr-1" />
            Timezone
          </label>
          <select
            id="timezone"
            value={settings.timezone}
            onChange={(e) => updateSettings({ timezone: e.target.value })}
            className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
          >
            {timezones.map((tz) => (
              <option key={tz.id} value={tz.id}>{tz.displayName}</option>
            ))}
          </select>
          {/* This zone times the weekly report digest, as Notifications says. Without saying
              which zone does what, it looked as if reports counted days in it; they count
              them in the workspace's zone. */}
          <p className="mt-1.5 text-xs text-slate-500">
            Your weekly report digest goes out on Monday from 7:00 in this time zone. Reports, forecasts and
            the activity lists count days in the workspace's time zone,{' '}
            {workspace.timeZone.replace(/_/g, ' ')}.
          </p>
        </div>

        {/* The language the app is in, applied at once and kept on the account. It replaces a
            select that offered sixteen languages, saved one, and changed nothing. */}
        <div>
          <LanguageSwitcher
            withAutomatic
            showLabel
            className="flex flex-wrap items-center gap-2 text-sm text-slate-600 [&_select]:h-11 [&_select]:rounded-xl [&_select]:px-4"
          />
          <p className="mt-1.5 text-xs text-slate-500">{t('language.automaticHint')}</p>
        </div>
      </div>

      <div>
        <label htmlFor="bio" className="block text-sm font-medium text-slate-700 mb-2">
          Bio
        </label>
        <textarea
          id="bio"
          value={settings.bio || ''}
          onChange={(e) => updateSettings({ bio: e.target.value })}
          placeholder="Tell us a bit about yourself..."
          rows={3}
          maxLength={500}
          className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none resize-none transition-colors"
        />
        <p className="text-xs text-slate-500 mt-1">{settings.bio?.length || 0}/500 characters</p>
      </div>
    </div>
  );
}
