// Lead routing, as an owner or a manager sets it up — and as a member sees it.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const ops = {
  getSla: vi.fn(), getScoringRules: vi.fn(), getAssignmentRules: vi.fn(), getTerritories: vi.fn(),
  getQueues: vi.fn(), getCaptureForms: vi.fn(), getConversionReport: vi.fn(),
  saveSla: vi.fn(), saveScoringRules: vi.fn(), saveAssignmentRule: vi.fn(), saveTerritory: vi.fn(),
  saveQueue: vi.fn(), createCaptureForm: vi.fn(),
  deleteAssignmentRule: vi.fn(), deleteTerritory: vi.fn(), deleteQueue: vi.fn(),
};
vi.mock('@/app/api/leadOps', () => ops);

const members = vi.fn();
vi.mock('@/app/api/organizations', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/organizations')>('@/app/api/organizations');
  return { ...actual, getOrgMembers: (...a: unknown[]) => members(...a) };
});

let live = true;
vi.mock('@/app/api/apiClient', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/apiClient')>('@/app/api/apiClient');
  return { ...actual, isUsingRealApi: () => live, servesDemoData: () => !live };
});

let role = 0;
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrgId: 'org-1', currentOrg: { id: 'org-1', name: 'Club', ownerUserId: 'u-own', isOwner: role === 0, role } }),
}));

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const LeadRouting = (await import('./LeadRouting')).default;

const show = () => render(<MemoryRouter><LeadRouting /></MemoryRouter>);
const section = (name: string) => screen.getByRole('heading', { name: new RegExp(`^${name}`) }).closest('section') as HTMLElement;

const TEAM = [
  { userId: 'u-ann', name: 'Ann', email: 'ann@club.ch', role: 0 },
  { userId: 'u-bob', name: 'Bob', email: 'bob@club.ch', role: 1 },
  { userId: 'u-viv', name: 'Viv', email: 'viv@club.ch', role: 3 },
];

beforeEach(() => {
  live = true;
  role = 0;
  for (const fn of Object.values(ops)) fn.mockReset();
  members.mockReset().mockResolvedValue(TEAM);
  toast.success.mockReset();
  toast.error.mockReset();
  ops.getSla.mockResolvedValue({ hours: 6 });
  ops.getScoringRules.mockResolvedValue([{ id: 'r1', field: 'source', matchValue: 'Website', points: 10, isActive: true }]);
  ops.getAssignmentRules.mockResolvedValue([]);
  ops.getTerritories.mockResolvedValue([
    { id: 't-fr', name: 'Romandie', countries: 'Switzerland' },
    { id: 't-de', name: 'Germany', countries: 'Germany' },
  ]);
  ops.getQueues.mockResolvedValue([]);
  ops.getCaptureForms.mockResolvedValue([]);
  ops.getConversionReport.mockResolvedValue({
    bySource: [],
    byOwner: [{ key: 'u-bob', created: 4, qualified: 2, converted: 1, conversionRate: 0.25 }],
  });
});

