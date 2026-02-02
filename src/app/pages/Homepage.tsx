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
      <header className="w-full px-[var(--page-padding)] py-6 flex justify-between items-center sticky top-0 bg-white/80 backdrop-blur-md z-50 rounded-b-2xl shadow-sm border-b border-slate-200/50" role="banner">
        <Link to="/" className="flex items-center gap-2 group focus-visible:rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2" aria-label="ACI home">
          <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg group-hover:scale-105 transition-transform" aria-hidden>
            <Sparkles className="w-6 h-6 text-white" />
          </div>
          <span className="text-xl font-bold text-slate-900">ACI</span>
        </Link>
        <Link to="/login" className="text-orange-600 hover:text-orange-700 font-semibold px-6 py-2 rounded-lg hover:bg-orange-50 transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2">
          Sign In
        </Link>
      </header>

      <main id={MAIN_CONTENT_ID} className="w-full" tabIndex={-1}>
      <section className="w-full px-[var(--page-padding)] py-24 text-center relative overflow-hidden" aria-labelledby="hero-heading">
        {/* Animated background elements */}
        <div className="absolute inset-0 -z-10">
          <div className="absolute top-20 left-10 w-72 h-72 bg-orange-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob"></div>
          <div className="absolute top-40 right-10 w-72 h-72 bg-yellow-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-2000"></div>
          <div className="absolute -bottom-8 left-1/2 w-72 h-72 bg-pink-200 rounded-full mix-blend-multiply filter blur-xl opacity-30 animate-blob animation-delay-4000"></div>
        </div>
        
        {/* Trust Badge */}
        <div className="inline-flex items-center gap-2 bg-white/90 backdrop-blur-sm border border-orange-200/80 text-orange-700 px-5 py-2.5 rounded-full text-sm font-semibold mb-6 animate-fade-in shadow-md">
          <Star className="w-4 h-4 fill-current text-orange-500" />
          Full sales CRM + AI copy—all in one application
        </div>
        
        <h1 id="hero-heading" className="text-5xl md:text-7xl font-bold text-slate-900 mb-6 leading-tight animate-fade-in-up">
          Do all your CRM here.<br />
          <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-600 to-orange-500">Sell more with AI.</span>
        </h1>
        <p className="text-xl md:text-2xl text-slate-600 mb-10 max-w-3xl mx-auto leading-relaxed animate-fade-in-up animation-delay-200">
          Manage leads, deals, contacts, companies, tasks, and activities—and create personalized emails, follow-ups, notes, and deal updates in your brand voice. Send everything to your CRM with one click. No copying, no switching apps.
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
            See how it works
          </Link>
        </div>
        
        {/* All CRM in one app - pillars */}
        <div className="max-w-5xl mx-auto mt-20">
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

      {/* What you can do with your sales in the application */}
      <section className="w-full px-[var(--page-padding)] py-24 bg-slate-50/80 border-y border-slate-200/60" aria-labelledby="sales-features-heading">
        <div className="max-w-6xl mx-auto">
          <div className="text-center mb-14 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Sales features</span>
            <h2 id="sales-features-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">What you can do with your sales in the application</h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">Every core sales and CRM function is available in one place—manage your pipeline, people, and AI copy without leaving the app.</p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[
              { icon: Users, title: 'Manage leads', desc: 'Create, edit, and track leads with name, email, phone, company, source, and status. Search and filter so you never lose a prospect.' },
              { icon: Kanban, title: 'Track pipeline & deals', desc: 'Move deals through stages on a visual board. Set expected close dates, mark won or lost, and see pipeline value at a glance.' },
              { icon: UserCircle, title: 'Contacts', desc: 'Keep all your contacts in one place. View and search contacts, link them to companies, and see interaction history.' },
              { icon: Building2, title: 'Companies', desc: 'Manage companies and accounts. List, search, and organize your accounts so leads and deals stay linked to the right org.' },
              { icon: CheckSquare, title: 'Tasks & follow-ups', desc: 'Create tasks linked to leads or deals, set due dates, and mark them complete. Stay on top of follow-ups and never miss a step.' },
              { icon: Activity, title: 'Activities & timeline', desc: 'Log calls, meetings, emails, and notes. Filter by contact or deal to see a clear timeline of every interaction.' },
              { icon: Mail, title: 'AI sales copy', desc: 'Generate sales emails, follow-ups, CRM notes, deal messages, and workflow copy in your brand voice—in seconds.' },
              { icon: LayoutTemplate, title: 'Templates', desc: 'Use pre-built templates for common goals (schedule meeting, follow up after demo, close the deal) and generate faster.' },
              { icon: Send, title: 'Send to CRM', desc: 'One click sends generated copy to the right contact or deal in your CRM. No copy-paste or switching between apps.' },
              { icon: History, title: 'History', desc: 'Every piece of copy you generate and send is stored in history. Reference or reuse past messages anytime.' },
              { icon: Link2, title: 'CRM connection', desc: 'Connect your CRM once. The app keeps the link secure so you can push copy to contacts and deals from inside the app.' },
              { icon: Settings, title: 'Brand & settings', desc: 'Set your company name and brand tone. Every AI-generated message stays consistent and on-brand.' },
            ].map((item, idx) => (
              <div
                key={item.title}
                className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-sm hover:shadow-lg hover:border-orange-200/60 transition-all duration-300 animate-fade-in-up"
                style={{ animationDelay: `${0.1 + idx * 0.05}s`, animationFillMode: 'backwards' }}
              >
                <div className="w-12 h-12 rounded-xl bg-orange-100 text-orange-600 flex items-center justify-center mb-4">
                  <item.icon className="w-6 h-6" />
                </div>
                <h3 className="font-bold text-lg text-slate-900 mb-2">{item.title}</h3>
                <p className="text-slate-600 text-sm leading-relaxed">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* How it works - step cards with connector line */}
      <section className="w-full px-[var(--page-padding)] py-24 bg-white" aria-labelledby="how-it-works-heading">
        <div className="max-w-6xl mx-auto">
          <div className="text-center mb-16 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Process</span>
            <h2 id="how-it-works-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">How It Works</h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">Three steps: connect your CRM, tell the AI what you need, then send—no copy-paste.</p>
          </div>
          <div className="relative grid md:grid-cols-3 gap-8 md:gap-6">
            {/* Connector line (desktop) */}
            <div className="hidden md:block absolute top-20 left-[15%] right-[15%] h-0.5 bg-gradient-to-r from-orange-200 via-orange-300 to-orange-200 rounded-full" aria-hidden />
            {[
              { step: '1', title: 'Connect your CRM', desc: 'Link your CRM once. Your connection stays secure so you can push copy to contacts and deals with one click from inside the app.', Icon: Link2 },
              { step: '2', title: 'Tell the AI what you need', desc: 'Choose a content type (sales email, follow-up, CRM note, deal message) or a template. Add your goal and any context—the AI writes in your brand voice.', Icon: Sparkles },
              { step: '3', title: 'Review and send', desc: 'Read the generated copy, tweak if you like, then send it straight to a contact or deal in your CRM. Everything is logged in your history.', Icon: Send },
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

      {/* What the app does - bento-style grid with stagger */}
      <section className="bg-gradient-to-b from-slate-50 to-white py-24" aria-labelledby="features-heading">
        <div className="w-full px-[var(--page-padding)] max-w-6xl mx-auto">
          <div className="text-center mb-14 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Features</span>
            <h2 id="features-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">What the App Does</h2>
            <p className="text-xl text-slate-600 max-w-2xl mx-auto">Run your full CRM and create AI copy in your voice, use templates, and send it into your CRM—all in one place.</p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-5">
            {[
              { icon: Mail, title: 'Sales email', desc: 'AI writes personalized outreach that sounds like you and matches your brand.', color: 'from-blue-500 to-blue-600' },
              { icon: MessageSquare, title: 'Follow-up', desc: 'Keep the conversation going with the right tone and next step.', color: 'from-green-500 to-green-600' },
              { icon: FileText, title: 'CRM note', desc: 'Turn calls and meetings into clear notes and next steps, ready to attach to the record.', color: 'from-purple-500 to-purple-600' },
              { icon: Briefcase, title: 'Deal message', desc: 'Draft updates for stakeholders and move deals forward without rewriting from scratch.', color: 'from-pink-500 to-pink-600' },
              { icon: Workflow, title: 'Workflow message', desc: 'Copy for sequences and automation so your outreach stays consistent.', color: 'from-orange-500 to-orange-600' },
              { icon: LayoutTemplate, title: 'Templates', desc: 'Pre-built content types and goals so you can generate faster with one click.', color: 'from-indigo-500 to-indigo-600' },
              { icon: History, title: 'History', desc: 'Every piece of copy you generate and send, in one place, for reference or reuse.', color: 'from-slate-600 to-slate-700' },
              { icon: Send, title: 'Send to CRM', desc: 'One click sends the copy to the right contact or deal—no copy-paste between apps.', color: 'from-emerald-500 to-emerald-600' },
              { icon: Link2, title: 'Connection', desc: 'Connect your CRM once; the app keeps the link secure so you can send from here.', color: 'from-amber-500 to-amber-600' },
              { icon: Settings, title: 'Settings', desc: 'Set your company name and brand tone so every piece of copy stays on-brand.', color: 'from-slate-500 to-slate-600' },
            ].map((item, idx) => (
              <div
                key={idx}
                className="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-sm hover:shadow-lg hover:border-slate-300/80 transition-all duration-300 group hover:-translate-y-1 animate-fade-in-scale"
                style={{ animationDelay: `${0.1 + idx * 0.06}s`, animationFillMode: 'backwards' }}
              >
                <div className={`w-12 h-12 bg-gradient-to-br ${item.color} rounded-xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform shadow-md`} aria-hidden>
                  <item.icon className="w-6 h-6 text-white" />
                </div>
                <h3 className="font-bold text-lg mb-2 text-slate-900">{item.title}</h3>
                <p className="text-slate-600 text-sm leading-relaxed">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Before vs With ACI - two-column contrast */}
      <section className="w-full px-[var(--page-padding)] py-24 bg-slate-900 text-slate-200" aria-labelledby="before-after-heading">
        <div className="max-w-5xl mx-auto">
          <div className="text-center mb-14 animate-fade-in-up">
            <span className="inline-block text-xs font-semibold text-orange-400 uppercase tracking-widest mb-3">The difference</span>
            <h2 id="before-after-heading" className="text-4xl md:text-5xl font-bold text-white mb-4">Before ACI vs With ACI</h2>
            <p className="text-xl text-slate-400 max-w-2xl mx-auto">See how ACI changes the way you write and send copy.</p>
          </div>
          <div className="grid md:grid-cols-2 gap-6 md:gap-8">
            <div className="bg-slate-800/80 rounded-2xl border border-slate-700 p-8 animate-slide-in-left hover:border-slate-600 transition-colors" style={{ animationDelay: '0.1s', animationFillMode: 'backwards' }}>
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 rounded-xl bg-slate-700 flex items-center justify-center text-slate-400">
                  <Copy className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-300">Without ACI</h3>
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
                <h3 className="text-xl font-bold text-white">With ACI</h3>
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

      {/* Why use ACI - accent-bar cards */}
      <section className="w-full px-[var(--page-padding)] py-24 max-w-6xl mx-auto bg-white" aria-labelledby="why-heading">
        <div className="text-center mb-14 animate-fade-in-up">
          <span className="inline-block text-xs font-semibold text-orange-600 uppercase tracking-widest mb-3">Benefits</span>
          <h2 id="why-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-4">Why Use ACI</h2>
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
      <section className="bg-gradient-to-b from-slate-100 to-slate-50 py-24" aria-labelledby="trust-heading">
        <div className="w-full px-[var(--page-padding)] max-w-4xl mx-auto">
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
      <section className="bg-gradient-to-r from-orange-600 via-orange-500 to-amber-500 py-24 relative overflow-hidden" aria-labelledby="cta-heading">
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNjAiIGhlaWdodD0iNjAiIHZpZXdCb3g9IjAgMCA2MCA2MCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZyBmaWxsPSJub25lIiBmaWxsLXJ1bGU9ImV2ZW5vZGQiPjxwYXRoIGQ9Ik0zNiAxOGMzLjMxNCAwIDYgMi42ODYgNiA2cy0yLjY4NiA2LTYgNi02LTIuNjg2LTYtNiAyLjY4Ni02IDYtNnptMCA0YzEuMTA1IDAgMiAuODk1IDIgMnMtLjg5NSAyLTIgMi0yLS44OTUtMi0yIC44OTUtMiAyLTJ6IiBmaWxsPSIjZmZmIiBmaWxsLW9wYWNpdHk9Ii4xIi8+PC9nPjwvc3ZnPg==')] opacity-10" aria-hidden />
        <div className="w-full px-[var(--page-padding)] text-center relative z-10 max-w-4xl mx-auto animate-fade-in-up">
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

      <footer className="bg-slate-900 text-slate-400 py-16" role="contentinfo">
        <div className="w-full px-[var(--page-padding)] max-w-6xl mx-auto">
          <div className="grid md:grid-cols-4 gap-8 mb-12">
            <div className="md:col-span-2">
              <div className="flex items-center gap-2 mb-4">
                <div className="w-10 h-10 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-lg" aria-hidden>
                  <Sparkles className="w-6 h-6 text-white" />
                </div>
                <span className="text-xl font-bold text-white">ACI</span>
              </div>
              <p className="text-slate-400 max-w-md">ACI is your full sales CRM and AI copy tool in one app. Manage leads, deals, contacts, companies, tasks, and activities—and create sales emails, follow-ups, notes, and deal updates in your brand voice. Send everything to your CRM with one click.</p>
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
            <p>&copy; 2026 ACI. All rights reserved.</p>
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