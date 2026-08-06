// The screen-reader half of the phase transition.
//
// The choreography says "done → next" visually in 480ms. This says the same
// thing in words, and it is deliberately on its OWN timer: an utterance needs
// far longer to be read than an animation needs to play, and tying the two
// together would either cut the sentence off or leave a dead window where the
// tracker looks finished but still thinks it is mid-transition.

import { useEffect, useRef, useState } from 'react';
import { PHASE_TITLES } from '../leadPipeline';

/** How long the sentence stays in the live region before being cleared. */
const ANNOUNCE_MS = 2000;

export interface TrackerAnnouncementArgs {
  /** Index of the phase that just completed, from usePhaseTransitions. */
  justCompleted: number | null;
  /** Bumped on every detected transition — the effect's trigger. */
  nonce: number;
  /** 1-based current phase. */
  phase: number;
  lost: string | null;
  complete: boolean;
}

/**
 * Text for a polite live region, or '' when there is nothing to say.
 *
 * Announces two independent things: a phase completing, and the pipeline
 * stopping or reopening. The second matters because a lead can drop out without
 * any phase completing, which is silent from the transition detector's point of
 * view but is the single most important thing that just happened.
 */
export function useTrackerAnnouncement({
  justCompleted,
  nonce,
  phase,
  lost,
  complete,
}: TrackerAnnouncementArgs): string {
  const [message, setMessage] = useState('');
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const previousLost = useRef<string | null | undefined>(undefined);

  const say = (text: string) => {
    setMessage(text);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      setMessage('');
      timer.current = null;
    }, ANNOUNCE_MS);
  };

  // Phase completion. Keyed on the nonce so completing the same phase twice
  // (tick → untick → tick) is announced twice rather than deduped into silence.
  useEffect(() => {
    if (nonce === 0 || justCompleted === null) return;
    const n = justCompleted + 1;
    const title = PHASE_TITLES[justCompleted];
    const head = `Phase ${n} of ${PHASE_TITLES.length}, ${title}, complete.`;

    if (lost) say(`${head} Pipeline stopped: ${lost}.`);
    else if (complete) say(`${head} All five phases complete. Ready to convert to a deal.`);
    else say(`${head} Now on phase ${phase}: ${PHASE_TITLES[phase - 1]}.`);
    // Deliberately keyed on the nonce alone: the other values are read at the
    // moment of the transition and must not re-fire the announcement on their own.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nonce]);

  // Dropping out / reopening, which can happen with no phase completing at all.
  useEffect(() => {
    const prev = previousLost.current;
    previousLost.current = lost;
    if (prev === undefined) return;      // first render establishes the baseline
    if (prev === lost) return;
    if (lost) say(`Pipeline stopped: ${lost}.`);
    else say('Pipeline reopened.');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lost]);

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  return message;
}
