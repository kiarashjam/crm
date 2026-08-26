// Reading text off a lead without trusting it to be there.
//
// `Lead.name` and `Lead.email` are declared required, and the Leads page took
// them at their word: `l.name.toLowerCase()` in the search filter and
// `lead.name.split(' ')` for the avatar initials. But that type is a compile-time
// claim about data the SERVER controls, and production produced records where it
// does not hold — the page threw
//
//     Uncaught TypeError: Cannot read properties of undefined (reading 'toLowerCase')
//
// during an API outage, which is exactly when a page most needs to stay standing.
// Whether the nulls come from the server or from the localStorage fallback the API
// client degrades to, the answer is the same: a search box must never be able to
// crash the list it is filtering.
//
// So these two functions are the only way the page reads that text, and they are
// total — every input produces a string, never a throw.

import type { Lead } from '@/app/api/types';

/** Whatever this field holds, as a trimmed string. Never throws. */
function text(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

/**
 * Whether a lead matches a free-text query, across name, email and phone.
 *
 * An empty or whitespace-only query matches everything, so clearing the box
 * restores the full list rather than emptying it.
 */
export function matchesLeadSearch(lead: Partial<Lead>, query: string): boolean {
  const q = text(query).toLowerCase();
  if (q.length === 0) return true;
  return text(lead.name).toLowerCase().includes(q)
    || text(lead.email).toLowerCase().includes(q)
    // Phone is matched unlowered: it has no case, and lowering it would only
    // hide the fact that the query was never letters to begin with.
    || text(lead.phone).includes(q);
}

/**
 * Up to two initials for the avatar.
 *
 * Takes the first and last word rather than the first two, so "Jean Michel
 * Dupont" gives JD — the surname is the half people recognise. Returns "?" when
 * there is no name at all, because an avatar with nothing in it reads as a
 * rendering bug rather than as missing data.
 */
export function leadInitials(name: unknown): string {
  const words = text(name).split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  const first = words[0]![0]!;
  const last = words.length > 1 ? words[words.length - 1]![0]! : (words[0]![1] ?? '');
  return (first + last).toUpperCase();
}
