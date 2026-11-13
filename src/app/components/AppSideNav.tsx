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
  Landmark,
  LayoutDashboard,
  LayoutGrid,
  LayoutTemplate,
  Megaphone,
  Menu,
  MessageSquareText,
  Paperclip,
  PlugZap,
  Receipt,
  RefreshCw,
  PartyPopper,
  Repeat2,
  ScanLine,
  ScrollText,
  Search,
  Settings,
  ShieldCheck,
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
import { type Permission, type WorkspaceModule, PERMISSION } from '@/app/api/organizations';
import { mayIn } from '@/app/hooks/useMay';
import { useModules, type ModulesOn } from '@/app/hooks/useModules';
import { cn } from './ui/utils';
import { useT, type MessageKey } from '@/app/i18n';

type NavLinkItem = {
  path: string;
  label: string;
  icon: LucideIcon;
  /** The module the page belongs to: it leaves the menu while the workspace has that module off. */
  module?: WorkspaceModule;
  /** Shown only while this module is off: a shared page whose usual place is in that module's group. */
  onlyWhenOff?: WorkspaceModule;
  /** Shown only in a workspace, to those who hold this permission: the page has nothing for anyone else. */
  permission?: Permission;
  children?: { path: string; label: string; icon: LucideIcon }[];
};

type NavGroup = {
  id: string;
  label: string;
  /** What the group is called while Sales is off and only its shared pages are left in it. */
  labelWithoutSales?: string;
  bar: string;
  chip: string;
  items: NavLinkItem[];
};

/** A menu entry as written below: its words are catalogue keys, read in the person's language when the menu is built. */
type NavItemDef = Omit<NavLinkItem, 'label' | 'children'> & {
  label: MessageKey;
  children?: { path: string; label: MessageKey; icon: LucideIcon }[];
};

type NavGroupDef = Omit<NavGroup, 'label' | 'labelWithoutSales' | 'items'> & {
  label: MessageKey;
  labelWithoutSales?: MessageKey;
  items: NavItemDef[];
};

