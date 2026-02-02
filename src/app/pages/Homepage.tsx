import { Link } from 'react-router-dom';
import {
  ArrowRight,
  Check,
  Sparkles,
  Star,
  Mail,
  MessageSquare,
  FileText,
  Briefcase,
  Workflow,
  LayoutTemplate,
  History,
  Send,
  Link2,
  Settings,
  Clock,
  Zap,
  Copy,
  Shield,
  Target,
  Users,
  Kanban,
  UserCircle,
  Building2,
  CheckSquare,
  Activity,
} from 'lucide-react';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Homepage() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50 via-white to-slate-50">
      <header className="w-full px-[var(--page-padding)] py-[var(--header-block-padding-y)] flex justify-between items-center sticky top-0 bg-white/80 backdrop-blur-md z-50 rounded-b-2xl shadow-sm border-b border-slate-200/50" role="banner">
        <Link to="/" className="flex items-center gap-2 group focus-visible:rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2" aria-label="Cadence home">
          <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg group-hover:scale-105 transition-transform" aria-hidden>
            <Sparkles className="w-6 h-6 text-white" />
          </div>
          <span className="text-xl font-bold text-slate-900">Cadence</span>
        </Link>
        <Link to="/login" className="text-orange-600 hover:text-orange-700 font-semibold px-6 py-2 rounded-lg hover:bg-orange-50 transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2">
          Sign In
        </Link>
      </header>

      <main id={MAIN_CONTENT_ID} className="w-full" tabIndex={-1}>
      <section className="w-full px-[var(--page-padding)] py-[var(--section-padding-y)] text-center relative overflow-hidden" aria-labelledby="hero-heading">
        {/* Animated background elements */}
        <div className="absolute inset-0 -z-10">
          <div className="absolute top-20 left-10 w-72 h-72 bg-orange-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob"></div>
          <div className="absolute top-40 right-10 w-72 h-72 bg-yellow-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-2000"></div>
          <div className="absolute -bottom-8 left-1/2 w-72 h-72 bg-pink-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-4000"></div>
        </div>
        
        {/* Trust Badge */}
        <div className="inline-flex items-center gap-2 bg-white/90 backdrop-blur-sm border border-orange-200/80 text-orange-700 px-5 py-2.5 rounded-full text-sm font-semibold mb-6 animate-fade-in shadow-md">
          <Star className="w-4 h-4 fill-current text-orange-500" />
          Sales at the right pace
        </div>
        
        <h1 id="hero-heading" className="text-5xl md:text-7xl font-bold text-slate-900 mb-6 leading-tight animate-fade-in-up">
          Find your rhythm.<br />
          <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-600 to-orange-500">Close more deals.</span>
        </h1>
        <p className="text-xl md:text-2xl text-slate-600 mb-10 max-w-3xl mx-auto leading-relaxed animate-fade-in-up animation-delay-200">
          Full CRM and AI copy in one place. Manage leads, deals, contacts, and activities—then generate and send on-brand emails, follow-ups, and notes with one click. No copying, no context switching.
        </p>
        <div className="flex flex-col sm:flex-row gap-4 justify-center animate-fade-in-up animation-delay-400">
          <Link
            to="/login"
            className="bg-gradient-to-r from-orange-600 to-orange-500 hover:from-orange-700 hover:to-orange-600 text-white px-10 py-5 rounded-xl font-semibold flex items-center justify-center gap-2 transition-all shadow-xl hover:shadow-2xl hover:scale-105 transform focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
          >
            Get started
            <ArrowRight className="w-5 h-5" aria-hidden />
          </Link>
          <Link
            to="/help"
            className="bg-white hover:bg-slate-50 text-slate-900 px-10 py-5 rounded-xl font-semibold border-2 border-slate-200 hover:border-orange-300 transition-all shadow-lg hover:shadow-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
          >
            How it works
          </Link>
        </div>
        
        {/* All CRM in one app - pillars */}
        <div className="w-full mt-20">
          <p className="text-center text-sm font-semibold text-slate-500 uppercase tracking-widest mb-6 animate-fade-in-up" style={{ animationDelay: '0.4s', animationFillMode: 'backwards' }}>
            All your CRM in one application
          </p>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-6 gap-4">
            {[
              { label: 'Leads', sub: 'Capture & qualify', icon: Users },
              { label: 'Deals', sub: 'Pipeline & stages', icon: Kanban },
              { label: 'Contacts', sub: 'People & history', icon: UserCircle },
              { label: 'Companies', sub: 'Accounts & orgs', icon: Building2 },
              { label: 'Tasks', sub: 'Follow-ups & due dates', icon: CheckSquare },
              { label: 'Activities', sub: 'Calls, meetings, notes', icon: Activity },
            ].map((item, i) => (
              <div
                key={item.label}
                className="bg-white/90 backdrop-blur-sm rounded-2xl border border-slate-200/80 shadow-lg shadow-slate-200/30 px-4 py-5 text-center animate-fade-in-up hover:shadow-xl hover:-translate-y-1 transition-all duration-300"
                style={{ animationDelay: `${0.5 + i * 0.08}s`, animationFillMode: 'backwards' }}
              >
                <div className="inline-flex items-center justify-center w-11 h-11 rounded-xl bg-orange-100 text-orange-600 mb-2">
                  <item.icon className="w-5 h-5" />
                </div>
                <div className="text-sm font-bold text-slate-900">{item.label}</div>
                <div className="text-xs text-slate-500">{item.sub}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Sales features - modern accent cards */}
      <section className="w-full px-[var(--page-padding)] py-[var(--section-padding-y)] relative overflow-hidden" aria-labelledby="sales-features-heading">
        {/* Subtle background */}
        <div className="absolute inset-0 bg-gradient-to-b from-white via-orange-50/20 to-slate-50/50" aria-hidden />
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[800px] h-[400px] bg-orange-200/20 rounded-full blur-3xl -z-10" aria-hidden />

        <div className="relative max-w-6xl mx-auto">
          <div className="text-center mb-16 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Sales features</span>
            <h2 id="sales-features-heading" className="text-4xl md:text-5xl lg:text-6xl font-bold text-slate-900 mb-5 tracking-tight">Your sales hub</h2>
            <p className="text-lg md:text-xl text-slate-600 max-w-2xl mx-auto">Pipeline, people, and AI copy—all in one app. No tab switching, no copy-paste.</p>
          </div>

          <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4 md:gap-5">
            {[
              { icon: Users, title: 'Manage leads', desc: 'Create, edit, and track leads. Search and filter so you never lose a prospect.', tag: 'Pipeline' },
              { icon: Kanban, title: 'Pipeline & deals', desc: 'Visual board, stages, close dates. See pipeline value at a glance.', tag: 'Pipeline' },
              { icon: UserCircle, title: 'Contacts', desc: 'All contacts in one place. Link to companies, see interaction history.', tag: 'People' },
              { icon: Building2, title: 'Companies', desc: 'Accounts and orgs. Keep leads and deals linked to the right company.', tag: 'People' },
              { icon: CheckSquare, title: 'Tasks & follow-ups', desc: 'Tasks linked to leads or deals. Due dates, done—never miss a step.', tag: 'Execution' },
              { icon: Activity, title: 'Activities & timeline', desc: 'Log calls, meetings, notes. Filter by contact or deal.', tag: 'Execution' },
              { icon: Mail, title: 'AI sales copy', desc: 'Emails, follow-ups, notes, deal messages—in your brand voice, in seconds.', tag: 'Copy' },
              { icon: LayoutTemplate, title: 'Templates', desc: 'Pre-built goals: schedule meeting, follow up, close the deal. Generate faster.', tag: 'Copy' },
              { icon: Send, title: 'Send to CRM', desc: 'One action → right contact or deal. No copy-paste or app switching.', tag: 'Sync' },
              { icon: History, title: 'History', desc: 'Every send stored. Reference or reuse past messages anytime.', tag: 'Sync' },
              { icon: Link2, title: 'CRM connection', desc: 'Connect once. Encrypted link—push copy to contacts and deals from here.', tag: 'Sync' },
              { icon: Settings, title: 'Brand & settings', desc: 'Company name + brand tone. Every message stays on-brand.', tag: 'Sync' },
            ].map((item, idx) => (
              <div
                key={item.title}
                className="group flex gap-4 p-5 md:p-6 rounded-2xl bg-white/80 backdrop-blur-sm border border-slate-200/80 shadow-sm hover:shadow-xl hover:border-orange-200/80 hover:bg-white transition-all duration-300 animate-fade-in-up border-l-4 border-l-orange-500"
                style={{ animationDelay: `${0.05 + idx * 0.04}s`, animationFillMode: 'backwards' }}
              >
                <div className="shrink-0 w-11 h-11 rounded-xl bg-orange-100 text-orange-600 flex items-center justify-center group-hover:bg-orange-500 group-hover:text-white transition-colors duration-300">
                  <item.icon className="w-5 h-5" />
                </div>
                <div className="min-w-0">
                  <span className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">{item.tag}</span>
                  <h3 className="font-bold text-slate-900 mb-1.5 mt-0.5">{item.title}</h3>
                  <p className="text-slate-600 text-sm leading-relaxed">{item.desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* How it works - step cards with connector line */}
      <section className="w-full px-[var(--page-padding)] py-[var(--section-padding-y)] bg-white" aria-labelledby="how-it-works-heading">
        <div className="w-full">
          <div className="text-center mb-16 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Process</span>
            <h2 id="how-it-works-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">How It Works</h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">Connect once. Generate in seconds. Send from the app. No switching tabs or copy-paste.</p>
          </div>
          <div className="relative grid md:grid-cols-3 gap-8 md:gap-6">
            {/* Connector line (desktop) */}
            <div className="hidden md:block absolute top-20 left-[15%] right-[15%] h-0.5 bg-gradient-to-r from-orange-200 via-orange-300 to-orange-200 rounded-full" aria-hidden />
            {[
              { step: '1', title: 'Connect your CRM', desc: 'Authorize Cadence to talk to your CRM. We keep the link encrypted—you send to contacts and deals from inside the app, no copying between tools.', Icon: Link2 },
              { step: '2', title: 'Pick type & goal, get copy', desc: 'Choose what you need: sales email, follow-up, note, or deal message. Set a goal (e.g. schedule a meeting), add context—we draft it in your brand voice.', Icon: Sparkles },
              { step: '3', title: 'Review, tweak, send', desc: 'Read the draft, adjust tone or length if you want, then send it to the right contact or deal. Every send is saved in your history.', Icon: Send },
            ].map((item, idx) => (
              <div
                key={idx}
                className="relative bg-gradient-to-b from-slate-50 to-white p-8 rounded-2xl border border-slate-200/80 shadow-md hover:shadow-xl hover:border-orange-200/60 transition-all duration-300 group animate-rise-in"
                style={{ animationDelay: `${0.2 + idx * 0.15}s`, animationFillMode: 'backwards' }}
              >
                <div className="absolute -top-4 left-8 w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-xl flex items-center justify-center text-white font-bold shadow-lg group-hover:scale-110 transition-transform" aria-hidden>
                  {item.step}
                </div>
                <div className="w-14 h-14 mt-4 bg-orange-100 rounded-xl flex items-center justify-center text-orange-600 mb-5 group-hover:scale-105 transition-transform">
                  <item.Icon className="w-7 h-7" />
                </div>
                <h3 className="text-xl font-bold text-slate-900 mb-3">{item.title}</h3>
                <p className="text-slate-600 leading-relaxed text-sm">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* What the app does - bento layout, editorial style */}
      <section className="relative py-[var(--section-padding-y)] overflow-hidden" aria-labelledby="features-heading">
        {/* Background */}
        <div className="absolute inset-0 bg-slate-900" aria-hidden />
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_80%_60%_at_50%_0%,rgba(249,115,22,0.12),transparent)]" aria-hidden />
        <div className="absolute bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-orange-500/30 to-transparent" aria-hidden />

        <div className="relative w-full px-[var(--page-padding)]">
          <div className="text-center mb-16 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-400 uppercase tracking-widest mb-3">What the app does</span>
            <h2 id="features-heading" className="text-4xl md:text-6xl font-bold text-white mb-5 tracking-tight">Your CRM. Your voice. One app.</h2>
            <p className="text-lg md:text-xl text-slate-400 max-w-2xl mx-auto">Pipeline, contacts, and AI copy in your brand voice—templates, send-to-CRM, and history—all from a single place.</p>
          </div>

          {/* Bento grid: 2 hero cards + 3 + 3 + 2 */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 md:gap-5">
            {/* Hero: Sales email - spans 2 cols on lg */}
            <div
              className="lg:col-span-2 group relative rounded-2xl bg-white/5 border border-white/10 p-6 md:p-8 backdrop-blur-sm hover:bg-white/[0.08] hover:border-orange-500/30 transition-all duration-300 animate-fade-in-up"
              style={{ animationDelay: '0.05s', animationFillMode: 'backwards' }}
            >
              <div className="flex items-start gap-5">
                <div className="w-14 h-14 rounded-2xl bg-orange-500/20 border border-orange-500/30 flex items-center justify-center text-orange-400 shrink-0 group-hover:scale-105 transition-transform" aria-hidden>
                  <Mail className="w-7 h-7" />
                </div>
                <div>
                  <span className="text-xs font-semibold text-orange-400 uppercase tracking-wider">Outreach</span>
                  <h3 className="text-xl md:text-2xl font-bold text-white mt-1 mb-2">Sales email</h3>
                  <p className="text-slate-400 text-sm md:text-base leading-relaxed">Personalized outreach that sounds like you. Pick a goal, add context—we draft it in your brand voice so every email feels human, not templated.</p>
                </div>
              </div>
            </div>

            {/* Hero: Follow-up - spans 2 cols on lg */}
            <div
              className="lg:col-span-2 group relative rounded-2xl bg-white/5 border border-white/10 p-6 md:p-8 backdrop-blur-sm hover:bg-white/[0.08] hover:border-orange-500/30 transition-all duration-300 animate-fade-in-up"
              style={{ animationDelay: '0.1s', animationFillMode: 'backwards' }}
            >
              <div className="flex items-start gap-5">
                <div className="w-14 h-14 rounded-2xl bg-emerald-500/20 border border-emerald-500/30 flex items-center justify-center text-emerald-400 shrink-0 group-hover:scale-105 transition-transform" aria-hidden>
                  <MessageSquare className="w-7 h-7" />
                </div>
                <div>
                  <span className="text-xs font-semibold text-emerald-400 uppercase tracking-wider">Conversations</span>
                  <h3 className="text-xl md:text-2xl font-bold text-white mt-1 mb-2">Follow-up</h3>
                  <p className="text-slate-400 text-sm md:text-base leading-relaxed">Right tone, clear next step. Keep the conversation moving without sounding repetitive—we adapt to the goal you choose.</p>
                </div>
              </div>
            </div>

            {/* Compact cards - 4 across on lg */}
            {[
              { icon: FileText, title: 'CRM note', desc: 'Calls and meetings → clear notes and next steps, ready to attach.', tag: 'Notes', delay: 0.15 },
              { icon: Briefcase, title: 'Deal message', desc: 'Stakeholder updates and deal-moving copy without rewriting.', tag: 'Deals', delay: 0.2 },
              { icon: Workflow, title: 'Workflow message', desc: 'Copy for sequences and automation—consistent outreach.', tag: 'Automation', delay: 0.25 },
              { icon: LayoutTemplate, title: 'Templates', desc: 'Pre-built goals and types. Generate faster, one click.', tag: 'Speed', delay: 0.3 },
              { icon: History, title: 'History', desc: 'Every send stored. Reference or reuse anytime.', tag: 'Library', delay: 0.35 },
              { icon: Send, title: 'Send to CRM', desc: 'One action → right contact or deal. No copy-paste.', tag: 'Sync', delay: 0.4 },
              { icon: Link2, title: 'Connection', desc: 'Connect once. Encrypted link—send from here.', tag: 'Secure', delay: 0.45 },
              { icon: Settings, title: 'Settings', desc: 'Company name + brand tone. Every copy stays on-brand.', tag: 'Voice', delay: 0.5 },
            ].map((item, idx) => (
              <div
                key={idx}
                className="group rounded-xl bg-white/5 border border-white/10 p-5 backdrop-blur-sm hover:bg-white/[0.08] hover:border-white/20 transition-all duration-300 animate-fade-in-up"
                style={{ animationDelay: `${item.delay}s`, animationFillMode: 'backwards' }}
              >
                <div className="flex items-center gap-3 mb-3">
                  <div className="w-10 h-10 rounded-lg bg-white/10 flex items-center justify-center text-slate-300 group-hover:text-orange-400 transition-colors" aria-hidden>
                    <item.icon className="w-5 h-5" />
                  </div>
                  <span className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">{item.tag}</span>
                </div>
                <h3 className="font-bold text-white mb-1.5">{item.title}</h3>
                <p className="text-slate-500 text-sm leading-relaxed">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Before vs With Cadence - two-column contrast */}
      <section className="w-full px-[var(--page-padding)] py-[var(--section-padding-y)] bg-slate-900 text-slate-200" aria-labelledby="before-after-heading">
        <div className="w-full">
          <div className="text-center mb-14 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-400 uppercase tracking-widest mb-3">The difference</span>
            <h2 id="before-after-heading" className="text-4xl md:text-5xl font-bold text-white mb-4">Before Cadence vs With Cadence</h2>
            <p className="text-xl text-slate-400 max-w-2xl mx-auto">See how Cadence changes the way you write and send copy.</p>
          </div>
          <div className="grid md:grid-cols-2 gap-6 md:gap-8">
            <div className="bg-slate-800/80 rounded-2xl border border-slate-700 p-8 animate-slide-in-left hover:border-slate-600 transition-colors" style={{ animationDelay: '0.1s', animationFillMode: 'backwards' }}>
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 rounded-xl bg-slate-700 flex items-center justify-center text-slate-400">
                  <Copy className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-300">Without Cadence</h3>
              </div>
              <ul className="space-y-3 text-slate-400 text-sm">
                <li className="flex items-start gap-2"><span className="text-red-400 mt-0.5">×</span> Write every email and note from scratch</li>
                <li className="flex items-start gap-2"><span className="text-red-400 mt-0.5">×</span> Copy-paste between your CRM and other tools</li>
                <li className="flex items-start gap-2"><span className="text-red-400 mt-0.5">×</span> Inconsistent tone and extra time spent</li>
              </ul>
            </div>
            <div className="bg-gradient-to-br from-orange-600/20 to-amber-600/10 rounded-2xl border border-orange-500/30 p-8 animate-slide-in-right hover:border-orange-400/50 transition-all" style={{ animationDelay: '0.2s', animationFillMode: 'backwards' }}>
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 rounded-xl bg-orange-500/30 flex items-center justify-center text-orange-300">
                  <Zap className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-white">With Cadence</h3>
              </div>
              <ul className="space-y-3 text-slate-300 text-sm">
                <li className="flex items-start gap-2"><Check className="w-5 h-5 text-emerald-400 shrink-0 mt-0.5" /> AI drafts copy in your voice in seconds</li>
                <li className="flex items-start gap-2"><Check className="w-5 h-5 text-emerald-400 shrink-0 mt-0.5" /> One click sends to the right contact or deal in your CRM</li>
                <li className="flex items-start gap-2"><Check className="w-5 h-5 text-emerald-400 shrink-0 mt-0.5" /> Consistent, on-brand copy and more time for selling</li>
              </ul>
            </div>
          </div>
        </div>
      </section>

      {/* Why use Cadence - accent-bar cards */}
      <section className="w-full px-[var(--page-padding)] py-[var(--section-padding-y)] bg-white" aria-labelledby="why-heading">
        <div className="text-center mb-14 animate-fade-in-up">
          <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Benefits</span>
          <h2 id="why-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">Why Cadence</h2>
          <p className="text-xl text-slate-600 max-w-2xl mx-auto">Full CRM plus AI copy in one app—so you manage leads, deals, and contacts and spend less time writing and more time closing.</p>
        </div>
        <div className="grid md:grid-cols-3 gap-8">
          {[
            { icon: Clock, title: 'Save time', desc: 'Get emails, follow-ups, and CRM notes in seconds instead of staring at a blank screen. Same quality, far less effort.', stat: 'Less writing', accent: 'bg-amber-500' },
            { icon: Check, title: 'Simple setup', desc: 'Connect your CRM once, pick a type or template, and generate. No long onboarding or complicated workflows.', stat: 'Zero hassle', accent: 'bg-emerald-500' },
            { icon: Sparkles, title: 'Your brand voice', desc: 'Your company name and tone drive every message. The AI writes like you, so your copy stays consistent and on-brand.', stat: 'Sales-first', accent: 'bg-orange-500' }
          ].map((item, idx) => (
            <div
              key={idx}
              className="relative overflow-hidden bg-slate-50 rounded-2xl border border-slate-200 p-8 text-center group hover:shadow-xl hover:border-slate-300 transition-all duration-300 animate-rise-in"
              style={{ animationDelay: `${0.15 + idx * 0.1}s`, animationFillMode: 'backwards' }}
            >
              <div className="absolute top-0 left-0 right-0 h-1.5 bg-gradient-to-r from-orange-500 to-amber-500 opacity-80 group-hover:opacity-100 transition-opacity" aria-hidden />
              <div className="w-16 h-16 bg-white rounded-2xl border border-slate-200 flex items-center justify-center mx-auto mb-5 shadow-sm group-hover:scale-105 transition-transform" aria-hidden>
                <item.icon className="w-8 h-8 text-orange-600" />
              </div>
              <div className="inline-block bg-orange-100 text-orange-700 px-3 py-1 rounded-full text-xs font-semibold mb-3">{item.stat}</div>
              <h3 className="text-xl font-bold mb-3 text-slate-900">{item.title}</h3>
              <p className="text-slate-600 leading-relaxed text-sm">{item.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Trust / Simple checklist - different layout */}
      <section className="bg-gradient-to-b from-slate-100 to-slate-50 py-[var(--section-padding-y)]" aria-labelledby="trust-heading">
        <div className="w-full px-[var(--page-padding)]">
          <div className="text-center mb-12 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Simple & secure</span>
            <h2 id="trust-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">Simple as 1-2-3, Built to Trust</h2>
            <p className="text-xl text-slate-600 max-w-xl mx-auto">No complicated setup. Your CRM connection is secure, and your data stays yours.</p>
          </div>
          <div className="grid sm:grid-cols-3 gap-6">
            {[
              { icon: Target, title: 'Clear goal', desc: 'Pick a goal (e.g. schedule a meeting). The AI writes toward it.', label: '1' },
              { icon: Shield, title: 'Secure connection', desc: 'Connect your CRM once. We keep the link secure so you can send with one click.', label: '2' },
              { icon: Zap, title: 'Instant send', desc: 'Review, tweak if needed, then send to the right contact or deal. Done.', label: '3' },
            ].map((item, idx) => (
              <div
                key={idx}
                className="flex flex-col sm:flex-row sm:items-start gap-4 bg-white rounded-2xl border border-slate-200 p-6 shadow-sm hover:shadow-md transition-all duration-300 animate-fade-in-scale"
                style={{ animationDelay: `${0.2 + idx * 0.1}s`, animationFillMode: 'backwards' }}
              >
                <div className="flex items-center gap-3 sm:flex-col sm:items-center sm:gap-2">
                  <div className="w-10 h-10 rounded-xl bg-orange-100 text-orange-600 flex items-center justify-center font-bold shrink-0">{item.label}</div>
                  <div className="w-12 h-12 rounded-xl bg-slate-100 flex items-center justify-center text-slate-600 shrink-0">
                    <item.icon className="w-6 h-6" />
                  </div>
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 mb-1">{item.title}</h3>
                  <p className="text-slate-600 text-sm leading-relaxed">{item.desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA - gradient band with glow */}
      <section className="bg-gradient-to-r from-orange-600 via-orange-500 to-amber-500 py-[var(--section-padding-y)] relative overflow-hidden" aria-labelledby="cta-heading">
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNjAiIGhlaWdodD0iNjAiIHZpZXdCb3g9IjAgMCA2MCA2MCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZyBmaWxsPSJub25lIiBmaWxsLXJ1bGU9ImV2ZW5vZGQiPjxwYXRoIGQ9Ik0zNiAxOGMzLjMxNCAwIDYgMi42ODYgNiA2cy0yLjY4NiA2LTYgNi02LTIuNjg2LTYtNiAyLjY4Ni02IDYtNnptMCA0YzEuMTA1IDAgMiAuODk1IDIgMnMtLjg5NSAyLTIgMi0yLS44OTUtMi0yIC44OTUtMiAyLTJ6IiBmaWxsPSIjZmZmIiBmaWxsLW9wYWNpdHk9Ii4xIi8+PC9nPjwvc3ZnPg==')] opacity-10" aria-hidden />
        <div className="w-full px-[var(--page-padding)] text-center relative z-10 animate-fade-in-up">
          <h2 id="cta-heading" className="text-4xl md:text-5xl font-bold text-white mb-6">Create copy and send it to your CRM—from one place</h2>
          <p className="text-orange-100 mb-10 text-xl max-w-2xl mx-auto">No switching apps or copy-paste. Connect your CRM, tell the AI what you need, and send. That’s it.</p>
          <Link
            to="/login"
            className="bg-white hover:bg-slate-50 text-orange-600 px-12 py-5 rounded-xl font-bold text-lg inline-flex items-center gap-2 transition-all shadow-2xl hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-orange-500 animate-glow-pulse"
          >
            Get Started Free
            <ArrowRight className="w-6 h-6" aria-hidden />
          </Link>
          <p className="text-orange-100 mt-6 text-sm font-medium">No credit card required • Free forever plan</p>
        </div>
      </section>
      </main>

      <footer className="bg-slate-900 text-slate-400 py-[var(--footer-block-padding-y)]" role="contentinfo">
        <div className="w-full px-[var(--page-padding)]">
          <div className="grid md:grid-cols-4 gap-8 mb-12">
            <div className="md:col-span-2">
              <div className="flex items-center gap-2 mb-4">
                <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg" aria-hidden>
                  <Sparkles className="w-6 h-6 text-white" />
                </div>
                <span className="text-xl font-bold text-white">Cadence</span>
              </div>
              <p className="text-slate-400 max-w-md">Cadence is your full sales CRM and AI copy tool in one app. Manage leads, deals, contacts, companies, tasks, and activities—and create sales emails, follow-ups, notes, and deal updates in your brand voice. Send everything to your CRM with one click.</p>
            </div>
            <nav aria-label="Product links">
              <h3 className="font-semibold text-white mb-4">Product</h3>
              <ul className="space-y-2">
                <li><Link to="/dashboard" className="hover:text-white transition-colors focus-visible:underline">Dashboard</Link></li>
                <li><Link to="/leads" className="hover:text-white transition-colors focus-visible:underline">Leads</Link></li>
                <li><Link to="/deals" className="hover:text-white transition-colors focus-visible:underline">Deals</Link></li>
                <li><Link to="/contacts" className="hover:text-white transition-colors focus-visible:underline">Contacts</Link></li>
                <li><Link to="/companies" className="hover:text-white transition-colors focus-visible:underline">Companies</Link></li>
                <li><Link to="/tasks" className="hover:text-white transition-colors focus-visible:underline">Tasks</Link></li>
                <li><Link to="/activities" className="hover:text-white transition-colors focus-visible:underline">Activities</Link></li>
                <li><Link to="/templates" className="hover:text-white transition-colors focus-visible:underline">Templates</Link></li>
                <li><Link to="/history" className="hover:text-white transition-colors focus-visible:underline">History</Link></li>
                <li><Link to="/settings" className="hover:text-white transition-colors focus-visible:underline">Settings</Link></li>
                <li><Link to="/connect" className="hover:text-white transition-colors focus-visible:underline">Connect CRM</Link></li>
                <li><Link to="/help" className="hover:text-white transition-colors focus-visible:underline">How it works</Link></li>
              </ul>
            </nav>
            <nav aria-label="Legal links">
              <h3 className="font-semibold text-white mb-4">Legal</h3>
              <ul className="space-y-2">
                <li><Link to="/privacy" className="hover:text-white transition-colors focus-visible:underline">Privacy Policy</Link></li>
                <li><Link to="/terms" className="hover:text-white transition-colors focus-visible:underline">Terms of Service</Link></li>
                <li><Link to="/help" className="hover:text-white transition-colors focus-visible:underline">Help Center</Link></li>
              </ul>
            </nav>
          </div>
          <div className="border-t border-slate-800 pt-8 flex flex-col md:flex-row justify-between items-center gap-4">
            <p>&copy; 2026 Cadence. All rights reserved.</p>
            <nav className="flex gap-6" aria-label="Social links">
              <a href="https://twitter.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors focus-visible:underline" aria-label="Twitter (opens in new window)">Twitter</a>
              <a href="https://linkedin.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors focus-visible:underline" aria-label="LinkedIn (opens in new window)">LinkedIn</a>
              <a href="https://github.com" target="_blank" rel="noopener noreferrer" className="hover:text-white transition-colors focus-visible:underline" aria-label="GitHub (opens in new window)">GitHub</a>
            </nav>
          </div>
        </div>
      </footer>
    </div>
  );
}