import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { CheckCircle, Search, FileText } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import { getContacts, getDeals, sendCopyToCrm } from '@/app/api';
import type { Contact, Deal } from '@/app/api/types';
import { messages } from '@/app/api/messages';

type ObjectType = 'contact' | 'deal' | 'workflow' | 'email';

export default function SendToCrm() {
  const location = useLocation();
  const state = location.state as { copy?: string; copyTypeLabel?: string } | null;
  const copy = state?.copy ?? '';
  const copyTypeLabel = state?.copyTypeLabel ?? 'Copy';

  const [objectType, setObjectType] = useState<ObjectType>('contact');
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedRecord, setSelectedRecord] = useState<{ id: string; name: string } | null>(null);
  const [isSent, setIsSent] = useState(false);
  const [sending, setSending] = useState(false);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    Promise.all([getContacts(), getDeals()])
      .then(([c, d]) => {
        setContacts(c);
        setDeals(d);
        setLoading(false);
      })
      .catch(() => setLoading(false));
  }, []);

  const filteredContacts = searchQuery.trim()
    ? contacts.filter(
        (c) =>
          c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          c.email.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : contacts;

  const filteredDeals = searchQuery.trim()
    ? deals.filter(
        (d) =>
          d.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          d.value.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : deals;

  const records = objectType === 'contact'
    ? filteredContacts.map((c) => ({ id: c.id, name: c.name, sub: c.email }))
    : objectType === 'deal'
      ? filteredDeals.map((d) => ({
          id: d.id,
          name: d.name,
          sub: d.stage ? `${d.value} · ${d.stage}` : d.value,
        }))
      : [];

  const handleSend = async () => {
    if (!selectedRecord || !copy.trim()) return;
    setSending(true);
    try {
      await sendCopyToCrm({
        objectType,
        recordId: selectedRecord.id,
        recordName: selectedRecord.name,
        copy,
        copyTypeLabel,
      });
      toast.success(messages.copy.sentToCrm);
      setIsSent(true);
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSending(false);
    }
  };

  if (!copy.trim() && !isSent) {
    return (
      <div className="min-h-screen bg-slate-50">
        <AppHeader />
        <main className="w-full max-w-2xl mx-auto px-[var(--page-padding)] py-12">
          <EmptyState
            icon={FileText}
            title="No copy to send"
            description="Generate copy on the dashboard first, then come back here to send it to a contact or deal."
            actionLabel="Go to Dashboard"
            actionHref="/dashboard"
          />
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main id={MAIN_CONTENT_ID} className="w-full max-w-2xl mx-auto px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full">
          {!isSent ? (
            <>
              <div className="mb-8">
                <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Send to CRM</h1>
                <p className="text-slate-600">Choose where to save your generated copy</p>
              </div>

              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 sm:p-8">
                <div className="mb-6">
                  <label className="block text-sm font-medium text-slate-700 mb-3">
                    Select object type
                  </label>
                  <div className="grid grid-cols-2 gap-3">
                    {[
                      { id: 'contact' as const, label: 'Contact', sub: 'Individual person' },
                      { id: 'deal' as const, label: 'Deal', sub: 'Sales opportunity' },
                      { id: 'workflow' as const, label: 'Workflow', sub: 'Automation sequence' },
                      { id: 'email' as const, label: 'Email draft', sub: 'Save as template' },
                    ].map((opt) => (
                      <button
                        key={opt.id}
                        type="button"
                        onClick={() => {
                          setObjectType(opt.id);
                          setSelectedRecord(null);
                        }}
                        className={`p-4 rounded-xl border-2 transition-colors text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 ${
                          objectType === opt.id
                            ? 'border-orange-500 bg-orange-50 text-slate-900'
                            : 'border-slate-200 hover:border-orange-200 bg-white'
                        }`}
                      >
                        <p className="font-semibold text-slate-900">{opt.label}</p>
                        <p className="text-xs text-slate-600 mt-0.5">{opt.sub}</p>
                      </button>
                    ))}
                  </div>
                </div>

                {(objectType === 'contact' || objectType === 'deal') && (
                  <div className="mb-8">
                    <label className="block text-sm font-medium text-slate-700 mb-3">
                      Select specific record
                    </label>
                    <div className="relative mb-3">
                      <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" aria-hidden />
                      <input
                        type="search"
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        placeholder={`Search ${objectType}s...`}
                        className="w-full h-11 pl-10 pr-4 border border-slate-300 rounded-lg bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                        aria-label={`Search ${objectType}s`}
                      />
                    </div>
                    {loading ? (
                      <div className="py-10 flex justify-center">
                        <LoadingSpinner size="md" />
                      </div>
                    ) : (
                      <div className="border border-slate-200 rounded-xl divide-y max-h-64 overflow-y-auto">
                        {records.map((record) => (
                          <label
                            key={record.id}
                            className="flex items-center p-4 hover:bg-slate-50 cursor-pointer transition-colors"
                          >
                            <input
                              type="radio"
                              name="record"
                              value={record.id}
                              checked={selectedRecord?.id === record.id}
                              onChange={() => setSelectedRecord({ id: record.id, name: record.name })}
                              className="w-4 h-4 text-orange-600 focus:ring-orange-500 focus:ring-offset-0"
                            />
                            <div className="ml-3 min-w-0">
                              <p className="font-medium text-slate-900 truncate">{record.name}</p>
                              <p className="text-sm text-slate-600 truncate">{record.sub}</p>
                            </div>
                          </label>
                        ))}
                        {records.length === 0 && (
                          <div className="p-6 text-center text-slate-500 text-sm">No records found</div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {(objectType === 'workflow' || objectType === 'email') && (
                  <div className="mb-8 p-4 bg-amber-50 border border-amber-200 rounded-xl text-sm text-amber-800">
                    In the demo, select Contact or Deal to send your copy. Workflow and Email draft will be available with a full CRM integration.
                  </div>
                )}

                <button
                  type="button"
                  onClick={handleSend}
                  disabled={!selectedRecord || sending || (objectType !== 'contact' && objectType !== 'deal')}
                  className="w-full h-12 bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-500 disabled:cursor-not-allowed text-white font-semibold rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  {sending ? 'Sending...' : 'Confirm & Send'}
                </button>
              </div>
            </>
          ) : (
            <div className="text-center">
              <div className="w-20 h-20 bg-emerald-100 rounded-full flex items-center justify-center mx-auto mb-6" aria-hidden>
                <CheckCircle className="w-12 h-12 text-emerald-600" />
              </div>
              <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-4">Successfully Sent!</h1>
              <p className="text-slate-600 mb-8 max-w-md mx-auto">
                Your copy has been added to your CRM and is ready to use.
              </p>

              {selectedRecord && (
                <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6 mb-8 text-left max-w-md mx-auto">
                  <p className="text-sm text-slate-600 mb-2">Sent to</p>
                  <p className="font-semibold text-slate-900 mb-1">{selectedRecord.name}</p>
                  <p className="text-sm text-slate-600 capitalize">{objectType}</p>
                </div>
              )}

              <div className="flex gap-4 justify-center flex-wrap">
                <button
                  type="button"
                  onClick={() => navigate('/dashboard')}
                  className="flex-1 min-w-[140px] h-11 bg-orange-600 hover:bg-orange-500 text-white font-semibold rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  Create Another
                </button>
                <button
                  type="button"
                  onClick={() => navigate('/history')}
                  className="flex-1 min-w-[140px] h-11 bg-white hover:bg-slate-50 text-slate-900 border-2 border-slate-200 font-semibold rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  View History
                </button>
              </div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
