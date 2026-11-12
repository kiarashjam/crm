// The English catalogue: the source every other language translates, key for key.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';
import { signing } from './signing';

export const sections = { common, publicPages, messages, signing };

export const en = { ...common, ...publicPages, ...messages, ...signing };
