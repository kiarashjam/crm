import { describe, expect, it } from 'vitest';
import { blanksIn, fillPlaceholders, mergeValues, splitBlanks, unknownPlaceholders } from './mergeText';

const values = mergeValues({ name: 'Jean-Michel Dupont', email: 'jm@example.ch', company: '' }, 'Claire Martin', 'Pavillon 46');

describe('a prepared email for one person', () => {
  it('fills the placeholders as the outbox does, whatever their case, with their fallback when there is no value', () => {
    const { text, unfilled } = fillPlaceholders('Bonjour {{FirstName}} {{lastName}}, de {{company|votre société}} — {{senderName}}, {{workspace}}', values);
    expect(text).toBe('Bonjour Jean-Michel Dupont, de votre société — Claire Martin, Pavillon 46');
    expect(unfilled).toEqual([]);
  });

  it('names a placeholder it cannot fill, once', () => {
    expect(fillPlaceholders('{{company}} {{company}}', values).unfilled).toEqual(['{{company}}']);
  });

  it('knows the placeholders the outbox does not fill', () => {
    expect(unknownPlaceholders('{{firstname}} {{first_name}} {{date}}', ['firstName'])).toEqual(['{{first_name}}', '{{date}}']);
  });

  it('finds the blanks left to fill in, once each, and marks them in the text', () => {
    expect(blanksIn('Le [[date]] à [[heure]], [[date]]')).toEqual(['date', 'heure']);
    expect(splitBlanks('Le [[date]] à midi')).toEqual([
      { text: 'Le ', blank: false }, { text: 'date', blank: true }, { text: ' à midi', blank: false },
    ]);
  });
});
