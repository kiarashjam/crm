// The English catalogue: the source every other language translates, key for key.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';

export const sections = { common, publicPages, messages };

export const en = { ...common, ...publicPages, ...messages };
