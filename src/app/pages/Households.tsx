import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Crown,
  DollarSign,
  FileDown,
  HomeIcon,
  Loader2,
  Plus,
  Search,
  Trash2,
  UserPlus,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/app/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import {
  MEMBER_ROLES,
  addPersonToHousehold,
  createHousehold,
  deleteHousehold,
  downloadHouseholdStatement,
  householdTotals,
  makeHouseholdPrimary,
  removeHouseholdPerson,
  updateHouseholdPerson,
  type Household,
  type HouseholdPerson,
  type HouseholdRole,
} from '@/app/api/households';
import { ApiError } from '@/app/api/apiClient';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { ClubReadState } from '@/app/components/ClubReadState';
import { NONE, useClubMembers, useClubReads, useHouseholds } from '@/app/hooks/queries';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { DuplicateMerge, HOUSEHOLD_DUPLICATES } from '@/app/components/duplicates';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { useClubMoney } from '@/app/hooks/useClubMoney';
import { WriteOnly } from '@/app/components/WriteOnly';
import { orgCan, PERMISSION, type Permission } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';

const ROLE_LABEL: Record<HouseholdRole, string> = {
  Primary: 'Primary',
  Spouse: 'Spouse',
  Partner: 'Partner',
  Child: 'Child',
  Dependent: 'Dependent',
  Parent: 'Parent',
  Sibling: 'Sibling',
  Other: 'Other',
};

/** The month so far, as the household statement's default period. */
function thisMonth(): { fromUtc: string; toUtc: string } {
  const now = new Date();
  const from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
  const to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
  return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
}

function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/** Whether the signed-in member may do this, as WriteOnly decides it. */
function useMay(): (permission: Permission) => boolean {
  const org = useOrgOptional();
  return (permission) => !org || (!org.isReadOnly && orgCan(org.currentOrg ?? null, permission));
}

