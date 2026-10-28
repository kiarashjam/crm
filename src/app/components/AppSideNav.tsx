import { useEffect, useId, useMemo, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import {
  Activity,
  ArrowRightLeft,
  BarChart3,
  Bot,
  Building2,
  Calculator,
  CalendarClock,
  CalendarDays,
  CalendarHeart,
  CheckSquare,
  ChevronDown,
  ClipboardList,
  Copy,
  Crown,
  FileText,
  FileUp,
  Gift,
  HeartHandshake,
  HelpCircle,
  History,
  Home,
  Kanban,
  LayoutDashboard,
  LayoutGrid,
  LayoutTemplate,
  Megaphone,
  Menu,
  MessageSquareText,
  Paperclip,
  Receipt,
  Repeat2,
  ScanLine,
  ScrollText,
  Search,
  Settings,
  SlidersHorizontal,
  UserCircle,
  Users,
  UsersRound,
  Webhook,
  Wine,
  Workflow,
  Zap,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from '@/app/components/ui/sheet';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { cn } from './ui/utils';

type NavLinkItem = {
  path: string;
  label: string;
  icon: LucideIcon;
  children?: { path: string; label: string; icon: LucideIcon }[];
};

type NavGroup = {
  id: string;
  label: string;
  bar: string;
  chip: string;
  items: NavLinkItem[];
};

const GROUPS: NavGroup[] = [
  {
    id: 'today',
    label: 'Today',
    bar: 'bg-orange-500',
    chip: 'bg-orange-100 text-orange-700',
    items: [
      { path: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
    ],
  },
  {
    id: 'pipeline',
    label: 'Pipeline',
    bar: 'bg-blue-500',
    chip: 'bg-blue-100 text-blue-700',
    items: [
      { path: '/leads', label: 'Leads', icon: Users, children: [
        { path: '/leads/import', label: 'Import leads', icon: FileUp },
        { path: '/leads/webhook', label: 'Lead webhook', icon: Webhook },
      ] },
      { path: '/deals', label: 'Deals', icon: Kanban },
      { path: '/tasks', label: 'Tasks', icon: CheckSquare },
      { path: '/activities', label: 'Activities', icon: Activity },
    ],
  },
  {
    id: 'people',
    label: 'People',
    bar: 'bg-emerald-500',
    chip: 'bg-emerald-100 text-emerald-700',
    items: [
      { path: '/contacts', label: 'Contacts', icon: UserCircle },
      { path: '/companies', label: 'Companies', icon: Building2 },
      { path: '/team', label: 'Team', icon: UsersRound },
    ],
  },
  {
    id: 'outreach',
    label: 'Outreach',
    bar: 'bg-violet-500',
    chip: 'bg-violet-100 text-violet-700',
    items: [
      { path: '/send', label: 'Attach copy', icon: Paperclip },
      { path: '/sequences', label: 'Sequences', icon: Workflow },
      { path: '/templates', label: 'Templates', icon: LayoutTemplate },
      { path: '/history', label: 'History', icon: History },
      { path: '/automations', label: 'Automations', icon: Zap },
    ],
  },
  {
    id: 'insight',
    label: 'Insight',
    bar: 'bg-rose-500',
    chip: 'bg-rose-100 text-rose-700',
    items: [
      { path: '/reports', label: 'Reports', icon: BarChart3 },
      { path: '/duplicates', label: 'Duplicates', icon: Copy },
      { path: '/audit', label: 'Audit', icon: ScrollText },
    ],
  },
  {
    id: 'club',
    label: 'Club',
    bar: 'bg-amber-500',
    chip: 'bg-amber-100 text-amber-800',
    items: [
      { path: '/hospitality', label: 'Club today', icon: LayoutDashboard },
      { path: '/pos', label: 'POS', icon: Calculator },
      { path: '/members', label: 'Members', icon: Crown, children: [
        { path: '/members/import', label: 'Import members', icon: FileUp },
      ] },
      { path: '/households', label: 'Households', icon: Home },
      { path: '/applications', label: 'Applications', icon: ClipboardList },
      { path: '/calendar', label: 'Calendar', icon: CalendarDays },
      { path: '/events', label: 'Events', icon: CalendarHeart },
      { path: '/reservations', label: 'Reservations', icon: CalendarClock },
      { path: '/visits', label: 'Visits', icon: ScanLine },
      { path: '/tabs', label: 'Tabs', icon: Wine },
      { path: '/charges', label: 'House accounts', icon: Receipt },
      { path: '/subscriptions', label: 'Subscriptions', icon: Repeat2 },
      { path: '/statements', label: 'Statements', icon: FileText },
      { path: '/loyalty', label: 'Loyalty', icon: Gift },
      { path: '/gift-cards', label: 'Gift cards', icon: Gift },
      { path: '/donations', label: 'Donations', icon: HeartHandshake },
      { path: '/campaigns', label: 'Campaigns', icon: Megaphone },
      { path: '/communications', label: 'Communications', icon: MessageSquareText },
      { path: '/club-reports', label: 'Club reports', icon: BarChart3 },
      { path: '/portal', label: 'Member portal', icon: LayoutGrid },
      { path: '/hospitality-settings', label: 'Club settings', icon: SlidersHorizontal },
    ],
  },
  {
    id: 'workspace',
    label: 'Workspace',
    bar: 'bg-cyan-500',
    chip: 'bg-cyan-100 text-cyan-800',
    items: [
      { path: '/organizations', label: 'Workspaces', icon: ArrowRightLeft },
      { path: '/settings', label: 'Settings', icon: Settings },
      { path: '/settings?tab=claude', label: 'Claude', icon: Bot },
      { path: '/help', label: 'Help', icon: HelpCircle },
    ],
  },
];

function pathOnly(path: string): string {
  return path.split('?')[0] ?? path;
}

function matchesPath(pathname: string, search: string, path: string): boolean {
  const target = pathOnly(path);
  const tab = path.includes('?') ? new URLSearchParams(path.split('?')[1]).get('tab') : null;
  const currentTab = new URLSearchParams(search).get('tab');

  if (tab) return pathname === target && currentTab === tab;
  if (pathname === target) return !(target === '/settings' && currentTab === 'claude');
  if (pathname === '/leads/import' || pathname === '/leads/webhook' || pathname === '/members/import') return false;
  return pathname.startsWith(`${target}/`);
}

function groupForPath(pathname: string, search: string): string {
  return GROUPS.find((group) => group.items.some((item) => itemMatches(pathname, search, item)))?.id ?? 'today';
}

function itemMatches(pathname: string, search: string, item: NavLinkItem): boolean {
  return matchesPath(pathname, search, item.path) || (item.children?.some((child) => matchesPath(pathname, search, child.path)) ?? false);
}

function currentItem(pathname: string, search: string): NavLinkItem | undefined {
  for (const group of GROUPS) {
    for (const item of group.items) {
      const child = item.children?.find((entry) => matchesPath(pathname, search, entry.path));
      if (child) return child;
      if (matchesPath(pathname, search, item.path)) return item;
    }
  }
  return undefined;
}

export function AppSideNav() {
  const [open, setOpen] = useState(false);

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          className="h-11 w-11 gap-0 rounded-xl px-0 text-slate-700 hover:bg-white hover:text-slate-900 focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] sm:w-auto sm:gap-2 sm:px-3 dark:text-slate-200 dark:hover:bg-slate-800"
          aria-label="Open menu"
        >
          <Menu className="h-5 w-5" aria-hidden="true" />
          <span className="hidden text-sm font-semibold sm:inline">Menu</span>
        </Button>
      </SheetTrigger>
      <SheetContent
        side="left"
        className="flex w-[min(100%,22rem)] max-w-[22rem] flex-col gap-0 border-slate-200 bg-white p-0 shadow-2xl data-[state=open]:duration-300 dark:border-slate-800 dark:bg-slate-950 [&>button]:top-3.5 [&>button]:right-3 [&>button]:flex [&>button]:h-11 [&>button]:w-11 [&>button]:items-center [&>button]:justify-center [&>button]:rounded-full [&>button]:opacity-100 [&>button]:ring-1 [&>button]:ring-slate-200 [&>button]:hover:bg-slate-50 dark:[&>button]:ring-slate-700 dark:[&>button]:hover:bg-slate-900"
      >
        <SideNavBody onNavigate={() => setOpen(false)} />
      </SheetContent>
    </Sheet>
  );
}

function SideNavBody({ onNavigate }: { onNavigate: () => void }) {
  const location = useLocation();
  const org = useOrgOptional();
  const filterId = useId();
  const activeGroup = groupForPath(location.pathname, location.search);
  const here = currentItem(location.pathname, location.search);
  const [query, setQuery] = useState('');
  const [expanded, setExpanded] = useState<Record<string, boolean>>(() => ({ [activeGroup]: true }));

  useEffect(() => {
    setExpanded((current) => ({ ...current, [activeGroup]: true }));
  }, [activeGroup]);

  const needle = query.trim().toLowerCase();
  const visibleGroups = useMemo(() => {
    if (!needle) return GROUPS;
    return GROUPS.map((group) => ({
      ...group,
      items: group.items.flatMap((item) => {
        const parentHit = item.label.toLowerCase().includes(needle);
        const children = (item.children ?? []).filter((child) =>
          child.label.toLowerCase().includes(needle),
        );
        if (parentHit) return [item];
        if (children.length > 0) return [{ ...item, children }];
        return [];
      }),
    })).filter((group) => group.items.length > 0);
  }, [needle]);

  return (
    <>
      <SheetHeader className="gap-3 border-b border-slate-200 px-5 pb-4 pt-5 pr-16 dark:border-slate-800">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-[var(--brand-primary)]">Cadence</p>
          <SheetTitle className="mt-1 text-left text-lg text-slate-950 dark:text-white">
            {org?.currentOrg?.name ?? 'Menu'}
          </SheetTitle>
          <SheetDescription className="sr-only">
            {here ? `You are in ${here.label}` : 'Choose where to go'}
          </SheetDescription>
        </div>
        <div>
          <label htmlFor={filterId} className="sr-only">Find a page</label>
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-slate-500" aria-hidden="true" />
            <input
              id={filterId}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Find a page"
              className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50 pr-3 pl-9 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus-visible:border-[var(--brand-primary)] focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]/30 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
            />
          </div>
        </div>
      </SheetHeader>

      <nav className="flex-1 overflow-y-auto px-3 py-3" aria-label="Main navigation">
        {visibleGroups.length === 0 ? (
          <p className="px-2 py-8 text-sm text-slate-500">No pages match “{query.trim()}”.</p>
        ) : (
          <ul>
            {visibleGroups.map((group) => (
              <li key={group.id}>
                <NavCategory
                  group={group}
                  expanded={needle.length > 0 || expanded[group.id] === true}
                  pathname={location.pathname}
                search={location.search}
                  onToggle={() => {
                    if (needle) return;
                    setExpanded((current) => ({ ...current, [group.id]: !current[group.id] }));
                  }}
                  onNavigate={onNavigate}
                  filtering={needle.length > 0}
                />
              </li>
            ))}
          </ul>
        )}
      </nav>
    </>
  );
}

function NavCategory({
  group,
  expanded,
  pathname,
  search,
  onToggle,
  onNavigate,
  filtering,
}: {
  group: NavGroup;
  expanded: boolean;
  pathname: string;
  search: string;
  onToggle: () => void;
  onNavigate: () => void;
  filtering: boolean;
}) {
  const panelId = `nav-group-${group.id}`;

  return (
    <section className="mb-3 border-b-2 border-slate-200 pb-3 last:mb-0 last:border-b-0 last:pb-0 dark:border-slate-700">
      <button
        type="button"
        className="flex min-h-11 w-full items-center gap-3 px-2 py-2 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--brand-primary)]"
        aria-expanded={expanded}
        aria-controls={panelId}
        onClick={onToggle}
        disabled={filtering}
      >
        <span className={cn('h-6 w-1.5 shrink-0 rounded-full', group.bar)} aria-hidden="true" />
        <span className="min-w-0 flex-1 text-sm font-semibold text-slate-900 dark:text-white">{group.label}</span>
        {!filtering && (
          <ChevronDown className={cn('h-4 w-4 shrink-0 text-slate-500 transition-transform duration-200 motion-reduce:transition-none', expanded && 'rotate-180')} aria-hidden="true" />
        )}
      </button>
      <div
        id={panelId}
        className={cn('grid transition-[grid-template-rows] duration-200 ease-out motion-reduce:transition-none', expanded ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]')}
      >
        <div
          className="overflow-hidden"
          aria-hidden={!expanded}
          ref={(node) => {
            if (node) node.inert = !expanded;
          }}
        >
          <ul className="pb-2">
            {group.items.map((item) => (
              <NavEntry key={item.path} item={item} chip={group.chip} pathname={pathname} search={search} onNavigate={onNavigate} />
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function NavEntry({
  item,
  chip,
  pathname,
  search,
  onNavigate,
  nested = false,
}: {
  item: NavLinkItem;
  chip: string;
  pathname: string;
  search: string;
  onNavigate: () => void;
  nested?: boolean;
}) {
  const active = matchesPath(pathname, search, item.path);
  const Icon = item.icon;
  return (
    <li>
      <Link
        to={item.path}
        onClick={onNavigate}
        aria-current={active ? 'page' : undefined}
        className={cn(
          'flex min-h-11 items-center gap-3 rounded-lg py-1.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]',
          nested ? 'pr-2 pl-11' : 'px-2',
          active
            ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-950'
            : 'text-slate-800 hover:bg-slate-50 dark:text-slate-100 dark:hover:bg-slate-900',
        )}
      >
        <span className={cn('flex h-8 w-8 shrink-0 items-center justify-center rounded-lg', active ? 'bg-white/15 text-white dark:bg-slate-900 dark:text-white' : chip)} aria-hidden="true">
          <Icon className="h-4 w-4" />
        </span>
        <span className="truncate text-sm font-medium">{item.label}</span>
      </Link>
      {item.children && item.children.length > 0 && (
        <ul className="mt-0.5 ml-6 border-l-2 border-slate-200 dark:border-slate-700">
          {item.children.map((child) => (
            <NavEntry key={child.path} item={child} chip={chip} pathname={pathname} search={search} onNavigate={onNavigate} nested />
          ))}
        </ul>
      )}
    </li>
  );
}
