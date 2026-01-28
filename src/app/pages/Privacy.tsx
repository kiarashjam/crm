import { Link } from 'react-router-dom';
import { ArrowLeft, Sparkles } from 'lucide-react';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Privacy() {
  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b border-slate-200 sticky top-0 z-40" role="banner">
        <div className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-4 flex justify-between items-center">
          <Link to="/" className="flex items-center gap-2 text-slate-600 hover:text-slate-900 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg" aria-label="Back to home">
            <ArrowLeft className="w-5 h-5" aria-hidden />
            Back to Home
          </Link>
          <Link to="/" className="flex items-center gap-2" aria-label="ACI home">
            <div className="w-10 h-10 bg-orange-600 rounded-lg flex items-center justify-center" aria-hidden>
              <Sparkles className="w-6 h-6 text-white" />
            </div>
            <span className="text-xl font-bold text-slate-900">ACI</span>
          </Link>
        </div>
      </header>

      <main id={MAIN_CONTENT_ID} className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full">
          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8 md:p-12">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-4">Privacy Policy</h1>
            <p className="text-slate-600 mb-8">Last updated: January 28, 2026</p>

            <div className="prose prose-gray max-w-none">
              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Introduction</h2>
                <p className="text-slate-700 mb-4">
                  Welcome to ACI. We respect your privacy and are committed to protecting your personal data. This privacy policy explains how we collect, use, and safeguard your information when you use our service.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Information We Collect</h2>
                <p className="text-slate-700 mb-4">We collect several types of information:</p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li><strong>Account Information:</strong> Email address, name, and company details</li>
                  <li><strong>CRM Data:</strong> Contact names, deal information, and CRM data (only what's needed to generate copy)</li>
                  <li><strong>Generated Content:</strong> Copy you create using our AI tools</li>
                  <li><strong>Usage Data:</strong> How you interact with our service</li>
                  <li><strong>Technical Data:</strong> IP address, browser type, and device information</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">How We Use Your Information</h2>
                <p className="text-slate-700 mb-4">We use your information to:</p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>Provide and improve our AI copywriting services</li>
                  <li>Generate personalized copy based on your CRM data</li>
                  <li>Send service updates and important notifications</li>
                  <li>Analyze usage patterns to enhance our product</li>
                  <li>Comply with legal obligations</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Data Security</h2>
                <p className="text-slate-700 mb-4">
                  We implement industry-standard security measures to protect your data:
                </p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>All data is encrypted in transit using TLS/SSL</li>
                  <li>Data at rest is encrypted using AES-256 encryption</li>
                  <li>Regular security audits and penetration testing</li>
                  <li>Strict access controls and authentication requirements</li>
                  <li>CRM connection tokens are securely stored and never exposed</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Data Sharing</h2>
                <p className="text-slate-700 mb-4">
                  We do not sell your personal data. We may share your information only in these limited circumstances:
                </p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li><strong>Service Providers:</strong> Trusted third parties who help us operate our service (e.g., hosting, analytics)</li>
                  <li><strong>Legal Requirements:</strong> When required by law or to protect our rights</li>
                  <li><strong>Business Transfers:</strong> In connection with a merger or acquisition</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Your Rights</h2>
                <p className="text-slate-700 mb-4">You have the right to:</p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>Access your personal data</li>
                  <li>Correct inaccurate data</li>
                  <li>Request deletion of your data</li>
                  <li>Object to processing of your data</li>
                  <li>Export your data in a portable format</li>
                  <li>Withdraw consent at any time</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">CRM Integration</h2>
                <p className="text-slate-700 mb-4">
                  When you connect your CRM account, we use secure OAuth protocol. We only request the minimum permissions necessary to provide our service. You can disconnect your CRM account at any time from the Settings page.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Cookies</h2>
                <p className="text-slate-700 mb-4">
                  We use essential cookies to maintain your session and preferences. We do not use tracking cookies for advertising purposes.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">International Transfers</h2>
                <p className="text-slate-700 mb-4">
                  Your data may be transferred to and processed in countries other than your country of residence. We ensure appropriate safeguards are in place to protect your data in accordance with this privacy policy.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Children's Privacy</h2>
                <p className="text-slate-700 mb-4">
                  Our service is not intended for children under 16. We do not knowingly collect personal information from children.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Changes to This Policy</h2>
                <p className="text-slate-700 mb-4">
                  We may update this privacy policy from time to time. We will notify you of any significant changes by email or through our service.
                </p>
              </section>

              <section>
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Contact Us</h2>
                <p className="text-slate-700 mb-4">
                  If you have questions about this privacy policy or how we handle your data, please contact us at:
                </p>
                <p className="text-slate-700">
                  Email: privacy@aci.com<br />
                  Address: 123 Privacy Street, San Francisco, CA 94102
                </p>
              </section>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
