// Le catalogue français (Suisse romande), traduit clé par clé depuis l’anglais.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';
import { signing } from './signing';

export const sections = { common, publicPages, messages, signing };

export const fr = { ...common, ...publicPages, ...messages, ...signing };
