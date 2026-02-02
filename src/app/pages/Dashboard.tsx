import { useState, useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import {
  Sparkles,
  Mail,
  MessageSquare,
  FileText,
  Briefcase,
  Workflow,
  ChevronDown,
  FileOutput,
  Send,
  TrendingUp,
  LayoutTemplate,
  Lightbulb,
  ArrowRight,
  Link2,
  History,
  Settings,
  CheckCircle2,
  XCircle,
  Users,
} from 'lucide-react';
import { getCurrentUser } from '@/app/lib/auth';
import AppHeader from '@/app/components/AppHeader';
import { cn } from '@/app/components/ui/utils';
import {
  getTemplateById,
  getUserSettings,
  generateCopy,
  getCopyHistoryStats,
  getCopyHistory,
  getTemplates,
  getConnectionStatus,
  getDashboardStats,
} from '@/app/api';
import type { CopyTypeId } from '@/app/api/types';
import type { CopyHistoryItem } from '@/app/api/types';
import { toast } from 'sonner';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';

const copyTypes = [
  { id: 'sales-email' as CopyTypeId, icon: Mail, title: 'Sales Email', desc: 'Personalized outreach' },
  { id: 'follow-up' as CopyTypeId, icon: MessageSquare, title: 'Follow-up', desc: 'Keep conversations going' },
  { id: 'crm-note' as CopyTypeId, icon: FileText, title: 'CRM Note', desc: 'Document interactions' },
  { id: 'deal-message' as CopyTypeId, icon: Briefcase, title: 'Deal Message', desc: 'Update stakeholders' },
  { id: 'workflow-message' as CopyTypeId, icon: Workflow, title: 'Workflow', desc: 'Automated sequences' },
];

const goals = [
  'Schedule a meeting',
  'Follow up after demo',
  'Request feedback',
  'Share resources',
  'Check in on progress',
  'Close the deal',
];

export default function Dashboard() {
  const location = useLocation();
  const templateId = (location.state as { templateId?: string } | null)?.templateId;
  const [selectedType, setSelectedType] = useState<CopyTypeId | ''>('');
  const [goal, setGoal] = useState(goals[0]);
  const [context, setContext] = useState('');
  const [length, setLength] = useState<'short' | 'medium' | 'long'>('medium');
  const [isGenerating, setIsGenerating] = useState(false);
  const [stats, setStats] = useState<{ sentThisWeek: number; totalSent: number; templateCount: number }>({
    sentThisWeek: 0,
    totalSent: 0,
    templateCount: 8,
  });
  const [crmStats, setCrmStats] = useState<{
    activeLeadsCount: number;
    activeDealsCount: number;
    pipelineValue: number;
    dealsWonCount: number;
    dealsLostCount: number;
  } | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<{ connected: boolean; accountEmail?: string }>({ connected: false });
  const [recentActivity, setRecentActivity] = useState<CopyHistoryItem[]>([]);
  const navigate = useNavigate();
  const user = getCurrentUser();
  const displayName = user?.name ?? 'there';

  useEffect(() => {
    let cancelled = false;
    const guard = <T,>(fn: (x: T) => void) => (x: T) => { if (!cancelled) fn(x); };
    getCopyHistoryStats()
      .then(guard((s) => setStats((prev) => ({ ...prev, ...s }))))
      .catch(() => {});
    getTemplates()
      .then(guard((t) => setStats((prev) => ({ ...prev, templateCount: t.length }))))
      .catch(() => {});
    getConnectionStatus()
      .then(guard((s) => setConnectionStatus({ connected: s.connected, accountEmail: s.accountEmail })))
      .catch(() => {});
    getCopyHistory()
      .then(guard((items) => setRecentActivity(items.slice(0, 5))))
      .catch(() => {});
    getDashboardStats()
      .then(guard(setCrmStats))
      .catch(() => {});
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!templateId) return;
    let cancelled = false;
    getTemplateById(templateId)
      .then((t) => {
        if (!cancelled && t) {
          setSelectedType(t.copyTypeId);
          setGoal(t.goal);
        }
      })
      .catch(() => {});
    return () => { cancelled = true; };
  }, [templateId]);

  const regenerateContext = (location.state as { regenerateContext?: string } | null)?.regenerateContext;
  useEffect(() => {
    if (regenerateContext?.trim()) setContext(regenerateContext);
  }, [regenerateContext]);

  const handleGenerate = async () => {
    if (!selectedType) return;
    setIsGenerating(true);
    try {
      const settings = await getUserSettings();
      const copy = await generateCopy({
        copyTypeId: selectedType,
        goal: goal || goals[0],
        context: context.trim() || undefined,
        length,
        companyName: settings.companyName,
        brandTone: settings.brandTone,
      });
      const copyTypeLabel = copyTypes.find((t) => t.id === selectedType)?.title ?? 'Copy';
      toast.success(messages.copy.generated);
      navigate('/generated', { state: { copy, copyTypeLabel } });
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setIsGenerating(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-50 via-slate-50/95 to-slate-100">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-6xl mx-auto px-[var(--page-padding)] py-8 lg:py-10" tabIndex={-1}>
        <div className="w-full">
          {/* Hero: title with in-text highlight */}
          <header className="mb-10 animate-fade-in">
            <p className="text-xs font-semibold text-slate-400 uppercase tracking-[0.2em] mb-3">
              Dashboard
            </p>
            <h1 className="text-3xl sm:text-4xl lg:text-5xl font-bold text-slate-900 tracking-tight mb-3">
              Welcome back,{' '}
              <span className="bg-gradient-to-r from-orange-500 via-amber-500 to-orange-600 bg-clip-text text-transparent">
                {displayName}
              </span>
            </h1>
            <p className="text-slate-600 text-base sm:text-lg max-w-xl leading-relaxed">
              Create AI-powered copy and send it directly to your CRM. Select a type below to get started.
            </p>
          </header>

          {/* Connection status */}
          <div
            className={cn(
              'mb-8 flex flex-wrap items-center gap-3 rounded-2xl border px-5 py-4 transition-all duration-300',
              connectionStatus.connected
                ? 'bg-emerald-50/80 border-emerald-200/80 text-emerald-800 shadow-sm shadow-emerald-100/50'
                : 'bg-amber-50/80 border-amber-200/80 text-amber-800 shadow-sm shadow-amber-100/50'
            )}
            aria-live="polite"
          >
            {connectionStatus.connected ? (
              <>
                <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-emerald-100 text-emerald-600 animate-pulse-soft">
                  <CheckCircle2 className="w-5 h-5 shrink-0" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold">CRM connected</p>
                  {connectionStatus.accountEmail && (
                    <p className="text-sm opacity-90 truncate">{connectionStatus.accountEmail}</p>
                  )}
                </div>
              </>
            ) : (
              <>
                <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-amber-100 text-amber-600">
                  <XCircle className="w-5 h-5 shrink-0" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold">CRM not connected</p>
                  <p className="text-sm opacity-90">Connect to send copy to your CRM.</p>
                </div>
              </>
            )}
            <Link
              to="/connect"
              className="inline-flex items-center gap-2 text-sm font-semibold underline underline-offset-2 hover:no-underline focus-visible:rounded-lg px-3 py-2 -m-2 rounded-lg hover:bg-black/5 transition-colors"
            >
              <Link2 className="w-4 h-4" />
              {connectionStatus.connected ? 'Manage' : 'Connect'}
            </Link>
          </div>

          {/* Stats grid with staggered animation */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-2">
            {[
              { value: String(stats.sentThisWeek), label: 'Generated this week', icon: FileOutput, color: 'bg-orange-50 text-orange-700 border-orange-100' },
              { value: String(stats.templateCount), label: 'Templates', icon: LayoutTemplate, color: 'bg-slate-100 text-slate-600 border-slate-200' },
              { value: String(stats.totalSent), label: 'Sent to CRM', icon: Send, color: 'bg-emerald-50 text-emerald-700 border-emerald-100' },
              { value: '~2 min', label: 'Avg. time saved', icon: TrendingUp, color: 'bg-violet-50 text-violet-700 border-violet-100' },
            ].map((stat, i) => (
              <div
                key={stat.label}
                className="bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-md hover:-translate-y-0.5 transition-all duration-300 animate-fade-in-up"
                style={{ animationDelay: `${i * 100}ms`, animationFillMode: 'backwards' }}
              >
                <div className={cn('inline-flex p-2.5 rounded-xl border', stat.color)}>
                  <stat.icon className="w-5 h-5" />
                </div>
                <p className="mt-3 text-2xl font-bold text-slate-900 tabular-nums">{stat.value}</p>
                <p className="text-sm text-slate-500 font-medium">{stat.label}</p>
              </div>
            ))}
          </div>
          {crmStats !== null && (
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mt-4">
              {[
                { value: String(crmStats.activeLeadsCount), label: 'Active leads', icon: Users, color: 'bg-blue-50 text-blue-700 border-blue-100' },
                { value: String(crmStats.activeDealsCount), label: 'Active deals', icon: Briefcase, color: 'bg-amber-50 text-amber-700 border-amber-100' },
                { value: `$${Number(crmStats.pipelineValue).toLocaleString()}`, label: 'Pipeline value', icon: TrendingUp, color: 'bg-emerald-50 text-emerald-700 border-emerald-100' },
                { value: `${crmStats.dealsWonCount} won / ${crmStats.dealsLostCount} lost`, label: 'Won vs lost', icon: CheckCircle2, color: 'bg-slate-100 text-slate-600 border-slate-200' },
              ].map((stat, i) => (
                <div
                  key={stat.label}
                  className="bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-md hover:-translate-y-0.5 transition-all duration-300 animate-fade-in-up"
                  style={{ animationDelay: `${400 + i * 80}ms`, animationFillMode: 'backwards' }}
                >
                  <div className={cn('inline-flex p-2.5 rounded-xl border', stat.color)}>
                    <stat.icon className="w-5 h-5" />
                  </div>
                  <p className="mt-3 text-2xl font-bold text-slate-900 tabular-nums">{stat.value}</p>
                  <p className="text-sm text-slate-500 font-medium">{stat.label}</p>
                </div>
              ))}
            </div>
          )}

          {/* Section divider */}
          <div className="flex items-center gap-4 my-12" aria-hidden="true">
            <span className="h-px flex-1 bg-gradient-to-r from-transparent via-orange-300/70 to-transparent rounded-full" />
            <span className="flex items-center gap-2 px-5 py-2.5 rounded-full bg-white border border-orange-200/80 shadow-md text-sm font-semibold text-orange-800 ring-1 ring-orange-100/50 transition-all duration-300 hover:shadow-lg hover:scale-105">
              <Sparkles className="w-4 h-4 text-orange-500 animate-float" />
              Create your content
            </span>
            <span className="h-px flex-1 bg-gradient-to-l from-transparent via-orange-300/70 to-transparent rounded-full" />
          </div>

          {/* Create new copy section */}
          <section className="bg-white rounded-2xl border border-slate-200/80 shadow-lg shadow-slate-200/30 overflow-hidden animate-scale-in" aria-labelledby="create-copy-heading">
            <div className="px-6 sm:px-8 py-6 border-b border-slate-100 bg-gradient-to-b from-slate-50/50 to-white">
              <div className="flex items-center gap-3">
                <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-orange-100 text-orange-600" aria-hidden>
                  <Sparkles className="w-5 h-5" />
                </div>
                <div>
                  <span className="text-xs font-semibold text-slate-400 uppercase tracking-widest">Create</span>
                  <h2 id="create-copy-heading" className="text-xl font-bold text-slate-900 mt-0.5">Create new copy</h2>
                  <p className="text-sm text-slate-500 mt-0.5">Choose a content type and customize your message.</p>
                </div>
              </div>
            </div>

            <div className="p-6 sm:p-8">
              <div className="mb-8">
                <label className="block text-sm font-semibold text-slate-700 mb-3">Content type</label>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                  {copyTypes.map((type) => {
                    const isSelected = selectedType === type.id;
                    return (
                      <button
                        key={type.id}
                        type="button"
                        onClick={() => setSelectedType(type.id)}
                        className={cn(
                          'flex flex-col items-center text-center p-4 rounded-2xl border-2 transition-all duration-300',
                          isSelected
                            ? 'border-orange-500 bg-orange-50/70 shadow-md shadow-orange-100/50 scale-[1.02]'
                            : 'border-slate-200 bg-slate-50/50 hover:border-orange-200 hover:bg-orange-50/30 hover:shadow-sm'
                        )}
                      >
                        <span
                          className={cn(
                            'flex items-center justify-center w-12 h-12 rounded-xl mb-3 transition-all duration-300',
                            isSelected ? 'bg-gradient-to-br from-orange-500 to-amber-500 text-white shadow-lg' : 'bg-slate-200 text-slate-600'
                          )}
                        >
                          <type.icon className="w-6 h-6" />
                        </span>
                        <span className="text-sm font-semibold text-slate-900">{type.title}</span>
                        <span className="text-xs text-slate-500 mt-0.5">{type.desc}</span>
                      </button>
                    );
                  })}
                </div>
              </div>

              {selectedType ? (
                <div className="space-y-6 pt-6 border-t border-slate-100">
                  <div>
                    <label htmlFor="goal" className="block text-sm font-medium text-slate-700 mb-2">
                      Message goal
                    </label>
                    <div className="relative">
                      <select
                        id="goal"
                        value={goal}
                        onChange={(e) => setGoal(e.target.value)}
                        className="w-full h-11 pl-4 pr-10 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-colors"
                      >
                        {goals.map((g) => (
                          <option key={g} value={g}>
                            {g}
                          </option>
                        ))}
                      </select>
                      <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400 pointer-events-none" />
                    </div>
                  </div>

                  <div>
                    <label htmlFor="context" className="block text-sm font-medium text-slate-700 mb-2">
                      Optional context
                    </label>
                    <textarea
                      id="context"
                      value={context}
                      onChange={(e) => setContext(e.target.value)}
                      placeholder="e.g. previous conversation, company info, pain points..."
                      rows={3}
                      className="w-full px-4 py-3 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm placeholder:text-slate-400 focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20 outline-none transition-colors resize-none"
                    />
                    <p className="flex items-center gap-1.5 mt-2 text-xs text-slate-500">
                      <Lightbulb className="w-3.5 h-3.5 shrink-0" />
                      More context improves the AI output.
                    </p>
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-2">Length</label>
                    <div className="flex gap-2">
                      {[
                        { id: 'short' as const, label: 'Short', sub: '2–3 sentences' },
                        { id: 'medium' as const, label: 'Medium', sub: '1 paragraph' },
                        { id: 'long' as const, label: 'Long', sub: '2+ paragraphs' },
                      ].map((opt) => (
                        <button
                          key={opt.id}
                          type="button"
                          onClick={() => setLength(opt.id)}
                          className={cn(
                            'flex-1 py-3 px-4 rounded-xl border-2 text-left transition-all duration-300',
                            length === opt.id
                              ? 'border-orange-500 bg-orange-50 text-orange-800 shadow-sm scale-[1.02]'
                              : 'border-slate-200 bg-white text-slate-700 hover:border-orange-200 hover:bg-orange-50/30'
                          )}
                        >
                          <span className="block text-sm font-medium">{opt.label}</span>
                          <span className="block text-xs text-slate-500 mt-0.5">{opt.sub}</span>
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="pt-2">
                    <button
                      type="button"
                      onClick={handleGenerate}
                      disabled={isGenerating}
                      className={cn(
                        'w-full sm:w-auto min-w-[200px] h-12 px-6 rounded-xl font-semibold text-white flex items-center justify-center gap-2 transition-all duration-300',
                        'bg-gradient-to-r from-orange-600 to-amber-600 hover:from-orange-500 hover:to-amber-500 active:scale-[0.98] shadow-lg shadow-orange-200/50 hover:shadow-xl hover:shadow-orange-200/60',
                        'disabled:bg-slate-300 disabled:shadow-none disabled:cursor-not-allowed disabled:opacity-90'
                      )}
                    >
                      {isGenerating ? (
                        <>
                          <span className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                          Generating...
                        </>
                      ) : (
                        <>
                          <Sparkles className="w-5 h-5" />
                          Generate copy
                        </>
                      )}
                    </button>
                    {!connectionStatus.connected && (
                      <p className="mt-3 text-sm text-slate-600">
                        <Link to="/connect" className="font-medium text-orange-600 hover:text-orange-700 focus-visible:underline">
                          Connect CRM
                        </Link>
                        {' '}to send copy to contacts and deals.
                      </p>
                    )}
                  </div>
                </div>
              ) : (
                <EmptyState
                  icon={Sparkles}
                  title="Select a content type"
                  description="Choose one of the options above to create your copy."
                  actionLabel="Try a template"
                  actionHref="/templates"
                  secondaryActionLabel={!connectionStatus.connected ? 'Connect CRM' : undefined}
                  secondaryActionHref={!connectionStatus.connected ? '/connect' : undefined}
                  className="animate-fade-in"
                />
              )}
            </div>
          </section>

          {/* Recent activity section */}
          <section
            className="mt-10 bg-white rounded-2xl border border-slate-200/80 shadow-lg shadow-slate-200/30 overflow-hidden animate-scale-in"
            style={{ animationDelay: '150ms', animationFillMode: 'backwards' }}
            aria-labelledby="recent-activity-heading"
          >
            <div className="px-6 sm:px-8 py-6 border-b border-slate-100 bg-gradient-to-b from-slate-50/50 to-white">
              <div className="flex items-center gap-3">
                <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-slate-100 text-slate-600" aria-hidden>
                  <History className="w-5 h-5" />
                </div>
                <div>
                  <span className="text-xs font-semibold text-slate-400 uppercase tracking-widest">Activity</span>
                  <h2 id="recent-activity-heading" className="text-xl font-bold text-slate-900 mt-0.5">Recent activity</h2>
                  <p className="text-sm text-slate-500 mt-0.5">Latest copy sent to CRM.</p>
                </div>
              </div>
            </div>
            <div className="p-6 sm:p-8">
              {recentActivity.length === 0 ? (
                <EmptyState
                  icon={History}
                  title="No activity yet"
                  description="Generate copy and send it to your CRM to see it here."
                  actionLabel="Try a template"
                  actionHref="/templates"
                  className="animate-fade-in"
                />
              ) : (
                <ul className="space-y-0 divide-y divide-slate-100">
                  {recentActivity.map((item, i) => (
                    <li
                      key={item.id}
                      className="flex flex-wrap items-center gap-2 py-4 first:pt-0 text-sm animate-slide-in-right hover:bg-slate-50/50 -mx-2 px-2 rounded-lg transition-colors"
                      style={{ animationDelay: `${200 + i * 60}ms`, animationFillMode: 'backwards' }}
                    >
                      <span className="font-semibold text-slate-900">{item.type}</span>
                      <span className="text-slate-400">→</span>
                      <span className="text-slate-700">{item.recipientName}</span>
                      <span className="ml-auto text-slate-400 text-xs font-medium">
                        {new Date(item.createdAt).toLocaleDateString(undefined, { dateStyle: 'short' })}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              <div className="mt-5">
                <Link
                  to="/history"
                  className="inline-flex items-center gap-2 text-sm font-semibold text-orange-600 hover:text-orange-700 transition-colors focus-visible:rounded-lg px-3 py-2 -m-2 rounded-lg hover:bg-orange-50"
                >
                  View full history
                  <ArrowRight className="w-4 h-4" />
                </Link>
              </div>
            </div>
          </section>

          {/* More options divider */}
          <div className="flex items-center gap-4 my-12" aria-hidden="true">
            <span className="h-px flex-1 bg-gradient-to-r from-transparent via-slate-300 to-transparent rounded-full" />
            <span className="flex items-center gap-2 px-5 py-2.5 rounded-full bg-white border border-slate-200 shadow-md text-sm font-semibold text-slate-600 transition-all duration-300 hover:shadow-lg hover:border-slate-300 hover:scale-105">
              <LayoutTemplate className="w-4 h-4 text-slate-500" />
              More options
            </span>
            <span className="h-px flex-1 bg-gradient-to-l from-transparent via-slate-300 to-transparent rounded-full" />
          </div>

          {/* Quick links */}
          <nav className="flex flex-wrap justify-center gap-3 sm:gap-4" aria-label="Quick links">
            {[
              { to: '/templates', icon: LayoutTemplate, label: 'Templates' },
              { to: '/history', icon: History, label: 'History' },
              { to: '/settings', icon: Settings, label: 'Settings' },
              { to: '/connect', icon: Link2, label: 'Connection' },
            ].map((link) => (
              <Link
                key={link.to}
                to={link.to}
                className="inline-flex items-center gap-2 px-5 py-3 rounded-xl text-sm font-semibold text-slate-700 bg-white border border-slate-200 shadow-sm hover:shadow-md hover:border-orange-200 hover:text-orange-700 hover:bg-orange-50/50 transition-all duration-300 focus-visible:rounded-xl"
              >
                <link.icon className="w-4 h-4" />
                {link.label}
              </Link>
            ))}
          </nav>
        </div>
      </main>
    </div>
  );
}
