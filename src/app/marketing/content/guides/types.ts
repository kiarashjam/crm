// The shape of a guide: a long-form article on one question people search for ("how to run a
// private members' club", "Swiss QR-bill for clubs"), useful on its own, with CadenceE where it
// truly helps. GuidePage lays it out; each language has its own file per guide
// (guides/<lang>/<id>.ts), and its title and description are in guides/meta/<id>.ts.
//
// In any paragraph, bullet, step or answer, "[label](page:<id>)" links to a page of the site in
// the same language (a topic page or another guide) and "[label](/login?demo=1)" to an address.

import type { FaqItem } from '../types';

export interface GuideSubsection {
  heading: string;
  paragraphs: string[];
  bullets?: string[];
}

export interface GuideSection {
  /** The section's anchor in the page (#…): lower-case words joined by hyphens, unique in the guide. */
  id: string;
  /** Its H2. */
  heading: string;
  paragraphs: string[];
  bullets?: string[];
  /** Numbered steps, when the section is a procedure. */
  steps?: { title: string; text: string }[];
  /** One practical tip, set apart. */
  tip?: string;
  /** H3s under the H2. */
  subsections?: GuideSubsection[];
}

export interface GuideContent {
  /** The small label above the H1 ("Club management"). */
  eyebrow: string;
  /** The guide's one H1: the question or promise, with its main keyword. */
  h1: string;
  /** The paragraph under the H1: what the reader will know by the end. */
  lead: string;
  /** Minutes to read, rounded (about 200 words a minute). */
  readingMinutes: number;
  /** Three to six one-line answers, shown in a box at the top: the guide in brief. */
  keyTakeaways: string[];
  sections: GuideSection[];
  faq: { heading: string; items: FaqItem[] };
  /** Where the guide meets CadenceE: shown in a box partway through and at the end. */
  cta: { heading: string; text: string };
}
