// The English catalogue: the source every other language translates, key for key.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';
import { signing } from './signing';
import { settings } from './settings';

export const sections = { common, publicPages, messages, signing, settings };

export const en = { ...common, ...publicPages, ...messages, ...signing, ...settings };
