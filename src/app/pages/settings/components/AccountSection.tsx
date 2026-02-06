import { Link } from 'react-router-dom';
import { Settings2, DollarSign, Clock, Download, RefreshCw, LogOut, Trash2 } from 'lucide-react';
import type { UserSettings, UpdateUserSettingsRequest, CurrencyOption } from '@/app/api/types';

export interface AccountSectionProps {
  settings: UserSettings;
  updateSettings: (updates: UpdateUserSettingsRequest) => void;
  currencies: CurrencyOption[];
  showDeleteConfirm: boolean;
  setShowDeleteConfirm: (val: boolean) => void;
  deleteConfirmText: string;
  setDeleteConfirmText: (val: string) => void;
  deleteConfirmChecked: boolean;
  setDeleteConfirmChecked: (val: boolean) => void;
  canConfirmDelete: boolean;
  handleDeleteAccount: () => void;
  handleReset: () => void;
}

export function AccountSection({
  settings,
  updateSettings,
  currencies,
  showDeleteConfirm,
  setShowDeleteConfirm,
  deleteConfirmText,
  setDeleteConfirmText,
  deleteConfirmChecked,
  setDeleteConfirmChecked,
  canConfirmDelete,
  handleDeleteAccount,
  handleReset,
}: AccountSectionProps) {
  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Settings2 className="w-5 h-5 text-orange-600" />
          Account & Data
        </h2>
        <p className="text-slate-600 text-sm mt-1">Manage your account settings and data</p>
      </div>

      {/* Defaults */}
      <div>
        <h3 className="font-semibold text-slate-900 mb-4">Default Settings</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="defaultCurrency" className="block text-sm font-medium text-slate-700 mb-2">
              <DollarSign className="w-4 h-4 inline mr-1" />
              Default Currency
            </label>
            <select
              id="defaultCurrency"
              value={settings.defaultCurrency || 'USD'}
              onChange={(e) => updateSettings({ defaultCurrency: e.target.value })}
              className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
            >
              {currencies.map((c) => (
                <option key={c.code} value={c.code}>{c.symbol} {c.code} - {c.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="followUpDays" className="block text-sm font-medium text-slate-700 mb-2">
              <Clock className="w-4 h-4 inline mr-1" />
              Default Follow-up Days
            </label>
            <input
              type="number"
              id="followUpDays"
              min={1}
              max={30}
              value={settings.defaultFollowUpDays}
              onChange={(e) => updateSettings({ defaultFollowUpDays: parseInt(e.target.value, 10) || 3 })}
              className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none"
            />
          </div>
        </div>
      </div>

      {/* Privacy */}
      <div className="pt-6 border-t border-slate-200">
        <h3 className="font-semibold text-slate-900 mb-4">Privacy</h3>
        <div className="space-y-3">
          <label className="flex items-center justify-between p-4 bg-slate-50 rounded-xl cursor-pointer hover:bg-slate-100 transition-colors">
            <div>
              <p className="font-medium text-slate-900">Show Activity Status</p>
              <p className="text-sm text-slate-500">Let others see when you're active</p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={settings.showActivityStatus}
              onClick={() => updateSettings({ showActivityStatus: !settings.showActivityStatus })}
              className={`relative w-12 h-7 rounded-full transition-colors ${
                settings.showActivityStatus ? 'bg-orange-600' : 'bg-slate-300'
              }`}
            >
              <span className={`absolute top-0.5 left-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform ${
                settings.showActivityStatus ? 'translate-x-5' : ''
              }`} />
            </button>
          </label>

          <label className="flex items-center justify-between p-4 bg-slate-50 rounded-xl cursor-pointer hover:bg-slate-100 transition-colors">
            <div>
              <p className="font-medium text-slate-900">Usage Analytics</p>
              <p className="text-sm text-slate-500">Help us improve by sharing anonymous usage data</p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={settings.allowAnalytics}
              onClick={() => updateSettings({ allowAnalytics: !settings.allowAnalytics })}
              className={`relative w-12 h-7 rounded-full transition-colors ${
                settings.allowAnalytics ? 'bg-orange-600' : 'bg-slate-300'
              }`}
            >
              <span className={`absolute top-0.5 left-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform ${
                settings.allowAnalytics ? 'translate-x-5' : ''
              }`} />
            </button>
          </label>
        </div>
      </div>

      {/* Data Management */}
      <div className="pt-6 border-t border-slate-200">
        <h3 className="font-semibold text-slate-900 mb-4">Data Management</h3>
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            className="h-11 px-5 border-2 border-slate-200 rounded-xl text-slate-700 font-medium hover:bg-slate-50 flex items-center gap-2 transition-colors"
          >
            <Download className="w-4 h-4" />
            Export Data
          </button>
          <button
            type="button"
            onClick={handleReset}
            className="h-11 px-5 border-2 border-slate-200 rounded-xl text-slate-700 font-medium hover:bg-slate-50 flex items-center gap-2 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Reset Settings
          </button>
        </div>
      </div>

      {/* Account Actions */}
      <div className="pt-6 border-t border-slate-200 space-y-4">
        <h3 className="font-semibold text-slate-900">Account Actions</h3>
        
        <Link
          to="/login"
          className="w-full flex items-center justify-center gap-2 h-11 border-2 border-slate-200 rounded-xl hover:border-slate-300 transition-colors font-medium text-slate-700"
        >
          <LogOut className="w-5 h-5" />
          Logout
        </Link>

        {showDeleteConfirm ? (
          <div className="p-5 border-2 border-red-200 rounded-xl bg-red-50/50 space-y-4">
            <p className="text-sm text-slate-700 font-medium">To permanently delete your account:</p>
            <label className="flex items-start gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={deleteConfirmChecked}
                onChange={(e) => setDeleteConfirmChecked(e.target.checked)}
                className="mt-1 rounded border-slate-300 text-red-600 focus:ring-red-500"
              />
              <span className="text-sm text-slate-700">I understand all my data will be permanently deleted and this cannot be undone.</span>
            </label>
            <input
              type="text"
              value={deleteConfirmText}
              onChange={(e) => setDeleteConfirmText(e.target.value)}
              placeholder='Type "DELETE" to confirm'
              className="w-full h-11 px-4 border border-slate-300 rounded-xl"
            />
            <div className="flex gap-3">
              <button
                type="button"
                onClick={() => { setShowDeleteConfirm(false); setDeleteConfirmText(''); setDeleteConfirmChecked(false); }}
                className="flex-1 h-11 border-2 border-slate-300 rounded-xl font-medium text-slate-700 hover:bg-slate-100"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleDeleteAccount}
                disabled={!canConfirmDelete}
                className="flex-1 h-11 bg-red-600 hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-semibold rounded-xl"
              >
                Delete Account
              </button>
            </div>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => setShowDeleteConfirm(true)}
            className="w-full flex items-center justify-center gap-2 h-11 border-2 border-red-200 text-red-600 hover:border-red-300 hover:bg-red-50 rounded-xl font-medium transition-colors"
          >
            <Trash2 className="w-5 h-5" />
            Delete Account
          </button>
        )}

        <Link to="/help" className="block text-center text-sm text-orange-600 hover:text-orange-700 font-medium pt-2">
          Need help?
        </Link>
      </div>
    </div>
  );
}
