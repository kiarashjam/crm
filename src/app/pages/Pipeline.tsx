import { useState, useEffect } from 'react';
import { DndProvider, useDrag, useDrop } from 'react-dnd';
import { HTML5Backend } from 'react-dnd-html5-backend';
import {
  Briefcase,
  ChevronDown,
  Plus,
  Trash2,
  Pencil,
  Target,
  Handshake,
  Trophy,
  XCircle,
  DollarSign,
  LayoutGrid,
  List,
  GripVertical,
  Calendar,
  User,
  AlertCircle,
  Sparkles,
} from 'lucide-react';
import { toast } from 'sonner';
import { motion, AnimatePresence } from 'motion/react';
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
import LoadingSpinner from '@/app/components/LoadingSpinner';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getDeals, updateDeal, createDeal, deleteDeal, getCompanies, getContacts, messages } from '@/app/api';
import type { Deal, Company, Contact } from '@/app/api/types';
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
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/app/components/ui/tabs';
import { Card, CardContent, CardHeader } from '@/app/components/ui/card';
import { cn } from '@/app/components/ui/utils';

const STAGES = ['Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'] as const;

const STAGE_COLORS: Record<string, { bar: string; accent: string; bg: string; border: string }> = {
  Qualification: { bar: '#3b82f6', accent: 'text-blue-600', bg: 'bg-blue-500/10', border: 'border-l-blue-500' },
  Proposal: { bar: '#f59e0b', accent: 'text-amber-600', bg: 'bg-amber-500/10', border: 'border-l-amber-500' },
  Negotiation: { bar: '#8b5cf6', accent: 'text-violet-600', bg: 'bg-violet-500/10', border: 'border-l-violet-500' },
  'Closed Won': { bar: '#10b981', accent: 'text-emerald-600', bg: 'bg-emerald-500/10', border: 'border-l-emerald-500' },
  'Closed Lost': { bar: '#64748b', accent: 'text-slate-500', bg: 'bg-slate-500/10', border: 'border-l-slate-400' },
};

const STAGE_ICONS: Record<string, typeof Briefcase> = {
  Qualification: Target,
  Proposal: Briefcase,
  Negotiation: Handshake,
  'Closed Won': Trophy,
  'Closed Lost': XCircle,
};

function formatLastActivity(iso: string | undefined): string {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return '';
  }
}

function formatValueSum(values: string[]): string {
  const sum = values.reduce((acc, v) => {
    const num = parseFloat(String(v).replace(/[^0-9.-]/g, '')) || 0;
    return acc + num;
  }, 0);
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(sum);
}

function getDaysUntilClose(iso: string | undefined): number | null {
  if (!iso) return null;
  try {
    const d = new Date(iso);
    d.setHours(0, 0, 0, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return Math.ceil((d.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
  } catch {
    return null;
  }
}

function UrgencyBadge({ days }: { days: number }) {
  if (days < 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-red-500/15 px-2 py-0.5 text-xs font-medium text-red-700">
        <AlertCircle className="h-3 w-3" /> Overdue
      </span>
    );
  }
  if (days <= 7) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-amber-500/15 px-2 py-0.5 text-xs font-medium text-amber-700">
        <Calendar className="h-3 w-3" /> Due in {days}d
      </span>
    );
  }
  if (days <= 30) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-slate-500/10 px-2 py-0.5 text-xs font-medium text-slate-600">
        <Calendar className="h-3 w-3" /> {days}d to close
      </span>
    );
  }
  return null;
}

const DEAL_CARD_TYPE = 'DEAL_CARD';

