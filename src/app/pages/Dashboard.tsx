import { useState, useEffect, useMemo, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { Copy, Check, Paperclip } from 'lucide-react';
import { getCurrentUser, isDemoMode } from '@/app/lib/auth';
import AppHeader from '@/app/components/AppHeader';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { PageTransition } from '@/app/components/PageTransition';
import {
  getTemplateById,
  getUserSettings,
  sendCopyToCrm,
} from '@/app/api';
import {
  useActivitiesPage, useAllContacts, useAllDeals, useAllLeads, useCopyStats, useDashboardStats, usePipelineByAssignee,
  usePipelineByStage, useTemplates,
} from '@/app/hooks/queries';
import { 
  generateCopyWithRecipient, 
  generateCopyInLanguage,
  type RecipientContext, 
  type SupportedLanguage,
  SUPPORTED_LANGUAGES 
} from '@/app/api/copyGenerator';
import type { Lead, Contact, Deal } from '@/app/api/types';
import type { PipelineValueByAssignee } from '@/app/api/reporting';
import { toast } from 'sonner';
import { messages } from '@/app/api/messages';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

// Import extracted components and config
import { DashboardHero } from './dashboard/DashboardHero';
import { StaggerItem } from '@/app/components/motion/Stagger';
import { PipelineChart } from './dashboard/PipelineChart';
import { QuickNav } from './dashboard/QuickNav';
import { CopyStatsWidget } from './dashboard/CopyStatsWidget';
import { RecentActivity } from './dashboard/RecentActivity';
import { TeamPerformance } from './dashboard/TeamPerformance';
import { SalesWriter } from './dashboard/SalesWriter';
import type { WriterBrief } from '@/app/salesAgent/writerBrief';
import { copyTypes, goals } from './dashboard/config';
import { mostRecent } from './dashboard/dashboardData';
import type { DashboardStats, CopyStats, PipelineStage } from './dashboard/types';

const NO_STAGES: PipelineStage[] = [];
const NO_TEAM: PipelineValueByAssignee[] = [];
const NO_LEADS: Lead[] = [];
const NO_CONTACTS: Contact[] = [];
const NO_DEALS: Deal[] = [];

export default function Dashboard() {
  const location = useLocation();
  const templateId = (location.state as { templateId?: string } | null)?.templateId;
  const [selectedType, setSelectedType] = useState<typeof copyTypes[number]['id'] | ''>(() =>
    isDemoMode() ? 'sales-email' : ''
  );
  const [goal, setGoal] = useState<string>(goals[0]!);
  const [context, setContext] = useState('');
  const [length, setLength] = useState<'short' | 'medium' | 'long'>('medium');
  const [language, setLanguage] = useState<SupportedLanguage>('en');
  const [isGenerating, setIsGenerating] = useState(false);
  // Each figure is its own read through the shared cache, and fails on its own: a broken
  // endpoint shows as "could not be loaded" with a retry, never as a zero, which is
  // indistinguishable from an empty CRM. "Nothing yet", "nothing there" and "we could
  // not look" are three different things, and only the middle one is a zero.
  const copyStatsQuery = useCopyStats();
  const templatesQuery = useTemplates();
  // A template count nobody counted is left null, which the widget shows as a dash.
  const stats: CopyStats = {
    sentThisWeek: copyStatsQuery.data?.sentThisWeek ?? null,
    totalSent: copyStatsQuery.data?.totalSent ?? null,
    templateCount: templatesQuery.data ? templatesQuery.data.length : null,
  };
  const crmQuery = useDashboardStats();
  const stagesQuery = usePipelineByStage();
  const teamQuery = usePipelineByAssignee();
  const activityQuery = useActivitiesPage({ page: 1, pageSize: 5 });
  const crmStats: DashboardStats | null = crmQuery.data ?? null;
  const pipelineByStage: PipelineStage[] = stagesQuery.data ?? NO_STAGES;
  const pipelineByAssignee: PipelineValueByAssignee[] = teamQuery.data ?? NO_TEAM;
  const recentActivity = useMemo(() => mostRecent(activityQuery.data?.items ?? [], 5), [activityQuery.data]);
  usePageLoading(crmQuery.isPending);
  /** Come back from the server (with figures or a failure), and failed with nothing to show. */
  const loadedOf = (q: { isPending: boolean }) => !q.isPending;
  const failedOf = (q: { isError: boolean; data: unknown }) => q.isError && q.data === undefined;
  // Figures on screen whose refresh failed: kept, and said to be the last copy read.
  // They used to stay with no word, so a dashboard could be hours old and look current.
  const staleOf = (q: { isError: boolean; data: unknown }) => q.isError && q.data !== undefined;
  const retryOf = (q: { refetch: () => unknown }) => () => { void q.refetch(); };

  // Recipient selection states
  const [showRecipientPicker, setShowRecipientPicker] = useState(false);
  const [recipientType, setRecipientType] = useState<'lead' | 'contact' | 'deal' | null>(null);
  // The people the writer can address, read once the picker opens on their kind.
  const leads: Lead[] = useAllLeads(showRecipientPicker && recipientType === 'lead').data ?? NO_LEADS;
  const contacts: Contact[] = useAllContacts(showRecipientPicker && recipientType === 'contact').data ?? NO_CONTACTS;
  const deals: Deal[] = useAllDeals(showRecipientPicker && recipientType === 'deal').data ?? NO_DEALS;
  const [selectedRecipient, setSelectedRecipient] = useState<{
    type: 'lead' | 'contact' | 'deal';
    id: string;
    name: string;
    email?: string;
    company?: string;
    dealStage?: string;
    dealValue?: string;
  } | null>(null);
  const [recipientSearch, setRecipientSearch] = useState('');
  
  // Generated copy state
  const [generatedCopy, setGeneratedCopy] = useState<{
    /** Optional: only the email-shaped copy types produce one. */
    subject?: string;
    body: string;
    copyTypeLabel: string;
    copyTypeId: string;
  } | null>(null);
  const [copied, setCopied] = useState(false);
  
  const user = getCurrentUser();
  const displayName = user?.name ?? 'there';
  const writerAnchor = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const state = location.state as {
      writerBrief?: WriterBrief;
      writerRecipient?: { type: 'lead' | 'contact' | 'deal'; id: string; name: string } | null;
    } | null;
    if (!state?.writerBrief) return;
    setSelectedType(state.writerBrief.copyTypeId);
    setGoal(state.writerBrief.goal);
    setContext(state.writerBrief.context);
    if (state.writerRecipient) {
      setSelectedRecipient(state.writerRecipient);
      setRecipientType(state.writerRecipient.type);
      setShowRecipientPicker(false);
    }
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    writerAnchor.current?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
  }, [location.state]);

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
      
      // Build recipient context if a recipient is selected
      let recipient: RecipientContext | undefined;
      if (selectedRecipient) {
        recipient = {
          name: selectedRecipient.name,
          email: selectedRecipient.email,
          company: selectedRecipient.company,
          type: selectedRecipient.type,
          dealStage: selectedRecipient.dealStage,
          dealValue: selectedRecipient.dealValue,
        };
      }
      
      // Use multi-language generator if not English
      const result = language !== 'en' 
        ? await generateCopyInLanguage({
            copyTypeId: selectedType,
            goal: goal || goals[0]!,
            context: context.trim() || undefined,
            length,
            brandName: settings.brandName,
            brandTone: settings.brandTone,
            recipient,
            targetLanguage: language,
          })
        : await generateCopyWithRecipient({
            copyTypeId: selectedType,
            goal: goal || goals[0]!,
            context: context.trim() || undefined,
            length,
            brandName: settings.brandName,
            brandTone: settings.brandTone,
            recipient,
          });
      
      const copyTypeLabel = copyTypes.find((t) => t.id === selectedType)?.title ?? 'Copy';
      const langName = SUPPORTED_LANGUAGES.find(l => l.code === language)?.name ?? '';
      toast.success(messages.copy.generated + (language !== 'en' ? ` (${langName})` : ''));
      setGeneratedCopy({
        subject: result.subject,
        body: result.body,
        copyTypeLabel,
        copyTypeId: selectedType,
      });
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setIsGenerating(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />

      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          {/* Hero Section - Extracted Component (it places the agent's next step after itself) */}
          <DashboardHero
            displayName={displayName}
            stats={crmStats}
            loaded={loadedOf(crmQuery)}
            failed={failedOf(crmQuery)}
            stale={staleOf(crmQuery)}
            onRetry={retryOf(crmQuery)}
            retrying={crmQuery.isFetching}
          />

        {/* Main Content Grid */}
        <div className="space-y-8">
          <div className="grid lg:grid-cols-3 gap-6 lg:gap-8">
            {/* Left Column - 2 cols */}
            <div className="lg:col-span-2 space-y-6">
              
              {/* Pipeline by Stage - Extracted Component */}
              <StaggerItem index={0}>
                <PipelineChart stages={pipelineByStage} loaded={loadedOf(stagesQuery)} failed={failedOf(stagesQuery)} stale={staleOf(stagesQuery)} onRetry={retryOf(stagesQuery)} retrying={stagesQuery.isFetching} />
              </StaggerItem>

              {/* Intelligent Sales Writer - Extracted Component */}
              <StaggerItem index={1}>
              <div id="sales-writer" ref={writerAnchor} className="scroll-mt-24">
              <SalesWriter
                selectedType={selectedType}
                setSelectedType={setSelectedType}
                goal={goal}
                setGoal={setGoal}
                context={context}
                setContext={setContext}
                length={length}
                setLength={setLength}
                language={language}
                setLanguage={setLanguage}
                isGenerating={isGenerating}
                onGenerate={handleGenerate}
                showRecipientPicker={showRecipientPicker}
                setShowRecipientPicker={setShowRecipientPicker}
                recipientType={recipientType}
                setRecipientType={setRecipientType}
                leads={leads}
                contacts={contacts}
                deals={deals}
                selectedRecipient={selectedRecipient}
                setSelectedRecipient={setSelectedRecipient}
                recipientSearch={recipientSearch}
                setRecipientSearch={setRecipientSearch}
              />
              </div>
              </StaggerItem>

              {/* Generated Copy Display */}
              {generatedCopy && (
                <StaggerItem className="bg-white dark:bg-slate-800 rounded-xl shadow-sm border border-slate-200 dark:border-slate-700 p-6">
                  <div className="flex items-center justify-between mb-4">
                    <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
                      Generated {generatedCopy.copyTypeLabel}
                    </h3>
                    <button
                      onClick={() => setGeneratedCopy(null)}
                      className="text-slate-500 hover:text-slate-600 dark:hover:text-slate-300"
                      aria-label="Close"
                    >
                      ×
                    </button>
                  </div>
                  
                  {generatedCopy.subject && (
                    <div className="mb-4">
                      <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                        Subject
                      </label>
                      <div className="p-3 bg-slate-50 dark:bg-slate-900 rounded-lg text-slate-900 dark:text-slate-100">
                        {generatedCopy.subject}
                      </div>
                    </div>
                  )}
                  
                  <div className="mb-4">
                    <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                      Body
                    </label>
                    <div className="p-3 bg-slate-50 dark:bg-slate-900 rounded-lg text-slate-900 dark:text-slate-100 whitespace-pre-wrap">
                      {generatedCopy.body}
                    </div>
                  </div>
                  
                  <div className="flex gap-3">
                    <button
                      onClick={async () => {
                        const textToCopy = generatedCopy.subject 
                          ? `${generatedCopy.subject}\n\n${generatedCopy.body}`
                          : generatedCopy.body;
                        await navigator.clipboard.writeText(textToCopy);
                        setCopied(true);
                        toast.success('Copied to clipboard');
                        setTimeout(() => setCopied(false), 2000);
                      }}
                      className="flex items-center gap-2 px-4 py-2 bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-slate-900 dark:text-slate-100 rounded-lg transition-colors"
                    >
                      {copied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                      {copied ? 'Copied!' : 'Copy'}
                    </button>
                    
                    {selectedRecipient && (
                      <button
                        onClick={async () => {
                          try {
                            await sendCopyToCrm({
                              objectType: selectedRecipient.type,
                              recordId: selectedRecipient.id,
                              recordName: selectedRecipient.name,
                              copy: generatedCopy.body,
                              copyTypeLabel: generatedCopy.copyTypeLabel,
                            });
                            toast.success(`Saved on ${selectedRecipient.name}`);
                          } catch {
                            toast.error('Could not save this writing');
                          }
                        }}
                        className="flex items-center gap-2 px-4 py-2 bg-orange-600 hover:bg-orange-500 text-white rounded-lg transition-colors"
                      >
                        <Paperclip className="w-4 h-4" />
                        Save on {selectedRecipient.name}
                      </button>
                    )}
                  </div>
                </StaggerItem>
              )}

              {/* Team Performance - Extracted Component */}
              <StaggerItem index={2}>
                <TeamPerformance members={pipelineByAssignee} loaded={loadedOf(teamQuery)} failed={failedOf(teamQuery)} stale={staleOf(teamQuery)} onRetry={retryOf(teamQuery)} retrying={teamQuery.isFetching} />
              </StaggerItem>
            </div>

            {/* Right Column - Sidebar with Extracted Components */}
            <div className="space-y-6">
              <StaggerItem index={1}>
                <QuickNav />
              </StaggerItem>
              <StaggerItem index={2}>
                <CopyStatsWidget
                  stats={stats}
                  failed={failedOf(copyStatsQuery)}
                  stale={staleOf(copyStatsQuery)}
                  onRetry={retryOf(copyStatsQuery)}
                  retrying={copyStatsQuery.isFetching}
                />
              </StaggerItem>
              <StaggerItem index={3}>
                <RecentActivity items={recentActivity} loaded={loadedOf(activityQuery)} failed={failedOf(activityQuery)} stale={staleOf(activityQuery)} onRetry={retryOf(activityQuery)} retrying={activityQuery.isFetching} />
              </StaggerItem>
            </div>
          </div>
        </div>
        </main>
      </PageTransition>
    </div>
  );
}
