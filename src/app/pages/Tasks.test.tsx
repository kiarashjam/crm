// A change to a task's assignee or deal, made from its card or its detail view, and
// refused by the server: someone outside the workspace, or who cannot open the task's
// private deal. The change is shown at once, so a refusal has to take it back.
import { Children, isValidElement, type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { TaskItem } from '@/app/api/types';
import { withQueries } from '@/test/queryWrapper';

const REFUSED = 'The person you picked cannot see this private deal, so they could not open the task.';

const api = {
  getTasks: vi.fn(), getTaskStats: vi.fn(), getLeads: vi.fn(), getDeals: vi.fn(), getContacts: vi.fn(),
  getOrgMembers: vi.fn(), assignTask: vi.fn(), linkTaskToDeal: vi.fn(), linkTaskToLead: vi.fn(), createActivity: vi.fn(),
  createTask: vi.fn(),
};
vi.mock('@/app/api', async () => {
  const actual = await vi.importActual<typeof import('@/app/api')>('@/app/api');
  return { ...actual, ...api };
});
vi.mock('@/app/api/activities', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/activities')>('@/app/api/activities');
  return { ...actual, getActivitiesByLead: async () => [], getActivitiesByDeal: async () => [] };
});
vi.mock('@/app/lib/auth', async () => {
  const actual = await vi.importActual<typeof import('@/app/lib/auth')>('@/app/lib/auth');
  return { ...actual, getCurrentOrganizationId: () => 'org-1' };
});
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

// The card, reduced to what it shows and the two changes it offers.
vi.mock('./tasks/components/KanbanTaskCard', () => ({
  KanbanTaskCard: ({ task, handleAssigneeChange, handleDealChange, onViewDetails }: {
    task: TaskItem;
    handleAssigneeChange: (t: TaskItem, id: string | null) => void;
    handleDealChange: (t: TaskItem, id: string | null) => void;
    onViewDetails?: (t: TaskItem) => void;
  }) => (
    <article aria-label={`Card ${task.title}`}>
      <p>Assignee: {task.assigneeName ?? 'nobody'}</p>
      <p>Deal: {task.dealName ?? 'none'}</p>
      <button type="button" onClick={() => handleAssigneeChange(task, 'u-rep')}>Give to Rep</button>
      <button type="button" onClick={() => handleDealChange(task, 'd-secret')}>Link Secret</button>
      <button type="button" onClick={() => onViewDetails?.(task)}>Details</button>
    </article>
  ),
}));

// The detail view's pickers as plain selects, which jsdom can drive.
const textOf = (node: ReactNode): string =>
  Children.toArray(node).map((child) => (typeof child === 'string' || typeof child === 'number'
    ? String(child)
    : isValidElement<{ children?: ReactNode }>(child) ? textOf(child.props.children) : '')).join('').trim();
vi.mock('@/app/components/ui/select', () => ({
  Select: ({ value, onValueChange, children }: { value?: string; onValueChange?: (v: string) => void; children?: ReactNode }) => (
    <select value={value} onChange={(e) => onValueChange?.(e.target.value)}>{children}</select>
  ),
  SelectTrigger: () => null,
  SelectValue: () => null,
  SelectContent: ({ children }: { children?: ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children?: ReactNode }) => <option value={value}>{textOf(children)}</option>,
}));

const Tasks = (await import('./Tasks')).default;

const TASK: TaskItem = {
  id: 't-1', title: 'Send the offer', status: 'todo', priority: 'none', completed: false,
  createdAtUtc: '2026-10-01T09:00:00Z',
} as TaskItem;

const show = () => render(withQueries(<MemoryRouter><Tasks /></MemoryRouter>));
/** The read was handed a signal, so leaving the page or switching scope cancels it. */
const cancellable = expect.objectContaining({ signal: expect.any(AbortSignal) });
// The open detail view hides the page behind it from the accessibility tree.
const card = () => screen.getByRole('article', { name: 'Card Send the offer', hidden: true });
const assigneePicker = () => {
  const picker = screen.getAllByRole('combobox').find((s) => s.querySelector('option[value="u-rep"]'));
  if (!picker) throw new Error('the detail view has no assignee picker');
  return picker as HTMLSelectElement;
};

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  api.getTasks.mockResolvedValue([TASK]);
  api.getTaskStats.mockResolvedValue({ total: 1, todo: 1, inProgress: 0, completed: 0, overdue: 0, dueToday: 0 });
  api.getLeads.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([{ id: 'd-secret', name: 'Secret', value: '1', createdAtUtc: '2026-10-01T09:00:00Z' }]);
  api.getContacts.mockResolvedValue([]);
  api.getOrgMembers.mockResolvedValue([{ userId: 'u-rep', name: 'Rep', email: 'rep@club.ch' }]);
  api.createActivity.mockResolvedValue(null);
});

