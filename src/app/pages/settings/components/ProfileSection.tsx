import { User, Clock, Globe } from 'lucide-react';
import type { UserSettings, UpdateUserSettingsRequest, TimezoneOption, LanguageOption } from '@/app/api/types';

export interface ProfileSectionProps {
  settings: UserSettings;
  updateSettings: (updates: UpdateUserSettingsRequest) => void;
  timezones: TimezoneOption[];
  languages: LanguageOption[];
}

export function ProfileSection({
  settings,
  updateSettings,
  timezones,
  languages,
}: ProfileSectionProps) {
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
          {settings.companyName?.charAt(0) || 'U'}
        </div>
        <div>
          <p className="font-medium text-slate-900">Profile Photo</p>
          <p className="text-sm text-slate-500 mb-2">JPG, PNG or GIF. Max size 2MB.</p>
          <button className="text-sm text-orange-600 hover:text-orange-700 font-medium">
            Upload photo
          </button>
        </div>
      </div>

      <div className="grid gap-6 sm:grid-cols-2">
        <div>
          <label htmlFor="companyName" className="block text-sm font-medium text-slate-700 mb-2">
            Company Name
          </label>
          <input
            type="text"
            id="companyName"
            value={settings.companyName}
            onChange={(e) => updateSettings({ companyName: e.target.value })}
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
        </div>

        <div>
          <label htmlFor="language" className="block text-sm font-medium text-slate-700 mb-2">
            <Globe className="w-4 h-4 inline mr-1" />
            Language
          </label>
          <select
            id="language"
            value={settings.language}
            onChange={(e) => updateSettings({ language: e.target.value })}
            className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
          >
            {languages.map((lang) => (
              <option key={lang.code} value={lang.code}>{lang.name} ({lang.nativeName})</option>
            ))}
          </select>
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
        <p className="text-xs text-slate-400 mt-1">{settings.bio?.length || 0}/500 characters</p>
      </div>
    </div>
  );
}
