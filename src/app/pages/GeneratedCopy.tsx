import { useState, useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Copy, RotateCcw, Send } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

const DEFAULT_COPY = `Hi [First Name],

I hope this email finds you well! I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

We've helped companies similar to yours:
• Increase productivity by 40%
• Reduce operational costs by 25%
• Streamline workflows and save time

Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

Looking forward to connecting!

Best regards,
[Your Name]`;

export default function GeneratedCopy() {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as { copy?: string; copyTypeLabel?: string } | null;
  const [generatedText, setGeneratedText] = useState(state?.copy ?? DEFAULT_COPY);
  const [copyTypeLabel, setCopyTypeLabel] = useState(state?.copyTypeLabel ?? 'Copy');
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (state?.copy) setGeneratedText(state.copy);
    if (state?.copyTypeLabel) setCopyTypeLabel(state.copyTypeLabel);
  }, [state?.copy, state?.copyTypeLabel]);

  const handleCopy = () => {
    try {
      navigator.clipboard.writeText(generatedText);
      setCopied(true);
      toast.success(messages.copy.copied);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error(messages.errors.generic);
    }
  };

  const handleRegenerate = () => {
    navigate('/dashboard', { state: { regenerateContext: generatedText.slice(0, 200) } });
  };

  const handleMakeShorter = () => {
    setGeneratedText(
      `Hi [First Name],\n\nI wanted to see if you'd be interested in a quick call to discuss how we can help [Company Name].\n\nWe've helped similar companies increase productivity by 40% and reduce costs by 25%.\n\nAvailable for a 15-minute call next week?\n\nBest,\n[Your Name]`
    );
  };

  const handleMakeFriendlier = () => {
    setGeneratedText(
      `Hey [First Name]! 👋\n\nI hope you're having a great day! I'd love to chat about how we might be able to help [Company Name] reach its goals.\n\nWe've had some amazing results with companies like yours:\n• 40% boost in productivity 🚀\n• 25% reduction in costs 💰\n• Smoother workflows that save tons of time ⏰\n\nWould you be up for a quick 15-minute call next week? I'd love to hear about what you're working on and share some ideas!\n\nCan't wait to connect!\n\n[Your Name]`
    );
  };

  const handleMakePersuasive = () => {
    setGeneratedText(
      `Dear [First Name],\n\nI'm reaching out because I believe [Company Name] could be missing out on significant opportunities for growth and efficiency.\n\nConsider this: Companies that partnered with us have seen:\n• 40% productivity gains within 90 days\n• 25% cost reduction in the first quarter\n• Streamlined operations that free up valuable time for strategic initiatives\n\nThe question isn't whether these results are possible—it's whether you can afford to wait any longer to achieve them.\n\nI'd like to offer you a complimentary 15-minute strategy session next week. During our call, I'll show you exactly how we can deliver these same results for [Company Name].\n\nAre you available? The sooner we start, the sooner you'll see results.\n\nI look forward to your response.\n\n[Your Name]`
    );
  };

  const handleSendToCrm = () => {
    navigate('/send', { state: { copy: generatedText, copyTypeLabel } });
  };

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-5xl mx-auto px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Generated Copy</h1>
            <p className="text-slate-600">Review and adjust your AI-generated content</p>
          </div>

          <div className="grid md:grid-cols-3 gap-6 lg:gap-8">
            <div className="md:col-span-2">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 mb-6">
                <div className="flex justify-between items-center mb-4">
                  <label htmlFor="copy-textarea" className="block text-sm font-medium text-slate-700">
                    Your Copy
                  </label>
                  <button
                    type="button"
                    onClick={handleCopy}
                    className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors focus-visible:rounded-md"
                  >
                    <Copy className="w-4 h-4" aria-hidden />
                    {copied ? 'Copied!' : 'Copy'}
                  </button>
                </div>
                <textarea
                  id="copy-textarea"
                  value={generatedText}
                  onChange={(e) => setGeneratedText(e.target.value)}
                  rows={16}
                  className="w-full px-4 py-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors resize-none font-mono text-sm"
                  aria-label="Generated copy text"
                />
              </div>

              <button
                type="button"
                onClick={handleSendToCrm}
                className="w-full h-12 bg-orange-600 hover:bg-orange-500 text-white font-semibold rounded-xl transition-colors flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
              >
                <Send className="w-5 h-5" aria-hidden />
                Send to CRM
              </button>
            </div>

            <div className="space-y-4">
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                <h2 className="font-semibold text-slate-900 mb-4">Adjust Copy</h2>
                <div className="space-y-3">
                  {[
                    { label: 'Make shorter', onClick: handleMakeShorter },
                    { label: 'Make friendlier', onClick: handleMakeFriendlier },
                    { label: 'Make more persuasive', onClick: handleMakePersuasive },
                  ].map(({ label, onClick }) => (
                    <button
                      key={label}
                      type="button"
                      onClick={onClick}
                      className="w-full py-3 px-4 border-2 border-slate-200 rounded-xl hover:border-orange-300 hover:bg-orange-50 transition-colors text-sm font-medium text-slate-700 hover:text-orange-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                    >
                      {label}
                    </button>
                  ))}
                  <button
                    type="button"
                    onClick={handleRegenerate}
                    className="w-full py-3 px-4 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors text-sm font-medium text-slate-700 flex items-center justify-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                  >
                    <RotateCcw className="w-4 h-4" aria-hidden />
                    Regenerate
                  </button>
                </div>
              </div>

              <div className="bg-sky-50 border border-sky-200 rounded-xl p-4">
                <p className="text-sm text-sky-800">
                  <strong>Tip:</strong> You can edit the text directly or use the adjustment buttons to refine your copy.
                </p>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
