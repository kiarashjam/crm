import { useEffect, useMemo, useState } from 'react';
import {
  HomeIcon,
  Plus,
  Search,
  Trash2,
  Users,
  DollarSign,
  UserPlus,
  Loader2,
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
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  getHouseholdRollups,
  createHousehold,
  deleteHousehold,
  addMemberToHousehold,
  removeMemberFromHousehold,
  HOUSEHOLD_ROLES,
  type HouseholdRollup,
  type HouseholdRole,
} from '@/app/api/households';
import { getMembers, type Member } from '@/app/api/members';

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
}

export default function Households() {
  const [rollups, setRollups] = useState<HouseholdRollup[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [addMemberOpen, setAddMemberOpen] = useState<string | null>(null);

  const [createForm, setCreateForm] = useState({ name: '', primaryMemberId: '', notes: '' });
  const [addForm, setAddForm] = useState({
    memberId: '',
    role: 'Spouse' as HouseholdRole,
    rollUp: true,
  });
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try {
      const [r, m] = await Promise.all([getHouseholdRollups(), getMembers()]);
      setRollups(r);
      setMembers(m);
    } catch {
      toast.error('Failed to load households');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rollups;
    return rollups.filter((r) =>
      `${r.household.name} ${r.members.map((m) => m.member?.firstName + ' ' + m.member?.lastName).join(' ')}`
        .toLowerCase()
        .includes(q),
    );
  }, [rollups, search]);

  const memberIdsInHouseholds = useMemo(() => {
    const set = new Set<string>();
    for (const r of rollups) for (const m of r.members) set.add(m.link.memberId);
    return set;
  }, [rollups]);

  const totals = useMemo(() => {
    return {
      count: rollups.length,
      membersInHouseholds: memberIdsInHouseholds.size,
      mrr: rollups.reduce((s, r) => s + r.totalDuesMonthly, 0),
      balance: rollups.reduce((s, r) => s + r.totalHouseBalance, 0),
    };
  }, [rollups, memberIdsInHouseholds]);

  const openCreate = () => {
    setCreateForm({ name: '', primaryMemberId: '', notes: '' });
    setCreateOpen(true);
  };

  const submitCreate = async () => {
    if (!createForm.name.trim() || !createForm.primaryMemberId) {
      toast.error('Name and primary member are required');
      return;
    }
    setBusy(true);
    try {
      await createHousehold({
        name: createForm.name.trim(),
        primaryMemberId: createForm.primaryMemberId,
        notes: createForm.notes.trim() || undefined,
      });
      toast.success('Household created');
      setCreateOpen(false);
      await load();
    } finally {
      setBusy(false);
    }
  };

  const openAddMember = (householdId: string) => {
    setAddForm({ memberId: '', role: 'Spouse', rollUp: true });
    setAddMemberOpen(householdId);
  };

  const submitAddMember = async () => {
    if (!addMemberOpen || !addForm.memberId) {
      toast.error('Pick a member');
      return;
    }
    setBusy(true);
    try {
      await addMemberToHousehold({
        householdId: addMemberOpen,
        memberId: addForm.memberId,
        role: addForm.role,
        rollUpBilling: addForm.rollUp,
      });
      toast.success('Added to household');
      setAddMemberOpen(null);
      await load();
    } finally {
      setBusy(false);
    }
  };

  const removeMember = async (linkId: string) => {
    if (!confirm('Remove this member from the household?')) return;
    await removeMemberFromHousehold(linkId);
    toast.success('Member removed');
    await load();
  };

  const removeHousehold = async (id: string, name: string) => {
    if (!confirm(`Delete household "${name}"? Member records stay, just the grouping is removed.`)) return;
    await deleteHousehold(id);
    toast.success('Household deleted');
    await load();
  };

  const availableMembers = useMemo(
    () => members.filter((m) => !memberIdsInHouseholds.has(m.id)),
    [members, memberIdsInHouseholds],
  );

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-teal-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-cyan-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-teal-500 to-cyan-500 flex items-center justify-center shadow-2xl shadow-teal-500/30">
                    <HomeIcon className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Households</h1>
                    <p className="text-slate-400 mt-1">
                      Family groupings with primary member, sub-members, and roll-up billing.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-teal-500 to-cyan-500 hover:from-teal-600 hover:to-cyan-600 shadow-lg shadow-teal-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  New household
                </Button>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<HomeIcon className="w-5 h-5 text-teal-600" />}
              label="Households"
              value={String(totals.count)}
            />
            <StatCard
              icon={<Users className="w-5 h-5 text-blue-600" />}
              label="Members linked"
              value={String(totals.membersInHouseholds)}
              hint={`${members.length - totals.membersInHouseholds} unlinked`}
            />
            <StatCard
              icon={<DollarSign className="w-5 h-5 text-emerald-600" />}
              label="Household MRR"
              value={formatCurrency(totals.mrr)}
            />
            <StatCard
              icon={<DollarSign className="w-5 h-5 text-rose-600" />}
              label="House balances"
              value={formatCurrency(totals.balance)}
            />
          </div>

          <div className="flex gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search by household or member..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={HomeIcon}
              title={rollups.length === 0 ? 'No households yet' : 'No households match'}
              description={
                rollups.length === 0
                  ? 'Group a primary member with their spouse, partner, or dependents to consolidate billing.'
                  : 'Try clearing the search.'
              }
              actionLabel={rollups.length === 0 ? 'New household' : undefined}
              onAction={rollups.length === 0 ? openCreate : undefined}
              variant="teal"
            />
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              {filtered.map((r) => (
                <article
                  key={r.household.id}
                  className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5"
                >
                  <div className="flex items-start justify-between gap-2 mb-3">
                    <div>
                      <h3 className="font-semibold text-slate-900 flex items-center gap-2">
                        <HomeIcon className="w-4 h-4 text-teal-600" /> The {r.household.name} household
                      </h3>
                      {r.household.notes && (
                        <p className="text-xs text-slate-500 mt-1">{r.household.notes}</p>
                      )}
                    </div>
                    <div className="flex gap-1">
                      <button
                        type="button"
                        onClick={() => openAddMember(r.household.id)}
                        className="rounded p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                        aria-label="Add member"
                      >
                        <UserPlus className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => removeHousehold(r.household.id, r.household.name)}
                        className="rounded p-2 text-slate-400 hover:bg-rose-50 hover:text-rose-600"
                        aria-label="Delete household"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>

                  <ul className="space-y-2 mb-3">
                    {r.members.map((row) => (
                      <li
                        key={row.link.id}
                        className="flex items-center justify-between gap-2 rounded-lg bg-slate-50 px-3 py-2"
                      >
                        <div className="flex items-center gap-2 min-w-0">
                          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-teal-100 to-cyan-100 flex items-center justify-center text-teal-700 font-semibold text-xs shrink-0">
                            {row.member
                              ? (row.member.firstName[0] ?? '') + (row.member.lastName[0] ?? '')
                              : '?'}
                          </div>
                          <div className="min-w-0">
                            <p className="text-sm font-medium text-slate-900 truncate">
                              {row.member
                                ? `${row.member.firstName} ${row.member.lastName}`
                                : 'Unknown member'}
                            </p>
                            <p className="text-xs text-slate-500">
                              {row.link.role}
                              {row.link.rollUpBilling && row.link.role !== 'Primary' && (
                                <span className="ml-1.5 text-teal-600">· rolls up</span>
                              )}
                            </p>
                          </div>
                        </div>
                        {row.link.role !== 'Primary' && (
                          <button
                            type="button"
                            onClick={() => removeMember(row.link.id)}
                            className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 shrink-0"
                            aria-label="Remove from household"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        )}
                      </li>
                    ))}
                  </ul>

                  <div className="grid grid-cols-2 gap-3 pt-3 border-t border-slate-100">
                    <div>
                      <p className="text-xs text-slate-500">Total MRR</p>
                      <p className="font-semibold text-slate-900">{formatCurrency(r.totalDuesMonthly)}</p>
                    </div>
                    <div>
                      <p className="text-xs text-slate-500">House balance</p>
                      <p
                        className={`font-semibold ${
                          r.totalHouseBalance < 0 ? 'text-rose-600' : 'text-slate-900'
                        }`}
                      >
                        {formatCurrency(r.totalHouseBalance)}
                      </p>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </main>
      </PageTransition>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>New household</DialogTitle>
            <DialogDescription>
              Pick the primary member — they'll receive consolidated bills for the household.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Household name</Label>
              <Input
                value={createForm.name}
                onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
                placeholder="e.g. Hartwell, Okafor"
              />
            </div>
            <div>
              <Label>Primary member</Label>
              <Select
                value={createForm.primaryMemberId}
                onValueChange={(v) => setCreateForm({ ...createForm, primaryMemberId: v })}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {availableMembers.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} — {m.tier}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Notes</Label>
              <Input
                value={createForm.notes}
                onChange={(e) => setCreateForm({ ...createForm, notes: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submitCreate} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Creating
                </>
              ) : (
                'Create household'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={addMemberOpen !== null} onOpenChange={(o) => !o && setAddMemberOpen(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Add household member</DialogTitle>
            <DialogDescription>Link a spouse, partner, or dependent member.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Member</Label>
              <Select
                value={addForm.memberId}
                onValueChange={(v) => setAddForm({ ...addForm, memberId: v })}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {availableMembers.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} — {m.tier}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Role</Label>
              <Select
                value={addForm.role}
                onValueChange={(v) => setAddForm({ ...addForm, role: v as HouseholdRole })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {HOUSEHOLD_ROLES.filter((r) => r !== 'Primary').map((r) => (
                    <SelectItem key={r} value={r}>
                      {r}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center justify-between bg-slate-50 rounded-lg px-3 py-2">
              <div>
                <p className="text-sm font-medium text-slate-900">Roll up billing</p>
                <p className="text-xs text-slate-500">
                  Charges this member incurs land on the primary member's account.
                </p>
              </div>
              <Switch
                checked={addForm.rollUp}
                onCheckedChange={(c) => setAddForm({ ...addForm, rollUp: c })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddMemberOpen(null)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submitAddMember} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Adding
                </>
              ) : (
                'Add member'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function StatCard({
  icon,
  label,
  value,
  hint,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <div className="flex items-center justify-between mb-2">
        <div className="w-10 h-10 rounded-xl bg-slate-50 flex items-center justify-center">{icon}</div>
      </div>
      <p className="text-2xl font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500 mt-0.5">{label}</p>
      {hint && <p className="text-xs text-slate-400 mt-1">{hint}</p>}
    </div>
  );
}

