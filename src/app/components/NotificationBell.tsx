import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, CheckCheck, CheckSquare, Briefcase, AtSign, Sparkles, UserPlus } from 'lucide-react';
import { Popover, PopoverTrigger, PopoverContent } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';
import {
  getNotifications,
  getUnreadNotificationCount,
  markNotificationRead,
  markAllNotificationsRead,
  syncTaskReminders,
  getTasks,
  type AppNotification,
  type NotificationType,
} from '@/app/api';

const TYPE_ICON: Record<NotificationType, React.ElementType> = {
  task: CheckSquare,
  deal: Briefcase,
  lead: UserPlus,
  mention: AtSign,
  system: Sparkles,
};

const TYPE_TONE: Record<NotificationType, string> = {
  task: 'bg-orange-100 text-orange-600',
  deal: 'bg-emerald-100 text-emerald-600',
  lead: 'bg-blue-100 text-blue-600',
  mention: 'bg-violet-100 text-violet-600',
  system: 'bg-slate-100 text-slate-500',
};

function relativeTime(iso: string): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return '';
  const diff = Date.now() - t;
  const m = Math.floor(diff / 60_000);
  if (m < 1) return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  if (d < 7) return `${d}d ago`;
  return new Date(t).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

/**
 * Header bell: shows an unread count and a dropdown of recent notifications.
 * In demo mode it also runs the client-side reminder engine so overdue /
 * due-today tasks surface here without a backend clock.
 */
export default function NotificationBell() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<AppNotification[]>([]);
  const [unread, setUnread] = useState(0);
  const [loading, setLoading] = useState(false);
  const synced = useRef(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      // Reminder engine: synthesize due/overdue task reminders once per session
      // before listing. Harmless if the backend also pushes reminders; essential
      // when it doesn't (notifications then live in the local store).
      if (!synced.current) {
        synced.current = true;
        try {
          syncTaskReminders(await getTasks());
        } catch {
          /* non-fatal */
        }
      }
      const [list, count] = await Promise.all([getNotifications(), getUnreadNotificationCount()]);
      setItems(list);
      setUnread(count);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial load + lightweight poll for the unread badge.
  useEffect(() => {
    void refresh();
    const id = window.setInterval(() => {
      getUnreadNotificationCount().then(setUnread).catch(() => {});
    }, 60_000);
    return () => window.clearInterval(id);
  }, [refresh]);

  // Reload the full list whenever the panel is opened.
  useEffect(() => {
    if (open) void refresh();
  }, [open, refresh]);

  const handleOpen = async (n: AppNotification) => {
    if (!n.read) {
      setItems((prev) => prev.map((x) => (x.id === n.id ? { ...x, read: true } : x)));
      setUnread((u) => Math.max(0, u - 1));
      void markNotificationRead(n.id);
    }
    setOpen(false);
    if (n.link) navigate(n.link);
  };

  const handleMarkAll = async () => {
    setItems((prev) => prev.map((x) => ({ ...x, read: true })));
    setUnread(0);
    await markAllNotificationsRead();
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-label={unread > 0 ? `Notifications (${unread} unread)` : 'Notifications'}
          className="relative flex h-9 w-9 items-center justify-center rounded-full text-slate-600 ring-1 ring-slate-200/60 bg-white/60 transition-all hover:bg-white hover:text-slate-900 hover:ring-slate-300/80 dark:bg-slate-800/60 dark:text-slate-300 dark:ring-slate-700/60"
        >
          <Bell className="h-[18px] w-[18px]" />
          {unread > 0 && (
            <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold leading-none text-white ring-2 ring-white dark:ring-slate-900">
              {unread > 9 ? '9+' : unread}
            </span>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[360px] max-w-[calc(100vw-2rem)] p-0">
        <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
          <h3 className="text-sm font-semibold text-slate-800">Notifications</h3>
          {unread > 0 && (
            <button
              type="button"
              onClick={handleMarkAll}
              className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800"
            >
              <CheckCheck className="h-3.5 w-3.5" /> Mark all read
            </button>
          )}
        </div>
        <div className="max-h-[400px] overflow-y-auto">
          {items.length === 0 ? (
            <div className="px-4 py-10 text-center text-sm text-slate-400">
              {loading ? 'Loading…' : "You're all caught up."}
            </div>
          ) : (
            <ul className="divide-y divide-slate-50">
              {items.map((n) => {
                const Icon = TYPE_ICON[n.type] ?? Sparkles;
                return (
                  <li key={n.id}>
                    <button
                      type="button"
                      onClick={() => handleOpen(n)}
                      className={cn(
                        'flex w-full items-start gap-3 px-4 py-3 text-left transition-colors hover:bg-slate-50',
                        !n.read && 'bg-indigo-50/40',
                      )}
                    >
                      <span className={cn('mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full', TYPE_TONE[n.type])}>
                        <Icon className="h-3.5 w-3.5" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="flex items-center gap-2">
                          <span className={cn('truncate text-sm', n.read ? 'font-medium text-slate-700' : 'font-semibold text-slate-900')}>
                            {n.title}
                          </span>
                          {!n.read && <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-indigo-500" />}
                        </span>
                        {n.message && <span className="mt-0.5 block truncate text-xs text-slate-500">{n.message}</span>}
                        <span className="mt-0.5 block text-[11px] text-slate-400">{relativeTime(n.createdAtUtc)}</span>
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
