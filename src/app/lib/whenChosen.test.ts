// A select's change handler ignores the empty value Radix reports by itself, and passes
// on every value someone chose, the "none" sentinel included.

import { describe, it, expect, vi } from 'vitest';
import { whenChosen } from './whenChosen';

describe('whenChosen', () => {
  it('passes on a chosen value, "none" included', () => {
    const handler = vi.fn();
    const onValueChange = whenChosen(handler);

    onValueChange('co-1');
    onValueChange('none');

    expect(handler.mock.calls).toEqual([['co-1'], ['none']]);
  });

  it('ignores the "" the hidden native select falls back to', () => {
    const handler = vi.fn();

    whenChosen(handler)('');

    expect(handler).not.toHaveBeenCalled();
  });
});
