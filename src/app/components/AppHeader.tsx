import { useState, useEffect } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import {
  Sparkles,
  LayoutDashboard,
  LayoutTemplate,
  History,
  HelpCircle,
  User,
  LogOut,
  Settings,
  Menu,
  ChevronDown,
  Users,
  UserCircle,
  Kanban,
  CheckSquare,
  Activity,
  Building2,
  Link2,
} from 'lucide-react';
import { getCurrentUser, clearSession } from '@/app/lib/auth';
import { getConnectionStatus } from '@/app/api';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/app/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/app/components/ui/avatar';
import { Button } from '@/app/components/ui/button';
import { Sheet, SheetContent, SheetTrigger } from '@/app/components/ui/sheet';
import { cn } from './ui/utils';
import DemoBanner from './DemoBanner';

const navItems = [
  { path: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { path: '/leads', label: 'Leads', icon: Users },
  { path: '/deals', label: 'Deals', icon: Kanban },
  { path: '/tasks', label: 'Tasks', icon: CheckSquare },
  { path: '/activities', label: 'Activities', icon: Activity },
  { path: '/contacts', label: 'Contacts', icon: UserCircle },
  { path: '/companies', label: 'Companies', icon: Building2 },
  { path: '/templates', label: 'Templates', icon: LayoutTemplate },
  { path: '/history', label: 'History', icon: History },
  { path: '/help', label: 'Help', icon: HelpCircle },
] as const;

function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const location = useLocation();

  return (
    <>
      {navItems.map(({ path, label, icon: Icon }) => {
        const isActive = location.pathname === path;
        const link = (
          <Link
            to={path}
            onClick={onNavigate}
            className={cn(
              'flex items-center gap-2 px-3.5 py-2 rounded-full text-sm font-medium transition-all duration-200',
              isActive
                ? 'bg-white/90 text-slate-900 shadow-sm ring-1 ring-slate-200/80'
                : 'text-slate-500 hover:text-slate-800 hover:bg-white/50'
            )}
            aria-current={isActive ? 'page' : undefined}
          >
            <Icon className={cn('w-4 h-4 shrink-0', isActive ? 'text-amber-500' : 'opacity-80')} />
            {label}
          </Link>
        );
        return <span key={path}>{link}</span>;
      })}
    </>
  );
}

export default function AppHeader() {
  const navigate = useNavigate();
  const user = getCurrentUser();
  const [sheetOpen, setSheetOpen] = useState(false);
  const [crmConnected, setCrmConnected] = useState<boolean | null>(null);

  useEffect(() => {
    getConnectionStatus()
      .then((s) => setCrmConnected(s.connected))
      .catch(() => setCrmConnected(false));
  }, []);

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
      className="sticky top-0 z-50 border-b border-slate-200/60 bg-slate-50/95 backdrop-blur-md supports-[backdrop-filter]:bg-slate-50/80"
      role="banner"
    >
      <div className="w-full px-[var(--page-padding)]">
        <div className="flex h-16 items-center justify-between gap-4">
          {/* Logo */}
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
            <span className="hidden text-lg font-semibold tracking-tight text-slate-800 sm:inline">
              Cadence
            </span>
          </Link>

          <nav
            className="hidden items-center gap-1 rounded-full bg-slate-200/50 px-1.5 py-1 md:flex"
            aria-label="Main navigation"
          >
            <NavLinks />
          </nav>

          {/* Right: user menu + mobile trigger */}
          <div className="flex items-center gap-2">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  className="h-auto gap-2 rounded-full px-2.5 py-1.5 text-slate-600 shadow-sm ring-1 ring-slate-200/60 transition-all duration-200 hover:bg-white/80 hover:text-slate-900 hover:ring-slate-300/80 focus-visible:ring-2 focus-visible:ring-amber-500/60 focus-visible:ring-offset-2"
                >
                  <Avatar className="h-8 w-8 rounded-full border-2 border-amber-200/80 shadow-inner">
                    <AvatarFallback className="bg-gradient-to-br from-amber-100 to-orange-100 text-sm font-semibold text-amber-800">
                      {initials}
                    </AvatarFallback>
                  </Avatar>
                  <span className="hidden max-w-[120px] truncate text-sm font-medium sm:inline">
                    {user?.name ?? 'Account'}
                  </span>
                  <ChevronDown className="h-4 w-4 shrink-0 text-slate-400 transition-transform [[data-state=open]_&]:rotate-180" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                align="end"
                className="w-60 rounded-xl border-slate-200/80 bg-white/95 p-1.5 shadow-xl shadow-slate-200/50 backdrop-blur-sm"
              >
                <div className="px-3 py-2.5">
                  <p className="truncate text-sm font-semibold text-slate-900">
                    {user?.name ?? 'Account'}
                  </p>
                  <p className="truncate text-xs text-slate-500">{user?.email ?? ''}</p>
                </div>
                <DropdownMenuSeparator className="bg-slate-100" />
                <DropdownMenuItem asChild>
                  <Link to="/connect" className="flex cursor-pointer items-center gap-2 rounded-lg">
                    <Link2 className="h-4 w-4" />
                    {crmConnected === true
                      ? 'CRM: Connected'
                      : crmConnected === false
                        ? 'CRM: Not connected'
                        : 'Connection'}
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <Link to="/settings" className="flex cursor-pointer items-center gap-2 rounded-lg">
                    <User className="h-4 w-4" />
                    Settings
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator className="bg-slate-100" />
                <DropdownMenuItem
                  onClick={handleLogout}
                  className="cursor-pointer rounded-lg text-red-600 focus:text-red-600"
                >
                  <LogOut className="h-4 w-4" />
                  Sign out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
              <SheetTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="rounded-full text-slate-600 ring-1 ring-slate-200/60 hover:bg-white/80 md:hidden"
                  aria-label="Open menu"
                >
                  <Menu className="h-5 w-5" />
                </Button>
              </SheetTrigger>
              <SheetContent
                side="right"
                className="w-72 border-slate-200/80 bg-slate-50/95 pt-8 backdrop-blur-md"
              >
                <nav className="flex flex-col gap-1 rounded-xl bg-slate-200/40 p-1.5" aria-label="Main navigation">
                  <NavLinks onNavigate={() => setSheetOpen(false)} />
                </nav>
                <div className="mt-6 border-t border-slate-200/80 pt-6">
                  <Link
                    to="/settings"
                    onClick={() => setSheetOpen(false)}
                    className="flex items-center gap-2 rounded-lg px-3 py-2.5 text-slate-600 transition-colors hover:bg-white/80 hover:text-slate-900"
                  >
                    <Settings className="h-5 w-5" />
                    Settings
                  </Link>
                  <button
                    type="button"
                    onClick={() => {
                      setSheetOpen(false);
                      handleLogout();
                    }}
                    className="flex w-full items-center gap-2 rounded-lg px-3 py-2.5 text-left text-red-600 transition-colors hover:bg-red-50"
                  >
                    <LogOut className="h-5 w-5" />
                    Sign out
                  </button>
                </div>
              </SheetContent>
            </Sheet>
          </div>
        </div>
      </div>
    </header>
    <DemoBanner />
    </>
  );
}
