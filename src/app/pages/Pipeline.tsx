import { useState, useEffect, useMemo, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { DndProvider } from 'react-dnd';
import { HTML5Backend } from 'react-dnd-html5-backend';
import {
  Briefcase,
  Plus,
  Trash2,
  Pencil,
  Target,
  Handshake,
  Trophy,
  DollarSign,
  User,
  AlertCircle,
  Sparkles,
  Search,
  X,
  TrendingUp,
  BarChart3,
  Clock,
  Zap,
  Filter,
  CheckCircle2,
  Activity as ActivityIcon,
  Calendar,
  CalendarClock,
  ListTodo,
  Phone,
  Mail,
  Users,
  FileText,
  Video,
  Presentation,
} from 'lucide-react';
import { toast } from 'sonner';
import { motion } from 'motion/react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  ResponsiveContainer,
  Cell,
  Tooltip,
} from 'recharts';
import AppHeader from '@/app/components/AppHeader';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { PageTransition } from '@/app/components/PageTransition';
import { KanbanSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getDealBoardColumn, getDealById, updateDeal, createDeal, deleteDeal, messages, readFlexParams, appendFlexFilters, writeFlexParams,
} from '@/app/api';
import { FlexFilterBar } from '@/app/components/flex/FlexFilterBar';
import { SavedViewsMenu } from '@/app/components/flex/SavedViewsMenu';
import {
  dealBoardKey, queryKeys, recordActivitiesKey, tasksKey, useAllCompanies, useAllContacts, useAllDeals, useCacheWriter,
  useDealBoard, useOrgMembers, usePipelines, useRecordActivities, useTasksList, useWorkspaceScope,
} from '@/app/hooks/queries';
import type { OrgMemberDto } from '@/app/api/organizations';
import { LoadFailed } from '@/app/components/LoadFailed';
import { isEditConflict, showEditConflict } from '@/app/lib/editConflict';
import { whenChosen } from '@/app/lib/whenChosen';
import { createActivity } from '@/app/api/activities';
import type { Deal, Company, Contact, Pipeline, Activity, DealBoard, DealBoardColumn, DealBoardQuery } from '@/app/api/types';
import { useDebounce } from '@/app/hooks/useDebounce';
import { getCurrentUser } from '@/app/lib/auth';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/app/components/ui/sheet';
import { cn } from '@/app/components/ui/utils';

// Import extracted components and utilities
import { DroppableStageColumn } from './pipeline/DroppableStageColumn';
import { STAGE_COLORS_MAP } from './pipeline/config';
import { getStageList, dealOutcome, daysSince, formatDays, getDaysUntilClose, formatCloseDate, UrgencyBadge } from './pipeline/utils';
import { AMOUNT_HINT, amountInput, amountOf, currencyOptions, parseMoneyInput } from '@/app/lib/money';
import { dateOnlyKey } from '@/app/lib/zonedTime';
import { useMoney } from '@/app/hooks/useMoney';
import { DealCard } from './pipeline/DealCard';
import { recordHref } from '@/app/hooks/useDocumentTitle';
import { createTask } from '@/app/api/tasks';
import type { TaskItem as TaskItemType } from '@/app/api/types';
import { useOrg } from '@/app/contexts/OrgContext';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, type PageStat } from '@/app/components/PageHero';
import { DuplicateMerge, DEAL_DUPLICATES } from '@/app/components/duplicates';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { StaggerItem } from '@/app/components/motion/Stagger';
import { ActivityFactsFields } from '@/app/components/ActivityFactsFields';
import { withActivity } from '@/app/components/RecordActivity';
import { activityFactsLine, activityTime, factsPayload, factsProblem, EMPTY_FACTS, type ActivityFactsDraft } from '@/app/lib/activityFacts';
import { WriteOnly } from '@/app/components/WriteOnly';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { currentDateLocale } from '@/app/i18n/current';

/** Cards per column in each page of the board. */
const BOARD_PAGE = 25;

/** The edit dialog's fields for a deal, as the dialog opens on it. */
function editFormFor(deal: Deal) {
  return {
    name: deal.name,
    value: amountInput(deal.amount),
    currency: deal.currency ?? '',
    expectedCloseDate: deal.expectedCloseDateUtc ? deal.expectedCloseDateUtc.slice(0, 10) : '',
    companyId: deal.companyId || '',
    pipelineId: deal.pipelineId ?? '',
    stageId: deal.dealStageId ?? (deal.stage ?? ''),
    assigneeId: deal.assigneeId ?? '',
    description: deal.description ?? '',
    probability: deal.probability?.toString() ?? '',
    // The save is checked against this, so it travels with the fields it belongs to.
    version: deal.version ?? '',
  };
}

const NO_PIPELINES: Pipeline[] = [];
const NO_DEALS: Deal[] = [];
const NO_CONTACTS: Contact[] = [];
const NO_COMPANIES: Company[] = [];
const NO_MEMBERS: OrgMemberDto[] = [];
const NO_TASKS: TaskItemType[] = [];
const NO_ACTIVITIES: Activity[] = [];

/** The board with one card taken from its column and put at the top of another. */
function withCardMoved(board: DealBoard, dealId: string, toStageId: string, toStageName: string): DealBoard {
  const card = board.columns.flatMap((c) => c.deals).find((d) => d.id === dealId);
  const target = board.columns.find((c) => c.key === toStageId || c.dealStageId === toStageId);
  if (!card || !target) return board;
  const moved: Deal = { ...card, dealStageId: target.dealStageId ?? card.dealStageId, stage: toStageName };
  return {
    ...board,
    columns: board.columns.map((c) => {
      const had = c.deals.some((d) => d.id === dealId);
      if (c === target) return had ? c : { ...c, count: c.count + 1, deals: [moved, ...c.deals] };
      return had ? { ...c, count: Math.max(0, c.count - 1), deals: c.deals.filter((d) => d.id !== dealId) } : c;
    }),
  };
}

