import { Link } from 'react-router-dom';
import { ArrowLeft, Sparkles } from 'lucide-react';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import DemoBanner from '@/app/components/DemoBanner';

export default function Terms() {
  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b border-slate-200 sticky top-0 z-40 shadow-sm" role="banner">
        <div className="w-full px-[var(--page-padding)] py-4 flex justify-between items-center">
          <Link to="/" className="flex items-center gap-2 text-slate-600 hover:text-slate-900 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg min-h-[44px] min-w-[44px] items-center justify-center" aria-label="Back to home">
            <ArrowLeft className="w-5 h-5" aria-hidden />
            Back to Home
          </Link>
          <Link to="/" className="flex items-center gap-2" aria-label="Cadence home">
            <div className="w-10 h-10 bg-orange-600 rounded-lg flex items-center justify-center" aria-hidden>
              <Sparkles className="w-6 h-6 text-white" />
            </div>
            <span className="text-xl font-bold text-slate-900">Cadence</span>
          </Link>
        </div>
      </header>
      <DemoBanner />

      <main id={MAIN_CONTENT_ID} className="w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="w-full">
          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8 md:p-12">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-4">Terms of Service</h1>
            <p className="text-slate-600 mb-8">Last updated: January 28, 2026</p>

            <div className="prose prose-gray max-w-none">
              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Agreement to Terms</h2>
                <p className="text-slate-700 mb-4">
                  By accessing or using Cadence, you agree to be bound by these Terms of Service and all applicable laws and regulations. If you do not agree with any of these terms, you are prohibited from using this service.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Use License</h2>
                <p className="text-slate-700 mb-4">
                  We grant you a limited, non-exclusive, non-transferable license to use Cadence for your business purposes, subject to these terms:
                </p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>You must be at least 18 years old to use this service</li>
                  <li>You must provide accurate account information</li>
                  <li>You are responsible for maintaining the security of your account</li>
                  <li>You must not use the service for any illegal or unauthorized purpose</li>
                  <li>You must not violate any laws in your jurisdiction</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Service Description</h2>
                <p className="text-slate-700 mb-4">
                  Cadence provides AI-powered copywriting tools that integrate with your CRM. We reserve the right to:
                </p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>Modify or discontinue any feature at any time</li>
                  <li>Refuse service to anyone for any reason</li>
                  <li>Terminate accounts that violate these terms</li>
                  <li>Change pricing with 30 days notice</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Acceptable Use</h2>
                <p className="text-slate-700 mb-4">You agree not to:</p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>Use the service to generate spam, harmful, or illegal content</li>
                  <li>Attempt to gain unauthorized access to our systems</li>
                  <li>Interfere with or disrupt the service</li>
                  <li>Use automated tools to access the service (except our API)</li>
                  <li>Reverse engineer or copy our AI models</li>
                  <li>Resell or redistribute our service without permission</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Content Ownership</h2>
                <p className="text-slate-700 mb-4">
                  <strong>Your Content:</strong> You retain all rights to content you input into the service. By using our service, you grant us permission to process this content to provide our AI copywriting features.
                </p>
                <p className="text-slate-700 mb-4">
                  <strong>Generated Content:</strong> You own all rights to content generated by our AI for your account. However, AI-generated content may not be unique, as similar prompts could produce similar results for other users.
                </p>
                <p className="text-slate-700 mb-4">
                  <strong>Our IP:</strong> Cadence, our AI models, software, and branding remain our intellectual property.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">CRM Integration</h2>
                <p className="text-slate-700 mb-4">
                  Our service integrates with your CRM. You are responsible for:
                </p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>Maintaining an active CRM account</li>
                  <li>Ensuring you have permission to access the CRM data you use</li>
                  <li>Complying with your CRM provider's terms of service</li>
                  <li>Any actions taken in your CRM account using our service</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Payment Terms</h2>
                <p className="text-slate-700 mb-4">
                  If you subscribe to a paid plan:
                </p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>You authorize us to charge your payment method</li>
                  <li>Subscriptions automatically renew unless cancelled</li>
                  <li>Refunds are provided at our discretion</li>
                  <li>Failed payments may result in service suspension</li>
                  <li>You're responsible for all taxes</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Disclaimers</h2>
                <p className="text-slate-700 mb-4">
                  THE SERVICE IS PROVIDED "AS IS" WITHOUT WARRANTIES OF ANY KIND. WE DISCLAIM ALL WARRANTIES, EXPRESS OR IMPLIED, INCLUDING:
                </p>
                <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                  <li>Merchantability or fitness for a particular purpose</li>
                  <li>Uninterrupted or error-free operation</li>
                  <li>Accuracy or reliability of AI-generated content</li>
                  <li>Security of data transmission</li>
                </ul>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Limitation of Liability</h2>
                <p className="text-slate-700 mb-4">
                  TO THE MAXIMUM EXTENT PERMITTED BY LAW, WE SHALL NOT BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES, OR ANY LOSS OF PROFITS OR REVENUES, WHETHER INCURRED DIRECTLY OR INDIRECTLY.
                </p>
                <p className="text-slate-700 mb-4">
                  OUR TOTAL LIABILITY SHALL NOT EXCEED THE AMOUNT YOU PAID US IN THE PAST 12 MONTHS.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Indemnification</h2>
                <p className="text-slate-700 mb-4">
                  You agree to indemnify and hold us harmless from any claims, damages, or expenses arising from your use of the service or violation of these terms.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Termination</h2>
                <p className="text-slate-700 mb-4">
                  We may terminate or suspend your account at any time for violations of these terms. You may cancel your account at any time from the Settings page.
                </p>
                <p className="text-slate-700 mb-4">
                  Upon termination, your right to use the service ceases immediately. Data retention is governed by our Privacy Policy.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Changes to Terms</h2>
                <p className="text-slate-700 mb-4">
                  We reserve the right to modify these terms at any time. We will notify you of material changes via email or through the service. Continued use after changes constitutes acceptance of the new terms.
                </p>
              </section>

              <section className="mb-8">
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Governing Law</h2>
                <p className="text-slate-700 mb-4">
                  These terms are governed by the laws of the State of California, USA, without regard to conflict of law principles.
                </p>
              </section>

              <section>
                <h2 className="text-2xl font-bold text-slate-900 mb-4">Contact Information</h2>
                <p className="text-slate-700 mb-4">
                  Questions about these Terms of Service? Contact us at:
                </p>
                <p className="text-slate-700">
                  Email: legal@aci.com<br />
                  Address: 123 Legal Street, San Francisco, CA 94102
                </p>
              </section>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
