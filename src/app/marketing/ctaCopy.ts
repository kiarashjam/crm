// The words around the public site's calls to action: the demo button, the reassurance line
// under the hero's buttons, and the bar that keeps "Start free" in reach on a phone. Every
// item is true: signing up asks for a name, an email address and a password, then a
// workspace name (no card); the demo runs in the browser with sample data and no account.

import { withFrenchSpacing } from './content/typography';
import type { SiteLang } from './site';

export interface CtaCopy {
  /** The button that opens the demo workspace, no account needed. */
  tryDemo: string;
  /** Under the hero's buttons while the launch offer runs. */
  reassurance: string[];
  /** The phone bar's line beside "Start free" while the offer runs. */
  barNote: string;
  /** The phone bar's line once the offer has ended. */
  barNoteAfterOffer: string;
}

export const CTA_COPY: Record<SiteLang, CtaCopy> = {
  en: {
    tryDemo: 'Try the demo, no sign-up',
    reassurance: ['No credit card', 'Every feature included', 'Ready in minutes', 'English, French, German'],
    barNote: 'Free until 31 Dec 2026 · No card',
    barNoteAfterOffer: 'Swiss CRM for clubs and sales',
  },
  fr: withFrenchSpacing({
    tryDemo: 'Essayer la démo sans compte',
    reassurance: ['Sans carte de crédit', 'Toutes les fonctionnalités', 'Prêt en quelques minutes', 'Français, allemand, anglais'],
    barNote: 'Gratuit jusqu’au 31.12.2026 · Sans carte',
    barNoteAfterOffer: 'Le CRM suisse des clubs et des ventes',
  }),
  de: {
    tryDemo: 'Demo testen, ohne Anmeldung',
    reassurance: ['Keine Kreditkarte', 'Alle Funktionen inklusive', 'In wenigen Minuten startklar', 'Deutsch, Französisch, Englisch'],
    barNote: 'Kostenlos bis 31.12.2026 · Ohne Karte',
    barNoteAfterOffer: 'Schweizer CRM für Clubs und Vertrieb',
  },
};
