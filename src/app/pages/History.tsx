import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Copy, RotateCcw, Search, Mail, MessageSquare, FileText, Briefcase, History as HistoryIcon } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import EmptyState from '@/app/components/EmptyState';
import LoadingSpinner from '@/app/components/LoadingSpinner';
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
  const navigate = useNavigate();

  useEffect(() => {
    getCopyHistory()
      .then((list) => {
        setItems(list);
        setLoading(false);
      })
      .catch(() => setLoading(false));
  }, []);

  const handleCopy = (id: string, text: string) => {
    try {
      navigator.clipboard.writeText(text);
      setCopiedId(id);
      toast.success(messages.copy.copied);
      setTimeout(() => setCopiedId(null), 2000);
    } catch {
      toast.error(messages.errors.generic);
    }
  };

  const handleRegenerate = (item: CopyHistoryItem) => {
    navigate('/dashboard', { state: { regenerateContext: item.copy.slice(0, 300) } });
  };

  const filteredItems = items.filter(
    (item) =>
      item.copy.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.type.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.recipientName.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Copy History</h1>
            <p className="text-slate-600">View and reuse your previously generated copy</p>
          </div>

          <div className="mb-6">
            <div className="relative">
              <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" aria-hidden />
              <input
                type="search"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search your copy history..."
                className="w-full h-11 pl-12 pr-4 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                aria-label="Search copy history"
              />
            </div>
          </div>

          {loading ? (
            <div className="py-14 flex justify-center">
              <LoadingSpinner size="md" />
            </div>
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
                      <div className="w-12 h-12 bg-orange-100 rounded-xl flex items-center justify-center flex-shrink-0 text-orange-600">
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

                        <div className="flex gap-4">
                          <button
                            type="button"
                            onClick={() => handleCopy(item.id, item.copy)}
                            className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors focus-visible:rounded"
                          >
                            <Copy className="w-4 h-4" aria-hidden />
                            {copiedId === item.id ? 'Copied!' : 'Copy'}
                          </button>
                          <button
                            type="button"
                            onClick={() => handleRegenerate(item)}
                            className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors focus-visible:rounded"
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
    </div>
  );
}
