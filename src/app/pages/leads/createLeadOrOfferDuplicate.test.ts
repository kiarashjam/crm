// Adding a lead whose email an open lead already has.
import { beforeEach, describe, expect, it, vi } from 'vitest';

const create = vi.fn();
vi.mock('@/app/api/leads', () => ({ createLead: (...a: unknown[]) => create(...a) }));
const toast = { warning: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const { ApiError } = await import('@/app/api/apiClient');
const { createLeadOrOfferDuplicate } = await import('./createLeadOrOfferDuplicate');

const params = { name: 'Jean', email: 'jean@example.ch' };

beforeEach(() => {
  create.mockReset();
  toast.warning.mockReset();
  toast.error.mockReset();
});

describe('createLeadOrOfferDuplicate', () => {
  it('names the existing lead and offers to create this one anyway', async () => {
    create.mockRejectedValueOnce(new ApiError('An open lead with this email already exists (Jean Dupont).', 409));
    const onAnyway = vi.fn();
    await expect(createLeadOrOfferDuplicate(params, onAnyway)).resolves.toBe('offered');

    const [message, options] = toast.warning.mock.calls[0]!;
    expect(message).toBe('An open lead with this email already exists (Jean Dupont).');
    expect(options.action.label).toBe('Create anyway');

    create.mockResolvedValueOnce({ id: 'l2', ...params });
    options.action.onClick();
    await vi.waitFor(() => expect(onAnyway).toHaveBeenCalledWith(expect.objectContaining({ id: 'l2' })));
    expect(create).toHaveBeenLastCalledWith({ ...params, allowDuplicate: true });
  });

  it('other failures still throw', async () => {
    create.mockRejectedValueOnce(new ApiError('Email is invalid', 400));
    await expect(createLeadOrOfferDuplicate(params, vi.fn())).rejects.toThrow('Email is invalid');
    expect(toast.warning).not.toHaveBeenCalled();
  });
});
