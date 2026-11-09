// When an activity happened and how it went: what the forms send, and what they stop
// before the server would refuse it.
import { describe, expect, it } from 'vitest';
import {
  activityFactsLine, activityTime, factsPayload, factsProblem, factsUpdatePayload, isOurResponse, outcomesFor, EMPTY_FACTS,
} from './activityFacts';
import { toLocalInputValue } from './dateInputs';

const NOW = new Date('2026-11-09T15:00:00Z');

describe('factsPayload', () => {
  it('sends only what the kind of activity can carry', () => {
    const draft = { when: '', outcome: 'no_answer', direction: 'outbound' as const, duration: '3' };

    expect(factsPayload('call', draft)).toEqual({ outcome: 'no_answer', direction: 'outbound', durationMinutes: 3 });
    // A note has none of them, whatever the form still holds from when it was a call.
    expect(factsPayload('note', draft)).toEqual({});
    // A meeting has a duration, but no direction, and no call outcome.
    expect(factsPayload('meeting', draft)).toEqual({ durationMinutes: 3 });
  });

  it('leaves the time out when none was picked, so the server stamps the moment it is saved', () => {
    expect(factsPayload('call', EMPTY_FACTS)).toEqual({});
  });

  it('sends a picked time as UTC', () => {
    const when = toLocalInputValue('2026-11-09T08:30:00Z');
    expect(factsPayload('note', { ...EMPTY_FACTS, when }).occurredAtUtc).toBe('2026-11-09T08:30:00.000Z');
  });
});

describe('factsUpdatePayload', () => {
  it('clears what the form emptied, so an edit can take an outcome away', () => {
    expect(factsUpdatePayload('call', { ...EMPTY_FACTS, outcome: 'busy' })).toEqual({
      outcome: 'busy', clearDuration: true, clearDirection: true,
    });
  });
});

describe('factsProblem', () => {
  it('stops a time in the future: what is planned is a task', () => {
    const when = toLocalInputValue('2026-11-10T09:00:00Z');
    expect(factsProblem('meeting', { ...EMPTY_FACTS, when }, NOW)).toMatch(/task/);
  });

  it('allows a time that has passed', () => {
    const when = toLocalInputValue('2026-11-08T09:00:00Z');
    expect(factsProblem('meeting', { ...EMPTY_FACTS, when }, NOW)).toBeNull();
  });

  it.each(['-1', '1441', '2.5', 'ten'])('stops a duration of %s', (duration) => {
    expect(factsProblem('call', { ...EMPTY_FACTS, duration }, NOW)).toMatch(/minutes/);
  });

  it('ignores a duration left over on a kind that has none', () => {
    expect(factsProblem('note', { ...EMPTY_FACTS, duration: '-1' }, NOW)).toBeNull();
  });
});

describe('reading an activity back', () => {
  it('says how it went in a few words', () => {
    expect(activityFactsLine({ direction: 'inbound', outcome: 'left_voicemail', durationMinutes: 1 })).toBe('Inbound · Left voicemail · 1 min');
    expect(activityFactsLine({})).toBe('');
  });

  it('dates it by when it happened, or else when it was logged', () => {
    expect(activityTime({ occurredAt: '2026-11-09T08:00:00Z', createdAt: '2026-11-09T20:00:00Z' })).toBe('2026-11-09T08:00:00Z');
    expect(activityTime({ createdAt: '2026-11-09T20:00:00Z' })).toBe('2026-11-09T20:00:00Z');
  });

  it('offers the outcomes the server takes for each kind', () => {
    expect(outcomesFor('Call')).toContain('no_answer');
    expect(outcomesFor('demo')).toContain('no_show');
    expect(outcomesFor('email')).toEqual([]);
  });
});

describe('isOurResponse', () => {
  it('does not count an email they sent, or their call nobody picked up', () => {
    expect(isOurResponse('email', 'inbound')).toBe(false);
    expect(isOurResponse('call', 'inbound', 'no_answer')).toBe(false);
    expect(isOurResponse('call', 'inbound', 'connected')).toBe(true);
    expect(isOurResponse('call', 'outbound', 'no_answer')).toBe(true);
    expect(isOurResponse('meeting')).toBe(true);
  });
});
