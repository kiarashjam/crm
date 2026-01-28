import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Copy, RotateCcw, Search, Mail, MessageSquare, FileText } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';

export default function History() {
  const [searchQuery, setSearchQuery] = useState('');
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const historyItems = [
    {
      id: '1',
      type: 'Sales Email',
      icon: Mail,
      preview: 'Hi [First Name], I hope this email finds you well! I wanted to reach out...',
      date: '2 hours ago',
      recipient: 'John Smith',
    },
    {
      id: '2',
      type: 'Follow-up Message',
      icon: MessageSquare,
      preview: 'Thanks for taking the time to meet with me yesterday. I wanted to follow up...',
      date: 'Yesterday',
      recipient: 'Sarah Johnson',
    },
    {
      id: '3',
      type: 'CRM Note',
      icon: FileText,
      preview: 'Had a productive call with the prospect. Key takeaways: They are interested...',
      date: '2 days ago',
      recipient: 'Mike Williams',
    },
    {
      id: '4',
      type: 'Deal Message',
      icon: Mail,
      preview: 'I wanted to provide you with an update on our proposal. We are excited...',
      date: '3 days ago',
      recipient: 'Acme Corp',
    },
    {
      id: '5',
      type: 'Sales Email',
      icon: Mail,
      preview: 'I noticed you downloaded our whitepaper on digital transformation...',
      date: '1 week ago',
      recipient: 'TechStart Inc',
    },
  ];

  const handleCopy = (id: string, text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  };

  const filteredItems = historyItems.filter(item =>
    item.preview.toLowerCase().includes(searchQuery.toLowerCase()) ||
    item.type.toLowerCase().includes(searchQuery.toLowerCase()) ||
    item.recipient.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Top Bar */}
      <AppHeader />

      {/* Main Content */}
      <main className="w-full px-[var(--page-padding)] py-12">
        <div className="w-full">
          <div className="mb-8">
            <h1 className="text-3xl font-bold text-gray-900 mb-2">Copy History</h1>
            <p className="text-gray-600">View and reuse your previously generated copy</p>
          </div>

          {/* Search */}
          <div className="mb-6">
            <div className="relative">
              <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search your copy history..."
                className="w-full pl-12 pr-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition-colors"
              />
            </div>
          </div>

          {/* History List */}
          <div className="space-y-4">
            {filteredItems.map((item) => (
              <div
                key={item.id}
                className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow"
              >
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center flex-shrink-0">
                    <item.icon className="w-6 h-6 text-orange-600" />
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-start justify-between mb-2">
                      <div>
                        <h3 className="font-semibold text-gray-900">{item.type}</h3>
                        <p className="text-sm text-gray-600">To: {item.recipient}</p>
                      </div>
                      <span className="text-xs text-gray-500 whitespace-nowrap ml-4">{item.date}</span>
                    </div>

                    <p className="text-gray-700 mb-4 line-clamp-2">{item.preview}</p>

                    <div className="flex gap-3">
                      <button
                        onClick={() => handleCopy(item.id, item.preview)}
                        className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors"
                      >
                        <Copy className="w-4 h-4" />
                        {copiedId === item.id ? 'Copied!' : 'Copy'}
                      </button>
                      <button className="flex items-center gap-2 text-sm text-orange-600 hover:text-orange-700 font-medium transition-colors">
                        <RotateCcw className="w-4 h-4" />
                        Regenerate
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {filteredItems.length === 0 && (
            <div className="text-center py-12">
              <p className="text-gray-500">No results found</p>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
