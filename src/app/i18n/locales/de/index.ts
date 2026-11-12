// Der deutsche Katalog (Schweizer Schreibweise), Schlüssel für Schlüssel aus dem englischen übersetzt.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';

export const sections = { common, publicPages, messages };

export const de = { ...common, ...publicPages, ...messages };