const GROUPS: NavGroupDef[] = [
  {
    id: 'today',
    label: 'nav.group.today',
    bar: 'bg-orange-500',
    chip: 'bg-orange-100 text-orange-700',
    items: [
      { path: '/dashboard', label: 'nav.dashboard', icon: LayoutDashboard, module: 'sales' },
    ],
  },
  {
    id: 'pipeline',
    label: 'nav.group.pipeline',
    labelWithoutSales: 'nav.group.work',
    bar: 'bg-blue-500',
    chip: 'bg-blue-100 text-blue-700',
    items: [
      { path: '/leads', label: 'nav.leads', icon: Users, module: 'sales', children: [
        { path: '/leads/import', label: 'nav.importLeads', icon: FileUp },
        { path: '/leads/webhook', label: 'nav.leadWebhook', icon: Webhook },
      ] },
      { path: '/deals', label: 'nav.deals', icon: Kanban, module: 'sales' },
      { path: '/tasks', label: 'nav.tasks', icon: CheckSquare },
      // The calendar lives with the club's pages; without the club, it is here.
      { path: '/calendar', label: 'nav.calendar', icon: CalendarDays, onlyWhenOff: 'club' },
      { path: '/activities', label: 'nav.activities', icon: Activity },
    ],
  },
  {
    id: 'people',
    label: 'nav.group.people',
    bar: 'bg-emerald-500',
    chip: 'bg-emerald-100 text-emerald-700',
    items: [
      { path: '/contacts', label: 'nav.contacts', icon: UserCircle, children: [
        { path: '/contacts/import', label: 'nav.importContacts', icon: FileUp },
      ] },
      { path: '/companies', label: 'nav.companies', icon: Building2, children: [
        { path: '/companies/import', label: 'nav.importCompanies', icon: FileUp },
      ] },
      { path: '/team', label: 'nav.team', icon: UsersRound },
    ],
  },
  {
    id: 'outreach',
    label: 'nav.group.outreach',
    bar: 'bg-violet-500',
    chip: 'bg-violet-100 text-violet-700',
    items: [
      { path: '/send', label: 'nav.attachCopy', icon: Paperclip, module: 'sales' },
      { path: '/sequences', label: 'nav.sequences', icon: Workflow, module: 'sales' },
      { path: '/templates', label: 'nav.templates', icon: LayoutTemplate },
      { path: '/history', label: 'nav.history', icon: History, module: 'sales' },
      { path: '/automations', label: 'nav.automations', icon: Zap },
    ],
  },
  {
    id: 'insight',
    label: 'nav.group.insight',
    bar: 'bg-rose-500',
    chip: 'bg-rose-100 text-rose-700',
    items: [
      { path: '/reports', label: 'nav.reports', icon: BarChart3, module: 'sales' },
      { path: '/duplicates', label: 'nav.duplicates', icon: Copy },
      { path: '/audit', label: 'nav.audit', icon: ScrollText },
      { path: '/privacy-requests', label: 'nav.privacyRequests', icon: ShieldCheck, permission: PERMISSION.ManagePrivacy },
    ],
  },
  {
    id: 'club',
    label: 'nav.group.club',
    bar: 'bg-amber-500',
    chip: 'bg-amber-100 text-amber-800',
    items: [
      { path: '/hospitality', label: 'nav.clubToday', icon: LayoutDashboard, module: 'club' },
      { path: '/pos', label: 'nav.houseCharge', icon: Calculator, module: 'club' },
      { path: '/pos-tickets', label: 'nav.posTickets', icon: PlugZap, module: 'club' },
      { path: '/members', label: 'nav.members', icon: Crown, module: 'club', children: [
        { path: '/members/import', label: 'nav.importMembers', icon: FileUp },
      ] },
      { path: '/households', label: 'nav.households', icon: Home, module: 'club' },
      { path: '/applications', label: 'nav.applications', icon: ClipboardList, module: 'club' },
      { path: '/renewals', label: 'nav.renewals', icon: RefreshCw, module: 'club' },
      { path: '/corporate-memberships', label: 'nav.corporateMemberships', icon: Building2, module: 'club' },
      { path: '/private-events', label: 'nav.privateEvents', icon: PartyPopper, module: 'club' },
      { path: '/calendar', label: 'nav.calendar', icon: CalendarDays, module: 'club' },
      { path: '/events', label: 'nav.events', icon: CalendarHeart, module: 'club' },
      { path: '/reservations', label: 'nav.reservations', icon: CalendarClock, module: 'club' },
      { path: '/visits', label: 'nav.visits', icon: ScanLine, module: 'club' },
      { path: '/tabs', label: 'nav.tabs', icon: Wine, module: 'club' },
      { path: '/charges', label: 'nav.houseAccounts', icon: Receipt, module: 'club' },
      { path: '/subscriptions', label: 'nav.subscriptions', icon: Repeat2, module: 'club' },
      { path: '/statements', label: 'nav.statements', icon: FileText, module: 'club' },
      { path: '/club-payments', label: 'nav.payments', icon: Landmark, module: 'club' },
      { path: '/loyalty', label: 'nav.loyalty', icon: Gift, module: 'club' },
      { path: '/gift-cards', label: 'nav.giftCards', icon: Gift, module: 'club' },
      { path: '/donations', label: 'nav.donations', icon: HeartHandshake, module: 'club' },
      { path: '/campaigns', label: 'nav.campaigns', icon: Megaphone, module: 'club' },
      { path: '/communications', label: 'nav.communications', icon: MessageSquareText, module: 'club' },
      { path: '/club-reports', label: 'nav.clubReports', icon: BarChart3, module: 'club' },
      { path: '/portal', label: 'nav.memberPortal', icon: LayoutGrid, module: 'club' },
      { path: '/hospitality-settings', label: 'nav.clubSettings', icon: SlidersHorizontal, module: 'club' },
    ],
  },
  {
    id: 'workspace',
    label: 'nav.group.workspace',
    bar: 'bg-cyan-500',
    chip: 'bg-cyan-100 text-cyan-800',
    items: [
      { path: '/organizations', label: 'nav.workspaces', icon: ArrowRightLeft },
      { path: '/settings', label: 'nav.settings', icon: Settings },
      { path: '/settings?tab=claude', label: 'nav.claude', icon: Bot },
      { path: '/help', label: 'nav.help', icon: HelpCircle },
    ],
  },
];

/** Pages under a list that have their own entry, so the list's entry is not lit for them too. */
const SUB_PAGES = ['/leads/import', '/leads/webhook', '/members/import', '/contacts/import', '/companies/import'];

function pathOnly(path: string): string {
  return path.split('?')[0] ?? path;
}

