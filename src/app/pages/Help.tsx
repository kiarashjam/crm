import { Link } from 'react-router-dom';
import { HelpCircle, Shield, Mail } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';

const TOC = [
  { href: '#what-heading', label: 'What Cadence does' },
  { href: '#generate-heading', label: 'Generating copy (4 steps)' },
  { href: '#crm-heading', label: 'Your data in Cadence' },
  { href: '#privacy-help-heading', label: 'Data & privacy' },
  { href: '#support-heading', label: 'Contact support' },
];

const STEPS = [
  { title: 'Pick what you’re writing', body: 'Sales email, follow-up, CRM note, deal message, or workflow copy. You can also start from a template.' },
  { title: 'Set the goal', body: 'Choose an outcome—e.g. schedule a meeting, follow up after a demo, close the deal—and add any extra context. We use your company name and brand tone from Settings.' },
  { title: 'Generate and tweak', body: 'Hit generate, then review the draft. Use the controls to make it shorter, friendlier, or more persuasive—then copy it or send it on.' },
  { title: 'Attach it to a record', body: 'Open Attach copy, pick a lead, contact, or deal, and save. The writing stays in Cadence and in History. Nothing is emailed.' },
];

export default function Help() {
  return (
    <div className="min-h-screen flex flex-col bg-gradient-calm">
      <AppHeader />

      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={HelpCircle}
            tone="orange"
            title="How It Works"
            subtitle="A quick guide to getting the most out of Cadence"
          >
            <nav className="flex flex-wrap items-center gap-x-2 gap-y-1.5" aria-label="Table of contents">
              <h2 className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">On this page</h2>
              <ul className="flex flex-wrap gap-1.5">
                {TOC.map((item) => (
                  <li key={item.href}>
                    <a
                      href={item.href}
                      className="inline-flex rounded-full border border-slate-200/80 bg-white/80 px-2.5 py-1 text-xs font-medium text-teal-700 shadow-sm backdrop-blur transition-colors hover:border-teal-200 hover:bg-white hover:text-teal-800"
                    >
                      {item.label}
                    </a>
                  </li>
                ))}
              </ul>
            </nav>
          </PageHero>

        <div className="w-full">

          {/* What Cadence does */}
          <section className="mb-12" aria-labelledby="what-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-teal-100 rounded-lg flex items-center justify-center">
                <HelpCircle className="w-6 h-6 text-teal-600" />
              </div>
              <h2 id="what-heading" className="text-2xl font-bold text-slate-900">What Cadence Does</h2>
            </div>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                Cadence drafts sales copy for you—emails, follow-ups, CRM notes, deal messages—in your brand voice. You pick the type and goal; we generate the text. No blank screen, no hopping between apps.
              </p>
              <p className="text-slate-700">
                Everything stays inside Cadence until you send it. When you’re ready, you push the copy to the right contact or deal in your CRM. Fast, consistent, and on-brand.
              </p>
            </div>
          </section>

          <section className="mb-12" aria-labelledby="generate-heading">
            <h2 id="generate-heading" className="text-2xl font-bold text-slate-900 mb-6">Generating Copy (4 Steps)</h2>
            <div className="space-y-6">
              {STEPS.map((step, index) => (
                <StaggerItem
                  key={step.title}
                  index={index}
                  className={`bg-white rounded-2xl shadow-sm border border-slate-200 p-6 hover:border-orange-200 ${liftOnHover}`}
                >
                  <div className="flex items-start gap-4">
                    <div className="w-10 h-10 bg-orange-600 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold">
                      {index + 1}
                    </div>
                    <div>
                      <h3 className="font-semibold text-lg text-slate-900 mb-2">{step.title}</h3>
                      <p className="text-slate-700">{step.body}</p>
                    </div>
                  </div>
                </StaggerItem>
              ))}
            </div>
          </section>

          {/* Your data in Cadence */}
          <section className="mb-12">
            <h2 id="crm-heading" className="text-2xl font-bold text-slate-900 mb-6">Your Data in Cadence</h2>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                <strong>Cadence is the only CRM you need.</strong> All your data — contacts, deals, leads, copy history — stays in Cadence. No external CRM is required.
              </p>
              <p className="text-slate-700">
                Attach copy saves writing on a lead, contact, or deal <strong>in Cadence</strong>. Nothing is emailed and nothing is sent to another system.
              </p>
            </div>
          </section>

          <section className="mb-12" aria-labelledby="privacy-help-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-teal-100 rounded-lg flex items-center justify-center" aria-hidden>
                <Shield className="w-6 h-6 text-teal-600" />
              </div>
              <h2 id="privacy-help-heading" className="text-2xl font-bold text-slate-900">Data & Privacy</h2>
            </div>
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
              <p className="text-slate-700 mb-4">
                We only use what’s needed to generate and send copy. Here’s the short version:
              </p>
              <ul className="list-disc list-inside space-y-2 text-slate-700 mb-4">
                <li>Access is limited to the data required for copy and sending</li>
                <li>Data is encrypted in transit and at rest</li>
                <li>We don’t sell or share your data</li>
                <li>You can delete your account and data whenever you want</li>
                <li>We follow GDPR, CCPA, and similar privacy rules</li>
              </ul>
              <Link to="/privacy" className="text-teal-700 hover:text-teal-800 font-medium focus-visible:underline">
                Full Privacy Policy →
              </Link>
            </div>
          </section>

          <section aria-labelledby="support-heading">
            <div className="flex items-center gap-3 mb-6">
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center" aria-hidden>
                <Mail className="w-6 h-6 text-orange-600" />
              </div>
              <h2 id="support-heading" className="text-2xl font-bold text-slate-900">Contact Support</h2>
            </div>
            <div className="bg-gradient-to-r from-teal-600 to-teal-500 rounded-2xl shadow-lg shadow-teal-200/50 p-8 text-white">
              <p className="text-lg mb-4">Something unclear or broken? We’re here to help.</p>
              <a href="mailto:support@example.com" className="inline-block bg-white text-teal-700 hover:bg-slate-50 px-6 py-3 rounded-2xl font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-teal-500">
                Contact Support
              </a>
            </div>
          </section>
        </div>
        </main>
      </PageTransition>
    </div>
  );
}
