// Le catalogue français (Suisse romande), traduit clé par clé depuis l’anglais.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';
import { signing } from './signing';
import { settings } from './settings';

export const sections = { common, publicPages, messages, signing, settings };

export const fr = { ...common, ...publicPages, ...messages, ...signing, ...settings };
