// Le catalogue français (Suisse romande), traduit clé par clé depuis l’anglais.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';
import { signing } from './signing';
import { settings } from './settings';
import { nav } from './nav';
import { members } from './members';

export const sections = { common, publicPages, messages, signing, settings, nav, members };

export const fr = { ...common, ...publicPages, ...messages, ...signing, ...settings, ...nav, ...members };
