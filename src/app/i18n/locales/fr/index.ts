// Le catalogue français (Suisse romande), traduit clé par clé depuis l’anglais.
import { common } from './common';
import { publicPages } from './publicPages';

export const sections = { common, publicPages };

export const fr = { ...common, ...publicPages };
