// The English catalogue: the source every other language translates, key for key.
import { common } from './common';
import { publicPages } from './publicPages';

export const sections = { common, publicPages };

export const en = { ...common, ...publicPages };