function DealCard({
  deal,
  contactName,
  stage,
  onMoveStage,
  onEdit,
  onDelete,
  onOpenDetail,
  stages,
  isDragging,
}: {
  deal: Deal;
  contactName: string | undefined;
  stage: string;
  onMoveStage: (dealId: string, stage: string) => void;
  onEdit: () => void;
  onDelete: () => void;
  onOpenDetail: () => void;
  stages: readonly string[];
  isDragging?: boolean;
}) {
  const config = STAGE_COLORS[stage] ?? STAGE_COLORS.Qualification;
  const lastActivity = formatLastActivity(deal.lastActivityAtUtc);
  const daysToClose = getDaysUntilClose(deal.expectedCloseDateUtc);

  const [{ isDragging: drag }, dragRef] = useDrag(() => ({
    type: DEAL_CARD_TYPE,
    item: { dealId: deal.id, fromStage: stage },
    collect: (monitor) => ({ isDragging: monitor.isDragging() }),
  }), [deal.id, stage]);

  const isActuallyDragging = isDragging ?? drag;

  return (
    <motion.div
      ref={dragRef}
      layout
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.98 }}
      className={cn(
        'group cursor-grab active:cursor-grabbing rounded-xl border bg-white shadow-sm transition-all',
        isActuallyDragging && 'opacity-60 shadow-xl rotate-1 scale-[1.02]',
        `border-l-4 ${config.border}`
      )}
    >
      <Card className="border-0 shadow-none bg-transparent">
        <CardHeader className="p-4 pb-2">
          <div className="flex items-start gap-2">
            <div className="mt-0.5 shrink-0 text-slate-300 group-hover:text-slate-500 transition-colors">
              <GripVertical className="w-4 h-4" />
            </div>
            <div className="flex-1 min-w-0">
              <button
                type="button"
                onClick={onOpenDetail}
                className="text-left w-full font-semibold text-slate-900 text-sm leading-tight truncate block hover:text-orange-600 transition-colors"
                title={deal.name}
              >
                {deal.name}
              </button>
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <span className="inline-flex items-center rounded-lg bg-emerald-500/12 px-2.5 py-1 text-xs font-semibold text-emerald-700">
                  {deal.value}
                </span>
                {daysToClose !== null && <UrgencyBadge days={daysToClose} />}
              </div>
              {contactName && (
                <p className="text-xs text-slate-500 mt-1.5 truncate flex items-center gap-1" title={contactName}>
                  <User className="w-3 h-3 shrink-0" />
                  {contactName}
                </p>
              )}
              {lastActivity && (
                <p className="text-xs text-slate-400 mt-0.5">Last: {lastActivity}</p>
              )}
            </div>
            <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-slate-500 hover:text-slate-700 hover:bg-slate-100"
                onClick={(e) => { e.stopPropagation(); onEdit(); }}
                aria-label="Edit deal"
              >
                <Pencil className="w-3.5 h-3.5" />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-slate-500 hover:text-red-600 hover:bg-red-50"
                onClick={(e) => { e.stopPropagation(); onDelete(); }}
                aria-label="Delete deal"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="p-4 pt-0">
          <Select
            value={stage}
            onValueChange={(v) => onMoveStage(deal.id, v)}
          >
            <SelectTrigger className="h-9 text-xs border-slate-200 bg-slate-50/80 hover:bg-slate-100/80" onClick={(e) => e.stopPropagation()}>
              <ChevronDown className="w-3.5 h-3.5" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {stages.map((s) => (
                <SelectItem key={s} value={s}>
                  {s}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>
    </motion.div>
  );
}

function DroppableStageColumn({
  stage,
  deals,
  contacts,
  onMoveStage,
  onEdit,
  onDelete,
  onOpenDetail,
}: {
  stage: string;
  deals: Deal[];
  contacts: Contact[];
  onMoveStage: (dealId: string, stage: string) => void;
  onEdit: (deal: Deal) => void;
  onDelete: (deal: Deal) => void;
  onOpenDetail: (deal: Deal) => void;
}) {
  const config = STAGE_COLORS[stage] ?? STAGE_COLORS.Qualification;
  const StageIcon = STAGE_ICONS[stage] ?? Briefcase;

  const [{ isOver: dropIsOver }, dropRef] = useDrop(
    () => ({
      accept: DEAL_CARD_TYPE,
      drop: (item: { dealId: string; fromStage: string }) => {
        if (item.fromStage !== stage) {
          onMoveStage(item.dealId, stage);
        }
      },
      collect: (monitor) => ({ isOver: monitor.isOver() }),
    }),
    [stage, onMoveStage]
  );

  const showDropZone = dropIsOver;

  return (
    <div
      ref={dropRef}
      className={cn(
        'flex flex-col min-w-[300px] w-[300px] h-full max-h-full flex-shrink-0 rounded-2xl border border-slate-200/80 bg-white/80 backdrop-blur-sm overflow-hidden transition-all duration-200',
        `border-l-4 ${config.border}`,
        showDropZone && 'ring-2 ring-orange-400 ring-offset-2 bg-orange-50/50'
      )}
    >
      <div className={cn('shrink-0 px-4 py-3 border-b border-slate-200/80', config.bg)}>
        <div className="flex items-center gap-2">
          <StageIcon className={cn('w-4 h-4', config.accent)} aria-hidden />
          <h2 className={cn('font-semibold text-sm', config.accent)}>{stage}</h2>
        </div>
        <div className="mt-2 flex items-center justify-between text-xs text-slate-600">
          <span>{deals.length} deal{deals.length !== 1 ? 's' : ''}</span>
          <span className="font-medium">{formatValueSum(deals.map((d) => d.value))}</span>
        </div>
      </div>
      <div className="flex-1 min-h-0 overflow-y-auto p-3 space-y-3">
        <AnimatePresence mode="popLayout">
          {deals.map((deal) => (
            <DealCard
              key={deal.id}
              deal={deal}
              contactName={deal.contactId ? contacts.find((c) => c.id === deal.contactId)?.name : undefined}
              stage={stage}
              onMoveStage={onMoveStage}
              onEdit={() => onEdit(deal)}
              onDelete={() => onDelete(deal)}
              onOpenDetail={() => onOpenDetail(deal)}
              stages={STAGES}
            />
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}

export default function Pipeline() {
  const [deals, setDeals] = useState<Deal[]>([]);
  const [loading, setLoading] = useState(true);
  const [viewMode, setViewMode] = useState<'kanban' | 'list'>('kanban');
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState({
    name: '',
    value: '',
    stage: 'Qualification',
    expectedCloseDate: '',
    companyId: '',
    contactId: '',
  });
  const [companies, setCompanies] = useState<Company[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [saving, setSaving] = useState(false);
  const [editDeal, setEditDeal] = useState<Deal | null>(null);
  const [editForm, setEditForm] = useState({ name: '', value: '', expectedCloseDate: '', companyId: '' });
  const [savingEdit, setSavingEdit] = useState(false);
  const [deleteConfirmDeal, setDeleteConfirmDeal] = useState<Deal | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [detailDeal, setDetailDeal] = useState<Deal | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all([getDeals(), getContacts()])
      .then(([dealsData, contactsData]) => {
        if (!cancelled) {
          setDeals(dealsData);
          setContacts(contactsData);
        }
      })
      .catch(() => { if (!cancelled) toast.error(messages.errors.loadFailed); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (createOpen || editDeal || detailDeal) {
      getCompanies().then(setCompanies).catch(() => {});
    }
  }, [createOpen, editDeal, detailDeal]);

  useEffect(() => {
    if (editDeal) {
      setEditForm({
        name: editDeal.name,
        value: editDeal.value,
        expectedCloseDate: editDeal.expectedCloseDateUtc ? editDeal.expectedCloseDateUtc.slice(0, 10) : '',
        companyId: editDeal.companyId || '',
      });
    }
  }, [editDeal]);

  const dealsByStage = STAGES.map((stage) => ({
    stage,
    deals: deals.filter((d) => (d.stage || 'Qualification') === stage),
  }));

  const funnelData = dealsByStage.map(({ stage, deals }) => ({
    name: stage,
    count: deals.length,
    value: deals.reduce((acc, d) => {
      const num = parseFloat(String(d.value).replace(/[^0-9.-]/g, '')) || 0;
      return acc + num;
    }, 0),
    fill: STAGE_COLORS[stage]?.bar ?? '#94a3b8',
  }));

  const handleMoveStage = async (dealId: string, newStage: string) => {
    const deal = deals.find((d) => d.id === dealId);
    if (deal && (deal.stage || 'Qualification') === newStage) return;
    const isWon = newStage === 'Closed Won' ? true : newStage === 'Closed Lost' ? false : undefined;
    const payload: { stage: string; isWon?: boolean } = { stage: newStage };
    if (isWon !== undefined) payload.isWon = isWon;
    try {
      const updated = await updateDeal(dealId, payload);
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === dealId ? updated : d)));
        toast.success(messages.success.dealMoved);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
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

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editDeal) return;
    if (!editForm.name.trim() || !editForm.value.trim()) {
      toast.error(messages.validation.nameAndValueRequired);
      return;
    }
    setSavingEdit(true);
    try {
      const expectedCloseDateUtc = editForm.expectedCloseDate
        ? new Date(editForm.expectedCloseDate).toISOString()
        : undefined;
      const updated = await updateDeal(editDeal.id, {
        name: editForm.name.trim(),
        value: editForm.value.trim(),
        expectedCloseDateUtc,
        companyId: editForm.companyId || undefined,
      });
      if (updated) {
        setDeals((prev) => prev.map((d) => (d.id === editDeal.id ? updated : d)));
        toast.success(messages.success.dealUpdated);
        setEditDeal(null);
        if (detailDeal?.id === editDeal.id) setDetailDeal(updated);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSavingEdit(false);
    }
  };

  const setExpectedCloseInDays = (days: number) => {
    const d = new Date();
    d.setDate(d.getDate() + days);
    setCreateForm((f) => ({ ...f, expectedCloseDate: d.toISOString().slice(0, 10) }));
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!createForm.name.trim() || !createForm.value.trim()) {
      toast.error(messages.validation.nameAndValueRequired);
      return;
    }
    setSaving(true);
    try {
      const expectedCloseDateUtc = createForm.expectedCloseDate
        ? new Date(createForm.expectedCloseDate).toISOString()
        : undefined;
      const created = await createDeal({
        name: createForm.name.trim(),
        value: createForm.value.trim(),
        stage: createForm.stage,
        expectedCloseDateUtc,
        companyId: createForm.companyId || undefined,
        contactId: createForm.contactId || undefined,
      });
      if (created) {
        setDeals((prev) => [created, ...prev]);
        toast.success(messages.success.dealCreated);
        setCreateOpen(false);
        setCreateForm({ name: '', value: '', stage: 'Qualification', expectedCloseDate: '', companyId: '', contactId: '' });
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  const activeDeals = deals.filter((d) => (d.stage || 'Qualification') !== 'Closed Lost');
  const totalPipelineValue = formatValueSum(activeDeals.map((d) => d.value));
  const wonCount = deals.filter((d) => d.stage === 'Closed Won').length;
  const lostCount = deals.filter((d) => d.stage === 'Closed Lost').length;

  const detailContact = detailDeal?.contactId ? contacts.find((c) => c.id === detailDeal.contactId) : undefined;
  const detailCompany = detailDeal?.companyId ? companies.find((c) => c.id === detailDeal.companyId) : undefined;

  return (
    <div className="min-h-screen flex flex-col overflow-hidden bg-[linear-gradient(180deg,_#0f172a_0%,_#1e293b_50%,_#f8fafc_100%)]">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 min-h-0 flex flex-col w-full max-w-[1800px] mx-auto px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        {/* Hero */}
        <motion.div
          initial={{ opacity: 0, y: -12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.4 }}
          className="shrink-0 mb-6 rounded-3xl bg-gradient-to-br from-indigo-600 via-violet-600 to-teal-700 text-white p-6 sm:p-8 shadow-2xl relative overflow-hidden"
        >
          <div className="absolute inset-0 bg-[url('data:image/svg+xml,%3Csvg width=\'60\' height=\'60\' viewBox=\'0 0 60 60\' xmlns=\'http://www.w3.org/2000/svg\'%3E%3Cg fill=\'none\' fill-rule=\'evenodd\'%3E%3Cg fill=\'%23ffffff\' fill-opacity=\'0.05\'%3E%3Cpath d=\'M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z\'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E')] opacity-50" />
          <div className="relative flex flex-col sm:flex-row sm:items-center sm:justify-between gap-6">
            <div>
              <div className="flex items-center gap-2 text-amber-200/90 text-sm font-medium mb-2">
                <Sparkles className="w-4 h-4" />
                Pipeline
              </div>
              <h1 className="text-3xl sm:text-4xl font-bold tracking-tight">Deals</h1>
              <p className="text-slate-200/90 mt-2 text-sm sm:text-base max-w-xl">
                Track opportunities from first touch to close. Drag cards between stages or use the dropdown.
              </p>
            </div>
            <Button
              onClick={() => setCreateOpen(true)}
              className="shrink-0 gap-2 bg-white text-indigo-700 hover:bg-slate-100 border-0 shadow-lg hover:shadow-xl transition-all font-semibold"
            >
              <Plus className="w-4 h-4" />
              New deal
            </Button>
          </div>
          {!loading && deals.length > 0 && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ delay: 0.2 }}
              className="mt-6 flex flex-wrap gap-3"
            >
              <div className="flex items-center gap-2 rounded-xl bg-white/12 backdrop-blur-sm px-4 py-2.5 border border-white/20">
                <Briefcase className="w-5 h-5 text-amber-300" />
                <span className="text-sm font-medium">{deals.length} deals</span>
              </div>
              <div className="flex items-center gap-2 rounded-xl bg-white/12 backdrop-blur-sm px-4 py-2.5 border border-white/20">
                <DollarSign className="w-5 h-5 text-emerald-300" />
                <span className="text-sm font-medium">Pipeline: {totalPipelineValue}</span>
              </div>
              <div className="flex items-center gap-2 rounded-xl bg-white/12 backdrop-blur-sm px-4 py-2.5 border border-white/20">
                <Trophy className="w-5 h-5 text-emerald-400" />
                <span className="text-sm font-medium">{wonCount} won / {lostCount} lost</span>
              </div>
            </motion.div>
          )}
        </motion.div>

        {loading ? (
          <div className="flex justify-center py-16">
            <LoadingSpinner />
          </div>
        ) : deals.length === 0 ? (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.3 }}
          >
            <EmptyState
              icon={Briefcase}
              title="No deals yet"
              description="Create a deal or connect your CRM to see deals here."
              actionLabel="New deal"
              onAction={() => setCreateOpen(true)}
              className="shadow-sm"
            />
          </motion.div>
        ) : (
          <>
            {/* Funnel chart */}
            <motion.div
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.15, duration: 0.3 }}
              className="shrink-0 mb-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"
            >
              <h3 className="text-sm font-semibold text-slate-700 mb-4">Pipeline funnel</h3>
              <div className="h-[120px] w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={funnelData} layout="vertical" margin={{ top: 0, right: 24, left: 80, bottom: 0 }}>
                    <XAxis type="number" hide />
                    <YAxis type="category" dataKey="name" width={75} tick={{ fontSize: 11 }} />
                    <Tooltip
                      formatter={(value: number) => [value, 'Deals']}
                      labelFormatter={(label) => label}
                      contentStyle={{ borderRadius: 8, border: '1px solid #e2e8f0' }}
                    />
                    <Bar dataKey="count" radius={[0, 4, 4, 0]} maxBarSize={24}>
                      {funnelData.map((entry, index) => (
                        <Cell key={index} fill={entry.fill} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </motion.div>

            {/* View toggle */}
            <div className="flex items-center justify-between mb-4 shrink-0">
              <Tabs value={viewMode} onValueChange={(v) => setViewMode(v as 'kanban' | 'list')}>
                <TabsList className="bg-slate-100">
                  <TabsTrigger value="kanban" className="gap-1.5">
                    <LayoutGrid className="w-4 h-4" />
                    Kanban
                  </TabsTrigger>
                  <TabsTrigger value="list" className="gap-1.5">
                    <List className="w-4 h-4" />
                    List
                  </TabsTrigger>
                </TabsList>
              </Tabs>
            </div>

            {viewMode === 'kanban' ? (
              <DndProvider backend={HTML5Backend}>
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="flex-1 min-h-0 flex gap-4 overflow-x-auto overflow-y-hidden pb-6 -mx-1 px-1"
                >
                  {dealsByStage.map(({ stage, deals: stageDeals }) => (
                    <DroppableStageColumn
                      key={stage}
                      stage={stage}
                      deals={stageDeals}
                      contacts={contacts}
                      onMoveStage={handleMoveStage}
                      onEdit={setEditDeal}
                      onDelete={setDeleteConfirmDeal}
                      onOpenDetail={setDetailDeal}
                    />
                  ))}
                </motion.div>
              </DndProvider>
            ) : (
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                className="flex-1 min-h-0 overflow-auto"
              >
                <div className="rounded-2xl border border-slate-200 bg-white overflow-hidden">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 bg-slate-50/80">
                        <th className="text-left py-3 px-4 font-semibold text-slate-700">Deal</th>
                        <th className="text-left py-3 px-4 font-semibold text-slate-700">Value</th>
                        <th className="text-left py-3 px-4 font-semibold text-slate-700">Stage</th>
                        <th className="text-left py-3 px-4 font-semibold text-slate-700">Contact</th>
                        <th className="text-left py-3 px-4 font-semibold text-slate-700">Close date</th>
                        <th className="w-12" />
                      </tr>
                    </thead>
                    <tbody>
                      {deals.map((deal) => {
                        const config = STAGE_COLORS[deal.stage || 'Qualification'] ?? STAGE_COLORS.Qualification;
                        const contact = deal.contactId ? contacts.find((c) => c.id === deal.contactId) : undefined;
                        return (
                          <tr
                            key={deal.id}
                            className="border-b border-slate-100 last:border-0 hover:bg-slate-50/50 transition-colors"
                          >
                            <td className="py-3 px-4">
                              <button
                                type="button"
                                onClick={() => setDetailDeal(deal)}
                                className="font-medium text-slate-900 hover:text-orange-600 text-left"
                              >
                                {deal.name}
                              </button>
                            </td>
                            <td className="py-3 px-4 text-emerald-700 font-medium">{deal.value}</td>
                            <td className="py-3 px-4">
                              <span className={cn('inline-flex items-center rounded-lg px-2 py-1 text-xs font-medium', config.bg, config.accent)}>
                                {deal.stage || 'Qualification'}
                              </span>
                            </td>
                            <td className="py-3 px-4 text-slate-600">{contact?.name ?? '—'}</td>
                            <td className="py-3 px-4 text-slate-600">
                              {deal.expectedCloseDateUtc
                                ? new Date(deal.expectedCloseDateUtc).toLocaleDateString()
                                : '—'}
                            </td>
                            <td className="py-3 px-4">
                              <div className="flex gap-1">
                                <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => setEditDeal(deal)} aria-label="Edit">
                                  <Pencil className="w-3.5 h-3.5" />
                                </Button>
                                <Button variant="ghost" size="icon" className="h-8 w-8 text-red-600 hover:text-red-700" onClick={() => setDeleteConfirmDeal(deal)} aria-label="Delete">
                                  <Trash2 className="w-3.5 h-3.5" />
                                </Button>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </motion.div>
            )}
          </>
        )}
      </main>

      {/* Deal detail sheet */}
      <Sheet open={!!detailDeal} onOpenChange={(open) => !open && setDetailDeal(null)}>
        <SheetContent side="right" className="w-full sm:max-w-md">
          {detailDeal && (
            <>
              <SheetHeader>
                <SheetTitle className="text-xl">{detailDeal.name}</SheetTitle>
              </SheetHeader>
              <div className="flex flex-col gap-6 py-4 overflow-auto">
                <div>
                  <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Value</p>
                  <p className="text-lg font-semibold text-emerald-700">{detailDeal.value}</p>
                </div>
                <div>
                  <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Stage</p>
                  <span className={cn(
                    'inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-medium',
                    STAGE_COLORS[detailDeal.stage || 'Qualification']?.bg,
                    STAGE_COLORS[detailDeal.stage || 'Qualification']?.accent
                  )}>
                    {detailDeal.stage || 'Qualification'}
                  </span>
                </div>
                {detailContact && (
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Contact</p>
                    <p className="text-slate-900">{detailContact.name}</p>
                    {detailContact.email && <p className="text-sm text-slate-500">{detailContact.email}</p>}
                  </div>
                )}
                {detailCompany && (
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Company</p>
                    <p className="text-slate-900">{detailCompany.name}</p>
                  </div>
                )}
                {detailDeal.expectedCloseDateUtc && (
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Expected close</p>
                    <p className="text-slate-900">
                      {new Date(detailDeal.expectedCloseDateUtc).toLocaleDateString(undefined, {
                        weekday: 'long',
                        year: 'numeric',
                        month: 'long',
                        day: 'numeric',
                      })}
                    </p>
                    {getDaysUntilClose(detailDeal.expectedCloseDateUtc) !== null && (
                      <UrgencyBadge days={getDaysUntilClose(detailDeal.expectedCloseDateUtc)!} />
                    )}
                  </div>
                )}
                {detailDeal.lastActivityAtUtc && (
                  <div>
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">Last activity</p>
                    <p className="text-slate-600">{formatLastActivity(detailDeal.lastActivityAtUtc)}</p>
                  </div>
                )}
                <div className="flex gap-2 pt-4 border-t border-slate-200">
                  <Button
                    variant="outline"
                    className="flex-1 gap-2"
                    onClick={() => {
                      setEditDeal(detailDeal);
                      setDetailDeal(null);
                    }}
                  >
                    <Pencil className="w-4 h-4" />
                    Edit
                  </Button>
                  <Button
                    variant="outline"
                    className="text-red-600 hover:text-red-700 hover:bg-red-50"
                    onClick={() => {
                      setDeleteConfirmDeal(detailDeal);
                      setDetailDeal(null);
                    }}
                  >
                    <Trash2 className="w-4 h-4" />
                  </Button>
                </div>
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>New deal</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleCreate} className="space-y-4">
            <div>
              <Label htmlFor="deal-name">Deal name *</Label>
              <Input
                id="deal-name"
                value={createForm.name}
                onChange={(e) => setCreateForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="deal-value">Value *</Label>
              <Input
                id="deal-value"
                value={createForm.value}
                onChange={(e) => setCreateForm((f) => ({ ...f, value: e.target.value }))}
                placeholder="e.g. $50,000"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label>Stage</Label>
              <Select
                value={createForm.stage}
                onValueChange={(v) => setCreateForm((f) => ({ ...f, stage: v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {STAGES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
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
                <SelectTrigger className="mt-1">
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
                <SelectTrigger className="mt-1">
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
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving} className="bg-orange-600 hover:bg-orange-500">
                {saving ? 'Creating...' : 'Create deal'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editDeal} onOpenChange={(open) => !open && setEditDeal(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Edit deal</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSaveEdit} className="space-y-4">
            <div>
              <Label htmlFor="edit-deal-name">Deal name *</Label>
              <Input
                id="edit-deal-name"
                value={editForm.name}
                onChange={(e) => setEditForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Acme Corp - Enterprise"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="edit-deal-value">Value *</Label>
              <Input
                id="edit-deal-value"
                value={editForm.value}
                onChange={(e) => setEditForm((f) => ({ ...f, value: e.target.value }))}
                placeholder="e.g. $50,000"
                className="mt-1"
                required
              />
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
                onValueChange={(v) => setEditForm((f) => ({ ...f, companyId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
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
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditDeal(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={savingEdit} className="bg-orange-600 hover:bg-orange-500">
                {savingEdit ? 'Saving...' : 'Save'}
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
    </div>
  );
}