export default function Households() {
  const { format: formatCurrency } = useClubMoney();
  const householdsQuery = useHouseholds();
  const membersQuery = useClubMembers();
  const reads = useClubReads(householdsQuery, membersQuery);
  const households: Household[] = householdsQuery.data ?? NONE;
  const members = membersQuery.data ?? NONE;
  usePageLoading(reads.loading);
  const may = useMay();
  const canMoney = may(PERMISSION.ManageMoney);

  const [search, setSearch] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState({ name: '', primaryMemberId: '', notes: '' });
  const [adding, setAdding] = useState<Household | null>(null);
  const [addForm, setAddForm] = useState<{ memberId: string; role: HouseholdRole; rollUpBilling: boolean }>({ memberId: '', role: 'Spouse', rollUpBilling: false });
  const [busy, setBusy] = useState<string | null>(null);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return households;
    return households.filter((h) => `${h.name} ${h.people.map((p) => `${p.name} ${p.email ?? ''}`).join(' ')}`.toLowerCase().includes(q));
  }, [households, search]);

  const inAHousehold = useMemo(() => {
    const set = new Set<string>();
    for (const h of households) for (const p of h.people) if (p.memberId) set.add(p.memberId);
    return set;
  }, [households]);

  const totals = useMemo(() => {
    let dues = 0;
    let rollingUp = 0;
    for (const h of households) {
      dues += householdTotals(h).duesMonthly;
      rollingUp += h.people.filter((p) => p.rollUpBilling).length;
    }
    return { count: households.length, linked: inAHousehold.size, dues, rollingUp };
  }, [households, inAHousehold]);

  const availableMembers = useMemo(
    () => members.filter((m) => !inAHousehold.has(m.id)).sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`)),
    [members, inAHousehold],
  );

  /** A change to a household: saved against the version on screen, and the page read again either way. */
  const change = async (key: string, run: () => Promise<unknown>, done: string, failed: string) => {
    setBusy(key);
    try {
      await run();
      toast.success(done);
    } catch (err) {
      // Someone changed the household first: say so, and show what they saved.
      toast.error(err instanceof ApiError && err.status === 409 ? errorText(err, 'Someone changed this household first. It has been reloaded.') : errorText(err, failed));
    } finally {
      setBusy(null);
      await reads.reload();
    }
  };

  const submitCreate = async () => {
    if (!createForm.name.trim() || !createForm.primaryMemberId) {
      toast.error('Give the household a name and pick who heads it.');
      return;
    }
    await change('create', async () => {
      await createHousehold({ name: createForm.name.trim(), primaryMemberId: createForm.primaryMemberId, notes: createForm.notes.trim() || undefined });
      setCreateOpen(false);
    }, 'Household created', 'The household was not created.');
  };

  const openAdd = (h: Household) => {
    setAddForm({ memberId: '', role: h.people.some((p) => p.role === 'Spouse' || p.role === 'Partner') ? 'Child' : 'Spouse', rollUpBilling: false });
    setAdding(h);
  };

  const submitAdd = async () => {
    if (!adding || !addForm.memberId) {
      toast.error('Pick a member.');
      return;
    }
    const target = adding;
    await change(`add:${target.id}`, async () => {
      await addPersonToHousehold(target.id, {
        memberId: addForm.memberId,
        role: addForm.role,
        rollUpBilling: canMoney ? addForm.rollUpBilling : undefined,
        version: target.version,
      });
      setAdding(null);
    }, `Added to the ${target.name} household`, 'They were not added.');
  };

  const setRole = (h: Household, p: HouseholdPerson, role: HouseholdRole) =>
    change(p.linkId, () => updateHouseholdPerson(h.id, p.linkId, { role, version: h.version }), `${p.name} is now ${ROLE_LABEL[role].toLowerCase()}`, 'The role was not changed.');

  const setRollUp = (h: Household, p: HouseholdPerson, rollUpBilling: boolean) =>
    change(p.linkId, () => updateHouseholdPerson(h.id, p.linkId, { rollUpBilling, version: h.version }),
      rollUpBilling ? `${p.name}'s bills now go to ${h.primaryName ?? 'the primary'}` : `${p.name} is billed on their own account again`,
      'Roll-up was not changed.');

  const makePrimary = (h: Household, p: HouseholdPerson) =>
    change(p.linkId, () => makeHouseholdPrimary(h.id, p.linkId, h.version), `${p.name} now heads the ${h.name} household`, 'The primary was not changed.');

  const removePerson = (h: Household, p: HouseholdPerson) => {
    if (!confirm(`Take ${p.name} out of the ${h.name} household?`)) return;
    void change(p.linkId, () => removeHouseholdPerson(h.id, p.linkId, h.version), `${p.name} left the household`, 'They were not removed.');
  };

  const removeHousehold = (h: Household) => {
    if (!confirm(`Delete the ${h.name} household? The members and contacts in it stay; only the grouping goes.`)) return;
    void change(`delete:${h.id}`, () => deleteHousehold(h.id), 'Household deleted', 'The household was not deleted.');
  };

  const statement = async (h: Household) => {
    setBusy(`pdf:${h.id}`);
    try {
      const { fromUtc, toUtc } = thisMonth();
      await downloadHouseholdStatement(h.id, fromUtc, toUtc);
    } catch (err) {
      toast.error(errorText(err, 'The statement could not be made.'));
    } finally {
      setBusy(null);
    }
  };

  // How this person likes households shown: cards, a list or a table, saved for them.
  const view = useViewPreference('households', { layouts: ['grid', 'list', 'table'] });
  const names = (h: Household) => h.people.map((p) => p.name).join(', ');
  const householdColumns: RecordColumn<Household>[] = [
    {
      id: 'name',
      label: 'Household',
      required: true,
      cell: (h) => (
        <span className="flex items-center gap-2.5">
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-teal-400 to-cyan-500 text-white shadow-sm"><HomeIcon className="h-3.5 w-3.5" aria-hidden /></span>
          <span className="truncate">The {h.name} household</span>
        </span>
      ),
    },
    { id: 'primary', label: 'Primary', cell: (h) => h.primaryName ?? '—' },
    { id: 'people', label: 'People', numeric: true, cell: (h) => h.people.length },
    { id: 'names', label: 'Who', byDefault: false, cell: names },
    { id: 'rollup', label: 'Bills to primary', numeric: true, cell: (h) => h.people.filter((p) => p.rollUpBilling).length },
    { id: 'dues', label: 'Dues a month', numeric: true, cell: (h) => formatCurrency(householdTotals(h).duesMonthly) },
  ];

  /** Add someone, the statement, delete: the same in every layout. */
  const householdActions = (h: Household) => (
    <>
      <WriteOnly permission={PERMISSION.ClubMembers}>
        <button
          type="button"
          onClick={() => openAdd(h)}
          className="rounded p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
          aria-label={`Add someone to the ${h.name} household`}
        >
          <UserPlus className="w-4 h-4" />
        </button>
      </WriteOnly>
      {h.primaryMemberId && (
        <button
          type="button"
          onClick={() => void statement(h)}
          disabled={busy === `pdf:${h.id}`}
          className="rounded p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700 disabled:opacity-50"
          aria-label={`Download the ${h.name} household statement`}
          title="This month's household statement (PDF)"
        >
          {busy === `pdf:${h.id}` ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileDown className="w-4 h-4" />}
        </button>
      )}
      <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
        <button
          type="button"
          onClick={() => removeHousehold(h)}
          className="rounded p-2 text-slate-500 hover:bg-rose-50 hover:text-rose-600"
          aria-label={`Delete the ${h.name} household`}
        >
          <Trash2 className="w-4 h-4" />
        </button>
      </WriteOnly>
    </>
  );

  const personRow = (h: Household, p: HouseholdPerson) => {
    const working = busy === p.linkId;
    return (
      <li key={p.linkId} className="rounded-lg bg-slate-50 px-3 py-2">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-teal-100 to-cyan-100 flex items-center justify-center text-teal-700 font-semibold text-xs shrink-0">
              {p.isPrimary ? <Crown className="h-3.5 w-3.5" aria-label="Primary" /> : p.name.split(/\s+/).map((x) => x[0]).slice(0, 2).join('').toUpperCase()}
            </div>
            <div className="min-w-0">
              <p className="text-sm font-medium text-slate-900 truncate">
                {p.memberId ? (
                  <Link to={`/members/${p.memberId}`} className="hover:text-teal-700 hover:underline">{p.name}</Link>
                ) : p.contactId ? (
                  <Link to={p.leadId ? `/leads/${p.leadId}` : `/contacts/${p.contactId}`} className="hover:text-teal-700 hover:underline">{p.name}</Link>
                ) : p.name}
              </p>
              <p className="text-xs text-slate-500">
                {p.isPrimary ? 'Primary' : ROLE_LABEL[p.role]}
                {p.memberId ? (p.tier ? ` · ${p.tier}` : '') : ' · not a member yet'}
                {p.isArchived ? ' · archived contact' : ''}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1 shrink-0">
            {working && <Loader2 className="h-3.5 w-3.5 animate-spin text-teal-600" aria-hidden />}
            {!p.isPrimary && (
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Select value={p.role} onValueChange={(v) => void setRole(h, p, v as HouseholdRole)} disabled={!!busy}>
                  <SelectTrigger className="h-7 w-auto gap-1 px-2 text-xs" aria-label={`${p.name}'s role in the household`}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {MEMBER_ROLES.map((r) => (
                      <SelectItem key={r} value={r}>{ROLE_LABEL[r]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <button
                  type="button"
                  onClick={() => void makePrimary(h, p)}
                  disabled={!!busy}
                  className="rounded px-1.5 py-1 text-[11px] font-medium text-teal-700 hover:bg-teal-50 disabled:opacity-50"
                  aria-label={`Make ${p.name} the primary`}
                >
                  Make primary
                </button>
                <button
                  type="button"
                  onClick={() => removePerson(h, p)}
                  disabled={!!busy}
                  className="rounded p-1 text-slate-500 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                  aria-label={`Take ${p.name} out of the household`}
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </WriteOnly>
            )}
          </div>
        </div>
        {/* Whose account a member's bills go on. Switching it is money: money.manage. */}
        {!p.isPrimary && p.memberId && (
          <div className="mt-2 flex items-center justify-between gap-2 border-t border-slate-200/70 pt-2">
            <p className="text-xs text-slate-600">
              {p.rollUpBilling
                ? `Bills go to ${h.primaryName ?? 'the primary'}`
                : 'Billed on their own account'}
            </p>
            {canMoney && h.primaryMemberId && (
              <Switch
                checked={p.rollUpBilling}
                onCheckedChange={(on) => void setRollUp(h, p, on)}
                disabled={!!busy}
                aria-label={`Bill ${p.name}'s charges to ${h.primaryName ?? 'the primary'}`}
              />
            )}
          </div>
        )}
      </li>
    );
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <PageHero
            icon={HomeIcon}
            tone="teal"
            title="Households"
            subtitle="Families with one primary. A member whose bills roll up has their charges, tabs and dues put on the primary's account, each line saying whose it was."
            actions={
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button onClick={() => { setCreateForm({ name: '', primaryMemberId: '', notes: '' }); setCreateOpen(true); }} className={heroPrimary('teal')}>
                  <Plus className="h-4 w-4" />
                  New household
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: 'Households', value: totals.count, icon: HomeIcon, tone: 'teal' },
              { label: 'Members in one', value: totals.linked, icon: Users, tone: 'blue', hint: `${Math.max(0, members.length - totals.linked)} in none` },
              { label: 'Bills to a primary', value: totals.rollingUp, icon: Crown, tone: 'violet', hint: 'people whose bills roll up' },
              { label: 'Household dues', value: totals.dues, icon: DollarSign, tone: 'emerald', format: formatCurrency, hint: 'a month' },
            ]}
          />

          <DuplicateMerge spec={HOUSEHOLD_DUPLICATES} onMerged={() => { void reads.reload(); }} />

          <div className="flex flex-wrap items-center gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search by household or person..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <ViewSwitcher view={view} columns={columnChoices(householdColumns)} className="ml-auto" />
          </div>

          <ClubReadState reads={reads} what="Households">
            {reads.loading ? (
              <div className="flex items-center justify-center py-20 text-slate-500">
                <Loader2 className="h-6 w-6 animate-spin" />
              </div>
            ) : filtered.length === 0 ? (
              <EmptyState
                icon={HomeIcon}
                title={households.length === 0 ? 'No households yet' : 'No households match'}
                description={
                  households.length === 0
                    ? 'Group a primary member with their spouse, partner or children, and roll their bills up to one account.'
                    : 'Try clearing the search.'
                }
                variant="teal"
              />
            ) : (
              <ViewTransition layout={view.layout}>
                {view.layout === 'table' ? (
                  <RecordTable label="Households" items={filtered} view={view} getKey={(h) => h.id} columns={householdColumns} actions={householdActions} />
                ) : view.layout === 'list' ? (
                  <RecordList
                    label="Households"
                    items={filtered}
                    view={view}
                    getKey={(h) => h.id}
                    leading={() => (
                      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-teal-400 to-cyan-500 text-white shadow-sm"><HomeIcon className="h-4 w-4" aria-hidden /></span>
                    )}
                    title={(h) => `The ${h.name} household`}
                    subtitle={(h) => names(h) || 'Nobody yet'}
                    columns={householdColumns.filter((c) => c.id !== 'names')}
                    actions={householdActions}
                  />
                ) : (
                  <div className={`grid grid-cols-1 lg:grid-cols-2 ${gridDensity(view, 'gap-4', 'gap-3 2xl:grid-cols-3')}`}>
                    {filtered.map((h, index) => {
                      const t = householdTotals(h);
                      return (
                        <StaggerItem
                          as="article"
                          key={h.id}
                          index={index}
                          className={`bg-white rounded-2xl border border-slate-200 shadow-sm hover:border-teal-200 ${view.density === 'compact' ? 'p-4' : 'p-5'} ${liftOnHover}`}
                        >
                          <div className="flex items-start justify-between gap-2 mb-3">
                            <div>
                              <h3 className="font-semibold text-slate-900 flex items-center gap-2">
                                <HomeIcon className="w-4 h-4 text-teal-600" /> The {h.name} household
                              </h3>
                              {h.notes && <p className="text-xs text-slate-500 mt-1 whitespace-pre-line">{h.notes}</p>}
                            </div>
                            <div className="flex gap-1">{householdActions(h)}</div>
                          </div>
                          <ul className="space-y-2 mb-3">{h.people.map((p) => personRow(h, p))}</ul>
                          <div className="grid grid-cols-2 gap-3 pt-3 border-t border-slate-100">
                            <div>
                              <p className="text-xs text-slate-500">Dues a month</p>
                              <p className="font-semibold text-slate-900">{formatCurrency(t.duesMonthly)}</p>
                            </div>
                            <div>
                              <p className="text-xs text-slate-500">Members' own balances</p>
                              <p className={`font-semibold ${t.balance < 0 ? 'text-rose-600' : 'text-slate-900'}`}>{formatCurrency(t.balance)}</p>
                            </div>
                          </div>
                        </StaggerItem>
                      );
                    })}
                  </div>
                )}
              </ViewTransition>
            )}
          </ClubReadState>
        </main>
      </PageTransition>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>New household</DialogTitle>
            <DialogDescription>Pick the member who heads it: the bills of anyone whose bills roll up go to their account.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label htmlFor="household-name">Household name</Label>
              <Input id="household-name" value={createForm.name} onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })} placeholder="e.g. Hartwell, Okafor" />
            </div>
            <div>
              <Label>Primary member</Label>
              <Select value={createForm.primaryMemberId} onValueChange={(v) => setCreateForm({ ...createForm, primaryMemberId: v })}>
                <SelectTrigger aria-label="Primary member">
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {availableMembers.map((m) => (
                    <SelectItem key={m.id} value={m.id}>{m.firstName} {m.lastName} — {m.tier}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="household-notes">Notes</Label>
              <Input id="household-notes" value={createForm.notes} onChange={(e) => setCreateForm({ ...createForm, notes: e.target.value })} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)} disabled={busy === 'create'}>Cancel</Button>
            <Button onClick={() => void submitCreate()} disabled={busy === 'create'}>
              {busy === 'create' ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Creating</> : 'Create household'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={adding !== null} onOpenChange={(o) => !o && setAdding(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Add to the {adding?.name} household</DialogTitle>
            <DialogDescription>A person is in one household. Their role is their relation to {adding?.primaryName ?? 'the primary'}.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Member</Label>
              <Select value={addForm.memberId} onValueChange={(v) => setAddForm({ ...addForm, memberId: v })}>
                <SelectTrigger aria-label="Member">
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {availableMembers.map((m) => (
                    <SelectItem key={m.id} value={m.id}>{m.firstName} {m.lastName} — {m.tier}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Role</Label>
              <Select value={addForm.role} onValueChange={(v) => setAddForm({ ...addForm, role: v as HouseholdRole })}>
                <SelectTrigger aria-label="Role">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEMBER_ROLES.map((r) => (
                    <SelectItem key={r} value={r}>{ROLE_LABEL[r]}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {canMoney && adding?.primaryMemberId ? (
              <div className="flex items-center justify-between gap-3 rounded-lg bg-slate-50 px-3 py-2">
                <Label htmlFor="household-rollup" className="text-sm font-normal text-slate-700">
                  Bill their charges, tabs and dues to {adding.primaryName ?? 'the primary'}
                </Label>
                <Switch id="household-rollup" checked={addForm.rollUpBilling} onCheckedChange={(on) => setAddForm({ ...addForm, rollUpBilling: on })} />
              </div>
            ) : (
              <p className="rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600">
                They are billed on their own account. Someone who handles the club&apos;s money can roll their bills up to the primary.
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(null)} disabled={busy?.startsWith('add:')}>Cancel</Button>
            <Button onClick={() => void submitAdd()} disabled={busy?.startsWith('add:')}>
              {busy?.startsWith('add:') ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Adding</> : 'Add to household'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
