// Der deutsche Katalog (Schweizer Schreibweise), Schlüssel für Schlüssel aus dem englischen übersetzt.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';
import { signing } from './signing';

export const sections = { common, publicPages, messages, signing };

export const de = { ...common, ...publicPages, ...messages, ...signing };