function matchesPath(pathname: string, search: string, path: string): boolean {
  const target = pathOnly(path);
  const tab = path.includes('?') ? new URLSearchParams(path.split('?')[1]).get('tab') : null;
  const currentTab = new URLSearchParams(search).get('tab');

  if (tab) return pathname === target && currentTab === tab;
  if (pathname === target) return !(target === '/settings' && currentTab === 'claude');
  if (SUB_PAGES.includes(pathname)) return false;
  return pathname.startsWith(`${target}/`);
}

/**
 * The menu for the modules the workspace has on: a module's pages leave it while that
 * module is off, a group with nothing left goes, and a page whose usual place went with
 * its module shows where it is kept instead.
 */
function groupsFor(modules: ModulesOn, may: (permission: Permission) => boolean, t: (key: MessageKey) => string): NavGroup[] {
  return GROUPS.map((group) => ({
    ...group,
    label: t(!modules.sales && group.labelWithoutSales ? group.labelWithoutSales : group.label),
    labelWithoutSales: undefined,
    items: group.items
      .filter((item) =>
        (!item.module || modules[item.module]) && (!item.onlyWhenOff || !modules[item.onlyWhenOff])
        && (!item.permission || may(item.permission)))
      .map((item) => ({
        ...item,
        label: t(item.label),
        children: item.children?.map((child) => ({ ...child, label: t(child.label) })),
      })),
  })).filter((group) => group.items.length > 0);
}

function groupForPath(groups: NavGroup[], pathname: string, search: string): string {
  return groups.find((group) => group.items.some((item) => itemMatches(pathname, search, item)))?.id ?? groups[0]?.id ?? 'today';
}

function itemMatches(pathname: string, search: string, item: NavLinkItem): boolean {
  return matchesPath(pathname, search, item.path) || (item.children?.some((child) => matchesPath(pathname, search, child.path)) ?? false);
}

function currentItem(groups: NavGroup[], pathname: string, search: string): NavLinkItem | undefined {
  for (const group of groups) {
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
  const { t } = useT();

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          className="h-11 w-11 gap-0 rounded-xl px-0 text-slate-700 hover:bg-white hover:text-slate-900 focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] sm:w-auto sm:gap-2 sm:px-3 dark:text-slate-200 dark:hover:bg-slate-800"
          aria-label={t('nav.openMenu')}
        >
          <Menu className="h-5 w-5" aria-hidden="true" />
          <span className="hidden text-sm font-semibold sm:inline">{t('nav.menu')}</span>
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
  const modules = useModules();
  const { t } = useT();
  const groups = useMemo(
    // A page behind a permission needs a workspace too: a personal one has no such page.
    () => groupsFor(modules, (permission) => !!org?.currentOrg && mayIn(org, permission), t),
    [modules, org, t],
  );
  const filterId = useId();
  const activeGroup = groupForPath(groups, location.pathname, location.search);
  const here = currentItem(groups, location.pathname, location.search);
  const [query, setQuery] = useState('');
  const [expanded, setExpanded] = useState<Record<string, boolean>>(() => ({ [activeGroup]: true }));

  useEffect(() => {
    setExpanded((current) => ({ ...current, [activeGroup]: true }));
  }, [activeGroup]);

  const needle = query.trim().toLowerCase();
  const visibleGroups = useMemo(() => {
    if (!needle) return groups;
    return groups.map((group) => ({
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
  }, [needle, groups]);

  return (
    <>
      <SheetHeader className="gap-3 border-b border-slate-200 px-5 pb-4 pt-5 pr-16 dark:border-slate-800">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-[var(--brand-primary)]">CadenceE · Cadence Engine</p>
          <SheetTitle className="mt-1 text-left text-lg text-slate-950 dark:text-white">
            {org?.currentOrg?.name ?? t('nav.menu')}
          </SheetTitle>
          <SheetDescription className="sr-only">
            {here ? t('nav.youAreIn', { page: here.label }) : t('nav.chooseWhere')}
          </SheetDescription>
        </div>
        <div>
          <label htmlFor={filterId} className="sr-only">{t('nav.findPage')}</label>
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-slate-500" aria-hidden="true" />
            <input
              id={filterId}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t('nav.findPage')}
              className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50 pr-3 pl-9 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus-visible:border-[var(--brand-primary)] focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]/30 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
            />
          </div>
        </div>
      </SheetHeader>

      <nav className="flex-1 overflow-y-auto px-3 py-3" aria-label={t('nav.main')}>
        {visibleGroups.length === 0 ? (
          <p className="px-2 py-8 text-sm text-slate-500">{t('nav.noMatch', { query: query.trim() })}</p>
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
