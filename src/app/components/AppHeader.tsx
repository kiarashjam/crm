import { useState, useRef, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Sparkles,
  User,
  UserCircle,
  LogOut,
  ChevronDown,
  Users,
  Search,
  X,
  Target,
  Building2,
} from 'lucide-react';
import { getCurrentUser, clearSession } from '@/app/lib/auth';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { isUsingRealApi } from '@/app/api';
import { authFetchJson } from '@/app/api/apiClient';
import type { GlobalSearchResult } from '@/app/api/search';
import NotificationBell from '@/app/components/NotificationBell';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/app/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/app/components/ui/avatar';
import { Button } from '@/app/components/ui/button';
import DemoBanner from './DemoBanner';
import ReadOnlyBanner from './ReadOnlyBanner';
import LocalOnlyNotice from './LocalOnlyNotice';
import { AppSideNav } from './AppSideNav';
import { NextActions } from '@/app/salesAgent/NextActions';

// HP-9: Global search bar component
function GlobalSearchBar() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<GlobalSearchResult | null>(null);
  const [loading, setLoading] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  // Keyboard shortcut: Cmd/Ctrl + K
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        setOpen(true);
        setTimeout(() => inputRef.current?.focus(), 50);
      }
      if (e.key === 'Escape') {
        setOpen(false);
        setQuery('');
        setResults(null);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  // Click outside to close
  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  // Debounced search
  useEffect(() => {
    if (!query.trim() || query.trim().length < 2) {
      setResults(null);
      return;
    }
    const timer = setTimeout(async () => {
      setLoading(true);
      try {
        const q = encodeURIComponent(query.trim());
        const res = await authFetchJson<GlobalSearchResult>(`/api/search?q=${q}`);
        setResults({
          leads: Array.isArray(res?.leads) ? res.leads : [],
          contacts: Array.isArray(res?.contacts) ? res.contacts : [],
          companies: Array.isArray(res?.companies) ? res.companies : [],
          deals: Array.isArray(res?.deals) ? res.deals : [],
        });
      } catch {
        setResults(null);
      } finally {
        setLoading(false);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [query]);

  const navigateTo = (path: string) => {
    navigate(path);
    setOpen(false);
    setQuery('');
    setResults(null);
  };

  const totalResults = results
    ? results.leads.length + results.contacts.length + results.companies.length + results.deals.length
    : 0;

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => { setOpen(true); setTimeout(() => inputRef.current?.focus(), 50); }}
        className="hidden md:flex items-center gap-2 px-3 py-1.5 rounded-full text-sm text-slate-400 dark:text-slate-500 bg-white/50 dark:bg-slate-800/50 ring-1 ring-slate-200/60 dark:ring-slate-700/60 hover:ring-slate-300 dark:hover:ring-slate-600 hover:text-slate-600 dark:hover:text-slate-300 transition-all duration-200 shrink-0"
      >
        <Search className="w-3.5 h-3.5" />
        <span>Search...</span>
        <kbd className="hidden lg:inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[10px] font-medium bg-slate-100 dark:bg-slate-700 text-slate-400 dark:text-slate-500 border border-slate-200/80 dark:border-slate-600/80">
          <span className="text-xs">⌘</span>K
        </kbd>
      </button>
    );
  }

  return (
    <div ref={containerRef} className="hidden md:block relative z-50">
      <div className="flex items-center gap-2 bg-white dark:bg-slate-800 rounded-xl ring-2 ring-[var(--brand-primary)]/40 shadow-lg shadow-[var(--brand-primary)]/10 px-3 py-1 min-w-[320px] max-w-[420px]">
        <Search className="w-4 h-4 text-slate-400 shrink-0" />
        <input
          ref={inputRef}
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search contacts, companies, deals..."
          className="flex-1 bg-transparent text-sm text-slate-800 dark:text-slate-200 placeholder:text-slate-400 focus:outline-none py-1.5"
          autoFocus
        />
        {loading && (
          <svg className="animate-spin w-4 h-4 text-slate-400" viewBox="0 0 24 24" fill="none">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        )}
        <button
          type="button"
          onClick={() => { setOpen(false); setQuery(''); setResults(null); }}
          className="p-0.5 rounded hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Results dropdown */}
      {results && query.trim().length >= 2 && (
        <div className="absolute top-full left-0 right-0 mt-2 bg-white dark:bg-slate-800 rounded-xl shadow-2xl shadow-slate-200/60 dark:shadow-slate-900/60 border border-slate-200/80 dark:border-slate-700/80 max-h-[400px] overflow-y-auto z-50">
          {totalResults === 0 ? (
            <div className="px-4 py-6 text-center text-sm text-slate-400">
              No results found for &quot;{query}&quot;
            </div>
          ) : (
            <div className="py-1.5">
              {/* Contacts */}
              {results.contacts.length > 0 && (
                <div>
                  <div className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                    Contacts ({results.contacts.length})
                  </div>
                  {results.contacts.map((c) => (
                    <button
                      key={c.id}
                      type="button"
                      onClick={() => navigateTo(`/contacts`)}
                      className="w-full flex items-center gap-2.5 px-3 py-2 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors text-left"
                    >
                      <UserCircle className="w-4 h-4 text-blue-500 shrink-0" />
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-700 dark:text-slate-200 truncate">{c.name || c.email}</p>
                        {c.email && c.name && <p className="text-xs text-slate-400 truncate">{c.email}</p>}
                      </div>
                    </button>
                  ))}
                </div>
              )}

              {/* Companies */}
              {results.companies.length > 0 && (
                <div>
                  <div className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                    Companies ({results.companies.length})
                  </div>
                  {results.companies.map((c) => (
                    <button
                      key={c.id}
                      type="button"
                      onClick={() => navigateTo(`/companies/${c.id}`)}
                      className="w-full flex items-center gap-2.5 px-3 py-2 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors text-left"
                    >
                      <Building2 className="w-4 h-4 text-orange-500 shrink-0" />
                      <p className="text-sm font-medium text-slate-700 dark:text-slate-200 truncate">{c.name}</p>
                    </button>
                  ))}
                </div>
              )}

              {/* Deals */}
              {results.deals.length > 0 && (
                <div>
                  <div className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                    Deals ({results.deals.length})
                  </div>
                  {results.deals.map((d) => (
                    <button
                      key={d.id}
                      type="button"
                      onClick={() => navigateTo(`/deals/${d.id}`)}
                      className="w-full flex items-center gap-2.5 px-3 py-2 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors text-left"
                    >
                      <Target className="w-4 h-4 text-emerald-500 shrink-0" />
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-slate-700 dark:text-slate-200 truncate">{d.name}</p>
                      </div>
                      {d.value && <span className="text-xs font-semibold text-emerald-600 shrink-0">${d.value}</span>}
                    </button>
                  ))}
                </div>
              )}

              {/* Leads */}
              {results.leads.length > 0 && (
                <div>
                  <div className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                    Leads ({results.leads.length})
                  </div>
                  {results.leads.map((l) => (
                    <button
                      key={l.id}
                      type="button"
                      onClick={() => navigateTo(`/leads`)}
                      className="w-full flex items-center gap-2.5 px-3 py-2 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors text-left"
                    >
                      <Users className="w-4 h-4 text-violet-500 shrink-0" />
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-700 dark:text-slate-200 truncate">{l.name}</p>
                        {l.email && <p className="text-xs text-slate-400 truncate">{l.email}</p>}
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default function AppHeader() {
  const navigate = useNavigate();
  const user = getCurrentUser();
  const org = useOrgOptional();

  const handleLogout = () => {
    clearSession();
    navigate('/login', { replace: true });
  };

  const initials = user?.name
    ? user.name
        .split(' ')
        .map((n) => n[0])
        .join('')
        .toUpperCase()
        .slice(0, 2)
    : '?';

  return (
    <>
    <header
      className="sticky top-0 z-50 border-b border-slate-200/60 dark:border-slate-700/60 bg-slate-50/95 dark:bg-slate-900/95 backdrop-blur-md supports-[backdrop-filter]:bg-slate-50/80 dark:supports-[backdrop-filter]:bg-slate-900/80 overflow-hidden"
      role="banner"
    >
      <div className="w-full px-[var(--page-padding)] overflow-hidden">
        <div className="flex h-16 items-center justify-between gap-2">
          <div className="flex shrink-0 items-center gap-2">
            <AppSideNav />
            <Link
              to="/dashboard"
              className="group flex items-center gap-2.5 shrink-0 rounded-xl transition-all duration-200 hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500/60 focus-visible:ring-offset-2 focus-visible:ring-offset-slate-50"
              aria-label="Go to dashboard"
            >
              <div
                className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-amber-500 via-orange-500 to-amber-600 shadow-lg shadow-amber-500/25 ring-1 ring-white/20 transition-transform duration-200 group-hover:scale-105"
                aria-hidden
              >
                <Sparkles className="h-5 w-5 text-white drop-shadow-sm" />
              </div>
              <span className="hidden text-lg font-semibold tracking-tight text-slate-800 dark:text-white sm:inline">
                Cadence
              </span>
            </Link>
          </div>

          <div className="min-w-0 flex-1" />

          {/* HP-9: Global search bar */}
          {isUsingRealApi() && <GlobalSearchBar />}

          {/* Right: notifications, org, account */}
          <div className="flex items-center gap-2">
            <NotificationBell />

            {/* Organization Selector */}
            {org?.currentOrg && (
              <Link
                to="/organizations"
                className="hidden sm:flex items-center gap-2 px-3 py-1.5 rounded-full text-sm font-medium text-slate-600 dark:text-slate-300 bg-white/60 dark:bg-slate-800/60 hover:bg-white dark:hover:bg-slate-700 hover:text-slate-900 dark:hover:text-white ring-1 ring-slate-200/60 dark:ring-slate-700/60 hover:ring-slate-300/80 dark:hover:ring-slate-600/80 transition-all duration-200 max-w-[160px]"
              >
                <Building2 className="w-4 h-4 shrink-0" style={{ color: 'var(--brand-primary)' }} />
                <span className="truncate">{org.currentOrg.name}</span>
              </Link>
            )}

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  className="h-auto gap-2 rounded-full px-2.5 py-1.5 text-slate-600 dark:text-slate-300 shadow-sm ring-1 ring-slate-200/60 dark:ring-slate-700/60 transition-all duration-200 hover:bg-white/80 dark:hover:bg-slate-700/80 hover:text-slate-900 dark:hover:text-white hover:ring-slate-300/80 dark:hover:ring-slate-600/80 focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]/60 focus-visible:ring-offset-2"
                >
                  <Avatar className="h-8 w-8 rounded-full border-2 shadow-inner" style={{ borderColor: 'var(--brand-primary)', opacity: 0.6 }}>
                    <AvatarFallback className="text-sm font-semibold" style={{ background: 'linear-gradient(135deg, var(--brand-primary-light), var(--brand-primary))', color: 'white' }}>
                      {initials}
                    </AvatarFallback>
                  </Avatar>
                  <span className="hidden max-w-[120px] truncate text-sm font-medium sm:inline">
                    {user?.name ?? 'Account'}
                  </span>
                  <ChevronDown className="h-4 w-4 shrink-0 text-slate-400 dark:text-slate-500 transition-transform [[data-state=open]_&]:rotate-180" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                align="end"
                className="w-60 rounded-xl border-slate-200/80 dark:border-slate-700/80 bg-white/95 dark:bg-slate-800/95 p-1.5 shadow-xl shadow-slate-200/50 dark:shadow-slate-900/50 backdrop-blur-sm"
              >
                <div className="px-3 py-2.5">
                  <p className="truncate text-sm font-semibold text-slate-900 dark:text-white">
                    {user?.name ?? 'Account'}
                  </p>
                  <p className="truncate text-xs text-slate-500 dark:text-slate-400">{user?.email ?? ''}</p>
                </div>
                <DropdownMenuSeparator className="bg-slate-100 dark:bg-slate-700" />
                {isUsingRealApi() && org?.currentOrg && (
                  <DropdownMenuItem asChild>
                    <Link to="/organizations" className="flex cursor-pointer items-center gap-2 rounded-lg dark:text-slate-200 dark:hover:bg-slate-700">
                      <Building2 className="h-4 w-4" />
                      {org.currentOrg.name} — Switch org
                    </Link>
                  </DropdownMenuItem>
                )}
                <DropdownMenuItem asChild>
                  <Link to="/settings" className="flex cursor-pointer items-center gap-2 rounded-lg dark:text-slate-200 dark:hover:bg-slate-700">
                    <User className="h-4 w-4" />
                    Settings
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator className="bg-slate-100 dark:bg-slate-700" />
                <DropdownMenuItem
                  onClick={handleLogout}
                  className="cursor-pointer rounded-lg text-red-600 dark:text-red-400 focus:text-red-600 dark:focus:text-red-400"
                >
                  <LogOut className="h-4 w-4" />
                  Sign out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </div>
    </header>
    <DemoBanner />
    <ReadOnlyBanner />
    <LocalOnlyNotice />
    {/* Renders its own spacing, and nothing at all where sales work has no place. */}
    <NextActions />
    </>
  );
}
