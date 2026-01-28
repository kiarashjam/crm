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
} from 'lucide-react';
import { getDemoUser, clearDemoUser } from '@/app/lib/auth';
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
import { useState } from 'react';
import { cn } from './ui/utils';

const navItems = [
  { path: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
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
              'flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-colors',
              isActive
                ? 'bg-orange-100 text-orange-700'
                : 'text-gray-600 hover:text-orange-600 hover:bg-orange-50'
            )}
          >
            <Icon className="w-5 h-5 shrink-0" />
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
  const user = getDemoUser();
  const [sheetOpen, setSheetOpen] = useState(false);

  const handleLogout = () => {
    clearDemoUser();
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
    <header className="bg-white border-b border-gray-200 sticky top-0 z-50 shadow-sm">
      <div className="w-full px-[var(--page-padding)]">
        <div className="flex h-14 items-center justify-between gap-4">
          {/* Logo */}
          <Link
            to="/dashboard"
            className="flex items-center gap-2 shrink-0 rounded-lg transition-opacity hover:opacity-90"
          >
            <div className="w-9 h-9 bg-gradient-to-br from-orange-600 to-orange-500 rounded-lg flex items-center justify-center shadow-md">
              <Sparkles className="w-5 h-5 text-white" />
            </div>
            <span className="text-lg font-bold text-gray-900 hidden sm:inline">
              HubSpot AI Writer
            </span>
          </Link>

          {/* Desktop nav */}
          <nav className="hidden md:flex items-center gap-1">
            <NavLinks />
          </nav>

          {/* Right: user menu + mobile trigger */}
          <div className="flex items-center gap-2">
            {/* User dropdown */}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  className="gap-2 px-2 py-1.5 h-auto rounded-lg text-gray-600 hover:text-gray-900 hover:bg-gray-100"
                >
                  <Avatar className="h-8 w-8 rounded-full border-2 border-orange-200">
                    <AvatarFallback className="bg-orange-100 text-orange-700 text-xs font-semibold">
                      {initials}
                    </AvatarFallback>
                  </Avatar>
                  <span className="hidden sm:inline text-sm font-medium max-w-[120px] truncate">
                    {user?.name ?? 'Account'}
                  </span>
                  <ChevronDown className="w-4 h-4 shrink-0 text-gray-400" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-56">
                <div className="px-2 py-1.5">
                  <p className="text-sm font-medium text-gray-900 truncate">
                    {user?.name ?? 'Account'}
                  </p>
                  <p className="text-xs text-gray-500 truncate">{user?.email ?? ''}</p>
                </div>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link to="/settings" className="flex items-center gap-2 cursor-pointer">
                    <User className="w-4 h-4" />
                    Settings
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onClick={handleLogout}
                  className="text-red-600 focus:text-red-600 cursor-pointer"
                >
                  <LogOut className="w-4 h-4" />
                  Sign out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            {/* Mobile menu */}
            <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
              <SheetTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="md:hidden rounded-lg text-gray-600 hover:bg-gray-100"
                  aria-label="Open menu"
                >
                  <Menu className="w-5 h-5" />
                </Button>
              </SheetTrigger>
              <SheetContent side="right" className="w-72 pt-8">
                <nav className="flex flex-col gap-1">
                  <NavLinks onNavigate={() => setSheetOpen(false)} />
                </nav>
                <div className="mt-6 pt-6 border-t border-gray-200">
                  <Link
                    to="/settings"
                    onClick={() => setSheetOpen(false)}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg text-gray-600 hover:bg-orange-50 hover:text-orange-600"
                  >
                    <Settings className="w-5 h-5" />
                    Settings
                  </Link>
                  <button
                    type="button"
                    onClick={() => {
                      setSheetOpen(false);
                      handleLogout();
                    }}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg text-red-600 hover:bg-red-50 w-full text-left"
                  >
                    <LogOut className="w-5 h-5" />
                    Sign out
                  </button>
                </div>
              </SheetContent>
            </Sheet>
          </div>
        </div>
      </div>
    </header>
  );
}
