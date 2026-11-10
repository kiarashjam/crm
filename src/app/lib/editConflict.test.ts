// A save refused because someone else saved the record first is told apart from every
// other refusal, and offers the latest copy.

import { describe, it, expect, vi, beforeEach } from 'vitest';

const toast = vi.hoisted(() => ({ error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { isEditConflict, showEditConflict } = await import('./editConflict');
const { ApiError } = await import('@/app/api/apiClient');

beforeEach(() => toast.error.mockReset());

describe('isEditConflict', () => {
  it('is a 409 whose code says the record changed', () => {
    expect(isEditConflict(new ApiError('changed', 409, '/api/deals/1', true, 'Deal.Changed.Conflict'))).toBe(true);
    // The global handler's code, for a race no service put into words.
    expect(isEditConflict(new ApiError('changed', 409, '/api/leads/1', true, 'Record.Changed.Conflict'))).toBe(true);
  });

  it('is not any other refusal', () => {
    expect(isEditConflict(new ApiError('dup', 409, '/api/contacts/1', true, 'Contact.DuplicateEmail'))).toBe(false);
    expect(isEditConflict(new ApiError('closed', 409, '/api/tabs/1', true, 'Tab.Closed.Conflict'))).toBe(false);
    expect(isEditConflict(new ApiError('bad', 400, '/api/deals/1', true, 'Deal.Changed.Conflict'))).toBe(false);
    expect(isEditConflict(new Error('Deal.Changed.Conflict'))).toBe(false);
  });
});

describe('showEditConflict', () => {
  it("says the server's sentence and offers a Reload that brings the latest copy", () => {
    const reload = vi.fn();

    showEditConflict('Someone else changed this deal after you opened it.', reload);

    const [message, options] = toast.error.mock.calls[0] as [string, { action: { label: string; onClick: () => void } }];
    expect(message).toBe('Someone else changed this deal after you opened it.');
    expect(options.action.label).toBe('Reload');
    options.action.onClick();
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('says what happened when the server gave no sentence', () => {
    showEditConflict(undefined, vi.fn());
    expect(toast.error.mock.calls[0]![0]).toMatch(/someone else changed this/i);
  });
});
