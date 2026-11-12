// Der deutsche Katalog (Schweizer Schreibweise), Schlüssel für Schlüssel aus dem englischen übersetzt.
import { common } from './common';
import { publicPages } from './publicPages';

export const sections = { common, publicPages };

export const de = { ...common, ...publicPages };
