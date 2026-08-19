import { Link } from 'react-router-dom';
import { Activity, Phone, Mail, FileText, Users, Video, Presentation, CheckCircle, CalendarClock, Clock, TriangleAlert } from 'lucide-react';
import type { Activity as ActivityType } from '@/app/api/types';

interface RecentActivityProps {
  /** Already sorted newest-first by `mostRecent`. */
  items: ActivityType[];
  loaded: boolean;
  /** True when the request failed, which is not "nothing has happened". */
  failed: boolean;
}

function getActivityIcon(type: string) {
  switch (type.toLowerCase()) {
    case 'call':
      return Phone;
    case 'email':
      return Mail;
    case 'meeting':
      return Users;
    case 'note':
      return FileText;
    case 'video':
      return Video;
    case 'demo':
      return Presentation;
    case 'task':
      return CheckCircle;
    case 'follow_up':
      return CalendarClock;
    case 'deadline':
      return Clock;
    default:
      return Activity;
  }
}

function getActivityIconColor(type: string) {
  switch (type.toLowerCase()) {
    case 'call':
      return 'bg-emerald-100 text-emerald-600';
    case 'email':
      return 'bg-amber-100 text-amber-600';
    case 'meeting':
      return 'bg-blue-100 text-blue-600';
    case 'note':
      return 'bg-slate-100 text-slate-600';
    case 'video':
      return 'bg-purple-100 text-purple-600';
    case 'demo':
      return 'bg-rose-100 text-rose-600';
    case 'task':
      return 'bg-cyan-100 text-cyan-600';
    case 'follow_up':
      return 'bg-orange-100 text-orange-600';
    case 'deadline':
      return 'bg-red-100 text-red-600';
    default:
      return 'bg-slate-100 text-slate-600';
  }
}

export function RecentActivity({ items, loaded, failed }: RecentActivityProps) {
  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Activity className="w-4 h-4 text-orange-500" />
          <h2 className="text-sm font-bold text-slate-900">Recent Activity</h2>
        </div>
        <Link to="/activities" className="text-xs text-orange-600 hover:text-orange-700 font-medium">View all</Link>
      </div>
      <div className="p-5">
        {failed ? (
          <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2.5">
            <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
            <p className="text-xs text-amber-900">
              <strong className="font-semibold">Could not load activity.</strong> An empty panel
              here would have read as "nothing has happened".
            </p>
          </div>
        ) : !loaded ? (
          <div className="space-y-3" aria-busy="true">
            <p className="sr-only" role="status">Loading recent activity…</p>
            {[0, 1, 2, 3].map((i) => (
              <div key={i} className="flex items-center gap-3">
                <div className="h-8 w-8 shrink-0 animate-pulse rounded-lg bg-slate-200" />
                <div className="flex-1 space-y-1.5">
                  <div className="h-3 w-32 animate-pulse rounded bg-slate-200" />
                  <div className="h-2.5 w-20 animate-pulse rounded bg-slate-100" />
                </div>
              </div>
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="text-center py-6">
            <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center mx-auto mb-3">
              <Activity className="w-5 h-5 text-slate-400" />
            </div>
            <p className="text-sm text-slate-600 font-medium">No recent CRM activities</p>
            <p className="text-xs text-slate-500 mt-1">Log activities to track your sales interactions</p>
          </div>
        ) : (
          <div className="space-y-3">
            {items.map((item) => {
              const Icon = getActivityIcon(item.type);
              const iconColor = getActivityIconColor(item.type);
              const relatedName = item.contactName || item.dealName || item.leadName || '';
              
              return (
                <div
                  key={item.id}
                  className="flex items-center gap-3 p-2 -mx-2 rounded-lg hover:bg-slate-50 transition-colors"
                >
                  <div className={`w-8 h-8 rounded-lg ${iconColor} flex items-center justify-center shrink-0`}>
                    <Icon className="w-4 h-4" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-slate-900 truncate">{item.subject || item.type}</p>
                    {(relatedName || item.createdByName) && (
                      <p className="text-xs text-slate-500 truncate">
                        {relatedName}
                        {relatedName && item.createdByName && <> &middot; </>}
                        {item.createdByName && <>by {item.createdByName}</>}
                      </p>
                    )}
                  </div>
                  <span className="text-xs text-slate-400 shrink-0">
                    {new Date(item.createdAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                  </span>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}

export default RecentActivity;
