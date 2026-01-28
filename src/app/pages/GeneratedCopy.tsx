import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Copy, RotateCcw, Send } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';

export default function GeneratedCopy() {
  const navigate = useNavigate();
  const [generatedText, setGeneratedText] = useState(
    `Hi [First Name],\n\nI hope this email finds you well! I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.\n\nWe've helped companies similar to yours:\n• Increase productivity by 40%\n• Reduce operational costs by 25%\n• Streamline workflows and save time\n\nWould you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.\n\nLooking forward to connecting!\n\nBest regards,\n[Your Name]`
  );
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(generatedText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleRegenerate = () => {
    // Simulate regeneration
    setGeneratedText(generatedText + '\n\n(Regenerated version)');
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

  return (
    <div className="min-h-screen bg-gray-50">
      <AppHeader />

      {/* Main Content */}
      <main className="w-full px-[var(--page-padding)] py-12">
        <div className="w-full">
          <div className="mb-6">
            <h1 className="text-3xl font-bold text-gray-900 mb-2">Generated Copy</h1>
            <p className="text-gray-600">Review and adjust your AI-generated content</p>
          </div>

          <div className="grid md:grid-cols-3 gap-6">
            {/* Generated Text Area */}
            <div className="md:col-span-2">
              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-6">
                <div className="flex justify-between items-center mb-4">
                  <label className="block text-sm font-medium text-gray-700">
                    Your Copy
                  </label>
                  <button
                    onClick={handleCopy}
                    className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 transition-colors"
                  >
                    <Copy className="w-4 h-4" />
                    {copied ? 'Copied!' : 'Copy'}
                  </button>
                </div>
                <textarea
                  value={generatedText}
                  onChange={(e) => setGeneratedText(e.target.value)}
                  rows={16}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition-colors resize-none font-mono text-sm"
                />
              </div>

              {/* Send to HubSpot Button */}
              <button
                onClick={() => navigate('/send')}
                className="w-full bg-orange-600 hover:bg-orange-700 text-white font-semibold py-4 px-6 rounded-lg transition-colors flex items-center justify-center gap-2"
              >
                <Send className="w-5 h-5" />
                Send to HubSpot
              </button>
            </div>

            {/* Action Buttons */}
            <div className="space-y-4">
              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                <h3 className="font-semibold text-gray-900 mb-4">Adjust Copy</h3>
                <div className="space-y-3">
                  <button
                    onClick={handleMakeShorter}
                    className="w-full py-3 px-4 border-2 border-gray-300 rounded-lg hover:border-orange-500 hover:bg-orange-50 transition-colors text-sm font-medium text-gray-700 hover:text-orange-700"
                  >
                    Make shorter
                  </button>
                  <button
                    onClick={handleMakeFriendlier}
                    className="w-full py-3 px-4 border-2 border-gray-300 rounded-lg hover:border-orange-500 hover:bg-orange-50 transition-colors text-sm font-medium text-gray-700 hover:text-orange-700"
                  >
                    Make friendlier
                  </button>
                  <button
                    onClick={handleMakePersuasive}
                    className="w-full py-3 px-4 border-2 border-gray-300 rounded-lg hover:border-orange-500 hover:bg-orange-50 transition-colors text-sm font-medium text-gray-700 hover:text-orange-700"
                  >
                    Make more persuasive
                  </button>
                  <button
                    onClick={handleRegenerate}
                    className="w-full py-3 px-4 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors text-sm font-medium text-gray-700 flex items-center justify-center gap-2"
                  >
                    <RotateCcw className="w-4 h-4" />
                    Regenerate
                  </button>
                </div>
              </div>

              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <p className="text-sm text-blue-800">
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
