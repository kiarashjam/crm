import { Sparkles, FileText, MessageSquare, Zap, Check } from 'lucide-react';
import type { UserSettings, UpdateUserSettingsRequest, BrandToneType } from '@/app/api/types';

export interface BrandSectionProps {
  settings: UserSettings;
  updateSettings: (updates: UpdateUserSettingsRequest) => void;
}

const BRAND_TONES: { value: BrandToneType; label: string; desc: string; icon: React.ElementType }[] = [
  { value: 'professional', label: 'Professional', desc: 'Formal, business-focused communication', icon: FileText },
  { value: 'friendly', label: 'Friendly', desc: 'Warm, approachable, and conversational', icon: MessageSquare },
  { value: 'persuasive', label: 'Persuasive', desc: 'Compelling, action-oriented messaging', icon: Zap },
];

export function BrandSection({ settings, updateSettings }: BrandSectionProps) {
  return (
    <div className="p-6 space-y-8">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <Sparkles className="w-5 h-5 text-orange-600" />
          Brand & AI Settings
        </h2>
        <p className="text-slate-600 text-sm mt-1">Configure how AI generates copy for your brand</p>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700 mb-3">Brand Tone</label>
        <div className="grid gap-3 sm:grid-cols-3">
          {BRAND_TONES.map((opt) => {
            const Icon = opt.icon;
            const isSelected = settings.brandTone === opt.value;
            return (
              <label
                key={opt.value}
                className={`relative flex flex-col p-4 border-2 rounded-xl cursor-pointer transition-all ${
                  isSelected
                    ? 'border-orange-500 bg-orange-50 shadow-sm'
                    : 'border-slate-200 hover:border-orange-200 hover:bg-slate-50'
                }`}
              >
                <input
                  type="radio"
                  name="brandTone"
                  value={opt.value}
                  checked={isSelected}
                  onChange={() => updateSettings({ brandTone: opt.value })}
                  className="sr-only"
                />
                <Icon className={`w-6 h-6 mb-2 ${isSelected ? 'text-orange-600' : 'text-slate-400'}`} />
                <p className={`font-medium ${isSelected ? 'text-orange-700' : 'text-slate-900'}`}>{opt.label}</p>
                <p className={`text-xs mt-1 ${isSelected ? 'text-orange-600/70' : 'text-slate-500'}`}>{opt.desc}</p>
                {isSelected && (
                  <div className="absolute top-3 right-3 w-5 h-5 bg-orange-600 rounded-full flex items-center justify-center">
                    <Check className="w-3 h-3 text-white" />
                  </div>
                )}
              </label>
            );
          })}
        </div>
      </div>

      <div>
        <label htmlFor="emailSubjectPrefix" className="block text-sm font-medium text-slate-700 mb-2">
          Default Email Subject Prefix
        </label>
        <input
          type="text"
          id="emailSubjectPrefix"
          value={settings.defaultEmailSubjectPrefix || ''}
          onChange={(e) => updateSettings({ defaultEmailSubjectPrefix: e.target.value })}
          placeholder="e.g. [Acme Corp]"
          className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
        />
        <p className="text-xs text-slate-500 mt-1">Added to the beginning of generated email subjects</p>
      </div>

      <div>
        <label htmlFor="emailSignature" className="block text-sm font-medium text-slate-700 mb-2">
          Email Signature
        </label>
        <textarea
          id="emailSignature"
          value={settings.emailSignature || ''}
          onChange={(e) => updateSettings({ emailSignature: e.target.value })}
          placeholder="Best regards,&#10;John Doe&#10;Sales Manager | Acme Corp"
          rows={4}
          className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none resize-none font-mono text-sm transition-colors"
        />
        <p className="text-xs text-slate-500 mt-1">Automatically appended to generated emails</p>
      </div>
    </div>
  );
}
