import { Link } from 'react-router-dom';
import { ArrowLeft, Sparkles } from 'lucide-react';

export default function Privacy() {
  return (
    <div className="min-h-screen bg-gray-50">
      {/* Top Bar */}
      <header className="bg-white border-b border-gray-200">
        <div className="w-full px-[var(--page-padding)] py-4 flex justify-between items-center">
          <Link to="/" className="flex items-center gap-2 text-gray-600 hover:text-gray-900 transition-colors">
            <ArrowLeft className="w-5 h-5" />
            Back to Home
          </Link>
          <div className="flex items-center gap-2">
            <div className="w-10 h-10 bg-orange-600 rounded-lg flex items-center justify-center">
              <Sparkles className="w-6 h-6 text-white" />
            </div>
            <span className="text-xl font-bold text-gray-900">HubSpot AI Writer</span>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="w-full px-[var(--page-padding)] py-12">
        <div className="w-full">
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 md:p-12">
            <h1 className="text-4xl font-bold text-gray-900 mb-4">Privacy Policy</h1>
            <p className="text-gray-600 mb-8">Last updated: January 28, 2026</p>

            <div className="prose prose-gray max-w-none">
              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Introduction</h2>
                <p className="text-gray-700 mb-4">
                  Welcome to HubSpot AI Writer. We respect your privacy and are committed to protecting your personal data. This privacy policy explains how we collect, use, and safeguard your information when you use our service.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Information We Collect</h2>
                <p className="text-gray-700 mb-4">We collect several types of information:</p>
                <ul className="list-disc list-inside space-y-2 text-gray-700 mb-4">
                  <li><strong>Account Information:</strong> Email address, name, and company details</li>
                  <li><strong>HubSpot Data:</strong> Contact names, deal information, and CRM data (only what's needed to generate copy)</li>
                  <li><strong>Generated Content:</strong> Copy you create using our AI tools</li>
                  <li><strong>Usage Data:</strong> How you interact with our service</li>
                  <li><strong>Technical Data:</strong> IP address, browser type, and device information</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">How We Use Your Information</h2>
                <p className="text-gray-700 mb-4">We use your information to:</p>
                <ul className="list-disc list-inside space-y-2 text-gray-700 mb-4">
                  <li>Provide and improve our AI copywriting services</li>
                  <li>Generate personalized copy based on your HubSpot data</li>
                  <li>Send service updates and important notifications</li>
                  <li>Analyze usage patterns to enhance our product</li>
                  <li>Comply with legal obligations</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Data Security</h2>
                <p className="text-gray-700 mb-4">
                  We implement industry-standard security measures to protect your data:
                </p>
                <ul className="list-disc list-inside space-y-2 text-gray-700 mb-4">
                  <li>All data is encrypted in transit using TLS/SSL</li>
                  <li>Data at rest is encrypted using AES-256 encryption</li>
                  <li>Regular security audits and penetration testing</li>
                  <li>Strict access controls and authentication requirements</li>
                  <li>HubSpot OAuth tokens are securely stored and never exposed</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Data Sharing</h2>
                <p className="text-gray-700 mb-4">
                  We do not sell your personal data. We may share your information only in these limited circumstances:
                </p>
                <ul className="list-disc list-inside space-y-2 text-gray-700 mb-4">
                  <li><strong>Service Providers:</strong> Trusted third parties who help us operate our service (e.g., hosting, analytics)</li>
                  <li><strong>Legal Requirements:</strong> When required by law or to protect our rights</li>
                  <li><strong>Business Transfers:</strong> In connection with a merger or acquisition</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Your Rights</h2>
                <p className="text-gray-700 mb-4">You have the right to:</p>
                <ul className="list-disc list-inside space-y-2 text-gray-700 mb-4">
                  <li>Access your personal data</li>
                  <li>Correct inaccurate data</li>
                  <li>Request deletion of your data</li>
                  <li>Object to processing of your data</li>
                  <li>Export your data in a portable format</li>
                  <li>Withdraw consent at any time</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">HubSpot Integration</h2>
                <p className="text-gray-700 mb-4">
                  When you connect your HubSpot account, we use HubSpot's official OAuth protocol. We only request the minimum permissions necessary to provide our service. You can disconnect your HubSpot account at any time from the Settings page.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Cookies</h2>
                <p className="text-gray-700 mb-4">
                  We use essential cookies to maintain your session and preferences. We do not use tracking cookies for advertising purposes.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">International Transfers</h2>
                <p className="text-gray-700 mb-4">
                  Your data may be transferred to and processed in countries other than your country of residence. We ensure appropriate safeguards are in place to protect your data in accordance with this privacy policy.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Children's Privacy</h2>
                <p className="text-gray-700 mb-4">
                  Our service is not intended for children under 16. We do not knowingly collect personal information from children.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Changes to This Policy</h2>
                <p className="text-gray-700 mb-4">
                  We may update this privacy policy from time to time. We will notify you of any significant changes by email or through our service.
                </p>
              </section>

              <section>
                <h2 className="text-2xl font-bold text-gray-900 mb-4">Contact Us</h2>
                <p className="text-gray-700 mb-4">
                  If you have questions about this privacy policy or how we handle your data, please contact us at:
                </p>
                <p className="text-gray-700">
                  Email: privacy@hubspotaiwriter.com<br />
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
