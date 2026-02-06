import {
  LayoutDashboard,
  LayoutTemplate,
  History,
  HelpCircle,
  Users,
  UsersRound,
  UserCircle,
  Kanban,
  CheckSquare,
  Activity,
  Building2,
  Mail,
  FlaskConical,
  BarChart3,
  LucideIcon,
} from 'lucide-react';

export interface NavItem {
  path: string;
  label: string;
  icon: LucideIcon;
  badge?: string;
}

/**
 * Main navigation items in priority order
 * Items are shown based on available space, with overflow going to "More" menu
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { path: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { path: '/leads', label: 'Leads', icon: Users },
  { path: '/deals', label: 'Deals', icon: Kanban },
  { path: '/tasks', label: 'Tasks', icon: CheckSquare },
  { path: '/contacts', label: 'Contacts', icon: UserCircle },
  { path: '/activities', label: 'Activities', icon: Activity },
  { path: '/companies', label: 'Companies', icon: Building2 },
  { path: '/team', label: 'Team', icon: UsersRound },
  { path: '/templates', label: 'Templates', icon: LayoutTemplate },
  { path: '/sequences', label: 'Sequences', icon: Mail },
  { path: '/analytics', label: 'Analytics', icon: BarChart3 },
  { path: '/ab-tests', label: 'A/B Tests', icon: FlaskConical },
  { path: '/history', label: 'History', icon: History },
  { path: '/help', label: 'Help', icon: HelpCircle },
] as const;

/**
 * Navigation sizing constants
 */
export const NAV_ITEM_BASE_WIDTH = 16 + 8 + 24; // icon(16) + gap(8) + padding(24)
export const MORE_BUTTON_WIDTH = 80;
export const NAV_PADDING = 8;

/**
 * Estimate nav item width based on label length
 * @param label - Nav item label
 * @returns Estimated width in pixels
 */
export function estimateNavItemWidth(label: string): number {
  return NAV_ITEM_BASE_WIDTH + label.length * 7;
}

/**
 * Get visible nav items based on available width
 * @param availableWidth - Available width in pixels
 * @returns Array of visible nav items
 */
export function getVisibleNavItems(availableWidth: number): NavItem[] {
  const visible: NavItem[] = [];
  let usedWidth = NAV_PADDING;

  for (const item of NAV_ITEMS) {
    const itemWidth = estimateNavItemWidth(item.label);
    // Reserve space for "More" button if there are remaining items
    const remainingItems = NAV_ITEMS.length - visible.length - 1;
    const needsMoreButton = remainingItems > 0;
    const requiredWidth = usedWidth + itemWidth + (needsMoreButton ? MORE_BUTTON_WIDTH : 0);

    if (requiredWidth <= availableWidth) {
      visible.push(item);
      usedWidth += itemWidth;
    } else {
      break;
    }
  }

  return visible;
}

/**
 * Get overflow nav items (items that don't fit)
 * @param visibleCount - Number of visible items
 * @returns Array of overflow nav items
 */
export function getOverflowNavItems(visibleCount: number): NavItem[] {
  return NAV_ITEMS.slice(visibleCount) as NavItem[];
}