describe('a refused change on a task', () => {
  it('puts the card back and says why', async () => {
    const { ApiError } = await import('@/app/api/apiClient');
    api.assignTask.mockRejectedValue(new ApiError(REFUSED, 400, '/api/tasks/t-1/assign', true));
    api.linkTaskToDeal.mockRejectedValue(new ApiError(REFUSED, 400, '/api/tasks/t-1/link-deal', true));
    show();
    await userEvent.click(await within(await screen.findByRole('article', { name: 'Card Send the offer' })).findByRole('button', { name: 'Give to Rep' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(REFUSED));
    expect(within(card()).getByText('Assignee: nobody')).toBeInTheDocument();

    await userEvent.click(within(card()).getByRole('button', { name: 'Link Secret' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledTimes(2));
    expect(within(card()).getByText('Deal: none')).toBeInTheDocument();
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('leaves the detail view showing what is saved', async () => {
    const { ApiError } = await import('@/app/api/apiClient');
    api.assignTask.mockRejectedValue(new ApiError(REFUSED, 400, '/api/tasks/t-1/assign', true));
    show();
    await userEvent.click(await within(await screen.findByRole('article', { name: 'Card Send the offer' })).findByRole('button', { name: 'Details' }));

    await userEvent.selectOptions(await waitFor(assigneePicker), 'u-rep');

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(REFUSED));
    expect(toast.error).toHaveBeenCalledTimes(1);
    expect(assigneePicker().value).toBe('none');
    expect(within(card()).getByText('Assignee: nobody')).toBeInTheDocument();
  });

  it('a change the server takes stays', async () => {
    api.assignTask.mockResolvedValue({ ...TASK, assigneeId: 'u-rep', assigneeName: 'Rep' });
    show();
    await userEvent.click(await within(await screen.findByRole('article', { name: 'Card Send the offer' })).findByRole('button', { name: 'Details' }));

    await userEvent.selectOptions(await waitFor(assigneePicker), 'u-rep');

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Task assigned'));
    expect(assigneePicker().value).toBe('u-rep');
    expect(within(card()).getByText('Assignee: Rep')).toBeInTheDocument();
    expect(toast.error).not.toHaveBeenCalled();
  });
});

describe('whose tasks', () => {
  it("opens on my tasks and their figures, and shows the team's on the switch, remembered", async () => {
    localStorage.clear();
    const { unmount } = show();
    await screen.findByRole('article', { name: 'Card Send the offer' });

    expect(api.getTasks).toHaveBeenCalledWith({ scope: 'mine' }, cancellable);
    expect(api.getTaskStats).toHaveBeenCalledWith('mine', cancellable);
    expect(screen.getByRole('button', { name: 'My tasks' })).toHaveAttribute('aria-pressed', 'true');

    await userEvent.click(screen.getByRole('button', { name: 'Team' }));

    await waitFor(() => expect(api.getTasks).toHaveBeenLastCalledWith({ scope: 'team' }, cancellable));
    await waitFor(() => expect(api.getTaskStats).toHaveBeenLastCalledWith('team', cancellable));
    unmount();

    api.getTasks.mockClear();
    show();
    await screen.findByRole('article', { name: 'Card Send the offer' });
    expect(api.getTasks).toHaveBeenCalledWith({ scope: 'team' }, cancellable);
  });
});

describe('when a read fails', () => {
  it('says the tasks could not be loaded, instead of "No tasks yet", and loads them on retry', async () => {
    api.getTasks.mockRejectedValueOnce(new Error('The server is not answering.'));
    show();

    expect(await screen.findByText('Your tasks could not be loaded.')).toBeInTheDocument();
    expect(screen.getByText('The server is not answering.')).toBeInTheDocument();
    expect(screen.queryByText('No tasks yet')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('article', { name: 'Card Send the offer' })).toBeInTheDocument();
    expect(screen.queryByText('Your tasks could not be loaded.')).toBeNull();
  });

  it('still says so after a task is created, rather than showing that task as the whole list', async () => {
    // A list that was never read is not a list. The new task used to be written into
    // it, and the page showed a board of that one task, as fresh, and no word that
    // every other task was missing.
    api.getTasks.mockRejectedValue(new Error('The server is not answering.'));
    api.createTask.mockResolvedValue({ ...TASK, id: 't-new', title: 'Call the Müllers' });
    show();
    expect(await screen.findByText('Your tasks could not be loaded.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /New Task/ }));
    await userEvent.type(screen.getByLabelText(/Task Title/), 'Call the Müllers');
    await userEvent.click(screen.getByRole('button', { name: /Create Task/ }));
    await waitFor(() => expect(toast.success).toHaveBeenCalled());

    expect(screen.getByText('Your tasks could not be loaded.')).toBeInTheDocument();
    expect(screen.queryByRole('article', { name: 'Card Call the Müllers', hidden: true })).toBeNull();
  });

  it('still shows the tasks when a picker list fails', async () => {
    // One slow list used to blank the whole page.
    api.getContacts.mockRejectedValue(new Error('timeout'));
    api.getLeads.mockRejectedValue(new Error('timeout'));
    show();

    expect(await screen.findByRole('article', { name: 'Card Send the offer' })).toBeInTheDocument();
    expect(screen.queryByText(/could not be loaded/)).toBeNull();
  });
});
