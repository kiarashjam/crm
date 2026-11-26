import { Link } from 'react-router-dom';
import {
  ArrowRight,
  Check,
  Sparkles,
  Mail,
  MessageSquare,
  FileText,
  Briefcase,
  Settings,
  Shield,
  Target,
  Users,
  Kanban,
  UserCircle,
  Building2,
  CheckSquare,
  Activity,
  TrendingUp,
  BarChart3,
  Globe,
  Lock,
  Database,
  UserPlus,
  Rocket,
  Play,
  CheckCircle2,
  XCircle,
  Zap,
  Award,
  MousePointerClick,
  Layers,
  Gem,
  Flame,
  Crown,
  Server,
  Gift,
} from 'lucide-react';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { usePageLanguage } from '@/app/i18n';
import { MarketingHeader } from '@/app/marketing/components/MarketingHeader';
import { MarketingFooter } from '@/app/marketing/components/MarketingFooter';
import { CtaLink } from '@/app/marketing/components/CtaLink';
import { Reassurance } from '@/app/marketing/components/Reassurance';
import { StickySignupBar } from '@/app/marketing/components/StickySignupBar';
import { CTA_COPY } from '@/app/marketing/ctaCopy';
import { LaunchOfferStrip, useLaunchOfferActive, useSignupLabel } from '@/app/marketing/components/LaunchOffer';
import { LAUNCH_OFFER_COPY } from '@/app/lib/launchOffer';
import { PageCard } from '@/app/marketing/components/PageCard';
import { faqPageJsonLd, serializeJsonLd } from '@/app/marketing/jsonld';
import { LANDING_PAGE_IDS, marketingPath, type MarketingPageId } from '@/app/marketing/routes';

// Import extracted animation components and hooks
import {
  useMousePosition,
  AnimatedSection,
  MagneticButton,
  FloatingElement,
  RippleButton,
  TiltCard,
  GradientText,
  FAQItemNew,
  BouncingDots,
  RotatingBorder,
  AnimatedShapes,
  GlitchText,
  ShakeOnHover,
  ParticleExplosion,
  ChevronDown,
} from './homepage/animations';

// ============================================
// MAIN COMPONENT
// ============================================

/** The questions the home page answers; the same words feed its FAQPage structured data. */
const HOME_FAQ: { q: string; a: string; icon: typeof Sparkles; color: string }[] = [
  {
    q: 'What is CadenceE?',
    a: 'A CRM built in Switzerland that runs a whole club and its sales: leads followed through five phases, contracts with e-signature, members and households, events, tabs and billing, email, reports and an AI assistant, in one place.',
    icon: Layers,
    color: 'from-blue-500 to-cyan-500',
  },
  {
    q: 'Who is CadenceE for?',
    a: 'Private members’ clubs, restaurants, hotels and event venues, and sales teams that follow each lead to the deposit. Pavillon 46, a private members’ club in La Croix-sur-Lutry, Vaud, uses it every day.',
    icon: Users,
    color: 'from-violet-500 to-purple-600',
  },
  {
    q: 'How does a lead become a member?',
    a: 'The lead moves through outreach, meeting, contract, signature and deposit. The contract is signed online without an account, the deposit is paid by card or Swiss QR-bill, and one step turns the lead into an Active member with a welcome email and the member portal.',
    icon: Target,
    color: 'from-emerald-500 to-teal-600',
  },
  {
    q: 'Which payment methods and accounting tools are supported?',
    a: 'Members and clients pay by card, TWINT, SEPA or Swiss QR-bill. Bank files are matched against what is owed, and the accounting export goes to Bexio or Abacus.',
    icon: FileText,
    color: 'from-orange-500 to-amber-500',
  },
  {
    q: 'Which languages does CadenceE speak?',
    a: 'The whole app is available in English, French and German, and each person chooses their own. Prepared emails are available in French and English.',
    icon: Globe,
    color: 'from-cyan-500 to-blue-500',
  },
  {
    q: 'Is my data secure?',
    a: 'People sign in with a password, Google, Microsoft or a passkey and can add two-factor authentication. Roles and permissions decide who sees what, every change is written to an audit log, and requests to export or erase personal data are handled under the GDPR and the Swiss FADP.',
    icon: Shield,
    color: 'from-rose-500 to-pink-600',
  },
];

/** The dot that trails the pointer: its own component, so moving the mouse re-renders only it. */
function CursorFollower() {
  const mousePosition = useMousePosition();
  return (
    <div
      className="fixed w-6 h-6 rounded-full bg-orange-500/20 pointer-events-none z-50 mix-blend-multiply hidden lg:block"
      style={{
        left: mousePosition.x - 12,
        top: mousePosition.y - 12,
        transition: 'left 0.1s, top 0.1s',
      }}
      aria-hidden="true"
    />
  );
}

/** Below the first screen, the browser skips the layout and paint of a section until it nears the viewport. */
const BELOW_FOLD = '[content-visibility:auto] [contain-intrinsic-size:auto_1000px]';

