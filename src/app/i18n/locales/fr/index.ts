// Le catalogue français (Suisse romande), traduit clé par clé depuis l’anglais.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';

export const sections = { common, publicPages, messages };

export const fr = { ...common, ...publicPages, ...messages };
