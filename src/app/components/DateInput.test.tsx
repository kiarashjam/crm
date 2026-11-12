// A day typed the way the workspace's region writes it. The browser's own date field
// follows the browser: a Swiss club's front desk on an English (US) laptop was offered
// mm/dd/yyyy, and 03/04 meant two different days to the field and to the person.

import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DateInput, dateOrder, parseDay, writeDay } from './DateInput';

describe('reading a typed day', () => {
  it('reads Swiss dates day first, with a two-digit year in this century', () => {
    expect(parseDay('31.12.2026', 'de-CH')).toBe('2026-12-31');
    expect(parseDay('1.2.26', 'fr-CH')).toBe('2026-02-01');
    expect(parseDay('03.04.2026', 'en-CH')).toBe('2026-04-03');
    expect(parseDay('31122026', 'de-CH')).toBe('2026-12-31');
    expect(parseDay('311226', 'de-CH')).toBe('2026-12-31');
  });

  it('reads ISO in any locale, and month first only where the region writes it so', () => {
    expect(parseDay('2026-12-31', 'de-CH')).toBe('2026-12-31');
    expect(parseDay('12/31/2026', 'en-US')).toBe('2026-12-31');
    expect(parseDay('12/31/2026', 'de-CH')).toBeNull();
  });

  it('refuses what is not a day', () => {
    expect(parseDay('31.02.2026', 'de-CH')).toBeNull();
    expect(parseDay('31.12', 'de-CH')).toBeNull();
    expect(parseDay('soon', 'de-CH')).toBeNull();
    expect(parseDay('', 'de-CH')).toBeNull();
  });

  it('writes a day in the region’s order', () => {
    expect(writeDay('2026-12-31', 'de-CH')).toBe('31.12.2026');
    expect(writeDay('2026-12-31', 'en-US')).toBe('12/31/2026');
    expect(dateOrder('fr-CH').order).toEqual(['day', 'month', 'year']);
  });
});

function Field({ initial = '', onChange }: { initial?: string; onChange: (v: string) => void }) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <label htmlFor="due">Due date</label>
      <DateInput id="due" locale="de-CH" value={value} onChange={(v) => { setValue(v); onChange(v); }} />
    </>
  );
}

describe('the date field', () => {
  it('shows the saved day the Swiss way and hands the page yyyy-mm-dd when one is typed', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Field initial="2026-12-31" onChange={onChange} />);

    const field = screen.getByLabelText('Due date');
    expect(field).toHaveValue('31.12.2026');
    await user.clear(field);
    expect(onChange).toHaveBeenLastCalledWith('');
    await user.type(field, '03.04.2027');
    expect(onChange).toHaveBeenLastCalledWith('2027-04-03');
    await user.tab();
    expect(field).toHaveValue('03.04.2027');
  });

  it('says so when what was typed is not a day, and keeps it to be corrected', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Field onChange={onChange} />);

    const field = screen.getByLabelText('Due date');
    await user.type(field, '12/31/2026');
    await user.tab();
    expect(field).toHaveValue('12/31/2026');
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Not a date. Write it as dd.mm.yyyy.')).toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalledWith(expect.stringMatching(/\d{4}-/));
  });
});
