import { Link } from 'react-router-dom';
import { History, Mail } from 'lucide-react';
import type { CopyHistoryItem } from '@/app/api/types';

interface RecentActivityProps {
  items: CopyHistoryItem[];
}

export function RecentActivity({ items }: RecentActivityProps) {
  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <History className="w-4 h-4 text-slate-500" />
          <h2 className="text-sm font-bold text-slate-900">Recent Activity</h2>
        </div>
        <Link to="/history" className="text-xs text-orange-600 hover:text-orange-700 font-medium">View all</Link>
      </div>
      <div className="p-5">
        {items.length === 0 ? (
          <div className="text-center py-6">
            <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center mx-auto mb-3">
              <History className="w-5 h-5 text-slate-400" />
            </div>
            <p className="text-sm text-slate-600 font-medium">No activity yet</p>
            <p className="text-xs text-slate-500 mt-1">Generated copy will appear here</p>
          </div>
        ) : (
          <div className="space-y-3">
            {items.map((item) => (
              <div
                key={item.id}
                className="flex items-center gap-3 p-2 -mx-2 rounded-lg hover:bg-slate-50 transition-colors"
              >
                <div className="w-8 h-8 rounded-lg bg-orange-100 flex items-center justify-center shrink-0">
                  <Mail className="w-4 h-4 text-orange-600" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-slate-900 truncate">{item.type}</p>
                  <p className="text-xs text-slate-500 truncate">{item.recipientName}</p>
                </div>
                <span className="text-xs text-slate-400 shrink-0">
                  {new Date(item.createdAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

export default RecentActivity;
