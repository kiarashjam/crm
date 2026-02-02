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
  UserCircle,
  Building2,
  CheckSquare,
  Activity,
} from 'lucide-react';
import { getCurrentUser, isDemoMode } from '@/app/lib/auth';
import AppHeader from '@/app/components/AppHeader';
import { cn } from '@/app/components/ui/utils';
import {
  getTemplateById,
  getUserSettings,
  generateCopy,
  getCopyHistoryStats,
  getCopyHistory,
  getTemplates,
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

const quickNavItems = [
  { to: '/leads', icon: Users, label: 'Leads' },
  { to: '/contacts', icon: UserCircle, label: 'Contacts' },
  { to: '/deals', icon: Briefcase, label: 'Deals' },
  { to: '/tasks', icon: CheckSquare, label: 'Tasks' },
  { to: '/companies', icon: Building2, label: 'Companies' },
  { to: '/activities', icon: Activity, label: 'Activities' },
];

export default function Dashboard() {
  const location = useLocation();
  const templateId = (location.state as { templateId?: string } | null)?.templateId;
  const [selectedType, setSelectedType] = useState<CopyTypeId | ''>(() =>
    isDemoMode() ? 'sales-email' : ''
  );
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

  // North Star metric: pipeline value or generated this week (F-pattern: top-left prominence)
  const northStarValue = crmStats ? `$${Number(crmStats.pipelineValue).toLocaleString()}` : String(stats.sentThisWeek);
  const northStarLabel = crmStats ? 'Pipeline value' : 'Generated this week';

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="flex-1 w-full max-w-7xl mx-auto px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        <div className="space-y-8">
          {/* Hero: F-pattern — North Star metric + welcome top-left */}
          <header className="flex flex-col lg:flex-row lg:items-end lg:justify-between gap-6 animate-fade-in">
            <div>
              <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">
                Welcome back, <span className="text-orange-600">{displayName}</span>
              </h1>
              <p className="mt-1.5 text-slate-600 text-sm sm:text-base">
                Here’s your CRM at a glance.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-white border border-slate-200 shadow-sm">
                <TrendingUp className="w-5 h-5 text-orange-500" aria-hidden />
                <div>
                  <p className="text-xl font-bold text-slate-900 tabular-nums">{northStarValue}</p>
                  <p className="text-xs font-medium text-slate-500">{northStarLabel}</p>
                </div>
              </div>
            </div>
          </header>

          {/* KPI row: 4–5 metrics max, flat design, no gradients */}
          <section className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4" aria-labelledby="kpi-heading">
            <h2 id="kpi-heading" className="sr-only">Key metrics</h2>
            {[
              { value: String(stats.sentThisWeek), label: 'Generated this week', icon: FileOutput },
              { value: String(stats.templateCount), label: 'Templates', icon: LayoutTemplate },
              { value: String(stats.totalSent), label: 'Sent to CRM', icon: Send },
              { value: '~2 min', label: 'Avg. time saved', icon: TrendingUp },
            ].map((stat, i) => (
              <div
                key={stat.label}
                className="bg-white rounded-xl border border-slate-200 p-4 sm:p-5 animate-fade-in-up"
                style={{ animationDelay: `${i * 80}ms`, animationFillMode: 'backwards' }}
              >
                <stat.icon className="w-5 h-5 text-slate-400" aria-hidden />
                <p className="mt-2 text-lg sm:text-xl font-bold text-slate-900 tabular-nums">{stat.value}</p>
                <p className="text-xs sm:text-sm text-slate-500 font-medium">{stat.label}</p>
              </div>
            ))}
          </section>

          {crmStats !== null && (
            <section className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4" aria-labelledby="crm-kpi-heading">
              <h2 id="crm-kpi-heading" className="sr-only">CRM metrics</h2>
              {[
                { value: String(crmStats.activeLeadsCount), label: 'Active leads', icon: Users },
                { value: String(crmStats.activeDealsCount), label: 'Active deals', icon: Briefcase },
                { value: String(crmStats.dealsWonCount), label: 'Deals won', icon: CheckCircle2 },
                { value: String(crmStats.dealsLostCount), label: 'Deals lost', icon: XCircle },
              ].map((stat, i) => (
                <div
                  key={stat.label}
                  className="bg-white rounded-xl border border-slate-200 p-4 sm:p-5 animate-fade-in-up"
                  style={{ animationDelay: `${100 + i * 80}ms`, animationFillMode: 'backwards' }}
                >
                  <stat.icon className="w-5 h-5 text-slate-400" aria-hidden />
                  <p className="mt-2 text-lg sm:text-xl font-bold text-slate-900 tabular-nums">{stat.value}</p>
                  <p className="text-xs sm:text-sm text-slate-500 font-medium">{stat.label}</p>
                </div>
              ))}
            </section>
          )}

          {/* Asymmetrical layout: Create copy (2/3) + Activity (1/3) */}
          <div className="grid lg:grid-cols-3 gap-6 lg:gap-8">
            {/* Primary action: Create copy — hero section, 2 cols on desktop */}
            <section
              className="lg:col-span-2 bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden animate-scale-in"
              aria-labelledby="create-copy-heading"
            >
              <div className="px-5 sm:px-6 py-4 border-b border-slate-100 flex items-center gap-3">
                <div className="flex items-center justify-center w-9 h-9 rounded-lg bg-orange-100 text-orange-600">
                  <Sparkles className="w-4 h-4" aria-hidden />
                </div>
                <div>
                  <h2 id="create-copy-heading" className="text-base font-bold text-slate-900">Create new copy</h2>
                  <p className="text-xs text-slate-500">Choose a type, set your goal, generate.</p>
                </div>
              </div>
              <div className="p-5 sm:p-6">
                <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-3">Content type</label>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2 sm:gap-3">
                  {copyTypes.map((type) => {
                    const isSelected = selectedType === type.id;
                    return (
                      <button
                        key={type.id}
                        type="button"
                        onClick={() => setSelectedType(type.id)}
                        className={cn(
                          'flex flex-col items-center text-center p-3 sm:p-4 rounded-lg border-2 transition-all duration-200',
                          isSelected
                            ? 'border-orange-500 bg-orange-50 text-orange-800'
                            : 'border-slate-200 bg-slate-50/50 hover:border-slate-300 hover:bg-slate-100 text-slate-700'
                        )}
                      >
                        <span
                          className={cn(
                            'flex items-center justify-center w-10 h-10 sm:w-11 sm:h-11 rounded-lg mb-2 transition-colors',
                            isSelected ? 'bg-orange-500 text-white' : 'bg-slate-200 text-slate-600'
                          )}
                        >
                          <type.icon className="w-5 h-5" />
                        </span>
                        <span className="text-xs sm:text-sm font-medium">{type.title}</span>
                      </button>
                    );
                  })}
                </div>

                {selectedType ? (
                  <div className="mt-6 pt-5 border-t border-slate-100 space-y-4">
                    <div>
                      <label htmlFor="goal" className="block text-sm font-medium text-slate-700 mb-1.5">Message goal</label>
                      <div className="relative">
                        <select
                          id="goal"
                          value={goal}
                          onChange={(e) => setGoal(e.target.value)}
                          className="w-full h-10 pl-3 pr-9 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm focus:border-orange-500 focus:ring-1 focus:ring-orange-500 outline-none"
                        >
                          {goals.map((g) => (
                            <option key={g} value={g}>{g}</option>
                          ))}
                        </select>
                        <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400 pointer-events-none" />
                      </div>
                    </div>
                    <div>
                      <label htmlFor="context" className="block text-sm font-medium text-slate-700 mb-1.5">Optional context</label>
                      <textarea
                        id="context"
                        value={context}
                        onChange={(e) => setContext(e.target.value)}
                        placeholder="Previous conversation, company info, pain points..."
                        rows={2}
                        className="w-full px-3 py-2.5 rounded-lg border border-slate-300 bg-white text-slate-900 text-sm placeholder:text-slate-400 focus:border-orange-500 focus:ring-1 focus:ring-orange-500 outline-none resize-none"
                      />
                      <p className="flex items-center gap-1.5 mt-1.5 text-xs text-slate-500">
                        <Lightbulb className="w-3.5 h-3.5 shrink-0" />
                        More context improves output.
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
                              'flex-1 py-2.5 px-3 rounded-lg border-2 text-left transition-colors',
                              length === opt.id
                                ? 'border-orange-500 bg-orange-50 text-orange-800'
                                : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'
                            )}
                          >
                            <span className="block text-sm font-medium">{opt.label}</span>
                            <span className="block text-xs text-slate-500">{opt.sub}</span>
                          </button>
                        ))}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={handleGenerate}
                      disabled={isGenerating}
                      className={cn(
                        'w-full sm:w-auto min-w-[180px] h-10 px-5 rounded-lg font-semibold text-white flex items-center justify-center gap-2 transition-colors',
                        'bg-orange-600 hover:bg-orange-500 active:scale-[0.98]',
                        'disabled:bg-slate-300 disabled:cursor-not-allowed'
                      )}
                    >
                      {isGenerating ? (
                        <>
                          <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                          Generating...
                        </>
                      ) : (
                        <>
                          <Sparkles className="w-4 h-4" />
                          Generate copy
                        </>
                      )}
                    </button>
                  </div>
                ) : (
                  <EmptyState
                    icon={Sparkles}
                    title="Select a content type"
                    description="Click one of the options above to create your copy."
                    actionLabel="Try a template"
                    actionHref="/templates"
                    className="mt-6 animate-fade-in"
                  />
                )}
              </div>
            </section>

            {/* Secondary: Recent activity + Quick nav — 1 col on desktop */}
            <aside className="space-y-6">
              <section
                className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden animate-scale-in"
                style={{ animationDelay: '100ms', animationFillMode: 'backwards' }}
                aria-labelledby="recent-activity-heading"
              >
                <div className="px-5 py-4 border-b border-slate-100 flex items-center gap-2">
                  <History className="w-4 h-4 text-slate-500" aria-hidden />
                  <h2 id="recent-activity-heading" className="text-sm font-bold text-slate-900">Recent activity</h2>
                </div>
                <div className="p-5">
                  {recentActivity.length === 0 ? (
                    <EmptyState
                      icon={History}
                      title="No activity yet"
                      description="Generate copy and send it to your CRM."
                      actionLabel="Try a template"
                      actionHref="/templates"
                      className="!p-6 !pt-4"
                    />
                  ) : (
                    <ul className="space-y-0">
                      {recentActivity.map((item, i) => (
                        <li
                          key={item.id}
                          className="flex items-center gap-2 py-3 border-b border-slate-100 last:border-0 last:pb-0 first:pt-0 text-sm"
                        >
                          <span className="font-medium text-slate-900 truncate">{item.type}</span>
                          <span className="text-slate-400 shrink-0">→</span>
                          <span className="text-slate-600 truncate">{item.recipientName}</span>
                          <span className="ml-auto text-xs text-slate-400 shrink-0">
                            {new Date(item.createdAt).toLocaleDateString(undefined, { dateStyle: 'short' })}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                  <Link
                    to="/history"
                    className="mt-4 flex items-center gap-1.5 text-sm font-medium text-orange-600 hover:text-orange-700"
                  >
                    View full history
                    <ArrowRight className="w-4 h-4" />
                  </Link>
                </div>
              </section>

              {/* Quick nav — compact, icon + label */}
              <nav className="bg-white rounded-xl border border-slate-200 p-4 animate-fade-in-up" style={{ animationDelay: '150ms', animationFillMode: 'backwards' }} aria-label="Quick links">
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-3">Go to</p>
                <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                  {quickNavItems.map((item) => (
                    <Link
                      key={item.to}
                      to={item.to}
                      className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium text-slate-700 hover:bg-slate-50 hover:text-orange-600 transition-colors"
                    >
                      <item.icon className="w-4 h-4 shrink-0 text-slate-500" />
                      <span>{item.label}</span>
                    </Link>
                  ))}
                </div>
                <div className="mt-3 pt-3 border-t border-slate-100 flex flex-wrap gap-2">
                  <Link to="/templates" className="flex items-center gap-1.5 text-xs font-medium text-slate-600 hover:text-orange-600">
                    <LayoutTemplate className="w-3.5 h-3.5" /> Templates
                  </Link>
                  <Link to="/settings" className="flex items-center gap-1.5 text-xs font-medium text-slate-600 hover:text-orange-600">
                    <Settings className="w-3.5 h-3.5" /> Settings
                  </Link>
                  <Link to="/connect" className="flex items-center gap-1.5 text-xs font-medium text-slate-600 hover:text-orange-600">
                    <Link2 className="w-3.5 h-3.5" /> Connection
                  </Link>
                </div>
              </nav>
            </aside>
          </div>
        </div>
      </main>
    </div>
  );
}
