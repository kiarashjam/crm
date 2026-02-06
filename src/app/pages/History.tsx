import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Copy, RotateCcw, Search, Mail, MessageSquare, FileText, Briefcase, History as HistoryIcon, Send } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import EmptyState from '@/app/components/EmptyState';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getCopyHistory } from '@/app/api';
import type { CopyHistoryItem } from '@/app/api/types';
import { messages } from '@/app/api/messages';

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

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />

      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          {/* Enhanced Header Section with Dark Decorative Elements */}
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
          {/* Decorative blur elements */}
          <div className="absolute inset-0 overflow-hidden">
            <div className="absolute -top-20 -right-20 w-80 h-80 bg-orange-500/20 rounded-full blur-3xl animate-pulse" />
            <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-violet-500/15 rounded-full blur-3xl" />
            <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl" />
            <div className="absolute inset-0 opacity-5" style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M54.627 0l.83.828-1.415 1.415L51.8 0h2.827zM5.373 0l-.83.828L5.96 2.243 8.2 0H5.374zM48.97 0l3.657 3.657-1.414 1.414L46.143 0h2.828zM11.03 0L7.372 3.657 8.787 5.07 13.857 0H11.03zm32.284 0L49.8 6.485 48.384 7.9l-7.9-7.9h2.83zM16.686 0L10.2 6.485 11.616 7.9l7.9-7.9h-2.83zM22.343 0L13.857 8.485 15.272 9.9l9.9-9.9h-2.83zM32 0l-3.486 3.485-1.414-1.414L30.586 0H32zM0 5.373l.828-.83 1.415 1.415L0 8.2V5.374zm0 5.656l.828-.829 5.657 5.657-1.414 1.414L0 11.03v-.001zm0 5.656l.828-.828 8.485 8.485-1.414 1.414L0 16.686v-.001zm0 5.657l.828-.828 11.314 11.314-1.414 1.414L0 22.343v-.001zM60 5.373l-.828-.83-1.415 1.415L60 8.2V5.374zm0 5.656l-.828-.829-5.657 5.657 1.414 1.414L60 11.03v-.001zm0 5.656l-.828-.828-8.485 8.485 1.414 1.414L60 16.686v-.001zm0 5.657l-.828-.828-11.314 11.314 1.414 1.414L60 22.343v-.001z' fill='%23fff' fill-opacity='1' fill-rule='evenodd'/%3E%3C/svg%3E")` }} />
          </div>
          
          <div className="relative px-6 lg:px-8 py-8 lg:py-10">
            <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
              <div className="flex items-center gap-5">
                <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-2xl shadow-orange-500/30">
                  <HistoryIcon className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Copy History</h1>
                  <p className="text-slate-400 mt-1">View and reuse your previously generated copy</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="w-full">

          {/* Search Bar - Modern Dark Theme */}
          <div className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mb-6 shadow-xl overflow-hidden">
            {/* Decorative elements */}
            <div className="absolute top-0 right-0 w-32 h-32 bg-teal-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
            <div className="absolute bottom-0 left-0 w-24 h-24 bg-cyan-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
            
            <div className="relative">
              {/* Search Input - Enhanced */}
              <div className="relative group">
                <div className="absolute inset-0 bg-gradient-to-r from-teal-500/20 via-cyan-500/20 to-teal-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                <div className="relative">
                  <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-teal-500/20 to-cyan-500/20 flex items-center justify-center group-focus-within:from-teal-500/40 group-focus-within:to-cyan-500/40 transition-all duration-300">
                    <Search className="w-4 h-4 text-teal-300 group-focus-within:text-teal-200 transition-colors" aria-hidden />
                  </div>
                  <input
                    type="search"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search your copy history..."
                    className="w-full h-11 pl-14 pr-10 border border-white/10 rounded-xl bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:ring-2 focus:ring-teal-400/20 focus:border-teal-400/50 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 outline-none transition-all duration-300"
                    aria-label="Search copy history"
                  />
                  {searchQuery && (
                    <button
                      type="button"
                      onClick={() => setSearchQuery('')}
                      className="absolute right-3 top-1/2 -translate-y-1/2 w-6 h-6 rounded-md bg-white/10 hover:bg-red-500/30 flex items-center justify-center text-slate-400 hover:text-red-300 transition-all duration-200"
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
                  <span className="text-sm text-slate-400">
                    {filteredItems.length} {filteredItems.length === 1 ? 'item' : 'items'}
                  </span>
                  {searchQuery.trim() && (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white/10 text-white text-xs font-medium border border-white/10">
                      <Search className="w-3 h-3" />
                      &quot;{searchQuery.trim()}&quot;
                    </span>
                  )}
                  {searchQuery.trim() && filteredItems.length === 0 && items.length > 0 && (
                    <span className="text-sm text-slate-400">
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
              {filteredItems.map((item) => {
                const Icon = typeIcons[item.type] ?? FileText;
                return (
                  <article
                    key={item.id}
                    className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 hover:shadow-md transition-shadow"
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
                            aria-label="Send again to CRM"
                          >
                            <Send className="w-4 h-4" aria-hidden />
                            Send again
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
                  </article>
                );
              })}
            </div>
          ) : (
            <EmptyState
              icon={HistoryIcon}
              title={items.length === 0 ? 'No copy history yet' : 'No results found'}
              description={
                items.length === 0
                  ? 'Copy you send to CRM will appear here. Generate and send your first copy to get started.'
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