describe('lead routing', () => {
  it('a section that failed to load cannot be saved over the real settings', async () => {
    // These used to load in one Promise.all: one failure left the score rules empty,
    // and "Save" then wrote the empty list over them.
    ops.getScoringRules.mockRejectedValue(new Error('500'));
    show();
    const scoring = section('Score rules');
    expect(await within(scoring).findByText(/could not be loaded/)).toBeInTheDocument();
    expect(within(scoring).queryByRole('button', { name: /Save score rules/ })).toBeNull();
    // The other sections are unaffected.
    expect(await within(section('First response')).findByDisplayValue('6')).toBeInTheDocument();

    ops.getScoringRules.mockResolvedValue([]);
    await userEvent.setup().click(within(scoring).getByRole('button', { name: /Try again/ }));
    expect(await within(scoring).findByRole('button', { name: /Save score rules/ })).toBeEnabled();
  });

  it('saving one section does not reload the others, so their unsaved edits survive', async () => {
    ops.saveSla.mockResolvedValue({ hours: 8 });
    show();
    const user = userEvent.setup();
    const sla = within(section('First response'));
    const hours = await sla.findByDisplayValue('6');
    const matchValue = await within(section('Score rules')).findByDisplayValue('Website');
    await user.clear(matchValue);
    await user.type(matchValue, 'Referral');

    await user.clear(hours);
    await user.type(hours, '8');
    await user.click(sla.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Response deadline saved'));
    expect(ops.getScoringRules).toHaveBeenCalledTimes(1);
    expect(within(section('Score rules')).getByDisplayValue('Referral')).toBeInTheDocument();
  });

  it('a refused save says why', async () => {
    ops.saveSla.mockRejectedValue(new Error('Only owners and managers can change lead routing.'));
    show();
    const sla = within(section('First response'));
    await sla.findByDisplayValue('6');
    await userEvent.setup().click(sla.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Only owners and managers can change lead routing.'));
  });

  it('queues are filled in by name, and Viewers are not offered', async () => {
    ops.saveQueue.mockImplementation(async (q: object) => ({ id: 'q1', ...q }));
    show();
    const user = userEvent.setup();
    const queues = within(section('Queues'));
    await queues.findByLabelText('Bob');
    expect(queues.queryByLabelText('Viv')).toBeNull();

    await user.type(queues.getByLabelText('Queue name'), 'Inbound');
    await user.click(queues.getByLabelText('Bob'));
    await user.click(queues.getByRole('button', { name: 'Add queue' }));

    expect(ops.saveQueue).toHaveBeenCalledWith({ name: 'Inbound', memberUserIds: ['u-bob'] });
    expect(await queues.findByText('Bob', { selector: 'span' })).toBeInTheDocument();
  });

  it('a territory rule uses the territory chosen, not the first one', async () => {
    ops.saveAssignmentRule.mockImplementation(async (r: object) => ({ id: 'a1', memberUserIds: [], ...r }));
    show();
    const user = userEvent.setup();
    const rules = within(section('Assignment rules'));
    await user.type(await rules.findByLabelText('Name'), 'German leads');
    await user.selectOptions(rules.getByLabelText('Matches'), 'Territory');
    await user.selectOptions(rules.getByLabelText('Territory'), 'Germany');
    await user.click(rules.getByRole('button', { name: 'Add rule' }));

    expect(ops.saveAssignmentRule).toHaveBeenCalledWith(expect.objectContaining({ kind: 'Territory', territoryId: 't-de' }));
    expect(await rules.findByText(/territory Germany/)).toBeInTheDocument();
  });

  it('rules, territories and queues can be deleted', async () => {
    ops.getAssignmentRules.mockResolvedValue([{ id: 'a1', name: 'Everyone', kind: 'RoundRobin', priority: 0, memberUserIds: [], isActive: true }]);
    ops.deleteAssignmentRule.mockResolvedValue(undefined);
    ops.deleteTerritory.mockResolvedValue(undefined);
    show();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Delete rule Everyone' }));
    await user.click(screen.getByRole('button', { name: 'Delete territory Romandie' }));

    expect(ops.deleteAssignmentRule).toHaveBeenCalledWith('a1');
    expect(ops.deleteTerritory).toHaveBeenCalledWith('t-fr');
    await waitFor(() => expect(screen.queryByText('Everyone')).toBeNull());
    expect(screen.queryByText('Romandie')).toBeNull();
  });

  it('score rules say what they check, including the stage reached', async () => {
    show();
    const scoring = within(section('Score rules'));
    const field = await scoring.findByLabelText('Rule 1: what it checks');
    expect(within(field).getByRole('option', { name: 'Reached stage' })).toHaveValue('stageAtLeast');
  });

  it('the report shows owners by name, not by id', async () => {
    show();
    const report = within(section('How leads convert'));
    expect(await report.findByText('Bob')).toBeInTheDocument();
    expect(report.queryByText('u-bob')).toBeNull();
  });

  it('a member sees the setup but cannot change it', async () => {
    role = 1;
    show();
    expect(await screen.findByText(/Only owners and managers can change lead routing/)).toBeInTheDocument();
    expect(await within(section('First response')).findByRole('button', { name: 'Save' })).toBeDisabled();
    expect(within(section('Queues')).getByLabelText('Queue name')).toBeDisabled();
  });

  it('in demo mode it says routing needs the live workspace, and calls nothing', () => {
    live = false;
    show();
    expect(screen.getByText(/not available in demo mode/)).toBeInTheDocument();
    expect(ops.getSla).not.toHaveBeenCalled();
  });
});