export default function Pipeline() {
  const { currentOrg, isReadOnly: viewOnly } = useOrg();
  // Working deals takes records.edit: finance, the front desk and read-only members see them.
  const isReadOnly = viewOnly || !orgCan(currentOrg, PERMISSION.EditRecords);
  const money = useMoney();
  // Everything here is read through the shared cache, keyed by workspace, and read again
  // after a save that changed it (hooks/queries/invalidation).
  const ws = useWorkspaceScope();
  const writeCache = useCacheWriter();
  const [loadingMore, setLoadingMore] = useState<string | null>(null);
  const [boardSort, setBoardSort] = useState<NonNullable<DealBoardQuery['sort']>>('close');
  // The pipelines first: the board is drawn for one of them.
  const pipelinesQuery = usePipelines();
  const pipelines: Pipeline[] = pipelinesQuery.data ?? NO_PIPELINES;
  const pipelinesLoaded = !pipelinesQuery.isPending;
  const [selectedPipelineId, setSelectedPipelineId] = useState<string | null>(null);
  useEffect(() => {
    if (pipelines.length) setSelectedPipelineId((prev) => prev ?? pipelines[0]?.id ?? null);
  }, [pipelines]);
  // How this person likes deals shown: the board, a table, a list or cards, saved for them.
  const view = useViewPreference('deals', { layouts: ['board', 'table', 'list', 'grid'] });
  // Every deal, for the table, list and card layouts; read only when one of them is open.
  const allDealsQuery = useAllDeals(view.layout !== 'board');
  const deals: Deal[] = allDealsQuery.data ?? NO_DEALS;
  /** Show a change in the full list at once, if it has been read; the read after the save confirms it. */
  const setDeals = useCallback((update: (prev: Deal[]) => Deal[]) => {
    writeCache<Deal[]>(queryKeys.deals.all(ws), (current) => current && update(current));
  }, [writeCache, ws]);
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState({
    name: '',
    value: '',
    currency: '',
    stageId: '',
    pipelineId: '',
    expectedCloseDate: '',
    companyId: '',
    contactId: '',
    assigneeId: '',
    description: '',
    probability: '',
  });
  const contacts: Contact[] = useAllContacts().data ?? NO_CONTACTS;
  const [saving, setSaving] = useState(false);
  const [editDeal, setEditDeal] = useState<Deal | null>(null);
  const [editForm, setEditForm] = useState({ name: '', value: '', currency: '', expectedCloseDate: '', companyId: '', pipelineId: '', stageId: '', assigneeId: '', description: '', probability: '', version: '' });
  const [savingEdit, setSavingEdit] = useState(false);
  const [deleteConfirmDeal, setDeleteConfirmDeal] = useState<Deal | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [detailDeal, setDetailDeal] = useState<Deal | null>(null);
  // The companies to pick from, read once a deal dialog opens.
  const companies: Company[] = useAllCompanies(createOpen || !!editDeal || !!detailDeal).data ?? NO_COMPANIES;

  const openDealDetail = (deal: Deal) => {
    window.open(recordHref(`/deals/${deal.id}`, deal.name, 'Deals'), '_blank', 'noopener,noreferrer');
  };
  const orgMembers: OrgMemberDto[] = useOrgMembers().data ?? NO_MEMBERS;
  
  // HP-8: Close deal dialog state
  const [closeDeal, setCloseDeal] = useState<Deal | null>(null);
  const [closeAsWon, setCloseAsWon] = useState(true);
  const [closeReason, setCloseReason] = useState('');
  const [savingClose, setSavingClose] = useState(false);
  
  // HP-7 & HP-10: Advanced filter state
  const [filterAssignee, setFilterAssignee] = useState<string>('all');
  const [filterMyDeals, setFilterMyDeals] = useState(false);
  const [filterValueMin, setFilterValueMin] = useState('');
  const [filterValueMax, setFilterValueMax] = useState('');
  const [filterCloseDateFrom, setFilterCloseDateFrom] = useState('');
  const [filterCloseDateTo, setFilterCloseDateTo] = useState('');
  const [filterStale, setFilterStale] = useState(false);

  // HP-7 & HP-8: Tasks linked to deals
  const allTasks: TaskItemType[] = useTasksList().data ?? NO_TASKS;
  // A list never read is left alone, not replaced by the one change (see Tasks.tsx).
  const setAllTasks = (update: (prev: TaskItemType[]) => TaskItemType[]) =>
    writeCache<TaskItemType[]>(tasksKey(ws), (current) => current && update(current));
  const [addTaskDealId, setAddTaskDealId] = useState<string | null>(null);
  const [addTaskTitle, setAddTaskTitle] = useState('');
  const [savingTask, setSavingTask] = useState(false);

  const taskCountsByDeal = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const t of allTasks) {
      // Exclude completed and cancelled tasks from counts
      if (t.dealId && t.status !== 'completed' && t.status !== 'cancelled') {
        counts[t.dealId] = (counts[t.dealId] || 0) + 1;
      }
    }
    return counts;
  }, [allTasks]);
  
  // Deal activities state
  // The open deal's activities, read when its detail opens.
  const dealActivitiesQuery = useRecordActivities('deal', detailDeal?.id);
  const dealActivities: Activity[] = dealActivitiesQuery.data ?? NO_ACTIVITIES;
  const activitiesLoading = !!detailDeal && dealActivitiesQuery.isPending;
  const setDealActivities = (update: (prev: Activity[]) => Activity[]) => {
    if (detailDeal) writeCache<Activity[]>(recordActivitiesKey(ws, 'deal', detailDeal.id), (current) => current && update(current));
  };
  const [activityForm, setActivityForm] = useState<{ type: string; subject: string; body: string; facts?: ActivityFactsDraft }>({ type: 'note', subject: '', body: '' });
  const [savingActivity, setSavingActivity] = useState(false);
  
  // Search and Filter state
  const [searchQuery, setSearchQuery] = useState('');
  const [filterStage, setFilterStage] = useState<string>('all');
  const [_showFilters, _setShowFilters] = useState(false);

  const stageList = useMemo(() => getStageList(pipelines, selectedPipelineId), [pipelines, selectedPipelineId]);

  // The deals of the pipeline on screen. A deal saved before it had a pipeline belongs to the first one.
  const pipelineDeals = useMemo(() => {
    if (pipelines.length < 2 || !selectedPipelineId) return deals;
    const fallbackPipelineId = pipelines[0]?.id;
    return deals.filter((d) => (d.pipelineId ?? fallbackPipelineId) === selectedPipelineId);
  }, [deals, pipelines, selectedPipelineId]);

  // Filter deals — includes HP-7 (My Deals) and HP-10 (value range, assignee). The board
  // sends these to the server; the table, list and card layouts filter every deal here.
  const currentUserId = useMemo(() => getCurrentUser()?.id, []);
  // Typed filters wait for a pause in the typing, so each keystroke is not a request.
  const debouncedSearch = useDebounce(searchQuery, 300);
  const debouncedMin = useDebounce(filterValueMin, 300);
  const debouncedMax = useDebounce(filterValueMax, 300);
  const [searchParams, setSearchParams] = useSearchParams();
  const flexKey = searchParams.toString();
  const flex = useMemo(() => readFlexParams(new URLSearchParams(flexKey)), [flexKey]);
  const boardQuery = useMemo<DealBoardQuery>(() => ({
    pipelineId: selectedPipelineId ?? undefined,
    take: BOARD_PAGE,
    sort: boardSort,
    search: debouncedSearch.trim() || undefined,
    mine: filterMyDeals || undefined,
    assigneeId: filterAssignee !== 'all' && filterAssignee !== 'unassigned' ? filterAssignee : undefined,
    unassigned: filterAssignee === 'unassigned' || undefined,
    minAmount: parseMoneyInput(debouncedMin) ?? undefined,
    maxAmount: parseMoneyInput(debouncedMax) ?? undefined,
    closeFrom: filterCloseDateFrom || undefined,
    closeTo: filterCloseDateTo || undefined,
    staleOnly: filterStale || undefined,
    // Conditions on the deals' tags and custom fields, kept in the address.
    tag: flex.tags,
    tagMatch: flex.tagMatch,
    notTag: flex.notTags,
    field: flex.fields,
  }), [selectedPipelineId, boardSort, debouncedSearch, filterMyDeals, filterAssignee, debouncedMin, debouncedMax, filterCloseDateFrom, filterCloseDateTo, filterStale, flex]);

  // The board's filters as a saved view keeps them: the page's own (kept in its state) under the
  // names of the board's query, with the tag and field conditions from the address. The
  // pipeline on screen is not part of a view, so a view opens on whichever one is shown.
  const boardView = useMemo(() => {
    const kept = new URLSearchParams();
    if (searchQuery.trim()) kept.set('search', searchQuery.trim());
    if (filterMyDeals) kept.set('mine', 'true');
    if (filterAssignee !== 'all') kept.set('assignee', filterAssignee);
    if (filterValueMin.trim()) kept.set('minAmount', filterValueMin.trim());
    if (filterValueMax.trim()) kept.set('maxAmount', filterValueMax.trim());
    if (filterCloseDateFrom) kept.set('closeFrom', filterCloseDateFrom);
    if (filterCloseDateTo) kept.set('closeTo', filterCloseDateTo);
    if (filterStale) kept.set('staleOnly', 'true');
    if (boardSort !== 'close') kept.set('sort', boardSort);
    return appendFlexFilters(kept, flex);
  }, [searchQuery, filterMyDeals, filterAssignee, filterValueMin, filterValueMax, filterCloseDateFrom, filterCloseDateTo, filterStale, boardSort, flex]);

  /** Open the board as a saved view has it: its own filters set, its tag and field conditions put in the address. */
  const openBoardView = (next: URLSearchParams) => {
    setSearchQuery(next.get('search') ?? '');
    setFilterMyDeals(next.get('mine') === 'true');
    setFilterAssignee(next.get('assignee') || 'all');
    setFilterValueMin(next.get('minAmount') ?? '');
    setFilterValueMax(next.get('maxAmount') ?? '');
    setFilterCloseDateFrom(next.get('closeFrom') ?? '');
    setFilterCloseDateTo(next.get('closeTo') ?? '');
    setFilterStale(next.get('staleOnly') === 'true');
    const sort = next.get('sort');
    setBoardSort(sort === 'value' || sort === 'created' || sort === 'name' ? sort : 'close');
    setSearchParams(writeFlexParams(searchParams, readFlexParams(next)), { replace: true });
  };

  // The board for the pipeline on screen and the filters set, a page of cards per stage.
  // Keyed by those filters, so an older filter's answer can never replace the board for
  // newer ones, and the read for filters changed since is cancelled. It waits for the
  // pipelines, so the first read is already for the pipeline on screen.
  const boardReady = pipelinesLoaded && (pipelines.length === 0 || selectedPipelineId !== null);
  const boardResult = useDealBoard(boardQuery, boardReady);
  const board: DealBoard | null = boardResult.data ?? null;
  const loading = !boardReady || boardResult.isPending;
  usePageLoading(loading);
  /** Change the board on screen at once; the read after the save puts the server's figures back. */
  const setBoard = useCallback((update: (prev: DealBoard) => DealBoard) => {
    writeCache<DealBoard>(dealBoardKey(ws, boardQuery), (current) => current && update(current));
  }, [writeCache, ws, boardQuery]);

  // The figures above the board: the pipeline's, worked out by the server over all its
  // deals, whatever the filters. They were added up here from every deal downloaded.
  const summary = board?.summary;
  const dealStats = useMemo(() => {
    // Per currency, never across. A currency whose deals have no readable amount adds nothing.
    const withAmounts = (rows: { currency: string; value: number; count: number; unreadable: number }[] = []) =>
      rows.map((r) => ({ currency: r.currency, value: r.value, count: r.count - r.unreadable })).filter((r) => r.count > 0);
    return {
      total: summary?.total ?? 0,
      won: summary?.won ?? 0,
      lost: summary?.lost ?? 0,
      active: summary?.open ?? 0,
      activeTotals: withAmounts(summary?.openTotals),
      wonTotals: withAmounts(summary?.wonTotals),
      weighted: summary?.weighted ?? [],
      winRate: summary?.winRate === undefined ? 0 : Math.round(summary.winRate * 100),
      overdue: summary?.overdue ?? 0,
      closingThisWeek: summary?.closingThisWeek ?? 0,
      stale: summary?.stale ?? 0,
    };
  }, [summary]);


  const filteredDeals = useMemo(() => {
    let result = [...pipelineDeals];

    // Search filter
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(d =>
        d.name.toLowerCase().includes(q) ||
        d.value.toLowerCase().includes(q) ||
        (d.contactName?.toLowerCase().includes(q)) ||
        (d.companyName?.toLowerCase().includes(q)) ||
        (d.assigneeName?.toLowerCase().includes(q)) ||
        (contacts.find(c => c.id === d.contactId)?.name.toLowerCase().includes(q))
      );
    }

    // Stage filter
    if (filterStage !== 'all') {
      result = result.filter(d => {
        const stage = d.dealStageId ? stageList.find(s => s.id === d.dealStageId)?.name : d.stage;
        return stage === filterStage;
      });
    }

    // HP-7: My Deals. The board also counts a deal nobody owns that I created; a deal's
    // creator is not sent to the browser, so here it is the deals I own.
    if (filterMyDeals && currentUserId) {
      result = result.filter(d => d.assigneeId === currentUserId);
    }

    // HP-10: Assignee filter
    if (filterAssignee !== 'all') {
      if (filterAssignee === 'unassigned') {
        result = result.filter(d => !d.assigneeId);
      } else {
        result = result.filter(d => d.assigneeId === filterAssignee);
      }
    }

    // HP-10: Value range filter, on the stored amounts. A deal with none is outside any range.
    const min = parseMoneyInput(filterValueMin);
    if (min !== null) result = result.filter(d => (amountOf(d) ?? Number.NaN) >= min);
    const max = parseMoneyInput(filterValueMax);
    if (max !== null) result = result.filter(d => (amountOf(d) ?? Number.NaN) <= max);

    // HP-10: Expected close date range filter, by the day picked.
    if (filterCloseDateFrom) {
      result = result.filter(d => d.expectedCloseDateUtc && dateOnlyKey(d.expectedCloseDateUtc) >= filterCloseDateFrom);
    }
    if (filterCloseDateTo) {
      result = result.filter(d => d.expectedCloseDateUtc && dateOnlyKey(d.expectedCloseDateUtc) <= filterCloseDateTo);
    }

    if (filterStale) result = result.filter(d => d.isStale);

    return result;
  }, [pipelineDeals, searchQuery, filterStage, contacts, stageList, filterMyDeals, currentUserId, filterAssignee, filterValueMin, filterValueMax, filterCloseDateFrom, filterCloseDateTo, filterStale]);

  // The board's columns; picking a stage shows that one alone.
  const boardColumns = useMemo(() => {
    const columns = board?.columns ?? [];
    return filterStage === 'all' ? columns : columns.filter((c) => c.name === filterStage);
  }, [board, filterStage]);

  /**
   * Read the board again now, and every deal if a layout shows them: after duplicates are
   * merged, after a refused save, and on retry. A save of a deal does not need it: the
   * cache reads the deals again once the save is announced.
   */
  const refresh = () => {
    void boardResult.refetch();
    if (view.layout !== 'board') void allDealsQuery.refetch();
  };

  /** The next cards of one column, with the board's filters and order. */
  const loadMore = async (column: DealBoardColumn) => {
    setLoadingMore(column.key);
    try {
      const page = await getDealBoardColumn(boardQuery, column.key, column.deals.length);
      setBoard((prev) => prev && {
        ...prev,
        columns: prev.columns.map((c) => c.key !== column.key ? c : {
          ...c,
          deals: [...c.deals, ...page.deals.filter((d) => !c.deals.some((x) => x.id === d.id))],
          hasMore: page.hasMore,
        }),
      });
    } catch {
      toast.error(messages.errors.loadFailed);
    } finally {
      setLoadingMore(null);
    }
  };

  /** Opens the edit dialog on a deal, with that deal's fields, in one update. */
  const openEditDeal = (deal: Deal) => {
    setEditDeal(deal);
    setEditForm(editFormFor(deal));
  };

  // The board may have loaded minutes ago, and the dialog's save is checked against the
  // version it opened with. So the dialog takes the deal as it is now, read when it
  // opens, as long as nothing has been typed yet: a fresher copy never throws away what
  // someone entered, and a save from the older one is refused and offers a reload.
  const [freshEditDeal, setFreshEditDeal] = useState<Deal | null>(null);
  const editDealId = editDeal?.id;
  useEffect(() => {
    if (!editDealId) return;
    let live = true;
    getDealById(editDealId)
      .then((fresh) => { if (live && fresh) setFreshEditDeal(fresh); })
      .catch(() => { /* the board's copy stands */ });
    return () => { live = false; };
  }, [editDealId]);
  useEffect(() => {
    if (!freshEditDeal) return;
    setFreshEditDeal(null);
    if (!editDeal || editDeal.id !== freshEditDeal.id) return;
    // Decided inside the form's own update, against the form as it is when the update
    // applies. Checked here and applied by a later effect, a keystroke that landed in
    // between was overwritten by the fresh copy: an emptied description came back.
    const opened = JSON.stringify(editFormFor(editDeal));
    setEditForm((current) => (JSON.stringify(current) === opened ? editFormFor(freshEditDeal) : current));
  }, [freshEditDeal, editDeal]);

  /** After a refused save: the deal as it is now, in the dialog and on the board. */
  const reloadEditDeal = async (id: string) => {
    try {
      const fresh = await getDealById(id);
      if (!fresh) return;
      setEditDeal(fresh);
      setEditForm(editFormFor(fresh));
      setDeals((prev) => prev.map((d) => (d.id === id ? fresh : d)));
      refresh();
    } catch {
      toast.error(messages.errors.generic);
    }
  };

  // Drawn by count: a bar per stage cannot hold money in more than one currency. The
  // count is the stage's, from the server, not the cards loaded so far.
  const funnelData = boardColumns.map((column) => ({
    name: column.name,
    count: column.count,
    fill: STAGE_COLORS_MAP[column.name]?.bar ?? '#94a3b8',
  }));

  const handleMoveStage = async (dealId: string, newStageId: string, newStageName: string) => {
    const deal = board?.columns.flatMap((c) => c.deals).find((d) => d.id === dealId) ?? deals.find((d) => d.id === dealId);
    const currentStageId = deal?.dealStageId ?? deal?.stage ?? '';
    if (deal && currentStageId === newStageId) return;
    const target = stageList.find((s) => s.id === newStageId);
    const isWon = target?.isWon ? true : target?.isLost ? false : undefined;
    const payload: { stage: string; dealStageId?: string; isWon?: boolean } = { stage: newStageName };
    if (/^[0-9a-f-]{36}$/i.test(newStageId)) payload.dealStageId = newStageId;
    if (isWon !== undefined) payload.isWon = isWon;
    // The card moves to its new column at once. The columns' counts, money and stale
    // deals are the server's to work out again, which the cache does once the save is
    // announced; a refused move puts the board back as it was. While the board for new
    // filters is on its way, the one on screen is the last filters' stand-in: nothing
    // moves in it, and it is never put back under the new filters.
    const before = boardResult.isPlaceholderData ? null : board;
    if (deal) setBoard((prev) => withCardMoved(prev, dealId, newStageId, newStageName));
    const putBack = () => { if (before) writeCache<DealBoard>(dealBoardKey(ws, boardQuery), () => before); };
    try {
      const updated = await updateDeal(dealId, payload);
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === dealId ? updated : d)));
        toast.success(messages.success.dealMoved);
      } else {
        putBack();
        toast.error(messages.errors.generic);
      }
    } catch {
      putBack();
      toast.error(messages.errors.generic);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmDeal) return;
    setDeleting(true);
    try {
      const ok = await deleteDeal(deleteConfirmDeal.id);
      if (ok) {
        setDeals((prev) => prev.filter((d) => d.id !== deleteConfirmDeal.id));
        toast.success(messages.success.dealDeleted);
        setDeleteConfirmDeal(null);
        if (detailDeal?.id === deleteConfirmDeal.id) setDetailDeal(null);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  const editStageList = getStageList(pipelines, editForm.pipelineId || editDeal?.pipelineId || selectedPipelineId);

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editDeal) return;
    if (!editForm.name.trim() || !editForm.value.trim()) {
      toast.error(messages.validation.nameAndValueRequired);
      return;
    }
    // Read once, here, the way the server read older deals: "85'500", "1.234,56" and
    // "CHF 12,500" are all numbers. The server took plain digits only and refused the rest.
    const amount = parseMoneyInput(editForm.value);
    if (amount === null) {
      toast.error(AMOUNT_HINT);
      return;
    }
    setSavingEdit(true);
    try {
      const expectedCloseDateUtc = editForm.expectedCloseDate
        ? new Date(editForm.expectedCloseDate).toISOString()
        : undefined;
      const stageName = editStageList.find((s) => s.id === editForm.stageId)?.name ?? editForm.stageId;
      const updated = await updateDeal(editDeal.id, {
        name: editForm.name.trim(),
        amount,
        currency: editForm.currency || undefined,
        stage: stageName,
        pipelineId: editForm.pipelineId || undefined,
        dealStageId: /^[0-9a-f-]{36}$/i.test(editForm.stageId) ? editForm.stageId : undefined,
        // An emptied field is sent as null, which takes it off. Left out, as it was,
        // choosing no company or no owner said "Deal updated" and changed nothing.
        expectedCloseDateUtc: expectedCloseDateUtc ?? null,
        companyId: editForm.companyId || null,
        assigneeId: editForm.assigneeId || null,
        description: editForm.description.trim() || null,
        probability: editForm.probability ? parseInt(editForm.probability, 10) : null,
        // The dialog sends back every field it shows, so the save is refused, rather
        // than written over, when someone saved the deal after this copy was read.
        version: editForm.version || undefined,
      });
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === editDeal.id ? updated : d)));
        toast.success(messages.success.dealUpdated);
        setEditDeal(null);
        if (detailDeal?.id === editDeal.id) setDetailDeal(updated);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch (err) {
      if (isEditConflict(err)) {
        const dealId = editDeal.id;
        showEditConflict(err.message, () => { void reloadEditDeal(dealId); });
      } else {
        // The server says why ("A currency is a three-letter ISO code"); "Something went
        // wrong" left people guessing which field it was.
        toast.error(err instanceof Error && err.message ? err.message : messages.errors.generic);
      }
    } finally {
      setSavingEdit(false);
    }
  };

  const setExpectedCloseInDays = (days: number) => {
    const d = new Date();
    d.setDate(d.getDate() + days);
    setCreateForm((f) => ({ ...f, expectedCloseDate: d.toISOString().slice(0, 10) }));
  };

  const createStageList = getStageList(pipelines, createForm.pipelineId || selectedPipelineId);
  const openCreateDeal = () => {
    const pipelineId = selectedPipelineId ?? pipelines[0]?.id ?? '';
    const firstStage = getStageList(pipelines, pipelineId)[0];
    // Priced in the workspace's currency unless someone picks another. The dialog
    // opened with none picked, and a deal saved like that counted as USD.
    setCreateForm({
      name: '',
      value: '',
      currency: money.currency,
      stageId: firstStage?.id ?? 'Qualification',
      pipelineId,
      expectedCloseDate: '',
      companyId: '',
      contactId: '',
      assigneeId: '',
      description: '',
      probability: '',
    });
    setCreateOpen(true);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!createForm.name.trim() || !createForm.value.trim()) {
      toast.error(messages.validation.nameAndValueRequired);
      return;
    }
    const amount = parseMoneyInput(createForm.value);
    if (amount === null) {
      toast.error(AMOUNT_HINT);
      return;
    }
    setSaving(true);
    try {
      const expectedCloseDateUtc = createForm.expectedCloseDate
        ? new Date(createForm.expectedCloseDate).toISOString()
        : undefined;
      const stageName = createStageList.find((s) => s.id === createForm.stageId)?.name ?? createForm.stageId;
      const created = await createDeal({
        name: createForm.name.trim(),
        amount,
        currency: createForm.currency.trim() || money.currency,
        stage: stageName,
        pipelineId: createForm.pipelineId || undefined,
        dealStageId: /^[0-9a-f-]{36}$/i.test(createForm.stageId) ? createForm.stageId : undefined,
        expectedCloseDateUtc,
        companyId: createForm.companyId || undefined,
        contactId: createForm.contactId || undefined,
        assigneeId: createForm.assigneeId || undefined,
        description: createForm.description.trim() || undefined,
        probability: createForm.probability ? parseInt(createForm.probability, 10) : undefined,
      });
      if (created) {
        setDeals((prev) => [created, ...prev]);
        toast.success(messages.success.dealCreated);
        setCreateOpen(false);
        const pipelineId = selectedPipelineId ?? pipelines[0]?.id ?? '';
        const firstStage = getStageList(pipelines, pipelineId)[0];
        setCreateForm({ name: '', value: '', currency: money.currency, stageId: firstStage?.id ?? 'Qualification', pipelineId, expectedCloseDate: '', companyId: '', contactId: '', assigneeId: '', description: '', probability: '' });
      } else {
        toast.error(messages.errors.generic);
      }
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  // Handle logging activity for a deal
  const handleLogActivity = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detailDeal || !activityForm.subject.trim()) {
      toast.error('Subject is required');
      return;
    }
    setSavingActivity(true);
    try {
      const activity = await createActivity({
        type: activityForm.type,
        subject: activityForm.subject.trim(),
        body: activityForm.body.trim() || undefined,
        dealId: detailDeal.id,
        ...factsPayload(activityForm.type, activityForm.facts),
      });
      if (activity) {
        setDealActivities((prev) => withActivity(prev, activity));
        setActivityForm({ type: 'note', subject: '', body: '' });
        toast.success('Activity logged');
        // The deal's last activity moves to when this happened, and never back: a
        // call logged late for last week does not undo what has happened since.
        const happened = activityTime(activity);
        const later = (current?: string | null) =>
          current && Date.parse(current) >= Date.parse(happened) ? current : happened;
        setDeals((prev) =>
          prev.map((d) =>
            d.id === detailDeal.id
              ? { ...d, lastActivityAtUtc: later(d.lastActivityAtUtc) }
              : d
          )
        );
        setDetailDeal((prev) =>
          prev ? { ...prev, lastActivityAtUtc: later(prev.lastActivityAtUtc) } : prev
        );
      } else {
        toast.error('Failed to log activity');
      }
    } catch {
      toast.error('Failed to log activity');
    } finally {
      setSavingActivity(false);
    }
  };

  // HP-8: Close deal with reason
  const handleCloseDeal = async () => {
    if (!closeDeal) return;
    setSavingClose(true);
    try {
      const updated = await updateDeal(closeDeal.id, {
        isWon: closeAsWon,
        closedReason: closeReason.trim() || undefined,
      });
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === closeDeal.id ? updated : d)));
        toast.success(closeAsWon ? 'Deal marked as Won!' : 'Deal marked as Lost');
        setCloseDeal(null);
        setCloseReason('');
        if (detailDeal?.id === closeDeal.id) setDetailDeal(updated);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSavingClose(false);
    }
  };

  // HP-8: Add task linked to a deal
  const handleAddTaskToDeal = async () => {
    if (!addTaskDealId || !addTaskTitle.trim()) return;
    setSavingTask(true);
    try {
      const task = await createTask({ title: addTaskTitle.trim(), dealId: addTaskDealId });
      if (task) {
        setAllTasks((prev) => [...prev, task]);
        toast.success('Task added to deal');
        setAddTaskDealId(null);
        setAddTaskTitle('');
      }
    } catch {
      toast.error('Failed to add task');
    } finally {
      setSavingTask(false);
    }
  };

  const dealStageName = (d: Deal) => {
    if (d.dealStageId) {
      const s = stageList.find((x) => x.id === d.dealStageId);
      if (s) return s.name;
    }
    return d.stage || 'Qualification';
  };
  // The open pipeline, one figure per currency. The header badge used to count won
  // deals in too, so it read larger than the tile beneath it.
  const openPipeline = money.formatTotals(dealStats.activeTotals, 'never');
  const wonText = dealStats.wonTotals.length > 0 ? money.formatTotals(dealStats.wonTotals, 'never') : null;
  // The tile counts up one number, so it shows the currency most of the pipeline is
  // in, and names the others beside it rather than adding them in.
  const [mainOpen, ...otherOpen] = dealStats.activeTotals;
  const openCurrency = mainOpen?.currency ?? money.currency;
  // The open pipeline weighted by each deal's probability, or its stage's, per currency.
  const weightedText = dealStats.weighted.some((w) => w.covered > 0)
    ? money.formatTotals(dealStats.weighted.filter((w) => w.covered > 0), 'never')
    : null;
  // Every layout but the board filters every deal here; the board's are counted by the server.
  const hasDeals = dealStats.total > 0;
  const shownCount = view.layout === 'board' ? (board?.count ?? 0) : filteredDeals.length;
  const filtersActive = !!(searchQuery || filterStage !== 'all' || filterMyDeals || filterAssignee !== 'all' || filterValueMin
    || filterValueMax || filterCloseDateFrom || filterCloseDateTo || filterStale);
  const stagePill = (deal: Deal) => {
    const name = dealStageName(deal);
    const outcome = dealOutcome(deal, stageList);
    return (
      <span className={cn(
        'inline-flex items-center gap-2 rounded-xl px-2.5 py-1 text-xs font-bold',
        outcome === 'won' && 'bg-emerald-100 text-emerald-700',
        outcome === 'lost' && 'bg-slate-100 text-slate-600',
        outcome === 'open' && name === 'Qualification' && 'bg-blue-100 text-blue-700',
        outcome === 'open' && name === 'Proposal' && 'bg-amber-100 text-amber-700',
        outcome === 'open' && name === 'Negotiation' && 'bg-violet-100 text-violet-700',
        outcome === 'open' && !['Qualification', 'Proposal', 'Negotiation'].includes(name) && 'bg-slate-100 text-slate-600',
      )}>
        <span className="h-2 w-2 rounded-full" style={{ backgroundColor: STAGE_COLORS_MAP[name]?.bar ?? '#94a3b8' }} />
        {name}
      </span>
    );
  };
  // The stored amount, in the deal's own currency.
  const dealValue = (deal: Deal) => (
    <span className="font-semibold text-emerald-700">{money.formatDeal(deal)}</span>
  );
  const contactOf = (deal: Deal) => (deal.contactId ? contacts.find((c) => c.id === deal.contactId)?.name : undefined) ?? deal.contactName;
  const closeDate = (deal: Deal) => {
    if (!deal.expectedCloseDateUtc) return '—';
    const days = getDaysUntilClose(deal.expectedCloseDateUtc, money.timeZone);
    return (
      <span className="flex flex-wrap items-center gap-1.5">
        <span>{formatCloseDate(deal.expectedCloseDateUtc)}</span>
        {days !== null && <UrgencyBadge days={days} />}
      </span>
    );
  };
  const dealColumns: RecordColumn<Deal>[] = [
    {
      id: 'name',
      label: 'Deal',
      required: true,
      cell: (d) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={d.name} size="sm" />
          <span className="truncate">{d.name}</span>
          {d.isPrivate && <span className="shrink-0 rounded-full bg-slate-100 px-1.5 text-[10px] font-semibold text-slate-600">Private</span>}
          {d.isStale && <span className="shrink-0 rounded-full bg-amber-100 px-1.5 text-[10px] font-semibold text-amber-800">Stale</span>}
        </span>
      ),
    },
    { id: 'value', label: 'Value', numeric: true, cell: dealValue },
    { id: 'stage', label: 'Stage', cell: stagePill },
    { id: 'company', label: 'Company', byDefault: false, cell: (d) => d.companyName || '—' },
    { id: 'contact', label: 'Contact', cell: (d) => contactOf(d) || '—' },
    { id: 'owner', label: 'Owner', byDefault: false, cell: (d) => d.assigneeName || '—' },
    { id: 'probability', label: 'Probability', numeric: true, byDefault: false, cell: (d) => (d.probability == null ? '—' : `${d.probability}%`) },
    { id: 'close', label: 'Close date', cell: closeDate },
    {
      id: 'inStage', label: 'In stage', byDefault: false,
      cell: (d) => { const days = daysSince(d.stageEnteredAtUtc); return days === null ? '—' : formatDays(days); },
    },
    { id: 'tasks', label: 'Tasks', numeric: true, byDefault: false, cell: (d) => taskCountsByDeal[d.id] || '—' },
  ];
  /** Edit and delete, the same on every layout. */
  const dealActions = (deal: Deal) => (isReadOnly ? null : (
    <>
      <Button
        variant="ghost"
        size="icon"
        className="h-8 w-8 rounded-xl text-slate-500 hover:bg-emerald-50 hover:text-emerald-600"
        onClick={() => openEditDeal(deal)}
        aria-label={`Edit ${deal.name}`}
      >
        <Pencil className="h-4 w-4" />
      </Button>
      <WriteOnly permission={[PERMISSION.EditRecords, PERMISSION.DeleteRecords]} all>
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8 rounded-xl text-slate-500 hover:bg-red-50 hover:text-red-600"
          onClick={() => setDeleteConfirmDeal(deal)}
          aria-label={`Delete ${deal.name}`}
        >
          <Trash2 className="h-4 w-4" />
        </Button>
      </WriteOnly>
    </>
  ));

  // The board's numbers, in its header. Deals Won shows the won deals.
  const dealHeroStats: PageStat[] = [
    {
      label: 'Total Pipeline Value', value: mainOpen?.value ?? 0, icon: DollarSign, tone: 'emerald',
      hint: [
        otherOpen.length > 0 ? `and ${money.formatTotals(otherOpen, 'never')}` : null,
        weightedText ? `${weightedText} weighted` : null,
        wonText ? `${wonText} won` : null,
      ].filter(Boolean).join(' · ') || undefined,
      format: (n) => money.format(n, openCurrency, 'never'),
    },
    {
      label: 'Win Rate', value: dealStats.winRate, icon: TrendingUp, tone: 'blue',
      hint: dealStats.winRate >= 50 ? 'great' : 'needs work', format: (n) => `${Math.round(n)}%`,
    },
    {
      label: 'Total Deals', value: dealStats.total, icon: BarChart3, tone: 'slate',
      hint: `${dealStats.active} open · ${dealStats.won + dealStats.lost} closed`,
    },
    {
      label: 'Deals Won', value: dealStats.won, icon: Trophy, tone: 'orange',
      hint: wonText ?? undefined,
      onClick: () => setFilterStage(stageList.find((st) => st.isWon)?.name ?? 'Closed Won'),
      actionLabel: `Deals won, ${dealStats.won}. Activate to show the won deals.`,
    },
    dealStats.overdue > 0
      ? { label: 'Overdue Deals', value: dealStats.overdue, icon: AlertCircle, tone: 'rose', hint: 'action needed' }
      : { label: 'All on track!', value: dealStats.overdue, icon: CheckCircle2, tone: 'emerald', hint: 'none overdue' },
    ...(board && board.staleAfterDays > 0
      ? [{
        label: 'Stale Deals', value: dealStats.stale, icon: Clock, tone: dealStats.stale > 0 ? 'amber' as const : 'slate' as const,
        hint: `no activity for ${board.staleAfterDays}+ days`,
        onClick: () => setFilterStale((on) => !on),
        pressed: filterStale,
        actionLabel: `Stale deals, ${dealStats.stale}. Activate to show only the stale deals.`,
      }]
      : []),
  ];

  const detailContact = detailDeal?.contactId ? contacts.find((c) => c.id === detailDeal.contactId) : undefined;
  const detailCompany = detailDeal?.companyId ? companies.find((c) => c.id === detailDeal.companyId) : undefined;

  return (
    <div className="min-h-screen flex flex-col overflow-hidden bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 min-h-0 flex flex-col w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Target}
            tone="violet"
            title="Deals"
            subtitle={loading ? 'Loading your pipeline…' : 'Track opportunities from qualification to close'}
            badges={!loading && hasDeals && (
              <>
                <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse motion-reduce:animate-none" aria-hidden />
                  {dealStats.active} active
                </span>
                <span className="inline-flex items-center gap-1 rounded-full border border-violet-200 bg-violet-50 px-2 py-0.5 text-[11px] font-semibold text-violet-700">
                  <TrendingUp className="h-3 w-3" aria-hidden />
                  {openPipeline}
                </span>
              </>
            )}
            actions={!isReadOnly && (
              <Button onClick={openCreateDeal} className={heroPrimary('violet')}>
                <Plus className="h-4 w-4" />
                New Deal
              </Button>
            )}
            stats={!loading && hasDeals ? dealHeroStats : undefined}
          />

          {/* The board is the server's now: a merge reloads it, not only the full list. */}
          <DuplicateMerge spec={DEAL_DUPLICATES} onMerged={refresh} />

          {/* Action Alert Banner */}
          {!loading && hasDeals && dealStats.overdue > 0 && (
            <motion.div 
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.4 }}
              className="mb-4 relative overflow-hidden rounded-2xl bg-gradient-to-r from-red-500 via-rose-500 to-pink-500 p-[1px] shadow-lg shadow-red-200/50"
            >
              <div className="relative bg-gradient-to-r from-red-50 via-rose-50 to-pink-50 rounded-2xl p-4">
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-red-500 to-rose-500 flex items-center justify-center shadow-lg shadow-red-300/50 shrink-0">
                    <AlertCircle className="w-6 h-6 text-white" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-bold text-red-800 flex items-center gap-2">
                      <Zap className="w-4 h-4" />
                      Immediate Attention Required
                    </p>
                    <p className="mt-0.5 text-sm text-red-700">
                      {dealStats.overdue} deal{dealStats.overdue > 1 ? 's are' : ' is'} past the expected close date. Review and update to keep your pipeline healthy.
                    </p>
                  </div>
                  <Button 
                    variant="outline" 
                    size="sm" 
                    className="shrink-0 border-red-300 text-red-700 hover:bg-red-100 hover:border-red-400"
                    onClick={() => setFilterStage('all')}
                  >
                    Review Now
                  </Button>
                </div>
              </div>
            </motion.div>
          )}

          {/* Search and filter bar */}
          {!loading && hasDeals && (
            <motion.div 
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.3 }}
              className="relative mb-6 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur"
            >
              {/* Decorative elements */}
              <div className="absolute top-0 right-0 w-32 h-32 bg-violet-300/25 rounded-full blur-2xl -mr-10 -mt-10" />
              <div className="absolute bottom-0 left-0 w-24 h-24 bg-emerald-300/25 rounded-full blur-2xl -ml-10 -mb-10" />
              
              <div className="relative flex flex-col sm:flex-row gap-3">
                {/* Search Input - Compact Enhanced */}
                <div className="relative flex-1 group">
                  <div className="absolute inset-0 bg-gradient-to-r from-violet-500/20 via-fuchsia-500/20 to-violet-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                  <div className="relative">
                    <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-violet-100 to-fuchsia-100 flex items-center justify-center group-focus-within:from-violet-200 group-focus-within:to-fuchsia-200 transition-all duration-300">
                      <Search className="w-4 h-4 text-violet-500 group-focus-within:text-violet-600 transition-colors" aria-hidden />
                    </div>
                    <input
                      type="search"
                      placeholder="Search deals by name, value, or contact..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="w-full pl-14 pr-10 h-11 rounded-xl border border-slate-200 bg-slate-50/80 text-slate-900 text-sm placeholder:text-slate-400 focus:outline-none focus:border-violet-300 focus:ring-4 focus:ring-violet-200/50 focus:bg-white hover:bg-white hover:border-slate-300 transition-all duration-300"
                      aria-label="Search deals"
                    />
                    {searchQuery && (
                      <button
                        type="button"
                        onClick={() => setSearchQuery('')}
                        className="absolute right-3 top-1/2 -translate-y-1/2 w-7 h-7 rounded-lg bg-slate-100 hover:bg-red-50 flex items-center justify-center text-slate-500 hover:text-red-500 transition-all duration-200"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>

                {pipelines.length > 1 && (
                  <Select
                    value={selectedPipelineId ?? pipelines[0]?.id}
                    onValueChange={(id) => { setSelectedPipelineId(id); setFilterStage('all'); }}
                  >
                    <SelectTrigger aria-label="Pipeline" className="relative h-11 w-full sm:w-[200px] rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-violet-50 hover:border-violet-300 focus:border-violet-300 focus:ring-2 focus:ring-violet-200/60 transition-all duration-300">
                      <SelectValue placeholder="Pipeline" />
                    </SelectTrigger>
                    <SelectContent className="rounded-xl border border-slate-200 shadow-xl">
                      {pipelines.map((p) => (
                        <SelectItem key={p.id} value={p.id} className="rounded-lg">{p.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}

                {/* Stage Filter - Compact Enhanced */}
                <div className="relative group/filter">
                  <div className="absolute inset-0 bg-violet-200/50 rounded-xl blur-lg opacity-0 group-hover/filter:opacity-60 transition-all duration-300" />
                  <Select value={filterStage} onValueChange={setFilterStage}>
                    <SelectTrigger aria-label="Filter by stage" className="relative h-11 w-full sm:w-[200px] rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-violet-50 hover:border-violet-300 focus:border-violet-300 focus:ring-2 focus:ring-violet-200/60 transition-all duration-300">
                      <div className="flex items-center gap-2">
                        <div className="w-7 h-7 rounded-lg bg-violet-100 flex items-center justify-center">
                          <Filter className="w-3.5 h-3.5 text-violet-600" />
                        </div>
                        <SelectValue placeholder="All stages" />
                      </div>
                    </SelectTrigger>
                  <SelectContent className="rounded-xl border border-slate-200 shadow-xl">
                    <SelectItem value="all" className="rounded-lg">All stages</SelectItem>
                    {stageList.map((stage) => (
                      <SelectItem key={stage.id} value={stage.name} className="rounded-lg">
                        <span className="flex items-center gap-2">
                          <span 
                            className="w-3 h-3 rounded-full shadow-sm"
                            style={{ backgroundColor: STAGE_COLORS_MAP[stage.name]?.bar ?? '#94a3b8' }}
                          />
                          {stage.name}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

                {/* HP-7: My Deals toggle */}
                <button
                  type="button"
                  onClick={() => setFilterMyDeals(!filterMyDeals)}
                  className={cn(
                    'relative h-11 px-4 rounded-xl border shadow-sm font-medium text-sm transition-all duration-300',
                    filterMyDeals
                      ? 'bg-gradient-to-r from-emerald-500 to-teal-500 border-emerald-400/50 text-white'
                      : 'bg-white border-slate-200 text-slate-700 hover:bg-emerald-50 hover:border-emerald-300 hover:text-emerald-700'
                  )}
                >
                  <User className="w-4 h-4 inline mr-1.5" />
                  My Deals
                </button>

                {board && board.staleAfterDays > 0 && (
                  <button
                    type="button"
                    onClick={() => setFilterStale(!filterStale)}
                    aria-pressed={filterStale}
                    title={`Open deals with no activity for ${board.staleAfterDays} days or more`}
                    className={cn(
                      'relative h-11 px-4 rounded-xl border shadow-sm font-medium text-sm transition-all duration-300',
                      filterStale
                        ? 'bg-gradient-to-r from-amber-500 to-orange-500 border-amber-400/50 text-white'
                        : 'bg-white border-slate-200 text-slate-700 hover:bg-amber-50 hover:border-amber-300 hover:text-amber-700'
                    )}
                  >
                    <Clock className="w-4 h-4 inline mr-1.5" />
                    Stale
                  </button>
                )}

                {view.layout === 'board' && (
                  <FlexFilterBar entity="deal" params={searchParams} onChange={(next) => setSearchParams(next, { replace: true })} />
                )}

                {/* The board's filters, order and tag and field conditions kept under a name. */}
                {view.layout === 'board' && <SavedViewsMenu list="deals" params={boardView} onChange={openBoardView} />}

                {view.layout === 'board' && (
                  <Select value={boardSort} onValueChange={(v) => setBoardSort(v as NonNullable<DealBoardQuery['sort']>)}>
                    <SelectTrigger aria-label="Order cards by" className="relative h-11 w-full sm:w-[170px] rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent className="rounded-xl border border-slate-200 shadow-xl">
                      <SelectItem value="close" className="rounded-lg">Closing soonest</SelectItem>
                      <SelectItem value="value" className="rounded-lg">Largest first</SelectItem>
                      <SelectItem value="created" className="rounded-lg">Newest first</SelectItem>
                      <SelectItem value="name" className="rounded-lg">By name</SelectItem>
                    </SelectContent>
                  </Select>
                )}

                {/* HP-10: Assignee filter */}
                {orgMembers.length > 0 && (
                  <Select value={filterAssignee} onValueChange={setFilterAssignee}>
                    <SelectTrigger aria-label="Filter by assignee" className="relative h-11 w-full sm:w-[180px] rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-fuchsia-50 hover:border-fuchsia-300 focus:border-fuchsia-300 focus:ring-2 focus:ring-fuchsia-200/60 transition-all duration-300">
                      <div className="flex items-center gap-2">
                        <div className="w-7 h-7 rounded-lg bg-fuchsia-100 flex items-center justify-center">
                          <User className="w-3.5 h-3.5 text-fuchsia-600" />
                        </div>
                        <SelectValue placeholder="Assignee" />
                      </div>
                    </SelectTrigger>
                    <SelectContent className="rounded-xl border border-slate-200 shadow-xl">
                      <SelectItem value="all" className="rounded-lg">All assignees</SelectItem>
                      <SelectItem value="unassigned" className="rounded-lg">Unassigned</SelectItem>
                      {orgMembers.map((m) => (
                        <SelectItem key={m.userId} value={m.userId} className="rounded-lg">{m.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>

              {/* HP-10: Value range row */}
              <div className="relative flex flex-wrap gap-3 mt-3">
                <div className="flex items-center gap-2">
                  <span className="text-xs text-slate-500 font-medium whitespace-nowrap">Value range:</span>
                  <input
                    type="number"
                    placeholder="Min"
                    value={filterValueMin}
                    onChange={(e) => setFilterValueMin(e.target.value)}
                    className="w-24 h-9 rounded-lg border border-slate-200 bg-slate-50/80 text-slate-900 text-xs px-3 focus:outline-none focus:border-violet-300 focus:ring-4 focus:ring-violet-200/50 focus:bg-white placeholder:text-slate-400"
                  />
                  <span className="text-xs text-slate-500">—</span>
                  <input
                    type="number"
                    placeholder="Max"
                    aria-label="Maximum deal value"
                    value={filterValueMax}
                    onChange={(e) => setFilterValueMax(e.target.value)}
                    className="w-24 h-9 rounded-lg border border-slate-200 bg-slate-50/80 text-slate-900 text-xs px-3 focus:outline-none focus:border-violet-300 focus:ring-4 focus:ring-violet-200/50 focus:bg-white placeholder:text-slate-400"
                  />
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs text-slate-500 font-medium whitespace-nowrap">Close date:</span>
                  <input
                    type="date"
                    aria-label="Close date from"
                    value={filterCloseDateFrom}
                    onChange={(e) => setFilterCloseDateFrom(e.target.value)}
                    className="h-9 rounded-lg border border-slate-200 bg-slate-50/80 text-slate-900 text-xs px-3 focus:outline-none focus:border-violet-300 focus:ring-4 focus:ring-violet-200/50 focus:bg-white"
                  />
                  <span className="text-xs text-slate-500">—</span>
                  <input
                    type="date"
                    aria-label="Close date to"
                    value={filterCloseDateTo}
                    onChange={(e) => setFilterCloseDateTo(e.target.value)}
                    className="h-9 rounded-lg border border-slate-200 bg-slate-50/80 text-slate-900 text-xs px-3 focus:outline-none focus:border-violet-300 focus:ring-4 focus:ring-violet-200/50 focus:bg-white"
                  />
                </div>
              </div>

              {/* Active Filter Pills */}
              {filtersActive && (
                <div className="relative mt-4 flex flex-wrap items-center gap-2">
                  <span className="text-xs text-slate-500 font-medium">Active filters:</span>
                  {searchQuery && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-slate-100 text-slate-700 text-xs font-medium border border-slate-200">
                      <Search className="w-3 h-3" />
                      &quot;{searchQuery}&quot;
                      <button type="button" onClick={() => setSearchQuery('')} aria-label="Clear search" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-violet-100 hover:text-violet-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {filterStage !== 'all' && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-emerald-50 text-emerald-700 text-xs font-medium border border-emerald-200">
                      <span 
                        className="w-2 h-2 rounded-full bg-emerald-500"
                      />
                      {filterStage}
                      <button type="button" onClick={() => setFilterStage('all')} aria-label={`Remove ${filterStage} filter`} className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-emerald-100 hover:text-emerald-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-400">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {filterMyDeals && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-blue-50 text-blue-700 text-xs font-medium border border-blue-200">
                      <User className="w-3 h-3" />
                      My Deals
                      <button type="button" onClick={() => setFilterMyDeals(false)} aria-label="Remove “my deals” filter" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-blue-100 hover:text-blue-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-400">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {filterAssignee !== 'all' && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-purple-50 text-purple-700 text-xs font-medium border border-purple-200">
                      <User className="w-3 h-3" />
                      {filterAssignee === 'unassigned' ? 'Unassigned' : orgMembers.find(m => m.userId === filterAssignee)?.name}
                      <button type="button" onClick={() => setFilterAssignee('all')} aria-label="Remove owner filter" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-purple-100 hover:text-purple-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-purple-400">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {(filterValueMin || filterValueMax) && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-amber-50 text-amber-700 text-xs font-medium border border-amber-200">
                      <DollarSign className="w-3 h-3" />
                      {filterValueMin || '0'} – {filterValueMax || '∞'}
                      <button type="button" onClick={() => { setFilterValueMin(''); setFilterValueMax(''); }} aria-label="Remove value filter" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-amber-100 hover:text-amber-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-400">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {filterStale && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-amber-50 text-amber-700 text-xs font-medium border border-amber-200">
                      <Clock className="w-3 h-3" />
                      Stale
                      <button type="button" onClick={() => setFilterStale(false)} aria-label="Remove stale filter" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-amber-100 hover:text-amber-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-400">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  {(filterCloseDateFrom || filterCloseDateTo) && (
                    <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-cyan-50 text-cyan-700 text-xs font-medium border border-cyan-200">
                      <Calendar className="w-3 h-3" />
                      {filterCloseDateFrom || 'any'} – {filterCloseDateTo || 'any'}
                      <button type="button" onClick={() => { setFilterCloseDateFrom(''); setFilterCloseDateTo(''); }} aria-label="Remove close date filter" className="-my-1 -mr-1.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-cyan-100 hover:text-cyan-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400">
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  )}
                  <span className="text-xs text-slate-500 ml-2">
                    {shownCount} of {view.layout === 'board' ? dealStats.total : pipelineDeals.length} deals
                  </span>
                </div>
              )}
            </motion.div>
          )}

        {!loading && boardResult.isError && board && (
          <LoadFailed stale what="The board" error={boardResult.error} onRetry={refresh} retrying={boardResult.isFetching} className="mb-4" />
        )}
        {loading ? (
          <KanbanSkeleton columns={5} />
        ) : boardResult.isError && !board ? (
          <LoadFailed what="Your deals" error={boardResult.error} onRetry={refresh} retrying={boardResult.isFetching} />
        ) : !hasDeals ? (
          /* Empty State - matching Leads page style */
          <div className="w-full">
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              {/* Gradient header */}
              <div className="relative overflow-hidden bg-gradient-to-br from-emerald-500 via-teal-500 to-cyan-500 px-8 py-10 text-center">
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
                <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
                <div className="relative">
                  <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg mb-4">
                    <Target className="w-8 h-8 text-white" />
                  </div>
                  <h2 className="text-2xl font-bold text-white mb-2">Build Your Sales Pipeline</h2>
                  <p className="text-white/80 max-w-md mx-auto">
                    Track deals from qualification to close. Visualize your pipeline and forecast revenue.
                  </p>
                </div>
              </div>
              
              {/* Content */}
              <div className="px-8 py-6">
                <div className="grid sm:grid-cols-3 gap-4 mb-6">
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center mx-auto mb-2">
                      <Target className="w-5 h-5 text-blue-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Qualify</p>
                    <p className="text-xs text-slate-500 mt-0.5">Identify promising opportunities</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-amber-100 flex items-center justify-center mx-auto mb-2">
                      <Handshake className="w-5 h-5 text-amber-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Negotiate</p>
                    <p className="text-xs text-slate-500 mt-0.5">Move deals through stages</p>
                  </div>
                  <div className="text-center p-4 rounded-xl bg-slate-50 border border-slate-100">
                    <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center mx-auto mb-2">
                      <Trophy className="w-5 h-5 text-emerald-600" />
                    </div>
                    <p className="text-sm font-medium text-slate-800">Close</p>
                    <p className="text-xs text-slate-500 mt-0.5">Win more deals, grow revenue</p>
                  </div>
                </div>
                
                {!isReadOnly && (
                  <div className="flex flex-col sm:flex-row gap-3 justify-center">
                    <Button onClick={openCreateDeal} className="gap-2 h-11 px-6 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 shadow-lg shadow-emerald-200/50">
                      <Plus className="w-4 h-4" />
                      Add Your First Deal
                    </Button>
                  </div>
                )}
              </div>
            </div>
          </div>
        ) : shownCount === 0 && filtersActive ? (
          /* No results state */
          <motion.div 
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="flex items-center gap-5 p-6 rounded-2xl bg-white border-2 border-dashed border-slate-200 shadow-lg shadow-slate-100"
          >
            <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center">
              <Search className="w-8 h-8 text-slate-500" />
            </div>
            <div className="flex-1">
              <p className="text-base font-semibold text-slate-700">No deals match your filters</p>
              <p className="text-sm text-slate-500 mt-1">Try adjusting your search or filter criteria to find what you're looking for</p>
            </div>
            <Button 
              variant="outline" 
              onClick={() => {
                setSearchQuery(''); setFilterStage('all'); setFilterMyDeals(false); setFilterAssignee('all');
                setFilterValueMin(''); setFilterValueMax(''); setFilterCloseDateFrom(''); setFilterCloseDateTo(''); setFilterStale(false);
              }}
              className="shrink-0 h-11 px-6 rounded-xl border-2 hover:bg-slate-50"
            >
              Clear all filters
            </Button>
          </motion.div>
        ) : (
          <>
            {/* Pipeline Visualization - Modern Funnel */}
            <motion.div
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.15, duration: 0.3 }}
              className="shrink-0 mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-lg shadow-slate-100"
            >
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center shadow-lg shadow-indigo-200/50">
                    <BarChart3 className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <h3 className="text-base font-semibold text-slate-800">Pipeline Distribution</h3>
                    <p className="text-xs text-slate-500">Deals by stage</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {funnelData.slice(0, 3).map((stage, i) => (
                    <div key={i} className="flex items-center gap-1.5 text-xs text-slate-500">
                      <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: stage.fill }} />
                      <span className="hidden sm:inline">{stage.name}</span>
                    </div>
                  ))}
                </div>
              </div>
              <div className="h-[140px] w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={funnelData} layout="vertical" margin={{ top: 0, right: 30, left: 90, bottom: 0 }}>
                    <XAxis type="number" hide />
                    <YAxis 
                      type="category" 
                      dataKey="name" 
                      width={85} 
                      tick={{ fontSize: 12, fill: '#64748b', fontWeight: 500 }}
                      axisLine={false}
                      tickLine={false}
                    />
                    <Tooltip
                      formatter={(value: number, _name: string) => [value, 'Deals']}
                      labelFormatter={(label) => label}
                      contentStyle={{ 
                        borderRadius: 12, 
                        border: 'none',
                        boxShadow: '0 10px 40px rgba(0,0,0,0.15)',
                        padding: '12px 16px'
                      }}
                      cursor={{ fill: 'rgba(0,0,0,0.05)', radius: 8 }}
                    />
                    <Bar dataKey="count" radius={[4, 8, 8, 4]} maxBarSize={28}>
                      {funnelData.map((entry, index) => (
                        <Cell key={index} fill={entry.fill} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </motion.div>

            {/* View Toggle - Floating Pills */}
            <div className="flex items-center justify-between mb-5 shrink-0">
              <ViewSwitcher view={view} columns={columnChoices(dealColumns)} />
              
              <div className="hidden sm:flex items-center gap-2 text-xs text-slate-500">
                <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-slate-100">
                  <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
                  <span>{shownCount} deals</span>
                </div>
              </div>
            </div>

            <ViewTransition layout={view.layout} className="flex min-h-0 flex-1 flex-col">
            {view.layout === 'board' ? (
              <DndProvider backend={HTML5Backend}>
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="flex-1 min-h-0 flex gap-5 overflow-x-auto overflow-y-hidden pb-6 -mx-2 px-2"
                >
                  {boardColumns.map((column, index) => (
                    // The column rises in; the deal cards in it are drag sources, so they keep their own motion.
                    <StaggerItem key={column.key} index={index}>
                      <DroppableStageColumn
                        stageId={column.key}
                        stageName={column.name}
                        deals={column.deals}
                        count={column.count}
                        totals={column.totals}
                        weighted={column.weighted}
                        probability={column.probability}
                        stale={column.stale}
                        hasMore={column.hasMore}
                        loadingMore={loadingMore === column.key}
                        onLoadMore={() => loadMore(column)}
                        // Nothing is moved to "No stage": it holds deals in none of the stages.
                        acceptsDrops={column.key !== 'unset'}
                        contacts={contacts}
                        stageList={stageList}
                        onMoveStage={handleMoveStage}
                        onEdit={openEditDeal}
                        onDelete={setDeleteConfirmDeal}
                        onOpenDetail={openDealDetail}
                        taskCountsByDeal={taskCountsByDeal}
                        onAddTask={(dealId) => setAddTaskDealId(dealId)}
                        readOnly={isReadOnly}
                      />
                    </StaggerItem>
                  ))}
                </motion.div>
              </DndProvider>
            ) : view.layout === 'table' ? (
              <div className="min-h-0 flex-1 overflow-auto">
                <RecordTable
                  label="Deals"
                  items={filteredDeals}
                  view={view}
                  getKey={(d) => d.id}
                  onOpen={openDealDetail}
                  columns={dealColumns}
                  actions={dealActions}
                />
              </div>
            ) : view.layout === 'list' ? (
              <div className="min-h-0 flex-1 overflow-auto">
                <RecordList
                  label="Deals"
                  items={filteredDeals}
                  view={view}
                  getKey={(d) => d.id}
                  onOpen={openDealDetail}
                  leading={(d) => <InitialsAvatar name={d.name} />}
                  title={(d) => d.name}
                  subtitle={(d) => [d.companyName, contactOf(d)].filter(Boolean).join(' · ') || dealStageName(d)}
                  columns={dealColumns}
                  actions={dealActions}
                />
              </div>
            ) : (
              <DndProvider backend={HTML5Backend}>
                <div className={cn('grid min-h-0 flex-1 content-start overflow-auto pb-6 sm:grid-cols-2 lg:grid-cols-3', gridDensity(view, 'gap-4', 'gap-3 xl:grid-cols-4'))}>
                  {filteredDeals.map((deal, index) => (
                    <StaggerItem key={deal.id} index={index}>
                      <DealCard
                        deal={deal}
                        contactName={contactOf(deal)}
                        stageId={deal.dealStageId ?? dealStageName(deal)}
                        stageName={dealStageName(deal)}
                        onMoveStage={handleMoveStage}
                        onEdit={() => openEditDeal(deal)}
                        onDelete={() => setDeleteConfirmDeal(deal)}
                        onOpenDetail={() => openDealDetail(deal)}
                        stageList={stageList}
                        taskCount={taskCountsByDeal[deal.id]}
                        onAddTask={(dealId) => setAddTaskDealId(dealId)}
                        readOnly={isReadOnly}
                      />
                    </StaggerItem>
                  ))}
                </div>
              </DndProvider>
            )}
            </ViewTransition>
          </>
        )}
        </main>
      </PageTransition>

      {/* Deal detail sheet */}
      <Sheet open={!!detailDeal} onOpenChange={(open) => !open && setDetailDeal(null)}>
        <SheetContent side="right" className="w-full sm:max-w-md">
          {detailDeal && (
            <>
              <SheetHeader>
                <SheetTitle className="text-xl">{detailDeal.name}</SheetTitle>
              </SheetHeader>
              <div className="flex flex-col gap-4 py-4 overflow-auto flex-1">
                {/* Deal Info Section */}
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Value</p>
                    <p className="text-lg font-semibold text-emerald-700">{money.formatDeal(detailDeal)}</p>
                  </div>
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Stage</p>
                    <span className={cn(
                      'inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-medium',
                      STAGE_COLORS_MAP[dealStageName(detailDeal)]?.bg,
                      STAGE_COLORS_MAP[dealStageName(detailDeal)]?.accent
                    )}>
                      {dealStageName(detailDeal)}
                    </span>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  {detailContact && (
                    <div>
                      <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Contact</p>
                      <p className="text-slate-900 text-sm">{detailContact.name}</p>
                      {detailContact.email && <p className="text-xs text-slate-500">{detailContact.email}</p>}
                    </div>
                  )}
                  {detailCompany && (
                    <div>
                      <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Company</p>
                      <p className="text-slate-900 text-sm">{detailCompany.name}</p>
                    </div>
                  )}
                </div>
                {detailDeal.expectedCloseDateUtc && (
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Expected close</p>
                    <p className="text-slate-900 text-sm">{formatCloseDate(detailDeal.expectedCloseDateUtc)}</p>
                    {getDaysUntilClose(detailDeal.expectedCloseDateUtc, money.timeZone) !== null && (
                      <UrgencyBadge days={getDaysUntilClose(detailDeal.expectedCloseDateUtc, money.timeZone)!} />
                    )}
                  </div>
                )}

                {/* HP-1: Description */}
                {detailDeal.description && (
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Description</p>
                    <p className="text-slate-700 text-sm whitespace-pre-wrap">{detailDeal.description}</p>
                  </div>
                )}
                {/* HP-1: Probability & HP-7: Assignee */}
                <div className="grid grid-cols-2 gap-4">
                  {detailDeal.probability != null && (
                    <div>
                      <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Win Probability</p>
                      <p className="text-slate-900 text-sm font-semibold">{detailDeal.probability}%</p>
                    </div>
                  )}
                  {detailDeal.assigneeName && (
                    <div>
                      <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Assignee</p>
                      <p className="text-slate-900 text-sm">{detailDeal.assigneeName}</p>
                    </div>
                  )}
                </div>
                {/* HP-8: Closed info */}
                {detailDeal.isWon !== undefined && detailDeal.isWon !== null && (
                  <div className={cn(
                    'p-3 rounded-lg border',
                    detailDeal.isWon ? 'bg-emerald-50 border-emerald-200' : 'bg-slate-50 border-slate-200'
                  )}>
                    <p className={cn('text-sm font-semibold', detailDeal.isWon ? 'text-emerald-700' : 'text-slate-600')}>
                      {detailDeal.isWon ? 'Won' : 'Lost'}
                      {detailDeal.closedAtUtc && (
                        <span className="font-normal text-xs ml-2">
                          on {new Date(detailDeal.closedAtUtc).toLocaleDateString(currentDateLocale(), { month: 'short', day: 'numeric', year: 'numeric' })}
                        </span>
                      )}
                    </p>
                    {detailDeal.closedReason && (
                      <p className="text-xs text-slate-600 mt-1">{detailDeal.closedReason}</p>
                    )}
                  </div>
                )}

                {/* Quick Actions */}
                <div className="flex gap-2 pt-2 border-t border-slate-200">
                  <Button
                    variant="outline"
                    size="sm"
                    className="flex-1 gap-2"
                    onClick={() => {
                      openEditDeal(detailDeal);
                      setDetailDeal(null);
                    }}
                  >
                    <Pencil className="w-3.5 h-3.5" />
                    Edit
                  </Button>
                  {/* HP-8: Close deal buttons */}
                  {detailDeal.isWon == null && (
                    <>
                      <Button
                        variant="outline"
                        size="sm"
                        className="text-emerald-700 hover:text-emerald-800 hover:bg-emerald-50"
                        onClick={() => { setCloseDeal(detailDeal); setCloseAsWon(true); }}
                      >
                        <Trophy className="w-3.5 h-3.5 mr-1" />
                        Won
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        className="text-slate-600 hover:text-slate-700 hover:bg-slate-100"
                        onClick={() => { setCloseDeal(detailDeal); setCloseAsWon(false); }}
                      >
                        <X className="w-3.5 h-3.5 mr-1" />
                        Lost
                      </Button>
                    </>
                  )}
                  <WriteOnly permission={[PERMISSION.EditRecords, PERMISSION.DeleteRecords]} all>
                    <Button
                      variant="outline"
                      size="sm"
                      className="text-red-600 hover:text-red-700 hover:bg-red-50"
                      onClick={() => {
                        setDeleteConfirmDeal(detailDeal);
                        setDetailDeal(null);
                      }}
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </Button>
                  </WriteOnly>
                </div>

                {/* Activities Section */}
                <div className="pt-4 border-t border-slate-200">
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2">
                      <ActivityIcon className="w-4 h-4 text-slate-500" />
                      <h3 className="text-sm font-semibold text-slate-700">Activity Log</h3>
                      <span className="text-xs bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded-full">
                        {dealActivities.length}
                      </span>
                    </div>
                  </div>

                  {/* Log Activity Form */}
                  <form onSubmit={handleLogActivity} className="mb-4 p-3 bg-slate-50 rounded-lg border border-slate-200">
                    <div className="flex gap-2 mb-2">
                      <Select
                        value={activityForm.type}
                        onValueChange={(v) => setActivityForm((f) => ({ ...f, type: v }))}
                      >
                        <SelectTrigger aria-label="Activity type" className="w-28 h-8 text-xs bg-white">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="note">Note</SelectItem>
                          <SelectItem value="call">Call</SelectItem>
                          <SelectItem value="email">Email</SelectItem>
                          <SelectItem value="meeting">Meeting</SelectItem>
                          <SelectItem value="task">Task</SelectItem>
                        </SelectContent>
                      </Select>
                      <Input
                        placeholder="Subject..."
                        value={activityForm.subject}
                        onChange={(e) => setActivityForm((f) => ({ ...f, subject: e.target.value }))}
                        className="flex-1 h-8 text-sm bg-white"
                      />
                    </div>
                    <textarea
                      placeholder="Details (optional)..."
                      value={activityForm.body}
                      onChange={(e) => setActivityForm((f) => ({ ...f, body: e.target.value }))}
                      className="w-full px-3 py-2 text-sm border border-slate-200 rounded-md bg-white resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-400"
                      rows={2}
                    />
                    <ActivityFactsFields
                      className="mt-2 sm:grid-cols-2"
                      type={activityForm.type}
                      value={activityForm.facts ?? EMPTY_FACTS}
                      onChange={(facts) => setActivityForm((f) => ({ ...f, facts }))}
                    />
                    <div className="flex justify-end mt-2">
                      <Button
                        type="submit"
                        size="sm"
                        disabled={savingActivity || !activityForm.subject.trim() || !!factsProblem(activityForm.type, activityForm.facts)}
                        className="h-7 text-xs bg-emerald-600 hover:bg-emerald-700 text-white"
                      >
                        {savingActivity ? (
                          <span className="flex items-center gap-1">
                            <Clock className="w-3 h-3 animate-spin" />
                            Saving...
                          </span>
                        ) : (
                          <span className="flex items-center gap-1">
                            <Plus className="w-3 h-3" />
                            Log Activity
                          </span>
                        )}
                      </Button>
                    </div>
                  </form>

                  {/* Activities Timeline */}
                  <div className="space-y-2 max-h-[300px] overflow-y-auto">
                    {activitiesLoading ? (
                      <div className="py-6 text-center">
                        <Spinner size={22} className="mx-auto mb-2" />
                        <p className="text-xs text-slate-500">Loading activities...</p>
                      </div>
                    ) : dealActivities.length === 0 ? (
                      <div className="py-6 text-center">
                        <ActivityIcon className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                        <p className="text-sm text-slate-500">No activities yet</p>
                        <p className="text-xs text-slate-500 mt-1">Log your first activity above</p>
                      </div>
                    ) : (
                      dealActivities.map((activity) => (
                        <div
                          key={activity.id}
                          className="flex gap-3 p-3 bg-white rounded-lg border border-slate-100 hover:border-slate-200 transition-colors"
                        >
                          <div className={cn(
                            'w-8 h-8 rounded-lg flex items-center justify-center shrink-0',
                            activity.type === 'call' && 'bg-emerald-100 text-emerald-700',
                            activity.type === 'email' && 'bg-amber-100 text-amber-700',
                            activity.type === 'meeting' && 'bg-blue-100 text-blue-600',
                            activity.type === 'note' && 'bg-slate-100 text-slate-600',
                            activity.type === 'video' && 'bg-purple-100 text-purple-600',
                            activity.type === 'demo' && 'bg-rose-100 text-rose-600',
                            activity.type === 'task' && 'bg-cyan-100 text-cyan-600',
                            activity.type === 'follow_up' && 'bg-orange-100 text-orange-700',
                            activity.type === 'deadline' && 'bg-red-100 text-red-600',
                            !['call', 'email', 'meeting', 'note', 'video', 'demo', 'task', 'follow_up', 'deadline'].includes(activity.type) && 'bg-slate-100 text-slate-600'
                          )}>
                            {activity.type === 'call' && <Phone className="w-4 h-4" />}
                            {activity.type === 'email' && <Mail className="w-4 h-4" />}
                            {activity.type === 'meeting' && <Users className="w-4 h-4" />}
                            {activity.type === 'note' && <FileText className="w-4 h-4" />}
                            {activity.type === 'video' && <Video className="w-4 h-4" />}
                            {activity.type === 'demo' && <Presentation className="w-4 h-4" />}
                            {activity.type === 'task' && <CheckCircle2 className="w-4 h-4" />}
                            {activity.type === 'follow_up' && <CalendarClock className="w-4 h-4" />}
                            {activity.type === 'deadline' && <Clock className="w-4 h-4" />}
                            {!['call', 'email', 'meeting', 'note', 'video', 'demo', 'task', 'follow_up', 'deadline'].includes(activity.type) && <ActivityIcon className="w-4 h-4" />}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-start justify-between gap-2">
                              <div>
                                <p className="text-sm font-medium text-slate-800 truncate">{activity.subject || activity.type}</p>
                                <p className="text-xs text-slate-500">
                                  {activity.type}{activityFactsLine(activity) && <> • {activityFactsLine(activity)}</>} • {new Date(activityTime(activity)).toLocaleDateString(currentDateLocale(), { month: 'short', day: 'numeric', year: 'numeric' })}
                                </p>
                              </div>
                            </div>
                            {activity.body && (
                              <p className="text-xs text-slate-600 mt-1 line-clamp-2">{activity.body}</p>
                            )}
                            {/* HP-6: Show participants when present */}
                            {activity.participants && (
                              <div className="flex items-center gap-1.5 mt-1">
                                <User className="w-3 h-3 text-violet-500" />
                                <span className="text-xs text-violet-600">with {activity.participants}</span>
                              </div>
                            )}
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-[520px] p-0 gap-0 overflow-hidden max-h-[90vh] flex flex-col">
          {/* Gradient Header */}
          <div className="relative overflow-hidden shrink-0">
            <div className="absolute inset-0 bg-gradient-to-br from-emerald-500 via-teal-500 to-emerald-400" />
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
            <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
            
            <div className="relative px-6 py-5">
              <div className="flex items-center gap-4">
                <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                  <Target className="w-6 h-6 text-white" />
                </div>
                <div>
                  <DialogTitle className="text-xl font-bold text-white tracking-tight">
                    Create New Deal
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    Add a new sales opportunity to your pipeline
                  </p>
                </div>
              </div>
            </div>
          </div>

          <form onSubmit={handleCreate} className="flex-1 overflow-y-auto p-6 space-y-4">
            <div className="group">
              <Label htmlFor="deal-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-700">
                  <Briefcase className="w-3.5 h-3.5" />
                </div>
                Deal Name <span className="text-emerald-500">*</span>
              </Label>
              <Input
                id="deal-name"
                value={createForm.name}
                onChange={(e) => setCreateForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                required
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="group">
                <Label htmlFor="deal-value" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-700">
                    <DollarSign className="w-3.5 h-3.5" />
                  </div>
                  Value <span className="text-emerald-500">*</span>
                </Label>
                <Input
                  id="deal-value"
                  inputMode="decimal"
                  value={createForm.value}
                  onChange={(e) => setCreateForm((f) => ({ ...f, value: e.target.value }))}
                  placeholder="e.g. 50'000"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                  required
                />
              </div>
              <div className="group">
                <Label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-slate-100 text-slate-600">
                    <span className="text-xs font-bold">¤</span>
                  </div>
                  Currency
                </Label>
                <Select
                  value={createForm.currency || money.currency}
                  onValueChange={(v) => setCreateForm((f) => ({ ...f, currency: v }))}
                >
                  <SelectTrigger aria-label="Currency" className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all">
                    <SelectValue placeholder="Select currency" />
                  </SelectTrigger>
                  <SelectContent>
                    {currencyOptions(createForm.currency || money.currency).map((c) => (
                      <SelectItem key={c.code} value={c.code}>{c.code} - {c.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {pipelines.length > 0 && (
              <div>
                <Label>Pipeline</Label>
                <Select
                  value={createForm.pipelineId || 'none'}
                  onValueChange={(v) => {
                    const pid = v === 'none' ? '' : v;
                    const firstStage = getStageList(pipelines, pid)[0];
                    setCreateForm((f) => ({ ...f, pipelineId: pid, stageId: firstStage?.id ?? f.stageId }));
                  }}
                >
                  <SelectTrigger aria-label="Pipeline" className="mt-1">
                    <SelectValue placeholder="Select pipeline" />
                  </SelectTrigger>
                  <SelectContent>
                    {pipelines.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div>
              <Label>Stage</Label>
              <Select
                value={createForm.stageId || createStageList[0]?.id}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, stageId: v }))}
              >
                <SelectTrigger aria-label="Stage" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {createStageList.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="deal-close">Expected close date</Label>
              <div className="flex gap-2 mt-1">
                <Input
                  id="deal-close"
                  type="date"
                  value={createForm.expectedCloseDate}
                  onChange={(e) => setCreateForm((f) => ({ ...f, expectedCloseDate: e.target.value }))}
                />
                <div className="flex gap-1">
                  {[7, 14, 30].map((d) => (
                    <Button key={d} type="button" variant="outline" size="sm" onClick={() => setExpectedCloseInDays(d)}>
                      +{d}d
                    </Button>
                  ))}
                </div>
              </div>
            </div>
            <div>
              <Label>Company</Label>
              <Select
                value={createForm.companyId || 'none'}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, companyId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger aria-label="Company" className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {companies.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Contact</Label>
              <Select
                value={createForm.contactId || 'none'}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, contactId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger aria-label="Contact" className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {contacts.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {orgMembers.length > 0 && (
              <div>
                <Label>Assignee</Label>
                <Select
                  value={createForm.assigneeId || 'none'}
                  onValueChange={(v) => setCreateForm((f) => ({ ...f, assigneeId: v === 'none' ? '' : v }))}
                >
                  <SelectTrigger aria-label="Assignee" className="mt-1">
                    <SelectValue placeholder="Optional" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— None —</SelectItem>
                    {orgMembers.map((m) => (
                      <SelectItem key={m.userId} value={m.userId}>
                        {m.name} {m.email ? `(${m.email})` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {/* HP-1: Description & Probability */}
            <div>
              <Label htmlFor="deal-description">Description</Label>
              <textarea
                id="deal-description"
                value={createForm.description}
                onChange={(e) => setCreateForm((f) => ({ ...f, description: e.target.value }))}
                placeholder="Deal notes, context, or details..."
                className="mt-1 w-full px-3 py-2 text-sm border border-slate-200 rounded-lg bg-slate-50/50 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-400"
                rows={3}
              />
            </div>
            <div>
              <Label htmlFor="deal-probability">Win Probability (%)</Label>
              <Input
                id="deal-probability"
                type="number"
                min="0"
                max="100"
                value={createForm.probability}
                onChange={(e) => setCreateForm((f) => ({ ...f, probability: e.target.value }))}
                placeholder="e.g. 75"
                className="mt-1"
              />
            </div>
            <DialogFooter className="pt-4 border-t border-slate-100">
              <Button type="button" variant="outline" onClick={() => setCreateOpen(false)} className="h-11 px-6">
                Cancel
              </Button>
              <Button 
                type="submit" 
                disabled={saving} 
                className="h-11 px-6 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 shadow-lg shadow-emerald-200/50"
              >
                {saving ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                    Creating...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    Create Deal
                    <Sparkles className="w-4 h-4" />
                  </span>
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editDeal} onOpenChange={(open) => !open && setEditDeal(null)}>
        <DialogContent className="sm:max-w-[520px] p-0 gap-0 overflow-hidden max-h-[90vh] flex flex-col">
          {/* Gradient Header */}
          <div className="relative overflow-hidden shrink-0">
            <div className="absolute inset-0 bg-gradient-to-br from-emerald-500 via-teal-500 to-emerald-400" />
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
            <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
            
            <div className="relative px-6 py-5">
              <div className="flex items-center gap-4">
                <div className="flex items-center justify-center w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm border border-white/30 shadow-lg">
                  <Pencil className="w-6 h-6 text-white" />
                </div>
                <div>
                  <DialogTitle className="text-xl font-bold text-white tracking-tight">
                    Edit Deal
                  </DialogTitle>
                  <p className="text-white/80 text-sm mt-0.5">
                    Update deal information
                  </p>
                </div>
              </div>
            </div>
          </div>

          <form onSubmit={handleSaveEdit} className="flex-1 overflow-y-auto p-6 space-y-4">
            <div className="group">
              <Label htmlFor="edit-deal-name" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                <div className="flex items-center justify-center w-6 h-6 rounded-md bg-emerald-100 text-emerald-700">
                  <Briefcase className="w-3.5 h-3.5" />
                </div>
                Deal Name <span className="text-emerald-500">*</span>
              </Label>
              <Input
                id="edit-deal-name"
                value={editForm.name}
                onChange={(e) => setEditForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                required
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="group">
                <Label htmlFor="edit-deal-value" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-amber-100 text-amber-700">
                    <DollarSign className="w-3.5 h-3.5" />
                  </div>
                  Value <span className="text-emerald-500">*</span>
                </Label>
                <Input
                  id="edit-deal-value"
                  inputMode="decimal"
                  value={editForm.value}
                  onChange={(e) => setEditForm((f) => ({ ...f, value: e.target.value }))}
                  placeholder="e.g. 50'000"
                  className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all"
                  required
                />
              </div>
              <div className="group">
                <Label htmlFor="edit-deal-currency" className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
                  <div className="flex items-center justify-center w-6 h-6 rounded-md bg-slate-100 text-slate-600">
                    <span className="text-xs font-bold">¤</span>
                  </div>
                  Currency
                </Label>
                {/* What the deal is in. It said CHF for a deal with none, while the rest of the app said USD. */}
                <Select
                  value={editForm.currency || money.currency}
                  onValueChange={(v) => setEditForm((f) => ({ ...f, currency: v }))}
                >
                  <SelectTrigger aria-label="Currency" className="h-11 bg-slate-50/50 border-slate-200 focus:bg-white focus:border-emerald-300 focus:ring-emerald-100 transition-all">
                    <SelectValue placeholder="Select currency" />
                  </SelectTrigger>
                  <SelectContent>
                    {currencyOptions(editForm.currency || money.currency).map((c) => (
                      <SelectItem key={c.code} value={c.code}>{c.code} - {c.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {pipelines.length > 0 && (
              <div>
                <Label>Pipeline</Label>
                <Select
                  value={editForm.pipelineId || 'none'}
                  onValueChange={(v) => {
                    const pid = v === 'none' ? '' : v;
                    const firstStage = getStageList(pipelines, pid)[0];
                    setEditForm((f) => ({ ...f, pipelineId: pid, stageId: firstStage?.id ?? f.stageId }));
                  }}
                >
                  <SelectTrigger aria-label="Pipeline" className="mt-1">
                    <SelectValue placeholder="Optional" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— None —</SelectItem>
                    {pipelines.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div>
              <Label>Stage</Label>
              <Select
                value={editForm.stageId || editStageList[0]?.id}
                onValueChange={(v) => setEditForm((f) => ({ ...f, stageId: v }))}
              >
                <SelectTrigger aria-label="Stage" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {editStageList.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="edit-deal-close">Expected close date</Label>
              <Input
                id="edit-deal-close"
                type="date"
                value={editForm.expectedCloseDate}
                onChange={(e) => setEditForm((f) => ({ ...f, expectedCloseDate: e.target.value }))}
                className="mt-1"
              />
            </div>
            <div>
              <Label>Company</Label>
              <Select
                value={editForm.companyId || 'none'}
                onValueChange={whenChosen((v) => setEditForm((f) => ({ ...f, companyId: v === 'none' ? '' : v })))}
              >
                <SelectTrigger aria-label="Company" className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {companies.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {orgMembers.length > 0 && (
              <div>
                <Label>Assignee</Label>
                <Select
                  value={editForm.assigneeId || 'none'}
                  onValueChange={whenChosen((v) => setEditForm((f) => ({ ...f, assigneeId: v === 'none' ? '' : v })))}
                >
                  <SelectTrigger aria-label="Assignee" className="mt-1">
                    <SelectValue placeholder="Optional" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— None —</SelectItem>
                    {orgMembers.map((m) => (
                      <SelectItem key={m.userId} value={m.userId}>
                        {m.name} {m.email ? `(${m.email})` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {/* HP-1: Description & Probability */}
            <div>
              <Label htmlFor="edit-deal-description">Description</Label>
              <textarea
                id="edit-deal-description"
                value={editForm.description}
                onChange={(e) => setEditForm((f) => ({ ...f, description: e.target.value }))}
                placeholder="Deal notes, context, or details..."
                className="mt-1 w-full px-3 py-2 text-sm border border-slate-200 rounded-lg bg-slate-50/50 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-400"
                rows={3}
              />
            </div>
            <div>
              <Label htmlFor="edit-deal-probability">Win Probability (%)</Label>
              <Input
                id="edit-deal-probability"
                type="number"
                min="0"
                max="100"
                value={editForm.probability}
                onChange={(e) => setEditForm((f) => ({ ...f, probability: e.target.value }))}
                placeholder="e.g. 75"
                className="mt-1"
              />
            </div>
            <DialogFooter className="pt-4 border-t border-slate-100">
              <Button type="button" variant="outline" onClick={() => setEditDeal(null)} className="h-11 px-6">
                Cancel
              </Button>
              <Button 
                type="submit" 
                disabled={savingEdit} 
                className="h-11 px-6 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 shadow-lg shadow-emerald-200/50"
              >
                {savingEdit ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                    Saving...
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    Save Changes
                    <CheckCircle2 className="w-4 h-4" />
                  </span>
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!deleteConfirmDeal} onOpenChange={(open) => !open && setDeleteConfirmDeal(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Delete deal</DialogTitle>
          </DialogHeader>
          <p className="text-slate-600">
            Are you sure you want to delete <strong>{deleteConfirmDeal?.name}</strong>? This cannot be undone.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteConfirmDeal(null)} disabled={deleting}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleDeleteConfirm} disabled={deleting}>
              {deleting ? 'Deleting...' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* HP-8: Close Deal Dialog */}
      <Dialog open={!!closeDeal} onOpenChange={(open) => { if (!open) { setCloseDeal(null); setCloseReason(''); } }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{closeAsWon ? 'Mark Deal as Won' : 'Mark Deal as Lost'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <p className="text-slate-600">
              Close <strong>{closeDeal?.name}</strong> as {closeAsWon ? 'Won' : 'Lost'}.
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                variant={closeAsWon ? 'default' : 'outline'}
                className={closeAsWon ? 'bg-emerald-600 hover:bg-emerald-700 text-white' : ''}
                onClick={() => setCloseAsWon(true)}
              >
                <Trophy className="w-4 h-4 mr-1" /> Won
              </Button>
              <Button
                type="button"
                variant={!closeAsWon ? 'default' : 'outline'}
                className={!closeAsWon ? 'bg-slate-600 hover:bg-slate-700 text-white' : ''}
                onClick={() => setCloseAsWon(false)}
              >
                <X className="w-4 h-4 mr-1" /> Lost
              </Button>
            </div>
            <div>
              <Label htmlFor="close-reason">Reason (optional)</Label>
              <textarea
                id="close-reason"
                value={closeReason}
                onChange={(e) => setCloseReason(e.target.value)}
                placeholder={closeAsWon ? 'Why did we win this deal?' : 'Why was this deal lost?'}
                className="mt-1 w-full px-3 py-2 text-sm border border-slate-200 rounded-lg bg-white resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-400"
                rows={3}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setCloseDeal(null); setCloseReason(''); }} disabled={savingClose}>
              Cancel
            </Button>
            <Button
              onClick={handleCloseDeal}
              disabled={savingClose}
              className={closeAsWon ? 'bg-emerald-600 hover:bg-emerald-700' : 'bg-slate-600 hover:bg-slate-700'}
            >
              {savingClose ? 'Saving...' : closeAsWon ? 'Mark as Won' : 'Mark as Lost'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      {/* HP-8: Add Task to Deal Dialog */}
      <Dialog open={!!addTaskDealId} onOpenChange={(open) => { if (!open) { setAddTaskDealId(null); setAddTaskTitle(''); } }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <ListTodo className="w-5 h-5 text-orange-500" />
              Add Task to Deal
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label htmlFor="task-title">Task Title</Label>
              <Input
                id="task-title"
                value={addTaskTitle}
                onChange={(e) => setAddTaskTitle(e.target.value)}
                placeholder="e.g., Follow up with client"
                className="mt-1"
                onKeyDown={(e) => { if (e.key === 'Enter' && addTaskTitle.trim()) handleAddTaskToDeal(); }}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setAddTaskDealId(null); setAddTaskTitle(''); }}>Cancel</Button>
            <Button onClick={handleAddTaskToDeal} disabled={savingTask || !addTaskTitle.trim()}>
              {savingTask ? 'Adding...' : 'Add Task'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
