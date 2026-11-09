// Mentioning a teammate in a comment or a note: "@" offers the workspace's people.
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { matchPeople, mentionQueryAt, MentionTextarea, type MentionPerson } from './MentionTextarea';

const PEOPLE: MentionPerson[] = [
  { userId: 'u-anna', name: 'Anna Keller', email: 'anna@pavillon46.ch' },
  { userId: 'u-luca', name: 'Luca Rossi', email: 'luca@pavillon46.ch' },
  { userId: 'u-marc', name: 'Marc Weber', email: 'marc.weber@pavillon46.ch' },
];

function Box({ onKeyDown }: { onKeyDown?: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void }) {
  const [value, setValue] = useState('');
  return (
    <>
      <MentionTextarea aria-label="Comment" value={value} onChange={setValue} people={PEOPLE} onKeyDown={onKeyDown} />
      <output>{value}</output>
      <button type="button" onClick={() => setValue('')}>Send</button>
    </>
  );
}

describe('mentionQueryAt', () => {
  it('finds what is typed after an @ at the start or after a space', () => {
    expect(mentionQueryAt('@an', 3)).toEqual({ start: 0, query: 'an' });
    expect(mentionQueryAt('Thanks @Luca R', 14)).toEqual({ start: 7, query: 'Luca R' });
  });

  it('takes an @ straight after a letter for an email address, as the server does', () => {
    expect(mentionQueryAt('bob@an', 6)).toBeNull();
  });

  it('stops at a new line or a space right after the @', () => {
    expect(mentionQueryAt('@an\nx', 5)).toBeNull();
    expect(mentionQueryAt('@ an', 4)).toBeNull();
  });
});

describe('matchPeople', () => {
  it('matches any word of the name, or the address', () => {
    expect(matchPeople(PEOPLE, 'ke').map((p) => p.userId)).toEqual(['u-anna']);
    expect(matchPeople(PEOPLE, 'marc.w').map((p) => p.userId)).toEqual(['u-marc']);
    expect(matchPeople(PEOPLE, '')).toHaveLength(3);
  });
});

describe('MentionTextarea', () => {
  it('offers matching teammates after @ and writes the chosen one as @Full Name', async () => {
    const user = userEvent.setup();
    render(<Box />);

    await user.type(screen.getByRole('textbox', { name: 'Comment' }), 'Can @an');

    expect(screen.getByRole('option', { name: /Anna Keller/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Luca Rossi/ })).toBeNull();
    await user.keyboard('{Enter}');

    expect(screen.getByRole('status')).toHaveTextContent('Can @Anna Keller');
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('offers people again in the next comment, after one was chosen at the same place', async () => {
    const user = userEvent.setup();
    render(<Box />);
    const box = screen.getByRole('textbox', { name: 'Comment' });
    await user.type(box, '@an');
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    await user.type(box, '@lu');

    expect(screen.getByRole('option', { name: /Luca Rossi/ })).toBeInTheDocument();
  });

  it('can be steered with the arrow keys', async () => {
    const user = userEvent.setup();
    render(<Box />);

    await user.type(screen.getByRole('textbox', { name: 'Comment' }), '@');
    await user.keyboard('{ArrowDown}{Enter}');

    expect(screen.getByRole('status')).toHaveTextContent('@Luca Rossi');
  });

  it('offers nobody inside an email address, and closes on Escape', async () => {
    const user = userEvent.setup();
    render(<Box />);
    const box = screen.getByRole('textbox', { name: 'Comment' });

    await user.type(box, 'bob@an');
    expect(screen.queryByRole('listbox')).toBeNull();

    await user.clear(box);
    await user.type(box, '@lu');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('leaves Ctrl+Enter to the page, to send', async () => {
    const user = userEvent.setup();
    const onKeyDown = vi.fn();
    render(<Box onKeyDown={onKeyDown} />);

    await user.type(screen.getByRole('textbox', { name: 'Comment' }), 'Done');
    await user.keyboard('{Control>}{Enter}{/Control}');

    expect(onKeyDown).toHaveBeenCalledWith(expect.objectContaining({ key: 'Enter', ctrlKey: true }));
  });
});