export default function Homepage() {
  usePageLanguage('en');
  // The launch offer (free until 31 December 2026) shows only while it runs.
  const offerActive = useLaunchOfferActive();
  const signupLabel = useSignupLabel('en');

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 overflow-x-hidden">
      <CursorFollower />

      <MarketingHeader lang="en" pageId="home" />

      <main id={MAIN_CONTENT_ID} className="w-full" tabIndex={-1}>
        {/* ==================== HERO SECTION ==================== */}
        <section className="relative min-h-screen flex items-center pt-20 overflow-hidden">
          <div className="absolute inset-0 bg-gradient-to-br from-orange-50 via-white to-amber-50/50" />

          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 py-20 relative">
            <div className="grid lg:grid-cols-2 gap-12 lg:gap-20 items-center">
              {/* Left content */}
              <div className="text-center lg:text-left">
                {/* The launch offer, while it runs */}
                {offerActive && (
                  <div className="mb-5" data-launch-offer="">
                    <Link
                      to={marketingPath('pricing', 'en')}
                      data-track="nav-pricing"
                      className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-emerald-600 to-teal-600 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-emerald-600/25 hover:shadow-xl transition-shadow"
                    >
                      <Gift className="w-4 h-4 shrink-0" aria-hidden="true" />
                      {LAUNCH_OFFER_COPY.en.ribbon}
                    </Link>
                  </div>
                )}

                {/* Badge */}
                <div className="inline-flex items-center gap-2 bg-white border border-orange-200 text-orange-800 px-5 py-2.5 rounded-full text-sm font-semibold mb-8 shadow-lg">
                  <Crown className="w-5 h-5" aria-hidden="true" />
                  <span>Swiss CRM for private clubs and sales teams</span>
                </div>

                {/* Headline: the page's one H1, with its main keywords */}
                <h1 className="text-4xl sm:text-5xl md:text-6xl lg:text-7xl font-bold text-slate-900 mb-6 leading-[1.05] tracking-tight">
                  <span className="block">Run your club.</span>{' '}
                  <span className="block">Close your sales.</span>{' '}
                  <span className="block">
                    {/* While the launch offer runs, the H1 says what searchers look for: a free CRM. */}
                    <GradientText>{offerActive ? 'One free Swiss CRM.' : 'One Swiss CRM.'}</GradientText>
                  </span>
                </h1>

                {/* Subheadline */}
                <p className="text-lg sm:text-xl text-slate-700 mb-10 max-w-xl mx-auto lg:mx-0 leading-relaxed">
                  CadenceE is the CRM for{' '}
                  <Link to={marketingPath('private-clubs', 'en')} data-track="nav-private-clubs" className="text-orange-700 font-semibold hover:underline underline-offset-4">private members’ clubs</Link>,{' '}
                  <Link to={marketingPath('hospitality', 'en')} data-track="nav-hospitality" className="text-orange-700 font-semibold hover:underline underline-offset-4">hospitality</Link> and{' '}
                  <Link to={marketingPath('sales', 'en')} data-track="nav-sales" className="text-orange-700 font-semibold hover:underline underline-offset-4">sales teams</Link>:
                  leads followed to the deposit, contracts signed online, members, events, tabs and billing with QR-bill and TWINT, email and an AI assistant.
                </p>

                {/* CTA Buttons */}
                <div className="flex flex-col sm:flex-row gap-4 justify-center lg:justify-start mb-12">
                  <CtaLink
                    kind="signup"
                    lang="en"
                    className="group bg-slate-900 hover:bg-slate-800 text-white px-8 py-4 rounded-2xl font-semibold text-lg flex items-center justify-center gap-3 shadow-xl hover:shadow-2xl transition-all duration-200"
                  >
                    <span>{signupLabel}</span>
                    <ArrowRight className="w-5 h-5 group-hover:translate-x-1 transition-transform" aria-hidden="true" />
                  </CtaLink>
                  <CtaLink
                    kind="try"
                    lang="en"
                    className="group bg-white hover:bg-slate-50 text-slate-900 px-8 py-4 rounded-2xl font-semibold text-lg border-2 border-slate-200 hover:border-orange-300 flex items-center justify-center gap-3 shadow-lg transition-all duration-200"
                  >
                    <Play className="w-5 h-5 text-orange-600" aria-hidden="true" />
                    {CTA_COPY.en.tryDemo}
                  </CtaLink>
                </div>
                <Reassurance lang="en" className="-mt-6 mb-10" />

                {/* Who uses it, and in which languages: only what is true */}
                <div className="flex flex-col sm:flex-row items-center justify-center lg:justify-start gap-6">
                  <div className="flex -space-x-2" aria-hidden="true">
                    {['EN', 'FR', 'DE'].map((code) => (
                      <div
                        key={code}
                        className="w-10 h-10 rounded-full bg-gradient-to-br from-orange-500 to-amber-500 border-2 border-white flex items-center justify-center text-white text-xs font-bold shadow-md"
                      >
                        {code}
                      </div>
                    ))}
                  </div>
                  <div className="text-center sm:text-left">
                    <p className="text-sm font-semibold text-slate-900 mb-1">Built in Switzerland · English, French, German</p>
                    <p className="text-sm text-slate-600">
                      In daily use at <span className="font-semibold text-orange-800">Pavillon 46</span>, a private members’ club in La Croix-sur-Lutry, Vaud
                    </p>
                  </div>
                </div>
              </div>

              {/* Right - Dashboard preview - CRAZY ANIMATED VERSION */}
              <div className="relative">
                {/* Animated decorations around the card */}
                <div className="absolute inset-0 -m-6 pointer-events-none">
                  {/* Orbiting dots */}
                  <div 
                    className="absolute inset-0"
                    style={{ animation: 'orbit 20s linear infinite' }}
                  >
                    <div className="absolute top-0 left-1/2 -translate-x-1/2 -translate-y-1/2 w-2 h-2 rounded-full bg-gradient-to-r from-orange-400 to-amber-400 shadow-lg shadow-orange-400/50" />
                  </div>
                  <div 
                    className="absolute inset-0"
                    style={{ animation: 'orbit 25s linear infinite reverse' }}
                  >
                    <div className="absolute bottom-0 left-1/2 -translate-x-1/2 translate-y-1/2 w-1.5 h-1.5 rounded-full bg-gradient-to-r from-emerald-400 to-teal-400 shadow-lg shadow-emerald-400/50" />
                  </div>
                  <div 
                    className="absolute inset-0"
                    style={{ animation: 'orbit 18s linear infinite', animationDelay: '-5s' }}
                  >
                    <div className="absolute top-1/2 right-0 translate-x-1/2 -translate-y-1/2 w-1.5 h-1.5 rounded-full bg-gradient-to-r from-violet-400 to-purple-400 shadow-lg shadow-violet-400/50" />
                  </div>
                  
                  {/* Animated corner brackets */}
                  <div className="absolute -top-2 -left-2 w-8 h-8">
                    <div 
                      className="absolute top-0 left-0 w-full h-0.5 bg-gradient-to-r from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite' }}
                    />
                    <div 
                      className="absolute top-0 left-0 h-full w-0.5 bg-gradient-to-b from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite' }}
                    />
                  </div>
                  <div className="absolute -top-2 -right-2 w-8 h-8">
                    <div 
                      className="absolute top-0 right-0 w-full h-0.5 bg-gradient-to-l from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite 0.5s' }}
                    />
                    <div 
                      className="absolute top-0 right-0 h-full w-0.5 bg-gradient-to-b from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite 0.5s' }}
                    />
                  </div>
                  <div className="absolute -bottom-2 -left-2 w-8 h-8">
                    <div 
                      className="absolute bottom-0 left-0 w-full h-0.5 bg-gradient-to-r from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite 1s' }}
                    />
                    <div 
                      className="absolute bottom-0 left-0 h-full w-0.5 bg-gradient-to-t from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite 1s' }}
                    />
                  </div>
                  <div className="absolute -bottom-2 -right-2 w-8 h-8">
                    <div 
                      className="absolute bottom-0 right-0 w-full h-0.5 bg-gradient-to-l from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite 1.5s' }}
                    />
                    <div 
                      className="absolute bottom-0 right-0 h-full w-0.5 bg-gradient-to-t from-orange-400 to-transparent"
                      style={{ animation: 'corner-glow 2s ease-in-out infinite 1.5s' }}
                    />
                  </div>
                  
                  {/* Rotating dashed border */}
                  <div 
                    className="absolute inset-0 rounded-2xl border-2 border-dashed border-slate-200/30"
                    style={{ animation: 'rotate-border 30s linear infinite' }}
                  />
                </div>

                {/* Floating particles around the card */}
                {[
                  { size: 'w-2 h-2', color: 'from-orange-400 to-amber-400', top: '15%', left: '-16px', delay: 0 },
                  { size: 'w-1.5 h-1.5', color: 'from-emerald-400 to-teal-400', top: '35%', right: '-14px', delay: 0.5 },
                  { size: 'w-2 h-2', color: 'from-violet-400 to-purple-400', top: '55%', left: '-18px', delay: 1 },
                  { size: 'w-1 h-1', color: 'from-blue-400 to-cyan-400', top: '75%', right: '-12px', delay: 1.5 },
                  { size: 'w-1.5 h-1.5', color: 'from-rose-400 to-pink-400', top: '90%', left: '-14px', delay: 2 },
                ].map((particle, i) => (
                  <div
                    key={i}
                    className={`absolute ${particle.size} rounded-full bg-gradient-to-r ${particle.color} shadow-sm`}
                    style={{
                      top: particle.top,
                      left: particle.left,
                      right: particle.right,
                      animation: `float-particle ${3 + i * 0.4}s ease-in-out infinite ${particle.delay}s`,
                      opacity: 0.7,
                    }}
                  />
                ))}
                <style>{`
                  @keyframes float-particle {
                    0%, 100% { transform: translateY(0) scale(1); opacity: 0.6; }
                    50% { transform: translateY(-20px) scale(1.2); opacity: 1; }
                  }
                  @keyframes shimmer {
                    0% { background-position: -200% 0; }
                    100% { background-position: 200% 0; }
                  }
                  @keyframes count-up {
                    0% { opacity: 0; transform: translateY(10px); }
                    100% { opacity: 1; transform: translateY(0); }
                  }
                  @keyframes bar-grow {
                    0% { transform: scaleX(0); }
                    100% { transform: scaleX(1); }
                  }
                  @keyframes border-dance {
                    0%, 100% { border-color: rgba(249, 115, 22, 0.3); }
                    50% { border-color: rgba(249, 115, 22, 0.6); }
                  }
                  @keyframes card-float {
                    0%, 100% { transform: translateY(0) rotate(0deg); }
                    25% { transform: translateY(-5px) rotate(0.5deg); }
                    75% { transform: translateY(5px) rotate(-0.5deg); }
                  }
                  @keyframes notification-pop {
                    0%, 100% { transform: scale(1) translateY(0); }
                    10% { transform: scale(1.05) translateY(-5px); }
                    20% { transform: scale(1) translateY(0); }
                  }
                  @keyframes sparkle-spin {
                    0% { transform: rotate(0deg) scale(1); }
                    50% { transform: rotate(180deg) scale(1.1); }
                    100% { transform: rotate(360deg) scale(1); }
                  }
                  @keyframes glow-pulse {
                    0%, 100% { box-shadow: 0 0 20px rgba(249, 115, 22, 0.3); }
                    50% { box-shadow: 0 0 40px rgba(249, 115, 22, 0.6); }
                  }
                  @keyframes soft-breathe {
                    0%, 100% { transform: scale(1); opacity: 1; }
                    50% { transform: scale(1.02); opacity: 0.95; }
                  }
                  @keyframes subtle-sway {
                    0%, 100% { transform: translateX(0); }
                    50% { transform: translateX(3px); }
                  }
                  @keyframes icon-wiggle {
                    0%, 100% { transform: rotate(0deg); }
                    25% { transform: rotate(-5deg); }
                    75% { transform: rotate(5deg); }
                  }
                  @keyframes text-glow {
                    0%, 100% { text-shadow: 0 0 0 transparent; }
                    50% { text-shadow: 0 0 8px rgba(249, 115, 22, 0.4); }
                  }
                  @keyframes gradient-shift {
                    0%, 100% { filter: hue-rotate(0deg); }
                    50% { filter: hue-rotate(10deg); }
                  }
                  @keyframes ripple {
                    0% { transform: scale(0.8); opacity: 0.5; }
                    100% { transform: scale(2); opacity: 0; }
                  }
                  @keyframes slide-in-bounce {
                    0% { transform: translateX(-10px); opacity: 0; }
                    60% { transform: translateX(3px); opacity: 1; }
                    100% { transform: translateX(0); opacity: 1; }
                  }
                  @keyframes orbit {
                    0% { transform: rotate(0deg); }
                    100% { transform: rotate(360deg); }
                  }
                  @keyframes corner-glow {
                    0%, 100% { opacity: 0.4; }
                    50% { opacity: 1; }
                  }
                  @keyframes rotate-border {
                    0% { transform: rotate(0deg); }
                    100% { transform: rotate(360deg); }
                  }
                  @keyframes trace-line {
                    0% { stroke-dashoffset: 1000; }
                    100% { stroke-dashoffset: 0; }
                  }
                `}</style>

                {/* Main dashboard card with floating animation */}
                <div 
                  className="relative bg-white rounded-2xl overflow-hidden shadow-2xl border-2 border-orange-200/50"
                  style={{ animation: 'card-float 6s ease-in-out infinite, border-dance 2s ease-in-out infinite, glow-pulse 3s ease-in-out infinite' }}
                >
                  {/* Shimmer overlay */}
                  <div 
                    className="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent pointer-events-none"
                    style={{ 
                      backgroundSize: '200% 100%',
                      animation: 'shimmer 3s linear infinite',
                    }}
                  />

                  {/* Browser chrome */}
                  <div className="flex items-center gap-3 px-5 py-4 bg-slate-50 border-b border-slate-200 relative overflow-hidden">
                    {/* Subtle shimmer across browser chrome */}
                    <div 
                      className="absolute inset-0 bg-gradient-to-r from-transparent via-white/40 to-transparent pointer-events-none"
                      style={{ 
                        backgroundSize: '200% 100%',
                        animation: 'shimmer 8s linear infinite',
                      }}
                    />
                    <div className="flex gap-2">
                      <div 
                        className="w-3 h-3 rounded-full bg-red-400 hover:scale-150 transition-transform cursor-pointer"
                        style={{ animation: 'pulse 2s ease-in-out infinite, soft-breathe 3s ease-in-out infinite' }}
                      />
                      <div 
                        className="w-3 h-3 rounded-full bg-amber-400 hover:scale-150 transition-transform cursor-pointer"
                        style={{ animation: 'pulse 2s ease-in-out infinite 0.3s, soft-breathe 3s ease-in-out infinite 0.3s' }}
                      />
                      <div 
                        className="w-3 h-3 rounded-full bg-emerald-400 hover:scale-150 transition-transform cursor-pointer"
                        style={{ animation: 'pulse 2s ease-in-out infinite 0.6s, soft-breathe 3s ease-in-out infinite 0.6s' }}
                      />
                    </div>
                    <div className="flex-1 flex justify-center">
                      <div 
                        className="bg-white rounded-lg px-4 py-1.5 text-sm text-slate-500 border border-slate-200 flex items-center gap-2 relative overflow-hidden"
                        style={{ animation: 'soft-breathe 4s ease-in-out infinite' }}
                      >
                        {/* URL bar inner glow */}
                        <div 
                          className="absolute inset-0 bg-gradient-to-r from-emerald-50/0 via-emerald-50/50 to-emerald-50/0 pointer-events-none"
                          style={{ animation: 'shimmer 6s linear infinite' }}
                        />
                        <Lock className="w-3 h-3 text-emerald-500 relative" style={{ animation: 'pulse 1.5s ease-in-out infinite, icon-wiggle 6s ease-in-out infinite' }} />
                        <span className="relative">
                          www.cadencee.ai/dashboard
                          <span 
                            className="absolute right-0 top-0 h-full w-0.5 bg-orange-500"
                            style={{ animation: 'pulse 1s ease-in-out infinite' }}
                          />
                        </span>
                      </div>
                    </div>
                  </div>
                  
                  {/* Dashboard content */}
                  <div className="p-6 bg-gradient-to-br from-slate-50 to-white relative">
                    {/* Stats grid with staggered animations */}
                    <div className="grid grid-cols-4 gap-2 mb-5">
                      {[
                        { label: 'Active Leads', value: '2,847', icon: Users, color: 'blue', delay: 0, trend: '+12%' },
                        { label: 'Active Deals', value: '183', icon: Target, color: 'orange', delay: 0.1, trend: '+8%' },
                        { label: 'Pipeline', value: 'CHF 1.2M', icon: TrendingUp, color: 'emerald', delay: 0.2, trend: '+23%' },
                        { label: 'Deposits Paid', value: '47', icon: Award, color: 'violet', delay: 0.3, trend: '+15%' },
                      ].map((stat) => (
                        <div 
                          key={stat.label}
                          className={`bg-gradient-to-br from-${stat.color}-50 to-white p-3 rounded-xl border border-${stat.color}-100 hover:scale-105 hover:shadow-lg transition-all duration-300 cursor-pointer group relative overflow-hidden`}
                          style={{ animation: `count-up 0.6s ease-out ${stat.delay}s both, soft-breathe 4s ease-in-out infinite ${stat.delay * 2}s` }}
                        >
                          {/* Hover glow effect */}
                          <div className={`absolute inset-0 bg-${stat.color}-400 opacity-0 group-hover:opacity-10 transition-opacity`} />
                          
                          {/* Subtle corner accent */}
                          <div 
                            className={`absolute -top-1 -right-1 w-8 h-8 bg-gradient-to-br from-${stat.color}-200/50 to-transparent rounded-full blur-sm`}
                            style={{ animation: `pulse 3s ease-in-out infinite ${stat.delay}s` }}
                          />
                          
                          <stat.icon 
                            className={`w-4 h-4 text-${stat.color}-500 mb-1 group-hover:scale-110 transition-transform`} 
                            style={{ animation: `pulse 2s ease-in-out infinite ${stat.delay}s, icon-wiggle 4s ease-in-out infinite ${stat.delay + 1}s` }}
                          />
                          <div 
                            className="text-lg font-bold text-slate-900"
                            style={{ animation: `count-up 0.8s ease-out ${stat.delay + 0.2}s both, text-glow 3s ease-in-out infinite ${stat.delay}s` }}
                          >
                            {stat.value}
                          </div>
                          <div 
                            className="text-[10px] text-slate-500 flex items-center gap-1"
                            style={{ animation: `slide-in-bounce 0.5s ease-out ${stat.delay + 0.3}s both` }}
                          >
                            {stat.label}
                            <span className="text-emerald-500 font-semibold">{stat.trend}</span>
                          </div>
                          
                          {/* Sparkle on hover */}
                          <div className="absolute top-1 right-1 opacity-0 group-hover:opacity-100 transition-opacity">
                            <Sparkles className={`w-3 h-3 text-${stat.color}-400`} style={{ animation: 'sparkle-spin 1s linear infinite' }} />
                          </div>
                        </div>
                      ))}
                    </div>
                    
                    {/* Pipeline section */}
                    <div 
                      className="bg-white rounded-2xl p-4 border border-slate-200 shadow-sm relative overflow-hidden"
                      style={{ animation: 'soft-breathe 5s ease-in-out infinite 0.5s' }}
                    >
                      {/* Animated background gradient */}
                      <div 
                        className="absolute inset-0 bg-gradient-to-r from-orange-50 via-transparent to-amber-50 opacity-50"
                        style={{ animation: 'shimmer 5s linear infinite, gradient-shift 12s ease-in-out infinite', backgroundSize: '200% 100%' }}
                      />
                      
                      {/* Subtle corner glow */}
                      <div 
                        className="absolute -top-2 -left-2 w-16 h-16 bg-gradient-to-br from-orange-300/30 to-transparent rounded-full blur-md"
                        style={{ animation: 'pulse 4s ease-in-out infinite' }}
                      />
                      
                      <div className="flex items-center justify-between mb-4 relative">
                        <p 
                          className="font-semibold text-slate-900 flex items-center gap-2"
                          style={{ animation: 'subtle-sway 6s ease-in-out infinite' }}
                        >
                          <Kanban 
                            className="w-4 h-4 text-orange-500" 
                            style={{ animation: 'pulse 2s ease-in-out infinite, icon-wiggle 5s ease-in-out infinite 1s' }}
                          />
                          Lead Phases
                        </p>
                        <span 
                          className="text-xs font-medium text-orange-700 bg-orange-50 px-2 py-1 rounded-full flex items-center gap-1"
                          style={{ animation: 'pulse 1.5s ease-in-out infinite, text-glow 2s ease-in-out infinite' }}
                        >
                          <TrendingUp className="w-3 h-3" />
                          CHF 1.2M Total
                        </span>
                      </div>
                      <div className="flex gap-2 relative">
                        {['Outreach', 'Meeting', 'Contract', 'Signature', 'Deposit'].map((stage, i) => (
                          <div key={stage} className="flex-1 group cursor-pointer">
                            <div className="relative h-3 mb-2">
                              <div className="absolute inset-0 rounded-full bg-slate-100" />
                              <div 
                                className={`absolute inset-0 rounded-full origin-left ${
                                  i === 4 ? 'bg-gradient-to-r from-emerald-400 to-emerald-500' : 'bg-gradient-to-r from-orange-300 to-amber-400'
                                } group-hover:shadow-lg transition-shadow`}
                                style={{ 
                                  opacity: 1 - i * 0.15,
                                  animation: `bar-grow 1s ease-out ${i * 0.2}s both`,
                                }} 
                              />
                              {/* Animated shine */}
                              <div 
                                className="absolute inset-0 rounded-full bg-gradient-to-r from-transparent via-white/40 to-transparent"
                                style={{ 
                                  animation: 'shimmer 2s linear infinite',
                                  animationDelay: `${i * 0.5}s`,
                                  backgroundSize: '200% 100%',
                                }}
                              />
                            </div>
                            <span className="text-[10px] text-slate-500 font-medium group-hover:text-orange-600 transition-colors">{stage}</span>
                          </div>
                        ))}
                      </div>
                      
                      {/* Mini activity feed */}
                      <div className="mt-4 pt-3 border-t border-slate-100 relative">
                        <div className="flex items-center justify-between mb-2">
                          <span className="text-[10px] font-semibold text-slate-500 uppercase tracking-wide">Recent Activity</span>
                          <span 
                            className="text-[9px] text-orange-500 font-medium"
                            style={{ animation: 'pulse 2s ease-in-out infinite' }}
                          >
                            Live
                          </span>
                        </div>
                        <div className="space-y-1.5">
                          {[
                            { text: 'Sarah: contract signed, deposit paid', time: '2m ago', color: 'emerald' },
                            { text: 'New lead from the website form', time: '5m ago', color: 'blue' },
                            { text: 'AI drafted 3 follow-up emails', time: '8m ago', color: 'orange' },
                          ].map((activity, i) => (
                            <div 
                              key={i}
                              className="flex items-center gap-2 text-[9px] text-slate-500"
                              style={{ animation: `slide-in-bounce 0.4s ease-out ${i * 0.1}s both` }}
                            >
                              <div 
                                className={`w-1.5 h-1.5 rounded-full bg-${activity.color}-400`}
                                style={{ animation: 'pulse 2s ease-in-out infinite' }}
                              />
                              <span className="flex-1 truncate">{activity.text}</span>
                              <span className="text-slate-500">{activity.time}</span>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Animated notification cards */}
                <div 
                  className="absolute -left-10 top-16 hidden lg:block"
                  style={{ animation: 'notification-pop 4s ease-in-out infinite, float-particle 5s ease-in-out infinite' }}
                >
                  <div className="bg-white rounded-xl shadow-2xl p-3 border-2 border-emerald-200 flex items-center gap-3 hover:scale-110 transition-transform cursor-pointer relative overflow-hidden">
                    {/* Glow effect */}
                    <div className="absolute inset-0 bg-emerald-400 opacity-0 hover:opacity-10 transition-opacity" />
                    {/* Shimmer overlay */}
                    <div 
                      className="absolute inset-0 bg-gradient-to-r from-transparent via-emerald-50/30 to-transparent pointer-events-none"
                      style={{ backgroundSize: '200% 100%', animation: 'shimmer 4s linear infinite' }}
                    />
                    <div 
                      className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-400 to-emerald-600 flex items-center justify-center"
                      style={{ animation: 'pulse 1.5s ease-in-out infinite, soft-breathe 3s ease-in-out infinite' }}
                    >
                      <Check className="w-5 h-5 text-white" style={{ animation: 'bounce 1s ease-in-out infinite' }} />
                    </div>
                    <div>
                      <p 
                        className="text-sm font-bold text-slate-900 flex items-center gap-1"
                        style={{ animation: 'text-glow 2s ease-in-out infinite' }}
                      >
                        Deposit Paid! 
                        <span style={{ animation: 'sparkle-spin 2s linear infinite' }}>🎉</span>
                      </p>
                      <p 
                        className="text-xs text-emerald-700 font-semibold"
                        style={{ animation: 'subtle-sway 3s ease-in-out infinite' }}
                      >
                        +CHF 4,800 • Membership
                      </p>
                    </div>
                    {/* Confetti particles */}
                    <div className="absolute -top-1 -right-1">
                      <div className="w-2 h-2 bg-emerald-400 rounded-full" style={{ animation: 'ping 1s cubic-bezier(0, 0, 0.2, 1) infinite' }} />
                    </div>
                    <div className="absolute -bottom-1 -left-1">
                      <div className="w-1.5 h-1.5 bg-emerald-300 rounded-full" style={{ animation: 'ping 1.5s cubic-bezier(0, 0, 0.2, 1) infinite 0.5s' }} />
                    </div>
                  </div>
                </div>

                <div 
                  className="absolute -right-8 bottom-20 hidden lg:block"
                  style={{ animation: 'notification-pop 4s ease-in-out infinite 2s, float-particle 6s ease-in-out infinite 1s' }}
                >
                  <div className="bg-white rounded-xl shadow-2xl p-3 border-2 border-orange-200 flex items-center gap-3 hover:scale-110 transition-transform cursor-pointer relative overflow-hidden">
                    {/* Glow effect */}
                    <div className="absolute inset-0 bg-orange-400 opacity-0 hover:opacity-10 transition-opacity" />
                    {/* Shimmer overlay */}
                    <div 
                      className="absolute inset-0 bg-gradient-to-r from-transparent via-orange-50/30 to-transparent pointer-events-none"
                      style={{ backgroundSize: '200% 100%', animation: 'shimmer 4s linear infinite 1s' }}
                    />
                    <div 
                      className="w-10 h-10 rounded-xl bg-gradient-to-br from-orange-400 to-amber-500 flex items-center justify-center relative"
                      style={{ animation: 'glow-pulse 2s ease-in-out infinite, soft-breathe 3s ease-in-out infinite 0.5s' }}
                    >
                      <Sparkles className="w-5 h-5 text-white" style={{ animation: 'sparkle-spin 2s linear infinite' }} />
                    </div>
                    <div>
                      <p 
                        className="text-sm font-bold text-slate-900 flex items-center gap-1"
                        style={{ animation: 'text-glow 2s ease-in-out infinite 0.5s' }}
                      >
                        AI Email Ready
                        <span style={{ animation: 'pulse 1s ease-in-out infinite' }}>✨</span>
                      </p>
                      <p className="text-xs text-orange-700 font-semibold flex items-center gap-1">
                        Follow-up drafted 
                        <span className="flex gap-0.5">
                          <span className="w-1 h-1 rounded-full bg-orange-500" style={{ animation: 'bounce 0.6s ease-in-out infinite' }} />
                          <span className="w-1 h-1 rounded-full bg-orange-500" style={{ animation: 'bounce 0.6s ease-in-out infinite 0.1s' }} />
                          <span className="w-1 h-1 rounded-full bg-orange-500" style={{ animation: 'bounce 0.6s ease-in-out infinite 0.2s' }} />
                        </span>
                      </p>
                    </div>
                    {/* Sparkle particles */}
                    <div className="absolute -top-1 -right-1">
                      <div className="w-2 h-2 bg-orange-400 rounded-full" style={{ animation: 'ping 1.5s cubic-bezier(0, 0, 0.2, 1) infinite' }} />
                    </div>
                    <div className="absolute -bottom-1 -left-1">
                      <div className="w-1.5 h-1.5 bg-amber-300 rounded-full" style={{ animation: 'ping 2s cubic-bezier(0, 0, 0.2, 1) infinite 0.8s' }} />
                    </div>
                  </div>
                </div>

                {/* Extra floating badges */}
                <div 
                  className="absolute -right-4 top-8 hidden lg:block"
                  style={{ animation: 'float-particle 4s ease-in-out infinite 0.5s' }}
                >
                  <div className="bg-gradient-to-r from-violet-500 to-purple-600 text-white text-xs font-bold px-3 py-1.5 rounded-full shadow-lg flex items-center gap-1">
                    <Zap className="w-3 h-3" style={{ animation: 'sparkle-spin 1s linear infinite' }} />
                    FR · DE · EN
                  </div>
                </div>

                <div 
                  className="absolute -left-4 bottom-8 hidden lg:block"
                  style={{ animation: 'float-particle 5s ease-in-out infinite 1s' }}
                >
                  <div className="bg-gradient-to-r from-emerald-500 to-teal-600 text-white text-xs font-bold px-3 py-1.5 rounded-full shadow-lg flex items-center gap-1">
                    <TrendingUp className="w-3 h-3" style={{ animation: 'bounce 1s ease-in-out infinite' }} />
                    +34% MTD
                  </div>
                </div>

                {/* Additional subtle floating elements */}
                <div 
                  className="absolute top-1/3 -left-6 hidden lg:block"
                  style={{ animation: 'float-particle 6s ease-in-out infinite 0.8s' }}
                >
                  <div className="w-3 h-3 rounded-full bg-gradient-to-r from-blue-400 to-cyan-400 opacity-60 blur-[1px]" />
                </div>
                <div 
                  className="absolute top-1/2 -right-6 hidden lg:block"
                  style={{ animation: 'float-particle 5s ease-in-out infinite 1.2s' }}
                >
                  <div className="w-2 h-2 rounded-full bg-gradient-to-r from-violet-400 to-purple-400 opacity-50 blur-[1px]" />
                </div>
                <div 
                  className="absolute bottom-1/4 -left-8 hidden lg:block"
                  style={{ animation: 'float-particle 7s ease-in-out infinite 0.5s' }}
                >
                  <div className="w-4 h-4 rounded-full bg-gradient-to-r from-amber-400/40 to-orange-400/40 blur-sm" />
                </div>
                
                {/* Subtle connecting lines */}
                <div 
                  className="absolute top-12 -right-3 w-12 h-px bg-gradient-to-r from-orange-300/60 to-transparent hidden lg:block"
                  style={{ animation: 'subtle-sway 3s ease-in-out infinite, pulse 2s ease-in-out infinite' }}
                />
                <div 
                  className="absolute bottom-16 -left-3 w-10 h-px bg-gradient-to-l from-emerald-300/60 to-transparent hidden lg:block"
                  style={{ animation: 'subtle-sway 4s ease-in-out infinite 1s, pulse 2s ease-in-out infinite 0.5s' }}
                />
              </div>
            </div>
          </div>
        </section>

        {/* ==================== STATS SECTION ==================== */}
        <section className="py-20 bg-gradient-to-r from-orange-600 via-amber-500 to-orange-600 relative" aria-label="CadenceE in numbers">
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12">
            <ul className="grid grid-cols-2 lg:grid-cols-4 gap-6 lg:gap-8">
              {[
                { value: '5', label: 'Lead phases, outreach to deposit', icon: Target },
                { value: '3', label: 'Languages: EN, FR, DE', icon: Globe },
                { value: '4', label: 'Ways to pay: card, TWINT, SEPA, QR-bill', icon: Mail },
                { value: '2', label: 'Accounting exports: Bexio, Abacus', icon: Shield },
              ].map((stat) => (
                <li key={stat.label} className="text-center text-white">
                  <div className="w-14 h-14 rounded-xl bg-white/20 flex items-center justify-center mx-auto mb-4" aria-hidden="true">
                    <stat.icon className="w-7 h-7 text-white" />
                  </div>
                  <div className="text-4xl lg:text-5xl font-bold mb-1">{stat.value}</div>
                  <div className="text-white font-medium">{stat.label}</div>
                </li>
              ))}
            </ul>
          </div>
        </section>

        {/* ==================== FEATURES SECTION ==================== */}
        <section id="features" className={`py-32 bg-slate-950 relative overflow-hidden ${BELOW_FOLD}`}>
          {/* Animated background */}
          <div className="absolute inset-0">
            {/* Gradient orbs */}
            <div 
              className="absolute top-0 left-1/4 w-[600px] h-[600px] bg-gradient-to-br from-orange-500/20 to-amber-500/20 rounded-full blur-[120px]"
              style={{ animation: 'pulse 8s ease-in-out infinite' }}
            />
            <div 
              className="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-gradient-to-br from-violet-500/15 to-purple-500/15 rounded-full blur-[100px]"
              style={{ animation: 'pulse 10s ease-in-out infinite 2s' }}
            />
            <div 
              className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-gradient-to-br from-cyan-500/10 to-blue-500/10 rounded-full blur-[150px]"
              style={{ animation: 'pulse 12s ease-in-out infinite 4s' }}
            />
            
            {/* Grid pattern */}
            <div 
              className="absolute inset-0 opacity-[0.03]"
              style={{
                backgroundImage: `linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px)`,
                backgroundSize: '60px 60px',
              }}
            />

            {/* Animated floating white dots */}
            <style>{`
              @keyframes float-dot-1 {
                0%, 100% { transform: translate(0, 0); }
                25% { transform: translate(100px, -50px); }
                50% { transform: translate(50px, -100px); }
                75% { transform: translate(-50px, -50px); }
              }
              @keyframes float-dot-2 {
                0%, 100% { transform: translate(0, 0); }
                25% { transform: translate(-80px, 60px); }
                50% { transform: translate(-40px, 120px); }
                75% { transform: translate(40px, 60px); }
              }
              @keyframes float-dot-3 {
                0%, 100% { transform: translate(0, 0); }
                33% { transform: translate(60px, 80px); }
                66% { transform: translate(-60px, 40px); }
              }
              @keyframes float-dot-4 {
                0%, 100% { transform: translate(0, 0); }
                20% { transform: translate(-100px, -30px); }
                40% { transform: translate(-50px, -80px); }
                60% { transform: translate(50px, -60px); }
                80% { transform: translate(30px, -20px); }
              }
              @keyframes float-dot-5 {
                0%, 100% { transform: translate(0, 0) scale(1); }
                50% { transform: translate(80px, 100px) scale(1.5); }
              }
              @keyframes float-dot-6 {
                0%, 100% { transform: translate(0, 0); }
                50% { transform: translate(-120px, -80px); }
              }
              @keyframes float-dot-7 {
                0%, 100% { transform: translate(0, 0) scale(1); opacity: 0.3; }
                50% { transform: translate(60px, -120px) scale(0.8); opacity: 0.6; }
              }
            `}</style>
            {[
              // Row 1 - top area
              { top: '3%', left: '5%', size: 3, opacity: 0.35, anim: 'float-dot-1', dur: '20s' },
              { top: '5%', left: '15%', size: 2, opacity: 0.25, anim: 'float-dot-2', dur: '25s' },
              { top: '2%', left: '25%', size: 4, opacity: 0.4, anim: 'float-dot-3', dur: '18s' },
              { top: '8%', left: '35%', size: 2, opacity: 0.3, anim: 'float-dot-4', dur: '22s' },
              { top: '4%', left: '45%', size: 3, opacity: 0.35, anim: 'float-dot-5', dur: '28s' },
              { top: '6%', left: '55%', size: 2, opacity: 0.25, anim: 'float-dot-6', dur: '24s' },
              { top: '3%', left: '65%', size: 4, opacity: 0.4, anim: 'float-dot-7', dur: '20s' },
              { top: '7%', left: '75%', size: 3, opacity: 0.35, anim: 'float-dot-1', dur: '26s' },
              { top: '5%', left: '85%', size: 2, opacity: 0.3, anim: 'float-dot-2', dur: '30s' },
              { top: '2%', left: '95%', size: 3, opacity: 0.35, anim: 'float-dot-3', dur: '22s' },
              // Row 2
              { top: '15%', left: '3%', size: 4, opacity: 0.4, anim: 'float-dot-4', dur: '19s' },
              { top: '18%', left: '12%', size: 2, opacity: 0.25, anim: 'float-dot-5', dur: '23s' },
              { top: '12%', left: '22%', size: 3, opacity: 0.35, anim: 'float-dot-6', dur: '27s' },
              { top: '20%', left: '32%', size: 2, opacity: 0.3, anim: 'float-dot-7', dur: '21s' },
              { top: '14%', left: '42%', size: 4, opacity: 0.4, anim: 'float-dot-1', dur: '25s' },
              { top: '16%', left: '58%', size: 3, opacity: 0.35, anim: 'float-dot-2', dur: '18s' },
              { top: '19%', left: '68%', size: 2, opacity: 0.25, anim: 'float-dot-3', dur: '24s' },
              { top: '13%', left: '78%', size: 4, opacity: 0.4, anim: 'float-dot-4', dur: '20s' },
              { top: '17%', left: '88%', size: 3, opacity: 0.35, anim: 'float-dot-5', dur: '26s' },
              { top: '11%', left: '97%', size: 2, opacity: 0.3, anim: 'float-dot-6', dur: '22s' },
              // Row 3
              { top: '28%', left: '2%', size: 3, opacity: 0.35, anim: 'float-dot-7', dur: '28s' },
              { top: '25%', left: '18%', size: 4, opacity: 0.4, anim: 'float-dot-1', dur: '19s' },
              { top: '30%', left: '28%', size: 2, opacity: 0.25, anim: 'float-dot-2', dur: '23s' },
              { top: '26%', left: '48%', size: 3, opacity: 0.35, anim: 'float-dot-3', dur: '21s' },
              { top: '32%', left: '62%', size: 4, opacity: 0.4, anim: 'float-dot-4', dur: '25s' },
              { top: '27%', left: '72%', size: 2, opacity: 0.3, anim: 'float-dot-5', dur: '27s' },
              { top: '29%', left: '92%', size: 3, opacity: 0.35, anim: 'float-dot-6', dur: '20s' },
              // Row 4 - middle
              { top: '40%', left: '5%', size: 4, opacity: 0.4, anim: 'float-dot-7', dur: '24s' },
              { top: '38%', left: '15%', size: 2, opacity: 0.25, anim: 'float-dot-1', dur: '18s' },
              { top: '42%', left: '25%', size: 3, opacity: 0.35, anim: 'float-dot-2', dur: '26s' },
              { top: '36%', left: '38%', size: 4, opacity: 0.4, anim: 'float-dot-3', dur: '22s' },
              { top: '44%', left: '52%', size: 2, opacity: 0.3, anim: 'float-dot-4', dur: '28s' },
              { top: '39%', left: '65%', size: 3, opacity: 0.35, anim: 'float-dot-5', dur: '19s' },
              { top: '41%', left: '78%', size: 4, opacity: 0.4, anim: 'float-dot-6', dur: '23s' },
              { top: '37%', left: '88%', size: 2, opacity: 0.25, anim: 'float-dot-7', dur: '21s' },
              { top: '43%', left: '96%', size: 3, opacity: 0.35, anim: 'float-dot-1', dur: '25s' },
              // Row 5
              { top: '52%', left: '3%', size: 3, opacity: 0.35, anim: 'float-dot-2', dur: '27s' },
              { top: '55%', left: '12%', size: 4, opacity: 0.4, anim: 'float-dot-3', dur: '20s' },
              { top: '50%', left: '22%', size: 2, opacity: 0.25, anim: 'float-dot-4', dur: '24s' },
              { top: '58%', left: '35%', size: 3, opacity: 0.35, anim: 'float-dot-5', dur: '18s' },
              { top: '53%', left: '48%', size: 4, opacity: 0.4, anim: 'float-dot-6', dur: '26s' },
              { top: '56%', left: '58%', size: 2, opacity: 0.3, anim: 'float-dot-7', dur: '22s' },
              { top: '51%', left: '72%', size: 3, opacity: 0.35, anim: 'float-dot-1', dur: '28s' },
              { top: '57%', left: '85%', size: 4, opacity: 0.4, anim: 'float-dot-2', dur: '19s' },
              { top: '54%', left: '95%', size: 2, opacity: 0.25, anim: 'float-dot-3', dur: '23s' },
              // Row 6
              { top: '65%', left: '2%', size: 4, opacity: 0.4, anim: 'float-dot-4', dur: '21s' },
              { top: '68%', left: '18%', size: 3, opacity: 0.35, anim: 'float-dot-5', dur: '25s' },
              { top: '62%', left: '32%', size: 2, opacity: 0.25, anim: 'float-dot-6', dur: '27s' },
              { top: '70%', left: '45%', size: 4, opacity: 0.4, anim: 'float-dot-7', dur: '20s' },
              { top: '64%', left: '55%', size: 3, opacity: 0.35, anim: 'float-dot-1', dur: '24s' },
              { top: '67%', left: '68%', size: 2, opacity: 0.3, anim: 'float-dot-2', dur: '18s' },
              { top: '63%', left: '82%', size: 4, opacity: 0.4, anim: 'float-dot-3', dur: '26s' },
              { top: '69%', left: '92%', size: 3, opacity: 0.35, anim: 'float-dot-4', dur: '22s' },
              // Row 7
              { top: '75%', left: '5%', size: 3, opacity: 0.35, anim: 'float-dot-5', dur: '28s' },
              { top: '78%', left: '15%', size: 2, opacity: 0.25, anim: 'float-dot-6', dur: '19s' },
              { top: '72%', left: '28%', size: 4, opacity: 0.4, anim: 'float-dot-7', dur: '23s' },
              { top: '80%', left: '42%', size: 3, opacity: 0.35, anim: 'float-dot-1', dur: '21s' },
              { top: '76%', left: '52%', size: 2, opacity: 0.3, anim: 'float-dot-2', dur: '25s' },
              { top: '79%', left: '65%', size: 4, opacity: 0.4, anim: 'float-dot-3', dur: '27s' },
              { top: '73%', left: '75%', size: 3, opacity: 0.35, anim: 'float-dot-4', dur: '20s' },
              { top: '77%', left: '88%', size: 2, opacity: 0.25, anim: 'float-dot-5', dur: '24s' },
              { top: '81%', left: '97%', size: 4, opacity: 0.4, anim: 'float-dot-6', dur: '18s' },
              // Row 8 - bottom
              { top: '88%', left: '3%', size: 4, opacity: 0.4, anim: 'float-dot-7', dur: '26s' },
              { top: '85%', left: '12%', size: 3, opacity: 0.35, anim: 'float-dot-1', dur: '22s' },
              { top: '92%', left: '25%', size: 2, opacity: 0.25, anim: 'float-dot-2', dur: '28s' },
              { top: '86%', left: '38%', size: 4, opacity: 0.4, anim: 'float-dot-3', dur: '19s' },
              { top: '90%', left: '48%', size: 3, opacity: 0.35, anim: 'float-dot-4', dur: '23s' },
              { top: '87%', left: '62%', size: 2, opacity: 0.3, anim: 'float-dot-5', dur: '21s' },
              { top: '93%', left: '72%', size: 4, opacity: 0.4, anim: 'float-dot-6', dur: '25s' },
              { top: '89%', left: '85%', size: 3, opacity: 0.35, anim: 'float-dot-7', dur: '27s' },
              { top: '95%', left: '95%', size: 2, opacity: 0.25, anim: 'float-dot-1', dur: '20s' },
            ].map((dot, i) => (
              <div
                key={i}
                className="absolute rounded-full bg-white pointer-events-none"
                style={{
                  top: dot.top,
                  left: dot.left,
                  width: `${dot.size}px`,
                  height: `${dot.size}px`,
                  opacity: dot.opacity,
                  animation: `${dot.anim} ${dot.dur} ease-in-out infinite`,
                  animationDelay: `${i * 0.3}s`,
                }}
              />
            ))}
          </div>

          {/* Floating particles */}
          <style>{`
            @keyframes float-up {
              0%, 100% { transform: translateY(0) translateX(0) rotate(0deg); opacity: 0.3; }
              50% { transform: translateY(-30px) translateX(10px) rotate(180deg); opacity: 0.8; }
            }
            @keyframes glow-border {
              0%, 100% { border-color: rgba(249, 115, 22, 0.3); box-shadow: 0 0 20px rgba(249, 115, 22, 0.1); }
              50% { border-color: rgba(249, 115, 22, 0.6); box-shadow: 0 0 40px rgba(249, 115, 22, 0.3); }
            }
            @keyframes icon-bounce {
              0%, 100% { transform: translateY(0) scale(1); }
              50% { transform: translateY(-5px) scale(1.1); }
            }
            @keyframes shimmer-slide {
              0% { transform: translateX(-100%); }
              100% { transform: translateX(100%); }
            }
            @keyframes counter-glow {
              0%, 100% { text-shadow: 0 0 20px rgba(249, 115, 22, 0.5); }
              50% { text-shadow: 0 0 40px rgba(249, 115, 22, 0.8), 0 0 60px rgba(249, 115, 22, 0.4); }
            }
          `}</style>
          
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 relative">
            {/* Header */}
            <div className="text-center mb-20">
              <div 
                className="inline-flex items-center gap-2 bg-orange-500/10 border border-orange-500/20 text-orange-400 px-5 py-2.5 rounded-full text-sm font-semibold mb-8 backdrop-blur-sm"
                style={{ animation: 'glow-border 3s ease-in-out infinite' }}
              >
                <Gem className="w-4 h-4" style={{ animation: 'icon-bounce 2s ease-in-out infinite' }} />
                CRM features
              </div>
              <h2 className="text-4xl md:text-5xl lg:text-6xl font-bold text-white mb-6">
                Everything your club and sales team need,{' '}
                <span
                  className="text-transparent bg-clip-text bg-gradient-to-r from-orange-400 via-amber-400 to-orange-400"
                  style={{ animation: 'counter-glow 3s ease-in-out infinite' }}
                >
                  in one CRM
                </span>
              </h2>
              <p className="text-lg text-slate-300 max-w-2xl mx-auto">
                From the first enquiry to the monthly statement: leads, contracts, members, events, billing and email.{' '}
                <Link to={marketingPath('features', 'en')} data-track="nav-features" className="font-semibold text-orange-300 underline underline-offset-4 hover:text-orange-200">
                  See every feature
                </Link>
              </p>
            </div>

            {/* Bento grid */}
            <div className="grid grid-cols-12 gap-4 lg:gap-6">
              
              {/* HERO CARD - Visual Pipeline (spans 8 cols) */}
              <div className="col-span-12 lg:col-span-8 group">
                <div 
                  className="relative h-full min-h-[320px] rounded-3xl overflow-hidden border-2 border-orange-500/20 hover:border-orange-500/40 transition-all duration-500"
                  style={{ animation: 'glow-border 4s ease-in-out infinite' }}
                >
                  {/* Animated gradient background */}
                  <div className="absolute inset-0 bg-gradient-to-br from-orange-600 via-amber-500 to-orange-600 bg-[length:200%_200%]" style={{ animation: 'gradient-flow 8s ease infinite' }} />
                  
                  {/* Pattern overlay */}
                  <div className="absolute inset-0 opacity-10" style={{
                    backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23ffffff' fill-opacity='0.4'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E")`,
                  }} />
                  
                  {/* Shimmer effect */}
                  <div className="absolute inset-0 overflow-hidden">
                    <div 
                      className="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent -skew-x-12"
                      style={{ animation: 'shimmer-slide 3s ease-in-out infinite' }}
                    />
                  </div>
                  
                  <div className="relative p-8 lg:p-10 h-full flex flex-col">
                    <div className="flex items-start justify-between mb-auto">
                      <div>
                        <div 
                          className="w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-6 border border-white/30"
                          style={{ animation: 'icon-bounce 3s ease-in-out infinite' }}
                        >
                          <Kanban className="w-8 h-8 text-white" />
                        </div>
                        <h3 className="text-3xl lg:text-4xl font-bold text-white mb-3"><Link to={marketingPath('sales', 'en')} data-track="nav-sales" className="hover:text-orange-300 transition-colors">Five-Phase Lead Tracker</Link></h3>
                        <p className="text-white/80 text-lg max-w-md">
                          Outreach, meeting, contract, signature, deposit: every lead on a board you drag across, ending with money in the bank.
                        </p>
                      </div>
                      <div className="hidden lg:block">
                        <div className="bg-white/10 backdrop-blur-md rounded-2xl p-5 border border-white/20">
                          <div 
                            className="text-4xl font-bold text-white mb-1"
                            style={{ animation: 'counter-glow 2s ease-in-out infinite' }}
                          >
                            CHF 428K
                          </div>
                          <div className="text-sm text-white/70">Pipeline value</div>
                        </div>
                      </div>
                    </div>
                    
                    {/* Pipeline stages with animations */}
                    <div className="flex gap-3 mt-6">
                      {[
                        { name: 'Outreach', count: 12, color: 'white' },
                        { name: 'Meeting', count: 10, color: 'white' },
                        { name: 'Contract', count: 8, color: 'white' },
                        { name: 'Signature', count: 5, color: 'white' },
                        { name: 'Deposit', count: 3, color: 'emerald' },
                      ].map((stage, i) => (
                        <div 
                          key={stage.name}
                          className={`flex-1 backdrop-blur-md rounded-xl p-3 border transition-all duration-300 cursor-pointer group/stage hover:scale-105 ${
                            stage.color === 'emerald' ? 'bg-emerald-500/20 border-emerald-400/40 hover:bg-emerald-500/30' :
                            stage.color === 'red' ? 'bg-red-500/20 border-red-400/40 hover:bg-red-500/30' :
                            'bg-white/10 border-white/20 hover:bg-white/20'
                          }`}
                          style={{ animationDelay: `${i * 100}ms` }}
                        >
                          <div className="text-xs text-white/90 mb-1 truncate">{stage.name}</div>
                          <div 
                            className="text-xl font-bold text-white group-hover/stage:scale-110 transition-transform"
                            style={{ animation: `count-up 0.5s ease-out ${i * 0.1}s both` }}
                          >
                            {stage.count}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {/* Lead Management (spans 4 cols) */}
              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[280px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-blue-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-blue-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-blue-500/20 rounded-full blur-3xl group-hover:bg-blue-500/30 transition-all" />
                  
                  <div className="relative p-6 h-full flex flex-col">
                    <div 
                      className="w-14 h-14 rounded-xl bg-gradient-to-br from-blue-500 to-cyan-500 flex items-center justify-center mb-4 shadow-lg shadow-blue-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300"
                      style={{ animation: 'icon-bounce 4s ease-in-out infinite' }}
                    >
                      <Users className="w-7 h-7 text-white" />
                    </div>
                    <h3 className="text-xl font-bold text-white mb-2"><Link to={marketingPath('sales', 'en')} data-track="nav-sales" className="hover:text-orange-300 transition-colors">Lead Management</Link></h3>
                    <p className="text-slate-300 mb-4 flex-1">Score and assign leads, block duplicates, record referrals and partners, import with a dry run.</p>
                    
                    {/* Mini visual */}
                    <div className="flex gap-2">
                      {[75, 60, 45, 30].map((h, i) => (
                        <div 
                          key={i} 
                          className="flex-1 bg-gradient-to-t from-blue-500 to-cyan-400 rounded-t-full origin-bottom"
                          style={{ 
                            height: `${h}px`,
                            animation: `bar-grow 1s ease-out ${i * 0.1}s both`,
                          }}
                        />
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {/* Intelligent Sales Writer (spans 4 cols) */}
              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[280px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-violet-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-violet-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-violet-500/20 rounded-full blur-3xl group-hover:bg-violet-500/30 transition-all" />
                  
                  <div className="relative p-6 h-full flex flex-col">
                    <div className="flex items-start justify-between mb-4">
                      <div 
                        className="w-14 h-14 rounded-xl bg-gradient-to-br from-violet-500 to-purple-600 flex items-center justify-center shadow-lg shadow-violet-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300"
                        style={{ animation: 'icon-bounce 4s ease-in-out infinite 0.5s' }}
                      >
                        <Sparkles className="w-7 h-7 text-white" />
                      </div>
                      <span className="text-xs font-bold text-violet-400 bg-violet-500/10 px-2 py-1 rounded-full border border-violet-500/20">AI</span>
                    </div>
                    <h3 className="text-xl font-bold text-white mb-2"><Link to={marketingPath('ai', 'en')} data-track="nav-ai" className="hover:text-orange-300 transition-colors">AI Sales Writer</Link></h3>
                    <p className="text-slate-300 mb-4 flex-1">Built on Claude: emails, follow-ups and notes in your tone, for you to review and send.</p>
                    
                    {/* Mini visual - typing animation */}
                    <div className="bg-slate-800/50 rounded-xl p-3 border border-slate-700/50">
                      <div className="flex items-center gap-2 text-sm text-slate-300">
                        <span>Generating</span>
                        <span className="flex gap-1">
                          <span className="w-1.5 h-1.5 rounded-full bg-violet-400" style={{ animation: 'bounce 0.6s ease-in-out infinite' }} />
                          <span className="w-1.5 h-1.5 rounded-full bg-violet-400" style={{ animation: 'bounce 0.6s ease-in-out infinite 0.1s' }} />
                          <span className="w-1.5 h-1.5 rounded-full bg-violet-400" style={{ animation: 'bounce 0.6s ease-in-out infinite 0.2s' }} />
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Contact Database (spans 4 cols) */}
              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[280px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-emerald-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-emerald-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-emerald-500/20 rounded-full blur-3xl group-hover:bg-emerald-500/30 transition-all" />
                  
                  <div className="relative p-6 h-full flex flex-col">
                    <div 
                      className="w-14 h-14 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center mb-4 shadow-lg shadow-emerald-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300"
                      style={{ animation: 'icon-bounce 4s ease-in-out infinite 1s' }}
                    >
                      <UserCircle className="w-7 h-7 text-white" />
                    </div>
                    <h3 className="text-xl font-bold text-white mb-2"><Link to={marketingPath('membership', 'en')} data-track="nav-membership" className="hover:text-orange-300 transition-colors">Members & Households</Link></h3>
                    <p className="text-slate-300 mb-4 flex-1">Applications, renewals with at-risk scores, households and corporate memberships.</p>
                    
                    {/* Mini visual - avatars */}
                    <div className="flex -space-x-2">
                      {['bg-emerald-500', 'bg-teal-500', 'bg-cyan-500', 'bg-blue-500', 'bg-violet-500'].map((bg, i) => (
                        <div 
                          key={i}
                          className={`w-8 h-8 rounded-full ${bg} border-2 border-slate-800 flex items-center justify-center text-xs font-bold text-white`}
                          style={{ animation: `count-up 0.5s ease-out ${i * 0.1}s both` }}
                        >
                          {String.fromCharCode(65 + i)}
                        </div>
                      ))}
                      <div className="w-8 h-8 rounded-full bg-slate-700 border-2 border-slate-800 flex items-center justify-center text-xs font-bold text-slate-200">
                        +99
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Company Accounts (spans 4 cols) */}
              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[200px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-amber-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-amber-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-amber-500/20 rounded-full blur-3xl group-hover:bg-amber-500/30 transition-all" />
                  
                  <div className="relative p-6 h-full">
                    <div 
                      className="w-12 h-12 rounded-xl bg-gradient-to-br from-amber-500 to-orange-600 flex items-center justify-center mb-4 shadow-lg shadow-amber-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300"
                    >
                      <Building2 className="w-6 h-6 text-white" />
                    </div>
                    <h3 className="text-lg font-bold text-white mb-1"><Link to={marketingPath('e-signature', 'en')} data-track="nav-e-signature" className="hover:text-orange-300 transition-colors">E-Signature Contracts</Link></h3>
                    <p className="text-slate-300 text-sm">Signed online without an account, with the deposit at signing.</p>
                  </div>
                </div>
              </div>

              {/* Task Management (spans 4 cols) */}
              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[200px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-rose-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-rose-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-rose-500/20 rounded-full blur-3xl group-hover:bg-rose-500/30 transition-all" />
                  
                  <div className="relative p-6 h-full">
                    <div 
                      className="w-12 h-12 rounded-xl bg-gradient-to-br from-rose-500 to-pink-600 flex items-center justify-center mb-4 shadow-lg shadow-rose-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300"
                    >
                      <CheckSquare className="w-6 h-6 text-white" />
                    </div>
                    <h3 className="text-lg font-bold text-white mb-1"><Link to={marketingPath('hospitality', 'en')} data-track="nav-hospitality" className="hover:text-orange-300 transition-colors">Events & Reservations</Link></h3>
                    <p className="text-slate-300 text-sm">Waitlists, private-event hire, visits and check-ins.</p>
                  </div>
                </div>
              </div>

              {/* Row of 3: Activity, Templates, Analytics */}
              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[200px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-cyan-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-cyan-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-cyan-500/20 rounded-full blur-3xl group-hover:bg-cyan-500/30 transition-all" />
                  <div className="relative p-6 h-full">
                    <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-cyan-500 to-blue-500 flex items-center justify-center mb-4 shadow-lg shadow-cyan-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                      <Activity className="w-6 h-6 text-white" />
                    </div>
                    <h3 className="text-lg font-bold text-white mb-1"><Link to={marketingPath('hospitality', 'en')} data-track="nav-hospitality" className="hover:text-orange-300 transition-colors">Tabs & Lightspeed POS</Link></h3>
                    <p className="text-slate-300 text-sm">Bar and restaurant tabs flow into house accounts.</p>
                  </div>
                </div>
              </div>

              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[200px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-pink-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-pink-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-pink-500/20 rounded-full blur-3xl group-hover:bg-pink-500/30 transition-all" />
                  <div className="relative p-6 h-full">
                    <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-pink-500 to-rose-600 flex items-center justify-center mb-4 shadow-lg shadow-pink-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                      <FileText className="w-6 h-6 text-white" />
                    </div>
                    <h3 className="text-lg font-bold text-white mb-1"><Link to={marketingPath('billing', 'en')} data-track="nav-billing" className="hover:text-orange-300 transition-colors">Swiss Payments</Link></h3>
                    <p className="text-slate-300 text-sm">Card, TWINT, SEPA and QR-bill, matched with your bank file.</p>
                  </div>
                </div>
              </div>

              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[200px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-indigo-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-indigo-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-indigo-500/20 rounded-full blur-3xl group-hover:bg-indigo-500/30 transition-all" />
                  <div className="relative p-6 h-full">
                    <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-indigo-500 to-blue-600 flex items-center justify-center mb-4 shadow-lg shadow-indigo-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                      <BarChart3 className="w-6 h-6 text-white" />
                    </div>
                    <h3 className="text-lg font-bold text-white mb-1"><Link to={marketingPath('features', 'en')} data-track="nav-features" className="hover:text-orange-300 transition-colors">Reports & Forecast</Link></h3>
                    <p className="text-slate-300 text-sm">Sales, membership and hospitality reports, with an audit log.</p>
                  </div>
                </div>
              </div>

              {/* Bottom row: Organization, Copy History (4 cols each) */}
              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[200px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-teal-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-teal-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-teal-500/20 rounded-full blur-3xl group-hover:bg-teal-500/30 transition-all" />
                  <div className="relative p-6 h-full flex flex-col">
                    <div className="flex items-start justify-between mb-3">
                      <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-teal-500 to-emerald-600 flex items-center justify-center shadow-lg shadow-teal-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                        <Users className="w-6 h-6 text-white" />
                      </div>
                      <span className="text-xs font-bold text-teal-400 bg-teal-500/10 px-2 py-1 rounded-full border border-teal-500/20">Teams</span>
                    </div>
                    <h3 className="text-lg font-bold text-white mb-1"><Link to={marketingPath('swiss', 'en')} data-track="nav-swiss" className="hover:text-orange-300 transition-colors">Roles & Workspaces</Link></h3>
                    <p className="text-slate-300 text-sm mb-3 flex-1">Invite your team with roles and permissions; sign in with Google, Microsoft or a passkey.</p>
                    <div className="flex items-center gap-2">
                      <div className="flex -space-x-2">
                        {['bg-teal-500', 'bg-emerald-500', 'bg-cyan-500'].map((bg, i) => (
                          <div key={i} className={`w-7 h-7 rounded-full ${bg} border-2 border-slate-800`} />
                        ))}
                      </div>
                      <span className="text-xs text-slate-300"><span className="text-teal-300 font-semibold">+5</span></span>
                    </div>
                  </div>
                </div>
              </div>

              <div className="col-span-12 sm:col-span-6 lg:col-span-4 group">
                <div className="relative h-full min-h-[200px] rounded-3xl bg-gradient-to-br from-slate-900 to-slate-800 border border-slate-700/50 hover:border-fuchsia-500/30 overflow-hidden transition-all duration-500 hover:shadow-2xl hover:shadow-fuchsia-500/10">
                  <div className="absolute -top-20 -right-20 w-40 h-40 bg-fuchsia-500/20 rounded-full blur-3xl group-hover:bg-fuchsia-500/30 transition-all" />
                  <div className="relative p-6 h-full flex flex-col">
                    <div className="flex items-start justify-between mb-3">
                      <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-fuchsia-500 to-purple-600 flex items-center justify-center shadow-lg shadow-fuchsia-500/25 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                        <Layers className="w-6 h-6 text-white" />
                      </div>
                      <span className="text-xs font-bold text-fuchsia-400 bg-fuchsia-500/10 px-2 py-1 rounded-full border border-fuchsia-500/20">History</span>
                    </div>
                    <h3 className="text-lg font-bold text-white mb-1"><Link to={marketingPath('email', 'en')} data-track="nav-email" className="hover:text-orange-300 transition-colors">Email Automation</Link></h3>
                    <p className="text-slate-300 text-sm mb-3 flex-1">One outbox, sequences, newsletters and campaigns with consent.</p>
                    <div className="space-y-1">
                      {['Sequence', 'Newsletter'].map((type) => (
                        <div key={type} className="flex items-center gap-2 text-xs">
                          <div className="w-1.5 h-1.5 rounded-full bg-fuchsia-400" />
                          <span className="text-slate-300">{type}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

            </div>
          </div>
        </section>

        {/* ==================== SOLUTIONS (internal links to every topic page) ==================== */}
        <section className={`py-24 bg-slate-50 relative ${BELOW_FOLD}`} aria-labelledby="solutions-heading">
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12">
            <div className="max-w-3xl mb-12">
              <p className="inline-flex items-center gap-2 bg-orange-100 text-orange-800 px-4 py-2 rounded-full text-sm font-semibold mb-6">
                <Briefcase className="w-4 h-4" aria-hidden="true" />
                Solutions
              </p>
              <h2 id="solutions-heading" className="text-4xl md:text-5xl font-bold text-slate-900 mb-5">
                A CRM for clubs, hospitality and sales, <GradientText>built in Switzerland</GradientText>
              </h2>
              <p className="text-xl text-slate-700">
                Each page explains one part of CadenceE in depth, with how it works and the questions people ask.
                Also in <Link to={marketingPath('home', 'fr')} hrefLang="fr" lang="fr" data-track="nav-lang-fr" className="font-semibold text-orange-700 underline underline-offset-4">français</Link> and{' '}
                <Link to={marketingPath('home', 'de')} hrefLang="de" lang="de" data-track="nav-lang-de" className="font-semibold text-orange-700 underline underline-offset-4">Deutsch</Link>.
              </p>
            </div>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {(['features', ...LANDING_PAGE_IDS] as MarketingPageId[]).map((id) => (
                <PageCard key={id} id={id} lang="en" />
              ))}
            </div>
          </div>
        </section>

        {/* ==================== PROBLEM/SOLUTION ==================== */}
        <section className={`py-28 bg-white relative overflow-hidden ${BELOW_FOLD}`}>
          <AnimatedShapes />
          
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 relative">
            <AnimatedSection className="text-center mb-20">
              <ParticleExplosion>
                <div className="inline-flex items-center gap-2 bg-red-50 text-red-600 px-4 py-2 rounded-full text-sm font-semibold mb-6 cursor-pointer hover:scale-105 transition-transform">
                  <Flame className="w-4 h-4 animate-pulse" />
                  The Problem
                </div>
              </ParticleExplosion>
              <h2 className="text-4xl md:text-5xl lg:text-6xl font-bold text-slate-900 mb-6">
                Your club’s tools <GlitchText><GradientText>don’t talk</GradientText></GlitchText> to each other
              </h2>
              <p className="text-xl text-slate-600 max-w-3xl mx-auto">
                Enquiries in a spreadsheet, contracts in a word processor, members in one tool and bar tabs in another: every hand-over loses a lead or a payment.
              </p>
            </AnimatedSection>

            <div className="grid lg:grid-cols-2 gap-8">
              <AnimatedSection animation="fade-left">
                <TiltCard>
                  <div className="relative h-full">
                    <div className="absolute inset-0 bg-gradient-to-br from-slate-200 to-slate-300 rounded-[2rem] transform rotate-2" />
                    <div className="relative bg-slate-100 rounded-[2rem] p-10 h-full">
                      <div className="flex items-center gap-4 mb-8">
                        <ShakeOnHover>
                          <div className="w-14 h-14 rounded-2xl bg-slate-300 flex items-center justify-center">
                            <XCircle className="w-7 h-7 text-slate-500" />
                          </div>
                        </ShakeOnHover>
                        <div>
                          <h3 className="text-2xl font-bold text-slate-700">The Old Way</h3>
                          <p className="text-slate-600">Fragmented and frustrating</p>
                        </div>
                      </div>
                      <ul className="space-y-5">
                        {[
                          { text: 'Enquiries, contracts and members in different tools', icon: Layers },
                          { text: 'Deposits chased by email after signing', icon: Activity },
                          { text: 'Monthly statements put together by hand', icon: FileText },
                          { text: 'No warning before a member decides to leave', icon: BarChart3 },
                          { text: 'Every email written from scratch', icon: MousePointerClick },
                        ].map((item, i) => (
                          <li 
                            key={i} 
                            className="flex items-center gap-4 group hover:translate-x-2 transition-all duration-300"
                            style={{ transitionDelay: `${i * 50}ms` }}
                          >
                            <div className="w-10 h-10 rounded-xl bg-slate-200 flex items-center justify-center group-hover:bg-red-100 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                              <item.icon className="w-5 h-5 text-slate-500 group-hover:text-red-500 transition-colors" />
                            </div>
                            <span className="text-slate-600 group-hover:text-slate-800 transition-colors">{item.text}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  </div>
                </TiltCard>
              </AnimatedSection>

              <AnimatedSection animation="fade-right">
                <TiltCard>
                  <div className="relative h-full">
                    <div className="absolute inset-0 bg-gradient-to-br from-orange-400 to-amber-500 rounded-[2rem] transform -rotate-2 shadow-2xl shadow-orange-500/30" />
                    <div className="relative bg-gradient-to-br from-orange-500 to-amber-500 rounded-[2rem] p-10 h-full text-white overflow-hidden">
                      {/* Animated particles */}
                      {[...Array(10)].map((_, i) => (
                        <div
                          key={i}
                          className="absolute w-2 h-2 bg-white/20 rounded-full"
                          style={{
                            left: `${Math.random() * 100}%`,
                            top: `${Math.random() * 100}%`,
                            animation: `float ${3 + Math.random() * 4}s ease-in-out infinite`,
                            animationDelay: `${Math.random() * 2}s`,
                          }}
                        />
                      ))}
                      <style>{`
                        @keyframes float {
                          0%, 100% { transform: translateY(0) scale(1); opacity: 0.3; }
                          50% { transform: translateY(-20px) scale(1.5); opacity: 0.6; }
                        }
                      `}</style>
                      
                      <div className="flex items-center gap-4 mb-8 relative">
                        <ShakeOnHover>
                          <div className="w-14 h-14 rounded-2xl bg-white/20 backdrop-blur-sm flex items-center justify-center">
                            <Sparkles className="w-7 h-7 text-white animate-pulse" />
                          </div>
                        </ShakeOnHover>
                        <div>
                          <h3 className="text-2xl font-bold">The CadenceE Way</h3>
                          <p className="text-white">One record, from enquiry to member</p>
                        </div>
                      </div>
                      <ul className="space-y-5 relative">
                        {[
                          { text: 'Leads, contracts, members and billing in one CRM', icon: Layers },
                          { text: 'Contracts signed online, deposit at signing', icon: Zap },
                          { text: 'AI drafts your emails, you confirm', icon: Sparkles },
                          { text: 'Tabs, dues and statements with QR-bill and TWINT', icon: BarChart3 },
                          { text: 'At-risk renewals flagged in time', icon: Target },
                        ].map((item, i) => (
                          <li 
                            key={i} 
                            className="flex items-center gap-4 group hover:translate-x-2 transition-all duration-300"
                            style={{ transitionDelay: `${i * 50}ms` }}
                          >
                            <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center group-hover:bg-white/30 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                              <item.icon className="w-5 h-5 text-white" />
                            </div>
                            <span className="text-white/90 group-hover:text-white transition-colors">{item.text}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  </div>
                </TiltCard>
              </AnimatedSection>
            </div>
          </div>
        </section>

        {/* ==================== AI SPOTLIGHT ==================== */}
        <section className={`py-28 bg-slate-900 relative overflow-hidden ${BELOW_FOLD}`}>
          <AnimatedShapes />
          
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 relative">
            <div className="grid lg:grid-cols-2 gap-16 items-center">
              <AnimatedSection animation="fade-left">
                <ParticleExplosion>
                  <div className="inline-flex items-center gap-2 bg-orange-500/20 text-orange-400 px-4 py-2 rounded-full text-sm font-semibold mb-6 backdrop-blur-sm border border-orange-500/30 cursor-pointer">
                    <Zap className="w-4 h-4 animate-pulse" />
                    AI sales writer
                  </div>
                </ParticleExplosion>
                <h2 className="text-4xl md:text-5xl font-bold text-white mb-6">
                  The AI sales writer drafts in{' '}
                  <GlitchText><span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-400 to-amber-400">seconds</span></GlitchText>
                </h2>
                <p className="text-xl text-slate-300 mb-10">
                  Built on Claude. Pick the goal, the length and your brand voice (professional, friendly or persuasive); the writer drafts, and a person reviews before anything is sent. The <Link to={marketingPath('ai', 'en')} data-track="nav-ai" className="font-semibold text-orange-300 underline underline-offset-4">AI assistant</Link> works the same way: it proposes, you confirm.
                </p>

                <div className="space-y-5">
                  {[
                    { icon: Mail, title: 'Sales Emails', desc: 'First contact with a new lead' },
                    { icon: MessageSquare, title: 'Follow-ups', desc: 'Keep conversations moving' },
                    { icon: FileText, title: 'CRM Notes', desc: 'Document meetings instantly' },
                    { icon: Briefcase, title: 'Deal Messages', desc: 'Move stakeholders to action' },
                    { icon: Activity, title: 'Workflow Messages', desc: 'For your sequences and automations' },
                  ].map((item, _i) => (
                    <div 
                      key={item.title} 
                      className="flex items-center gap-4 group hover:translate-x-4 transition-all duration-300"
                    >
                      <ShakeOnHover>
                        <div className="w-12 h-12 rounded-xl bg-white/5 border border-white/10 flex items-center justify-center group-hover:bg-orange-500/20 group-hover:border-orange-500/30 group-hover:scale-110 group-hover:rotate-6 transition-all duration-300">
                          <item.icon className="w-6 h-6 text-orange-400" />
                        </div>
                      </ShakeOnHover>
                      <div>
                        <h3 className="font-semibold text-white">{item.title}</h3>
                        <p className="text-sm text-slate-300">{item.desc}</p>
                      </div>
                    </div>
                  ))}
                </div>

                <MagneticButton className="mt-10">
                  <RippleButton
                    to="/login?mode=register"
                    track="cta-signup"
                    className="inline-flex items-center gap-2 bg-gradient-to-r from-orange-600 to-amber-500 text-white px-8 py-4 rounded-2xl font-semibold hover:shadow-xl hover:shadow-orange-500/25 group"
                  >
                    Try the AI sales writer
                    <ArrowRight className="w-5 h-5 group-hover:translate-x-1 transition-transform" />
                  </RippleButton>
                </MagneticButton>
              </AnimatedSection>

              <AnimatedSection animation="fade-right">
                <TiltCard>
                  <RotatingBorder>
                    <div className="bg-slate-800 rounded-2xl p-8">
                      <div className="flex items-center gap-3 mb-6">
                        <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center">
                          <Sparkles className="w-5 h-5 text-white animate-spin" style={{ animationDuration: '3s' }} />
                        </div>
                        <div>
                          <p className="font-semibold text-white">AI Sales Writer</p>
                          <p className="text-sm text-slate-400 flex items-center gap-1">
                            Built on Claude <BouncingDots className="text-orange-400" />
                          </p>
                        </div>
                      </div>

                      <div className="space-y-4">
                        {[
                          { label: 'Copy Type', value: 'Sales Email', options: '5 types: Sales, Follow-up, Note, Deal, Workflow' },
                          { label: 'Goal', value: 'Schedule a meeting', options: '6 goals available' },
                          { label: 'Brand Tone', value: 'Professional', options: 'Professional, Friendly, Persuasive' },
                          { label: 'Length', value: 'Medium', options: 'Short, Medium, Long' },
                        ].map((field) => (
                          <div key={field.label} className="group">
                            <label className="text-xs font-medium text-slate-500 block mb-2">{field.label}</label>
                            <div className="bg-slate-700/50 rounded-xl px-4 py-3 text-white border border-slate-600/50 flex items-center justify-between hover:border-orange-500/50 hover:bg-slate-700 transition-all duration-300 cursor-pointer group-hover:scale-[1.02]">
                              {field.value}
                              <ChevronDown className="w-4 h-4 text-slate-500 group-hover:rotate-180 transition-transform duration-300" />
                            </div>
                          </div>
                        ))}
                        <ParticleExplosion>
                          <button className="w-full bg-gradient-to-r from-orange-600 to-amber-500 text-white py-4 rounded-xl font-semibold flex items-center justify-center gap-2 hover:shadow-lg hover:shadow-orange-500/25 transition-all duration-300 hover:scale-[1.02] group">
                            <Sparkles className="w-5 h-5 group-hover:rotate-180 transition-transform duration-500" />
                            Generate Copy
                          </button>
                        </ParticleExplosion>
                      </div>
                    </div>
                  </RotatingBorder>
                </TiltCard>

                {/* Output preview */}
                <FloatingElement className="absolute -bottom-6 -right-6 hidden lg:block" delay={0.5} duration={5} distance={12}>
                  <TiltCard>
                    <div className="bg-white rounded-2xl shadow-2xl p-5 max-w-xs border border-slate-100">
                      <div className="flex items-center gap-2 text-emerald-700 text-sm font-medium mb-3">
                        <CheckCircle2 className="w-4 h-4 animate-bounce" />
                        Generated in 2.3s
                      </div>
                      <p className="text-slate-700 text-sm leading-relaxed">
                        "Hi Sarah, I noticed your team at TechCorp is scaling rapidly. I'd love to show you how..."
                      </p>
                    </div>
                  </TiltCard>
                </FloatingElement>
              </AnimatedSection>
            </div>
          </div>
        </section>

        {/* ==================== COPY WORKFLOW ==================== */}
        <section className={`py-28 bg-gradient-to-b from-slate-50 to-white relative overflow-hidden ${BELOW_FOLD}`}>
          <AnimatedShapes />
          
          {/* Additional animated background elements */}
          <style>{`
            @keyframes workflow-float {
              0%, 100% { transform: translateY(0) rotate(0deg); }
              50% { transform: translateY(-15px) rotate(3deg); }
            }
            @keyframes workflow-pulse {
              0%, 100% { opacity: 0.3; transform: scale(1); }
              50% { opacity: 0.6; transform: scale(1.1); }
            }
            @keyframes icon-rotate {
              0% { transform: rotate(0deg); }
              100% { transform: rotate(360deg); }
            }
            @keyframes list-slide {
              0% { opacity: 0; transform: translateX(-10px); }
              100% { opacity: 1; transform: translateX(0); }
            }
            @keyframes card-glow {
              0%, 100% { box-shadow: 0 0 0 rgba(249, 115, 22, 0); }
              50% { box-shadow: 0 0 30px rgba(249, 115, 22, 0.15); }
            }
            @keyframes badge-bounce {
              0%, 100% { transform: translateY(0); }
              50% { transform: translateY(-5px); }
            }
            @keyframes checkmark-pop {
              0% { transform: scale(0); opacity: 0; }
              50% { transform: scale(1.2); }
              100% { transform: scale(1); opacity: 1; }
            }
          `}</style>
          
          {/* Floating decorative elements */}
          <div className="absolute top-32 left-[5%] w-20 h-20 pointer-events-none hidden lg:block" style={{ animation: 'workflow-float 8s ease-in-out infinite' }}>
            <div className="w-full h-full rounded-2xl bg-gradient-to-br from-orange-400/10 to-amber-400/10 backdrop-blur-sm border border-orange-200/20" />
          </div>
          <div className="absolute bottom-40 right-[8%] w-16 h-16 pointer-events-none hidden lg:block" style={{ animation: 'workflow-float 6s ease-in-out infinite 1s' }}>
            <div className="w-full h-full rounded-full bg-gradient-to-br from-violet-400/10 to-purple-400/10 backdrop-blur-sm border border-violet-200/20" />
          </div>
          <div className="absolute top-1/2 left-[3%] w-12 h-12 pointer-events-none hidden lg:block" style={{ animation: 'workflow-float 7s ease-in-out infinite 2s' }}>
            <div className="w-full h-full rounded-lg bg-gradient-to-br from-emerald-400/10 to-teal-400/10 backdrop-blur-sm border border-emerald-200/20 rotate-45" />
          </div>
          
          {/* Animated connecting lines */}
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[80%] h-px bg-gradient-to-r from-transparent via-orange-200/30 to-transparent hidden lg:block" style={{ animation: 'workflow-pulse 4s ease-in-out infinite' }} />
          
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 relative">
            <AnimatedSection className="text-center mb-16">
              <ParticleExplosion>
                <div 
                  className="inline-flex items-center gap-2 bg-violet-100 text-violet-700 px-4 py-2 rounded-full text-sm font-semibold mb-6 cursor-pointer"
                  style={{ animation: 'badge-bounce 3s ease-in-out infinite' }}
                >
                  <Layers className="w-4 h-4" style={{ animation: 'icon-rotate 8s linear infinite' }} />
                  Sales writer workflow
                </div>
              </ParticleExplosion>
              <h2 className="text-4xl md:text-5xl font-bold text-slate-900 mb-6">
                Sales emails, from idea to <GradientText>sent</GradientText> in a few clicks
              </h2>
              <p className="text-xl text-slate-600 max-w-3xl mx-auto">
                Draft, adjust, keep and send from the CRM’s outbox, on the lead or deal it belongs to
              </p>
            </AnimatedSection>

            <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
              {/* 6 Goals */}
              <AnimatedSection animation="fade-up" delay={0}>
                <TiltCard className="h-full">
                  <div 
                    className="relative h-full bg-white rounded-2xl p-6 border border-slate-200 hover:shadow-2xl transition-all duration-500 group overflow-hidden"
                    style={{ animation: 'card-glow 4s ease-in-out infinite' }}
                  >
                    <div className="absolute inset-0 bg-gradient-to-br from-orange-500/0 to-amber-500/0 group-hover:from-orange-500/5 group-hover:to-amber-500/5 transition-all duration-500" />
                    {/* Animated corner accent */}
                    <div className="absolute -top-10 -right-10 w-20 h-20 bg-gradient-to-br from-orange-400/20 to-transparent rounded-full blur-xl" style={{ animation: 'workflow-pulse 3s ease-in-out infinite' }} />
                    <ShakeOnHover>
                      <div 
                        className="w-14 h-14 rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center mb-4 shadow-lg group-hover:scale-110 group-hover:rotate-6 transition-all duration-300 relative"
                        style={{ animation: 'workflow-float 5s ease-in-out infinite' }}
                      >
                        <Target className="w-7 h-7 text-white" style={{ animation: 'pulse 2s ease-in-out infinite' }} />
                        {/* Icon glow ring */}
                        <div className="absolute inset-0 rounded-xl bg-orange-400/30 blur-md -z-10" style={{ animation: 'workflow-pulse 2s ease-in-out infinite' }} />
                      </div>
                    </ShakeOnHover>
                    <h3 className="text-xl font-bold text-slate-900 mb-3">6 Smart Goals</h3>
                    <ul className="space-y-2 text-sm text-slate-600">
                      {['Schedule a meeting', 'Follow up after demo', 'Request feedback', 'Share resources', 'Check in on progress', 'Close the deal'].map((item, i) => (
                        <li 
                          key={item}
                          className="flex items-center gap-2 hover:translate-x-1 transition-transform"
                          style={{ animation: `list-slide 0.5s ease-out ${i * 0.1}s both` }}
                        >
                          <Check className="w-4 h-4 text-emerald-500" style={{ animation: `checkmark-pop 0.3s ease-out ${i * 0.1 + 0.2}s both` }} />
                          {item}
                        </li>
                      ))}
                    </ul>
                  </div>
                </TiltCard>
              </AnimatedSection>

              {/* Templates */}
              <AnimatedSection animation="fade-up" delay={100}>
                <TiltCard className="h-full">
                  <div 
                    className="relative h-full bg-white rounded-2xl p-6 border border-slate-200 hover:shadow-2xl transition-all duration-500 group overflow-hidden"
                    style={{ animation: 'card-glow 4s ease-in-out infinite 0.5s' }}
                  >
                    <div className="absolute inset-0 bg-gradient-to-br from-violet-500/0 to-purple-500/0 group-hover:from-violet-500/5 group-hover:to-purple-500/5 transition-all duration-500" />
                    {/* Animated corner accent */}
                    <div className="absolute -top-10 -right-10 w-20 h-20 bg-gradient-to-br from-violet-400/20 to-transparent rounded-full blur-xl" style={{ animation: 'workflow-pulse 3s ease-in-out infinite 0.5s' }} />
                    <ShakeOnHover>
                      <div 
                        className="w-14 h-14 rounded-xl bg-gradient-to-br from-violet-500 to-purple-500 flex items-center justify-center mb-4 shadow-lg group-hover:scale-110 group-hover:rotate-6 transition-all duration-300 relative"
                        style={{ animation: 'workflow-float 5s ease-in-out infinite 0.3s' }}
                      >
                        <FileText className="w-7 h-7 text-white" style={{ animation: 'pulse 2s ease-in-out infinite 0.2s' }} />
                        {/* Icon glow ring */}
                        <div className="absolute inset-0 rounded-xl bg-violet-400/30 blur-md -z-10" style={{ animation: 'workflow-pulse 2s ease-in-out infinite 0.3s' }} />
                      </div>
                    </ShakeOnHover>
                    <h3 className="text-xl font-bold text-slate-900 mb-3">Templates Library</h3>
                    <ul className="space-y-2 text-sm text-slate-600">
                      {['Sales templates', 'Follow-up templates', 'Meeting templates', 'Re-engagement templates'].map((item, i) => (
                        <li 
                          key={item}
                          className="flex items-center gap-2 hover:translate-x-1 transition-transform"
                          style={{ animation: `list-slide 0.5s ease-out ${i * 0.1 + 0.1}s both` }}
                        >
                          <Check className="w-4 h-4 text-emerald-500" style={{ animation: `checkmark-pop 0.3s ease-out ${i * 0.1 + 0.3}s both` }} />
                          {item}
                        </li>
                      ))}
                      <li 
                        className="text-slate-500 italic mt-2 flex items-center gap-1"
                        style={{ animation: 'list-slide 0.5s ease-out 0.5s both' }}
                      >
                        <Sparkles className="w-3 h-3 text-violet-400" style={{ animation: 'sparkle-spin 2s linear infinite' }} />
                        One-click to generate
                      </li>
                    </ul>
                  </div>
                </TiltCard>
              </AnimatedSection>

              {/* Copy Adjustments */}
              <AnimatedSection animation="fade-up" delay={200}>
                <TiltCard className="h-full">
                  <div 
                    className="relative h-full bg-white rounded-2xl p-6 border border-slate-200 hover:shadow-2xl transition-all duration-500 group overflow-hidden"
                    style={{ animation: 'card-glow 4s ease-in-out infinite 1s' }}
                  >
                    <div className="absolute inset-0 bg-gradient-to-br from-emerald-500/0 to-teal-500/0 group-hover:from-emerald-500/5 group-hover:to-teal-500/5 transition-all duration-500" />
                    {/* Animated corner accent */}
                    <div className="absolute -top-10 -right-10 w-20 h-20 bg-gradient-to-br from-emerald-400/20 to-transparent rounded-full blur-xl" style={{ animation: 'workflow-pulse 3s ease-in-out infinite 1s' }} />
                    <ShakeOnHover>
                      <div 
                        className="w-14 h-14 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-500 flex items-center justify-center mb-4 shadow-lg group-hover:scale-110 group-hover:rotate-6 transition-all duration-300 relative"
                        style={{ animation: 'workflow-float 5s ease-in-out infinite 0.6s' }}
                      >
                        <Settings className="w-7 h-7 text-white" style={{ animation: 'icon-rotate 8s linear infinite' }} />
                        {/* Icon glow ring */}
                        <div className="absolute inset-0 rounded-xl bg-emerald-400/30 blur-md -z-10" style={{ animation: 'workflow-pulse 2s ease-in-out infinite 0.6s' }} />
                      </div>
                    </ShakeOnHover>
                    <h3 className="text-xl font-bold text-slate-900 mb-3">Quick Adjustments</h3>
                    <ul className="space-y-2 text-sm text-slate-600">
                      {['Make it shorter', 'Make it friendlier', 'Make it persuasive', 'Regenerate instantly'].map((item, i) => (
                        <li 
                          key={item}
                          className="flex items-center gap-2 hover:translate-x-1 transition-transform"
                          style={{ animation: `list-slide 0.5s ease-out ${i * 0.1 + 0.2}s both` }}
                        >
                          <Check className="w-4 h-4 text-emerald-500" style={{ animation: `checkmark-pop 0.3s ease-out ${i * 0.1 + 0.4}s both` }} />
                          {item}
                        </li>
                      ))}
                      <li 
                        className="text-slate-500 italic mt-2 flex items-center gap-1"
                        style={{ animation: 'list-slide 0.5s ease-out 0.6s both' }}
                      >
                        <Sparkles className="w-3 h-3 text-emerald-400" style={{ animation: 'sparkle-spin 2s linear infinite' }} />
                        Perfect every message
                      </li>
                    </ul>
                  </div>
                </TiltCard>
              </AnimatedSection>

              {/* History & Send */}
              <AnimatedSection animation="fade-up" delay={300}>
                <TiltCard className="h-full">
                  <div 
                    className="relative h-full bg-white rounded-2xl p-6 border border-slate-200 hover:shadow-2xl transition-all duration-500 group overflow-hidden"
                    style={{ animation: 'card-glow 4s ease-in-out infinite 1.5s' }}
                  >
                    <div className="absolute inset-0 bg-gradient-to-br from-blue-500/0 to-cyan-500/0 group-hover:from-blue-500/5 group-hover:to-cyan-500/5 transition-all duration-500" />
                    {/* Animated corner accent */}
                    <div className="absolute -top-10 -right-10 w-20 h-20 bg-gradient-to-br from-blue-400/20 to-transparent rounded-full blur-xl" style={{ animation: 'workflow-pulse 3s ease-in-out infinite 1.5s' }} />
                    <ShakeOnHover>
                      <div 
                        className="w-14 h-14 rounded-xl bg-gradient-to-br from-blue-500 to-cyan-500 flex items-center justify-center mb-4 shadow-lg group-hover:scale-110 group-hover:rotate-6 transition-all duration-300 relative"
                        style={{ animation: 'workflow-float 5s ease-in-out infinite 0.9s' }}
                      >
                        <Database className="w-7 h-7 text-white" style={{ animation: 'pulse 2s ease-in-out infinite 0.4s' }} />
                        {/* Icon glow ring */}
                        <div className="absolute inset-0 rounded-xl bg-blue-400/30 blur-md -z-10" style={{ animation: 'workflow-pulse 2s ease-in-out infinite 0.9s' }} />
                      </div>
                    </ShakeOnHover>
                    <h3 className="text-xl font-bold text-slate-900 mb-3">History & Sending</h3>
                    <ul className="space-y-2 text-sm text-slate-600">
                      {['Full copy history', 'Search past copies', 'Send to contacts', 'Send to deals'].map((item, i) => (
                        <li 
                          key={item}
                          className="flex items-center gap-2 hover:translate-x-1 transition-transform"
                          style={{ animation: `list-slide 0.5s ease-out ${i * 0.1 + 0.3}s both` }}
                        >
                          <Check className="w-4 h-4 text-emerald-500" style={{ animation: `checkmark-pop 0.3s ease-out ${i * 0.1 + 0.5}s both` }} />
                          {item}
                        </li>
                      ))}
                      <li 
                        className="text-slate-500 italic mt-2 flex items-center gap-1"
                        style={{ animation: 'list-slide 0.5s ease-out 0.7s both' }}
                      >
                        <Sparkles className="w-3 h-3 text-blue-400" style={{ animation: 'sparkle-spin 2s linear infinite' }} />
                        Reuse & resend anytime
                      </li>
                    </ul>
                  </div>
                </TiltCard>
              </AnimatedSection>
            </div>
            
            {/* Animated step indicators below cards */}
            <div className="flex justify-center items-center gap-4 mt-12">
              {[1, 2, 3, 4].map((num, i) => (
                <div key={num} className="flex items-center">
                  <div 
                    className="w-8 h-8 rounded-full bg-gradient-to-r from-orange-500 to-amber-500 flex items-center justify-center text-white text-sm font-bold shadow-lg"
                    style={{ animation: `badge-bounce 2s ease-in-out infinite ${i * 0.2}s` }}
                  >
                    {num}
                  </div>
                  {i < 3 && (
                    <div 
                      className="w-12 h-0.5 bg-gradient-to-r from-orange-300 to-amber-300 mx-2"
                      style={{ animation: `workflow-pulse 2s ease-in-out infinite ${i * 0.3}s` }}
                    />
                  )}
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* ==================== HOW IT WORKS ==================== */}
        <section id="how-it-works" className={`py-28 bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 relative overflow-hidden ${BELOW_FOLD}`}>
          {/* Animated background elements */}
          <div className="absolute inset-0">
            {[...Array(20)].map((_, i) => (
              <div
                key={i}
                className="absolute w-1 h-1 bg-orange-500/30 rounded-full"
                style={{
                  left: `${Math.random() * 100}%`,
                  top: `${Math.random() * 100}%`,
                  animation: `twinkle ${2 + Math.random() * 3}s ease-in-out infinite`,
                  animationDelay: `${Math.random() * 2}s`,
                }}
              />
            ))}
          </div>
          <style>{`
            @keyframes twinkle {
              0%, 100% { opacity: 0.2; transform: scale(1); }
              50% { opacity: 1; transform: scale(2); }
            }
            @keyframes dash-flow {
              to { stroke-dashoffset: -20; }
            }
            @keyframes node-pulse {
              0%, 100% { box-shadow: 0 0 0 0 rgba(251, 146, 60, 0.4); }
              50% { box-shadow: 0 0 0 15px rgba(251, 146, 60, 0); }
            }
          `}</style>
          
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 relative">
            <AnimatedSection className="text-center mb-20">
              <ParticleExplosion>
                <div className="inline-flex items-center gap-2 bg-orange-500/20 text-orange-400 px-4 py-2 rounded-full text-sm font-semibold mb-6 backdrop-blur-sm border border-orange-500/30 cursor-pointer">
                  <Rocket className="w-4 h-4 animate-bounce" />
                  How it works
                </div>
              </ParticleExplosion>
              <h2 className="text-4xl md:text-5xl lg:text-6xl font-bold text-white mb-6">
                How a club or sales team <GradientText>goes live</GradientText>
              </h2>
              <p className="text-xl text-slate-300 max-w-2xl mx-auto">
                Four steps from a new workspace to leads, members and statements in one place
              </p>
            </AnimatedSection>

            {/* Journey Path - Desktop */}
            <div className="hidden lg:block relative">
              {/* SVG Path connecting nodes */}
              <svg className="absolute top-24 left-0 w-full h-32 pointer-events-none" viewBox="0 0 100 100" preserveAspectRatio="none">
                <path
                  d="M 12.5 50 Q 25 20, 37.5 50 T 62.5 50 T 87.5 50"
                  fill="none"
                  stroke="url(#gradient-path)"
                  strokeWidth="2"
                  strokeDasharray="4 4"
                  style={{ animation: 'dash-flow 1s linear infinite' }}
                />
                <defs>
                  <linearGradient id="gradient-path" x1="0%" y1="0%" x2="100%" y2="0%">
                    <stop offset="0%" stopColor="#f97316" />
                    <stop offset="50%" stopColor="#fbbf24" />
                    <stop offset="100%" stopColor="#f97316" />
                  </linearGradient>
                </defs>
              </svg>

              <div className="grid grid-cols-4 gap-8 items-stretch">
                {[
                  { step: 1, icon: UserPlus, title: 'Create your workspace', desc: 'Sign up, name your workspace and invite colleagues with the roles they need.', color: 'from-orange-500 to-red-500' },
                  { step: 2, icon: Settings, title: 'Set it up', desc: 'Add contract templates, payment methods (card, TWINT, SEPA, QR-bill) and your brand voice for the AI writer.', color: 'from-amber-500 to-orange-500' },
                  { step: 3, icon: Database, title: 'Bring your data', desc: 'Import leads, contacts and members with a dry run first, and put the enquiry form on your website.', color: 'from-emerald-500 to-teal-500' },
                  { step: 4, icon: Target, title: 'Sign, welcome, bill', desc: 'Leads sign online and pay the deposit, become members in one step, and get monthly statements.', color: 'from-violet-500 to-purple-500' },
                ].map((item, i) => (
                  <AnimatedSection key={item.step} animation="fade-up" delay={i * 150} className="h-full">
                    <div className="relative flex flex-col items-center group h-full">
                      {/* Animated node */}
                      <div 
                        className={`relative w-20 h-20 rounded-full bg-gradient-to-br ${item.color} flex items-center justify-center mb-8 shadow-2xl cursor-pointer z-10 flex-shrink-0`}
                        style={{ animation: 'node-pulse 2s ease-in-out infinite', animationDelay: `${i * 0.3}s` }}
                      >
                        {/* Orbiting ring */}
                        <div 
                          className="absolute inset-[-8px] rounded-full border-2 border-dashed border-white/30"
                          style={{ animation: 'spin 10s linear infinite' }}
                        />
                        {/* Inner glow */}
                        <div className="absolute inset-2 rounded-full bg-white/20 blur-sm" />
                        <item.icon className="w-9 h-9 text-white relative z-10 group-hover:scale-110 transition-transform duration-300" />
                        
                        {/* Step number badge */}
                        <div className="absolute -top-2 -right-2 w-8 h-8 rounded-full bg-slate-900 border-2 border-white/50 flex items-center justify-center text-white font-bold text-sm shadow-lg">
                          {item.step}
                        </div>
                      </div>

                      {/* Content card - fixed height */}
                      <div className="relative bg-white/5 backdrop-blur-xl rounded-2xl p-6 border border-white/10 hover:border-orange-500/50 hover:bg-white/10 transition-all duration-500 w-full group-hover:translate-y-[-8px] flex-1 flex flex-col min-h-[140px]">
                        {/* Glow effect on hover */}
                        <div className={`absolute inset-0 rounded-2xl bg-gradient-to-br ${item.color} opacity-0 group-hover:opacity-10 transition-opacity duration-500 blur-xl`} />
                        
                        <h3 className="text-xl font-bold text-white mb-3 relative">{item.title}</h3>
                        <p className="text-slate-300 text-sm leading-relaxed relative flex-1">{item.desc}</p>
                        
                        {/* Arrow indicator */}
                        {i < 3 && (
                          <div className="absolute -right-4 top-1/2 -translate-y-1/2 hidden xl:flex items-center justify-center w-8 h-8 rounded-full bg-orange-500/20 border border-orange-500/30">
                            <ArrowRight className="w-4 h-4 text-orange-400" />
                          </div>
                        )}
                      </div>
                    </div>
                  </AnimatedSection>
                ))}
              </div>
            </div>

            {/* Journey Path - Mobile/Tablet */}
            <div className="lg:hidden relative">
              {/* Vertical line */}
              <div className="absolute left-8 top-0 bottom-0 w-0.5 bg-gradient-to-b from-orange-500 via-amber-500 to-violet-500" />
              
              <div className="space-y-12">
                {[
                  { step: 1, icon: UserPlus, title: 'Create your workspace', desc: 'Sign up, name your workspace and invite colleagues with the roles they need.', color: 'from-orange-500 to-red-500' },
                  { step: 2, icon: Settings, title: 'Set it up', desc: 'Add contract templates, payment methods (card, TWINT, SEPA, QR-bill) and your brand voice for the AI writer.', color: 'from-amber-500 to-orange-500' },
                  { step: 3, icon: Database, title: 'Bring your data', desc: 'Import leads, contacts and members with a dry run first, and put the enquiry form on your website.', color: 'from-emerald-500 to-teal-500' },
                  { step: 4, icon: Target, title: 'Sign, welcome, bill', desc: 'Leads sign online and pay the deposit, become members in one step, and get monthly statements.', color: 'from-violet-500 to-purple-500' },
                ].map((item, i) => (
                  <AnimatedSection key={item.step} animation="fade-right" delay={i * 100}>
                    <div className="relative flex gap-6 group">
                      {/* Node */}
                      <div 
                        className={`relative flex-shrink-0 w-16 h-16 rounded-full bg-gradient-to-br ${item.color} flex items-center justify-center shadow-xl z-10`}
                        style={{ animation: 'node-pulse 2s ease-in-out infinite', animationDelay: `${i * 0.3}s` }}
                      >
                        <item.icon className="w-7 h-7 text-white" />
                        <div className="absolute -top-1 -right-1 w-6 h-6 rounded-full bg-slate-900 border-2 border-white/50 flex items-center justify-center text-white font-bold text-xs">
                          {item.step}
                        </div>
                      </div>

                      {/* Content */}
                      <div className="flex-1 bg-white/5 backdrop-blur-xl rounded-2xl p-5 border border-white/10 hover:border-orange-500/30 transition-all duration-300">
                        <h3 className="text-lg font-bold text-white mb-2">{item.title}</h3>
                        <p className="text-slate-300 text-sm">{item.desc}</p>
                      </div>
                    </div>
                  </AnimatedSection>
                ))}
              </div>
            </div>

            {/* CTA */}
            <AnimatedSection className="text-center mt-16" animation="fade-up" delay={600}>
              <MagneticButton>
                <RippleButton
                  to="/login?mode=register"
                  track="cta-signup"
                  className="inline-flex items-center gap-3 bg-gradient-to-r from-orange-600 to-amber-500 text-white px-10 py-5 rounded-2xl font-semibold text-lg shadow-2xl shadow-orange-500/30 hover:shadow-orange-500/50 group"
                >
                  <span>{signupLabel}</span>
                  <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center group-hover:bg-white/30 group-hover:rotate-45 transition-all duration-300">
                    <ArrowRight className="w-5 h-5" />
                  </div>
                </RippleButton>
              </MagneticButton>
            </AnimatedSection>
          </div>
        </section>

        {/* ==================== FAQ ==================== */}
        <section id="faq" className={`py-32 bg-white relative overflow-hidden ${BELOW_FOLD}`}>
          {/* Background decoration */}
          <div className="absolute top-0 right-0 w-1/2 h-full bg-gradient-to-l from-orange-50 to-transparent" />
          <div className="absolute bottom-0 left-0 w-96 h-96 bg-gradient-to-br from-amber-100 to-orange-100 rounded-full blur-3xl opacity-50 -translate-x-1/2 translate-y-1/2" />
          
          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 relative">
            <div className="grid lg:grid-cols-5 gap-12 lg:gap-20">
              {/* Left side - Header */}
              <div className="lg:col-span-2 lg:sticky lg:top-32 lg:self-start">
                <div className="inline-flex items-center gap-2 bg-gradient-to-r from-orange-500 to-amber-500 text-white px-4 py-2 rounded-full text-sm font-semibold mb-6">
                  <MessageSquare className="w-4 h-4" />
                  FAQ
                </div>
                <h2 className="text-4xl md:text-5xl lg:text-6xl font-bold text-slate-900 mb-6 leading-tight">
                  CRM questions?<br />
                  <span className="text-transparent bg-clip-text bg-gradient-to-r from-orange-600 to-amber-500">
                    Answers.
                  </span>
                </h2>
                <p className="text-lg text-slate-700 mb-8">
                  What CadenceE does, who it is for, and how it handles payments, languages and data. Can't find what you're looking for?
                </p>
                <CtaLink
                  kind="contact"
                  lang="en"
                  className="inline-flex items-center gap-2 text-orange-700 font-semibold hover:gap-3 transition-all"
                >
                  Contact our team
                  <ArrowRight className="w-4 h-4" aria-hidden="true" />
                </CtaLink>
              </div>

              {/* Right side - FAQ Items */}
              <div className="lg:col-span-3 space-y-4">
                {HOME_FAQ.map((faq, i) => (
                  <FAQItemNew key={faq.q} question={faq.q} answer={faq.a} icon={faq.icon} color={faq.color} index={i} />
                ))}
                <script
                  type="application/ld+json"
                  dangerouslySetInnerHTML={{ __html: serializeJsonLd(faqPageJsonLd(HOME_FAQ, 'en')) }}
                />
              </div>
            </div>
          </div>
        </section>

        {/* ==================== SECURITY ==================== */}
        <section className={`py-32 bg-slate-950 relative overflow-hidden ${BELOW_FOLD}`}>
          {/* Security section animations */}
          <style>{`
            @keyframes security-pulse {
              0%, 100% { opacity: 0.2; transform: scale(1); }
              50% { opacity: 0.4; transform: scale(1.05); }
            }
            @keyframes security-scan {
              0% { transform: translateY(-100%); opacity: 0; }
              50% { opacity: 1; }
              100% { transform: translateY(100%); opacity: 0; }
            }
            @keyframes security-float {
              0%, 100% { transform: translateY(0) rotate(0deg); }
              50% { transform: translateY(-10px) rotate(2deg); }
            }
            @keyframes lock-shake {
              0%, 100% { transform: rotate(0deg); }
              25% { transform: rotate(-3deg); }
              75% { transform: rotate(3deg); }
            }
            @keyframes data-flow {
              0% { background-position: 0% 50%; }
              100% { background-position: 200% 50%; }
            }
            @keyframes shield-glow {
              0%, 100% { box-shadow: 0 0 20px rgba(52, 211, 153, 0.3); }
              50% { box-shadow: 0 0 40px rgba(52, 211, 153, 0.6); }
            }
            @keyframes typing-dots {
              0%, 100% { opacity: 0.3; }
              50% { opacity: 1; }
            }
            @keyframes badge-float {
              0%, 100% { transform: translateY(0); }
              50% { transform: translateY(-8px); }
            }
            @keyframes grid-scan {
              0% { transform: translateY(-100%); }
              100% { transform: translateY(100%); }
            }
          `}</style>
          
          {/* Animated gradient orbs */}
          <div 
            className="absolute top-0 left-1/4 w-[500px] h-[500px] bg-gradient-to-br from-emerald-500/20 to-cyan-500/20 rounded-full blur-[120px]"
            style={{ animation: 'security-pulse 8s ease-in-out infinite' }}
          />
          <div 
            className="absolute bottom-0 right-1/4 w-[400px] h-[400px] bg-gradient-to-br from-violet-500/20 to-purple-500/20 rounded-full blur-[100px]"
            style={{ animation: 'security-pulse 10s ease-in-out infinite 2s' }}
          />
          <div 
            className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-gradient-to-br from-cyan-500/10 to-emerald-500/10 rounded-full blur-[150px]"
            style={{ animation: 'security-pulse 12s ease-in-out infinite 4s' }}
          />
          
          {/* Grid pattern overlay with scan effect */}
          <div 
            className="absolute inset-0 opacity-[0.03]"
            style={{
              backgroundImage: `linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px)`,
              backgroundSize: '60px 60px',
            }}
          />
          {/* Scanning line effect */}
          <div className="absolute inset-0 overflow-hidden pointer-events-none">
            <div 
              className="absolute left-0 right-0 h-32 bg-gradient-to-b from-transparent via-emerald-500/5 to-transparent"
              style={{ animation: 'grid-scan 8s linear infinite' }}
            />
          </div>
          
          {/* Floating security particles */}
          <div className="absolute top-20 left-[10%] hidden lg:block" style={{ animation: 'security-float 6s ease-in-out infinite' }}>
            <div className="w-3 h-3 rounded-full bg-emerald-400/30" />
          </div>
          <div className="absolute top-40 right-[15%] hidden lg:block" style={{ animation: 'security-float 5s ease-in-out infinite 1s' }}>
            <div className="w-2 h-2 rounded-full bg-cyan-400/40" />
          </div>
          <div className="absolute bottom-32 left-[20%] hidden lg:block" style={{ animation: 'security-float 7s ease-in-out infinite 2s' }}>
            <div className="w-2 h-2 rounded-full bg-violet-400/30" />
          </div>

          <div className="w-full max-w-[1600px] mx-auto px-6 lg:px-12 relative">
            {/* Header */}
            <div className="text-center mb-20">
              <div 
                className="inline-flex items-center gap-2 bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 px-5 py-2.5 rounded-full text-sm font-semibold mb-8"
                style={{ animation: 'badge-float 3s ease-in-out infinite' }}
              >
                <Shield className="w-4 h-4" style={{ animation: 'lock-shake 2s ease-in-out infinite' }} />
                Security and privacy
              </div>
              <h2 className="text-4xl md:text-5xl lg:text-6xl font-bold text-white mb-6">
                Your members’ data,{' '}
                <span className="relative inline-block">
                  <span 
                    className="text-transparent bg-clip-text bg-gradient-to-r from-emerald-400 via-cyan-400 to-emerald-400"
                    style={{ backgroundSize: '200% auto', animation: 'data-flow 3s linear infinite' }}
                  >
                    kept private
                  </span>
                  <svg className="absolute -bottom-2 left-0 w-full" viewBox="0 0 200 8" fill="none">
                    <path d="M0 4C50 4 50 1 100 1C150 1 150 7 200 7" stroke="url(#security-underline)" strokeWidth="2" strokeLinecap="round" />
                    <defs>
                      <linearGradient id="security-underline" x1="0" y1="0" x2="200" y2="0">
                        <stop stopColor="#34d399" />
                        <stop offset="0.5" stopColor="#22d3ee" />
                        <stop offset="1" stopColor="#34d399" />
                      </linearGradient>
                    </defs>
                  </svg>
                </span>
              </h2>
              <p className="text-lg text-slate-300 max-w-2xl mx-auto">
                Passkeys and two-factor sign-in, roles and permissions, an audit log of every change, and privacy requests under the GDPR and the Swiss FADP.
              </p>
            </div>

            {/* Bento Grid Layout */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-5">
              {/* Large Feature Card - Encryption */}
              <div className="lg:col-span-2 lg:row-span-2 group">
                <div 
                  className="h-full bg-gradient-to-br from-slate-900 to-slate-800 rounded-3xl p-8 border border-slate-700/50 hover:border-emerald-500/30 transition-all duration-300 relative overflow-hidden"
                >
                  {/* Decorative elements */}
                  <div 
                    className="absolute top-0 right-0 w-64 h-64 bg-gradient-to-br from-emerald-500/10 to-cyan-500/10 rounded-full blur-3xl translate-x-1/2 -translate-y-1/2"
                    style={{ animation: 'security-pulse 4s ease-in-out infinite' }}
                  />
                  {/* Scanning effect */}
                  <div className="absolute inset-0 overflow-hidden rounded-3xl pointer-events-none">
                    <div 
                      className="absolute left-0 right-0 h-20 bg-gradient-to-b from-transparent via-emerald-500/10 to-transparent"
                      style={{ animation: 'security-scan 4s linear infinite' }}
                    />
                  </div>
                  
                  <div className="relative">
                    <div className="flex items-start justify-between mb-8">
                      <div 
                        className="w-16 h-16 rounded-2xl bg-gradient-to-br from-emerald-500 to-cyan-500 flex items-center justify-center shadow-lg shadow-emerald-500/25"
                        style={{ animation: 'shield-glow 3s ease-in-out infinite' }}
                      >
                        <Lock className="w-8 h-8 text-white" style={{ animation: 'lock-shake 3s ease-in-out infinite' }} />
                      </div>
                      <div className="flex items-center gap-2 bg-emerald-500/10 text-emerald-400 px-3 py-1.5 rounded-full text-xs font-semibold">
                        <span className="w-1.5 h-1.5 bg-emerald-400 rounded-full" style={{ animation: 'pulse 1s ease-in-out infinite' }} />
                        Always recorded
                      </div>
                    </div>
                    
                    <h3 className="text-2xl font-bold text-white mb-3">An Audit Log of Every Change</h3>
                    <p className="text-slate-300 mb-8 leading-relaxed">
                      Every change to a lead, member, contract or payment is recorded with who made it and when. The ledger is only ever reversed, never edited, so the history always adds up.
                    </p>
                    
                    {/* Visual representation */}
                    <div className="bg-slate-800/50 rounded-2xl p-5 border border-slate-700/50">
                      <div className="flex items-center gap-4 mb-4">
                        <div className="flex-1 h-2 bg-slate-700 rounded-full overflow-hidden">
                          <div 
                            className="h-full w-full bg-gradient-to-r from-emerald-500 via-cyan-400 to-emerald-500 rounded-full"
                            style={{ backgroundSize: '200% 100%', animation: 'data-flow 2s linear infinite' }}
                          />
                        </div>
                        <span className="text-emerald-400 text-sm font-mono">audit log</span>
                      </div>
                      <div className="font-mono text-xs text-slate-500 break-all flex gap-1">
                        {[...Array(32)].map((_, i) => (
                          <span 
                            key={i} 
                            className="text-emerald-500/60"
                            style={{ animation: `typing-dots 1s ease-in-out infinite ${i * 0.05}s` }}
                          >
                            █
                          </span>
                        ))}
                      </div>
                      <div className="mt-3 flex items-center gap-2 text-xs text-slate-300">
                        <CheckCircle2 className="w-3 h-3 text-emerald-400" />
                        <span>Ledger reversed, never edited</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Two-Factor Authentication */}
              <div className="group">
                <div 
                  className="h-full bg-gradient-to-br from-slate-900 to-slate-800 rounded-3xl p-6 border border-slate-700/50 hover:border-violet-500/30 transition-all duration-300 relative overflow-hidden"
                >
                  <div className="absolute -top-12 -right-12 w-32 h-32 bg-violet-500/10 rounded-full blur-2xl" style={{ animation: 'security-pulse 5s ease-in-out infinite' }} />
                  
                  <div className="relative">
<div 
                        className="w-12 h-12 rounded-xl bg-gradient-to-br from-violet-500 to-purple-600 flex items-center justify-center mb-5 shadow-lg shadow-violet-500/25"
                      >
                      <Shield className="w-6 h-6 text-white" style={{ animation: 'pulse 2s ease-in-out infinite' }} />
                    </div>
                    
                    <h3 className="text-lg font-bold text-white mb-2">Passkeys and Two-Factor</h3>
                    <p className="text-slate-300 text-sm mb-4">Sign in with a passkey, Google or Microsoft, and add two-factor authentication.</p>
                    
                    {/* Mini visual - animated OTP */}
                    <div className="flex items-center gap-2">
                      {[4, 7, 2, 9, 1, 8].map((num, i) => (
                        <div 
                          key={i} 
                          className="w-6 h-8 rounded bg-slate-700 border border-slate-600 flex items-center justify-center text-violet-400 text-xs font-mono"
                          style={{ animation: `typing-dots 2s ease-in-out infinite ${i * 0.15}s` }}
                        >
                          {num}
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {/* Data Isolation */}
              <div className="group">
                <div 
                  className="h-full bg-gradient-to-br from-slate-900 to-slate-800 rounded-3xl p-6 border border-slate-700/50 hover:border-orange-500/30 transition-all duration-300 relative overflow-hidden"
                >
                  <div className="absolute -top-12 -right-12 w-32 h-32 bg-orange-500/10 rounded-full blur-2xl" style={{ animation: 'security-pulse 6s ease-in-out infinite 1s' }} />
                  
                  <div className="relative">
<div 
                        className="w-12 h-12 rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center mb-5 shadow-lg shadow-orange-500/25"
                      >
                      <Database className="w-6 h-6 text-white" style={{ animation: 'pulse 2s ease-in-out infinite 0.5s' }} />
                    </div>
                    
                    <h3 className="text-lg font-bold text-white mb-2">Roles and Permissions</h3>
                    <p className="text-slate-300 text-sm mb-4">Each colleague sees and changes what their role allows, in each of your workspaces.</p>
                    
                    {/* Mini visual - animated bars */}
                    <div className="flex items-center gap-2">
                      {[0, 1, 2].map((i) => (
                        <div 
                          key={i}
                          className={`flex-1 h-3 rounded border ${i === 1 ? 'bg-amber-500/30 border-amber-500/50' : 'bg-orange-500/20 border-orange-500/30'}`}
                          style={{ animation: `security-pulse 2s ease-in-out infinite ${i * 0.3}s` }}
                        />
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {/* GDPR Compliant */}
              <div className="group">
                <div 
                  className="h-full bg-gradient-to-br from-slate-900 to-slate-800 rounded-3xl p-6 border border-slate-700/50 hover:border-cyan-500/30 transition-all duration-300 relative overflow-hidden"
                >
                  <div className="absolute -top-12 -right-12 w-32 h-32 bg-cyan-500/10 rounded-full blur-2xl" style={{ animation: 'security-pulse 5s ease-in-out infinite 0.5s' }} />
                  
                  <div className="relative">
<div 
                        className="w-12 h-12 rounded-xl bg-gradient-to-br from-cyan-500 to-blue-500 flex items-center justify-center mb-5 shadow-lg shadow-cyan-500/25"
                      >
                      <Globe className="w-6 h-6 text-white" style={{ animation: 'icon-rotate 20s linear infinite' }} />
                    </div>
                    
                    <h3 className="text-lg font-bold text-white mb-2">GDPR and Swiss FADP</h3>
                    <p className="text-slate-300 text-sm mb-4">Export and erasure requests handled in the CRM, with consent and one-click unsubscribe.</p>
                    
                    {/* Mini visual - animated connection */}
                    <div className="flex items-center gap-3">
                      <div className="w-3 h-3 rounded-full bg-cyan-400" style={{ animation: 'pulse 1.5s ease-in-out infinite' }} />
                      <div 
                        className="flex-1 h-[2px] bg-gradient-to-r from-cyan-400 to-transparent"
                        style={{ animation: 'data-flow 2s linear infinite' }}
                      />
                      <Server className="w-4 h-4 text-cyan-400" style={{ animation: 'pulse 2s ease-in-out infinite 0.5s' }} />
                    </div>
                  </div>
                </div>
              </div>

              {/* Compliance Badge */}
              <div className="group">
                <div 
                  className="h-full bg-gradient-to-br from-emerald-500/10 to-cyan-500/10 rounded-3xl p-6 border border-emerald-500/20 hover:border-emerald-500/40 transition-all duration-300 relative overflow-hidden flex flex-col items-center justify-center text-center"
                >
                  {/* Animated ring */}
                  <div className="absolute inset-4 rounded-2xl border border-emerald-500/20" style={{ animation: 'security-pulse 3s ease-in-out infinite' }} />
                  
                  <div 
                    className="w-14 h-14 rounded-full bg-emerald-500/20 flex items-center justify-center mb-4"
                    style={{ animation: 'shield-glow 2s ease-in-out infinite' }}
                  >
                    <CheckCircle2 className="w-7 h-7 text-emerald-400" style={{ animation: 'pulse 1.5s ease-in-out infinite' }} />
                  </div>
                  <span className="text-white font-bold">System Health</span>
                  <span className="text-slate-300 text-sm">The CRM checks its own database, jobs and email outbox</span>
                  <div className="mt-2 flex items-center gap-1">
                    {[...Array(3)].map((_, i) => (
                      <div 
                        key={i} 
                        className="w-1.5 h-1.5 rounded-full bg-emerald-400"
                        style={{ animation: `typing-dots 1s ease-in-out infinite ${i * 0.2}s` }}
                      />
                    ))}
                  </div>
                </div>
              </div>
            </div>
            
            {/* Trust indicators */}
            <div className="mt-12 flex flex-wrap justify-center items-center gap-8 text-slate-300 text-sm">
              {[
                { icon: Lock, text: 'Passkey sign-in' },
                { icon: Shield, text: 'Two-factor authentication' },
                { icon: Globe, text: 'GDPR and FADP requests' },
                { icon: CheckCircle2, text: 'Audit log' },
              ].map((item, i) => (
                <div 
                  key={item.text}
                  className="flex items-center gap-2"
                  style={{ animation: `list-slide 0.5s ease-out ${i * 0.1}s both` }}
                >
                  <item.icon className="w-4 h-4 text-emerald-400/60" />
                  <span>{item.text}</span>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* ==================== FINAL CTA ==================== */}
        <section className={`py-28 relative overflow-hidden ${BELOW_FOLD}`}>
          <div 
            className="absolute inset-0 bg-gradient-to-br from-orange-600 via-amber-500 to-orange-600 bg-[length:300%_300%]"
            style={{ animation: 'gradient-flow 8s ease infinite' }}
          />
          <div className="absolute inset-0 opacity-20" style={{
            backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23ffffff' fill-opacity='0.4'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E")`,
          }} />
          
          {/* Floating shapes */}
          <FloatingElement className="absolute top-20 left-[8%] hidden lg:block" delay={0} duration={6} distance={25}>
            <div className="w-24 h-24 rounded-3xl bg-white/10 backdrop-blur-sm border border-white/20 rotate-12" />
          </FloatingElement>
          <FloatingElement className="absolute bottom-20 right-[12%] hidden lg:block" delay={2} duration={5} distance={20}>
            <div className="w-16 h-16 rounded-2xl bg-white/10 backdrop-blur-sm border border-white/20 -rotate-12" />
          </FloatingElement>
          <FloatingElement className="absolute top-1/2 left-[5%] hidden lg:block" delay={1} duration={7} distance={15}>
            <div className="w-12 h-12 rounded-xl bg-white/10 backdrop-blur-sm border border-white/20 rotate-45" />
          </FloatingElement>
          
          <div className="w-full max-w-5xl mx-auto px-6 lg:px-12 text-center relative">
            <AnimatedSection>
              <h2 className="text-4xl md:text-5xl lg:text-6xl font-bold text-white mb-6">
                Run your club and your sales <GlitchText>from one CRM</GlitchText>
              </h2>
              <p className="text-xl text-white mb-10 max-w-2xl mx-auto">
                Create a workspace for your club or sales team, or ask for a demo from the team that built CadenceE with Pavillon 46.
              </p>
              <div className="flex flex-col sm:flex-row gap-4 justify-center">
                <MagneticButton>
                  <RippleButton
                    to="/login?mode=register"
                    track="cta-signup"
                    className="group bg-white text-orange-800 px-10 py-5 rounded-2xl font-bold text-lg flex items-center justify-center gap-3 shadow-2xl hover:scale-105 transition-all duration-300"
                  >
                    {signupLabel}
                    <div className="w-8 h-8 rounded-full bg-orange-100 flex items-center justify-center group-hover:bg-orange-500 group-hover:text-white group-hover:rotate-45 transition-all duration-300">
                      <ArrowRight className="w-4 h-4" />
                    </div>
                  </RippleButton>
                </MagneticButton>
                <MagneticButton>
                  <CtaLink
                    kind="try"
                    lang="en"
                    className="bg-slate-900/20 backdrop-blur-sm border-2 border-white/60 text-white px-10 py-5 rounded-2xl font-bold text-lg flex items-center justify-center gap-2 hover:bg-slate-900/30 hover:scale-105 transition-all duration-300"
                  >
                    <Play className="w-5 h-5" aria-hidden="true" />
                    {CTA_COPY.en.tryDemo}
                  </CtaLink>
                </MagneticButton>
              </div>
              <div className="flex flex-wrap items-center justify-center gap-6 mt-10 text-white text-sm">
                {['Built in Switzerland', 'QR-bill and TWINT', 'English, French and German'].map((item) => (
                  <div key={item} className="flex items-center gap-2">
                    <CheckCircle2 className="w-4 h-4" />
                    {item}
                  </div>
                ))}
              </div>
              <LaunchOfferStrip lang="en" tone="onColor" />
            </AnimatedSection>
          </div>
        </section>
      </main>

      {/* ==================== FOOTER ==================== */}
      <MarketingFooter lang="en" pageId="home" />
      <StickySignupBar lang="en" />

      {/* Global styles */}
      <style>{`
        @keyframes gradient-flow {
          0%, 100% { background-position: 0% 50%; }
          50% { background-position: 100% 50%; }
        }
        @keyframes fade-in {
          from { opacity: 0; transform: translateY(10px); }
          to { opacity: 1; transform: translateY(0); }
        }
      `}</style>
    </div>
  );
}
