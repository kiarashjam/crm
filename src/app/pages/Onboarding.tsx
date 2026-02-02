import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Sparkles } from 'lucide-react';
import { toast } from 'sonner';
import { saveUserSettings } from '@/app/api';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import DemoBanner from '@/app/components/DemoBanner';

export default function Onboarding() {
  const [companyName, setCompanyName] = useState('');
  const [brandTone, setBrandTone] = useState<'professional' | 'friendly' | 'persuasive'>('professional');
  const [saving, setSaving] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await saveUserSettings({ companyName: companyName.trim() || 'My Company', brandTone });
      toast.success(messages.settings.saved);
      navigate('/dashboard');
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50/80 to-slate-50 flex flex-col items-center justify-center px-[var(--page-padding)] py-12">
      <DemoBanner />
      <main id={MAIN_CONTENT_ID} className="w-full max-w-md flex-1 flex flex-col justify-center" tabIndex={-1}>
        <div className="text-center mb-8">
          <div className="w-16 h-16 bg-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-lg" aria-hidden>
            <Sparkles className="w-10 h-10 text-white" />
          </div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Set your brand tone</h1>
          <p className="text-slate-600">This helps AI generate copy that matches your voice</p>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
          <div className="mb-6">
            <label htmlFor="company" className="block text-sm font-medium text-slate-700 mb-2">
              Company Name
            </label>
            <input
              type="text"
              id="company"
              value={companyName}
              onChange={(e) => setCompanyName(e.target.value)}
              placeholder="Enter your company name"
              className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
              required
              autoComplete="organization"
            />
          </div>

          <div className="mb-8">
            <label className="block text-sm font-medium text-slate-700 mb-3">
              Brand Tone
            </label>
            <div className="space-y-3" role="radiogroup" aria-label="Brand tone">
              {[
                { value: 'professional' as const, label: 'Professional', desc: 'Formal, business-focused communication', example: 'e.g. "We are pleased to inform you…"' },
                { value: 'friendly' as const, label: 'Friendly', desc: 'Warm, approachable, and conversational', example: 'e.g. "Hey! Hope you\'re having a great day…"' },
                { value: 'persuasive' as const, label: 'Persuasive', desc: 'Compelling, action-oriented messaging', example: 'e.g. "Consider this: you could save 40%…"' },
              ].map((opt) => (
                <label
                  key={opt.value}
                  className="flex items-start p-4 border-2 border-slate-200 rounded-xl cursor-pointer hover:border-orange-200 transition-colors focus-within:ring-2 focus-within:ring-orange-500 focus-within:ring-offset-2"
                >
                  <input
                    type="radio"
                    name="tone"
                    value={opt.value}
                    checked={brandTone === opt.value}
                    onChange={() => setBrandTone(opt.value)}
                    className="w-4 h-4 mt-0.5 text-orange-600 focus:ring-0"
                  />
                  <div className="ml-3">
                    <p className="font-medium text-slate-900">{opt.label}</p>
                    <p className="text-sm text-slate-600">{opt.desc}</p>
                    <p className="text-xs text-slate-500 mt-1 italic">{opt.example}</p>
                  </div>
                </label>
              ))}
            </div>
          </div>

          <button
            type="submit"
            disabled={saving}
            className="w-full h-12 bg-orange-600 hover:bg-orange-500 disabled:opacity-70 text-white font-semibold rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
          >
            {saving ? 'Saving...' : 'Save & Continue'}
          </button>

          <p className="text-center text-sm text-slate-500 mt-4">
            You can change these settings anytime
          </p>
        </form>
      </main>
    </div>
  );
}
