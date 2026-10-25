import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Copy, RotateCcw, Search, Mail, MessageSquare, FileText, Briefcase, History as HistoryIcon, Paperclip, Users, CalendarDays, Layers } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import EmptyState from '@/app/components/EmptyState';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getCopyHistory } from '@/app/api';
import type { CopyHistoryItem } from '@/app/api/types';
import { messages } from '@/app/api/messages';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';

const typeIcons: Record<string, typeof Mail> = {
  'Sales Email': Mail,
  'Follow-up': MessageSquare,
  'CRM Note': FileText,
  'Deal Message': Briefcase,
  'Workflow': MessageSquare,
};

function formatDate(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins} minute${diffMins === 1 ? '' : 's'} ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours === 1 ? '' : 's'} ago`;
  if (diffDays < 7) return `${diffDays} day${diffDays === 1 ? '' : 's'} ago`;
  return d.toLocaleDateString();
}

export default function History() {
  const [searchQuery, setSearchQuery] = useState('');
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [items, setItems] = useState<CopyHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const copyTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    let cancelled = false;
    getCopyHistory()
      .then((list) => {
        if (!cancelled) {
          setItems(list);
          setLoading(false);
        }
      })
      .catch(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const handleCopy = (id: string, text: string) => {
    try {
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
      navigator.clipboard.writeText(text);
      setCopiedId(id);
      toast.success(messages.copy.copied);
      copyTimeoutRef.current = setTimeout(() => {
        copyTimeoutRef.current = null;
        setCopiedId(null);
      }, 2000);
    } catch {
      toast.error(messages.errors.generic);
    }
  };

  useEffect(() => {
    return () => {
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
    };
  }, []);

  const handleRegenerate = (item: CopyHistoryItem) => {
    navigate('/dashboard', { state: { regenerateContext: item.copy.slice(0, 300) } });
  };

  const handleSendAgain = (item: CopyHistoryItem) => {
    navigate('/send', { state: { copy: item.copy, copyTypeLabel: item.type } });
  };

  const filteredItems = items.filter(
    (item) =>
      item.copy.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.type.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.recipientName.toLowerCase().includes(searchQuery.toLowerCase())
  );

  // The history's numbers, for the header.
  const weekAgo = Date.now() - 7 * 86400000;
  const thisWeekCount = items.filter((item) => new Date(item.createdAt).getTime() >= weekAgo).length;
  const recipientCount = new Set(items.map((item) => item.recipientName)).size;
  const typeCount = new Set(items.map((item) => item.type)).size;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />

      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={HistoryIcon}
            tone="cyan"
            title="Copy History"
            subtitle="View and reuse your previously generated copy"
            stats={loading ? undefined : [
              { label: 'Saved copy', value: items.length, icon: HistoryIcon, tone: 'cyan', hint: 'all time' },
              { label: 'This week', value: thisWeekCount, icon: CalendarDays, tone: 'violet', hint: 'last 7 days' },
              { label: 'Recipients', value: recipientCount, icon: Users, tone: 'amber', hint: 'people written to' },
              { label: 'Kinds of copy', value: typeCount, icon: Layers, tone: 'emerald', hint: 'emails, notes, messages' },
            ]}
          />

        <div className="w-full">

          {/* Search bar */}
          <div className="relative mb-6 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur">
            {/* Decorative elements */}
            <div className="absolute top-0 right-0 w-32 h-32 bg-cyan-300/25 rounded-full blur-2xl -mr-10 -mt-10" />
            <div className="absolute bottom-0 left-0 w-24 h-24 bg-teal-300/25 rounded-full blur-2xl -ml-10 -mb-10" />
            
            <div className="relative">
              {/* Search Input - Enhanced */}
              <div className="relative group">
                <div className="absolute inset-0 bg-gradient-to-r from-cyan-500/20 via-teal-500/20 to-cyan-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                <div className="relative">
                  <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-cyan-100 to-sky-100 flex items-center justify-center group-focus-within:from-cyan-200 group-focus-within:to-sky-200 transition-all duration-300">
                    <Search className="w-4 h-4 text-cyan-600 group-focus-within:text-cyan-700 transition-colors" aria-hidden />
                  </div>
                  <input
                    type="search"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search your copy history..."
                    className="w-full h-11 pl-14 pr-10 border border-slate-200 rounded-xl bg-slate-50/80 text-slate-900 placeholder:text-slate-400 focus:border-cyan-300 focus:ring-4 focus:ring-cyan-200/50 focus:bg-white hover:bg-white hover:border-slate-300 outline-none transition-all duration-300"
                    aria-label="Search copy history"
                  />
                  {searchQuery && (
                    <button
                      type="button"
                      onClick={() => setSearchQuery('')}
                      className="absolute right-3 top-1/2 -translate-y-1/2 w-6 h-6 rounded-md bg-slate-100 hover:bg-red-50 flex items-center justify-center text-slate-400 hover:text-red-500 transition-all duration-200"
                    >
                      <svg className="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M18 6 6 18" /><path d="m6 6 12 12" />
                      </svg>
                    </button>
                  )}
                </div>
              </div>
              {!loading && (
                <div className="mt-3 flex flex-wrap items-center gap-2">
                  <span className="text-sm text-slate-500">
                    {filteredItems.length} {filteredItems.length === 1 ? 'item' : 'items'}
                  </span>
                  {searchQuery.trim() && (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-cyan-50 text-cyan-700 text-xs font-medium border border-cyan-200">
                      <Search className="w-3 h-3" />
                      &quot;{searchQuery.trim()}&quot;
                    </span>
                  )}
                  {searchQuery.trim() && filteredItems.length === 0 && items.length > 0 && (
                    <span className="text-sm text-slate-500">
                      — No results. Try a different search.
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>

          {loading ? (
            <ContentSkeleton rows={5} />
          ) : filteredItems.length > 0 ? (
            <div className="space-y-4">
              {filteredItems.map((item, index) => {
                const Icon = typeIcons[item.type] ?? FileText;
                return (
                  <StaggerItem
                    as="article"
                    key={item.id}
                    index={index}
                    className={`bg-white rounded-2xl border border-slate-200 shadow-sm p-6 hover:border-cyan-200 ${liftOnHover}`}
                  >
                    <div className="flex items-start gap-4">
                      <div className="w-12 h-12 bg-teal-100 rounded-xl flex items-center justify-center flex-shrink-0 text-teal-600">
                        <Icon className="w-6 h-6" aria-hidden />
                      </div>

                      <div className="flex-1 min-w-0">
                        <div className="flex items-start justify-between gap-4 mb-2">
                          <div>
                            <h2 className="font-semibold text-slate-900">{item.type}</h2>
                            <p className="text-sm text-slate-600">To: {item.recipientName}</p>
                          </div>
                          <time dateTime={item.createdAt} className="text-xs text-slate-500 whitespace-nowrap shrink-0">
                            {formatDate(item.createdAt)}
                          </time>
                        </div>

                        <p className="text-slate-700 mb-4 line-clamp-2">{item.copy}</p>

                        <div className="flex flex-wrap gap-4">
                          <button
                            type="button"
                            onClick={() => handleCopy(item.id, item.copy)}
                            className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors focus-visible:rounded"
                            aria-label={copiedId === item.id ? 'Copied' : 'Copy to clipboard'}
                          >
                            <Copy className="w-4 h-4" aria-hidden />
                            {copiedId === item.id ? 'Copied!' : 'Copy'}
                          </button>
                          <button
                            type="button"
                            onClick={() => handleSendAgain(item)}
                            className="flex items-center gap-2 text-sm text-teal-600 hover:text-teal-700 font-medium transition-colors focus-visible:rounded"
                            aria-label="Attach this writing to a record"
                          >
                            <Paperclip className="w-4 h-4" aria-hidden />
                            Attach again
                          </button>
                          <button
                            type="button"
                            onClick={() => handleRegenerate(item)}
                            className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors focus-visible:rounded"
                            aria-label="Regenerate copy"
                          >
                            <RotateCcw className="w-4 h-4" aria-hidden />
                            Regenerate
                          </button>
                        </div>
                      </div>
                    </div>
                  </StaggerItem>
                );
              })}
            </div>
          ) : (
            <EmptyState
              icon={HistoryIcon}
              title={items.length === 0 ? 'No copy history yet' : 'No results found'}
              description={
                items.length === 0
                  ? 'Writing you save on a lead, contact, or deal shows up here.'
                  : 'Try a different search term.'
              }
              actionLabel={items.length === 0 ? 'Create your first copy' : undefined}
              actionHref={items.length === 0 ? '/dashboard' : undefined}
            />
          )}
        </div>
        </main>
      </PageTransition>
    </div>
  );
}
